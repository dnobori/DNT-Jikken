using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using static System.Console;
using Newtonsoft.Json;
using SoftEther.WebSocket.Helper;
using System.Security.Cryptography;

#pragma warning disable CS0162, CS1998

namespace SoftEther.WebSocket
{
    public enum WebSocketOpcode
    {
        Continue = 0x00,
        Text = 0x01,
        Bin = 0x02,
        Close = 0x08,
        Ping = 0x09,
        Pong = 0x0A,
    }

    class WebSocketStream : Stream
    {
        public string UserAgent { get; set; } = "Mozilla/5.0 (WebSocket) WebSocket Client";
        public int TimeoutOpen { get; set; } = 10 * 1000;
        public int TimeoutComm { get; set; } = 10 * 1000;

        CancelWatcher cancelWatcher;

        public CancellationToken Cancel { get; }
        public bool Opened { get; private set; } = false;
        public bool HasError { get; private set; } = false;
        public int MaxPayloadLenPerFrame { get; set; } = (8 * 1024 * 1024);
        public int SendSingleFragmentSize { get; set; } = (32 * 1024);

        public int MaxBufferSize { get; set; } = (1600 * 1600);

        Fifo PhysicalSendFifo = new Fifo();
        Fifo PhysicalRecvFifo = new Fifo();
        Fifo AppSendFifo = new Fifo();
        Fifo AppRecvFifo = new Fifo();

        Stream st;

        public WebSocketStream(Stream innerStream, CancellationToken cancel = default(CancellationToken))
        {
            this.st = innerStream;

            cancelWatcher = new CancelWatcher(cancel);
            this.Cancel = cancelWatcher.CancelToken;
        }

        public async Task OpenAsync(string uri)
        {
            if (Opened)
            {
                throw new ApplicationException("WebSocket is already opened.");
            }

            Uri u = new Uri(uri);
            HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, uri);

            byte[] nonce = new byte[16];
            new Random().NextBytes(nonce);
            string requestKey = Convert.ToBase64String(nonce);

            req.Headers.Add("Host", u.Host);
            req.Headers.Add("User-Agent", UserAgent);
            req.Headers.Add("Accept", "text/html");
            req.Headers.Add("Sec-WebSocket-Version", "13");
            req.Headers.Add("Origin", "null");
            req.Headers.Add("Sec-WebSocket-Key", requestKey);
            req.Headers.Add("Connection", "keep-alive, Upgrade");
            req.Headers.Add("Pragma", "no-cache");
            req.Headers.Add("Cache-Control", "no-cache");
            req.Headers.Add("Upgrade", "websocket");

            StringWriter tmpWriter = new StringWriter();
            tmpWriter.WriteLine($"{req.Method} {req.RequestUri.PathAndQuery} HTTP/1.1");
            tmpWriter.WriteLine(req.Headers.ToString());

            await st.WriteAsyncWithTimeout(tmpWriter.AsciiToByteArray(),
                timeout: this.TimeoutOpen,
                cancel: this.Cancel);

            Dictionary<string, string> headers = new Dictionary<string, string>();
            int num = 0;
            int responseCode = 0;

            StreamReader tmpReader = new StreamReader(st);
            while (true)
            {
                string line = await WebSocketHelper.DoAsyncWithTimeout((procCancel) => tmpReader.ReadLineAsync(),
                    timeout: this.TimeoutOpen,
                    cancel: this.Cancel);
                if (line == "")
                {
                    break;
                }

                if (num == 0)
                {
                    string[] tokens = line.Split(' ');
                    if (tokens[0] != "HTTP/1.1") throw new ApplicationException($"Cannot establish the WebSocket Protocol. Response: \"{tokens}\"");
                    responseCode = int.Parse(tokens[1]);
                }
                else
                {
                    string[] tokens = line.Split(':');
                    string name = tokens[0].Trim();
                    string value = tokens[1].Trim();
                    headers[name] = value;
                }

                num++;
            }

            if (responseCode != 101)
            {
                throw new ApplicationException($"Cannot establish the WebSocket Protocol. Perhaps the destination host does not support WebSocket. Wrong response code: \"{responseCode}\"");
            }

            if (headers["Upgrade"].Equals("websocket", StringComparison.InvariantCultureIgnoreCase) == false)
            {
                throw new ApplicationException($"Wrong Upgrade header: \"{headers["Upgrade"]}\"");
            }

            string acceptKey = headers["Sec-WebSocket-Accept"];
            string keyCalcStr = requestKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            SHA1 sha1 = new SHA1Managed();
            string acceptKey2 = Convert.ToBase64String(sha1.ComputeHash(keyCalcStr.AsciiToByteArray()));

            if (acceptKey != acceptKey2)
            {
                throw new ApplicationException($"Wrong accept_key: \'{acceptKey}\'");
            }

            Opened = true;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override bool CanTimeout => true;

        public override int ReadTimeout { get => this.TimeoutComm; set => this.TimeoutComm = value; }
        public override int WriteTimeout { get => this.TimeoutComm; set => this.TimeoutComm = value; }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.ReadAsync(buffer, offset, count).Result;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.WriteAsync(buffer, offset, count).Wait();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (count == 0 && this.HasError == false) return 0;
            ThrowIfDisconnected();

            while (true)
            {
                while (true)
                {
                    ThrowIfDisconnected();

                    Frame f = TryRecvNextFrame(out int readBufferSize);
                    if (f == null) break; // No more frames

                    if (f.Opcode == WebSocketOpcode.Continue || f.Opcode == WebSocketOpcode.Text || f.Opcode == WebSocketOpcode.Bin)
                    {
                        this.AppRecvFifo.Write(f.Data);
                    }
                    else if (f.Opcode == WebSocketOpcode.Ping)
                    {
                        // todo
                    }
                    else if (f.Opcode == WebSocketOpcode.Pong)
                    {
                        // todo
                    }
                    else
                    {
                        // Error: disconnect
                        this.HasError = true;
                    }

                    this.PhysicalRecvFifo.SkipRead(readBufferSize);
                }

                ThrowIfDisconnected();

                int sz = this.AppRecvFifo.Size;
                if (sz >= 1)
                {
                    if (sz > count) sz = count;
                    this.AppRecvFifo.Read(buffer, offset, sz);
                    return sz;
                }

                try
                {
                    byte[] tmpBuffer = new byte[65536];

                    int recvSize = await this.st.ReadAsyncWithTimeout(tmpBuffer,
                        0, tmpBuffer.Length,
                        timeout: TimeoutComm,
                        cancel: Cancel,
                        cancelTokens: cancellationToken);

                    this.PhysicalRecvFifo.Write(tmpBuffer, 0, recvSize);
                }
                catch
                {
                    this.HasError = true;
                }
            }
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (count == 0 && this.HasError == false) return;
            ThrowIfDisconnected();

            while (this.PhysicalSendFifo.Size > this.MaxBufferSize)
            {
                await Task.Delay(1, cancellationToken);
            }

            this.AppSendFifo.Write(buffer, offset, count);

            // App -> Physical
            while (true)
            {
                ThrowIfDisconnected();

                int size = AppSendFifo.Size;
                if (size == 0) break; // No more data

                size = Math.Min(size, SendSingleFragmentSize);

                TrySendFrame(WebSocketOpcode.Bin, AppSendFifo.Data, AppSendFifo.DataOffset, size);

                AppSendFifo.SkipRead(size);
            }

            ThrowIfDisconnected();

            try
            {
                await this.st.WriteAsyncWithTimeout(this.PhysicalSendFifo.Read(),
                    timeout: TimeoutComm,
                    cancel: Cancel,
                    cancelTokens: cancellationToken);
            }
            catch
            {
                this.HasError = true;
            }
        }
        Frame TryRecvNextFrame(out int readBufferSize)
        {
            readBufferSize = 0;

            try
            {
                ThrowIfDisconnected();

                var buf = this.PhysicalRecvFifo.Span;
                var bufPos0 = this.PhysicalRecvFifo.Span;

                if (buf.Length <= 2) return null;

                byte flagAndOpcode = buf.WalkReadUInt8();
                byte maskAndPayloadLen = buf.WalkReadUInt8();
                int maskFlag = maskAndPayloadLen & 0x80;
                int payloadLen = maskAndPayloadLen & 0x7F;
                if (payloadLen == 126)
                {
                    payloadLen = buf.WalkReadUInt16();
                }
                else if (payloadLen == 127)
                {
                    ulong u64 = buf.WalkReadUInt64();
                    if (u64 >= int.MaxValue)
                    {
                        this.HasError = true;
                        return null;
                    }
                    payloadLen = (int)u64;
                }

                if (payloadLen > MaxPayloadLenPerFrame)
                {
                    this.HasError = true;
                    return null;
                }

                var maskKey = Span<byte>.Empty;
                if (maskFlag != 0)
                {
                    maskKey = buf.WalkRead(4);
                }

                Frame f = new Frame()
                {
                    Data = buf.WalkRead(payloadLen).ToArray(),
                    Opcode = (WebSocketOpcode)(flagAndOpcode & 0xF),
                };

                if (maskFlag != 0)
                {
                    for (int i = 0; i < f.Data.Length; i++)
                    {
                        f.Data[i] ^= maskKey[i % 4];
                    }
                }

                readBufferSize = bufPos0.Length - buf.Length;

                return f;
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        void TrySendFrame(WebSocketOpcode opcode, byte[] data, int pos, int size)
        {
            ThrowIfDisconnected();

            bool useMask = true;
            byte[] mask = null;
            if (useMask) mask = WebSocketHelper.Rand(4);

            Buf b = new Buf();

            b.WriteByte((byte)((uint)0x80 | ((uint)opcode & 0x0f)));

            if (size < 125)
                b.WriteByte((byte)(size | (useMask ? 0x80 : 0x00)));
            else if (size <= 65536)
            {
                b.WriteByte((byte)(126 | (useMask ? 0x80 : 0x00)));
                b.WriteShort((ushort)size);
            }
            else
            {
                b.WriteByte((byte)(127 | (useMask ? 0x80 : 0x00)));
                b.WriteInt64((ulong)size);
            }

            if (useMask)
            {
                b.Write(mask);

                data = WebSocketHelper.CopyByte(data);

                for (int i = 0; i < data.Length; i++)
                {
                    data[i] ^= mask[i % 4];
                }
            }
            if (size < 0)
            {
                WriteLine();
            }
            b.Write(data, pos, size);


            PhysicalSendFifo.Write(b);
        }

        void ThrowIfDisconnected()
        {
            if (this.Cancel.IsCancellationRequested) this.HasError = true;
            if (this.HasError) throw new ApplicationException("WebSocket is disconnected.");
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            => ReadAsync(buffer, offset, count, CancellationToken.None).AsApm(callback, state);
        public override int EndRead(IAsyncResult asyncResult) => ((Task<int>)asyncResult).Result;

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            => WriteAsync(buffer, offset, count, CancellationToken.None).AsApm(callback, state);
        public override void EndWrite(IAsyncResult asyncResult) => ((Task)asyncResult).Wait();

        Once DisposeFlag;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (DisposeFlag.IsFirstCall())
                {
                    this.cancelWatcher.DisposeSafe();
                    this.st.DisposeSafe();
                }
            }

            base.Dispose(disposing);
        }

        class Frame
        {
            public WebSocketOpcode Opcode { get; set; }
            public byte[] Data { get; set; }
        }
    }
}


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

    public class WebSocketStream : Stream
    {
        public string UserAgent { get; set; } = "Mozilla/5.0 (WebSocket) WebSocket Client";
        public int TimeoutOpen { get; set; } = 10 * 1000;
        public int TimeoutComm { get; set; } = 10 * 1000;

        CancelWatcher cancel_watcher;

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

        public WebSocketStream(Stream inner_stream, CancellationToken cancel = default(CancellationToken))
        {
            this.st = inner_stream;

            cancel_watcher = new CancelWatcher(cancel);
            this.Cancel = cancel_watcher.CancelToken;
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
            string request_key = Convert.ToBase64String(nonce);

            req.Headers.Add("Host", u.Host);
            req.Headers.Add("User-Agent", UserAgent);
            req.Headers.Add("Accept", "text/html");
            req.Headers.Add("Sec-WebSocket-Version", "13");
            req.Headers.Add("Origin", "null");
            req.Headers.Add("Sec-WebSocket-Key", request_key);
            req.Headers.Add("Connection", "keep-alive, Upgrade");
            req.Headers.Add("Pragma", "no-cache");
            req.Headers.Add("Cache-Control", "no-cache");
            req.Headers.Add("Upgrade", "websocket");

            StringWriter tmp_writer = new StringWriter();
            tmp_writer.WriteLine($"{req.Method} {req.RequestUri.PathAndQuery} HTTP/1.1");
            tmp_writer.WriteLine(req.Headers.ToString());

            await st.WriteAsyncWithTimeout(tmp_writer.AsciiToByteArray(),
                timeout: this.TimeoutOpen,
                cancel: this.Cancel);

            Dictionary<string, string> headers = new Dictionary<string, string>();
            int num = 0;
            int response_code = 0;

            StreamReader tmp_reader = new StreamReader(st);
            while (true)
            {
                string line = await WebSocketHelper.DoAsyncWithTimeout<string>((proc_cancel) => tmp_reader.ReadLineAsync(),
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
                    response_code = int.Parse(tokens[1]);
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

            if (response_code != 101)
            {
                throw new ApplicationException($"Cannot establish the WebSocket Protocol. Perhaps the destination host does not support WebSocket. Wrong response code: \"{response_code}\"");
            }

            if (headers["Upgrade"].Equals("websocket", StringComparison.InvariantCultureIgnoreCase) == false)
            {
                throw new ApplicationException($"Wrong Upgrade header: \"{headers["Upgrade"]}\"");
            }

            string accept_key = headers["Sec-WebSocket-Accept"];
            string key_calc_str = request_key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            SHA1 sha1 = new SHA1Managed();
            string accept_key_2 = Convert.ToBase64String(sha1.ComputeHash(key_calc_str.AsciiToByteArray()));

            if (accept_key != accept_key_2)
            {
                throw new ApplicationException($"Wrong accept_key: \'{accept_key}\'");
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
            throw_if_disconnected();

            while (true)
            {
                while (true)
                {
                    throw_if_disconnected();

                    Frame f = try_recv_next_frame(out int read_buffer_size);
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

                    this.PhysicalRecvFifo.SkipRead(read_buffer_size);
                }

                throw_if_disconnected();

                int sz = this.AppRecvFifo.Size;
                if (sz >= 1)
                {
                    if (sz > count) sz = count;
                    this.AppRecvFifo.Read(buffer, offset, sz);
                    return sz;
                }

                try
                {
                    byte[] tmp_buffer = new byte[65536];

                    int recv_size = await this.st.ReadAsyncWithTimeout(tmp_buffer,
                        0, tmp_buffer.Length,
                        timeout: TimeoutComm,
                        cancel: Cancel,
                        cancel_tokens: cancellationToken);

                    this.PhysicalRecvFifo.Write(tmp_buffer, 0, recv_size);
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
            throw_if_disconnected();

            while (this.PhysicalSendFifo.Size > this.MaxBufferSize)
            {
                await Task.Delay(1, cancellationToken);
            }

            this.AppSendFifo.Write(buffer, offset, count);

            // App -> Physical
            while (true)
            {
                throw_if_disconnected();

                int size = AppSendFifo.Size;
                if (size == 0) break; // No more data

                size = Math.Min(size, SendSingleFragmentSize);

                try_send_frame(WebSocketOpcode.Bin, AppSendFifo.Data, AppSendFifo.DataOffset, size);

                AppSendFifo.SkipRead(size);
            }

            throw_if_disconnected();

            try
            {
                await this.st.WriteAsyncWithTimeout(this.PhysicalSendFifo.Read(),
                    timeout: TimeoutComm,
                    cancel: Cancel,
                    cancel_tokens: cancellationToken);
            }
            catch
            {
                this.HasError = true;
            }
        }
        Frame try_recv_next_frame(out int read_buffer_size)
        {
            read_buffer_size = 0;

            try
            {
                throw_if_disconnected();

                var buf = this.PhysicalRecvFifo.Span;
                var buf_pos0 = this.PhysicalRecvFifo.Span;

                if (buf.Length <= 2) return null;

                byte flag_and_opcode = buf.ReadByte();
                byte mask_and_payload_len = buf.ReadByte();
                int mask_flag = mask_and_payload_len & 0x80;
                int payload_len = mask_and_payload_len & 0x7F;
                if (payload_len == 126)
                {
                    payload_len = buf.ReadShort();
                }
                else if (payload_len == 127)
                {
                    ulong u64 = buf.ReadInt64();
                    if (u64 >= int.MaxValue)
                    {
                        this.HasError = true;
                        return null;
                    }
                    payload_len = (int)u64;
                }

                if (payload_len > MaxPayloadLenPerFrame)
                {
                    this.HasError = true;
                    return null;
                }

                var mask_key = Span<byte>.Empty;
                if (mask_flag != 0)
                {
                    mask_key = buf.Read(4);
                }

                Frame f = new Frame()
                {
                    Data = buf.Read(payload_len).ToArray(),
                    Opcode = (WebSocketOpcode)(flag_and_opcode & 0xF),
                };

                if (mask_flag != 0)
                {
                    for (int i = 0; i < f.Data.Length; i++)
                    {
                        f.Data[i] ^= mask_key[i % 4];
                    }
                }

                read_buffer_size = buf_pos0.Length - buf.Length;

                return f;
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        void try_send_frame(WebSocketOpcode opcode, byte[] data, int pos, int size)
        {
            throw_if_disconnected();

            bool use_mask = true;
            byte[] mask = null;
            if (use_mask) mask = WebSocketHelper.GenRandom(4);

            Buf b = new Buf();

            b.WriteByte((byte)((uint)0x80 | ((uint)opcode & 0x0f)));

            if (size < 125)
                b.WriteByte((byte)(size | (use_mask ? 0x80 : 0x00)));
            else if (size <= 65536)
            {
                b.WriteByte((byte)(126 | (use_mask ? 0x80 : 0x00)));
                b.WriteShort((ushort)size);
            }
            else
            {
                b.WriteByte((byte)(127 | (use_mask ? 0x80 : 0x00)));
                b.WriteInt64((ulong)size);
            }

            if (use_mask)
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

        void throw_if_disconnected()
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

        Once dispose_flag;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (dispose_flag.IsFirstCall())
                {
                    this.cancel_watcher.DisposeSafe();
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


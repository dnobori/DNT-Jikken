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
        public int TimeoutDataRecv { get; set; } = 10 * 1000;

        public CancellationToken Cancel { get; }
        public bool Opened { get; private set; } = false;
        public bool HasError { get; private set; } = false;
        public int MaxPayloadLenPerFrame { get; set; } = (8 * 1024 * 1024);
        public int SendSingleFragmentSize { get; set; } = (32 * 1024);

        Fifo PhysicalSendFifo = new Fifo();
        Fifo PhysicalRecvFifo = new Fifo();
        Fifo AppSendFifo = new Fifo();
        Fifo AppRecvFifo = new Fifo();

        Stream st;

        public WebSocketStream(Stream inner_stream, CancellationToken cancel = default(CancellationToken))
        {
            this.st = inner_stream;
            this.Cancel = cancel;
        }

        public async Task Open(string uri)
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
                string line = await WebSocketHelper.DoAsyncWithTimeout<string>(() => tmp_reader.ReadLineAsync(),
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

        public override bool CanRead => throw new NotImplementedException();

        public override bool CanSeek => throw new NotImplementedException();

        public override bool CanWrite => throw new NotImplementedException();

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

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
            await Task.CompletedTask;
            return 0;
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (count == 0 && this.HasError == false) return;
            throw_if_disconnected();

            this.AppSendFifo.Write(buffer, offset, count);

            try_sync();

            throw_if_disconnected();

            try
            {
                await this.st.WriteAsyncWithTimeout(this.PhysicalSendFifo.Read(),
                    timeout: TimeoutDataRecv,
                    cancel: Cancel);
            }
            catch
            {
                this.HasError = true;
            }
        }

        void try_sync()
        {
            // Physical -> App
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

            // App -> Physical
            while (true)
            {
                throw_if_disconnected();

                int size = AppSendFifo.Size;
                if (size == 0) break; // No more data

                size = Math.Min(size, SendSingleFragmentSize);

                try_send_frame(WebSocketOpcode.Bin, AppSendFifo.Data, AppSendFifo.DataOffset, size);
            }

            throw_if_disconnected();
        }

        Frame try_recv_next_frame(out int read_buffer_size)
        {
            read_buffer_size = 0;

            try
            {
                throw_if_disconnected();

                var buf = this.PhysicalRecvFifo.Span;
                var buf_pos0 = buf;

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

            Buf b = new Buf();

            b.WriteByte((byte)((uint)0x80 | ((uint)opcode & 0x0f)));

            if (size < 125)
                b.WriteByte((byte)size);
            else if (size <= 65536)
            {
                b.WriteByte(126);
                b.WriteShort((ushort)size);
            }
            else
            {
                b.WriteByte(127);
                b.WriteInt64((ulong)size);
            }

            b.Write(data, pos, size);

            PhysicalSendFifo.Write(b);
        }

        void throw_if_disconnected()
        {
            if (this.Cancel.IsCancellationRequested) this.HasError = true;
            if (this.HasError) throw new ApplicationException("WebSocket is disconnected.");
        }

        class Frame
        {
            public WebSocketOpcode Opcode { get; set; }
            public byte[] Data { get; set; }
        }
    }
}


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
using SoftEther.WebSocket;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using System.Diagnostics;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using System.Runtime.InteropServices;

#pragma warning disable CS0162, CS1998

namespace SoftEther.WebSocket.Helper
{
    // Buffer
    public class Buf
    {
        MemoryStream buf;

        public Buf()
        {
            init(new byte[0]);
        }
        public Buf(byte[] data)
        {
            init(data);
        }
        void init(byte[] data)
        {
            buf = new MemoryStream();
            Write(data);
            SeekToBegin();
        }

        public void Clear()
        {
            buf.SetLength(0);
        }

        public void WriteByte(byte data)
        {
            buf.WriteByte(data);
        }
        public void Write(byte[] data)
        {
            buf.Write(data, 0, data.Length);
        }
        public void Write(byte[] data, int pos, int len)
        {
            buf.Write(data, pos, len);
        }

        public uint Size
        {
            get
            {
                return (uint)buf.Length;
            }
        }

        public uint Pos
        {
            get
            {
                return (uint)buf.Position;
            }
        }

        public byte[] ByteData
        {
            get
            {
                return buf.ToArray();
            }
        }

        public byte this[uint i]
        {
            get
            {
                return buf.GetBuffer()[i];
            }

            set
            {
                buf.GetBuffer()[i] = value;
            }
        }

        public byte[] Read()
        {
            return Read(this.Size);
        }
        public byte[] Read(uint size)
        {
            byte[] tmp = new byte[size];
            int i = buf.Read(tmp, 0, (int)size);

            byte[] ret = new byte[i];
            Array.Copy(tmp, 0, ret, 0, i);

            return ret;
        }
        public byte ReadByte()
        {
            byte[] a = Read(1);

            return a[0];
        }

        public void SeekToBegin()
        {
            Seek(0);
        }
        public void SeekToEnd()
        {
            Seek(0, SeekOrigin.End);
        }
        public void Seek(uint offset)
        {
            Seek(offset, SeekOrigin.Begin);
        }
        public void Seek(uint offset, SeekOrigin mode)
        {
            buf.Seek(offset, mode);
        }

        public ushort ReadShort()
        {
            byte[] data = Read(2);
            if (data.Length != 2)
            {
                return 0;
            }
            if (WebSocketHelper.IsLittleEndian)
            {
                Array.Reverse(data);
            }
            return BitConverter.ToUInt16(data, 0);
        }
        public ushort RawReadShort()
        {
            byte[] data = Read(2);
            if (data.Length != 2)
            {
                return 0;
            }
            return BitConverter.ToUInt16(data, 0);
        }

        public uint ReadInt()
        {
            byte[] data = Read(4);
            if (data.Length != 4)
            {
                return 0;
            }
            if (WebSocketHelper.IsLittleEndian)
            {
                Array.Reverse(data);
            }
            return BitConverter.ToUInt32(data, 0);
        }
        public uint RawReadInt()
        {
            byte[] data = Read(4);
            if (data.Length != 4)
            {
                return 0;
            }
            return BitConverter.ToUInt32(data, 0);
        }

        public ulong ReadInt64()
        {
            byte[] data = Read(8);
            if (data.Length != 8)
            {
                return 0;
            }
            if (WebSocketHelper.IsLittleEndian)
            {
                Array.Reverse(data);
            }
            return BitConverter.ToUInt64(data, 0);
        }
        public ulong RawReadInt64()
        {
            byte[] data = Read(8);
            if (data.Length != 8)
            {
                return 0;
            }
            return BitConverter.ToUInt64(data, 0);
        }

        public void WriteShort(ushort shortValue)
        {
            byte[] data = BitConverter.GetBytes(shortValue);
            if (WebSocketHelper.IsLittleEndian)
            {
                Array.Reverse(data);
            }
            Write(data);
        }
        public void RawWriteShort(ushort shortValue)
        {
            byte[] data = BitConverter.GetBytes(shortValue);
            Write(data);
        }

        public void WriteInt(uint intValue)
        {
            byte[] data = BitConverter.GetBytes(intValue);
            if (WebSocketHelper.IsLittleEndian)
            {
                Array.Reverse(data);
            }
            Write(data);
        }
        public void RawWriteInt(uint intValue)
        {
            byte[] data = BitConverter.GetBytes(intValue);
            Write(data);
        }

        public void WriteInt64(ulong int64Value)
        {
            byte[] data = BitConverter.GetBytes(int64Value);
            if (WebSocketHelper.IsLittleEndian)
            {
                Array.Reverse(data);
            }
            Write(data);
        }
        public void RawWriteInt64(ulong int64Value)
        {
            byte[] data = BitConverter.GetBytes(int64Value);
            Write(data);
        }
        public static Buf ReadFromStream(Stream st)
        {
            Buf ret = new Buf();
            int size = 32767;

            while (true)
            {
                byte[] tmp = new byte[size];
                int i = st.Read(tmp, 0, tmp.Length);

                if (i <= 0)
                {
                    break;
                }

                Array.Resize<byte>(ref tmp, i);

                ret.Write(tmp);
            }

            ret.SeekToBegin();

            return ret;
        }
    }

    // FIFO
    public class Fifo
    {
        byte[] p;
        int pos, size;
        public int Size
        {
            get { return size; }
        }
        public byte[] Data
        {
            get
            {
                return this.p;
            }
        }
        public int DataOffset
        {
            get
            {
                return this.pos;
            }
        }
        public int PhysicalSize
        {
            get
            {
                return p.Length;
            }
        }

        int reallocMemSize;
        public const int FifoInitMemSize = 4096;
        public const int FifoReallocMemSize = 65536;
        public const int FifoReallocMemSizeSmall = 65536;

        long totalWriteSize = 0, totalReadSize = 0;

        public long TotalReadSize
        {
            get { return totalReadSize; }
        }
        public long TotalWriteSize
        {
            get { return totalWriteSize; }
        }

        public Fifo()
        {
            init(0);
        }
        public Fifo(int reallocMemSize)
        {
            init(reallocMemSize);
        }

        void init(int reallocMemSize)
        {
            if (reallocMemSize == 0)
            {
                reallocMemSize = FifoReallocMemSize;
            }

            this.size = this.pos = 0;
            this.reallocMemSize = reallocMemSize;

            this.p = new byte[FifoInitMemSize];
        }

        public void Write(Buf buf)
        {
            Write(buf.ByteData);
        }
        public void Write(byte[] src)
        {
            Write(src, src.Length);
        }
        public void SkipWrite(int size)
        {
            Write(null, size);
        }
        public void Write(byte[] src, int size)
        {
            Write(src, 0, size);
        }
        public void Write(byte[] src, int offset, int size)
        {
            int i, need_size;
            bool realloc_flag;

            i = this.size;
            this.size += size;
            need_size = this.pos + this.size;
            realloc_flag = false;

            int memsize = p.Length;
            while (need_size > memsize)
            {
                memsize = Math.Max(memsize, FifoInitMemSize) * 3;
                realloc_flag = true;
            }

            if (realloc_flag)
            {
                byte[] new_p = new byte[memsize];
                WebSocketHelper.CopyByte(new_p, 0, this.p, 0, this.p.Length);
                this.p = new_p;
            }

            if (src != null)
            {
                WebSocketHelper.CopyByte(this.p, this.pos + i, src, offset, size);
            }

            totalWriteSize += size;
        }

        public byte[] Read()
        {
            return Read(this.Size);
        }
        public void ReadToBuf(Buf buf, int size)
        {
            byte[] data = Read(size);

            buf.Write(data);
        }
        public Buf ReadToBuf(int size)
        {
            byte[] data = Read(size);

            return new Buf(data);
        }
        public byte[] Read(int size)
        {
            byte[] ret = new byte[size];
            int read_size = Read(ret);
            Array.Resize<byte>(ref ret, read_size);

            return ret;
        }
        public int Read(byte[] dst)
        {
            return Read(dst, dst.Length);
        }
        public int SkipRead(int size)
        {
            return Read(null, size);
        }
        public int Read(byte[] dst, int size)
        {
            return Read(dst, 0, size);
        }
        public int Read(byte[] dst, int offset, int size)
        {
            int read_size;

            read_size = Math.Min(size, this.size);
            if (read_size == 0)
            {
                return 0;
            }
            if (dst != null)
            {
                WebSocketHelper.CopyByte(dst, offset, this.p, this.pos, size);
            }
            this.pos += read_size;
            this.size -= read_size;

            if (this.size == 0)
            {
                this.pos = 0;
            }

            if (this.pos >= FifoInitMemSize &&
                this.p.Length >= this.reallocMemSize &&
                (this.p.Length / 2) > this.size)
            {
                byte[] new_p;
                int new_size;

                new_size = Math.Max(this.p.Length / 2, FifoInitMemSize);
                new_p = new byte[new_size];
                WebSocketHelper.CopyByte(new_p, 0, this.p, this.pos, this.size);

                this.p = new_p;

                this.pos = 0;
            }

            totalReadSize += read_size;

            return read_size;
        }

        public Span<byte> Span
        {
            get
            {
                return this.Data.AsSpan(this.pos, this.size);
            }
        }
    }

    public class AsyncLock : IDisposable
    {
        public class LockHolder : IDisposable
        {
            AsyncLock parent;
            internal LockHolder(AsyncLock parent)
            {
                this.parent = parent;
            }

            Once dispose_flag;
            public void Dispose()
            {
                if (dispose_flag.IsFirstCall())
                {
                    this.parent.Unlock();
                }
            }
        }

        SemaphoreSlim semaphone = new SemaphoreSlim(1, 1);
        Once dispose_flag;

        public async Task<LockHolder> LockWithAwait()
        {
            await LockAsync();

            return new LockHolder(this);
        }

        public Task LockAsync() => semaphone.WaitAsync();
        public void Unlock() => semaphone.Release();

        public void Dispose()
        {
            if (dispose_flag.IsFirstCall())
            {
                semaphone.DisposeSafe();
                semaphone = null;
            }
        }
    }

    public static class Dbg
    {
        static object LockObj = new object();

        public static long Where(string msg = "", [CallerFilePath] string filename = "", [CallerLineNumber] int line = 0, [CallerMemberName] string caller = null, long last_tick = 0)
        {
            lock (LockObj)
            {
                long now = DateTime.Now.Ticks;
                long diff = now - last_tick;
                WriteLine($"{Path.GetFileName(filename)}:{line} in {caller}()" + (last_tick == 0 ? "" : $" (took {diff} msecs) ") + (string.IsNullOrEmpty(msg) == false ? (": " + msg) : ""));
                return now;
            }
        }
    }

    public static class WebSocketHelper
    {
        public static bool IsLittleEndian { get; }
        public static bool IsBigEndian => !IsLittleEndian;

        static readonly Random random = new Random();

        public const int DefaultMaxDepth = 8;

        public static string NonNull(this string s) { if (s == null) return ""; else return s; }
        public static bool IsEmpty(this string str)
        {
            if (str == null || str.Trim().Length == 0)
                return true;
            else
                return false;
        }
        public static bool IsFilled(this string str) => !IsEmpty(str);

        public static string ObjectToJson(this object obj, bool include_null = false, bool escape_html = false, int? max_depth = DefaultMaxDepth, bool compact = false, bool reference_handling = false) => Serialize(obj, include_null, escape_html, max_depth, compact, reference_handling);
        public static T JsonToObject<T>(this string str, bool include_null = false, int? max_depth = DefaultMaxDepth) => Deserialize<T>(str, include_null, max_depth);
        public static object JsonToObject(this string str, Type type, bool include_null = false, int? max_depth = DefaultMaxDepth) => Deserialize(str, type, include_null, max_depth);

        public static string Serialize(object obj, bool include_null = false, bool escape_html = false, int? max_depth = DefaultMaxDepth, bool compact = false, bool reference_handling = false)
        {
            JsonSerializerSettings setting = new JsonSerializerSettings()
            {
                MaxDepth = max_depth,
                NullValueHandling = include_null ? NullValueHandling.Include : NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Error,
                PreserveReferencesHandling = reference_handling ? PreserveReferencesHandling.All : PreserveReferencesHandling.None,
                StringEscapeHandling = escape_html ? StringEscapeHandling.EscapeHtml : StringEscapeHandling.Default,
            };
            return JsonConvert.SerializeObject(obj, compact ? Formatting.None : Formatting.Indented, setting);
        }

        public static T Deserialize<T>(string str, bool include_null = false, int? max_depth = DefaultMaxDepth)
            => (T)Deserialize(str, typeof(T), include_null, max_depth);

        public static object Deserialize(string str, Type type, bool include_null = false, int? max_depth = DefaultMaxDepth)
        {
            JsonSerializerSettings setting = new JsonSerializerSettings()
            {
                MaxDepth = max_depth,
                NullValueHandling = include_null ? NullValueHandling.Include : NullValueHandling.Ignore,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                ReferenceLoopHandling = ReferenceLoopHandling.Error,
            };
            return JsonConvert.DeserializeObject(str, type, setting);
        }

        public static void Print(this object o)
        {
            string str = o.ObjectToJson();

            if (o is string) str = (string)o;

            Console.WriteLine(str);
        }

        static WebSocketHelper()
        {
            IsLittleEndian = BitConverter.IsLittleEndian;
        }

        public static bool CanUdpSocketErrorBeIgnored(SocketException e)
        {
            switch (e.SocketErrorCode)
            {
                case SocketError.ConnectionReset:
                case SocketError.NetworkReset:
                case SocketError.MessageSize:
                case SocketError.HostUnreachable:
                case SocketError.NetworkUnreachable:
                case SocketError.NoBufferSpaceAvailable:
                case SocketError.AddressNotAvailable:
                case SocketError.ConnectionRefused:
                case SocketError.Interrupted:
                case SocketError.WouldBlock:
                case SocketError.TryAgain:
                case SocketError.InProgress:
                case SocketError.InvalidArgument:
                case (SocketError)12: // ENOMEM
                case (SocketError)10068: // WSAEUSERS
                    return true;
            }
            return false;
        }

        static readonly IPEndPoint udp_ep_ipv4 = new IPEndPoint(IPAddress.Any, 0);
        static readonly IPEndPoint udp_ep_ipv6 = new IPEndPoint(IPAddress.IPv6Any, 0);
        const int udp_max_retry_on_ignore_error = 5;
        public static async Task<SocketReceiveFromResult> ReceiveFromSafeUdpErrorAsync(this Socket socket, ArraySegment<byte> buffer, SocketFlags socketFlags)
        {
            int num_retry = 0;

            LABEL_RETRY:

            try
            {
                Task<SocketReceiveFromResult> t = socket.ReceiveFromAsync(buffer, socketFlags, socket.AddressFamily == AddressFamily.InterNetworkV6 ? udp_ep_ipv6 : udp_ep_ipv4);
                if (t.IsCompleted)
                {
                    return t.Result;
                }
                else
                {
                    num_retry = 0;
                    return await t;
                }
            }
            catch (SocketException e) when (CanUdpSocketErrorBeIgnored(e))
            {
                num_retry++;
                if (num_retry >= udp_max_retry_on_ignore_error)
                {
                    throw;
                }
                goto LABEL_RETRY;
            }
        }

        public static async Task<int> SendToSafeUdpErrorAsync(this Socket socket, ArraySegment<byte> buffer, SocketFlags socketFlags, EndPoint remoteEP)
        {
            int num_retry = 0;

            LABEL_RETRY:

            try
            {
                Task<int> t = socket.SendToAsync(buffer, socketFlags, remoteEP);
                if (t.IsCompleted)
                {
                    return t.Result;
                }
                else
                {
                    num_retry = 0;
                    return await t;
                }
            }
            catch (SocketException e) when (CanUdpSocketErrorBeIgnored(e))
            {
                num_retry++;
                if (num_retry >= udp_max_retry_on_ignore_error)
                {
                    throw;
                }
                goto LABEL_RETRY;
            }
        }

        public static async Task ConnectAsync(this TcpClient tc, string host, int port,
            int timeout = Timeout.Infinite, CancellationToken cancel = default(CancellationToken), params CancellationToken[] cancel_tokens)
        {
            await DoAsyncWithTimeout<int>(
            main_proc: async c =>
            {
                await tc.ConnectAsync(host, port);
                return 0;
            },
            cancel_proc: () =>
            {
                tc.DisposeSafe();
            },
            timeout: timeout,
            cancel: cancel,
            cancel_tokens: cancel_tokens);
        }

        public static void DisposeSafe(this IDisposable obj)
        {
            try
            {
                if (obj != null) obj.Dispose();
            }
            catch { }
        }

        public static byte[] GenRandom(int size)
        {
            lock (random)
            {
                byte[] ret = new byte[size];
                random.NextBytes(ret);
                return ret;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ToShort(this byte[] data)
        {
            data = (byte[])data.Clone();
            if (WebSocketHelper.IsLittleEndian) Array.Reverse(data);
            return BitConverter.ToUInt16(data, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ToInt(this byte[] data)
        {
            data = (byte[])data.Clone();
            if (WebSocketHelper.IsLittleEndian) Array.Reverse(data);
            return BitConverter.ToUInt32(data, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ToInt64(this byte[] data)
        {
            data = (byte[])data.Clone();
            if (WebSocketHelper.IsLittleEndian) Array.Reverse(data);
            return BitConverter.ToUInt64(data, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<byte> Read(ref this Span<byte> span, int size)
        {
            if (size < 0) throw new ArgumentOutOfRangeException();
            if (size == 0) return Span<byte>.Empty;
            Span<byte> ret = span.Slice(0, size);
            span = span.Slice(size);
            return ret;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ReadByte(ref this Span<byte> span)
        {
            byte[] data = span.Read(1).ToArray();
            if (WebSocketHelper.IsLittleEndian) Array.Reverse(data);
            return (byte)data[0];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadShort(ref this Span<byte> span)
        {
            byte[] data = span.Read(2).ToArray();
            if (WebSocketHelper.IsLittleEndian) Array.Reverse(data);
            return BitConverter.ToUInt16(data, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadInt(ref this Span<byte> span)
        {
            byte[] data = span.Read(4).ToArray();
            if (WebSocketHelper.IsLittleEndian) Array.Reverse(data);
            return BitConverter.ToUInt32(data, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ReadInt64(ref this Span<byte> span)
        {
            byte[] data = span.Read(8).ToArray();
            if (WebSocketHelper.IsLittleEndian) Array.Reverse(data);
            return BitConverter.ToUInt64(data, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArraySegment<T> AsSegment<T>(this Memory<T> memory)
        {
            if (MemoryMarshal.TryGetArray(memory, out ArraySegment<T> seg) == false)
            {
                throw new ArgumentException("Memory<T> memory cannot be converted to ArraySegment<T>.");
            }

            return seg;
        }

        public static string ByteToHex(byte[] data)
        {
            return ByteToHex(data, "");
        }
        public static string ByteToHex(byte[] data, string paddingStr)
        {
            StringBuilder ret = new StringBuilder();

            int i;
            for (i = 0; i < data.Length; i++)
            {
                byte b = data[i];

                string s = b.ToString("X");
                if (s.Length == 1)
                {
                    s = "0" + s;
                }

                ret.Append(s);

                if (paddingStr != null)
                {
                    if (i != (data.Length - 1))
                    {
                        ret.Append(paddingStr);
                    }
                }
            }

            return ret.ToString().Trim();
        }

        public static byte[] HexToByte(string str)
        {
            try
            {
                List<byte> o = new List<byte>();
                string tmp = "";
                int i, len;

                str = str.ToUpper().Trim();
                len = str.Length;

                for (i = 0; i < len; i++)
                {
                    char c = str[i];
                    if (('0' <= c && c <= '9') || ('A' <= c && c <= 'F'))
                    {
                        tmp += c;
                        if (tmp.Length == 2)
                        {
                            byte b = Convert.ToByte(tmp, 16);
                            o.Add(b);
                            tmp = "";
                        }
                    }
                    else if (c == ' ' || c == ',' || c == '-' || c == ';')
                    {
                    }
                    else
                    {
                        break;
                    }
                }

                return o.ToArray();
            }
            catch
            {
                return new byte[0];
            }
        }

        public static byte[] CopyByte(byte[] src)
        {
            return (byte[])src.Clone();
        }
        public static byte[] CopyByte(byte[] src, int srcOffset)
        {
            return CopyByte(src, srcOffset, src.Length - srcOffset);
        }
        public static byte[] CopyByte(byte[] src, int srcOffset, int size)
        {
            byte[] ret = new byte[size];
            CopyByte(ret, 0, src, srcOffset, size);
            return ret;
        }
        public static void CopyByte(byte[] dst, byte[] src, int srcOffset, int size)
        {
            CopyByte(dst, 0, src, srcOffset, size);
        }
        public static void CopyByte(byte[] dst, int dstOffset, byte[] src)
        {
            CopyByte(dst, dstOffset, src, 0, src.Length);
        }
        public static void CopyByte(byte[] dst, int dstOffset, byte[] src, int srcOffset, int size)
        {
            Array.Copy(src, srcOffset, dst, dstOffset, size);
        }

        public static byte[] AsciiToByteArray(this object o)
        {
            return Encoding.ASCII.GetBytes(o.ToString());
        }

        public static string ByteArrayToAscii(this byte[] d)
        {
            return Encoding.ASCII.GetString(d);
        }

        public static void TryCloseNonBlock(this Stream stream)
        {
            new Task(() =>
            {
                try
                {
                    stream.Close();
                }
                catch
                {
                }
            }).Start();
        }

        public static Task WhenCanceled(CancellationToken cancel, out CancellationTokenRegistration registration)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            registration = cancel.Register(() =>
            {
                tcs.SetResult(true);
            });

            return tcs.Task;
        }

        public static async Task CancelAsync(this CancellationTokenSource cts, bool throwOnFirstException = false)
        {
            await Task.Run(() => cts.Cancel(throwOnFirstException));
        }

        public static async Task TryCancelAsync(this CancellationTokenSource cts)
        {
            await Task.Run(() => TryCancel(cts));
        }

        public static void TryCancel(this CancellationTokenSource cts)
        {
            try
            {
                cts.Cancel();
            }
            catch
            {
            }
        }

        public static async Task TryWaitAsync(this Task t)
        {
            try
            {
                await t;
            }
            catch (Exception ex)
            {
                WriteLine("Task exception: " + ex.ToString());
            }
        }

        public static void TryWait(this Task t)
        {
            try
            {
                t.Wait();
            }
            catch { }
        }

        public static async Task<byte[]> ReadAsyncWithTimeout(this Stream stream, int max_size = 65536, int? timeout = null, bool? read_all = false, CancellationToken cancel = default(CancellationToken))
        {
            byte[] tmp = new byte[max_size];
            int ret = await stream.ReadAsyncWithTimeout(tmp, 0, tmp.Length, timeout,
                read_all: read_all,
                cancel: cancel);
            return CopyByte(tmp, 0, ret);
        }

        public static async Task<int> ReadAsyncWithTimeout(this Stream stream, byte[] buffer, int offset = 0, int? count = null, int? timeout = null, bool? read_all = false, CancellationToken cancel = default(CancellationToken), params CancellationToken[] cancel_tokens)
        {
            if (timeout == null) timeout = stream.ReadTimeout;
            if (timeout <= 0) timeout = Timeout.Infinite;
            int target_read_size = count ?? (buffer.Length - offset);
            if (target_read_size == 0) return 0;

            try
            {
                int ret = await DoAsyncWithTimeout<int>(async (cancel_for_proc) =>
                {
                    if (read_all == false)
                    {
                        return await stream.ReadAsync(buffer, offset, target_read_size, cancel_for_proc);
                    }
                    else
                    {
                        int current_read_size = 0;

                        while (current_read_size != target_read_size)
                        {
                            int sz = await stream.ReadAsync(buffer, offset + current_read_size, target_read_size - current_read_size, cancel_for_proc);
                            if (sz == 0)
                            {
                                return 0;
                            }

                            current_read_size += sz;
                        }

                        return current_read_size;
                    }
                },
                timeout: (int)timeout,
                cancel: cancel,
                cancel_tokens: cancel_tokens);

                if (ret <= 0)
                {
                    throw new EndOfStreamException("The NetworkStream is disconnected.");
                }

                return ret;
            }
            catch
            {
                stream.TryCloseNonBlock();
                throw;
            }
        }

        public static async Task WriteAsyncWithTimeout(this Stream stream, byte[] buffer, int offset = 0, int? count = null, int? timeout = null, CancellationToken cancel = default(CancellationToken), params CancellationToken[] cancel_tokens)
        {
            if (timeout == null) timeout = stream.WriteTimeout;
            if (timeout <= 0) timeout = Timeout.Infinite;
            int target_write_size = count ?? (buffer.Length - offset);
            if (target_write_size == 0) return;

            try
            {
                await DoAsyncWithTimeout<int>(async (cancel_for_proc) =>
                {
                    await stream.WriteAsync(buffer, offset, target_write_size, cancel_for_proc);
                    return 0;
                },
                timeout: (int)timeout,
                cancel: cancel,
                cancel_tokens: cancel_tokens);

            }
            catch
            {
                stream.TryCloseNonBlock();
                throw;
            }
        }

        public static async Task<TResult> DoAsyncWithTimeout<TResult>(Func<CancellationToken, Task<TResult>> main_proc, Action cancel_proc = null, int timeout = Timeout.Infinite, CancellationToken cancel = default(CancellationToken), params CancellationToken[] cancel_tokens)
        {
            if (timeout < 0) timeout = Timeout.Infinite;
            if (timeout == 0) throw new TimeoutException("timeout == 0");

            List<Task> wait_tasks = new List<Task>();
            List<IDisposable> disposes = new List<IDisposable>();
            Task timeout_task = null;
            CancellationTokenSource timeout_cts = null;
            CancellationTokenSource cancel_for_proc = new CancellationTokenSource();

            if (timeout != Timeout.Infinite)
            {
                timeout_cts = new CancellationTokenSource();
                timeout_task = Task.Delay(timeout, timeout_cts.Token);
                disposes.Add(timeout_cts);

                wait_tasks.Add(timeout_task);
            }

            try
            {
                if (cancel.CanBeCanceled)
                {
                    cancel.ThrowIfCancellationRequested();

                    Task t = WhenCanceled(cancel, out CancellationTokenRegistration reg);
                    disposes.Add(reg);
                    wait_tasks.Add(t);
                }

                foreach (CancellationToken c in cancel_tokens)
                {
                    if (c.CanBeCanceled)
                    {
                        c.ThrowIfCancellationRequested();

                        Task t = WhenCanceled(c, out CancellationTokenRegistration reg);
                        disposes.Add(reg);
                        wait_tasks.Add(t);
                    }
                }

                Task<TResult> proc_task = main_proc(cancel_for_proc.Token);

                if (proc_task.IsCompleted)
                {
                    return proc_task.Result;
                }

                wait_tasks.Add(proc_task);

                await Task.WhenAny(wait_tasks.ToArray());

                foreach (CancellationToken c in cancel_tokens)
                {
                    c.ThrowIfCancellationRequested();
                }

                cancel.ThrowIfCancellationRequested();

                if (proc_task.IsCompleted)
                {
                    return proc_task.Result;
                }

                throw new TimeoutException();
            }
            catch
            {
                try
                {
                    cancel_for_proc.Cancel();
                }
                catch { }
                try
                {
                    if (cancel_proc != null) cancel_proc();
                }
                catch
                {
                }
                throw;
            }
            finally
            {
                if (timeout_cts != null)
                {
                    try
                    {
                        timeout_cts.Cancel();
                    }
                    catch
                    {
                    }
                }
                foreach (IDisposable i in disposes)
                {
                    i.DisposeSafe();
                }
            }
        }

        public static void LaissezFaire(this Task task) { }

        public static IAsyncResult AsApm<T>(this Task<T> task,
                                            AsyncCallback callback,
                                            object state)
        {
            if (task == null)
                throw new ArgumentNullException("task");

            var tcs = new TaskCompletionSource<T>(state);
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    tcs.TrySetException(t.Exception.InnerExceptions);
                else if (t.IsCanceled)
                    tcs.TrySetCanceled();
                else
                    tcs.TrySetResult(t.Result);

                if (callback != null)
                    callback(tcs.Task);
            }, TaskScheduler.Default);
            return tcs.Task;
        }

        public static IAsyncResult AsApm(this Task task,
                                            AsyncCallback callback,
                                            object state)
        {
            if (task == null)
                throw new ArgumentNullException("task");

            var tcs = new TaskCompletionSource<int>(state);
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    tcs.TrySetException(t.Exception.InnerExceptions);
                else if (t.IsCanceled)
                    tcs.TrySetCanceled();
                else
                    tcs.TrySetResult(0);

                if (callback != null)
                    callback(tcs.Task);
            }, TaskScheduler.Default);
            return tcs.Task;
        }



        public static T ClonePublics<T>(this T o)
        {
            byte[] data = ObjectToXml(o);

            return (T)XmlToObject(data, o.GetType());
        }

        public static byte[] ObjectToXml(object o)
        {
            if (o == null)
            {
                return null;
            }
            Type t = o.GetType();

            return ObjectToXml(o, t);
        }
        public static byte[] ObjectToXml(object o, Type t)
        {
            if (o == null)
            {
                return null;
            }

            MemoryStream ms = new MemoryStream();
            XmlSerializer x = new XmlSerializer(t);

            x.Serialize(ms, o);

            return ms.ToArray();
        }

        public static object XmlToObject(string str, Type t)
        {
            byte[] data = Encoding.UTF8.GetBytes(str);

            return XmlToObject(data, t);
        }
        public static object XmlToObject(byte[] data, Type t)
        {
            if (data == null || data.Length == 0)
            {
                return null;
            }

            MemoryStream ms = new MemoryStream();
            ms.Write(data, 0, data.Length);
            ms.Position = 0;

            XmlSerializer x = new XmlSerializer(t);

            return x.Deserialize(ms);
        }

        public static async Task WaitObjectsAsync(Task[] tasks = null, CancellationToken[] cancels = null, AsyncAutoResetEvent[] auto_events = null,
            AsyncManualResetEvent[] manual_events = null, int timeout = Timeout.Infinite)
        {
            if (tasks == null) tasks = new Task[0];
            if (cancels == null) cancels = new CancellationToken[0];
            if (auto_events == null) auto_events = new AsyncAutoResetEvent[0];
            if (manual_events == null) manual_events = new AsyncManualResetEvent[0];
            if (timeout == 0) return;

            List<Task> task_list = new List<Task>();
            List<CancellationTokenRegistration> reg_list = new List<CancellationTokenRegistration>();
            List<Action> undo_list = new List<Action>();

            foreach (Task t in tasks)
                task_list.Add(t);

            foreach (CancellationToken c in cancels)
            {
                task_list.Add(WhenCanceled(c, out CancellationTokenRegistration reg));
                reg_list.Add(reg);
            }

            foreach (AsyncAutoResetEvent ev in auto_events)
            {
                task_list.Add(ev.WaitOneAsync(out Action undo));
                undo_list.Add(undo);
            }

            foreach (AsyncManualResetEvent ev in manual_events)
            {
                task_list.Add(ev.WaitAsync());
            }

            CancellationTokenSource delay_cancel = new CancellationTokenSource();

            if (timeout >= 1)
            {
                task_list.Add(Task.Delay(timeout, delay_cancel.Token));
            }

            try
            {
                await Task.WhenAny(task_list.ToArray());
            }
            catch { }
            finally
            {
                foreach (Action undo in undo_list)
                    undo();

                foreach (CancellationTokenRegistration reg in reg_list)
                {
                    reg.Dispose();
                }

                if (delay_cancel != null)
                {
                    delay_cancel.Cancel();
                    delay_cancel.Dispose();
                }
            }
        }

        public const int AeadChaCha20Poly1305MacSize = 16;
        public const int AeadChaCha20Poly1305NonceSize = 12;
        public const int AeadChaCha20Poly1305KeySize = 32;

        static readonly byte[] zero15 = new byte[15];

        static void crypto_stream_chacha20_ietf(Memory<byte> c, Memory<byte> n, Memory<byte> k)
        {
            var kk = k.AsSegment();
            var nn = n.AsSegment();
            var cc = c.AsSegment();

            if (c.Length == 0) return;
            ChaCha7539Engine ctx = new ChaCha7539Engine();
            ctx.Init(true, new ParametersWithIV(new KeyParameter(kk.Array, kk.Offset, kk.Count), nn.Array, nn.Offset, nn.Count));
            c.Span.Fill(0);
            ctx.ProcessBytes(cc.Array, cc.Offset, cc.Count, cc.Array, cc.Offset);
        }

        static void crypto_stream_chacha20_ietf_xor_ic(Memory<byte> c, Memory<byte> m, Memory<byte> n, uint ic, Memory<byte> k)
        {
            var kk = k.AsSegment();
            var nn = n.AsSegment();
            var cc = c.AsSegment();

            if (c.Length == 0) return;
            ChaCha7539Engine ctx = new ChaCha7539Engine();
            byte[] ic_bytes = BitConverter.GetBytes(ic);
            if (IsLittleEndian == false) Array.Reverse(ic_bytes);
            ctx.Init(true, new ParametersWithIV(new KeyParameter(kk.Array, kk.Offset, kk.Count), nn.Array, nn.Offset, nn.Count));
        }

        static bool crypto_aead_chacha20poly1305_ietf_decrypt_detached(Memory<byte> m, Memory<byte> c, Memory<byte> mac, Memory<byte> ad, Memory<byte> npub, Memory<byte> k)
        {
            var kk = k.AsSegment();
            var nn = npub.AsSegment();
            var cc = c.AsSegment();
            var aa = ad.AsSegment();
            var mm = m.AsSegment();

            byte[] block0 = new byte[64];

            ChaCha7539Engine ctx = new ChaCha7539Engine();
            ctx.Init(true, new ParametersWithIV(new KeyParameter(kk.Array, kk.Offset, kk.Count), nn.Array, nn.Offset, nn.Count));
            ctx.ProcessBytes(block0, 0, block0.Length, block0, 0);

            Poly1305 state = new Poly1305();
            state.Init(new KeyParameter(block0, 0, AeadChaCha20Poly1305KeySize));

            state.BlockUpdate(aa.Array, aa.Offset, aa.Count);
            if ((aa.Count % 16) != 0)
                state.BlockUpdate(zero15, 0, 16 - (aa.Count % 16));

            state.BlockUpdate(cc.Array, cc.Offset, cc.Count);
            if ((cc.Count % 16) != 0)
                state.BlockUpdate(zero15, 0, 16 - (cc.Count % 16));

            byte[] slen = BitConverter.GetBytes((ulong)aa.Count);
            if (IsBigEndian) Array.Reverse(slen);
            state.BlockUpdate(slen, 0, slen.Length);

            byte[] mlen = BitConverter.GetBytes((ulong)cc.Count);
            if (IsBigEndian) Array.Reverse(mlen);
            state.BlockUpdate(mlen, 0, mlen.Length);

            byte[] computed_mac = new byte[AeadChaCha20Poly1305MacSize];
            state.DoFinal(computed_mac, 0);

            if (computed_mac.AsSpan().SequenceEqual(mac.Span) == false)
            {
                return false;
            }

            ctx.ProcessBytes(cc.Array, cc.Offset, cc.Count, mm.Array, mm.Offset);

            return true;
        }

        static void crypto_aead_chacha20poly1305_ietf_encrypt_detached(Memory<byte> c, Memory<byte> mac, Memory<byte> m, Memory<byte> ad, Memory<byte> npub, Memory<byte> k)
        {
            var kk = k.AsSegment();
            var nn = npub.AsSegment();
            var cc = c.AsSegment();
            var aa = ad.AsSegment();
            var mm = m.AsSegment();

            byte[] block0 = new byte[64];

            ChaCha7539Engine ctx = new ChaCha7539Engine();
            ctx.Init(true, new ParametersWithIV(new KeyParameter(kk.Array, kk.Offset, kk.Count), nn.Array, nn.Offset, nn.Count));
            ctx.ProcessBytes(block0, 0, block0.Length, block0, 0);

            Poly1305 state = new Poly1305();
            state.Init(new KeyParameter(block0, 0, AeadChaCha20Poly1305KeySize));

            state.BlockUpdate(aa.Array, aa.Offset, aa.Count);
            if ((aa.Count % 16) != 0)
                state.BlockUpdate(zero15, 0, 16 - (aa.Count % 16));

            ctx.ProcessBytes(mm.Array, mm.Offset, mm.Count, cc.Array, cc.Offset);

            state.BlockUpdate(cc.Array, cc.Offset, cc.Count);
            if ((cc.Count % 16) != 0)
                state.BlockUpdate(zero15, 0, 16 - (cc.Count % 16));

            byte[] slen = BitConverter.GetBytes((ulong)aa.Count);
            if (IsBigEndian) Array.Reverse(slen);
            state.BlockUpdate(slen, 0, slen.Length);

            byte[] mlen = BitConverter.GetBytes((ulong)mm.Count);
            if (IsBigEndian) Array.Reverse(mlen);
            state.BlockUpdate(mlen, 0, mlen.Length);

            var macmac = mac.AsSegment();
            state.DoFinal(macmac.Array, macmac.Offset);
        }

        static void crypto_aead_chacha20poly1305_ietf_encrypt(Memory<byte> c, Memory<byte> m, Memory<byte> ad, Memory<byte> npub, Memory<byte> k)
        {
            crypto_aead_chacha20poly1305_ietf_encrypt_detached(c.Slice(0, c.Length - AeadChaCha20Poly1305MacSize),
                c.Slice(c.Length - AeadChaCha20Poly1305MacSize, AeadChaCha20Poly1305MacSize),
                m, ad, npub, k);
        }

        static bool crypto_aead_chacha20poly1305_ietf_decrypt(Memory<byte> m, Memory<byte> c, Memory<byte> ad, Memory<byte> npub, Memory<byte> k)
        {
            //return crypto_aead_chacha20poly1305_ietf_decrypt_detached(m.Slice(0, c.Length - AeadChaCha20Poly1305MacSize), c.Slice(0, c.Length - AeadChaCha20Poly1305MacSize),
            //    c.Slice(c.Length - AeadChaCha20Poly1305MacSize, AeadChaCha20Poly1305MacSize),
            //    ad, npub, k);
            return crypto_aead_chacha20poly1305_ietf_decrypt_detached(m, c.Slice(0, c.Length - AeadChaCha20Poly1305MacSize),
                c.Slice(c.Length - AeadChaCha20Poly1305MacSize, AeadChaCha20Poly1305MacSize),
                ad, npub, k);
        }

        public static void Aead_ChaCha20Poly1305_Ietf_Encrypt(Memory<byte> dest, Memory<byte> src, Memory<byte> key, Memory<byte> nonce,
            Memory<byte> aad)
        {
            crypto_aead_chacha20poly1305_ietf_encrypt(dest, src, aad, nonce, key);
        }

        public static bool Aead_ChaCha20Poly1305_Ietf_Decrypt(Memory<byte> dest, Memory<byte> src, Memory<byte> key, Memory<byte> nonce,
            Memory<byte> aad)
        {
            return crypto_aead_chacha20poly1305_ietf_decrypt(dest, src, aad, nonce, key);
        }

        public static void Aead_ChaCha20Poly1305_Ietf_Test()
        {
            string nonce_hex = "07 00 00 00 40 41 42 43 44 45 46 47";
            string plaintext_hex =
                "4c 61 64 69 65 73 20 61 6e 64 20 47 65 6e 74 6c " +
                "65 6d 65 6e 20 6f 66 20 74 68 65 20 63 6c 61 73 " +
                "73 20 6f 66 20 27 39 39 3a 20 49 66 20 49 20 63 " +
                "6f 75 6c 64 20 6f 66 66 65 72 20 79 6f 75 20 6f " +
                "6e 6c 79 20 6f 6e 65 20 74 69 70 20 66 6f 72 20 " +
                "74 68 65 20 66 75 74 75 72 65 2c 20 73 75 6e 73 " +
                "63 72 65 65 6e 20 77 6f 75 6c 64 20 62 65 20 69 " +
                "74 2e";
            string aad_hex = "50 51 52 53 c0 c1 c2 c3 c4 c5 c6 c7";
            string key_hex = "80 81 82 83 84 85 86 87 88 89 8a 8b 8c 8d 8e 8f " +
                "90 91 92 93 94 95 96 97 98 99 9a 9b 9c 9d 9e 9f";

            string rfc_mac = "1a:e1:0b:59:4f:09:e2:6a:7e:90:2e:cb:d0:60:06:91".Replace(':', ' ');
            string rfc_enc = "d3 1a 8d 34 64 8e 60 db 7b 86 af bc 53 ef 7e c2 " +
                "a4 ad ed 51 29 6e 08 fe a9 e2 b5 a7 36 ee 62 d6 " +
                "3d be a4 5e 8c a9 67 12 82 fa fb 69 da 92 72 8b " +
                "1a 71 de 0a 9e 06 0b 29 05 d6 a5 b6 7e cd 3b 36 " +
                "92 dd bd 7f 2d 77 8b 8c 98 03 ae e3 28 09 1b 58 " +
                "fa b3 24 e4 fa d6 75 94 55 85 80 8b 48 31 d7 bc " +
                "3f f4 de f0 8e 4b 7a 9d e5 76 d2 65 86 ce c6 4b " +
                "61 16";

            var nonce = HexToByte(nonce_hex).AsMemory();
            var plaintext = HexToByte(plaintext_hex).AsMemory();
            var aad = HexToByte(aad_hex).AsMemory();
            var key = HexToByte(key_hex).AsMemory();
            var encrypted = new byte[plaintext.Length + AeadChaCha20Poly1305MacSize].AsMemory();
            var decrypted = new byte[plaintext.Length].AsMemory();

            Console.WriteLine("Aead_ChaCha20Poly1305_Ietf_Test()");

            Aead_ChaCha20Poly1305_Ietf_Encrypt(encrypted, plaintext, key, nonce, aad);

            string encrypted_hex = ByteToHex(encrypted.Slice(0, plaintext.Length).ToArray(), " ");
            string mac_hex = ByteToHex(encrypted.Slice(plaintext.Length, AeadChaCha20Poly1305MacSize).ToArray(), " ");

            Console.WriteLine($"Encrypted:\n{encrypted_hex}\n");

            Console.WriteLine($"MAC:\n{mac_hex}\n");

            var a = HexToByte(rfc_enc);
            if (encrypted.Slice(0, plaintext.Length).Span.SequenceEqual(a) == false)
            {
                throw new ApplicationException("encrypted != rfc_enc");
            }

            Console.WriteLine("Check OK.");

            if (Aead_ChaCha20Poly1305_Ietf_Decrypt(decrypted, encrypted, key, nonce, aad) == false)
            {
                throw new ApplicationException("Decrypt failed.");
            }
            else
            {
                Console.WriteLine("Decrypt OK.");

                if (plaintext.Span.SequenceEqual(decrypted.Span))
                {
                    Console.WriteLine("Same OK.");
                }
                else
                {
                    throw new ApplicationException("Different !!!");
                }
            }
        }
    }

    public struct Once
    {
        volatile private int flag;
        public bool IsFirstCall() => (Interlocked.CompareExchange(ref this.flag, 1, 0) == 0);
        public bool IsSet => (this.flag != 0);
    }

    public class TimeoutDetector : IDisposable
    {
        Task main_loop;

        object LockObj = new object();

        Stopwatch sw = new Stopwatch();
        long tick { get => this.sw.ElapsedMilliseconds; }

        public long Timeout { get; }

        long next_timeout;

        AsyncAutoResetEvent ev = new AsyncAutoResetEvent();

        CancellationTokenSource halt = new CancellationTokenSource();

        CancelWatcher watcher;
        AutoResetEvent event_auto;
        ManualResetEvent event_manual;

        CancellationTokenSource cts = new CancellationTokenSource();
        public CancellationToken Cancel { get => cts.Token; }
        public Task TaskWaitMe { get => this.main_loop; }

        Action callme;

        public TimeoutDetector(int timeout, CancelWatcher watcher = null, AutoResetEvent event_auto = null, ManualResetEvent event_manual = null, Action callme = null)
        {
            this.Timeout = timeout;
            this.watcher = watcher;
            this.event_auto = event_auto;
            this.event_manual = event_manual;
            this.callme = callme;

            sw.Start();
            next_timeout = tick + this.Timeout;
            main_loop = timeout_detector_main_loop();
        }

        public void Keep()
        {
            lock (LockObj)
            {
                this.next_timeout = tick + this.Timeout;
            }
        }

        async Task timeout_detector_main_loop()
        {
            while (halt.IsCancellationRequested == false)
            {
                long now, remain_time;

                lock (LockObj)
                {
                    now = tick;
                    remain_time = next_timeout - now;
                }

                Dbg.Where($"remain_time = {remain_time}");

                if (remain_time < 0)
                {
                    break;
                }
                else
                {
                    await WebSocketHelper.WaitObjectsAsync(
                        auto_events: new AsyncAutoResetEvent[] { ev },
                        cancels: new CancellationToken[] { halt.Token },
                        timeout: (int)remain_time);
                }
            }

            cts.TryCancelAsync().LaissezFaire();
            if (this.watcher != null) this.watcher.Cancel();
            if (this.event_auto != null) this.event_auto.Set();
            if (this.event_manual != null) this.event_manual.Set();
            if (this.callme != null)
            {
                new Task(() =>
                {
                    try
                    {
                        this.callme();
                    }
                    catch { }
                }).Start();
            }
        }

        Once dispose_flag;
        public void Dispose()
        {
            if (dispose_flag.IsFirstCall())
            {
                halt.TryCancelAsync().LaissezFaire();
            }
        }
    }

    public class CancelWatcher : IDisposable
    {
        CancellationTokenSource cts = new CancellationTokenSource();
        public CancellationToken CancelToken { get => cts.Token; }
        public Task TaskWaitMe { get; }
        public AsyncManualResetEvent EventWaitMe { get; } = new AsyncManualResetEvent();
        public bool Canceled { get; private set; } = false;

        CancellationTokenSource canceller = new CancellationTokenSource();

        AsyncAutoResetEvent ev = new AsyncAutoResetEvent();
        volatile bool halt = false;

        HashSet<CancellationToken> target_list = new HashSet<CancellationToken>();
        List<Task> task_list = new List<Task>();

        object LockObj = new object();

        public CancelWatcher(params CancellationToken[] cancels)
        {
            AddWatch(canceller.Token);
            AddWatch(cancels);

            this.TaskWaitMe = cancel_watch_mainloop();
        }

        public void Cancel()
        {
            canceller.TryCancelAsync().LaissezFaire();
            this.Canceled = true;
        }

        async Task cancel_watch_mainloop()
        {
            while (true)
            {
                List<CancellationToken> cancels = new List<CancellationToken>();

                lock (LockObj)
                {
                    foreach (CancellationToken c in target_list)
                        cancels.Add(c);
                }

                await WebSocketHelper.WaitObjectsAsync(
                    cancels: cancels.ToArray(),
                    auto_events: new AsyncAutoResetEvent[] { ev });

                bool canceled = false;

                lock (LockObj)
                {
                    foreach (CancellationToken c in target_list)
                    {
                        if (c.IsCancellationRequested)
                        {
                            canceled = true;
                            break;
                        }
                    }
                }

                if (halt)
                {
                    canceled = true;
                }

                if (canceled)
                {
                    this.cts.TryCancelAsync().LaissezFaire();
                    this.EventWaitMe.Set();
                    this.Canceled = true;
                    break;
                }
            }
        }

        public bool AddWatch(params CancellationToken[] cancels)
        {
            bool ret = false;

            lock (LockObj)
            {
                foreach (CancellationToken cancel in cancels)
                {
                    if (this.target_list.Contains(cancel) == false)
                    {
                        this.target_list.Add(cancel);
                        ret = true;
                    }
                }
            }

            if (ret)
            {
                this.ev.Set();
            }

            return ret;
        }

        Once dispose_flag;

        public void Dispose()
        {
            if (dispose_flag.IsFirstCall())
            {
                this.halt = true;
                this.Canceled = true;
                this.ev.Set();
                this.cts.TryCancelAsync().LaissezFaire();
                this.EventWaitMe.Set();
                this.TaskWaitMe.Wait();
            }
        }
    }

    public class AsyncAutoResetEvent
    {
        object lockobj = new object();
        List<AsyncManualResetEvent> event_queue = new List<AsyncManualResetEvent>();
        bool is_set = false;

        public Task WaitOneAsync(out Action cancel)
        {
            lock (lockobj)
            {
                if (is_set)
                {
                    is_set = false;
                    cancel = () => { };
                    return Task.CompletedTask;
                }

                AsyncManualResetEvent e = new AsyncManualResetEvent();

                Task ret = e.WaitAsync();

                event_queue.Add(e);

                cancel = () =>
                {
                    lock (lockobj)
                    {
                        event_queue.Remove(e);
                    }
                };

                return ret;
            }
        }

        public void Set()
        {
            AsyncManualResetEvent ev = null;
            lock (lockobj)
            {
                if (event_queue.Count >= 1)
                {
                    ev = event_queue[event_queue.Count - 1];
                    event_queue.Remove(ev);
                }

                if (ev == null)
                {
                    is_set = true;
                }
            }

            if (ev != null)
            {
                ev.Set();
            }
        }
    }

    public class AsyncManualResetEvent
    {
        object lockobj = new object();
        volatile TaskCompletionSource<bool> tcs;
        bool is_set = false;

        public AsyncManualResetEvent()
        {
            init();
        }

        void init()
        {
            this.tcs = new TaskCompletionSource<bool>();
        }

        public bool IsSet
        {
            get
            {
                lock (lockobj)
                {
                    return this.is_set;
                }
            }
        }

        public Task WaitAsync()
        {
            lock (lockobj)
            {
                if (is_set)
                {
                    return Task.CompletedTask;
                }
                else
                {
                    return tcs.Task;
                }
            }
        }

        public void Set()
        {
            lock (lockobj)
            {
                if (is_set == false)
                {
                    is_set = true;
                    tcs.TrySetResult(true);
                }
            }
        }

        public void Reset()
        {
            lock (lockobj)
            {
                if (is_set)
                {
                    is_set = false;
                    init();
                }
            }
        }
    }

    class TimeHelper
    {
        internal Stopwatch Sw;
        internal long Freq;
        internal DateTimeOffset FirstDateTimeOffset;

        public TimeHelper()
        {
            FirstDateTimeOffset = DateTimeOffset.Now;
            //FirstDateTimeOffset = 
            Sw = new Stopwatch();
            Sw.Start();
            Freq = Stopwatch.Frequency;
        }

        public DateTimeOffset GetDateTimeOffset()
        {
            return FirstDateTimeOffset + this.Sw.Elapsed;
        }
    }

    public static class Time
    {
        static TimeHelper h = new TimeHelper();
        static TimeSpan baseTimeSpan = new TimeSpan(0, 0, 1);

        static public TimeSpan NowTimeSpan
        {
            get
            {
                return h.Sw.Elapsed.Add(baseTimeSpan);
            }
        }

        static public long NowLong100Usecs
        {
            get
            {
                return NowTimeSpan.Ticks;
            }
        }

        static public long NowLongMillisecs
        {
            get
            {
                return NowLong100Usecs / 10000;
            }
        }

        static public long Tick64
        {
            get
            {
                return NowLongMillisecs;
            }
        }

        static public double NowDouble
        {
            get
            {
                return (double)NowLong100Usecs / (double)10000000.0;
            }
        }

        static public DateTime NowDateTimeLocal
        {
            get
            {
                return h.GetDateTimeOffset().LocalDateTime;
            }
        }


        static public DateTime NowDateTimeUtc
        {
            get
            {
                return h.GetDateTimeOffset().UtcDateTime;
            }
        }

        static public DateTimeOffset NowDateTimeOffset
        {
            get
            {
                return h.GetDateTimeOffset();
            }
        }
    }

    public class UdpPacket
    {
        public byte[] Data;
        public IPEndPoint EndPoint;

        public UdpPacket(byte[] data, IPEndPoint ep)
        {
            this.Data = data;
            this.EndPoint = ep;
        }
    }

    public class NonBlockSocket : IDisposable
    {
        public Socket Sock { get; }
        public bool IsStream { get; }
        public bool IsDisconnected { get => Watcher.Canceled; }
        public CancellationToken CancelToken { get => Watcher.CancelToken; }

        public AsyncAutoResetEvent EventSendReady { get; } = new AsyncAutoResetEvent();
        public AsyncAutoResetEvent EventRecvReady { get; } = new AsyncAutoResetEvent();
        public AsyncAutoResetEvent EventSendNow { get; } = new AsyncAutoResetEvent();

        CancelWatcher Watcher;
        byte[] TmpRecvBuffer;

        public Fifo RecvTcpFifo { get; } = new Fifo();
        public Fifo SendTcpFifo { get; } = new Fifo();

        public Queue<UdpPacket> RecvUdpQueue { get; } = new Queue<UdpPacket>();
        public Queue<UdpPacket> SendUdpQueue { get; } = new Queue<UdpPacket>();

        int MaxRecvFifoSize;
        int MaxRecvUdpQueueSize;

        Task RecvLoopTask = null;
        Task SendLoopTask = null;

        public NonBlockSocket(Socket s, CancellationToken cancel = default(CancellationToken), int tmp_buffer_size = 65536, int max_recv_buffer_size = 65536, int max_recv_udp_queue_size = 4096)
        {
            if (tmp_buffer_size < 65536) tmp_buffer_size = 65536;
            TmpRecvBuffer = new byte[tmp_buffer_size];
            MaxRecvFifoSize = max_recv_buffer_size;
            MaxRecvUdpQueueSize = max_recv_udp_queue_size;

            EventSendReady.Set();
            EventRecvReady.Set();

            Sock = s;
            IsStream = (s.SocketType == SocketType.Stream);
            Watcher = new CancelWatcher(cancel);

            if (IsStream)
            {
                RecvLoopTask = TCP_RecvLoop();
                SendLoopTask = TCP_SendLoop();
            }
            else
            {
                RecvLoopTask = UDP_RecvLoop();
                SendLoopTask = UDP_SendLoop();
            }
        }

        async Task TCP_RecvLoop()
        {
            try
            {
                await WebSocketHelper.DoAsyncWithTimeout<int>(async (cancel) =>
                {
                    while (cancel.IsCancellationRequested == false)
                    {
                        int r = await Sock.ReceiveAsync(TmpRecvBuffer, SocketFlags.None, cancel);
                        if (r <= 0) break;

                        while (cancel.IsCancellationRequested == false)
                        {
                            lock (RecvTcpFifo)
                            {
                                if (RecvTcpFifo.Size <= MaxRecvFifoSize)
                                {
                                    RecvTcpFifo.Write(TmpRecvBuffer, r);
                                    break;
                                }
                            }

                            await WebSocketHelper.WaitObjectsAsync(cancels: new CancellationToken[] { cancel },
                                timeout: 10);
                        }

                        EventRecvReady.Set();
                    }

                    return 0;
                },
                cancel: Watcher.CancelToken
                );
            }
            finally
            {
                this.Watcher.Cancel();
                EventSendReady.Set();
                EventRecvReady.Set();
            }
        }

        async Task UDP_RecvLoop()
        {
            try
            {
                await WebSocketHelper.DoAsyncWithTimeout<int>(async (cancel) =>
                {
                    while (cancel.IsCancellationRequested == false)
                    {
                        SocketReceiveFromResult r = await Sock.ReceiveFromSafeUdpErrorAsync(new ArraySegment<byte>(TmpRecvBuffer).Slice(0), SocketFlags.None);

                        while (cancel.IsCancellationRequested == false)
                        {
                            lock (RecvUdpQueue)
                            {
                                if (RecvUdpQueue.Count <= MaxRecvUdpQueueSize)
                                {
                                    RecvUdpQueue.Enqueue(new UdpPacket(TmpRecvBuffer.AsSpan(0, r.ReceivedBytes).ToArray(), (IPEndPoint)r.RemoteEndPoint));
                                    break;
                                }
                            }

                            await WebSocketHelper.WaitObjectsAsync(cancels: new CancellationToken[] { cancel },
                                timeout: 10);
                        }

                        EventRecvReady.Set();
                    }

                    return 0;
                },
                cancel: Watcher.CancelToken
                );
            }
            finally
            {
                this.Watcher.Cancel();
                EventSendReady.Set();
                EventRecvReady.Set();
            }
        }

        async Task TCP_SendLoop()
        {
            try
            {
                await WebSocketHelper.DoAsyncWithTimeout<int>(async (cancel) =>
                {
                    while (cancel.IsCancellationRequested == false)
                    {
                        byte[] send_data = null;

                        while (cancel.IsCancellationRequested == false)
                        {
                            lock (SendTcpFifo)
                            {
                                send_data = SendTcpFifo.Read();
                            }

                            if (send_data != null && send_data.Length >= 1)
                            {
                                break;
                            }

                            await WebSocketHelper.WaitObjectsAsync(cancels: new CancellationToken[] { cancel },
                                auto_events: new AsyncAutoResetEvent[] { EventSendNow });
                        }

                        int r = await Sock.SendAsync(send_data, SocketFlags.None, cancel);
                        if (r <= 0) break;

                        EventSendReady.Set();
                    }

                    return 0;
                },
                cancel: Watcher.CancelToken
                );
            }
            finally
            {
                this.Watcher.Cancel();
                EventSendReady.Set();
                EventRecvReady.Set();
            }
        }

        async Task UDP_SendLoop()
        {
            try
            {
                await WebSocketHelper.DoAsyncWithTimeout<int>(async (cancel) =>
                {
                    while (cancel.IsCancellationRequested == false)
                    {
                        UdpPacket pkt = null;

                        while (cancel.IsCancellationRequested == false)
                        {
                            lock (SendUdpQueue)
                            {
                                if (SendUdpQueue.Count >= 1)
                                {
                                    pkt = SendUdpQueue.Dequeue();
                                }
                            }

                            if (pkt != null)
                            {
                                break;
                            }

                            await WebSocketHelper.WaitObjectsAsync(cancels: new CancellationToken[] { cancel },
                                auto_events: new AsyncAutoResetEvent[] { EventSendNow });
                        }

                        int r = await Sock.SendToSafeUdpErrorAsync(pkt.Data, SocketFlags.None, pkt.EndPoint);
                        if (r <= 0) break;

                        EventSendReady.Set();
                    }

                    return 0;
                },
                cancel: Watcher.CancelToken
                );
            }
            finally
            {
                this.Watcher.Cancel();
                EventSendReady.Set();
                EventRecvReady.Set();
            }
        }

        Once dispose;
        public void Dispose()
        {
            if (dispose.IsFirstCall())
            {
                Watcher.DisposeSafe();
                Sock.DisposeSafe();
            }
        }

    }
}


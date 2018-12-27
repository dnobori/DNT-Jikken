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
        public static long Where(string msg = "", [CallerFilePath] string filename = "", [CallerLineNumber] int line = 0, [CallerMemberName] string caller = null, long last_tick = 0)
        {
            long now = DateTime.Now.Ticks;
            long diff = now - last_tick;
            WriteLine($"{Path.GetFileName(filename)}:{line} in {caller}()" + (last_tick == 0 ? "" : $" (took {diff} msecs) ") + (string.IsNullOrEmpty(msg) == false ? (": " + msg) : ""));
            return now;
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
}


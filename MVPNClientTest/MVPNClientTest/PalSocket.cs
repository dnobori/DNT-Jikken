using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;
using System.Text;
using System.Linq;
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
using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Runtime.ExceptionServices;
using System.Collections.Concurrent;
using System.Security.Authentication;

#pragma warning disable CS0162

namespace SoftEther.WebSocket.Helper
{
    struct PalSocketReceiveFromResult
    {
        public int ReceivedBytes;
        public EndPoint RemoteEndPoint;
    }

    class PalSocket : IDisposable
    {
        public static bool OSSupportsIPv4 { get => Socket.OSSupportsIPv4; }
        public static bool OSSupportsIPv6 { get => Socket.OSSupportsIPv6; }

        Socket _Socket;

        public AddressFamily AddressFamily { get; }
        public SocketType SocketType { get; }
        public ProtocolType ProtocolType { get; }

        object LockObj = new object();

        public CachedProperty<bool> NoDelay { get; }
        public CachedProperty<int> LingerTime { get; }
        public CachedProperty<int> SendBufferSize { get; }
        public CachedProperty<int> ReceiveBufferSize { get; }

        public CachedProperty<EndPoint> LocalEndPoint { get; }
        public CachedProperty<EndPoint> RemoteEndPoint { get; }

        LeakChecker.Holder Leak;

        public PalSocket(Socket s)
        {
            _Socket = s;

            AddressFamily = _Socket.AddressFamily;
            SocketType = _Socket.SocketType;
            ProtocolType = _Socket.ProtocolType;

            NoDelay = new CachedProperty<bool>(value => _Socket.NoDelay = value, () => _Socket.NoDelay);
            LingerTime = new CachedProperty<int>(value =>
            {
                if (value <= 0) value = 0;
                if (value == 0)
                    _Socket.LingerState = new LingerOption(false, 0);
                else
                    _Socket.LingerState = new LingerOption(true, value);

                try
                {
                    if (value == 0)
                        _Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
                    else
                        _Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, false);
                }
                catch { }

                return value;
            }, () =>
            {
                var lingerOption = _Socket.LingerState;
                if (lingerOption == null || lingerOption.Enabled == false)
                    return 0;
                else
                    return lingerOption.LingerTime;
            });
            SendBufferSize = new CachedProperty<int>(value => _Socket.SendBufferSize = value, () => _Socket.SendBufferSize);
            ReceiveBufferSize = new CachedProperty<int>(value => _Socket.ReceiveBufferSize = value, () => _Socket.ReceiveBufferSize);
            LocalEndPoint = new CachedProperty<EndPoint>(null, () => _Socket.LocalEndPoint);
            RemoteEndPoint = new CachedProperty<EndPoint>(null, () => _Socket.RemoteEndPoint);

            Leak = LeakChecker.Enter();
        }

        public PalSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
            : this(new Socket(addressFamily, socketType, protocolType)) { }

        public Task ConnectAsync(IPAddress address, int port) => ConnectAsync(new IPEndPoint(address, port));

        public async Task ConnectAsync(EndPoint remoteEP)
        {
            await _Socket.ConnectAsync(remoteEP);

            this.LocalEndPoint.Flush();
            this.RemoteEndPoint.Flush();
        }

        public void Connect(EndPoint remoteEP) => _Socket.Connect(remoteEP);

        public void Connect(IPAddress address, int port) => _Socket.Connect(address, port);

        public void Bind(EndPoint localEP)
        {
            _Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, true);
            _Socket.Bind(localEP);
            this.LocalEndPoint.Flush();
            this.RemoteEndPoint.Flush();
        }

        public void Listen(int backlog = int.MaxValue)
        {
            _Socket.Listen(backlog);
            this.LocalEndPoint.Flush();
            this.RemoteEndPoint.Flush();
        }

        public async Task<PalSocket> AcceptAsync()
        {
            Socket newSocket = await _Socket.AcceptAsync();
            return new PalSocket(newSocket);
        }

        public Task<int> SendAsync(IEnumerable<Memory<byte>> buffers)
        {
            List<ArraySegment<byte>> sendArraySegmentsList = new List<ArraySegment<byte>>();
            foreach (Memory<byte> mem in buffers)
                sendArraySegmentsList.Add(mem.AsSegment());

            return _Socket.SendAsync(sendArraySegmentsList, SocketFlags.None);
        }

        public async Task<int> SendAsync(ReadOnlyMemory<byte> buffer)
        {
            return await _Socket.SendAsync(buffer, SocketFlags.None);
        }

        public async Task<int> ReceiveAsync(Memory<byte> buffer)
        {
            return await _Socket.ReceiveAsync(buffer, SocketFlags.None);
        }

        public async Task<int> SendToAsync(Memory<byte> buffer, EndPoint remoteEP)
        {
            try
            {
                Task<int> t = _Socket.SendToAsync(buffer.AsSegment(), SocketFlags.None, remoteEP);
                if (t.IsCompleted == false)
                    await t;
                int ret = t.Result;
                if (ret <= 0) throw new SocketDisconnectedException();
                return ret;
            }
            catch (SocketException e) when (CanUdpSocketErrorBeIgnored(e))
            {
                return buffer.Length;
            }
        }

        static readonly IPEndPoint StaticUdpEndPointIPv4 = new IPEndPoint(IPAddress.Any, 0);
        static readonly IPEndPoint StaticUdpEndPointIPv6 = new IPEndPoint(IPAddress.IPv6Any, 0);
        const int UdpMaxRetryOnIgnoreError = 1000;

        public async Task<PalSocketReceiveFromResult> ReceiveFromAsync(Memory<byte> buffer)
        {
            int numRetry = 0;

            var bufferSegment = buffer.AsSegment();

            LABEL_RETRY:

            try
            {
                Task<SocketReceiveFromResult> t = _Socket.ReceiveFromAsync(bufferSegment, SocketFlags.None,
                    this.AddressFamily == AddressFamily.InterNetworkV6 ? StaticUdpEndPointIPv6 : StaticUdpEndPointIPv4);
                if (t.IsCompleted == false)
                {
                    numRetry = 0;
                    await t;
                }
                SocketReceiveFromResult ret = t.Result;
                if (ret.ReceivedBytes <= 0) throw new SocketDisconnectedException();
                return new PalSocketReceiveFromResult()
                {
                    ReceivedBytes = ret.ReceivedBytes,
                    RemoteEndPoint = ret.RemoteEndPoint,
                };
            }
            catch (SocketException e) when (CanUdpSocketErrorBeIgnored(e) || _Socket.Available >= 1)
            {
                numRetry++;
                if (numRetry >= UdpMaxRetryOnIgnoreError)
                {
                    throw;
                }
                await Task.Yield();
                goto LABEL_RETRY;
            }
        }

        Once DisposeFlag;
        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing)
        {
            if (DisposeFlag.IsFirstCall() && disposing)
            {
                _Socket.DisposeSafe();

                Leak.DisposeSafe();
            }
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
    }

    enum IPVersion
    {
        IPv4 = 0,
        IPv6 = 1,
    }

    enum ListenStatus
    {
        Trying,
        Listening,
        Stopped,
    }

    sealed class PalTcpListener : IAsyncCleanupable
    {
        public class Listener
        {
            public IPVersion IPVersion { get; }
            public IPAddress IPAddress { get; }
            public int Port { get; }

            public ListenStatus Status { get; internal set; }
            public Exception LastError { get; internal set; }

            internal Task _InternalTask { get; }

            internal CancellationTokenSource _InternalSelfCancelSource { get; }
            internal CancellationToken _InternalSelfCancelToken { get => _InternalSelfCancelSource.Token; }

            public PalTcpListener Manager { get; }

            public const long RetryIntervalStandard = 1 * 512;
            public const long RetryIntervalMax = 60 * 1000;

            internal Listener(PalTcpListener manager, IPVersion ver, IPAddress addr, int port)
            {
                Manager = manager;
                IPVersion = ver;
                IPAddress = addr;
                Port = port;
                LastError = null;
                Status = ListenStatus.Trying;
                _InternalSelfCancelSource = new CancellationTokenSource();

                _InternalTask = ListenLoop();
            }

            static internal string MakeHashKey(IPVersion ipVer, IPAddress ipAddress, int port)
            {
                return $"{port} / {ipAddress} / {ipAddress.AddressFamily} / {ipVer}";
            }

            async Task ListenLoop()
            {
                AsyncAutoResetEvent networkChangedEvent = new AsyncAutoResetEvent();
                int eventRegisterId = BackgroundState<HostNetInfo>.EventListener.RegisterAsyncEvent(networkChangedEvent);

                Status = ListenStatus.Trying;

                int numRetry = 0;
                int lastNetworkInfoVer = BackgroundState<HostNetInfo>.Current.Version;

                try
                {
                    while (_InternalSelfCancelToken.IsCancellationRequested == false)
                    {
                        Status = ListenStatus.Trying;
                        _InternalSelfCancelToken.ThrowIfCancellationRequested();

                        int sleepDelay = (int)Math.Min(RetryIntervalStandard * numRetry, RetryIntervalMax);
                        if (sleepDelay >= 1)
                            sleepDelay = WebSocketHelper.RandSInt31() % sleepDelay;
                        await WebSocketHelper.WaitObjectsAsync(timeout: sleepDelay,
                            cancels: new CancellationToken[] { _InternalSelfCancelToken },
                            events: new AsyncAutoResetEvent[] { networkChangedEvent });
                        numRetry++;

                        int networkInfoVer = BackgroundState<HostNetInfo>.Current.Version;
                        if (lastNetworkInfoVer != networkInfoVer)
                        {
                            lastNetworkInfoVer = networkInfoVer;
                            numRetry = 0;
                        }

                        _InternalSelfCancelToken.ThrowIfCancellationRequested();

                        try
                        {
                            TcpListener listener = new TcpListener(IPAddress, Port);
                            listener.ExclusiveAddressUse = true;
                            listener.Start();

                            var reg = _InternalSelfCancelToken.Register(() =>
                            {
                                try { listener.Stop(); } catch { };
                            });

                            try
                            {
                                Status = ListenStatus.Listening;

                                try
                                {
                                    while (true)
                                    {
                                        _InternalSelfCancelToken.ThrowIfCancellationRequested();

                                        var socket = await listener.AcceptSocketAsync();

                                        Manager.SocketAccepted(this, new PalSocket(socket));
                                    }
                                }
                                finally
                                {
                                    try { listener.Stop(); } catch { };
                                }
                            }
                            finally
                            {
                                reg.DisposeSafe();
                            }
                        }
                        catch (Exception ex)
                        {
                            LastError = ex;
                        }
                    }
                }
                finally
                {
                    BackgroundState<HostNetInfo>.EventListener.UnregisterAsyncEvent(eventRegisterId);
                    Status = ListenStatus.Stopped;
                }
            }

            internal async Task _InternalStopAsync()
            {
                await _InternalSelfCancelSource.TryCancelAsync();
                try
                {
                    await _InternalTask;
                }
                catch { }
            }
        }

        readonly object LockObj = new object();

        readonly Dictionary<string, Listener> List = new Dictionary<string, Listener>();

        readonly Dictionary<Task, PalSocket> RunningAcceptedTasks = new Dictionary<Task, PalSocket>();

        readonly CancellationTokenSource CancelSource = new CancellationTokenSource();

        Func<PalTcpListener, Listener, PalSocket, Task> AcceptedProc { get; }

        public int CurrentConnections
        {
            get
            {
                lock (RunningAcceptedTasks)
                    return RunningAcceptedTasks.Count;
            }
        }

        public PalTcpListener(Func<PalTcpListener, Listener, PalSocket, Task> acceptedProc)
        {
            AcceptedProc = acceptedProc;
            AsyncCleanuper = new AsyncCleanuper(this);
        }

        public Listener Add(int port, IPVersion? ipVer = null, IPAddress addr = null)
        {
            if (addr == null)
                addr = ((ipVer ?? IPVersion.IPv4) == IPVersion.IPv4) ? IPAddress.Any : IPAddress.IPv6Any;
            if (ipVer == null)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                    ipVer = IPVersion.IPv4;
                else if (addr.AddressFamily == AddressFamily.InterNetworkV6)
                    ipVer = IPVersion.IPv6;
                else
                    throw new ArgumentException("Unsupported AddressFamily.");
            }
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException("Port number is out of range.");

            lock (LockObj)
            {
                if (DisposeFlag.IsSet) throw new ObjectDisposedException("TcpListenManager");

                var s = Search(Listener.MakeHashKey((IPVersion)ipVer, addr, port));
                if (s != null)
                    return s;
                s = new Listener(this, (IPVersion)ipVer, addr, port);
                List.Add(Listener.MakeHashKey((IPVersion)ipVer, addr, port), s);
                return s;
            }
        }

        public async Task<bool> DeleteAsync(Listener listener)
        {
            Listener s;
            lock (LockObj)
            {
                string hashKey = Listener.MakeHashKey(listener.IPVersion, listener.IPAddress, listener.Port);
                s = Search(hashKey);
                if (s == null)
                    return false;
                List.Remove(hashKey);
            }
            await s._InternalStopAsync();
            return true;
        }

        Listener Search(string hashKey)
        {
            if (List.TryGetValue(hashKey, out Listener ret) == false)
                return null;
            return ret;
        }

        private void SocketAccepted(Listener listener, PalSocket s)
        {
            try
            {
                Task t = AcceptedProc(this, listener, s);

                if (t.IsCompleted)
                {
                    s.DisposeSafe();
                }
                else
                {
                    lock (LockObj)
                        RunningAcceptedTasks.Add(t, s);
                    t.ContinueWith(x =>
                    {
                        s.DisposeSafe();
                        lock (LockObj)
                            RunningAcceptedTasks.Remove(t);
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("AcceptedProc error: " + ex.ToString());
            }
        }

        public Listener[] Listeners
        {
            get
            {
                lock (LockObj)
                    return List.Values.ToArray();
            }
        }

        Once DisposeFlag;
        public void Dispose()
        {
            if (DisposeFlag.IsFirstCall())
            {
            }
        }

        public async Task _CleanupAsyncInternal()
        {
            List<Listener> o = new List<Listener>();
            lock (LockObj)
            {
                List.Values.ToList().ForEach(x => o.Add(x));
                List.Clear();
            }

            foreach (Listener s in o)
                await s._InternalStopAsync().TryWaitAsync();

            List<Task> waitTasks = new List<Task>();
            List<PalSocket> disconnectSockets = new List<PalSocket>();

            lock (LockObj)
            {
                foreach (var v in RunningAcceptedTasks)
                {
                    disconnectSockets.Add(v.Value);
                    waitTasks.Add(v.Key);
                }
                RunningAcceptedTasks.Clear();
            }

            foreach (var sock in disconnectSockets)
                sock.DisposeSafe();

            foreach (var task in waitTasks)
                await task.TryWaitAsync();

            Debug.Assert(CurrentConnections == 0);
        }

        public AsyncCleanuper AsyncCleanuper { get; }
    }

    class PalStream : FastStream
    {
        protected Stream SystemStream;

        public PalStream(Stream systemStream)
        {
            SystemStream = systemStream;
        }

        public override async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancel = default)
        {
            return await SystemStream.ReadAsync(buffer, cancel);
        }

        public override async Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancel = default)
        {
            await SystemStream.WriteAsync(buffer, cancel);
        }

        public async Task<byte[]> ReadAsyncWithTimeout(int maxSize = 65536, int? timeout = null, bool? readAll = false, CancellationToken cancel = default)
        {
            byte[] tmp = new byte[maxSize];
            int ret = await SystemStream.ReadAsyncWithTimeout(tmp, 0, tmp.Length, timeout,
                readAll: readAll,
                cancel: cancel);
            return WebSocketHelper.CopyByte(tmp, 0, ret);
        }

        public async Task<int> ReadAsyncWithTimeout(byte[] buffer, int offset = 0, int? count = null, int? timeout = null, bool? readAll = false, CancellationToken cancel = default, params CancellationToken[] cancelTokens)
        {
            if (timeout == null) timeout = SystemStream.ReadTimeout;
            if (timeout <= 0) timeout = Timeout.Infinite;
            int targetReadSize = count ?? (buffer.Length - offset);
            if (targetReadSize == 0) return 0;

            try
            {
                int ret = await WebSocketHelper.DoAsyncWithTimeout(async (cancelLocal) =>
                {
                    if (readAll == false)
                    {
                        return await SystemStream.ReadAsync(buffer, offset, targetReadSize, cancelLocal);
                    }
                    else
                    {
                        int currentReadSize = 0;

                        while (currentReadSize != targetReadSize)
                        {
                            int sz = await SystemStream.ReadAsync(buffer, offset + currentReadSize, targetReadSize - currentReadSize, cancelLocal);
                            if (sz == 0)
                            {
                                return 0;
                            }

                            currentReadSize += sz;
                        }

                        return currentReadSize;
                    }
                },
                timeout: (int)timeout,
                cancel: cancel,
                cancelTokens: cancelTokens);

                if (ret <= 0)
                {
                    throw new EndOfStreamException("The NetworkStream is disconnected.");
                }

                return ret;
            }
            catch
            {
                SystemStream.TryCloseNonBlock();
                throw;
            }
        }

        public async Task WriteAsyncWithTimeout(byte[] buffer, int offset = 0, int? count = null, int? timeout = null, CancellationToken cancel = default, params CancellationToken[] cancelTokens)
        {
            if (timeout == null) timeout = SystemStream.WriteTimeout;
            if (timeout <= 0) timeout = Timeout.Infinite;
            int targetWriteSize = count ?? (buffer.Length - offset);
            if (targetWriteSize == 0) return;

            try
            {
                await WebSocketHelper.DoAsyncWithTimeout(async (cancelLocal) =>
                {
                    await SystemStream.WriteAsync(buffer, offset, targetWriteSize, cancelLocal);
                    return 0;
                },
                timeout: (int)timeout,
                cancel: cancel,
                cancelTokens: cancelTokens);

            }
            catch
            {
                SystemStream.TryCloseNonBlock();
                throw;
            }
        }

        Once DisposeFlag;
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!disposing || DisposeFlag.IsFirstCall() == false) return;
                SystemStream.DisposeSafe();
            }
            finally { base.Dispose(disposing); }
        }
    }

    class PalSslStream : PalStream
    {
        public PalSslStream(SslStream sslStream) : base(sslStream) { }
    }

    static class PalDns
    {
        public static Task<IPAddress[]> GetHostAddressesAsync(string hostNameOrAddress, int timeout = Timeout.Infinite, CancellationToken cancel = default)
            => WebSocketHelper.DoAsyncWithTimeout(c => Dns.GetHostAddressesAsync(hostNameOrAddress),
                timeout: timeout, cancel: cancel);

        public static Task<IPHostEntry> GetHostEntryAsync(string hostNameOrAddress, int timeout = Timeout.Infinite, CancellationToken cancel = default)
            => WebSocketHelper.DoAsyncWithTimeout(c => Dns.GetHostEntryAsync(hostNameOrAddress),
                timeout: timeout, cancel: cancel);
    }
}

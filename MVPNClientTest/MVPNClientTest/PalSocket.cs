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
        }

        public PalSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
            : this(new Socket(addressFamily, socketType, protocolType)) { }

        public async Task ConnectAsync(EndPoint remoteEP)
        {
            await _Socket.ConnectAsync(remoteEP);

            this.LocalEndPoint.Flush();
            this.RemoteEndPoint.Flush();
        }

        public void Bind(EndPoint localEP)
        {
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

        public async Task<int> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return await _Socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
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
}

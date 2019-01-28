using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using static System.Console;
using Newtonsoft.Json;
using SoftEther.WebSocket;
using SoftEther.WebSocket.Helper;
using System.Security.Cryptography;

#pragma warning disable CS0162, CS1998, CS0169

namespace SoftEther.VpnClient
{
    class UdpAccel : IDisposable
    {
        public const int TmpBufSize = 2048;
        public const int PacketIvSize = 12;
        public const int PacketMacSize = 16;
        public const int CommonKeySize = 128;
        public const int ChaChaKeySize = 32;
        public const long NatT_GetIP_Interval_Initial = 5 * 1000;
        public const long NatT_GetIP_Interval_Max = 150 * 1000;
        public const int UdpNatTPort = RudpConsts.NatTPort;
        public const long WindowSizeMSec = 30 * 1000;
        public const int MaxPaddingSize = 32;
        public const long KeepAliveTimeoutFast = 2100;
        public const long KeepAliveIntervalMin = 500;
        public const long KeepAliveIntervalMax = 1000;
        public const long RequireContinuous = 10 * 1000;

        bool NoNatT;
        long Now;
        public byte[] MyKey { get; }
        public byte[] YourKey { get; set; }
        UdpClient Udp;
        NonBlockSocket Nb;
        public IPEndPoint MyEndPoint { get; }
        public IPEndPoint YourEndPoint { get; set; }
        IPEndPoint YourEndPoint2;
        bool IsIPv6;
        byte[] TmpBuf = new byte[TmpBufSize];
        long LastRecvYourTick;
        long LastRecvMyTick;
        public Queue<Datagram> RecvBlockQueue { get; } = new Queue<Datagram>();
        public bool PlainTextMode { get; set; }
        long LastSetSrcIpAndPortTick;
        long LastRecvTick;
        long NextSendKeepAlive;
        byte[] NextIv;
        public uint MyCookie { get; set; }
        public uint YourCookie { get; set; }
        bool Inited;
        bool ClientMode;
        int Mss;
        public int MaxUdpPacketSize { get; }
        IPAddress NatT_IP;
        Task NatT_GetIpTask;
        long NextPerformNatTTick;
        int CommToNatT_NumFail;
        int MyPortByNatTServer;
        public bool MyPortByNatTServerChanged { get; private set; }
        public int YourPortByNatTServer { get; set; }
        bool YourPortByNatTServerChanged;
        bool FatalError;
        public bool NatT_IP_Changed { get; private set; }
        ulong NatT_TranId;
        public bool IsReachedOnce { get; private set; }
        long CreatedTick;
        long FirstStableReceiveTick;
        CancelWatcher cancelWatcher;
        public AsyncAutoResetEvent EventRecvReady { get => Nb.EventRecvReady; }

        public UdpAccel(IPAddress ip = null, bool clientMode = true, bool noNatT = false, CancellationToken cancel = default)
        {
            if (ip == null) ip = IPAddress.Any;
            cancelWatcher = new CancelWatcher(cancel);
            ClientMode = clientMode;

            UdpClient s = new UdpClient(ip.AddressFamily);

            try
            {
                s.ExclusiveAddressUse = true;
                s.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);

                NoNatT = noNatT;

                NatT_TranId = WebSocketHelper.Rand(8).GetUInt64();
                Now = CreatedTick = Time.Tick64;

                MyKey = WebSocketHelper.Rand(CommonKeySize);
                YourKey = WebSocketHelper.Rand(CommonKeySize);

                Udp = s;

                s.Client.Bind(new IPEndPoint(ip, 0));

                Nb = new NonBlockSocket(new PalSocket(Udp.Client), cancelWatcher.CancelToken);
                MyEndPoint = (IPEndPoint)s.Client.LocalEndPoint;

                IsIPv6 = ip.AddressFamily == AddressFamily.InterNetworkV6;
                if (IsIPv6) NoNatT = true;

                NextIv = WebSocketHelper.Rand(PacketIvSize);
                do
                {
                    MyCookie = WebSocketHelper.Rand(4).GetUInt32();
                }
                while (MyCookie == 0);

                do
                {
                    YourCookie = WebSocketHelper.Rand(4).GetUInt32();
                }
                while (YourCookie == 0);

                MaxUdpPacketSize = 1500 - 46;
                if (IsIPv6 == false)
                    MaxUdpPacketSize -= 20;
                else
                    MaxUdpPacketSize -= 40;
                MaxUdpPacketSize -= 8;

                if (NoNatT == false)
                {
                    NatT_GetIpTask = NatT_GetIpTaskProc();
                }
            }
            catch
            {
                s.DisposeSafe();
                throw;
            }
        }

        Once d;
        public void Dispose()
        {
            if (d.IsFirstCall())
            {
                cancelWatcher.DisposeSafe();
                Nb.DisposeSafe();
                Udp.DisposeSafe();
            }
        }

        static IPAddress dummyIp = null;
        async Task NatT_GetIpTaskProc()
        {
            int numRetry = 0;

            if (dummyIp == null)
            {
                dummyIp = new IPAddress(new byte[] { 11, WebSocketHelper.Rand(1)[0], WebSocketHelper.Rand(1)[0], WebSocketHelper.Rand(1)[0] });
            }
            string hostname = VpnSession.GetNatTServerHostName(dummyIp);

            while (cancelWatcher.CancelToken.IsCancellationRequested == false)
            {
                try
                {
                    var hostent = await PalDns.GetHostEntryAsync(hostname, cancel: cancelWatcher.CancelToken);
                    IPAddress ip = hostent.AddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork).First();

                    NatT_IP = ip;
                    NatT_IP_Changed = true;

                    break;
                }
                catch { }

                numRetry++;

                int waitTime = (int)Math.Min(NatT_GetIP_Interval_Initial * numRetry, NatT_GetIP_Interval_Max);

                await WebSocketHelper.WaitObjectsAsync(cancels: new CancellationToken[] { cancelWatcher.CancelToken },
                    timeout: waitTime);
            }
        }

        public void InitClient(byte[] serverKey, IPEndPoint serverEndPoint, uint serverCookie, uint clientCookie, IPAddress serverIp2 = null)
        {
            if (ClientMode == false) throw new ApplicationException("ClientMode == false");
            if ((serverEndPoint.Address.AddressFamily == AddressFamily.InterNetworkV6) != IsIPv6) throw new ArgumentException("server_endpoint.Address.AddressFamily");
            YourKey = serverKey;
            YourEndPoint = serverEndPoint;
            if (serverIp2 != null)
            {
                YourEndPoint2 = new IPEndPoint(serverIp2, YourEndPoint.Port);
            }
            Now = Time.Tick64;
            MyCookie = clientCookie;
            YourCookie = serverCookie;
            Inited = true;
        }

        public void InitServer(byte[] clientKey, IPEndPoint clientEndPoint, IPEndPoint clientEndPoint2 = null)
        {
            if ((clientEndPoint.Address.AddressFamily == AddressFamily.InterNetworkV6) != IsIPv6) throw new ArgumentException("client_endpoint.Address.AddressFamily");
            if (clientEndPoint2 != null)
            {
                if ((clientEndPoint2.Address.AddressFamily == AddressFamily.InterNetworkV6) != IsIPv6) throw new ArgumentException("client_endpoint2.Address.AddressFamily");
            }
            YourKey = clientKey;
            YourEndPoint = clientEndPoint;
            YourEndPoint2 = clientEndPoint2;
            Now = Time.Tick64;
            Inited = true;
        }

        public void Poll()
        {
            IPAddress natT_IP = NatT_IP;

            Now = Time.Tick64;

            while (true)
            {
                Datagram ret = Nb.RecvFromNonBlock();

                if (ret == null)
                {
                    break;
                }

                if (natT_IP != null && natT_IP.Equals(ret.IPEndPoint.Address) && ret.IPEndPoint.Port == UdpNatTPort)
                {
                    var ep = ParseIPAndPortStr(ret.Data);
                    if (ep != null && ep.Port >= 1 && ep.Port <= 65535)
                    {
                        if (MyPortByNatTServer != ep.Port)
                        {
                            MyPortByNatTServer = ep.Port;
                            MyPortByNatTServerChanged = true;
                            CommToNatT_NumFail = 0;
                        }
                    }
                }
                else
                {
                    try
                    {
                        Datagram b = UdpAccelProcessRecvPacket(ret.Data, ret.IPEndPoint);
                        if (b.Data.Length >= 1)
                        {
                            RecvBlockQueue.Enqueue(b);
                        }
                    }
                    catch (Exception ex)
                    {
                        Dbg.Where(ex.ToString());
                    }
                }
            }

            if (NextSendKeepAlive == 0 || (NextSendKeepAlive <= Now) || YourPortByNatTServerChanged)
            {
                YourPortByNatTServerChanged = false;
                if (IsSendReady(false))
                {
                    int RandInterval = (int)(WebSocketHelper.RandSInt31() % (KeepAliveIntervalMax - KeepAliveIntervalMin) + KeepAliveIntervalMin);
                    NextSendKeepAlive = Now + RandInterval;

                    Send(null, 0, maxSize: 1000);
                }
            }

            if (NoNatT == false && natT_IP != null)
            {
                if (IsSendReady(true) == false)
                {
                    if (NextPerformNatTTick == 0 || (NextPerformNatTTick <= Now))
                    {
                        int randInterval = RudpConsts.NatTIntervalInitial * Math.Min(CommToNatT_NumFail, RudpConsts.NatTIntervalFailMax);
                        if (MyPortByNatTServer != 0)
                        {
                            randInterval = WebSocketHelper.GenRandInterval(RudpConsts.NatTIntervalMin, RudpConsts.NatTIntervalMax);
                        }

                        NextPerformNatTTick = Now + randInterval;

                        byte c = (byte)'B';

                        lock (Nb.SendUdpQueue)
                        {
                            if (Nb.SendUdpQueue.Count <= Nb.MaxRecvUdpQueueSize)
                            {
                                Nb.SendUdpQueue.Enqueue(new Datagram(new byte[] { c }, new IPEndPoint(natT_IP, RudpConsts.NatTPort)));
                                Nb.EventSendNow.SetLazy();
                            }
                        }
                    }
                }
            }
        }

        Datagram UdpAccelProcessRecvPacket(FastMemoryBuffer<byte> buf, IPEndPoint src)
        {
            Datagram ret;

            if (PlainTextMode == false)
            {
                var iv = buf.ReadAsMemory(PacketIvSize);
                buf = buf.SliceAfter();
                if (WebSocketHelper.Aead_ChaCha20Poly1305_Ietf_Decrypt(buf, buf, YourKey.AsMemory().Slice(0, ChaChaKeySize), iv, null) == false)
                {
                    throw new ApplicationException("Aead_ChaCha20Poly1305_Ietf_Decrypt failed.");
                }
                buf = buf.Slice(0, buf.Length - PacketMacSize);
            }

            uint cookie = buf.ReadUInt32();

            if (cookie != MyCookie)
            {
                throw new ApplicationException("cookie != MyCookie");
            }

            long myTick = (long)buf.ReadUInt64();
            long yourTick = (long)buf.ReadUInt64();
            ushort innerSize = buf.ReadUInt16();
            byte flag = buf.ReadUInt8();

            var innerData = buf.Read(innerSize);

            if (myTick < LastRecvYourTick)
            {
                if ((LastRecvYourTick - myTick) >= WindowSizeMSec)
                {
                    throw new ApplicationException("(LastRecvYourTick - my_tick) >= WindowSizeMSec");
                }
            }

            LastRecvMyTick = Math.Max(LastRecvMyTick, yourTick);
            LastRecvYourTick = Math.Max(LastRecvYourTick, myTick);

            ret = new Datagram(innerData.ToArray(), null, flag);

            if (LastSetSrcIpAndPortTick < LastRecvYourTick)
            {
                LastSetSrcIpAndPortTick = LastRecvYourTick;
                YourEndPoint = src;
            }

            if (LastRecvMyTick != 0)
            {
                if ((LastRecvMyTick + WindowSizeMSec) >= Now)
                {
                    LastRecvTick = Now;
                    IsReachedOnce = true;
                    if (FirstStableReceiveTick == 0)
                    {
                        FirstStableReceiveTick = Now;
                    }
                }
            }

            return ret;
        }

        public int CalcMss()
        {
            int ret = 1500 - 46; // PPPoE

            // IP header
            if (IsIPv6)
                ret -= 40;
            else
                ret -= 20;
            // UDP header
            ret -= 8;
            // IV
            if (PlainTextMode == false) ret -= 40;
            // Cookie
            ret -= 4;
            // My Tick
            ret -= 8;
            // Your Tick
            ret -= 8;
            // Size
            ret -= 2;
            // compression flag
            ret -= 1;
            // Ethernet header
            ret -= 14;
            // IPv4 header
            ret -= 20;
            // TCP header
            ret -= 20;

            return ret;
        }

        public void Send(Memory<byte> data, byte flag, int maxSize = 0)
        {
            if (maxSize == 0) maxSize = MaxUdpPacketSize;

            FastMemoryBuffer<byte> buf = new FastMemoryBuffer<byte>();

            // IV
            if (PlainTextMode == false)
            {
                buf.Write(NextIv);
            }

            int ivPos = buf.CurrentPosition;

            // Cookie
            buf.WriteUInt32(YourCookie);

            // My Tick
            buf.WriteUInt64((ulong)Math.Max(Now, 1));

            // Your Tick
            buf.WriteUInt64((ulong)LastRecvYourTick);

            // Size
            buf.WriteUInt16((ushort)data.Length);

            // Flag
            buf.WriteUInt8(flag);

            // Data
            buf.Write(data);

            if (PlainTextMode == false)
            {
                // Padding
                int currentLength = buf.CurrentPosition;

                if (currentLength < maxSize)
                {
                    int padSize = Math.Min(maxSize - currentLength, MaxPaddingSize);
                    padSize = WebSocketHelper.RandSInt31() % padSize;
                    Span<byte> pad = new byte[padSize];
                    buf.Write(pad);
                }

                buf.Walk(PacketMacSize);

                var memDst = buf.Memory.Slice(ivPos);
                var memSrc = memDst.Slice(0, memDst.Length - PacketMacSize);
                var key = MyKey.AsMemory().Slice(0, ChaChaKeySize);
                WebSocketHelper.Aead_ChaCha20Poly1305_Ietf_Encrypt(memDst, memSrc, key, NextIv, null);
                memSrc.Slice(memSrc.Length - PacketIvSize, PacketIvSize).CopyTo(NextIv);
            }

            var sendData = buf.Memory;

            lock (Nb.SendUdpQueue)
            {
                if (Nb.SendUdpQueue.Count <= Nb.MaxRecvUdpQueueSize)
                {
                    Nb.SendUdpQueue.Enqueue(new Datagram(sendData.ToArray(), YourEndPoint));
                    Nb.EventSendNow.SetLazy();
                }
            }

            if (data.IsEmpty)
            {
                if (IsSendReady(true) == false)
                {
                    if (YourPortByNatTServer != 0 && YourEndPoint.Port != YourPortByNatTServer)
                    {
                        lock (Nb.SendUdpQueue)
                        {
                            if (Nb.SendUdpQueue.Count <= Nb.MaxRecvUdpQueueSize)
                            {
                                Nb.SendUdpQueue.Enqueue(new Datagram(sendData.ToArray(), new IPEndPoint(YourEndPoint.Address, YourPortByNatTServer)));
                                Nb.EventSendNow.SetLazy();
                            }
                        }
                    }

                    if (YourEndPoint2 != null)
                    {
                        lock (Nb.SendUdpQueue)
                        {
                            if (Nb.SendUdpQueue.Count <= Nb.MaxRecvUdpQueueSize)
                            {
                                Nb.SendUdpQueue.Enqueue(new Datagram(sendData.ToArray(), YourEndPoint2));
                                if (YourPortByNatTServer != 0 && YourEndPoint2.Port != YourPortByNatTServer)
                                {
                                    Nb.SendUdpQueue.Enqueue(new Datagram(sendData.ToArray(), new IPEndPoint(YourEndPoint2.Address, YourPortByNatTServer)));
                                    Nb.EventSendNow.SetLazy();
                                }
                            }
                        }
                    }
                }
                else
                {
                    NextPerformNatTTick = 0;
                    CommToNatT_NumFail = 0;
                }
            }
        }

        public void SendFinish()
        {
            Nb.EventSendNow.SetIfLazyQueued();
        }

        public bool IsSendReady(bool checkKeepalive)
        {
            long timeoutValue;
            if (Inited == false) return false;
            if (YourEndPoint == null) return false;
            timeoutValue = KeepAliveTimeoutFast;

            if (checkKeepalive)
            {
                if (LastRecvTick == 0 || ((LastRecvTick + timeoutValue) < Now))
                {
                    FirstStableReceiveTick = 0;
                    return false;
                }
                else
                {
                    if ((FirstStableReceiveTick + RequireContinuous) <= Now)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public void SetTick(long tick64)
        {
            Now = tick64;
        }

        public static IPEndPoint ParseIPAndPortStr(Memory<byte> data)
        {
            try
            {
                return ParseIPAndPortStr(Encoding.ASCII.GetString(data.Span));
            }
            catch { }
            return null;
        }
        public static IPEndPoint ParseIPAndPortStr(string str)
        {
            try
            {
                var span = str.AsSpan();

                if (span.StartsWith("IP=", StringComparison.InvariantCultureIgnoreCase))
                {
                    span = span.Slice(3);
                    int i = span.IndexOf("#", StringComparison.InvariantCultureIgnoreCase);
                    if (i != -1) span = span.Slice(0, i);

                    i = span.IndexOf(",PORT=", StringComparison.InvariantCultureIgnoreCase);
                    if (i != -1)
                    {
                        var ipstr = span.Slice(0, i);
                        var portstr = span.Slice(i + 6);

                        return new IPEndPoint(IPAddress.Parse(ipstr), int.Parse(portstr));
                    }
                }
            }
            catch { }
            return null;
        }
    }
}

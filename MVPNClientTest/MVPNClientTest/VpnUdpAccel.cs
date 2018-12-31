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
    public class UdpAccel : IDisposable
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
        public Queue<UdpPacket> RecvBlockQueue { get; } = new Queue<UdpPacket>();
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
        CancelWatcher cancel_watcher;
        public AsyncAutoResetEvent EventRecvReady { get => Nb.EventRecvReady; }

        public UdpAccel(IPAddress ip = null, bool client_mode = true, bool no_nat_t = false, CancellationToken cancel = default(CancellationToken))
        {
            if (ip == null) ip = IPAddress.Any;
            cancel_watcher = new CancelWatcher(cancel);
            ClientMode = client_mode;

            UdpClient s = new UdpClient(ip.AddressFamily);

            try
            {
                s.ExclusiveAddressUse = true;
                s.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);

                NoNatT = no_nat_t;

                NatT_TranId = WebSocketHelper.Rand(8).ToUInt64();
                Now = CreatedTick = Time.Tick64;

                MyKey = WebSocketHelper.Rand(CommonKeySize);
                YourKey = WebSocketHelper.Rand(CommonKeySize);

                Udp = s;

                s.Client.Bind(new IPEndPoint(ip, 0));

                Nb = new NonBlockSocket(Udp.Client, cancel_watcher.CancelToken);
                MyEndPoint = (IPEndPoint)s.Client.LocalEndPoint;

                IsIPv6 = ip.AddressFamily == AddressFamily.InterNetworkV6;
                if (IsIPv6) NoNatT = true;

                NextIv = WebSocketHelper.Rand(PacketIvSize);
                do
                {
                    MyCookie = WebSocketHelper.Rand(4).ToUInt32();
                }
                while (MyCookie == 0);

                do
                {
                    YourCookie = WebSocketHelper.Rand(4).ToUInt32();
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
                cancel_watcher.DisposeSafe();
                Nb.DisposeSafe();
                Udp.DisposeSafe();
            }
        }

        static IPAddress dummy_ip = null;
        async Task NatT_GetIpTaskProc()
        {
            int num_retry = 0;

            if (dummy_ip == null)
            {
                dummy_ip = new IPAddress(new byte[] { 11, WebSocketHelper.Rand(1)[0], WebSocketHelper.Rand(1)[0], WebSocketHelper.Rand(1)[0] });
            }
            string hostname = VpnSession.GetNatTServerHostName(dummy_ip);

            while (cancel_watcher.CancelToken.IsCancellationRequested == false)
            {
                try
                {
                    var hostent = await Dns.GetHostEntryAsync(hostname);
                    IPAddress ip = hostent.AddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork).First();

                    NatT_IP = ip;
                    NatT_IP_Changed = true;

                    break;
                }
                catch { }

                num_retry++;

                int wait_time = (int)Math.Min(NatT_GetIP_Interval_Initial * num_retry, NatT_GetIP_Interval_Max);

                await WebSocketHelper.WaitObjectsAsync(cancels: new CancellationToken[] { cancel_watcher.CancelToken },
                    timeout: wait_time);
            }
        }

        public void InitClient(byte[] server_key, IPEndPoint server_endpoint, uint server_cookie, uint client_cookie, IPAddress server_ip_2 = null)
        {
            if (ClientMode == false) throw new ApplicationException("ClientMode == false");
            if ((server_endpoint.Address.AddressFamily == AddressFamily.InterNetworkV6) != IsIPv6) throw new ArgumentException("server_endpoint.Address.AddressFamily");
            YourKey = server_key;
            YourEndPoint = server_endpoint;
            if (server_ip_2 != null)
            {
                YourEndPoint2 = new IPEndPoint(server_ip_2, YourEndPoint.Port);
            }
            Now = Time.Tick64;
            MyCookie = client_cookie;
            YourCookie = server_cookie;
            Inited = true;
        }

        public void InitServer(byte[] client_key, IPEndPoint client_endpoint, IPEndPoint client_endpoint2 = null)
        {
            if ((client_endpoint.Address.AddressFamily == AddressFamily.InterNetworkV6) != IsIPv6) throw new ArgumentException("client_endpoint.Address.AddressFamily");
            if (client_endpoint2 != null)
            {
                if ((client_endpoint2.Address.AddressFamily == AddressFamily.InterNetworkV6) != IsIPv6) throw new ArgumentException("client_endpoint2.Address.AddressFamily");
            }
            YourKey = client_key;
            YourEndPoint = client_endpoint;
            YourEndPoint2 = client_endpoint2;
            Now = Time.Tick64;
            Inited = true;
        }

        public void Poll()
        {
            IPAddress nat_t_ip = NatT_IP;

            Now = Time.Tick64;

            while (true)
            {
                UdpPacket ret = Nb.RecvFromNonBlock();

                if (ret == null)
                {
                    break;
                }

                if (nat_t_ip != null && nat_t_ip.Equals(ret.EndPoint.Address) && ret.EndPoint.Port == UdpNatTPort)
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
                        UdpPacket b = UdpAccelProcessRecvPacket(ret.Data, ret.EndPoint);
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
                    int rand_interval = (int)(WebSocketHelper.RandSInt31() % (KeepAliveIntervalMax - KeepAliveIntervalMin) + KeepAliveIntervalMin);
                    NextSendKeepAlive = Now + rand_interval;

                    Send(null, 0, max_size: 1000);
                }
            }

            if (NoNatT == false && nat_t_ip != null)
            {
                if (IsSendReady(true) == false)
                {
                    if (NextPerformNatTTick == 0 || (NextPerformNatTTick <= Now))
                    {
                        int rand_interval = RudpConsts.NatTIntervalInitial * Math.Min(CommToNatT_NumFail, RudpConsts.NatTIntervalFailMax);
                        if (MyPortByNatTServer != 0)
                        {
                            rand_interval = WebSocketHelper.GenRandInterval(RudpConsts.NatTIntervalMin, RudpConsts.NatTIntervalMax);
                        }

                        NextPerformNatTTick = Now + rand_interval;

                        byte c = (byte)'B';

                        lock (Nb.SendUdpQueue)
                        {
                            if (Nb.SendUdpQueue.Count <= Nb.MaxRecvUdpQueueSize)
                            {
                                Nb.SendUdpQueue.Enqueue(new UdpPacket(new byte[] { c }, new IPEndPoint(nat_t_ip, RudpConsts.NatTPort)));
                                Nb.EventSendNow.SetLazy();
                            }
                        }
                    }
                }
            }
        }

        UdpPacket UdpAccelProcessRecvPacket(Memory<byte> buf, IPEndPoint src)
        {
            UdpPacket ret;

            if (PlainTextMode == false)
            {
                var iv = buf.WalkRead(PacketIvSize);
                if (WebSocketHelper.Aead_ChaCha20Poly1305_Ietf_Decrypt(buf, buf, YourKey.AsMemory().Slice(0, ChaChaKeySize), iv, null) == false)
                {
                    throw new ApplicationException("Aead_ChaCha20Poly1305_Ietf_Decrypt failed.");
                }
                buf = buf.Slice(0, buf.Length - PacketMacSize);
            }

            uint cookie = buf.WalkReadUInt32();

            if (cookie != MyCookie)
            {
                throw new ApplicationException("cookie != MyCookie");
            }

            long my_tick = (long)buf.WalkReadUInt64();
            long your_tick = (long)buf.WalkReadUInt64();
            ushort inner_size = buf.WalkReadUInt16();
            byte flag = buf.WalkReadUInt8();

            var inner_data = buf.WalkRead(inner_size);

            if (my_tick < LastRecvYourTick)
            {
                if ((LastRecvYourTick - my_tick) >= WindowSizeMSec)
                {
                    throw new ApplicationException("(LastRecvYourTick - my_tick) >= WindowSizeMSec");
                }
            }

            LastRecvMyTick = Math.Max(LastRecvMyTick, your_tick);
            LastRecvYourTick = Math.Max(LastRecvYourTick, my_tick);

            ret = new UdpPacket(inner_data.ToArray(), null, flag);

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

        public void Send(Memory<byte> data, byte flag, int max_size = 0)
        {
            if (max_size == 0) max_size = MaxUdpPacketSize;

            Memory<byte> buf = new Memory<byte>();
            int buf_pin = buf.WalkGetPin();

            // IV
            if (PlainTextMode == false)
            {
                buf.WalkAutoDynamicWrite(NextIv);
            }

            int iv_pin = buf.WalkGetPin();

            // Cookie
            buf.WalkAutoDynamicWriteUInt32(YourCookie);

            // My Tick
            buf.WalkAutoDynamicWriteUInt64((ulong)Math.Max(Now, 1));

            // Your Tick
            buf.WalkAutoDynamicWriteUInt64((ulong)LastRecvYourTick);

            // Size
            buf.WalkAutoDynamicWriteUInt16((ushort)data.Length);

            // Flag
            buf.WalkAutoDynamicWriteUInt8(flag);

            // Data
            buf.WalkAutoDynamicWrite(data);

            if (PlainTextMode == false)
            {
                // Padding
                int current_length = buf.WalkGetPin() - buf_pin;

                if (current_length < max_size)
                {
                    int pad_size = Math.Min(max_size - current_length, MaxPaddingSize);
                    pad_size = WebSocketHelper.RandSInt31() % pad_size;
                    Span<byte> pad = stackalloc byte[pad_size];
                    buf.WalkAutoDynamicWrite(pad);
                }

                buf.WalkAutoDynamic(PacketMacSize);

                var mem_dst = buf.SliceWithPin(iv_pin);
                var mem_src = mem_dst.Slice(0, mem_dst.Length - PacketMacSize);
                var key = MyKey.AsMemory().Slice(0, ChaChaKeySize);
                WebSocketHelper.Aead_ChaCha20Poly1305_Ietf_Encrypt(mem_dst, mem_src, key, NextIv, null);
                mem_src.Slice(mem_src.Length - PacketIvSize, PacketIvSize).CopyTo(NextIv);
            }

            var send_data = buf.SliceWithPin(buf_pin);

            lock (Nb.SendUdpQueue)
            {
                if (Nb.SendUdpQueue.Count <= Nb.MaxRecvUdpQueueSize)
                {
                    Nb.SendUdpQueue.Enqueue(new UdpPacket(send_data.ToArray(), YourEndPoint));
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
                                Nb.SendUdpQueue.Enqueue(new UdpPacket(send_data.ToArray(), new IPEndPoint(YourEndPoint.Address, YourPortByNatTServer)));
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
                                Nb.SendUdpQueue.Enqueue(new UdpPacket(send_data.ToArray(), YourEndPoint2));
                                if (YourPortByNatTServer != 0 && YourEndPoint2.Port != YourPortByNatTServer)
                                {
                                    Nb.SendUdpQueue.Enqueue(new UdpPacket(send_data.ToArray(), new IPEndPoint(YourEndPoint2.Address, YourPortByNatTServer)));
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

        public bool IsSendReady(bool check_keepalive)
        {
            long timeout_value;
            if (Inited == false) return false;
            if (YourEndPoint == null) return false;
            timeout_value = KeepAliveTimeoutFast;

            if (check_keepalive)
            {
                if (LastRecvTick == 0 || ((LastRecvTick + timeout_value) < Now))
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

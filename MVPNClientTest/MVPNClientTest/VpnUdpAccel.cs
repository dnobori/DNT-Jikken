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
        public const long NatT_GetIP_Interval_Initial = 5 * 1000;
        public const long NatT_GetIP_Interval_Max = 150 * 1000;
        public const int UdpNatTPort = 5004;
        public const long WindowSizeMSec = 30 * 1000;

        bool NoNatT;
        long Now;
        byte[] MyKey;
        byte[] YourKey;
        UdpClient Udp;
        NonBlockSocket Nb;
        IPEndPoint MyEndPoint;
        IPEndPoint YourEndPoint;
        IPEndPoint YourEndPoint2;
        bool IsIPv6;
        byte[] TmpBuf = new byte[TmpBufSize];
        long LastRecvYourTick;
        long LastRecvMyTick;
        Queue<UdpPacket> RecvBlockQueue = new Queue<UdpPacket>();
        bool PlainTextMode;
        long LastSetSrcIpAndPortTick;
        long LastRecvTick;
        long NextSendKeepAlive;
        byte[] NextIv;
        uint MyCookie;
        uint YourCookie;
        bool Inited;
        bool ClientMode;
        int Mss;
        int MaxUdpPacketSize;
        IPAddress NatT_IP;
        Task NatT_GetIpTask;
        long NextPerformNatTTick;
        int CommToNatT_NumFail;
        int MyPortByNatTServer;
        bool MyPortByNatTServerChanged;
        int YourPortByNatTServer;
        bool YourPortByNatTServerChanged;
        bool FatalError;
        bool NatT_IP_Changed;
        ulong NatT_TranId;
        bool IsReachedOnce;
        long CreatedTick;
        bool FastDetect;
        long FirstStableReceiveTick;
        CancelWatcher cancel_watcher;

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

                NatT_TranId = WebSocketHelper.GenRandom(8).ToUInt64();
                Now = CreatedTick = Time.Tick64;

                MyKey = WebSocketHelper.GenRandom(CommonKeySize);
                YourKey = WebSocketHelper.GenRandom(CommonKeySize);

                Udp = s;

                Nb = new NonBlockSocket(Udp.Client, cancel_watcher.CancelToken);

                s.Client.Bind(new IPEndPoint(ip, 0));
                MyEndPoint = (IPEndPoint)s.Client.LocalEndPoint;

                IsIPv6 = ip.AddressFamily == AddressFamily.InterNetworkV6;
                if (IsIPv6) NoNatT = true;

                NextIv = WebSocketHelper.GenRandom(PacketIvSize);
                do
                {
                    MyCookie = WebSocketHelper.GenRandom(4).ToUInt();
                }
                while (MyCookie == 0);

                do
                {
                    YourCookie = WebSocketHelper.GenRandom(4).ToUInt();
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
                dummy_ip = new IPAddress(new byte[] { 11, WebSocketHelper.GenRandom(1)[0], WebSocketHelper.GenRandom(1)[0], WebSocketHelper.GenRandom(1)[0] });
            }
            string hostname = VpnSession.GetNatTServerHostName(dummy_ip);

            while (cancel_watcher.CancelToken.IsCancellationRequested == false)
            {
                try
                {
                    var hostent = await Dns.GetHostEntryAsync(hostname + "a");
                    IPAddress ip = hostent.AddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork).First();

                    NatT_IP = ip;
                    NatT_IP_Changed = true;

                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }

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
                        WriteLine(ex.ToString());
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
                if (WebSocketHelper.Aead_ChaCha20Poly1305_Ietf_Decrypt(buf, buf, YourKey, iv, null) == false)
                {
                    throw new ApplicationException("Aead_ChaCha20Poly1305_Ietf_Decrypt failed.");
                }
                buf = buf.Slice(0, buf.Length - PacketMacSize);
            }

            uint cookie = buf.WalkReadUInt();

            if (cookie != MyCookie)
            {
                throw new ApplicationException("cookie != MyCookie");
            }

            long my_tick = (long)buf.WalkReadUInt64();
            long your_tick = (long)buf.WalkReadUInt64();
            ushort inner_size = buf.WalkReadUShort();
            byte compress_flag = buf.WalkReadByte();
            if (compress_flag != 0) throw new ApplicationException("compress_flag != 0");

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

            ret = new UdpPacket(inner_data.ToArray(), null);

            if (LastSetSrcIpAndPortTick < LastRecvYourTick)
            {
                LastSetSrcIpAndPortTick = LastRecvYourTick;
                YourEndPoint = src;
            }

            if (LastRecvMyTick != 0)
            {
                if ((LastRecvMyTick + WindowSizeMSec) >= Now)
                {
                    LastRecvMyTick = Now;
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

        public void Send(Memory<byte> data)
        {
            Buf buf = new Buf();

            if (PlainTextMode == false)
            {

            }
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

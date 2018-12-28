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
        public const int CommonKeySize = 128;
        public const long NatT_GetIP_Interval_Initial = 5 * 1000;
        public const long NatT_GetIP_Interval_Max = 150 * 1000;

        bool NoNatT;
        long Now;
        byte[] MyKey;
        byte[] YourKey;
        UdpClient Udp;
        IPEndPoint MyEndPoint;
        IPEndPoint YourEndPoint;
        IPEndPoint YourEndPoint2;
        bool IsIPv6;
        byte[] TmpBuf = new byte[TmpBufSize];
        long LastRecvYourTick;
        long LastRecvMyTick;
        Queue<byte[]> RecvBlockQueue = new Queue<byte[]>();
        bool PlainTextMode;
        long LastSetSrcIpAndPortTick;
        long LastRecvTick;
        long NextSendKeepAlive;
        byte[] NextIv;
        uint MyCookie;
        uint YourCookie;
        bool Inited;
        int Mss;
        int MaxUdpPacketSize;
        object NatT_Lock = new object();
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
        ulong FirstStableReceiveTick;
        CancelWatcher cancel_watcher;

        public UdpAccel(IPAddress ip = null, bool no_nat_t = false, CancellationToken cancel = default(CancellationToken))
        {
            if (ip == null) ip = IPAddress.Any;
            cancel_watcher = new CancelWatcher(cancel);
            UdpClient s = new UdpClient(ip.AddressFamily);
            try
            {
                s.ExclusiveAddressUse = true;
                s.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);

                NoNatT = no_nat_t;

                NatT_TranId = WebSocketHelper.GenRandom(8).ToInt64();
                Now = CreatedTick = Time.Tick64;

                MyKey = WebSocketHelper.GenRandom(CommonKeySize);
                YourKey = WebSocketHelper.GenRandom(CommonKeySize);

                Udp = s;
                s.Client.Bind(new IPEndPoint(ip, 0));
                MyEndPoint = (IPEndPoint)s.Client.LocalEndPoint;

                IsIPv6 = ip.AddressFamily == AddressFamily.InterNetworkV6;
                if (IsIPv6) NoNatT = true;

                NextIv = WebSocketHelper.GenRandom(PacketIvSize);
                do
                {
                    MyCookie = WebSocketHelper.GenRandom(4).ToInt();
                }
                while (MyCookie == 0);

                do
                {
                    YourCookie = WebSocketHelper.GenRandom(4).ToInt();
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
    }
}

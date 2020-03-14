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
using SoftEther.WebSocket.Helper;
using System.Security.Cryptography;

#pragma warning disable CS0162, CS1998

namespace SoftEther.VpnClient
{
    public class VpnError : ApplicationException
    {
        public string ErrorCode { get; }
        public string ErrorMessage { get; }
        public VpnError(string errorCode, string errorMessage) : base($"Error ({errorCode}): {errorMessage}")
        {
            this.ErrorCode = errorCode;
            this.ErrorMessage = errorMessage;
        }
    }

    public class VpnProxySetting
    {
    }

    public class VpnHostSetting
    {
        public string Hostname = "";
        public int Port = 443;
    }

    public class VpnSessionDetails
    {
        public int TimeoutConnect = 15 * 1000;
        public int TimeoutComm = 15 * 1000;
        public bool UseUdpAcceleration = true;
    }

    public class VpnSessionSetting
    {
        public VpnHostSetting Host = new VpnHostSetting();
        public VpnProxySetting Proxy = new VpnProxySetting();
        public VpnSessionDetails Details = new VpnSessionDetails();
    }

    public enum VpnSessionEventType
    {
        Init,
        SessionStarted,
        SessionConnectingToServer,
        SessionNegotiating,
        SessionEstablished,
        SessionStopped,
        Error,
    }

    public class VpnSessionEventArgs
    {
        public VpnSessionEventType EventType;
        public Exception Error;

        public VpnSessionEventArgs(VpnSessionEventType type, Exception err = null)
        {
            this.EventType = type;
            this.Error = err;
        }
    }

    public class VpnSessionNotify
    {
        public event EventHandler<VpnSessionEventArgs> VpnEventHandler;

        public VpnSessionNotify(EventHandler<VpnSessionEventArgs> handler)
        {
            this.VpnEventHandler = handler;
        }

        public void Fire(object sender, VpnSessionEventArgs e)
        {
            this.VpnEventHandler(sender, e);
        }
    }

    public class VpnSocket : IDisposable
    {
        public VpnSessionSetting Setting { get; }

        TcpClient tcpClient;
        Stream sock;
        SslStream ssl;
        WebSocketStream ws;

        public IPEndPoint LocalEndPoint { get; private set; }
        public IPEndPoint RemoteEndPoint { get; private set; }

        CancelWatcher cancelWatcher;
        CancellationToken cancel;

        public int TimeoutComm
        {
            get => this.Setting.Details.TimeoutComm;
            set
            {
                this.Setting.Details.TimeoutComm = value;
                this.ws.TimeoutComm = value;
            }
        }

        public VpnSocket(VpnSessionSetting setting, CancellationToken cancel)
        {
            this.Setting = setting.ClonePublics();
            this.cancelWatcher = new CancelWatcher(cancel);
            this.cancel = this.cancelWatcher.CancelToken;
        }

        static Boolean CheckCert(Object sender, X509Certificate certificate,
            X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public async Task ConnectAsync()
        {
            tcpClient = new TcpClient();

            tcpClient.NoDelay = true;
            tcpClient.LingerState = new LingerOption(false, 0);
            tcpClient.ExclusiveAddressUse = false;

            await tcpClient.ConnectAsync(Setting.Host.Hostname, Setting.Host.Port, timeout: Setting.Details.TimeoutConnect, cancel: this.cancel);

            this.LocalEndPoint = (IPEndPoint)tcpClient.Client.LocalEndPoint;
            this.RemoteEndPoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint;

            sock = tcpClient.GetStream();

            ssl = new SslStream(sock, true, CheckCert);

            await WebSocketHelper.DoAsyncWithTimeout(
                mainProc: async c =>
                {
                    await ssl.AuthenticateAsClientAsync(Setting.Host.Hostname);
                    return 0;
                },
                cancelProc: () => ssl.DisposeSafe(),
                timeout: Setting.Details.TimeoutConnect,
                cancel: this.cancel);

            ws = new WebSocketStream(ssl, this.cancel);
            ws.TimeoutOpen = ws.TimeoutComm = Setting.Details.TimeoutComm;

            await ws.OpenAsync($"wss://{Setting.Host.Hostname}/mvpn");
        }

        public async Task SendAsync(byte[] buffer, int pos, int size)
        {
            await ws.WriteAsyncWithTimeout(buffer, pos, size,
                timeout: Setting.Details.TimeoutComm,
                cancel: this.cancel);
        }

        public async Task<byte[]> RecvAsync(int maxSize, bool readAll = false)
        {
            return await ws.ReadAsyncWithTimeout(maxSize,
                timeout: Setting.Details.TimeoutComm,
                cancel: this.cancel,
                readAll: readAll);
        }

        public async Task<int> RecvAsync(byte[] buffer, int pos, int size, bool readAll = false)
        {
            int ret = await ws.ReadAsyncWithTimeout(buffer, pos, size,
                timeout: Setting.Details.TimeoutComm,
                cancel: this.cancel,
                readAll: readAll);

            return ret;
        }

        public async Task SendJsonAsync(object jsonData)
        {
            string jsonString = jsonData.ObjectToJson();
            byte[] jsonByte = Encoding.UTF8.GetBytes(jsonString);
            Buf buf = new Buf();
            buf.WriteShort((ushort)jsonByte.Length);
            buf.Write(jsonByte);
            byte[] sendData = buf.ByteData;
            await SendAsync(sendData, 0, sendData.Length);
        }

        public async Task<T> RecvJsonAsync<T>(bool noErrorCheck = false)
            where T : VpnJsonResponse
        {
            ushort size = (await RecvAsync(2, true)).GetUInt16();
            byte[] data = await RecvAsync(size, true);
            string jsonString = Encoding.UTF8.GetString(data);

            T ret = jsonString.JsonToObject<T>();

            if (noErrorCheck == false)
            {
                if (string.IsNullOrEmpty(ret.Error_str) == false && ret.Error_str.StartsWith("e_", StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new VpnError(ret.Error_str, ret.ErrorMessage_Utf);
                }
            }

            return ret;
        }

        Once DisposeFlag;
        public void Dispose()
        {
            if (DisposeFlag.IsFirstCall())
            {
                ws.DisposeSafe();
                ssl.DisposeSafe();
                sock.DisposeSafe();
                tcpClient.DisposeSafe();
                cancelWatcher.DisposeSafe();
            }
        }
    }

    static class VpnProtocolConsts
    {
        public const uint PacketMagicNumber = 0xCAFEBEEF;
        public const int MaxBufferingPacketSize = (1600 * 1600);
    }

    enum VpnProtocolPacketType
    {
        Ethernet = 0,
        IPv4 = 1,
        HeartBeat = 254,
    }

    class VpnJsonClientHello
    {
        public uint MvpnProtocolVersion_u32 = 0;
        public byte[] Nonce_bin = null;
        public string Implementation_str = null;
        public string NetworkName_str = null;
        public uint HeartBeatInterval_u32 = 0;
        public uint DisconnectTimeout_u32 = 0;

        public bool UseUdpAcceleration_bool = false;
        public string UdpAccelerationClientIp_ip = null;
        public uint UdpAccelerationClientPort_u32 = 0;
        public byte[] UdpAccelerationClientKey_bin = null;

        public bool L3HelperIPv4Enable_bool = false;
        public string L3HelperIPv4AddressType_str = null;
        public string L3HelperIPv4Address_ip = null;
        public string L3HelperIPv4SubnetMask_ip = null;
        public string L3HelperIPv4Gateway_ip = null;
    }

    class VpnJsonServerHello : VpnJsonResponse
    {
        public uint MvpnProtocolVersion_u32 = 0;
        public byte[] Nonce_bin = null;
        public string Implementation_str = null;
        public string SupportedAuthMethod_str = null;
    }

    class VpnJsonClientAuth
    {
        public string AuthMethod_str = null;
        public string AuthUsername_str = null;
        public string AuthPlainPassword_str = null;
    }

    class VpnJsonServerAuthResponse : VpnJsonResponse
    {
        public uint RetryAllowedCount_u32 = 0;
        public uint HeartBeatInterval_u32 = 0;
        public uint DisconnectTimeout_u32 = 0;
        public string NetworkName_str = null;

        public bool UseUdpAcceleration_bool = false;
        public string UdpAccelerationServerIp_ip = null;
        public uint UdpAccelerationServerPort_u32 = 0;
        public byte[] UdpAccelerationServerKey_bin = null;
        public uint UdpAccelerationServerCookie_u32 = 0;
        public uint UdpAccelerationClientCookie_u32 = 0;

        public bool L3HelperIPv4Enable_bool = false;
        public string L3HelperIPv4AddressType_str = null;
        public string L3HelperIPv4Address_ip = null;
        public string L3HelperIPv4SubnetMask_ip = null;
        public string L3HelperIPv4Gateway_ip = null;
        public string L3HelperIPv4DnsServer1_ip = null;
        public string L3HelperIPv4DnsServer2_ip = null;
        public string L3HelperIPv4WinsServer1_ip = null;
        public string L3HelperIPv4WinsServer2_ip = null;
        public string L3HelperIPv4PushedStaticRoutes_str = null;
    }

    public class VpnJsonResponse
    {
        public string Error_str = null;
        public string ErrorMessage_Utf = null;
    }

    struct VpnConnectionInfo
    {
        public VpnSessionSetting Settings;
        public IPEndPoint LocalEndPoint;
        public IPEndPoint RemoteEndPoint;
        public string ServerImplementation;
        public string NetworkName;
        public string ClientUserName;
        public DateTimeOffset ConnectedDateTime;
        public int HeartBeatInterval;
        public int DisconnectTimeout;
        public DateTimeOffset LastCommunicationDateTime;
        //public bool UdpAccelerationIsEnabled;
        //public bool UdpAccelerationIsActuallyUsed;
    }

    class VpnConnection : IDisposable
    {
        public VpnSession Session { get; }
        public VpnSessionSetting Setting { get; }
        VpnSocket Socket;
        CancellationToken CancelByParent;
        CancelWatcher CancelWatcher;
        TimeoutDetector TimeoutDetector = null;

        VpnConnectionInfo info = new VpnConnectionInfo();
        public VpnConnectionInfo Info { get => info; }

        VpnVirtualNetworkAdapter NetworkAdapter;

        UdpAccel UdpAccel = null;

        public VpnConnection(VpnSession session, CancellationToken cancel, VpnVirtualNetworkAdapter networkAdapter)
        {
            Session = session;
            Setting = session.Setting.ClonePublics();
            CancelByParent = cancel;
            CancelWatcher = new CancelWatcher(CancelByParent);
            NetworkAdapter = networkAdapter;

            info.Settings = this.Setting.ClonePublics();
        }

        public async Task ConnectAsync()
        {
            UdpAccel udpAccel = null;

            VpnSocket s = new VpnSocket(Session.Setting, CancelWatcher.CancelToken);
            this.Socket = s;

            await s.ConnectAsync();

            info.LocalEndPoint = s.LocalEndPoint;
            info.RemoteEndPoint = s.RemoteEndPoint;

            Session.NotifyEvent(new VpnSessionEventArgs(VpnSessionEventType.SessionNegotiating));

            if (Setting.Details.UseUdpAcceleration)
            {
                udpAccel = new UdpAccel(Socket.LocalEndPoint.Address, true, false, CancelWatcher.CancelToken);
            }

            // Phase 1: Send a Client Hello JSON message
            VpnJsonClientHello clientHello = new VpnJsonClientHello()
            {
                MvpnProtocolVersion_u32 = 100,
                Nonce_bin = WebSocketHelper.Rand(128),
                Implementation_str = ".NET Test Client",
                NetworkName_str = "DEFAULT",
                L3HelperIPv4Enable_bool = true,
                L3HelperIPv4AddressType_str = "dynamic",
            };

            if (udpAccel != null)
            {
                clientHello.UseUdpAcceleration_bool = true;
                clientHello.UdpAccelerationClientIp_ip = udpAccel.MyEndPoint.Address.ToString();
                clientHello.UdpAccelerationClientPort_u32 = (uint)udpAccel.MyEndPoint.Port;
                clientHello.UdpAccelerationClientKey_bin = udpAccel.MyKey;
            }

            await s.SendJsonAsync(clientHello);

            // Phase 2: Receive a Server Hello JSON message
            VpnJsonServerHello serverHello = await s.RecvJsonAsync<VpnJsonServerHello>();

            info.ServerImplementation = serverHello.Implementation_str;

            // Phase 3: Send a Client Auth JSON message
            VpnJsonClientAuth clientAuth = new VpnJsonClientAuth()
            {
                AuthMethod_str = "password_plain",
                AuthPlainPassword_str = "microsoft",
                AuthUsername_str = "test",
            };

            info.ClientUserName = clientAuth.AuthUsername_str;

            await s.SendJsonAsync(clientAuth);

            // Phase 4: Receive a Server Auth Response JSON message
            VpnJsonServerAuthResponse serverAuthResponse = await s.RecvJsonAsync<VpnJsonServerAuthResponse>();

            serverAuthResponse.Print();

            if (serverAuthResponse.UseUdpAcceleration_bool == false)
            {
                if (udpAccel != null)
                {
                    udpAccel.DisposeSafe();
                    udpAccel = null;
                }
            }
            else
            {
                if (udpAccel != null)
                {
                    udpAccel.InitClient(serverAuthResponse.UdpAccelerationServerKey_bin,
                        new IPEndPoint(IPAddress.Parse(serverAuthResponse.UdpAccelerationServerIp_ip), (int)serverAuthResponse.UdpAccelerationServerPort_u32),
                        serverAuthResponse.UdpAccelerationServerCookie_u32,
                        serverAuthResponse.UdpAccelerationClientCookie_u32);

                    UdpAccel = udpAccel;
                }
            }

            info.NetworkName = serverAuthResponse.NetworkName_str;
            info.HeartBeatInterval = (int)serverAuthResponse.HeartBeatInterval_u32;
            info.DisconnectTimeout = (int)serverAuthResponse.DisconnectTimeout_u32;
            info.ConnectedDateTime = DateTimeOffset.Now;

            s.TimeoutComm = Timeout.Infinite;
        }


        Exception disconnectReason = null;

        void SetDisconnectReason(Exception e)
        {
            if (disconnectReason == null) disconnectReason = e;
        }

        async Task sessionUdpAccelRecvLoopAsync()
        {
            try
            {
                while (CancelWatcher.CancelToken.IsCancellationRequested == false)
                {
                    try
                    {
                        UdpAccel.Poll();
                        UdpAccel.SendFinish();

                        while (UdpAccel.RecvBlockQueue.TryDequeue(out Datagram pkt))
                        {
                            this.info.LastCommunicationDateTime = DateTimeOffset.Now;

                            var packetData = pkt.Data;
                            VpnProtocolPacketType packetType = (VpnProtocolPacketType)pkt.Flag;

                            if (packetType == VpnProtocolPacketType.Ethernet || packetType == VpnProtocolPacketType.IPv4)
                            {
                                await this.NetworkAdapter.OnPacketsReceived(VnState, VnParam, new VpnPacket[] { new VpnPacket(packetType, packetData) });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Dbg.Where(ex.ToString());
                    }

                    await WebSocketHelper.WaitObjectsAsync(
                        cancels: new CancellationToken[] { this.CancelWatcher.CancelToken },
                        events: new AsyncAutoResetEvent[] { UdpAccel.EventRecvReady },
                        timeout: 256);
                }
            }
            catch (Exception ex) { SetDisconnectReason(ex); }
        }

        async Task sessionSockRecvLoopAsync()
        {
            try
            {
                while (CancelWatcher.CancelToken.IsCancellationRequested == false)
                {
                    byte[] signatureBin = await this.Socket.RecvAsync(4, true);
                    if (signatureBin.GetUInt32() != 0xCAFEBEEF)
                    {
                        throw new ApplicationException("VPN protocol error.");
                    }

                    byte[] packetTypeBin = await this.Socket.RecvAsync(1, true);
                    VpnProtocolPacketType packetType = (VpnProtocolPacketType)packetTypeBin[0];

                    byte[] packetDataSizeBin = await this.Socket.RecvAsync(2, true);
                    ushort packetDataSize = packetDataSizeBin.GetUInt16();

                    byte[] packetData = await this.Socket.RecvAsync(packetDataSize, true);

                    this.TimeoutDetector.Keep();
                    this.info.LastCommunicationDateTime = DateTimeOffset.Now;

                    if (packetType == VpnProtocolPacketType.Ethernet || packetType == VpnProtocolPacketType.IPv4)
                    {
                        await this.NetworkAdapter.OnPacketsReceived(VnState, VnParam, new VpnPacket[] { new VpnPacket(packetType, packetData) });
                    }
                }
            }
            catch (Exception ex) { SetDisconnectReason(ex); }
        }

        Fifo SockSendFifo = new Fifo();
        AsyncAutoResetEvent SockSendEvent = new AsyncAutoResetEvent();

        object VnState = null;
        VpnVirtualNetworkAdapterParam VnParam = null;

        async Task sessionSockGenerateHeartBeatLoopAsync()
        {
            while (CancelWatcher.CancelToken.IsCancellationRequested == false)
            {
                await WebSocketHelper.WaitObjectsAsync(
                    cancels: new CancellationToken[] { this.CancelWatcher.CancelToken },
                    timeout: Info.HeartBeatInterval);

                Buf buf = new Buf();
                buf.WriteInt(VpnProtocolConsts.PacketMagicNumber);
                buf.WriteByte((byte)VpnProtocolPacketType.HeartBeat);
                buf.WriteShort(0);

                lock (SockSendFifo)
                {
                    if (SockSendFifo.Size <= VpnProtocolConsts.MaxBufferingPacketSize)
                        SockSendFifo.Write(buf);
                }

                SockSendEvent.Set();
            }
        }

        async Task sessionSockSendLoopAsync()
        {
            while (CancelWatcher.CancelToken.IsCancellationRequested == false)
            {
                byte[] sendBuf;

                lock (SockSendFifo)
                {
                    sendBuf = SockSendFifo.Read();
                }

                if (sendBuf.Length >= 1)
                {
                    await this.Socket.SendAsync(sendBuf, 0, sendBuf.Length);
                    Dbg.Where($"sock send: {sendBuf.Length}");
                }

                await WebSocketHelper.WaitObjectsAsync(
                    cancels: new CancellationToken[] { this.CancelWatcher.CancelToken },
                    events: new AsyncAutoResetEvent[] { SockSendEvent });
            }
        }

        public void VnPacketsSendCallback(VpnPacket[] packets)
        {
            int num = 0;

            if (UdpAccel != null) UdpAccel.SetTick(Time.Tick64);

            foreach (VpnPacket p in packets)
            {
                if (UdpAccel != null && UdpAccel.IsSendReady(true))
                {
                    UdpAccel.Send(p.Data, (byte)p.Type);
                }
                else
                {
                    Buf buf = new Buf();
                    buf.WriteInt(VpnProtocolConsts.PacketMagicNumber);
                    buf.WriteByte((byte)p.Type);
                    buf.WriteShort((ushort)p.Data.Length);
                    buf.Write(p.Data.Span.ToArray());

                    lock (SockSendFifo)
                    {
                        if (SockSendFifo.Size <= VpnProtocolConsts.MaxBufferingPacketSize)
                            SockSendFifo.Write(buf);
                    }
                }

                num++;
            }

            if (num >= 1)
            {
                SockSendEvent.Set();
                if (UdpAccel != null) UdpAccel.SendFinish();
            }
        }

        Once DisconnectedByVn;
        public void VnDisconnectCallback()
        {
            if (DisconnectedByVn.IsFirstCall())
            {
                SetDisconnectReason(new ApplicationException("The user disconnected the VPN session."));
                this.CancelWatcher.Cancel();
            }
        }

        public async Task SessionMainLoopAsync()
        {
            VnParam = new VpnVirtualNetworkAdapterParam(this.Session, VnPacketsSendCallback, VnDisconnectCallback);
            VnState = await NetworkAdapter.OnConnected(VnParam);

            AsyncCleanuperLady lady = new AsyncCleanuperLady();

            try
            {
                this.TimeoutDetector = new TimeoutDetector(lady, Info.DisconnectTimeout,
                    callback: (x) =>
                    {
                        SetDisconnectReason(new ApplicationException("VPN transport communication timed out."));
                        return false;
                    });

                Task sockRecvTask = sessionSockRecvLoopAsync();
                Task sockGenerateHeartBeatTask = sessionSockGenerateHeartBeatLoopAsync();
                Task sockSendTask = sessionSockSendLoopAsync();

                Task udpAccelRecvTask = UdpAccel != null ? sessionUdpAccelRecvLoopAsync() : null;

                await WebSocketHelper.WaitObjectsAsync(
                    tasks: new Task[] { sockRecvTask, sockGenerateHeartBeatTask, sockSendTask, udpAccelRecvTask, this.TimeoutDetector.TaskWaitMe },
                    cancels: new CancellationToken[] { this.CancelWatcher.CancelToken }
                    );

                this.CancelWatcher.Cancel();

                await sockRecvTask.TryWaitAsync();
                await sockGenerateHeartBeatTask.TryWaitAsync();
                await sockSendTask.TryWaitAsync();
                await udpAccelRecvTask.TryWaitAsync();

                this.TimeoutDetector.DisposeSafe();

                if (this.disconnectReason != null) throw this.disconnectReason;
            }
            finally
            {
                await lady;
                await NetworkAdapter.OnDisconnected(VnState, VnParam).TryWaitAsync();
                VnState = null;
            }
        }

        Once DisposeFlag;
        public void Dispose()
        {
            if (DisposeFlag.IsFirstCall())
            {
                UdpAccel.DisposeSafe();
                Socket.DisposeSafe();
                CancelWatcher.DisposeSafe();
                TimeoutDetector.DisposeSafe();
            }
        }
    }

    delegate void VpnVirtualNetworkAdapterPacketsSendDelegate(VpnPacket[] packets);
    delegate void VpnVirtualNetworkAdapterDisconnectDelegate();

    class VpnVirtualNetworkAdapterParam
    {
        public readonly VpnSession Session;
        public readonly VpnVirtualNetworkAdapterPacketsSendDelegate SendPackets;
        public readonly VpnVirtualNetworkAdapterDisconnectDelegate Disconnect;

        public VpnVirtualNetworkAdapterParam(VpnSession session,
            VpnVirtualNetworkAdapterPacketsSendDelegate send, VpnVirtualNetworkAdapterDisconnectDelegate disconnect)
        {
            this.Session = session;
            this.SendPackets = send;
            this.Disconnect = disconnect;
        }
    }

    class VpnPacket
    {
        public readonly VpnProtocolPacketType Type;
        public readonly Memory<byte> Data;

        public VpnPacket(VpnProtocolPacketType type, Memory<byte> data)
        {
            this.Type = type;
            this.Data = data;
        }
    }

    abstract class VpnVirtualNetworkAdapter
    {
        public abstract Task<object> OnConnected(VpnVirtualNetworkAdapterParam param);
        public abstract Task OnDisconnected(object state, VpnVirtualNetworkAdapterParam param);
        public abstract Task OnPacketsReceived(object state, VpnVirtualNetworkAdapterParam param, VpnPacket[] packets);
    }

    class VpnSession
    {
        public VpnSessionSetting Setting { get; }
        public VpnSessionNotify Notify { get; }

        VpnVirtualNetworkAdapter NetworkAdapter;
        VpnConnection CurrentConnection = null;

        public VpnSession(VpnSessionSetting setting, VpnSessionNotify notify, VpnVirtualNetworkAdapter networkAdapter)
        {
            this.Setting = setting.ClonePublics();
            this.Notify = notify;
            this.NetworkAdapter = networkAdapter;

            this.NotifyEvent(new VpnSessionEventArgs(VpnSessionEventType.Init));
        }

        public VpnConnectionInfo ConnectionInfo { get => CurrentConnection.Info; }

        internal void NotifyEvent(VpnSessionEventArgs e)
        {
            this.Notify.Fire(this, e);
        }

        Task MainLoopTask = null;
        CancellationTokenSource Cancel = null;

        AsyncLock LockObj = new AsyncLock();

        public async Task StartAsync()
        {
            using (await LockObj.LockWithAwait())
            {
                if (MainLoopTask == null)
                {
                    Cancel = new CancellationTokenSource();
                    MainLoopTask = mainLoop();
                }
                await Task.CompletedTask;
            }
        }

        public async Task StopAsync()
        {
            using (await LockObj.LockWithAwait())
            {
                if (MainLoopTask == null) return;
                Cancel.Cancel();
                await MainLoopTask;
                MainLoopTask = null;
                Cancel = null;
            }
        }

        public static string GetNatTServerHostName(IPAddress ip)
        {
            string tmp;
            if (ip != null && ip.AddressFamily == AddressFamily.InterNetwork)
            {
                tmp = WebSocketHelper.ByteToHex(new SHA1Managed().ComputeHash(ip.GetAddressBytes()));
            }
            else
            {
                tmp = WebSocketHelper.ByteToHex(WebSocketHelper.Rand(4));
            }
            tmp = tmp.ToLowerInvariant();
            return $"x{tmp[2]}.x{tmp[3]}.servers.nat-traversal.softether-network.net.";
        }

        async Task mainLoop()
        {
            NotifyEvent(new VpnSessionEventArgs(VpnSessionEventType.SessionStarted));
            try
            {
                await DoVpnSession();
            }
            catch (Exception ex)
            {
                var aex = ex as AggregateException;
                if (aex != null)
                {
                    ex = aex.Flatten().InnerExceptions[0];
                }
                NotifyEvent(new VpnSessionEventArgs(VpnSessionEventType.Error, ex));
            }
            finally
            {
                NotifyEvent(new VpnSessionEventArgs(VpnSessionEventType.SessionStopped));
            }
        }

        async Task DoVpnSession()
        {
            using (VpnConnection c = new VpnConnection(this, this.Cancel.Token, this.NetworkAdapter))
            {
                CurrentConnection = c;
                try
                {
                    NotifyEvent(new VpnSessionEventArgs(VpnSessionEventType.SessionConnectingToServer));

                    await c.ConnectAsync();

                    NotifyEvent(new VpnSessionEventArgs(VpnSessionEventType.SessionEstablished));

                    await c.SessionMainLoopAsync();
                }
                finally
                {
                    CurrentConnection = null;
                }
            }
        }
    }
}

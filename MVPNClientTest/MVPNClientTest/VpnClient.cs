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
        public VpnError(string error_code, string error_message) : base($"Error ({error_code}): {error_message}")
        {
            this.ErrorCode = error_code;
            this.ErrorMessage = error_message;
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

        TcpClient tcp_client;
        Stream sock;
        SslStream ssl;
        WebSocketStream ws;

        public IPEndPoint LocalEndPoint { get; private set; }
        public IPEndPoint RemoteEndPoint { get; private set; }

        CancelWatcher cancel_watcher;
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
            this.cancel_watcher = new CancelWatcher(cancel);
            this.cancel = this.cancel_watcher.CancelToken;
        }

        static Boolean check_cert(Object sender, X509Certificate certificate,
            X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public async Task ConnectAsync()
        {
            tcp_client = new TcpClient();

            tcp_client.NoDelay = true;
            tcp_client.LingerState = new LingerOption(false, 0);
            tcp_client.ExclusiveAddressUse = false;

            await tcp_client.ConnectAsync(Setting.Host.Hostname, Setting.Host.Port, timeout: Setting.Details.TimeoutConnect, cancel: this.cancel);

            this.LocalEndPoint = (IPEndPoint)tcp_client.Client.LocalEndPoint;
            this.RemoteEndPoint = (IPEndPoint)tcp_client.Client.RemoteEndPoint;

            sock = tcp_client.GetStream();

            ssl = new SslStream(sock, true, check_cert);

            await WebSocketHelper.DoAsyncWithTimeout<int>(
                main_proc: async c =>
                {
                    await ssl.AuthenticateAsClientAsync(Setting.Host.Hostname);
                    return 0;
                },
                cancel_proc: () => ssl.DisposeSafe(),
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

        public async Task<byte[]> RecvAsync(int max_size, bool read_all = false)
        {
            return await ws.ReadAsyncWithTimeout(max_size,
                timeout: Setting.Details.TimeoutComm,
                cancel: this.cancel,
                read_all: read_all);
        }

        public async Task<int> RecvAsync(byte[] buffer, int pos, int size, bool read_all = false)
        {
            int ret = await ws.ReadAsyncWithTimeout(buffer, pos, size,
                timeout: Setting.Details.TimeoutComm,
                cancel: this.cancel,
                read_all: read_all);

            return ret;
        }

        public async Task SendJsonAsync(object json_data)
        {
            string json_string = json_data.ObjectToJson();
            byte[] json_byte = Encoding.UTF8.GetBytes(json_string);
            Buf buf = new Buf();
            buf.WriteShort((ushort)json_byte.Length);
            buf.Write(json_byte);
            byte[] send_data = buf.ByteData;
            await SendAsync(send_data, 0, send_data.Length);
        }

        public async Task<T> RecvJsonAsync<T>(bool no_error_check = false)
            where T : VpnJsonResponse
        {
            ushort size = (await RecvAsync(2, true)).GetUInt16();
            byte[] data = await RecvAsync(size, true);
            string json_string = Encoding.UTF8.GetString(data);

            T ret = json_string.JsonToObject<T>();

            if (no_error_check == false)
            {
                if (string.IsNullOrEmpty(ret.Error_str) == false && ret.Error_str.StartsWith("e_", StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new VpnError(ret.Error_str, ret.ErrorMessage_utf);
                }
            }

            return ret;
        }

        Once dispose_flag;
        public void Dispose()
        {
            if (dispose_flag.IsFirstCall())
            {
                ws.DisposeSafe();
                ssl.DisposeSafe();
                sock.DisposeSafe();
                tcp_client.DisposeSafe();
                cancel_watcher.DisposeSafe();
            }
        }
    }

    public static class VpnProtocolConsts
    {
        public const uint PacketMagicNumber = 0xCAFEBEEF;
        public const int MaxBufferingPacketSize = (1600 * 1600);
    }

    public enum VpnProtocolPacketType
    {
        Ethernet = 0,
        IPv4 = 1,
        HeartBeat = 254,
    }

    public class VpnJsonClientHello
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

    public class VpnJsonServerHello : VpnJsonResponse
    {
        public uint MvpnProtocolVersion_u32 = 0;
        public byte[] Nonce_bin = null;
        public string Implementation_str = null;
        public string SupportedAuthMethod_str = null;
    }

    public class VpnJsonClientAuth
    {
        public string AuthMethod_str = null;
        public string AuthUsername_str = null;
        public string AuthPlainPassword_str = null;
    }

    public class VpnJsonServerAuthResponse : VpnJsonResponse
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
        public string ErrorMessage_utf = null;
    }

    public struct VpnConnectionInfo
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
        public bool UdpAccelerationIsEnabled;
        public bool UdpAccelerationIsActuallyUsed;
    }

    public class VpnConnection : IDisposable
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

        public VpnConnection(VpnSession session, CancellationToken cancel, VpnVirtualNetworkAdapter network_adapter)
        {
            Session = session;
            Setting = session.Setting.ClonePublics();
            CancelByParent = cancel;
            CancelWatcher = new CancelWatcher(CancelByParent);
            NetworkAdapter = network_adapter;

            info.Settings = this.Setting.ClonePublics();
        }

        public async Task ConnectAsync()
        {
            UdpAccel udp_accel = null;

            VpnSocket s = new VpnSocket(Session.Setting, CancelWatcher.CancelToken);
            this.Socket = s;

            await s.ConnectAsync();

            info.LocalEndPoint = s.LocalEndPoint;
            info.RemoteEndPoint = s.RemoteEndPoint;

            Session.notify_event(new VpnSessionEventArgs(VpnSessionEventType.SessionNegotiating));

            if (Setting.Details.UseUdpAcceleration)
            {
                udp_accel = new UdpAccel(Socket.LocalEndPoint.Address, true, false, CancelWatcher.CancelToken);
            }

            // Phase 1: Send a Client Hello JSON message
            VpnJsonClientHello client_hello = new VpnJsonClientHello()
            {
                MvpnProtocolVersion_u32 = 100,
                Nonce_bin = WebSocketHelper.Rand(128),
                Implementation_str = ".NET Test Client",
                NetworkName_str = "DEFAULT",
            };

            if (udp_accel != null)
            {
                client_hello.UseUdpAcceleration_bool = true;
                client_hello.UdpAccelerationClientIp_ip = udp_accel.MyEndPoint.Address.ToString();
                client_hello.UdpAccelerationClientPort_u32 = (uint)udp_accel.MyEndPoint.Port;
                client_hello.UdpAccelerationClientKey_bin = udp_accel.MyKey;
            }

            await s.SendJsonAsync(client_hello);

            // Phase 2: Receive a Server Hello JSON message
            VpnJsonServerHello server_hello = await s.RecvJsonAsync<VpnJsonServerHello>();

            info.ServerImplementation = server_hello.Implementation_str;

            // Phase 3: Send a Client Auth JSON message
            VpnJsonClientAuth client_auth = new VpnJsonClientAuth()
            {
                AuthMethod_str = "password_plain",
                AuthPlainPassword_str = "microsoft",
                AuthUsername_str = "test",
            };

            info.ClientUserName = client_auth.AuthUsername_str;

            await s.SendJsonAsync(client_auth);

            // Phase 4: Receive a Server Auth Response JSON message
            VpnJsonServerAuthResponse server_auth_response = await s.RecvJsonAsync<VpnJsonServerAuthResponse>();

            if (server_auth_response.UseUdpAcceleration_bool == false)
            {
                if (udp_accel != null)
                {
                    udp_accel.DisposeSafe();
                    udp_accel = null;
                }
            }
            else
            {
                if (udp_accel != null)
                {
                    udp_accel.InitClient(server_auth_response.UdpAccelerationServerKey_bin,
                        new IPEndPoint(IPAddress.Parse(server_auth_response.UdpAccelerationServerIp_ip), (int)server_auth_response.UdpAccelerationServerPort_u32),
                        server_auth_response.UdpAccelerationServerCookie_u32,
                        server_auth_response.UdpAccelerationClientCookie_u32);

                    UdpAccel = udp_accel;
                }
            }

            info.NetworkName = server_auth_response.NetworkName_str;
            info.HeartBeatInterval = (int)server_auth_response.HeartBeatInterval_u32;
            info.DisconnectTimeout = (int)server_auth_response.DisconnectTimeout_u32;
            info.ConnectedDateTime = DateTimeOffset.Now;

            s.TimeoutComm = Timeout.Infinite;
        }


        Exception disconnect_reason = null;

        void set_disconnect_reason(Exception e)
        {
            if (disconnect_reason == null) disconnect_reason = e;
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

                        while (UdpAccel.RecvBlockQueue.TryDequeue(out UdpPacket pkt))
                        {
                            this.info.LastCommunicationDateTime = DateTimeOffset.Now;

                            var packet_data = pkt.Data;
                            VpnProtocolPacketType packet_type = (VpnProtocolPacketType)pkt.Flag;

                            if (packet_type == VpnProtocolPacketType.Ethernet || packet_type == VpnProtocolPacketType.IPv4)
                            {
                                await this.NetworkAdapter.OnPacketsReceived(VnState, VnParam, new VpnPacket[] { new VpnPacket(packet_type, packet_data) });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Dbg.Where(ex.ToString());
                    }

                    await WebSocketHelper.WaitObjectsAsync(
                        cancels: new CancellationToken[] { this.CancelWatcher.CancelToken },
                        auto_events: new AsyncAutoResetEvent[] { UdpAccel.EventRecvReady },
                        timeout: 256);
                }
            }
            catch (Exception ex) { set_disconnect_reason(ex); }
        }

        async Task sessionSockRecvLoopAsync()
        {
            try
            {
                while (CancelWatcher.CancelToken.IsCancellationRequested == false)
                {
                    byte[] signature_bin = await this.Socket.RecvAsync(4, true);
                    if (signature_bin.GetUInt32() != 0xCAFEBEEF)
                    {
                        throw new ApplicationException("VPN protocol error.");
                    }

                    byte[] packet_type_bin = await this.Socket.RecvAsync(1, true);
                    VpnProtocolPacketType packet_type = (VpnProtocolPacketType)packet_type_bin[0];

                    byte[] packet_data_size_bin = await this.Socket.RecvAsync(2, true);
                    ushort packet_data_size = packet_data_size_bin.GetUInt16();

                    byte[] packet_data = await this.Socket.RecvAsync(packet_data_size, true);

                    this.TimeoutDetector.Keep();
                    this.info.LastCommunicationDateTime = DateTimeOffset.Now;

                    if (packet_type == VpnProtocolPacketType.Ethernet || packet_type == VpnProtocolPacketType.IPv4)
                    {
                        await this.NetworkAdapter.OnPacketsReceived(VnState, VnParam, new VpnPacket[] { new VpnPacket(packet_type, packet_data) });
                    }
                }
            }
            catch (Exception ex) { set_disconnect_reason(ex); }
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
                byte[] send_buf;

                lock (SockSendFifo)
                {
                    send_buf = SockSendFifo.Read();
                }

                if (send_buf.Length >= 1)
                {
                    await this.Socket.SendAsync(send_buf, 0, send_buf.Length);
                    Dbg.Where($"sock send: {send_buf.Length}");
                }

                await WebSocketHelper.WaitObjectsAsync(
                    cancels: new CancellationToken[] { this.CancelWatcher.CancelToken },
                    auto_events: new AsyncAutoResetEvent[] { SockSendEvent });
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
                    buf.Write(p.Data);

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
                set_disconnect_reason(new ApplicationException("The user disconnected the VPN session."));
                this.CancelWatcher.Cancel();
            }
        }

        public async Task SessionMainLoopAsync()
        {
            VnParam = new VpnVirtualNetworkAdapterParam(this.Session, VnPacketsSendCallback, VnDisconnectCallback);
            VnState = await NetworkAdapter.OnConnected(VnParam);

            try
            {
                this.TimeoutDetector = new TimeoutDetector(Info.DisconnectTimeout,
                    callme: () =>
                    {
                        set_disconnect_reason(new ApplicationException("VPN transport communication timed out."));
                    });

                Task sock_recv_task = sessionSockRecvLoopAsync();
                Task sock_generate_heartbeat_task = sessionSockGenerateHeartBeatLoopAsync();
                Task sock_send_task = sessionSockSendLoopAsync();

                Task udp_accel_recv_task = UdpAccel != null ? sessionUdpAccelRecvLoopAsync() : null;

                await WebSocketHelper.WaitObjectsAsync(
                    tasks: new Task[] { sock_recv_task, sock_generate_heartbeat_task, sock_send_task, udp_accel_recv_task, this.TimeoutDetector.TaskWaitMe },
                    cancels: new CancellationToken[] { this.CancelWatcher.CancelToken }
                    );

                this.CancelWatcher.Cancel();

                await sock_recv_task.TryWaitAsync();
                await sock_generate_heartbeat_task.TryWaitAsync();
                await sock_send_task.TryWaitAsync();
                await udp_accel_recv_task.TryWaitAsync();

                this.TimeoutDetector.DisposeSafe();

                if (this.disconnect_reason != null) throw this.disconnect_reason;
            }
            finally
            {
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

    public delegate void VpnVirtualNetworkAdapterPacketsSendDelegate(VpnPacket[] packets);
    public delegate void VpnVirtualNetworkAdapterDisconnectDelegate();

    public class VpnVirtualNetworkAdapterParam
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

    public class VpnPacket
    {
        public readonly VpnProtocolPacketType Type;
        public readonly byte[] Data;

        public VpnPacket(VpnProtocolPacketType type, byte[] data)
        {
            this.Type = type;
            this.Data = data;
        }
    }

    public abstract class VpnVirtualNetworkAdapter
    {
        public abstract Task<object> OnConnected(VpnVirtualNetworkAdapterParam param);
        public abstract Task OnDisconnected(object state, VpnVirtualNetworkAdapterParam param);
        public abstract Task OnPacketsReceived(object state, VpnVirtualNetworkAdapterParam param, VpnPacket[] packets);
    }

    public class VpnSession
    {
        public VpnSessionSetting Setting { get; }
        public VpnSessionNotify Notify { get; }

        VpnVirtualNetworkAdapter NetworkAdapter;
        VpnConnection CurrentConnection = null;

        public VpnSession(VpnSessionSetting setting, VpnSessionNotify notify, VpnVirtualNetworkAdapter network_adapter)
        {
            this.Setting = setting.ClonePublics();
            this.Notify = notify;
            this.NetworkAdapter = network_adapter;

            this.notify_event(new VpnSessionEventArgs(VpnSessionEventType.Init));
        }

        public VpnConnectionInfo ConnectionInfo { get => CurrentConnection.Info; }

        internal void notify_event(VpnSessionEventArgs e)
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
                    MainLoopTask = main_loop();
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

        async Task main_loop()
        {
            notify_event(new VpnSessionEventArgs(VpnSessionEventType.SessionStarted));
            try
            {
                await do_vpn_session();
            }
            catch (Exception ex)
            {
                var aex = ex as AggregateException;
                if (aex != null)
                {
                    ex = aex.Flatten().InnerExceptions[0];
                }
                notify_event(new VpnSessionEventArgs(VpnSessionEventType.Error, ex));
            }
            finally
            {
                notify_event(new VpnSessionEventArgs(VpnSessionEventType.SessionStopped));
            }
        }

        async Task do_vpn_session()
        {
            using (VpnConnection c = new VpnConnection(this, this.Cancel.Token, this.NetworkAdapter))
            {
                CurrentConnection = c;
                try
                {
                    notify_event(new VpnSessionEventArgs(VpnSessionEventType.SessionConnectingToServer));

                    await c.ConnectAsync();

                    notify_event(new VpnSessionEventArgs(VpnSessionEventType.SessionEstablished));

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

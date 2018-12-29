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
            ushort size = (await RecvAsync(2, true)).ToUShort();
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
        Disconnect = 255,
    }

    public class VpnJsonClientHello
    {
        public uint MvpnProtocolVersion_u32 = 0;
        public byte[] Nonce_bin = null;
        public string Implementation_str = null;
        public string NetworkName_str = null;
        public uint HeartBeatInterval_u32 = 0;
        public uint DisconnectTimeout_u32 = 0;
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
    }

    public class VpnConnection : IDisposable
    {
        public VpnSession Session { get; }
        public VpnSessionSetting Setting { get; }
        VpnSocket socket;
        CancellationToken cancel_by_parent;
        CancelWatcher cancel_watcher;
        TimeoutDetector timeout_detector = null;

        VpnConnectionInfo info = new VpnConnectionInfo();
        public VpnConnectionInfo Info { get => info; }

        VpnVirtualNetworkAdapter network_adapter;

        public VpnConnection(VpnSession session, CancellationToken cancel, VpnVirtualNetworkAdapter network_adapter)
        {
            this.Session = session;
            this.Setting = session.Setting.ClonePublics();
            this.cancel_by_parent = cancel;
            this.cancel_watcher = new CancelWatcher(this.cancel_by_parent);
            this.network_adapter = network_adapter;

            info.Settings = this.Setting.ClonePublics();
        }

        public async Task ConnectAsync()
        {
            VpnSocket s = new VpnSocket(Session.Setting, this.cancel_watcher.CancelToken);
            this.socket = s;

            await s.ConnectAsync();

            info.LocalEndPoint = s.LocalEndPoint;
            info.RemoteEndPoint = s.RemoteEndPoint;

            Session.notify_event(new VpnSessionEventArgs(VpnSessionEventType.SessionNegotiating));

            // Phase 1: Send a Client Hello JSON message
            VpnJsonClientHello client_hello = new VpnJsonClientHello()
            {
                MvpnProtocolVersion_u32 = 100,
                Nonce_bin = WebSocketHelper.Rand(128),
                Implementation_str = ".NET Test Client",
                NetworkName_str = "DEFAULT",
            };

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

        async Task sessionSockRecvLoopAsync()
        {
            try
            {
                while (cancel_watcher.CancelToken.IsCancellationRequested == false)
                {
                    byte[] signature_bin = await this.socket.RecvAsync(4, true);
                    if (signature_bin.ToUInt() != 0xCAFEBEEF)
                    {
                        throw new ApplicationException("VPN protocol error.");
                    }

                    byte[] packet_type_bin = await this.socket.RecvAsync(1, true);
                    VpnProtocolPacketType packet_type = (VpnProtocolPacketType)packet_type_bin[0];

                    byte[] packet_data_size_bin = await this.socket.RecvAsync(2, true);
                    ushort packet_data_size = packet_data_size_bin.ToUShort();

                    byte[] packet_data = await this.socket.RecvAsync(packet_data_size, true);

                    if (packet_type == VpnProtocolPacketType.Disconnect)
                    {
                        throw new ApplicationException("VPN session is disconnected.");
                    }

                    Dbg.Where($"packet_size = {packet_data_size}");

                    this.timeout_detector.Keep();
                    this.info.LastCommunicationDateTime = DateTimeOffset.Now;

                    if (packet_type == VpnProtocolPacketType.Ethernet)
                    {
                        await this.network_adapter.OnPacketsReceived(vn_state, vn_param, new VpnPacket[] { new VpnPacket(packet_type, packet_data) });
                    }
                }
            }
            catch (Exception ex) { set_disconnect_reason(ex); }
        }

        Fifo sock_send_fifo = new Fifo();
        AsyncAutoResetEvent sock_send_event = new AsyncAutoResetEvent();

        object vn_state = null;
        VpnVirtualNetworkAdapterParam vn_param = null;

        async Task sessionSockGenerateHeartBeatLoopAsync()
        {
            while (cancel_watcher.CancelToken.IsCancellationRequested == false)
            {
                await WebSocketHelper.WaitObjectsAsync(
                    cancels: new CancellationToken[] { this.cancel_watcher.CancelToken },
                    timeout: Info.HeartBeatInterval);

                Buf buf = new Buf();
                buf.WriteInt(VpnProtocolConsts.PacketMagicNumber);
                buf.WriteByte((byte)VpnProtocolPacketType.HeartBeat);
                buf.WriteShort(0);

                lock (sock_send_fifo)
                {
                    if (sock_send_fifo.Size <= VpnProtocolConsts.MaxBufferingPacketSize)
                        sock_send_fifo.Write(buf);
                }
                sock_send_event.Set();

                //Buf eth = new Buf();
                //eth.Write(new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, });
                //eth.Write(new byte[] { 0x00, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, });
                //eth.WriteShort(0x0800);
                //eth.Write(WebSocketHelper.GenRandom(32));

                //buf = new Buf();
                //buf.WriteInt(VpnProtocolConsts.PacketMagicNumber);
                //buf.WriteByte((byte)VpnProtocolPacketType.Ethernet);
                //byte[] eth_data = eth.ByteData;
                //buf.WriteShort((ushort)eth_data.Length);
                //buf.Write(eth_data);

                //lock (sock_send_fifo)
                //{
                //    sock_send_fifo.Write(buf);
                //}
                //sock_send_event.Set();
            }
        }

        async Task sessionSockSendLoopAsync()
        {
            while (cancel_watcher.CancelToken.IsCancellationRequested == false)
            {
                byte[] send_buf;

                lock (sock_send_fifo)
                {
                    send_buf = sock_send_fifo.Read();
                }

                if (send_buf.Length >= 1)
                {
                    await this.socket.SendAsync(send_buf, 0, send_buf.Length);
                    Dbg.Where($"send: {send_buf.Length}");
                }

                await WebSocketHelper.WaitObjectsAsync(
                    cancels: new CancellationToken[] { this.cancel_watcher.CancelToken },
                    auto_events: new AsyncAutoResetEvent[] { sock_send_event });
            }
        }

        public void VnPacketsSendCallback(VpnPacket[] packets)
        {
            int num = 0;

            foreach (VpnPacket p in packets)
            {
                Buf buf = new Buf();
                buf.WriteInt(VpnProtocolConsts.PacketMagicNumber);
                buf.WriteByte((byte)p.Type);
                buf.WriteShort((ushort)p.Data.Length);
                buf.Write(p.Data);

                lock (sock_send_fifo)
                {
                    if (sock_send_fifo.Size <= VpnProtocolConsts.MaxBufferingPacketSize) 
                        sock_send_fifo.Write(buf);
                }

                num++;

                //Dbg.Where(sock_send_fifo.Size.ToString());
            }

            if (num >= 1) sock_send_event.Set();
        }

        Once disconnected_by_vn;
        public void VnDisconnectCallback()
        {
            if (disconnected_by_vn.IsFirstCall())
            {
                set_disconnect_reason(new ApplicationException("The user disconnected the VPN session."));
                this.cancel_watcher.Cancel();
            }
        }

        public async Task SessionMainLoopAsync()
        {
            vn_param = new VpnVirtualNetworkAdapterParam(this.Session, VnPacketsSendCallback, VnDisconnectCallback);
            vn_state = await network_adapter.OnConnected(vn_param);

            try
            {
                this.timeout_detector = new TimeoutDetector(Info.DisconnectTimeout,
                    callme: () =>
                    {
                        set_disconnect_reason(new ApplicationException("VPN transport communication timed out."));
                    });

                Task sock_recv_task = sessionSockRecvLoopAsync();
                Task sock_generate_heartbeat_task = sessionSockGenerateHeartBeatLoopAsync();
                Task sock_send_task = sessionSockSendLoopAsync();

                await WebSocketHelper.WaitObjectsAsync(
                    tasks: new Task[] { sock_recv_task, sock_generate_heartbeat_task, sock_send_task, this.timeout_detector.TaskWaitMe },
                    cancels: new CancellationToken[] { this.cancel_watcher.CancelToken }
                    );

                this.cancel_watcher.Cancel();

                await sock_recv_task.TryWaitAsync();
                await sock_generate_heartbeat_task.TryWaitAsync();
                await sock_send_task.TryWaitAsync();

                this.timeout_detector.DisposeSafe();

                if (this.disconnect_reason != null) throw this.disconnect_reason;
            }
            finally
            {
                await network_adapter.OnDisconnected(vn_state, vn_param).TryWaitAsync();
                vn_state = null;
            }
        }

        Once dispose_flag;
        public void Dispose()
        {
            if (dispose_flag.IsFirstCall())
            {
                this.socket.DisposeSafe();
                this.cancel_watcher.DisposeSafe();
                this.timeout_detector.DisposeSafe();
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

        VpnVirtualNetworkAdapter network_adapter;
        VpnConnection current_connection = null;

        public VpnSession(VpnSessionSetting setting, VpnSessionNotify notify, VpnVirtualNetworkAdapter network_adapter)
        {
            this.Setting = setting.ClonePublics();
            this.Notify = notify;
            this.network_adapter = network_adapter;

            this.notify_event(new VpnSessionEventArgs(VpnSessionEventType.Init));
        }

        public VpnConnectionInfo ConnectionInfo { get => current_connection.Info; }

        internal void notify_event(VpnSessionEventArgs e)
        {
            this.Notify.Fire(this, e);
        }

        Task main_loop_task = null;
        CancellationTokenSource cancel = null;

        AsyncLock LockObj = new AsyncLock();

        public async Task StartAsync()
        {
            using (await LockObj.LockWithAwait())
            {
                if (main_loop_task == null)
                {
                    cancel = new CancellationTokenSource();
                    main_loop_task = main_loop();
                }
                await Task.CompletedTask;
            }
        }

        public async Task StopAsync()
        {
            using (await LockObj.LockWithAwait())
            {
                if (main_loop_task == null) return;
                cancel.Cancel();
                await main_loop_task;
                main_loop_task = null;
                cancel = null;
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
            using (VpnConnection c = new VpnConnection(this, this.cancel.Token, this.network_adapter))
            {
                current_connection = c;
                try
                {
                    notify_event(new VpnSessionEventArgs(VpnSessionEventType.SessionConnectingToServer));

                    await c.ConnectAsync();

                    notify_event(new VpnSessionEventArgs(VpnSessionEventType.SessionEstablished));

                    await c.SessionMainLoopAsync();
                }
                finally
                {
                    current_connection = null;
                }
            }
        }
    }
}

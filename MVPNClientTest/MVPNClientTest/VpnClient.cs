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

        CancellationToken cancel;

        public VpnSocket(VpnSessionSetting setting, CancellationToken cancel)
        {
            this.Setting = setting.ClonePublics();
            this.cancel = cancel;
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
            where T: VpnJsonResponse
        {
            ushort size = (await RecvAsync(2, true)).ToShort();
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
            }
        }
    }

    public class VpnJsonClientHello
    {
        public uint MvpnProtocolVersion_u32 = 0;
        public byte[] Nonce_bin = null;
        public string Implementation_str = null;
        public string NetworkName_str = null;
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
        public uint RetryAllowedCount = 0;
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

    public class VpnConnection : IDisposable
    {
        public VpnSession Session { get; }
        public VpnSessionSetting Setting { get; }
        VpnSocket socket;
        CancellationToken cancel;

        public VpnConnection(VpnSession session, CancellationToken cancel)
        {
            this.Session = session;
            this.Setting = session.Setting.ClonePublics();
            this.cancel = cancel;
        }

        public async Task ConnectAsync()
        {
            VpnSocket s = new VpnSocket(Session.Setting, this.cancel);
            this.socket = s;

            await s.ConnectAsync();

            Session.notify_event(new VpnSessionEventArgs(VpnSessionEventType.SessionNegotiating));

            // Phase 1: Send a Client Hello JSON message
            VpnJsonClientHello client_hello = new VpnJsonClientHello()
            {
                MvpnProtocolVersion_u32 = 100,
                Nonce_bin = WebSocketHelper.GenRandom(128),
                Implementation_str = ".NET Test Client",
                NetworkName_str = "DEFAULT",
            };

            await s.SendJsonAsync(client_hello);

            // Phase 2: Receive a Server Hello JSON message
            VpnJsonServerHello server_hello = await s.RecvJsonAsync<VpnJsonServerHello>();

            // Phase 3: Send a Client Auth JSON message
            VpnJsonClientAuth client_auth = new VpnJsonClientAuth()
            {
                AuthMethod_str = "password_plain",
                AuthPlainPassword_str = "microsoft",
                AuthUsername_str = "test",
            };

            await s.SendJsonAsync(client_auth);

            // Phase 4: Receive a Server Auth Response JSON message
            VpnJsonServerAuthResponse server_auth_response = await s.RecvJsonAsync<VpnJsonServerAuthResponse>();
        }

        Once dispose_flag;
        public void Dispose()
        {
            if (dispose_flag.IsFirstCall())
            {
                this.socket.DisposeSafe();
            }
        }
    }

    public class VpnSession
    {
        public VpnSessionSetting Setting { get; }
        public VpnSessionNotify Notify { get; }

        public VpnSession(VpnSessionSetting setting, VpnSessionNotify notify)
        {
            this.Setting = setting.ClonePublics();
            this.Notify = notify;

            this.notify_event(new VpnSessionEventArgs(VpnSessionEventType.Init));
        }

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
            using (VpnConnection c = new VpnConnection(this, this.cancel.Token))
            {
                notify_event(new VpnSessionEventArgs(VpnSessionEventType.SessionConnectingToServer));

                await c.ConnectAsync();
            }
        }
    }
}


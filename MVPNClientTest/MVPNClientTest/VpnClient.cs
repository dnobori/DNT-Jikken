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
    public enum VpnErrorCode
    {
        ERR_NO_ERROR = 0, // No error
        ERR_CONNECT_FAILED = 1, // Connection to the server has failed
        ERR_SERVER_IS_NOT_VPN = 2, // The destination server is not a VPN server
        ERR_DISCONNECTED = 3, // The connection has been interrupted
        ERR_PROTOCOL_ERROR = 4, // Protocol error
        ERR_CLIENT_IS_NOT_VPN = 5, // Connecting client is not a VPN client
        ERR_USER_CANCEL = 6, // User cancel
        ERR_AUTHTYPE_NOT_SUPPORTED = 7, // Specified authentication method is not supported
        ERR_HUB_NOT_FOUND = 8, // The HUB does not exist
        ERR_AUTH_FAILED = 9, // Authentication failure
        ERR_HUB_STOPPING = 10, // HUB is stopped
        ERR_SESSION_REMOVED = 11, // Session has been deleted
        ERR_ACCESS_DENIED = 12, // Access denied
        ERR_SESSION_TIMEOUT = 13, // Session times out
        ERR_INVALID_PROTOCOL = 14, // Protocol is invalid
        ERR_TOO_MANY_CONNECTION = 15, // Too many connections
        ERR_HUB_IS_BUSY = 16, // Too many sessions of the HUB
        ERR_PROXY_CONNECT_FAILED = 17, // Connection to the proxy server fails
        ERR_PROXY_ERROR = 18, // Proxy Error
        ERR_PROXY_AUTH_FAILED = 19, // Failed to authenticate on the proxy server
        ERR_TOO_MANY_USER_SESSION = 20, // Too many sessions of the same user
        ERR_LICENSE_ERROR = 21, // License error
        ERR_DEVICE_DRIVER_ERROR = 22, // Device driver error
        ERR_INTERNAL_ERROR = 23, // Internal error
        ERR_SECURE_DEVICE_OPEN_FAILED = 24, // The secure device cannot be opened
        ERR_SECURE_PIN_LOGIN_FAILED = 25, // PIN code is incorrect
        ERR_SECURE_NO_CERT = 26, // Specified certificate is not stored
        ERR_SECURE_NO_PRIVATE_KEY = 27, // Specified private key is not stored
        ERR_SECURE_CANT_WRITE = 28, // Write failure
        ERR_OBJECT_NOT_FOUND = 29, // Specified object can not be found
        ERR_VLAN_ALREADY_EXISTS = 30, // Virtual LAN card with the specified name already exists
        ERR_VLAN_INSTALL_ERROR = 31, // Specified virtual LAN card cannot be created
        ERR_VLAN_INVALID_NAME = 32, // Specified name of the virtual LAN card is invalid
        ERR_NOT_SUPPORTED = 33, // Unsupported
        ERR_ACCOUNT_ALREADY_EXISTS = 34, // Account already exists
        ERR_ACCOUNT_ACTIVE = 35, // Account is operating
        ERR_ACCOUNT_NOT_FOUND = 36, // Specified account doesn't exist
        ERR_ACCOUNT_INACTIVE = 37, // Account is offline
        ERR_INVALID_PARAMETER = 38, // Parameter is invalid
        ERR_SECURE_DEVICE_ERROR = 39, // Error has occurred in the operation of the secure device
        ERR_NO_SECURE_DEVICE_SPECIFIED = 40, // Secure device is not specified
        ERR_VLAN_IS_USED = 41, // Virtual LAN card in use by account
        ERR_VLAN_FOR_ACCOUNT_NOT_FOUND = 42, // Virtual LAN card of the account can not be found
        ERR_VLAN_FOR_ACCOUNT_USED = 43, // Virtual LAN card of the account is already in use
        ERR_VLAN_FOR_ACCOUNT_DISABLED = 44, // Virtual LAN card of the account is disabled
        ERR_INVALID_VALUE = 45, // Value is invalid
        ERR_NOT_FARM_CONTROLLER = 46, // Not a farm controller
        ERR_TRYING_TO_CONNECT = 47, // Attempting to connect
        ERR_CONNECT_TO_FARM_CONTROLLER = 48, // Failed to connect to the farm controller
        ERR_COULD_NOT_HOST_HUB_ON_FARM = 49, // A virtual HUB on farm could not be created
        ERR_FARM_MEMBER_HUB_ADMIN = 50, // HUB cannot be managed on a farm member
        ERR_NULL_PASSWORD_LOCAL_ONLY = 51, // Accepting only local connections for an empty password
        ERR_NOT_ENOUGH_RIGHT = 52, // Right is insufficient
        ERR_LISTENER_NOT_FOUND = 53, // Listener can not be found
        ERR_LISTENER_ALREADY_EXISTS = 54, // Listener already exists
        ERR_NOT_FARM_MEMBER = 55, // Not a farm member
        ERR_CIPHER_NOT_SUPPORTED = 56, // Encryption algorithm is not supported
        ERR_HUB_ALREADY_EXISTS = 57, // HUB already exists
        ERR_TOO_MANY_HUBS = 58, // Too many HUBs
        ERR_LINK_ALREADY_EXISTS = 59, // Link already exists
        ERR_LINK_CANT_CREATE_ON_FARM = 60, // The link can not be created on the server farm
        ERR_LINK_IS_OFFLINE = 61, // Link is off-line
        ERR_TOO_MANY_ACCESS_LIST = 62, // Too many access list
        ERR_TOO_MANY_USER = 63, // Too many users
        ERR_TOO_MANY_GROUP = 64, // Too many Groups
        ERR_GROUP_NOT_FOUND = 65, // Group can not be found
        ERR_USER_ALREADY_EXISTS = 66, // User already exists
        ERR_GROUP_ALREADY_EXISTS = 67, // Group already exists
        ERR_USER_AUTHTYPE_NOT_PASSWORD = 68, // Authentication method of the user is not a password authentication
        ERR_OLD_PASSWORD_WRONG = 69, // The user does not exist or the old password is wrong
        ERR_LINK_CANT_DISCONNECT = 73, // Cascade session cannot be disconnected
        ERR_ACCOUNT_NOT_PRESENT = 74, // Not completed configure the connection to the VPN server
        ERR_ALREADY_ONLINE = 75, // It is already online
        ERR_OFFLINE = 76, // It is offline
        ERR_NOT_RSA_1024 = 77, // The certificate is not RSA 1024bit
        ERR_SNAT_CANT_DISCONNECT = 78, // SecureNAT session cannot be disconnected
        ERR_SNAT_NEED_STANDALONE = 79, // SecureNAT works only in stand-alone HUB
        ERR_SNAT_NOT_RUNNING = 80, // SecureNAT function is not working
        ERR_SE_VPN_BLOCK = 81, // Stopped by PacketiX VPN Block
        ERR_BRIDGE_CANT_DISCONNECT = 82, // Bridge session can not be disconnected
        ERR_LOCAL_BRIDGE_STOPPING = 83, // Bridge function is stopped
        ERR_LOCAL_BRIDGE_UNSUPPORTED = 84, // Bridge feature is not supported
        ERR_CERT_NOT_TRUSTED = 85, // Certificate of the destination server can not be trusted
        ERR_PRODUCT_CODE_INVALID = 86, // Product code is different
        ERR_VERSION_INVALID = 87, // Version is different
        ERR_CAPTURE_DEVICE_ADD_ERROR = 88, // Adding capture device failure
        ERR_VPN_CODE_INVALID = 89, // VPN code is different
        ERR_CAPTURE_NOT_FOUND = 90, // Capture device can not be found
        ERR_LAYER3_CANT_DISCONNECT = 91, // Layer-3 session cannot be disconnected
        ERR_LAYER3_SW_EXISTS = 92, // L3 switch of the same already exists
        ERR_LAYER3_SW_NOT_FOUND = 93, // Layer-3 switch can not be found
        ERR_INVALID_NAME = 94, // Name is invalid
        ERR_LAYER3_IF_ADD_FAILED = 95, // Failed to add interface
        ERR_LAYER3_IF_DEL_FAILED = 96, // Failed to delete the interface
        ERR_LAYER3_IF_EXISTS = 97, // Interface that you specified already exists
        ERR_LAYER3_TABLE_ADD_FAILED = 98, // Failed to add routing table
        ERR_LAYER3_TABLE_DEL_FAILED = 99, // Failed to delete the routing table
        ERR_LAYER3_TABLE_EXISTS = 100, // Routing table entry that you specified already exists
        ERR_BAD_CLOCK = 101, // Time is queer
        ERR_LAYER3_CANT_START_SWITCH = 102, // The Virtual Layer 3 Switch can not be started
        ERR_CLIENT_LICENSE_NOT_ENOUGH = 103, // Client connection licenses shortage
        ERR_BRIDGE_LICENSE_NOT_ENOUGH = 104, // Bridge connection licenses shortage
        ERR_SERVER_CANT_ACCEPT = 105, // Not Accept on the technical issues
        ERR_SERVER_CERT_EXPIRES = 106, // Destination VPN server has expired
        ERR_MONITOR_MODE_DENIED = 107, // Monitor port mode was rejected
        ERR_BRIDGE_MODE_DENIED = 108, // Bridge-mode or Routing-mode was rejected
        ERR_IP_ADDRESS_DENIED = 109, // Client IP address is denied
        ERR_TOO_MANT_ITEMS = 110, // Too many items
        ERR_MEMORY_NOT_ENOUGH = 111, // Out of memory
        ERR_OBJECT_EXISTS = 112, // Object already exists
        ERR_FATAL = 113, // A fatal error occurred
        ERR_SERVER_LICENSE_FAILED = 114, // License violation has occurred on the server side
        ERR_SERVER_INTERNET_FAILED = 115, // Server side is not connected to the Internet
        ERR_CLIENT_LICENSE_FAILED = 116, // License violation occurs on the client side
        ERR_BAD_COMMAND_OR_PARAM = 117, // Command or parameter is invalid
        ERR_INVALID_LICENSE_KEY = 118, // License key is invalid
        ERR_NO_VPN_SERVER_LICENSE = 119, // There is no valid license for the VPN Server
        ERR_NO_VPN_CLUSTER_LICENSE = 120, // There is no cluster license
        ERR_NOT_ADMINPACK_SERVER = 121, // Not trying to connect to a server with the Administrator Pack license
        ERR_NOT_ADMINPACK_SERVER_NET = 122, // Not trying to connect to a server with the Administrator Pack license (for .NET)
        ERR_BETA_EXPIRES = 123, // Destination Beta VPN Server has expired
        ERR_BRANDED_C_TO_S = 124, // Branding string of connection limit is different (Authentication on the server side)
        ERR_BRANDED_C_FROM_S = 125, // Branding string of connection limit is different (Authentication for client-side)
        ERR_AUTO_DISCONNECTED = 126, // VPN session is disconnected for a certain period of time has elapsed
        ERR_CLIENT_ID_REQUIRED = 127, // Client ID does not match
        ERR_TOO_MANY_USERS_CREATED = 128, // Too many created users
        ERR_SUBSCRIPTION_IS_OLDER = 129, // Subscription expiration date Is earlier than the build date of the VPN Server
        ERR_ILLEGAL_TRIAL_VERSION = 130, // Many trial license is used continuously
        ERR_NAT_T_TWO_OR_MORE = 131, // There are multiple servers in the back of a global IP address in the NAT-T connection
        ERR_DUPLICATE_DDNS_KEY = 132, // DDNS host key duplicate
        ERR_DDNS_HOSTNAME_EXISTS = 133, // Specified DDNS host name already exists
        ERR_DDNS_HOSTNAME_INVALID_CHAR = 134, // Characters that can not be used for the host name is included
        ERR_DDNS_HOSTNAME_TOO_LONG = 135, // Host name is too long
        ERR_DDNS_HOSTNAME_IS_EMPTY = 136, // Host name is not specified
        ERR_DDNS_HOSTNAME_TOO_SHORT = 137, // Host name is too short
        ERR_MSCHAP2_PASSWORD_NEED_RESET = 138, // Necessary that password is changed
        ERR_DDNS_DISCONNECTED = 139, // Communication to the dynamic DNS server is disconnected
        ERR_SPECIAL_LISTENER_ICMP_ERROR = 140, // The ICMP socket can not be opened
        ERR_SPECIAL_LISTENER_DNS_ERROR = 141, // Socket for DNS port can not be opened
        ERR_OPENVPN_IS_NOT_ENABLED = 142, // OpenVPN server feature is not enabled
        ERR_NOT_SUPPORTED_AUTH_ON_OPENSOURCE = 143, // It is the type of user authentication that are not supported in the open source version
        ERR_VPNGATE = 144, // Operation on VPN Gate Server is not available
        ERR_VPNGATE_CLIENT = 145, // Operation on VPN Gate Client is not available
        ERR_VPNGATE_INCLIENT_CANT_STOP = 146, // Can not be stopped if operating within VPN Client mode
        ERR_NOT_SUPPORTED_FUNCTION_ON_OPENSOURCE = 147, // It is a feature that is not supported in the open source version
        ERR_SUSPENDING = 148, // System is suspending
    }

    public class VpnError : ApplicationException
    {
        public VpnErrorCode ErrorCode { get; }
        public VpnError(VpnErrorCode code) : base(Enum.GetName(typeof(VpnErrorCode), code))
        {
            this.ErrorCode = code;
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

            await s.ConnectAsync();
            this.socket = s;

            Session.notify_event(new VpnSessionEventArgs(VpnSessionEventType.SessionNegotiating));

        }

        Once dispose_flag;
        public void Dispose()
        {
            if (dispose_flag.IsFirstCall())
            {
                socket.DisposeSafe();
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
                await do_once();
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

        async Task do_once()
        {
            using (VpnConnection c = new VpnConnection(this, this.cancel.Token))
            {
                notify_event(new VpnSessionEventArgs(VpnSessionEventType.SessionConnectingToServer));
                await c.ConnectAsync();
            }
        }
    }
}


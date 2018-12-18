using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Net.Http;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static System.Console;
using SoftEther.WebSocket;
using SoftEther.WebSocket.Helper;
using SoftEther.VpnClient;

#pragma warning disable CS0162

namespace MVPNClientTest
{
    class Program
    {
        static void Main(string[] args)
        {
            test0().Wait();
        }

        static async Task test0()
        {
            try
            {
                //await test_plain();
                //await test_ssl();
                await test_vpn();
            }
            catch (Exception ex)
            {
                WriteLine(ex.ToString());
            }
        }

        private static Boolean check_cert(Object sender, X509Certificate certificate,
            X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        static async Task test_plain()
        {
            string hostname = "echo.websocket.org";
            int port = 80;

            using (TcpClient tc = new TcpClient())
            {
                WriteLine("connecting.");
                await tc.ConnectAsync(hostname, port);

                WriteLine("connected.");

                using (NetworkStream st = tc.GetStream())
                {
                    using (WebSocketStream s = new WebSocketStream(st))
                    {
                        await s.OpenAsync("ws://echo.websocket.org");
                        WriteLine("opened.");

                        while (true)
                        {
                            string hello = "Hello World";
                            byte[] hello_bytes = hello.AsciiToByteArray();
                            await s.WriteAsync(hello_bytes.AsMemory());
                            WriteLine("Sent.");

                            byte[] recv_buffer = new byte[128];
                            int recv_ret = await s.ReadAsync(recv_buffer, 0, recv_buffer.Length);
                            WriteLine($"Recv: \"{recv_buffer.AsSpan(0, recv_ret).ToArray().ByteArrayToAscii()}\"");
                        }

                        Thread.Sleep(-1);
                    }
                }
            }
        }

        static async Task test_ssl()
        {
            string hostname = "echo.websocket.org";
            int port = 443;

            using (TcpClient tc = new TcpClient())
            {
                WriteLine("connecting.");
                await tc.ConnectAsync(hostname, port);

                WriteLine("connected.");

                using (NetworkStream st = tc.GetStream())
                {
                    using (SslStream ssl = new SslStream(st, false, check_cert))
                    {
                        WriteLine("start ssl");
                        await ssl.AuthenticateAsClientAsync(hostname);
                        WriteLine("end ssl");

                        using (WebSocketStream s = new WebSocketStream(ssl))
                        {
                            await s.OpenAsync("wss://echo.websocket.org");
                            WriteLine("opened.");

                            while (true)
                            {
                                string hello = "Hello World";
                                byte[] hello_bytes = hello.AsciiToByteArray();
                                await s.WriteAsync(hello_bytes.AsMemory());
                                WriteLine("Sent.");

                                byte[] recv_buffer = new byte[128];
                                int recv_ret = await s.ReadAsync(recv_buffer, 0, recv_buffer.Length);
                                WriteLine($"Recv: \"{recv_buffer.AsSpan(0, recv_ret).ToArray().ByteArrayToAscii()}\"");
                            }

                            Thread.Sleep(-1);
                        }
                    }
                }
            }
        }

        static async Task test_vpn()
        {
            VpnSessionSetting setting = new VpnSessionSetting()
            {
                Host = new VpnHostSetting()
                {
                    Hostname = "127.0.0.1",
                    Port = 443,
                },
                Proxy = new VpnProxySetting()
                {
                },
            };

            VpnSessionNotify notify = new VpnSessionNotify(Notify_VpnEventHandler);

            VpnSession sess = new VpnSession(setting, notify);

            await sess.StartAsync();

            Console.ReadLine();

            WriteLine("Stopping...");
            await sess.StopAsync();
            WriteLine("Stopped.");
        }

        private static void Notify_VpnEventHandler(object sender, VpnSessionEventArgs e)
        {
            Console.WriteLine($"{DateTime.Now}: {Enum.GetName(typeof(VpnSessionEventType), e.EventType)}");
            if (e.Error != null)
            {
                Console.WriteLine($"  Error: {e.Error}");
            }
        }
    }
}

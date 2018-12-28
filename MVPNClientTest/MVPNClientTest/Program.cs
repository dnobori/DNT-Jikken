﻿using System;
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

#pragma warning disable CS0162, CS1998

namespace MVPNClientTest
{
    class Program
    {
        static void Main(string[] args)
        {
            //test0().Wait();
            //nb_socket_tcp_test().Wait();
            nb_socket_udp_proc().Wait();
            //async_test2().LaissezFaire();
            //async_test1().Wait();
            //Thread.Sleep(-1);
        }

        static async Task nb_socket_udp_proc()
        {
            UdpClient uc = new UdpClient(AddressFamily.InterNetwork);
            uc.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
            Console.WriteLine($"port: {((IPEndPoint)uc.Client.LocalEndPoint).Port}");

            IPAddress server_ip = IPAddress.Parse("130.158.6.60");

            using (NonBlockSocket b = new NonBlockSocket(uc.Client))
            {
                long next_send = 0;
                while (b.IsDisconnected == false)
                {
                    long now = Time.Tick64;
                    if (next_send == 0 || next_send <= now)
                    {
                        next_send = now + 500;

                        lock (b.SendUdpQueue)
                        {
                            b.SendUdpQueue.Enqueue(new UdpPacket(new byte[] { (byte)'B' }, new IPEndPoint(server_ip, 5004)));
                        }

                        b.EventSendNow.Set();
                    }

                    lock (b.RecvUdpQueue)
                    {
                        while (b.RecvUdpQueue.Count >= 1)
                        {
                            UdpPacket pkt = b.RecvUdpQueue.Dequeue();
                            Dbg.Where($"recv: {pkt.Data.Length} {pkt.EndPoint}");
                        }
                    }

                    await WebSocketHelper.WaitObjectsAsync(
                        cancels: new CancellationToken[] { b.CancelToken },
                        auto_events: new AsyncAutoResetEvent[] { b.EventRecvReady, b.EventSendReady },
                        timeout: (int)(next_send - now));
                    Dbg.Where();
                }
                Dbg.Where("Disconnected.");
            }
        }

        static async Task nb_socket_tcp_proc(Socket s)
        {
            using (NonBlockSocket b = new NonBlockSocket(s))
            {
                while (b.IsDisconnected == false)
                {
                    byte[] data = null;
                    lock (b.RecvTcpFifo)
                    {
                        data = b.RecvTcpFifo.Read();
                    }
                    if (data.Length >= 1)
                    {
                        string str = Encoding.UTF8.GetString(data);
                        Console.Write(str);

                        lock (b.SendTcpFifo)
                        {
                            b.SendTcpFifo.Write(data);
                        }
                        b.EventSendNow.Set();
                    }
                    else
                    {
                        await b.EventRecvReady.WaitOneAsync(out _);
                    }
                }
                Dbg.Where("Disconnected.");
            }
        }

        static async Task nb_socket_tcp_test()
        {
            TcpListener t = new TcpListener(IPAddress.Any, 1);
            t.Start();

            while (true)
            {
                Socket s = await t.AcceptSocketAsync();

                nb_socket_tcp_proc(s).LaissezFaire();
            }
        }

        static async Task test0()
        {
            try
            {
                //await test_plain();
                //await test_ssl();
                //await test_vpn();
                await test_udpa();
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

        static async Task test_udpa()
        {
            UdpAccel a = new UdpAccel(IPAddress.Any, false);

            Console.ReadLine();

            a.Dispose();

            await Task.Yield();
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
            string hostname = "www.google.com";
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

                        string text = "GET / HTTP/1.1\r\nHOST: www.google.com\r\n\r\n";
                        byte[] bin = Encoding.UTF8.GetBytes(text);

                        //await ssl.WriteAsync(bin, 0, bin.Length);

                        for (int i = 0; i < bin.Length; i++)
                        {
                            await ssl.WriteAsyncWithTimeout(new byte[1] { bin[i] }, timeout: 1000);
                            WriteLine("send: " + i);
                        }

                        for (int i =0; ;i++)
                        {
                            byte[] ret = await ssl.ReadAsyncWithTimeout(1, read_all: true, timeout: 1000);
                            WriteLine("recv: " + i + " " + ret.Length);
                        }

                        WriteLine();
                    }
                }
            }
        }

        static async Task test_ssl__()
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

            if (true)
            {
                VpnSession sess = new VpnSession(setting, notify, new NetworkAdapterDummy());

                await sess.StartAsync();

                Console.ReadLine();

                WriteLine("Stopping...");
                await sess.StopAsync();
                WriteLine("Stopped.");

                await Task.Yield();
            }
            else
            {
                while (true)
                {
                    VpnSession sess = new VpnSession(setting, notify, new NetworkAdapterDummy());

                    await sess.StartAsync();

                    await Task.Delay(1000);

                    WriteLine("Stopping...");
                    await sess.StopAsync();
                    WriteLine("Stopped.");

                    await Task.Yield();

                    System.GC.Collect();
                }
            }
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

    public class NetworkAdapterDummy : VpnVirtualNetworkAdapter
    {
        VpnVirtualNetworkAdapterParam p;

        CancellationTokenSource cancel = new CancellationTokenSource();

        public override async Task<object> OnConnected(VpnVirtualNetworkAdapterParam param)
        {
            Console.WriteLine($"Connected.");

            cancel = new CancellationTokenSource();

            this.p = param;

            test().LaissezFaire();

            return new object();
        }

        public async Task test()
        {
            while (cancel.Token.IsCancellationRequested == false)
            {
                Buf eth = new Buf();
                eth.Write(new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, });
                eth.Write(new byte[] { 0x00, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, });
                eth.WriteShort(0x0800);
                eth.Write(WebSocketHelper.GenRandom(32));

                this.p.SendPackets(new VpnPacket[] { new VpnPacket(VpnProtocolPacketType.Ethernet, eth.ByteData) });

                await WebSocketHelper.WaitObjectsAsync(cancels: new CancellationToken[] { cancel.Token }, timeout: 100);
            }

            Dbg.Where();
        }

        public override async Task OnDisconnected(object state, VpnVirtualNetworkAdapterParam param)
        {
            Console.WriteLine($"Disconnected.");

            cancel.Cancel();
        }

        public override async Task OnPacketsReceived(object state, VpnVirtualNetworkAdapterParam param, VpnPacket[] packets)
        {
            Console.WriteLine($"recv packets: {packets[0].Data.Length}");
        }
    }
}

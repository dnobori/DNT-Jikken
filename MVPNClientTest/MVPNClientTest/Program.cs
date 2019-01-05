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

#pragma warning disable CS0162, CS1998

namespace MVPNClientTest
{
    class Program
    {
        static void Main(string[] args)
        {
            //TestStreamBuffer();
            BenchStreamBuffer();

            return;
            string s = "Hello";

            System.String s2 = s;

            test0().Wait();
            //nb_socket_tcp_test().Wait();
            //            nb_socket_udp_proc().Wait();
            //async_test2().LaissezFaire();
            //async_test1().Wait();
            //Thread.Sleep(-1);
        }

        static void BenchStreamBuffer()
        {
            FastStreamBuffer<byte> new_test_buf(int num = 1)
            {
                FastStreamBuffer<byte> ret = new FastStreamBuffer<byte>();
                for (int i = 0; i < num; i++)
                {
                    ret.Enqueue(new byte[] { 1, 2, 3, });
                    ret.Enqueue(new byte[] { 4, 5, 6, 7 });
                    ret.Enqueue(new byte[] { 8, 9, 10, 11, 12 });
                    ret.Enqueue(new byte[] { 13, 14, 15, 16, 17, 18 });
                }
                return ret;
            }

            MicroBenchmarkQueue q = new MicroBenchmarkQueue();

            q.Add(new MicroBenchmark<int>("InsertFirst", 1000,
                (x, iterations) =>
                {
                    List<int> rands = new List<int>();
                    for (int i = 0; i < 256; i++)
                        rands.Add(WebSocketHelper.RandSInt31());
                    Memory<byte> add_data = new byte[] { 1, 2, 3, 4, 5, };

                    FastStreamBuffer<byte> buf = new_test_buf();

                    for (int i = 0; i < iterations; i++)
                    {
                        //buf.Insert(buf.PinHead + 0, add_data);
                        buf.InsertHead(add_data);
                    }

                },
                () => 0), true, 0);

            q.Add(new MicroBenchmark<int>("InsertAndRemoveRandom", 10000,
                (x, iterations) =>
                {
                    List<int> rands = new List<int>();
                    for (int i = 0; i < 256; i++)
                        rands.Add(WebSocketHelper.RandSInt31());
                    Memory<byte> add_data = new byte[] { 1, 2, 3, 4, 5, };

                    FastStreamBuffer<byte> buf = new_test_buf();
                    
                    for (int i = 0; i < iterations; i++)
                    {
                        long pin = buf.PinHead + rands[i % rands.Count] % (buf.Length - add_data.Length);
                        buf.Insert(pin, add_data);
                        buf.Remove(pin, add_data.Length);
                    }

                },
                () => 0), true, 0);

            q.Add(new MicroBenchmark<int>("EnqueueAndDequeue", 10000,
                (x, iterations) =>
                {
                    List<int> rands = new List<int>();
                    for (int i = 0; i < 256; i++)
                        rands.Add(WebSocketHelper.RandSInt31());
                    Memory<byte> add_data = new byte[] { 1, 2, 3, 4, 5, };

                    FastStreamBuffer<byte> buf = new_test_buf();

                    for (int i = 0; i < iterations; i++)
                    {
                        buf.Enqueue(add_data);
                        var ret = buf.Dequeue(long.MaxValue, out _, false);
                    }

                },
                () => 0), true, 10);

            FastStreamBuffer<byte> bufx1 = new_test_buf(10000);
            FastStreamBuffer<byte> bufx2 = new_test_buf(10000);

            q.Add(new MicroBenchmark<int>("MoveToOtherEmpty", 100000,
                (x, iterations) =>
                {

                    for (int i = 0; i < iterations; i++)
                    {
                        bufx1.DequeueAllAndEnqueueToOther(bufx2);
                        bufx2.DequeueAllAndEnqueueToOther(bufx1);
                    }

                },
                () => 0), true, 20);

            System.GC.Collect();

            q.Add(new MicroBenchmark<int>("MoveToOtherNonEmpty", 1,
                (x, iterations) =>
                {
                    FastStreamBuffer<byte> buf1 = new_test_buf(10000);
                    FastStreamBuffer<byte> buf2 = new_test_buf(10000);

                    for (int i = 0; i < iterations; i++)
                    {
                        buf1.DequeueAllAndEnqueueToOther(buf2);
                    }

                },
                () => 0), true, 20);


            q.Add(new MicroBenchmark<int>("Datagram1", 10000,
                (x, iterations) =>
                {
                    FastDatagramBuffer<int> dg = new FastDatagramBuffer<int>();
                    List<int> a = new List<int>();

                    for (int i = 0; i < iterations; i++)
                    {
                        //dg.InsertTail(i);
                        a.Add(i);
                    }

                    //for (int i = 0; i < iterations; i++)
                    //{
                    //    var r = dg.Dequeue(1, out long _);
                    //}

                },
                () => 0), true, 30);



            q.Run();
        }

        static void TestStreamBuffer()
        {
            FastStreamBuffer<byte> new_test_buf()
            {
                FastStreamBuffer<byte> ret = new FastStreamBuffer<byte>();
                ret.Enqueue(new byte[] { 1, 2, 3, });
                ret.Enqueue(new byte[] { 4, 5, 6, 7 });
                ret.Enqueue(new byte[] { 8, 9, 10, 11, 12 });
                ret.Enqueue(new byte[] { 13, 14, 15, 16, 17, 18 });
                return ret;
            }

            {
                var buf = new_test_buf();
                var other = new FastStreamBuffer<byte>();
                buf.DequeueAllAndEnqueueToOther(other);
            }

            {
                var buf = new_test_buf();
                var other = new_test_buf();
                buf.DequeueAllAndEnqueueToOther(other);
            }

            {
                { var buf = new_test_buf(); buf.Insert(0, new byte[] { 21, 22, 23, }, false); }
                { var buf = new_test_buf(); buf.Insert(1, new byte[] { 21, 22, 23, }, false); }
                { var buf = new_test_buf(); buf.Insert(3, new byte[] { 21, 22, 23, }, false); }
                { var buf = new_test_buf(); buf.Insert(4, new byte[] { 21, 22, 23, }, false); }
                { var buf = new_test_buf(); buf.Insert(12, new byte[] { 21, 22, 23, }, false); }
                { var buf = new_test_buf(); buf.Insert(13, new byte[] { 21, 22, 23, }, false); }
                { var buf = new_test_buf(); buf.Insert(17, new byte[] { 21, 22, 23, }, false); }
                { var buf = new_test_buf(); buf.Insert(18, new byte[] { 21, 22, 23, }, false); }
                { var buf = new_test_buf(); buf.Insert(19, new byte[] { 21, 22, 23, }, true); }
                { var buf = new_test_buf(); buf.Insert(-1, new byte[] { 21, 22, 23, }, true); }
            }

            {
                { var buf = new_test_buf(); var a = buf.GetSegmentsFast(4, 14 + 100, out long read_size, true); }
            }

            {
                { var buf = new_test_buf(); var a = buf.GetSegmentsFast(0, 3, out long read_size, false); }
                { var buf = new_test_buf(); var a = buf.GetSegmentsFast(1, 1, out long read_size, false); }
                { var buf = new_test_buf(); var a = buf.GetSegmentsFast(1, 2, out long read_size, false); }
                { var buf = new_test_buf(); var a = buf.GetSegmentsFast(1, 3, out long read_size, false); }
                { var buf = new_test_buf(); var a = buf.GetSegmentsFast(1, 6, out long read_size, false); }
                { var buf = new_test_buf(); var a = buf.GetSegmentsFast(1, 7, out long read_size, false); }
                { var buf = new_test_buf(); var a = buf.GetSegmentsFast(1, 11, out long read_size, false); }
                { var buf = new_test_buf(); var a = buf.GetSegmentsFast(1, 12, out long read_size, false); }
                { var buf = new_test_buf(); var a = buf.GetSegmentsFast(1, 17, out long read_size, false); }
                { var buf = new_test_buf(); var a = buf.GetSegmentsFast(0, 18, out long read_size, false); }
                { var buf = new_test_buf(); var a = buf.GetSegmentsFast(4, 14, out long read_size, false); }
                { var buf = new_test_buf(); var a = buf.GetSegmentsFast(5, 13, out long read_size, false); }
                { var buf = new_test_buf(); var a = buf.GetSegmentsFast(11, 7, out long read_size, false); }
                { var buf = new_test_buf(); var a = buf.GetSegmentsFast(12, 6, out long read_size, false); }
                { var buf = new_test_buf(); var a = buf.GetSegmentsFast(13, 5, out long read_size, false); }
                { var buf = new_test_buf(); var a = buf.GetSegmentsFast(13, 4, out long read_size, false); }
            }

            {
                { var buf = new_test_buf(); buf.PutContiguous(0, 3, false).Span.Fill(88); }
                { var buf = new_test_buf(); buf.PutContiguous(1, 2, false).Span.Fill(88); }
                { var buf = new_test_buf(); buf.PutContiguous(1, 3, false).Span.Fill(88); }
                { var buf = new_test_buf(); buf.PutContiguous(1, 6, false).Span.Fill(88); }
                { var buf = new_test_buf(); buf.PutContiguous(1, 7, false).Span.Fill(88); }
                { var buf = new_test_buf(); buf.PutContiguous(1, 11, false).Span.Fill(88); }
                { var buf = new_test_buf(); buf.PutContiguous(1, 12, false).Span.Fill(88); }
                { var buf = new_test_buf(); buf.PutContiguous(1, 17, false).Span.Fill(88); }
                { var buf = new_test_buf(); buf.PutContiguous(0, 18, false).Span.Fill(88); }

                { var buf = new_test_buf(); buf.PutContiguous(-1, 3, true).Span.Fill(88); }
                { var buf = new_test_buf(); buf.PutContiguous(-1, 7, true).Span.Fill(88); }
                { var buf = new_test_buf(); buf.PutContiguous(11, 10, true).Span.Fill(88); }
                { var buf = new_test_buf(); buf.PutContiguous(-5, 28, true).Span.Fill(88); }
            }

            {
                { var buf = new_test_buf(); var a = buf.DequeueContiguousSlow(2); }
                { var buf = new_test_buf(); var a = buf.DequeueContiguousSlow(3); }
                { var buf = new_test_buf(); var a = buf.DequeueContiguousSlow(4); }
                { var buf = new_test_buf(); var a = buf.DequeueContiguousSlow(11); }
                { var buf = new_test_buf(); var a = buf.DequeueContiguousSlow(12); }
                { var buf = new_test_buf(); var a = buf.DequeueContiguousSlow(13); }
                { var buf = new_test_buf(); var a = buf.DequeueContiguousSlow(17); }
                { var buf = new_test_buf(); var a = buf.DequeueContiguousSlow(18); }
                { var buf = new_test_buf(); var a = buf.DequeueContiguousSlow(19); }
            }

            {
                var buf1 = new_test_buf();
                buf1.Remove(0, 3);

                var buf2 = new_test_buf();
                buf2.Remove(2, 4);

                var buf3 = new_test_buf();
                buf3.Remove(1, 17);

                var buf4 = new_test_buf();
                buf4.Remove(0, 17);

                var buf5 = new_test_buf();
                buf5.Remove(0, 3);

                var buf6 = new_test_buf();
                buf6.Remove(3, 4);

                var buf7 = new_test_buf();
                buf7.Remove(3, 9);

                var buf8 = new_test_buf();
                buf8.Remove(12, 6);

                var buf9 = new_test_buf();
                buf9.Remove(1, 16);

                var buf10 = new_test_buf();
                buf10.Remove(7, 2);

                var buf11 = new_test_buf();
                buf11.Remove(15, 2);

            }
            {
                var buf1 = new_test_buf();
                var r1 = buf1.GetContiguous(buf1.PinHead + 2, 2, false);
                buf1 = new_test_buf();
                var r2 = buf1.GetContiguous(buf1.PinHead + 2, 5, false);
                buf1 = new_test_buf();
                var r3 = buf1.GetContiguous(buf1.PinHead + 2, 6, false);
                buf1 = new_test_buf();
                var r4 = buf1.GetContiguous(buf1.PinHead + 2, 10, false);
                buf1 = new_test_buf();
                var r5 = buf1.GetContiguous(buf1.PinHead + 2, 15, false);
                buf1 = new_test_buf();
                var r6 = buf1.GetContiguous(buf1.PinHead + 2, 16, false);
                buf1 = new_test_buf();
                var r7 = buf1.GetContiguous(buf1.PinHead + 4, 14, false);
            }

            {
                var buf1 = new_test_buf();
                var r1 = buf1.GetContiguous(buf1.PinHead + 0, 3, false);

                buf1 = new_test_buf();
                var r2 = buf1.GetContiguous(buf1.PinHead + 3, 4, false);

                buf1 = new_test_buf();
                var r3 = buf1.GetContiguous(buf1.PinHead + 7, 5, false);

                buf1 = new_test_buf();
                var r4 = buf1.GetContiguous(buf1.PinHead + 12, 6, false);

                buf1 = new_test_buf();
                var r5 = buf1.GetContiguous(buf1.PinHead + 0, 2, false);

                buf1 = new_test_buf();
                var r6 = buf1.GetContiguous(buf1.PinHead + 2, 1, false);

                buf1 = new_test_buf();
                var r7 = buf1.GetContiguous(buf1.PinHead + 3, 1, false);

                buf1 = new_test_buf();
                var r8 = buf1.GetContiguous(buf1.PinHead + 4, 3, false);

                buf1 = new_test_buf();
                var r9 = buf1.GetContiguous(buf1.PinHead + 12, 2, false);

                buf1 = new_test_buf();
                var r10 = buf1.GetContiguous(buf1.PinHead + 14, 4, false);

                buf1 = new_test_buf();
                var r11 = buf1.GetContiguous(buf1.PinHead + 0, 19, true);

                buf1 = new_test_buf();
                var r12 = buf1.GetContiguous(buf1.PinHead + 2, 17, true);

                buf1 = new_test_buf();
                var r13 = buf1.GetContiguous(buf1.PinHead + 11, 8, true);

                buf1 = new_test_buf();
                var r14 = buf1.GetContiguous(buf1.PinHead + 13, 6, true);

                buf1 = new_test_buf();
                var r15 = buf1.GetContiguous(buf1.PinHead + 18, 1, true);
            }
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

                            string tmp = Encoding.ASCII.GetString(pkt.Data);
                            var ep = UdpAccel.ParseIPAndPortStr(tmp);
                            Console.WriteLine(ep);
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
                await test_vpn();
                //await test_udpa();
            }
            catch (Exception ex)
            {
                WriteLine(ex.ToString());
            }

            //try
            //{
            //    await test_ssl();
            //}
            //catch (Exception ex)
            //{
            //    WriteLine(ex.ToString());
            //}
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
            string hostname = "jp.softether-download.com";
            int port = 80;

            using (TcpClient tc = new TcpClient())
            {
                WriteLine("connecting.");
                await tc.ConnectAsync(hostname, port);

                WriteLine("connected.");

                using (NetworkStream ssl = tc.GetStream())
                {
                    string text = $"GET /files/softether/v4.28-9669-beta-2018.09.11-tree/Windows/Admin_Tools/VPN_Server_Manager_and_Command-line_Utility_Package/softether-vpn_admin_tools-v4.28-9669-beta-2018.09.11-win32.zip HTTP/1.1\r\nHOST: {hostname}\r\n\r\n";
                    byte[] bin = Encoding.UTF8.GetBytes(text);

                    //await ssl.WriteAsync(bin, 0, bin.Length);

                    for (int i = 0; i < bin.Length; i++)
                    {
                        await ssl.WriteAsyncWithTimeout(new byte[1] { bin[i] }, timeout: 1000);
                        WriteLine("send: " + i);
                    }

                    if (false)
                    {
                        int total_size = 0;
                        for (int i = 0; ; i++)
                        {
                            byte[] ret = await ssl.ReadAsyncWithTimeout(65536, read_all: false, timeout: 10000);
                            total_size += ret.Length;
                            WriteLine("recv: " + i + " " + ret.Length + "    total: " + total_size);
                        }
                    }
                    else
                    {
                        AsyncBulkReceiver<byte[], int> reader = new AsyncBulkReceiver<byte[], int>(async state =>
                        {
                            //num++;
                            //if ((num % 3) == 0) await Task.Yield();
                            //if (num >= 20) await Task.Delay(-1);
                            //return new byte[65536];
                            //return await ssl.ReadAsyncWithTimeout(1, read_all: false, timeout: -1);

                            byte[] ret = new byte[1];
                            int r = await ssl.ReadAsync(ret);
                            return ret;
                            //byte[] ret = new byte[65536];
                            //int r = await ssl.ReadAsync(ret);
                            //return ret.AsSpan().Slice(0, r).ToArray();
                        });

                        int total_size = 0;
                        for (int i = 0; ; i++)
                        {
                            byte[][] ret = await reader.Read(max_count: 100000);
                            total_size += ret.Length;
                            WriteLine("recv_bulk: " + i + " " + ret.Length + "    total: " + total_size);
                        }
                    }

                    WriteLine();
                }
            }
        }

        static async Task test_ssl()
        {
            string hostname = "jp.softether-download.com";
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

                        string text = $"GET /files/softether/v4.28-9669-beta-2018.09.11-tree/Windows/Admin_Tools/VPN_Server_Manager_and_Command-line_Utility_Package/softether-vpn_admin_tools-v4.28-9669-beta-2018.09.11-win32.zip HTTP/1.1\r\nHOST: {hostname}\r\n\r\n";
                        byte[] bin = Encoding.UTF8.GetBytes(text);

                        //await ssl.WriteAsync(bin, 0, bin.Length);

                        for (int i = 0; i < bin.Length; i++)
                        {
                            await ssl.WriteAsyncWithTimeout(new byte[1] { bin[i] }, timeout: 1000);
                            WriteLine("send: " + i);
                        }

                        if (false)
                        {
                            int total_size = 0;
                            for (int i = 0; ; i++)
                            {
                                byte[] ret = await ssl.ReadAsyncWithTimeout(65536, read_all: false, timeout: 10000);
                                total_size += ret.Length;
                                WriteLine("recv: " + i + " " + ret.Length + "    total: " + total_size);
                            }
                        }
                        else
                        {
                            AsyncBulkReceiver<byte[], int> reader = new AsyncBulkReceiver<byte[], int>(async state =>
                            {
                                //num++;
                                //if ((num % 3) == 0) await Task.Yield();
                                //if (num >= 20) await Task.Delay(-1);
                                //return new byte[65536];
                                return await ssl.ReadAsyncWithTimeout(1, read_all: false, timeout: 3000);

                                //byte[] ret = new byte[65536];
                                //int r = await ssl.ReadAsync(ret);
                                //return ret.AsSpan().Slice(0, r).ToArray();
                            });

                            int total_size = 0;
                            for (int i = 0; ; i++)
                            {
                                byte[][] ret = await reader.Read(max_count: 100000);
                                total_size += ret.Length;
                                WriteLine("recv_bulk: " + i + " " + ret.Length);
                            }
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
        VpnVirtualNetworkAdapterParam Param;

        CancellationTokenSource cancel = new CancellationTokenSource();

        public override async Task<object> OnConnected(VpnVirtualNetworkAdapterParam param)
        {
            Console.WriteLine($"Connected.");

            cancel = new CancellationTokenSource();

            this.Param = param;

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
                eth.Write(WebSocketHelper.Rand(32));

                Param.SendPackets(new VpnPacket[] { new VpnPacket(VpnProtocolPacketType.Ethernet, eth.ByteData) });

                await WebSocketHelper.WaitObjectsAsync(cancels: new CancellationToken[] { cancel.Token }, timeout: 1000);
            }
        }

        public override async Task OnDisconnected(object state, VpnVirtualNetworkAdapterParam param)
        {
            Console.WriteLine($"Disconnected.");

            cancel.Cancel();
        }

        public override async Task OnPacketsReceived(object state, VpnVirtualNetworkAdapterParam param, VpnPacket[] packets)
        {
            Console.WriteLine($"recv packet: {packets[0].Data.Length}");
        }
    }
}


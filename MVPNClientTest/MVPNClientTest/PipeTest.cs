using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using static System.Console;
using Newtonsoft.Json;
using SoftEther.WebSocket;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using System.Diagnostics;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using System.Runtime.InteropServices;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using SoftEther.WebSocket.Helper;
using SoftEther.VpnClient;
using System.Collections.Concurrent;

#pragma warning disable CS0162

namespace MVPNClientTest
{
    static class PipeTest
    {

        public static void TestMain()
        {
            CancellationTokenSource cancel = new CancellationTokenSource();

            //Console.Write("Mode>");
            //string mode = Console.ReadLine();

            Thread thread = new Thread(() =>
            {
                Console.ReadLine();

                cancel.Cancel();
            });
            thread.IsBackground = true;
            thread.Start();

            try
            {
                //Test_Pipe_TCP_Client(cancel.Token).Wait();
                //Test_Pipe_SslStream_Client(cancel.Token).Wait();

                //Test_Pipe_SpeedTest_Client("www.google.com", 80, 1, 5000, SpeedTest.ModeFlag.Recv, cancel.Token).Wait();

                //if (mode.StartsWith("s", StringComparison.OrdinalIgnoreCase))
                //{
                //    Test_Pipe_SpeedTest_Server(9821, cancel.Token).Wait();
                //}
                //else
                //{
                //    //Test_Pipe_SpeedTest_Client("speed.sec.softether.co.jp", 9821, 32, 60 * 60 * 1000, SpeedTest.ModeFlag.Download, cancel.Token).Wait();
                //    Test_Pipe_SpeedTest_Client("127.0.0.1", 9821, 32, 5 * 1000, SpeedTest.ModeFlag.Upload, cancel.Token).Wait();
                //}

                //Test_Pipe_SpeedTest_Server(9821, cancel.Token).Wait();


                //WebSocketHelper.WaitObjectsAsync
            }
            catch (Exception ex)
            {
                WriteLine("TestMain: " + ex.GetSingleException());
            }
            finally
            {
                LeakChecker.Shared.Print();
            }
        }

        class SpeedTest
        {
            [Flags]
            enum Direction
            {
                Send,
                Recv,
            }

            [Flags]
            public enum ModeFlag
            {
                Upload,
                Download,
                Both,
            }

            public class Result
            {
                public long NumBytesUpload;      // Uploaded size
                public long NumBytesDownload;    // Downloaded size
                public long NumBytesTotal;       // Total size
                public long Span;                // Period (in milliseconds)
                public long BpsUpload;           // Upload throughput
                public long BpsDownload;         // Download throughput
                public long BpsTotal;			 // Total throughput
            }

            bool IsServerMode;
            Memory<byte> SendData;

            IPAddress Ip;
            int Port;
            int NumConnection;
            ModeFlag Mode;
            int TimeSpan;
            ulong SessionId;
            CancellationToken Cancel;
            SharedExceptionQueue ExceptionQueue;
            AsyncManualResetEvent ClientStartEvent;

            int ConnectTimeout = 10 * 1000;
            int RecvTimeout = 5000;

            public SpeedTest(IPAddress ip, int port, int num_connection, int timespan, ModeFlag mode, CancellationToken cancel)
            {
                this.IsServerMode = false;
                this.Ip = ip;
                this.Port = port;
                this.Cancel = cancel;
                this.NumConnection = Math.Max(num_connection, 1);
                this.TimeSpan = Math.Max(timespan, 1000);
                this.Mode = mode;
                if (Mode == ModeFlag.Both)
                {
                    this.NumConnection = Math.Max(NumConnection, 2);
                }
                this.ClientStartEvent = new AsyncManualResetEvent();
                InitSendData();
            }

            public SpeedTest(int port, CancellationToken cancel)
            {
                IsServerMode = true;
                this.Cancel = cancel;
                this.Port = port;
                InitSendData();
            }

            void InitSendData()
            {
                int size = 65536;
                byte[] data = WebSocketHelper.Rand(size);
                for (int i = 0; i < data.Length; i++)
                {
                    if (data[i] == (byte)'!')
                        data[i] = (byte)'*';
                }
                SendData = data;
            }

            Once Once;

            public class SessionData
            {
                public bool NoMoreData = false;
            }

            public async Task RunServerAsync()
            {
                if (IsServerMode == false)
                    throw new ApplicationException("Client mode");

                if (Once.IsFirstCall() == false)
                    throw new ApplicationException("You cannot reuse the object.");

                using (var sessions = new GroupManager<ulong, SessionData>(
                    onNewGroup: (key, state) =>
                    {
                        Dbg.Where($"New session: {key}");
                        return new SessionData();
                    },
                    onDeleteGroup: (key, ctx, state) =>
                    {
                        Dbg.Where($"Delete session: {key}");
                    }))
                {
                    FastPipeTcpListener listener = new FastPipeTcpListener(async (lx, p, end) =>
                    {
                        try
                        {
                            Console.WriteLine($"Connected {p.RemoteEndPoint} -> {p.LocalEndPoint}");

                            using (var st = end.GetStream())
                            {
                                st.AttachHandle.SetStreamReceiveTimeout(RecvTimeout);

                                await st.SendAsync(Encoding.ASCII.GetBytes("TrafficServer\r\n\0"));

                                MemoryBuffer<byte> buf = await st.ReceiveAsync(17);

                                Direction dir = buf.ReadBool8() ? Direction.Send : Direction.Recv;
                                ulong session_id = 0;
                                long timespan = 0;

                                try
                                {
                                    session_id = buf.ReadUInt64();
                                    timespan = buf.ReadSInt64();
                                }
                                catch { }

                                long recv_end_tick = FastTick64.Now + timespan;
                                if (timespan == 0) recv_end_tick = long.MaxValue;

                                using (var session = sessions.Enter(session_id))
                                {
                                    using (new DelayAction((int)(Math.Min(timespan * 3 + 180 * 1000, int.MaxValue)), x => p.Disconnect()))
                                    {
                                        if (dir == Direction.Recv)
                                        {
                                            RefInt ref_tmp = new RefInt();
                                            long total_size = 0;

                                            while (true)
                                            {
                                                var ret = await st.FastReceiveAsync(total_recv_size: ref_tmp);
                                                if (ret.Count == 0)
                                                {
                                                    break;
                                                }
                                                total_size += ref_tmp;

                                                if (ret[0].Span[0] == (byte)'!')
                                                    break;

                                                if (FastTick64.Now >= recv_end_tick)
                                                    break;
                                            }

                                            st.AttachHandle.SetStreamReceiveTimeout(Timeout.Infinite);
                                            st.AttachHandle.SetStreamSendTimeout(60 * 5 * 1000);

                                            session.Context.NoMoreData = true;

                                            while (true)
                                            {
                                                MemoryBuffer<byte> send_buf = new MemoryBuffer<byte>();
                                                send_buf.WriteSInt64(total_size);

                                                await st.SendAsync(send_buf);

                                                await Task.Delay(100);
                                            }
                                        }
                                        else
                                        {
                                            st.AttachHandle.SetStreamReceiveTimeout(Timeout.Infinite);
                                            st.AttachHandle.SetStreamSendTimeout(Timeout.Infinite);

                                            while (true)
                                            {
                                                if (session_id == 0 || session.Context.NoMoreData == false)
                                                {
                                                    await st.SendAsync(SendData);
                                                }
                                                else
                                                {
                                                    await st.ReceiveAsync();
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            WriteLine(ex.GetSingleException().Message);
                        }
                    });

                    listener.ListenerManager.Add(this.Port, IPVersion.IPv4);
                    listener.ListenerManager.Add(this.Port, IPVersion.IPv6);

                    WriteLine("Listening.");

                    await WebSocketHelper.WaitObjectsAsync(cancels: this.Cancel.ToSingleArray());

                    listener.Dispose();
                }
            }

            public async Task<Result> RunClientAsync()
            {
                if (IsServerMode)
                    throw new ApplicationException("Server mode");

                if (Once.IsFirstCall() == false)
                    throw new ApplicationException("You cannot reuse the object.");

                WriteLine("Client mode start");

                ExceptionQueue = new SharedExceptionQueue();
                SessionId = WebSocketHelper.RandUInt64();

                List<Task<Result>> tasks = new List<Task<Result>>();
                List<AsyncManualResetEvent> ready_events = new List<AsyncManualResetEvent>();

                using (CancelWatcher cw = new CancelWatcher(this.Cancel))
                {
                    for (int i = 0; i < NumConnection; i++)
                    {
                        Direction dir;
                        if (Mode == ModeFlag.Download)
                            dir = Direction.Recv;
                        else if (Mode == ModeFlag.Upload)
                            dir = Direction.Send;
                        else
                            dir = ((i % 2) == 0) ? Direction.Recv : Direction.Send;

                        AsyncManualResetEvent ready_event = new AsyncManualResetEvent();
                        var t = ClientSingleConnectionAsync(dir, ready_event, cw.CancelToken);
                        ExceptionQueue.RegisterWatchedTask(t);
                        tasks.Add(t);
                        ready_events.Add(ready_event);
                    }

                    try
                    {
                        using (var when_all_ready = new WhenAll(ready_events.Select(x => x.WaitAsync())))
                        {
                            await WebSocketHelper.WaitObjectsAsync(
                                tasks: tasks.Append(when_all_ready.WaitMe).ToArray(),
                                cancels: cw.CancelToken.ToSingleArray(),
                                manual_events: ExceptionQueue.WhenExceptionAdded.ToSingleArray());
                        }

                        Cancel.ThrowIfCancellationRequested();
                        ExceptionQueue.ThrowFirstExceptionIfExists();

                        ExceptionQueue.WhenExceptionAdded.CallbackList.AddSoftCallback(x =>
                        {
                            cw.Cancel();
                        });

                        using (new DelayAction(TimeSpan * 3 + 180 * 1000, x =>
                        {
                            cw.Cancel();
                        }))
                        {
                            ClientStartEvent.Set(true);

                            using (var when_all_completed = new WhenAll(tasks))
                            {
                                await WebSocketHelper.WaitObjectsAsync(
                                    tasks: when_all_completed.WaitMe.ToSingleArray(),
                                    cancels: cw.CancelToken.ToSingleArray()
                                    );

                                await when_all_completed.WaitMe;
                            }
                        }

                        Result ret = new Result();

                        ret.Span = TimeSpan;

                        foreach (var r in tasks.Select(x => x.Result))
                        {
                            ret.NumBytesDownload += r.NumBytesDownload;
                            ret.NumBytesUpload += r.NumBytesUpload;
                        }

                        ret.NumBytesTotal = ret.NumBytesUpload + ret.NumBytesDownload;

                        ret.BpsUpload = (long)((double)ret.NumBytesUpload * 1000.0 * 8.0 / (double)ret.Span * 1514.0 / 1460.0);
                        ret.BpsDownload = (long)((double)ret.NumBytesDownload * 1000.0 * 8.0 / (double)ret.Span * 1514.0 / 1460.0);
                        ret.BpsTotal = ret.BpsUpload + ret.BpsDownload;

                        return ret;
                    }
                    catch (Exception ex)
                    {
                        await Task.Yield();
                        ExceptionQueue.Add(ex);
                    }
                    finally
                    {
                        cw.Cancel();
                        try
                        {
                            await Task.WhenAll(tasks);
                        }
                        catch { }
                    }

                    ExceptionQueue.ThrowFirstExceptionIfExists();
                }

                Dbg.Where();

                return null;
            }

            async Task<Result> ClientSingleConnectionAsync(Direction dir, AsyncManualResetEvent fire_me_when_ready, CancellationToken cancel)
            {
                Result ret = new Result();
                using (FastTcpPipe p = await FastTcpPipe.ConnectAsync(Ip, Port, cancel, ConnectTimeout))
                {
                    try
                    {
                        using (FastPipeEndStream st = p.GetStream())
                        {
                            if (dir == Direction.Recv)
                                st.AttachHandle.SetStreamReceiveTimeout(RecvTimeout);

                            try
                            {
                                var hello = await st.ReceiveAllAsync(16);

                                Dbg.Where();
                                if (Encoding.ASCII.GetString(hello.Span).StartsWith("TrafficServer\r\n") == false)
                                    throw new ApplicationException("Target server is not a Traffic Server.");
                                Dbg.Where();

                                //throw new ApplicationException("aaaa" + dir.ToString());

                                fire_me_when_ready.Set();

                                cancel.ThrowIfCancellationRequested();

                                await WebSocketHelper.WaitObjectsAsync(
                                    manual_events: ClientStartEvent.ToSingleArray(),
                                    cancels: cancel.ToSingleArray()
                                    );

                                long tick_start = FastTick64.Now;
                                long tick_end = tick_start + this.TimeSpan;

                                var send_data = new MemoryBuffer<byte>();
                                send_data.WriteBool8(dir == Direction.Recv);
                                send_data.WriteUInt64(SessionId);
                                send_data.WriteSInt64(TimeSpan);

                                await st.SendAsync(send_data);

                                if (dir == Direction.Recv)
                                {
                                    RefInt total_recv_size = new RefInt();
                                    while (true)
                                    {
                                        long now = FastTick64.Now;

                                        if (now >= tick_end)
                                            break;

                                        await WebSocketHelper.WaitObjectsAsync(
                                            tasks: st.FastReceiveAsync(total_recv_size: total_recv_size).ToSingleArray(),
                                            timeout: (int)(tick_end - now),
                                            exceptions: ExceptionWhen.TaskException | ExceptionWhen.CancelException);

                                        ret.NumBytesDownload += total_recv_size;
                                    }
                                }
                                else
                                {
                                    st.AttachHandle.SetStreamReceiveTimeout(Timeout.Infinite);

                                    while (true)
                                    {
                                        long now = FastTick64.Now;

                                        if (now >= tick_end)
                                            break;

                                        /*await WebSocketHelper.WaitObjectsAsync(
                                            tasks: st.FastSendAsync(SendData, flush: true).ToSingleArray(),
                                            timeout: (int)(tick_end - now),
                                            exceptions: ExceptionWhen.TaskException | ExceptionWhen.CancelException);*/

                                        await st.FastSendAsync(SendData, flush: true);
                                    }

                                    Task recv_result = Task.Run(async () =>
                                    {
                                        var recv_memory = await st.ReceiveAllAsync(8);

                                        MemoryBuffer<byte> recv_memory_buf = recv_memory;
                                        ret.NumBytesUpload = recv_memory_buf.ReadSInt64();

                                        st.Disconnect();
                                    });

                                    Task send_surprise = Task.Run(async () =>
                                    {
                                        byte[] surprise = new byte[260];
                                        surprise.AsSpan().Fill((byte)'!');
                                        while (true)
                                        {
                                            await st.SendAsync(surprise);

                                            await WebSocketHelper.WaitObjectsAsync(
                                                manual_events: p.OnDisconnectedEvent.ToSingleArray(),
                                                timeout: 200);
                                        }
                                    });

                                    await WhenAll.Await(false, recv_result, send_surprise);

                                    await recv_result;
                                }

                                st.Disconnect();

                                Dbg.Where();
                                return ret;
                            }
                            catch (Exception ex)
                            {
                                Dbg.Where(ex.Message);
                                ExceptionQueue.Add(ex);
                                throw;
                            }
                        }
                    }
                    finally
                    {
                        await p.WaitForLoopFinish(true);
                    }
                }
            }
        }

        static async Task Test_Pipe_SpeedTest_Server(int port, CancellationToken cancel)
        {
            SpeedTest test = new SpeedTest(port, cancel);

            await test.RunServerAsync();
        }

        static async Task Test_Pipe_SpeedTest_Client(string server_host, int server_port, int num_connection, int timespan, SpeedTest.ModeFlag mode, CancellationToken cancel, AddressFamily? af = null)
        {
            IPAddress target_ip = await FastTcpPipe.GetIPFromHostName(server_host, af, cancel);

            SpeedTest test = new SpeedTest(target_ip, server_port, num_connection, timespan, mode, cancel);

            var result = await test.RunClientAsync();

            Console.WriteLine("--- Result ---");
            Console.WriteLine(WebSocketHelper.ObjectToJson(result));
        }

        static async Task Test_Pipe_SslStream_Client(CancellationToken cancel)
        {
            string hostname = "news.goo.ne.jp";

            using (FastTcpPipe p = await FastTcpPipe.ConnectAsync(hostname, 443, null, cancel))
            {
                using (FastPipeEndStream st = p.GetStream())
                {
                    using (SslStream ssl = new SslStream(st))
                    {
                        st.AttachHandle.SetStreamReceiveTimeout(3000);

                        SslClientAuthenticationOptions opt = new SslClientAuthenticationOptions()
                        {
                            TargetHost = hostname,
                            AllowRenegotiation = true,
                            RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => { return true; },
                        };

                        await ssl.AuthenticateAsClientAsync(opt, cancel);
                        WriteLine("Connected.");
                        StreamWriter w = new StreamWriter(ssl);
                        w.AutoFlush = true;

                        await w.WriteAsync(
                            "GET / HTTP/1.0\r\n" +
                            $"HOST: {hostname}\r\n\r\n"
                            );

                        StreamReader r = new StreamReader(ssl);
                        while (true)
                        {
                            string s = await r.ReadLineAsync();
                            if (s == null) break;
                            WriteLine(s);
                        }

                        //WriteLine(await r.ReadToEndAsync());
                    }
                }

                await p.WaitForLoopFinish();
            }
        }

        static async Task Test_Pipe_TCP_Client(CancellationToken cancel)
        {
            using (FastTcpPipe p = await FastTcpPipe.ConnectAsync("www.google.com", 80, null, cancel))
            {
                using (FastPipeEndStream st = p.GetStream())
                {
                    WriteLine("Connected.");
                    StreamWriter w = new StreamWriter(st);
                    w.AutoFlush = true;

                    await w.WriteAsync(
                        "GET / HTTP/1.1\r\n" +
                        "HOST: www.google.com\r\n\r\n"
                        );

                    StreamReader r = new StreamReader(st);
                    while (true)
                    {
                        string s = await r.ReadLineAsync();
                        if (s == null) break;
                        WriteLine(s);
                    }

                    //WriteLine(await r.ReadToEndAsync());
                }

                await p.WaitForLoopFinish();
            }
        }
    }
}

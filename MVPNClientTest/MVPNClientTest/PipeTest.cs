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

                Test_Pipe_SslStream_Client2(cancel.Token).Wait();

                //Test_Pipe_SpeedTest_Client("speed.sec.softether.co.jp", 9821, 32, 3 * 1000, SpeedTest.ModeFlag.Both, cancel.Token).Wait();
                //Test_Pipe_SpeedTest_Server(9821, cancel.Token).Wait();

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
                LeakChecker.Print();
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

            IPAddress ServerIP;
            int ServerPort;
            int NumConnection;
            ModeFlag Mode;
            int TimeSpan;
            ulong SessionId;
            CancellationToken Cancel;
            SharedExceptionQueue ExceptionQueue;
            AsyncManualResetEvent ClientStartEvent;

            int ConnectTimeout = 10 * 1000;
            int RecvTimeout = 5000;

            public SpeedTest(IPAddress ip, int port, int numConnection, int timespan, ModeFlag mode, CancellationToken cancel)
            {
                this.IsServerMode = false;
                this.ServerIP = ip;
                this.ServerPort = port;
                this.Cancel = cancel;
                this.NumConnection = Math.Max(numConnection, 1);
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
                this.ServerPort = port;
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
                        CleanuperLady lady = new CleanuperLady();

                        try
                        {

                            Console.WriteLine($"Connected {p.RemoteEndPoint} -> {p.LocalEndPoint}");

                            using (var st = end.GetStream())
                            {
                                lady.Add(st);

                                st.AttachHandle.SetStreamReceiveTimeout(RecvTimeout);

                                await st.SendAsync(Encoding.ASCII.GetBytes("TrafficServer\r\n\0"));

                                MemoryBuffer<byte> buf = await st.ReceiveAsync(17);

                                Direction dir = buf.ReadBool8() ? Direction.Send : Direction.Recv;
                                ulong sessionId = 0;
                                long timespan = 0;

                                try
                                {
                                    sessionId = buf.ReadUInt64();
                                    timespan = buf.ReadSInt64();
                                }
                                catch { }

                                long recvEndTick = FastTick64.Now + timespan;
                                if (timespan == 0) recvEndTick = long.MaxValue;

                                using (var session = sessions.Enter(sessionId))
                                {
                                    using (var delay = new DelayAction((int)(Math.Min(timespan * 3 + 180 * 1000, int.MaxValue)), x => p.Disconnect()))
                                    {
                                        lady.Add(delay);
                                        if (dir == Direction.Recv)
                                        {
                                            RefInt refTmp = new RefInt();
                                            long totalSize = 0;

                                            while (true)
                                            {
                                                var ret = await st.FastReceiveAsync(totalRecvSize: refTmp);
                                                if (ret.Count == 0)
                                                {
                                                    break;
                                                }
                                                totalSize += refTmp;

                                                if (ret[0].Span[0] == (byte)'!')
                                                    break;

                                                if (FastTick64.Now >= recvEndTick)
                                                    break;
                                            }

                                            st.AttachHandle.SetStreamReceiveTimeout(Timeout.Infinite);
                                            st.AttachHandle.SetStreamSendTimeout(60 * 5 * 1000);

                                            session.Context.NoMoreData = true;

                                            while (true)
                                            {
                                                MemoryBuffer<byte> sendBuf = new MemoryBuffer<byte>();
                                                sendBuf.WriteSInt64(totalSize);

                                                await st.SendAsync(sendBuf);

                                                await Task.Delay(100);
                                            }
                                        }
                                        else
                                        {
                                            st.AttachHandle.SetStreamReceiveTimeout(Timeout.Infinite);
                                            st.AttachHandle.SetStreamSendTimeout(Timeout.Infinite);

                                            while (true)
                                            {
                                                if (sessionId == 0 || session.Context.NoMoreData == false)
                                                {
                                                    await st.SendAsync(SendData);
                                                }
                                                else
                                                {
                                                    var recvMemory = await st.ReceiveAsync();

                                                    if (recvMemory.Length == 0)
                                                        break;
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
                        finally
                        {
                            Dbg.Where();
                            await lady.CleanupAsync();
                            Dbg.Where();
                        }
                    });

                    try
                    {
                        listener.ListenerManager.Add(this.ServerPort, IPVersion.IPv4);
                        listener.ListenerManager.Add(this.ServerPort, IPVersion.IPv6);

                        WriteLine("Listening.");

                        await WebSocketHelper.WaitObjectsAsync(cancels: this.Cancel.ToSingleArray());
                    }
                    finally
                    {
                        listener.Dispose();
                        await listener.AsyncCleanuper;
                    }
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
                List<AsyncManualResetEvent> readyEvents = new List<AsyncManualResetEvent>();

                using (CancelWatcher cancelWatcher = new CancelWatcher(this.Cancel))
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

                        AsyncManualResetEvent readyEvent = new AsyncManualResetEvent();
                        var t = ClientSingleConnectionAsync(dir, readyEvent, cancelWatcher.CancelToken);
                        ExceptionQueue.RegisterWatchedTask(t);
                        tasks.Add(t);
                        readyEvents.Add(readyEvent);
                    }

                    try
                    {
                        using (var whenAllReady = new WhenAll(readyEvents.Select(x => x.WaitAsync())))
                        {
                            await WebSocketHelper.WaitObjectsAsync(
                                tasks: tasks.Append(whenAllReady.WaitMe).ToArray(),
                                cancels: cancelWatcher.CancelToken.ToSingleArray(),
                                manualEvents: ExceptionQueue.WhenExceptionAdded.ToSingleArray());
                        }

                        Cancel.ThrowIfCancellationRequested();
                        ExceptionQueue.ThrowFirstExceptionIfExists();

                        ExceptionQueue.WhenExceptionAdded.CallbackList.AddSoftCallback(x =>
                        {
                            cancelWatcher.Cancel();
                        });

                        using (new DelayAction(TimeSpan * 3 + 180 * 1000, x =>
                        {
                            cancelWatcher.Cancel();
                        }))
                        {
                            ClientStartEvent.Set(true);

                            using (var whenAllCompleted = new WhenAll(tasks))
                            {
                                await WebSocketHelper.WaitObjectsAsync(
                                    tasks: whenAllCompleted.WaitMe.ToSingleArray(),
                                    cancels: cancelWatcher.CancelToken.ToSingleArray()
                                    );

                                await whenAllCompleted.WaitMe;
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
                        cancelWatcher.Cancel();
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

            async Task<Result> ClientSingleConnectionAsync(Direction dir, AsyncManualResetEvent fireMeWhenReady, CancellationToken cancel)
            {
                Result ret = new Result();
                CleanuperLady lady = new CleanuperLady();
                try
                {
                    using (FastTcpPipe p = await FastTcpPipe.ConnectAsync(ServerIP, ServerPort, cancel, ConnectTimeout))
                    {
                        lady.Add(p);
                        using (FastPipeEndStream st = p.GetStream())
                        {
                            lady.Add(st);

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

                                fireMeWhenReady.Set();

                                cancel.ThrowIfCancellationRequested();

                                await WebSocketHelper.WaitObjectsAsync(
                                    manualEvents: ClientStartEvent.ToSingleArray(),
                                    cancels: cancel.ToSingleArray()
                                    );

                                long tickStart = FastTick64.Now;
                                long tickEnd = tickStart + this.TimeSpan;

                                var sendData = new MemoryBuffer<byte>();
                                sendData.WriteBool8(dir == Direction.Recv);
                                sendData.WriteUInt64(SessionId);
                                sendData.WriteSInt64(TimeSpan);

                                await st.SendAsync(sendData);

                                if (dir == Direction.Recv)
                                {
                                    RefInt totalRecvSize = new RefInt();
                                    while (true)
                                    {
                                        long now = FastTick64.Now;

                                        if (now >= tickEnd)
                                            break;

                                        await WebSocketHelper.WaitObjectsAsync(
                                            tasks: st.FastReceiveAsync(totalRecvSize: totalRecvSize).ToSingleArray(),
                                            timeout: (int)(tickEnd - now),
                                            exceptions: ExceptionWhen.TaskException | ExceptionWhen.CancelException);

                                        ret.NumBytesDownload += totalRecvSize;
                                    }
                                }
                                else
                                {
                                    st.AttachHandle.SetStreamReceiveTimeout(Timeout.Infinite);

                                    while (true)
                                    {
                                        long now = FastTick64.Now;

                                        if (now >= tickEnd)
                                            break;

                                        /*await WebSocketHelper.WaitObjectsAsync(
                                            tasks: st.FastSendAsync(SendData, flush: true).ToSingleArray(),
                                            timeout: (int)(tick_end - now),
                                            exceptions: ExceptionWhen.TaskException | ExceptionWhen.CancelException);*/

                                        await st.FastSendAsync(SendData, flush: true);
                                    }

                                    Task recvResult = Task.Run(async () =>
                                    {
                                        var recvMemory = await st.ReceiveAllAsync(8);

                                        MemoryBuffer<byte> recvMemoryBuf = recvMemory;
                                        ret.NumBytesUpload = recvMemoryBuf.ReadSInt64();

                                        st.Disconnect();
                                    });

                                    Task sendSurprise = Task.Run(async () =>
                                    {
                                        byte[] surprise = new byte[260];
                                        surprise.AsSpan().Fill((byte)'!');
                                        while (true)
                                        {
                                            await st.SendAsync(surprise);

                                            await WebSocketHelper.WaitObjectsAsync(
                                                manualEvents: p.OnDisconnectedEvent.ToSingleArray(),
                                                timeout: 200);
                                        }
                                    });

                                    await WhenAll.Await(false, recvResult, sendSurprise);

                                    await recvResult;
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
                }
                finally
                {
                    await lady.CleanupAsync();
                }
            }
        }

        static async Task Test_Pipe_SpeedTest_Server(int port, CancellationToken cancel)
        {
            SpeedTest test = new SpeedTest(port, cancel);

            await test.RunServerAsync();
        }

        static async Task Test_Pipe_SpeedTest_Client(string serverHost, int serverPort, int numConnection, int timespan, SpeedTest.ModeFlag mode, CancellationToken cancel, AddressFamily? af = null)
        {
            IPAddress targetIP = await FastTcpPipe.GetIPFromHostName(serverHost, af, cancel);

            SpeedTest test = new SpeedTest(targetIP, serverPort, numConnection, timespan, mode, cancel);

            var result = await test.RunClientAsync();

            Console.WriteLine("--- Result ---");
            Console.WriteLine(WebSocketHelper.ObjectToJson(result));
        }

        static async Task Test_Pipe_SslStream_Client2(CancellationToken cancel)
        {
            string hostname = "news.goo.ne.jp";
            CleanuperLady lady = new CleanuperLady();
            try
            {
                using (FastTcpPipe p = await FastTcpPipe.ConnectAsync(hostname, 443, null, cancel))
                {
                    lady.Add(p);

                    using (FastPipe p2 = new FastPipe(cancel))
                    {
                        lady.Add(p2);

                        SslClientAuthenticationOptions opt = new SslClientAuthenticationOptions()
                        {
                            TargetHost = hostname,
                            AllowRenegotiation = true,
                            RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => { return false; },
                        };

                        using (FastSslProtocolStack ssl = new FastSslProtocolStack(p.LocalPipeEnd, p2.A, opt, cancel))
                        {
                            lady.Add(ssl);

                            await ssl.WaitInitSuccessOrFailAsync();

                            using (var st = p2.B.GetStream())
                            {
                                lady.Add(st);

                                WriteLine("Connected.");
                                StreamWriter w = new StreamWriter(st);
                                w.AutoFlush = true;

                                await w.WriteAsync(
                                    "GET / HTTP/1.0\r\n" +
                                    $"HOST: {hostname}\r\n\r\n"
                                    );

                                StreamReader r = new StreamReader(st);
                                while (true)
                                {
                                    string s = await r.ReadLineAsync();
                                    if (s == null) break;
                                    WriteLine(s);
                                }
                                Dbg.Where();
                            }
                        }
                    }
                }
            }
            finally
            {
                await lady.CleanupAsync();
            }
        }

        static async Task Test_Pipe_SslStream_Client(CancellationToken cancel)
        {
            string hostname = "news.goo.ne.jp";

            CleanuperLady lady = new CleanuperLady();
            try
            {
                using (FastTcpPipe p = await FastTcpPipe.ConnectAsync(hostname, 443, null, cancel))
                {
                    lady.Add(p);
                    using (FastPipeEndStream st = p.GetStream())
                    {
                        lady.Add(st);
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
                }
            }
            finally
            {
                await lady.CleanupAsync();
            }
        }

        static async Task Test_Pipe_TCP_Client(CancellationToken cancel)
        {
            using (FastTcpPipe p = await FastTcpPipe.ConnectAsync("www.google.com", 80, null, cancel))
            {
                try
                {
                    using (FastPipeEndStream st = p.GetStream())
                    {
                        st.AttachHandle.SetStreamTimeout(2000, -1);
                        WriteLine("Connected.");
                        StreamWriter w = new StreamWriter(st);
                        w.AutoFlush = true;

                        await w.WriteAsync(
                            "GET / HTTP/1.0\r\n" +
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
                }
                finally
                {
                    await p.AsyncCleanuper;
                }
            }
        }
    }
}

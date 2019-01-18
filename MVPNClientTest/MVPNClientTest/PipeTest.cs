﻿using System;
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

#pragma warning disable CS0162

namespace MVPNClientTest
{
    static class PipeTest
    {

        public static void TestMain()
        {
            CancellationTokenSource cancel = new CancellationTokenSource();

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
                Test_Pipe_SpeedTest_Client("speed.sec.softether.co.jp", 9821, 1, 5000, SpeedTest.ModeFlag.Recv, cancel.Token).Wait();

                //WebSocketHelper.WaitObjectsAsync
                //t = Test_Pipe_SslStream_Client();
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
                Send,
                Recv,
                Both,
            }

            public class Result
            {
                public bool Raw;                   // Whether raw data
                public bool Double;                // Whether it is doubled
                public long NumBytesUpload;      // Uploaded size
                public long NumBytesDownload;    // Downloaded size
                public long NumBytesTotal;       // Total size
                public long Span;                // Period (in milliseconds)
                public long BpsUpload;           // Upload throughput
                public long BpsDownload;         // Download throughput
                public long BpsTotal;			// Total throughput
            }

            bool IsServerMode;
            IPAddress Ip;
            int Port;
            int NumConnection;
            ModeFlag Mode;
            int TimeSpan;
            ulong SessionId;
            CancellationToken Cancel;
            SharedExceptionQueue ExceptionQueue;
            AsyncManualResetEvent ClientStartEvent;

            int ConnectTimeout = 3000;

            public SpeedTest(IPAddress ip, int port, int num_connection, int timespan, ModeFlag mode, CancellationToken cancel)
            {
                this.IsServerMode = false;
                this.Ip = ip;
                this.Port = port;
                this.Cancel = cancel;
                this.NumConnection = Math.Max(num_connection, 1);
                this.TimeSpan = Math.Max(timespan, 3000);
                this.Mode = mode;
                if (Mode == ModeFlag.Both)
                {
                    this.NumConnection = Math.Max(NumConnection, 2);
                }
                this.ClientStartEvent = new AsyncManualResetEvent();
            }

            Once ClientOnce;

            public async Task<Result> RunClientAsync()
            {
                if (ClientOnce.IsFirstCall() == false)
                    throw new ApplicationException("You cannot reuse the object.");

                ExceptionQueue = new SharedExceptionQueue();
                SessionId = WebSocketHelper.RandUInt64();

                List<Task<long>> tasks = new List<Task<long>>();
                List<AsyncManualResetEvent> ready_events = new List<AsyncManualResetEvent>();

                using (CancelWatcher cw = new CancelWatcher(this.Cancel))
                {
                    for (int i = 0; i < NumConnection; i++)
                    {
                        Direction dir;
                        if (Mode == ModeFlag.Recv)
                            dir = Direction.Recv;
                        else if (Mode == ModeFlag.Send)
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
                                manual_events: ExceptionQueue.WhenExceptionRaised.ToSingleArray());
                        }

                        Cancel.ThrowIfCancellationRequested();
                        ExceptionQueue.ThrowFirstExceptionIfExists();

                        ClientStartEvent.Set(true);

                        using (var when_all_completed = new WhenAll(tasks))
                        {
                            Dbg.Where();
                            await WebSocketHelper.WaitObjectsAsync(
                                tasks: when_all_completed.WaitMe.ToSingleArray(),
                                cancels: cw.CancelToken.ToSingleArray()
                                );

                            await when_all_completed.WaitMe;
                        }
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

            async Task<long> ClientSingleConnectionAsync(Direction dir, AsyncManualResetEvent fire_me_when_ready, CancellationToken cancel)
            {
                long ret = 0;
                using (FastTcpPipe p = await FastTcpPipe.ConnectAsync(Ip, Port, cancel, ConnectTimeout))
                {
                    try
                    {
                        using (FastPipeEndStream st = p.GetStream())
                        {
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

                                Dbg.Where();
                                await st.SendAsync(send_data);
                                Dbg.Where();

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
                                            exceptions: ExceptionWhen.All);

                                        //await st.FastReceiveAsync(total_recv_size: total_recv_size);

                                        ret += total_recv_size;
                                    }

                                    Dbg.Where();
                                    return ret;
                                }
                                else
                                {
                                    throw new NotImplementedException();
                                }
                            }
                            catch (Exception ex)
                            {
                                Dbg.Where();
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

        static async Task Test_Pipe_SpeedTest_Client(string server_host, int server_port, int num_connection, int timespan, SpeedTest.ModeFlag mode, CancellationToken cancel, AddressFamily? af = null)
        {
            IPAddress target_ip = await FastTcpPipe.GetIPFromHostName(server_host, af, cancel);

            SpeedTest test = new SpeedTest(target_ip, server_port, num_connection, timespan, mode, cancel);

            var result = await test.RunClientAsync();
        }

        static async Task Test_Pipe_SslStream_Client(CancellationToken cancel)
        {
            using (FastTcpPipe p = await FastTcpPipe.ConnectAsync("www.google.com", 443, null, cancel))
            {
                using (FastPipeEndStream st = p.GetStream())
                {
                    using (SslStream ssl = new SslStream(st))
                    {
                        st.AttachHandle.SetStreamReceiveTimeout(3000);
                        await ssl.AuthenticateAsClientAsync("www.google.com");
                        WriteLine("Connected.");
                        StreamWriter w = new StreamWriter(ssl);
                        w.AutoFlush = true;

                        await w.WriteAsync(
                            "GET / HTTP/1.1\r\n" +
                            "HOST: www.google.com\r\n\r\n"
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

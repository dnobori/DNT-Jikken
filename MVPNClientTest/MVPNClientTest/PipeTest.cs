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
            Test_Pipe_TCP_Client().Wait();
            LeakChecker.Shared.Print();
        }

        static async Task Test_Pipe_TCP_Client()
        {
            using (FastTcpPipe p = await FastTcpPipe.ConnectAsync("www.google.com", 80))
            {
                using (Stream st = p.GetStream())
                {
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

                    Dbg.Where();
                }

                await p.WaitForLoopFinish();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Net;
using System.Net.Mail;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Net.NetworkInformation;
using System.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using DnsClient;
using DnsClient.Protocol;

using static System.Console;

namespace DNSQueryPerSecTest
{
    class Program
    {
        public const int NUM_THREADS = 128;

        public const int PORT = 53;

        public const string DNS_IP = "10.20.0.0";
        public const string DNS_TARGET = "vpn.i.open.ad.jp.";

        static long num_ok = 0;
        static long num_ng = 0;

        static long counter = DateTime.Now.Ticks;

        static void DNSQueryThread(object param)
        {
            IPAddress ip = IPAddress.Parse(DNS_IP);
            LookupClient client = new LookupClient(new IPEndPoint(ip, PORT));
            client.UseCache = false;

            WriteLine("Thread {0} starts.", Thread.CurrentThread.ManagedThreadId);

            while (true)
            {
                try
                {
                    counter++;
                    string target = DNS_TARGET;

                    target = counter.ToString() + "-" + target;

                    client.Query(target, QueryType.AAAA);

                    Interlocked.Increment(ref num_ok);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref num_ng);
                }
            }
        }

        static void Main(string[] args)
        {
            List<Thread> thread_list = new List<Thread>();

            for (int i = 0; i < NUM_THREADS; i++)
            {
                Thread t = new Thread(DNSQueryThread);

                thread_list.Add(t);
            }

            foreach (Thread t in thread_list)
            {
                t.Start();
            }

            Stopwatch w = new Stopwatch();
            w.Start();

            while (true)
            {
                long tick1 = w.ElapsedMilliseconds;
                long num1 = Interlocked.Read(ref num_ok);
                Thread.Sleep(1000);
                long tick2 = w.ElapsedMilliseconds;
                long num2 = Interlocked.Read(ref num_ok);
                long num = num2 - num1;

                WriteLine("qps = {0:F3}", (double)num * 1000.0 / (double)(tick2 - tick1));
            }
        }
    }
}

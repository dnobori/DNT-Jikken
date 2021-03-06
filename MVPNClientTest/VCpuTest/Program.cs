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
using System.Runtime.ExceptionServices;
using System.Collections.Concurrent;
using System.Security.Authentication;

using SoftEther.WebSocket.Helper;

#pragma warning disable CS0162

namespace VCpuTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("IPABox Test");
            Console.WriteLine();

            VCpuExecTest.ExecTest();
        }
    }

    static unsafe class VCpuExecTest
    {

        public static void ExecTest()
        {
            int count = 11;

            using (VProcess proc = new VProcess())
            {
                uint stackPtr = 0x500000 + 0x10000 / 2;

                if (VConsts.Addressing == AddressingMode.Paging)
                {
                    proc.Memory.AllocateMemory(0x8000000, 0x100000);
                    proc.Memory.AllocateMemory(0x500000, 0x10000);
                }
                else
                {
                    proc.Memory.AllocateContiguousMemory(0x500000, 0x8000000 + 0x100000 - 0x500000);
                }

                VCpuState state = new VCpuState(proc);

                uint ret = 0xffffffff;
                
                Stopwatch sw = new Stopwatch();
                
                //sw.Start();
                for (int i = 0; i < count; i++)
                {
                    if (i == 1) sw.Start();
                    state.Esp = stackPtr;
                    state.Esp -= 4;
                    proc.Memory.Write(state.Esp, (uint)VCode.CallRetAddress._MagicReturn);

                    VCode.Iam_The_IntelCPU_HaHaHa(state, (uint)VCode.FunctionTable.test_target1);

                    if (state.ExceptionString != null)
                    {
                        throw new ApplicationException($"Error: {state.ExceptionString} at 0x{state.ExceptionAddress:x}.");
                    }
                    else
                    {
                        uint r = state.Eax;
                        
                        if (ret == 0xffffffff)
                        {
                            ret = r;
                            Console.WriteLine($"ret = {state.Eax}");
                        }
                        else if (ret == r)
                        {
                        }
                        else
                        {
                            throw new ApplicationException("Error: Invalid result: " + r);
                        }
                    }
                }
                sw.Stop();

                long result = sw.Elapsed.Ticks * 100 / (count - 1);

                Console.WriteLine($"time = {result:#,0}");
            }
        }
    }
}

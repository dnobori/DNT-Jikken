using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Numerics;

public class Program
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Test1(int a, byte b)
    {
        Span<byte> tmp1 = new byte[32];

        {
            tmp1[a]++;
        }

        {
            tmp1[a]++;
        }
        {
            tmp1[a]++;
        }
        {
            tmp1[a]++;
        }
        {
            tmp1[a]++;
        }
        {
            ref byte r = ref tmp1[a];
            r++;
        }
        {
            ref byte r = ref tmp1[a];
            r++;
        }
        {
            ref byte r = ref tmp1[a];
            r++;
        }
        {
            ref byte r = ref tmp1[a];
            r++;
        }
        {
            ref byte r = ref tmp1[a];
            r++;
        }

        return 0;
    }

    static void Main(string[] args)
    {
        Console.WriteLine("Hello");
        Test1(1, 2);
    }
}

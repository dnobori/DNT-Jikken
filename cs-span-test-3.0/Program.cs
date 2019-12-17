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
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Test1(int a, byte b)
    {
        Span<byte> tmp1 = new byte[32];

        ref byte r = ref tmp1[a];

        r = b;

        return r;
    }

    static void Main(string[] args)
    {
        Test1(1, 2);
    }
}

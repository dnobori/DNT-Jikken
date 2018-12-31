using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using static System.Console;
using System.Collections.Concurrent;
using System.Collections;
using System.Buffers;
using System.Buffers.Binary;

namespace cs_unsafe_test1
{
    static class Util
    {
        public static volatile int Volatile = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOfStruct<T>()
        {
            return Unsafe.SizeOf<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOfStruct(Type t)
        {
            return Marshal.SizeOf(t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T AsStruct<T>(this ref byte src) => ref Unsafe.As<byte, T>(ref src);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<byte> GetSpan(this byte[] src, int start = 0)
        {
            return src.AsSpan(0, src.Length).Slice(start);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T AsStruct<T>(this Span<byte> span)
        {
            return ref AsStruct<T>(ref span.Slice(0, SizeOfStruct<T>())[0]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CheckRange(this Span<byte> span, int start, int length)
        {
            return !(start < 0 || length < 0 || (start + length) < 0 || (start + length) > span.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<byte> Advance(ref this Span<byte> span, int offset)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException();
            if (offset == 0) return Span<byte>.Empty;
            Span<byte> ret = span.Slice(0, offset);
            span = span.Slice(offset);
            return ret;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<byte> StructToSpan<T>(this ref T src) where T: struct
        {
            ref byte mem = ref Unsafe.As<T, byte>(ref src);
            int size = Util.SizeOfStruct<T>();
            return MemoryMarshal.CreateSpan(ref mem, size);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    unsafe struct Struct1
    {
        public byte b1;
        public byte b2;
        public byte b3;
        public uint i1;
        public uint i2;
        public uint i3;

        public void TestSet()
        {
            b1 = 1;
            b2 = 2;
            b3 = 3;
            i1 = 4;
            i2 = 5;
            i3 = 6;
        }

        public void Print()
        {
            WriteLine("---");
            WriteLine(b1);
            WriteLine(b2);
            WriteLine(b3);
            WriteLine(i1);
            WriteLine(i2);
            WriteLine(i3);
            WriteLine("---");
        }
    }

    class Program
    {
        static unsafe uint ToUInt32(Span<byte> span)
        {
            if (span.Length < 4) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* p = span)
            {
                return (uint)((uint)(p[0] << 24) + (uint)(p[1] << 16) + (uint)(p[2] << 8) + (uint)p[3]);
            }
        }
        static unsafe uint ToUInt32Raw(Span<byte> span)
        {
            if (span.Length < 4) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* p = span)
            {
                uint* q = (uint*)p;
                return *q;
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            Struct1 src = new Struct1();
            src.TestSet();

            src.Print();

            var span = src.StructToSpan();

            var dest = new byte[span.Length + 1].AsSpan();
            span.CopyTo(dest.Slice(1));

            ref Struct1 s2 = ref Util.AsStruct<Struct1>(dest.Slice(1));

            s2.Print();

            WriteLine($"ToUInt32Raw = {ToUInt32Raw(dest.Slice(1)).ToString("x")}");
            WriteLine($"ToUInt32 = {ToUInt32(dest.Slice(1)).ToString("x")}");

            unsafe
            {
                fixed (void* ptr = Util.StructToSpan(ref s2))
                {
                    WriteLine($"s2 ptr: {(ulong)ptr}");
                }
            }
        }
    }
}

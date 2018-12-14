using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using static System.Console;
using static cs_struct_bench1.Util;

namespace cs_struct_bench1
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
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    unsafe struct Struct1
    {
        public byte b1;
        public byte b2;
        public byte b3;
        public byte b4;
        public uint i1;
        public uint i2;
        public fixed byte fixedBuffer[10000];
        public uint i3;
    }

    class Test1
    {
        public byte[] Data;

        public Memory<byte> DataMemory;

        public Test1()
        {
            int size = SizeOfStruct<Struct1>() + 100;
            this.Data = new byte[size];

            this.DataMemory = this.Data.AsMemory(0, SizeOfStruct<Struct1>());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Struct1 GetStructWithCopy()
        {
            fixed (byte* p = &Data[0])
            {
                Struct1* s1 = (Struct1*)p;
                return *s1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void SetStructWithCopy(Struct1 s)
        {
            fixed (byte* p = &Data[0])
            {
                Struct1* s1 = (Struct1*)p;
                *s1 = s;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Struct1 GetStructWithUnsafe()
        {
            return ref Data[0].AsStruct<Struct1>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe ref Struct1 GetStructWithSafe(int pos = 0)
        {
            var span = Data.GetSpan().Slice(pos);
            if (span.CheckRange(0, SizeOfStruct<Struct1>() + 101) == false)
            {
                byte[] tmp = new byte[SizeOfStruct<Struct1>()];
                return ref tmp[0].AsStruct<Struct1>();
            }
            return ref span.AsStruct<Struct1>();
        }

        public ref Struct1 Struct1Ref { get => ref GetStructWithUnsafe(); }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            WriteLine($"{SizeOfStruct<Struct1>()}");

            Test1 t1 = new Test1();

            WriteLine("sizeof: " + do_test(100000, count =>
            {
                for (int i = 0; i < count; i++)
                {
                    Util.Volatile += SizeOfStruct<Struct1>();
                }
            }).ToString("#,0"));

            WriteLine("GetStructWithCopy: " + do_test(100000, count =>
            {
                for (int i = 0; i < count; i++)
                {
                    Util.Volatile += t1.GetStructWithCopy().b1;
                    Util.Volatile += t1.GetStructWithCopy().b2;
                    Util.Volatile += t1.GetStructWithCopy().b3;
                    Util.Volatile += t1.GetStructWithCopy().b4;
                }
            }).ToString("#,0"));

            WriteLine("GetStructWithUnsafe: " + do_test(10000000, count =>
            {
                for (int i = 0; i < count; i++)
                {
                    Util.Volatile += t1.GetStructWithUnsafe().b1;
                    Util.Volatile += t1.GetStructWithUnsafe().b2;
                    Util.Volatile += t1.GetStructWithUnsafe().b3;
                    Util.Volatile += t1.GetStructWithUnsafe().b4;
                }
            }).ToString("#,0"));

            WriteLine("GetStructWithUnsafe_prop: " + do_test(10000000, count =>
            {
                for (int i = 0; i < count; i++)
                {
                    Util.Volatile += t1.Struct1Ref.b1;
                    Util.Volatile += t1.Struct1Ref.b2;
                    Util.Volatile += t1.Struct1Ref.b3;
                    Util.Volatile += t1.Struct1Ref.b4;
                }
            }).ToString("#,0"));

            WriteLine("GetStructWithUnsafe save ref: " + do_test(10000000, count =>
            {
                ref Struct1 s = ref t1.Struct1Ref;
                for (int i = 0; i < count; i++)
                {
                    Util.Volatile += s.b1;
                    Util.Volatile += s.b2;
                    Util.Volatile += s.b3;
                    Util.Volatile += s.b4;
                }
            }).ToString("#,0"));


            WriteLine("GetStructWithSafe (span direct 1): " + do_test(10000000, count =>
            {
                for (int i = 0; i < count; i++)
                {
                    Span<byte> span = t1.Data.AsSpan();
                    Util.Volatile += span.Slice(0, 1)[0].AsStruct<Struct1>().b1;
                    Util.Volatile += span.Slice(0, 1)[0].AsStruct<Struct1>().b2;
                    Util.Volatile += span.Slice(0, 1)[0].AsStruct<Struct1>().b3;
                    Util.Volatile += span.Slice(0, 1)[0].AsStruct<Struct1>().b4;
                }
            }).ToString("#,0"));

            WriteLine("GetStructWithSafe (span direct 2): " + do_test(10000000, count =>
            {
                for (int i = 0; i < count; i++)
                {
                    Span<byte> span = t1.Data.AsSpan();
                    Util.Volatile += span.Slice(0, SizeOfStruct<Struct1>())[0].AsStruct<Struct1>().b1;
                    Util.Volatile += span.Slice(0, SizeOfStruct<Struct1>())[0].AsStruct<Struct1>().b2;
                    Util.Volatile += span.Slice(0, SizeOfStruct<Struct1>())[0].AsStruct<Struct1>().b3;
                    Util.Volatile += span.Slice(0, SizeOfStruct<Struct1>())[0].AsStruct<Struct1>().b4;
                }
            }).ToString("#,0"));

            WriteLine("GetStructWithSafe (span in func): " + do_test(10000000, count =>
            {
                for (int i = 0; i < count; i++)
                {
                    Util.Volatile += t1.GetStructWithSafe().b1;
                    Util.Volatile += t1.GetStructWithSafe().b2;
                    Util.Volatile += t1.GetStructWithSafe().b3;
                    Util.Volatile += t1.GetStructWithSafe().b4;
                }
            }).ToString("#,0"));
        }

        static int do_test(int count, Action<int> action)
        {
            Stopwatch sw = new Stopwatch();

            sw.Start();
            action(count);
            sw.Stop();

            double ret = (double)sw.Elapsed.Ticks * 100.0 / (double)count;

            return (int)ret;
        }
    }
}

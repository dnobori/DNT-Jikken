using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using static System.Console;
using static cs_struct_bench1.Util;
using System.Collections.Concurrent;
using System.Collections;
using System.IO.Pipelines;
using System.Buffers;
using System.Linq;
using System.Buffers.Binary;
using System.Net.NetworkInformation;
using System.Net;
using System.IO;

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
            //return new Span<byte>(src).Slice(start);
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
        public fixed byte fixedBuffer[1500];
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
        public ref Struct1 GetStructWithSafe(int pos = 0)
        {
            var span = Data.AsSpan();
            return ref span.AsStruct<Struct1>();
        }

        public ref Struct1 Struct1Ref { get => ref GetStructWithUnsafe(); }
    }

    public class MicroBenchmarkQueue
    {
        List<(IMicroBenchmark bm, int priority, int index)> List = new List<(IMicroBenchmark bm, int priority, int index)>();

        public MicroBenchmarkQueue Add(IMicroBenchmark benchmark, bool enabled = true, int priority = 0)
        {
            if (enabled)
                List.Add((benchmark, priority, List.Count));
            return this;
        }

        public void Run()
        {
            foreach (var a in List.OrderBy(x => x.index).OrderBy(x => -x.priority).Select(x => x.bm))
                a.StartAndPrint();
        }
    }

    public static class Limbo
    {
        public static long SInt = 0;
        public static ulong UInt = 0;
        public volatile static object ObjectSlow = null;
    }

    public class MicroBenchmarkGlobalParam
    {
        public static int DefaultDurationMSecs = 250;
        public static readonly double EmptyBaseLine;
    }

    public interface IMicroBenchmark
    {
        double Start(int duration = 0);
        double StartAndPrint(int duration = 0);
    }

    public class MicroBenchmark<TUserVariable> : IMicroBenchmark
    {
        public readonly string Name;
        volatile bool StopFlag;
        object LockObj = new object();
        public readonly int Iterations;

        public readonly Func<TUserVariable> Init;
        public readonly Action<TUserVariable, int> Proc;

        readonly Action<TUserVariable, int> DummyLoopProc = (state, count) =>
        {
            for (int i = 0; i < count; i++) Limbo.SInt++;
        };

        public MicroBenchmark(string name, int iterations, Action<TUserVariable, int> proc, Func<TUserVariable> init = null)
        {
            Name = name;
            Init = init;
            Proc = proc;
            Iterations = Math.Max(iterations, 1);
        }

        public double StartAndPrint(int duration = 0)
        {
            double ret = Start(duration);

            Console.WriteLine($"{Name}: {ret.ToString("#,0.00")} ns");

            return ret;
        }

        double MeasureInternal(int duration, TUserVariable state, Action<TUserVariable, int> proc, int interations_pass_value)
        {
            StopFlag = false;

            ManualResetEventSlim ev = new ManualResetEventSlim();

            Thread thread = new Thread(() =>
            {
                ev.Wait();
                Thread.Sleep(duration);
                StopFlag = true;
            });

            thread.IsBackground = true;
            thread.Priority = ThreadPriority.Highest;
            thread.Start();

            long count = 0;
            Stopwatch sw = new Stopwatch();
            ev.Set();
            sw.Start();
            TimeSpan ts1 = sw.Elapsed;
            while (StopFlag == false)
            {
                if (Init == null && interations_pass_value == 0)
                {
                    DummyLoopProc(state, Iterations);
                }
                else
                {
                    proc(state, interations_pass_value);
                    if (interations_pass_value == 0)
                    {
                        for (int i = 0; i < Iterations; i++) Limbo.SInt++;
                    }
                }
                count += Iterations;
            }
            TimeSpan ts2 = sw.Elapsed;
            TimeSpan ts = ts2 - ts1;
            thread.Join();

            double nano = (double)ts.Ticks * 100.0;
            double nano_per_call = nano / (double)count;

            return nano_per_call;
        }

        public double Start(int duration = 0)
        {
            lock (LockObj)
            {
                if (duration <= 0) duration = MicroBenchmarkGlobalParam.DefaultDurationMSecs;

                TUserVariable state = default(TUserVariable);

                double v1 = 0;
                double v2 = 0;

                if (Init != null) state = Init();
                v2 = MeasureInternal(duration, state, Proc, 0);

                if (Init != null) state = Init();
                v1 = MeasureInternal(duration, state, Proc, Iterations);

                double v = Math.Max(v1 - v2, 0);
                //v -= EmptyBaseLine;
                v = Math.Max(v, 0);
                return v;
            }
        }
    }

    class MySeg<T> : ReadOnlySequenceSegment<T>
    {
    }

    class ClassA
    {
        public void TestReal()
        {
            Limbo.SInt++;
        }

        public virtual void TestVirtual()
        {
            Limbo.SInt++;
        }

        //[ThreadStatic]
        public static volatile int ThreadInt;
    }

    class ClassB : ClassA
    {
        public override void TestVirtual()
        {
            Limbo.SInt++;
        }
    }


    class ClassTypeInitTest
    {
        static ClassTypeInitTest()
        {
            Limbo.SInt++;
            TestValue = (int)Limbo.SInt;
        }

        public static volatile int TestValue = 123;
    }

    class GenericClassTest<T>
    {
        public static volatile int TestValue = 123;
    }

    class Program
    {
        static Semaphore s = new Semaphore(1, 1);
        static async Task a(int count)
        {
            for (int i = 0; i < count; i++)
            {
                s.WaitOne();
                s.Release();
            }
        }


        static void thread_static_test()
        {
            while (true)
            {
                int a = ClassA.ThreadInt++;
                WriteLine(a);
            }
        }

        struct TestStructA<T>
        {
            public int a;
        }

        struct TestStructB
        {
            public int A;
            public int B;
        }

        struct CCC
        {
            public int x;
        }


        static volatile int vol1 = 1, vol2 = 2, vol3 = 3;

        static void Test1()
        {
            ClassTypeInitTest.TestValue++;
        }

        static int test_c_program_main()
        {
            int j;
            int total = 0;

            for (j = 3; j <= 20000; j++)
            {
                int k;
                bool ok = true;

                for (k = 2; k < j; k++)
                {
                    if ((j % k) == 0)
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok)
                {
                    total++;
                }
            }

            // 2261

            return total;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static unsafe void SetDWORD(byte[] data, int offset, int value)
        {
            fixed (byte* ptr = data)
                *((int*)(ptr + offset)) = value;
        }

        static unsafe int test_asm_program_main()
        {
            Memory<byte> mem = new byte[4096];
            Span<byte> m = mem.Span;
            byte[] a = new byte[4096];

            int ebp = 2000;
            int eax = 0;
            int edx = 0;


            fixed (byte* ptr = a)
            {
                //	int j;
                //	int total = 0;
                //013974BE  mov         dword ptr [ebp-14h],0 
                *((int*)(ptr + ebp - 0x14)) = 0;

                //	for (j = 3;j <= 20000;j++)
                //013974C5  mov         dword ptr [ebp-8],3 
                *((int*)(ptr + ebp - 0x8)) = 3;

                //013974CC  jmp         013974D7 
                goto L_013974D7;

                L_013974CE:
                //013974CE  mov         eax,dword ptr [ebp-8] 
                eax = *((int*)(ptr + ebp - 0x8));

                //013974D1  add         eax,1 
                eax = eax + 1;

                //013974D4  mov         dword ptr [ebp-8],eax 
                *((int*)(ptr + ebp - 0x8)) = eax;

                L_013974D7:
                //013974D7  cmp         dword ptr [ebp-8],4E20h 
                //013974DE  jg          01397528 
                if (*((int*)(ptr + ebp - 0x8)) > 0x4E20) goto L_01397528;

                //	{
                //		int k;
                //		bool ok = true;
                //013974E0  mov         dword ptr [ebp-2Ch],1 
                *((int*)(ptr + ebp - 0x2C)) = 1;

                //		for (k = 2;k < j;k++)
                //013974E7  mov         dword ptr [ebp-20h],2 
                *((int*)(ptr + ebp - 0x20)) = 2;

                //013974EE  jmp         013974F9 
                goto L_013974F9;

                L_013974F0:
                //013974F0  mov         eax,dword ptr [ebp-20h] 
                eax = *((int*)(ptr + ebp - 0x20));

                //013974F3  add         eax,1 
                eax = eax + 1;

                //013974F6  mov         dword ptr [ebp-20h],eax 
                *((int*)(ptr + ebp - 0x20)) = eax;

                L_013974F9:
                //013974F9  mov         eax,dword ptr [ebp-20h] 
                eax = *((int*)(ptr + ebp - 0x20));

                //013974FC  cmp         eax,dword ptr [ebp-8] (j)
                //013974FF  jge         01397517 
                if (*((int*)(ptr + ebp - 0x8)) <= eax) goto L_01397517; // if (j >= k)

                //		{
                //			if ((j % k) == 0)
                //01397501  mov         eax,dword ptr [ebp-8] (j)
                eax = *((int*)(ptr + ebp - 0x8));

                //01397504  cdq              
                edx = (int)((long)eax >> 32);

                //01397505  idiv        eax,dword ptr [ebp-20h] 
                if (false)
                {
                    int _edx = (int)((((long)edx) << 32) + eax) % *((int*)(ptr + ebp - 0x20));
                    eax = (int)((((long)edx) << 32) + eax) / *((int*)(ptr + ebp - 0x20));
                    edx = _edx;
                }
                else
                {
                    int tmp1 = (int)((((long)edx) << 32) + eax);
                    int tmp2 = *((int*)(ptr + ebp - 0x20));
                    //int _edx = tmp1 % tmp2;
                    eax = tmp1 / tmp2;
                    //edx = _edx;
                    edx = tmp1 - tmp2 * eax;
                }

                //01397508  test        edx,edx 
                //0139750A  jne         01397515 
                if (edx != 0) goto L_01397515;

                //			{
                //				ok = false;
                //0139750C  mov         dword ptr [ebp-2Ch],0 
                *((int*)(ptr + ebp - 0x2C)) = 0;

                //				break;
                //01397513  jmp         01397517 
                goto L_01397517;

                //			}
                //		}

                L_01397515:
                //01397515  jmp         013974F0 
                goto L_013974F0;

                //		if (ok)

                L_01397517:
                //01397517  cmp         dword ptr [ebp-2Ch],0 
                //0139751B  je          01397526 
                if (*((int*)(ptr + ebp - 0x2C)) == 0) goto L_01397526;

                //		{
                //			total++;
                //0139751D  mov         eax,dword ptr [ebp-14h] 
                eax = *((int*)(ptr + ebp - 0x14));

                //01397520  add         eax,1 
                eax += 1;

                //01397523  mov         dword ptr [ebp-14h],eax 
                *((int*)(ptr + ebp - 0x14)) = eax;
                //		}
                //	}

                L_01397526:
                //01397526  jmp         013974CE 
                goto L_013974CE;

                L_01397528:
                //	return total;
                // 01397528 8B 45 EC         mov         eax,dword ptr [ebp-14h]
                eax = *((int*)(ptr + ebp - 0x14));


                if (eax != 2261) throw new ApplicationException("Error!");
                return eax;
            }
        }





        static unsafe int test_asm_program_main_2()
        {
            Memory<byte> mem = new byte[4096];
            Span<byte> m = mem.Span;
            byte[] memory = cpu.memory;

            //ref int ebp = ref cpu.ebp;
            //ref int eax = ref cpu.eax;
            //ref int edx = ref cpu.edx;

            int ebp, eax, edx;

            ebp = 2000;


            fixed (byte* ptr = memory)
            {
                //	int j;
                //	int total = 0;
                //013974BE  mov         dword ptr [ebp-14h],0 
                *((int*)(ptr + ebp - 0x14)) = 0;

                //	for (j = 3;j <= 20000;j++)
                //013974C5  mov         dword ptr [ebp-8],3 
                *((int*)(ptr + ebp - 0x8)) = 3;

                //013974CC  jmp         013974D7 
                goto L_013974D7;

                L_013974CE:
                //013974CE  mov         eax,dword ptr [ebp-8] 
                eax = *((int*)(ptr + ebp - 0x8));

                //013974D1  add         eax,1 
                eax = eax + 1;

                //013974D4  mov         dword ptr [ebp-8],eax 
                *((int*)(ptr + ebp - 0x8)) = eax;

                L_013974D7:
                //013974D7  cmp         dword ptr [ebp-8],4E20h 
                //013974DE  jg          01397528 
                if (*((int*)(ptr + ebp - 0x8)) > 0x4E20) goto L_01397528;

                //	{
                //		int k;
                //		bool ok = true;
                //013974E0  mov         dword ptr [ebp-2Ch],1 
                *((int*)(ptr + ebp - 0x2C)) = 1;

                //		for (k = 2;k < j;k++)
                //013974E7  mov         dword ptr [ebp-20h],2 
                *((int*)(ptr + ebp - 0x20)) = 2;

                //013974EE  jmp         013974F9 
                goto L_013974F9;

                L_013974F0:
                //013974F0  mov         eax,dword ptr [ebp-20h] 
                eax = *((int*)(ptr + ebp - 0x20));

                //013974F3  add         eax,1 
                eax = eax + 1;

                //013974F6  mov         dword ptr [ebp-20h],eax 
                *((int*)(ptr + ebp - 0x20)) = eax;

                L_013974F9:
                //013974F9  mov         eax,dword ptr [ebp-20h] 
                eax = *((int*)(ptr + ebp - 0x20));

                //013974FC  cmp         eax,dword ptr [ebp-8] (j)
                //013974FF  jge         01397517 
                if (*((int*)(ptr + ebp - 0x8)) <= eax) goto L_01397517; // if (j >= k)

                //		{
                //			if ((j % k) == 0)
                //01397501  mov         eax,dword ptr [ebp-8] (j)
                eax = *((int*)(ptr + ebp - 0x8));

                //01397504  cdq              
                edx = (int)((long)eax >> 32);

                //01397505  idiv        eax,dword ptr [ebp-20h] 
                if (false)
                {
                    int _edx = (int)((((long)edx) << 32) + eax) % *((int*)(ptr + ebp - 0x20));
                    eax = (int)((((long)edx) << 32) + eax) / *((int*)(ptr + ebp - 0x20));
                    edx = _edx;
                }
                else
                {
                    int tmp1 = (int)((((long)edx) << 32) + eax);
                    int tmp2 = *((int*)(ptr + ebp - 0x20));
                    //int _edx = tmp1 % tmp2;
                    eax = tmp1 / tmp2;
                    //edx = _edx;
                    edx = tmp1 - tmp2 * eax;
                }

                //01397508  test        edx,edx 
                //0139750A  jne         01397515 
                if (edx != 0) goto L_01397515;

                //			{
                //				ok = false;
                //0139750C  mov         dword ptr [ebp-2Ch],0 
                *((int*)(ptr + ebp - 0x2C)) = 0;

                //				break;
                //01397513  jmp         01397517 
                goto L_01397517;

                //			}
                //		}

                L_01397515:
                //01397515  jmp         013974F0 
                goto L_013974F0;

                //		if (ok)

                L_01397517:
                //01397517  cmp         dword ptr [ebp-2Ch],0 
                //0139751B  je          01397526 
                if (*((int*)(ptr + ebp - 0x2C)) == 0) goto L_01397526;

                //		{
                //			total++;
                //0139751D  mov         eax,dword ptr [ebp-14h] 
                eax = *((int*)(ptr + ebp - 0x14));

                //01397520  add         eax,1 
                eax += 1;

                //01397523  mov         dword ptr [ebp-14h],eax 
                *((int*)(ptr + ebp - 0x14)) = eax;
                //		}
                //	}

                L_01397526:
                //01397526  jmp         013974CE 
                goto L_013974CE;

                L_01397528:
                //	return total;
                // 01397528 8B 45 EC         mov         eax,dword ptr [ebp-14h]
                eax = *((int*)(ptr + ebp - 0x14));


                if (eax != 2261) throw new ApplicationException("Error!");
                return eax;
            }
        }


        static unsafe int test_asm_program_main_3()
        {
            Memory<byte> mem = new byte[4096];
            Span<byte> m = mem.Span;
            byte[] memory = cpu.memory;

            //ref int ebp = ref cpu.ebp;
            //ref int eax = ref cpu.eax;
            //ref int edx = ref cpu.edx;

            int ebp = 0, eax = 0, edx = 0;

            ebp = 2000;


            fixed (byte* ptr = memory)
            {
                //	int j;
                //	int total = 0;
                //013974BE  mov         dword ptr [ebp-14h],0 
                if (ebp - 0x14 + 4 >= memory.Length) throw new IndexOutOfRangeException($"{ebp} {eax} {edx}");
                * ((int*)(ptr + ebp - 0x14)) = 0;

                //	for (j = 3;j <= 20000;j++)
                //013974C5  mov         dword ptr [ebp-8],3 
                if (ebp - 0x8 + 4 >= memory.Length) throw new IndexOutOfRangeException($"{ebp} {eax} {edx}");
                *((int*)(ptr + ebp - 0x8)) = 3;

                //013974CC  jmp         013974D7 
                goto L_013974D7;

                L_013974CE:
                //013974CE  mov         eax,dword ptr [ebp-8] 
                if (ebp - 0x8 + 4 >= memory.Length) throw new IndexOutOfRangeException($"{ebp} {eax} {edx}");
                eax = *((int*)(ptr + ebp - 0x8));

                //013974D1  add         eax,1 
                eax = eax + 1;

                //013974D4  mov         dword ptr [ebp-8],eax 
                if (ebp - 0x8 + 4 >= memory.Length) throw new IndexOutOfRangeException($"{ebp} {eax} {edx}");
                *((int*)(ptr + ebp - 0x8)) = eax;

                L_013974D7:
                //013974D7  cmp         dword ptr [ebp-8],4E20h 
                //013974DE  jg          01397528 
                if (ebp - 0x8 + 4 >= memory.Length) throw new IndexOutOfRangeException($"{ebp} {eax} {edx}");
                if (*((int*)(ptr + ebp - 0x8)) > 0x4E20) goto L_01397528;

                //	{
                //		int k;
                //		bool ok = true;
                //013974E0  mov         dword ptr [ebp-2Ch],1 
                if (ebp - 0x2C + 4 >= memory.Length) throw new IndexOutOfRangeException($"{ebp} {eax} {edx}");
                *((int*)(ptr + ebp - 0x2C)) = 1;

                //		for (k = 2;k < j;k++)
                //013974E7  mov         dword ptr [ebp-20h],2 
                if (ebp - 0x20 + 4 >= memory.Length) throw new IndexOutOfRangeException($"{ebp} {eax} {edx}");
                *((int*)(ptr + ebp - 0x20)) = 2;

                //013974EE  jmp         013974F9 
                goto L_013974F9;

                L_013974F0:
                //013974F0  mov         eax,dword ptr [ebp-20h] 
                if (ebp - 0x20 + 4 >= memory.Length) throw new IndexOutOfRangeException($"{ebp} {eax} {edx}");
                eax = *((int*)(ptr + ebp - 0x20));

                //013974F3  add         eax,1 
                eax = eax + 1;

                //013974F6  mov         dword ptr [ebp-20h],eax 
                if (ebp - 0x20 + 4 >= memory.Length) throw new IndexOutOfRangeException($"{ebp} {eax} {edx}");
                *((int*)(ptr + ebp - 0x20)) = eax;

                L_013974F9:
                //013974F9  mov         eax,dword ptr [ebp-20h] 
                if (ebp - 0x20 + 4 >= memory.Length) throw new IndexOutOfRangeException($"{ebp} {eax} {edx}");
                eax = *((int*)(ptr + ebp - 0x20));

                //013974FC  cmp         eax,dword ptr [ebp-8] (j)
                //013974FF  jge         01397517 
                if (ebp - 0x8 + 4 >= memory.Length) throw new IndexOutOfRangeException($"{ebp} {eax} {edx}");
                if (*((int*)(ptr + ebp - 0x8)) <= eax) goto L_01397517; // if (j >= k)

                //		{
                //			if ((j % k) == 0)
                //01397501  mov         eax,dword ptr [ebp-8] (j)
                if (ebp - 0x8 + 4 >= memory.Length) throw new IndexOutOfRangeException($"{ebp} {eax} {edx}");
                eax = *((int*)(ptr + ebp - 0x8));

                //01397504  cdq              
                edx = (int)((long)eax >> 32);

                //01397505  idiv        eax,dword ptr [ebp-20h] 
                if (false)
                {
                    int _edx = (int)((((long)edx) << 32) + eax) % *((int*)(ptr + ebp - 0x20));
                    eax = (int)((((long)edx) << 32) + eax) / *((int*)(ptr + ebp - 0x20));
                    edx = _edx;
                }
                else
                {
                    int tmp1 = (int)((((long)edx) << 32) + eax);
                    int tmp2 = *((int*)(ptr + ebp - 0x20));
                    //int _edx = tmp1 % tmp2;
                    eax = tmp1 / tmp2;
                    //edx = _edx;
                    edx = tmp1 - tmp2 * eax;
                }

                //01397508  test        edx,edx 
                //0139750A  jne         01397515 
                if (edx != 0) goto L_01397515;

                //			{
                //				ok = false;
                //0139750C  mov         dword ptr [ebp-2Ch],0 
                if (ebp - 0x2c + 4 >= memory.Length) throw new IndexOutOfRangeException($"{ebp} {eax} {edx}");
                *((int*)(ptr + ebp - 0x2C)) = 0;

                //				break;
                //01397513  jmp         01397517 
                goto L_01397517;

                //			}
                //		}

                L_01397515:
                //01397515  jmp         013974F0 
                goto L_013974F0;

                //		if (ok)

                L_01397517:
                //01397517  cmp         dword ptr [ebp-2Ch],0 
                //0139751B  je          01397526 
                if (ebp - 0x2c + 4 >= memory.Length) throw new IndexOutOfRangeException($"{ebp} {eax} {edx}");
                if (*((int*)(ptr + ebp - 0x2C)) == 0) goto L_01397526;

                //		{
                //			total++;
                //0139751D  mov         eax,dword ptr [ebp-14h] 
                if (ebp - 0x14 + 4 >= memory.Length) throw new IndexOutOfRangeException($"{ebp} {eax} {edx}");
                eax = *((int*)(ptr + ebp - 0x14));

                //01397520  add         eax,1 
                eax += 1;

                //01397523  mov         dword ptr [ebp-14h],eax 
                if (ebp - 0x14 + 4 >= memory.Length) throw new IndexOutOfRangeException($"{ebp} {eax} {edx}");
                *((int*)(ptr + ebp - 0x14)) = eax;
                //		}
                //	}

                L_01397526:
                //01397526  jmp         013974CE 
                goto L_013974CE;

                L_01397528:
                //	return total;
                // 01397528 8B 45 EC         mov         eax,dword ptr [ebp-14h]
                if (ebp - 0x14 + 4 >= memory.Length) throw new IndexOutOfRangeException($"{ebp} {eax} {edx}");
                eax = *((int*)(ptr + ebp - 0x14));


                if (eax != 2261) throw new ApplicationException("Error!");
                return eax;
            }
        }



        public class TestCpu
        {
            public byte[] memory = new byte[4096];
            public IntPtr heap;
            public IntPtr table;
            public IntPtr flags;
            public int[] flags2 = new int[4096];
            public int last_write_offset = -1;
            public int last_read_offset = -1;
        }

        static TestCpu cpu = new TestCpu();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static unsafe void memory_write(byte* ptr, int offset, int length, int value)
        {
            if (offset + 4 >= length) throw new IndexOutOfRangeException("aaa");
            void** table_ptr = (void**)ptr;

            int index = offset / 256;
            ptr = (byte*)table_ptr[offset];

            if (cpu.last_write_offset != index)
            {
                int* flags_ptr = (int*)cpu.flags;
                if (flags_ptr[index] != 0) throw new IndexOutOfRangeException("bbb");

                flags_ptr[index] = 0;
                cpu.last_write_offset = index;
            }

            *((int*)(ptr + offset)) = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static unsafe int memory_read(byte* ptr, int offset, int length)
        {
            if (offset + 4 >= length) throw new IndexOutOfRangeException("aaa");
            void** table_ptr = (void**)ptr;

            int index = offset / 256;
            ptr = (byte*)table_ptr[offset];

            if (cpu.last_write_offset != index)
            {
                int* flags_ptr = (int*)cpu.flags;
                if (flags_ptr[index] != 0) throw new IndexOutOfRangeException("bbb");
                flags_ptr[index] = 0;

                cpu.last_write_offset = index;
            }

            int ret =  *((int*)(ptr + offset));


            return ret;
        }

        static unsafe int test_asm_program_main_4()
        {
            Memory<byte> mem = new byte[4096];
            Span<byte> m = mem.Span;
            byte[] memory = cpu.memory;
            cpu.heap = Marshal.AllocHGlobal(4096);
            cpu.table = Marshal.AllocHGlobal(4096 * sizeof(void *));
            cpu.flags = Marshal.AllocHGlobal(4096 * sizeof(int));

            void** table_ptr = (void **)cpu.table;
            for (int i = 0; i < 4086; i++)
            {
                table_ptr[i] = (void *)cpu.heap;
            }

            //ref int ebp = ref cpu.ebp;
            //ref int eax = ref cpu.eax;
            //ref int edx = ref cpu.edx;

            int ebp = 0, eax = 0, edx = 0;

            ebp = 2000;


            byte* ptr = (byte *)table_ptr;

            {
                //	int j;
                //	int total = 0;
                //013974BE  mov         dword ptr [ebp-14h],0 
                //if (ebp - 0x14 + 4 >= memory.Length) throw new IndexOutOfRangeException($"{ebp} {eax} {edx}");
                //*((int*)(ptr + ebp - 0x14)) = 0;
                memory_write(ptr, ebp - 0x14, memory.Length, 0);

                //	for (j = 3;j <= 20000;j++)
                //013974C5  mov         dword ptr [ebp-8],3 
                memory_write(ptr, ebp - 0x8, memory.Length, 3);
                if (ebp - 0x8 + 4 >= memory.Length) throw new IndexOutOfRangeException($"{ebp} {eax} {edx}");

                //013974CC  jmp         013974D7 
                goto L_013974D7;

                L_013974CE:
                //013974CE  mov         eax,dword ptr [ebp-8] 
                if (ebp - 0x8 + 4 >= memory.Length) throw new IndexOutOfRangeException($"{ebp} {eax} {edx}");
                eax = memory_read(ptr, ebp - 0x8, memory.Length);

                //013974D1  add         eax,1 
                eax = eax + 1;

                //013974D4  mov         dword ptr [ebp-8],eax 
                memory_write(ptr, ebp - 0x8, memory.Length, eax);

                L_013974D7:
                //013974D7  cmp         dword ptr [ebp-8],4E20h 
                //013974DE  jg          01397528 
                if (memory_read(ptr, ebp - 0x8, memory.Length) > 0x4E20) goto L_01397528;

                //	{
                //		int k;
                //		bool ok = true;
                //013974E0  mov         dword ptr [ebp-2Ch],1 
                memory_write(ptr, ebp - 0x2C, memory.Length, 1);

                //		for (k = 2;k < j;k++)
                //013974E7  mov         dword ptr [ebp-20h],2 
                memory_write(ptr, ebp - 0x20, memory.Length, 2);

                //013974EE  jmp         013974F9 
                goto L_013974F9;

                L_013974F0:
                //013974F0  mov         eax,dword ptr [ebp-20h] 
                eax = memory_read(ptr, ebp - 0x20, memory.Length);

                //013974F3  add         eax,1 
                eax = eax + 1;

                //013974F6  mov         dword ptr [ebp-20h],eax 
                memory_write(ptr, ebp - 0x20, memory.Length, eax);

                L_013974F9:
                //013974F9  mov         eax,dword ptr [ebp-20h] 
                eax = memory_read(ptr, ebp - 0x20, memory.Length);

                //013974FC  cmp         eax,dword ptr [ebp-8] (j)
                //013974FF  jge         01397517 
                if (memory_read(ptr, ebp - 0x8, memory.Length) <= eax) goto L_01397517; // if (j >= k)

                //		{
                //			if ((j % k) == 0)
                //01397501  mov         eax,dword ptr [ebp-8] (j)
                eax = memory_read(ptr, ebp - 0x8, memory.Length);

                //01397504  cdq              
                edx = (int)((long)eax >> 32);

                //01397505  idiv        eax,dword ptr [ebp-20h] 
                if (false)
                {
                    int _edx = (int)((((long)edx) << 32) + eax) % *((int*)(ptr + ebp - 0x20));
                    eax = (int)((((long)edx) << 32) + eax) / *((int*)(ptr + ebp - 0x20));
                    edx = _edx;
                }
                else
                {
                    int tmp1 = (int)((((long)edx) << 32) + eax);
                    int tmp2 = memory_read(ptr, ebp - 0x20, memory.Length);
                    //int _edx = tmp1 % tmp2;
                    eax = tmp1 / tmp2;
                    //edx = _edx;
                    edx = tmp1 - tmp2 * eax;
                }

                //01397508  test        edx,edx 
                //0139750A  jne         01397515 
                if (edx != 0) goto L_01397515;

                //			{
                //				ok = false;
                //0139750C  mov         dword ptr [ebp-2Ch],0 
                memory_write(ptr, ebp - 0x2C, memory.Length, 0);

                //				break;
                //01397513  jmp         01397517 
                goto L_01397517;

                //			}
                //		}

                L_01397515:
                //01397515  jmp         013974F0 
                goto L_013974F0;

                //		if (ok)

                L_01397517:
                //01397517  cmp         dword ptr [ebp-2Ch],0 
                //0139751B  je          01397526 
                if (memory_read(ptr, ebp - 0x2c, memory.Length) == 0) goto L_01397526;

                //		{
                //			total++;
                //0139751D  mov         eax,dword ptr [ebp-14h] 
                eax = memory_read(ptr, ebp - 0x14, memory.Length);

                //01397520  add         eax,1 
                eax += 1;

                //01397523  mov         dword ptr [ebp-14h],eax 
                if (ebp - 0x14 + 4 >= memory.Length) throw new IndexOutOfRangeException($"{ebp} {eax} {edx}");
                memory_write(ptr, ebp - 0x14, memory.Length, eax);
                //		}
                //	}

                L_01397526:
                //01397526  jmp         013974CE 
                goto L_013974CE;

                L_01397528:
                //	return total;
                // 01397528 8B 45 EC         mov         eax,dword ptr [ebp-14h]
                if (ebp - 0x14 + 4 >= memory.Length) throw new IndexOutOfRangeException($"{ebp} {eax} {edx}");
                eax = memory_read(ptr, ebp - 0x14, memory.Length);


                if (eax != 2261) throw new ApplicationException("Error!");
                return eax;
            }
        }

        static uint test_target4(uint a)
        {
            if (a == 0)
            {
                return 0;
            }
            else if (a == 1)
            {
                return 1;
            }
            else
            {
                return test_target4(a - 1) + test_target4(a - 2);
            }
        }

        static uint test_target3()
        {
            uint a = 34;

            return test_target4(a);
        }



        static uint test_target2()
        {
            uint[] tmp = new uint[2000];
            uint p = (uint)tmp.Length;
            uint i, j;
            uint ret = 0;

            for (i = 0; i < p; i++)
            {
                tmp[i] = i;
            }
            for (j = 0; j < 50000; j++)
            {
                for (i = 0; i < p; i++)
                {
                    ret += tmp[i];
                }
            }
            return ret;
        }

        ref struct RefStruct
        {
            public uint a, b, c, d;
        }

        static void ref_struct_test2(ref RefStruct x)
        {
        }

        static void testtest1(Span<int> array)
        {
            ref int x = ref array[0];
            for (int j = 0; j < 100; j++)
            {
                x = (int)Limbo.SInt * 0xcafe;
                x = 123;
            }
            Limbo.UInt += (uint)(x);
        }

        static void ref_struct_test()
        {
            int count = 100;
            RefStruct x = new RefStruct();
            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < 100; j++)
                {
                    x.a = x.b * 0xcafe + x.c * 0xbeef;
                    x.a = 0;
                    x.d = x.a + x.c;
                }
                Limbo.UInt += x.a;
            }

            Span<int> array = stackalloc int[16];

            testtest1(array);
        }

        static void Main(string[] args)
        {
            WriteLine("Started.");
            WriteLine();

            if (true)
            {
            }

            if (false)
            {
                TestStructB bb1 = new TestStructB();
                bb1.A = 123;
                bb1.B = 456;

                TestStructB bb2 = new TestStructB();
                bb2.A = 123;
                bb2.B = 456;

                Console.WriteLine(bb1.GetHashCode());
                Console.WriteLine(bb2.GetHashCode());

                Console.WriteLine(FastHashHelper.ComputeHash32<TestStructB>(ref bb1));
                Console.WriteLine(FastHashHelper.ComputeHash32<TestStructB>(ref bb2));

                Console.WriteLine(FastHashHelper.ComputeHash32<TestStructB>(new TestStructB[] { bb1 }));
                return;
            }

            if (false)
            {
                Thread test_thread_1 = new Thread(thread_static_test);
                test_thread_1.Start();
                Thread test_thread_2 = new Thread(thread_static_test);
                test_thread_2.Start();
                return;
            }

            if (false)
            {
                async Task test1()
                {
                    while (true)
                    {
                        Stopwatch sw = new Stopwatch();
                        sw.Start();
                        await Task.Delay(1000);
                        sw.Stop();
                        Console.WriteLine(sw.Elapsed);

                        //await Task.Delay(1000);

                        for (int i = 0; i < 10000000; i++)
                        {
                            object x = Task.Delay(10000);
                        }
                    }

                }

                test1().Wait();

                return;
            }

            //Tick.DebugBase = 0xffffffff - (uint)Tick.Tick64 - 500;

            //while (true)
            //{
            //    Console.WriteLine(Tick.Tick64);

            //    Thread.Sleep(100);
            //}


            int pool_test_size = 1024;

            //WriteLine(test_c_program_main());
            //return;


            WriteLine("Start Bench");
            FastMemoryAllocator<byte> alloc = new FastMemoryAllocator<byte>();
            alloc.Reserve(65536);

            Memory<byte> byte12345 = new byte[12345];

            var q = new MicroBenchmarkQueue()


                .Add(new MicroBenchmark<Memory<byte>>("ref_struct_test", 10, (state, iterations) =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        ref_struct_test();
                        // 42ns
                    }
                }
                ), true, 190202)

                .Add(new MicroBenchmark<Memory<byte>>("test_target3", 10, (state, iterations) =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        test_target3();
                        // 42ns
                    }
                }
                ), true, 190202)


                .Add(new MicroBenchmark<Memory<byte>>("test_target2", 10, (state, iterations) =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        test_target2();
                        // 66ns
                    }
                }
                ), true, 190202)

                .Add(new MicroBenchmark<Memory<byte>>("test_asm_program_main_4", 5, (state, iterations) =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        test_asm_program_main_4();
                        // 56 ms
                    }
                }
                ), true, 190126)

                .Add(new MicroBenchmark<Memory<byte>>("test_asm_program_main_3", 5, (state, iterations) =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        test_asm_program_main_3();
                        // 56 ms
                    }
                }
                ), true, 190126)

                .Add(new MicroBenchmark<Memory<byte>>("test_asm_program_main_2", 5, (state, iterations) =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        test_asm_program_main_2();
                        // 56 ms
                    }
                }
                ), true, 190125)
                .Add(new MicroBenchmark<Memory<byte>>("test_asm_program_main", 5, (state, iterations) =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        test_asm_program_main();
                        // 56 ms
                    }
                }
                ), true, 190125)


                .Add(new MicroBenchmark<Memory<byte>>("test_c_program", 5, (state, iterations) =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        test_c_program_main();
                        // 48 ms
                    }
                }
                ), true, 190125)


                .Add(new MicroBenchmark<Memory<byte>>("Type init test", 100000, (state, iterations) =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        Test1();
                    }
                }
                ), true, 190111)

                .Add(new MicroBenchmark<Memory<byte>>("Access to GenericClassTest<T> public static field", 100000, (state, iterations) =>
                {
                    int ret = 0;
                    GenericClassTest<sbyte>.TestValue++;
                    GenericClassTest<Decimal>.TestValue++;
                    GenericClassTest<double>.TestValue++;
                    GenericClassTest<string>.TestValue++;
                    GenericClassTest<float>.TestValue++;
                    GenericClassTest<int>.TestValue++;
                    for (int i = 0; i < iterations; i++)
                    {
                        GenericClassTest<int>.TestValue++;
                    }
                    ret = GenericClassTest<int>.TestValue;

                    Limbo.SInt = ret;
                }
                ), true, 190111)

                .Add(new MicroBenchmark<Memory<byte>>("boxing", 100000, (state, iterations) =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        object o = (object)byte12345;
                        Limbo.ObjectSlow = o;
                    }
                }
                ), true, 190111)

                .Add(new MicroBenchmark<Memory<byte>>("unboxing", 100000, (state, iterations) =>
                {
                    object o = (object)byte12345;
                    Limbo.ObjectSlow = o;
                    for (int i = 0; i < iterations; i++)
                    {
                        byte12345 = (Memory<byte>)Limbo.ObjectSlow;
                    }
                }
                ), true, 190111)


                .Add(new MicroBenchmark<Memory<byte>>("normal new byte[64]", 100000, (state, iterations) =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        var x = new byte[64];
                        Limbo.SInt += x.Length;
                    }
                }
                ), true, 190111)

                .Add(new MicroBenchmark<Memory<byte>>("normal new byte[65536]", 100000, (state, iterations) =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        var x = new byte[65536];
                        Limbo.SInt += x.Length;
                    }
                }
                ), true, 190111)

                .Add(new MicroBenchmark<Memory<byte>>("FastMemoryAllocator alloc 65536 and use 64", 100000, (state, iterations) =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        var mem = alloc.Reserve(65536);
                        mem = alloc.Commit(mem, 64);
                        Limbo.SInt += mem.Length;
                    }
                }
                ), true, 190111)

                //.Add(new MicroBenchmark<Memory<byte>>("FastArray read 10000 * 10000", 1000, (state, iterations) =>
                //{
                //    for (int i = 0; i < iterations; i++)
                //    {
                //        Limbo.SInt += fa2.ReadAsMemoryList().Count;
                //    }
                //}
                //), true, 190111)


                .Add(new MicroBenchmark<Memory<byte>>("struct hash 10", 100000, (state, iterations) =>
                {
                    TestStructB bb = new TestStructB();
                    bb.A = 12345;
                    bb.B = 65790;
                    for (int i = 0; i < iterations; i++)
                    {
                        Limbo.SInt += FastHashHelper.ComputeHash32(ref bb);
                        Limbo.SInt += bb.GetHashCode();
                    }
                }
                ), true, 190111)




                .Add(new MicroBenchmark<Memory<byte>>("netinfo", 1000, (state, iterations) =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        //IPGlobalProperties prop = IPGlobalProperties.GetIPGlobalProperties();
                        //UnicastIPAddressInformationCollection info = prop.GetUnicastAddressesAsync().Result;
                        Dns.GetHostAddresses(Dns.GetHostName());
                    }
                }
                ), true, 190111)

                .Add(new MicroBenchmark<Memory<byte>>("tick", 100000, (state, iterations) =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        Limbo.SInt += FastTick64.Now;
                    }
                }
                ), true, 190108)


                .Add(new MicroBenchmark<Memory<byte>>("triple lock", 100000, (state, iterations) =>
                {
                    object lock_obj = new object();

                    void func3()
                    {
                        lock (lock_obj)
                        {
                            Limbo.SInt++;
                        }
                    }

                    void func2()
                    {
                        lock (lock_obj)
                        {
                            func3();
                        }
                    }

                    void func1()
                    {
                        lock (lock_obj)
                        {
                            func2();
                        }
                    }

                    for (int i = 0; i < iterations; i++)
                    {
                        func1();
                    }
                }
                ), true, 190108)

                .Add(new MicroBenchmark<Memory<byte>>("single lock", 100000, (state, iterations) =>
                {
                    object lock_obj = new object();

                    void func1()
                    {
                        lock (lock_obj)
                        {
                            Limbo.SInt++;
                        }
                    }

                    for (int i = 0; i < iterations; i++)
                    {
                        func1();
                    }
                }
                ), true, 190108)

                .Add(new MicroBenchmark<Memory<byte>>("TryGetArrayFast", 100000, (state, iterations) =>
                {
                    Memory<byte> m = new byte[10];
                    for (int i = 0; i < iterations; i++)
                    {
                        var seg = m.AsSegment();
                        Limbo.SInt += seg.Count;
                    }
                }
                ), true, 190115)

                .Add(new MicroBenchmark<Memory<byte>>("MemoryMarshal.TryGetArray", 100000, (state, iterations) =>
                {
                    Memory<byte> a = new byte[1];
                    for (int i = 0; i < iterations; i++)
                    {
                        MemoryMarshal.TryGetArray(a, out ArraySegment<byte> seg);
                    }
                }
                ), true, 190115)

                .Add(new MicroBenchmark<Memory<byte>>("get memory length", 100000, (state, iterations) =>
                {
                    TestStructA<byte> aaa = new TestStructA<byte>();
                    Memory<byte> m = new byte[1];
                    var f = m.GetType().GetField("_object", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    IntPtr a = Marshal.OffsetOf<Memory<byte>>("_object");
                    for (int i = 0; i < iterations; i++)
                    {
                        unsafe
                        {
                            byte* ptr = (byte*)Unsafe.AsPointer(ref m);
                            ptr += a.ToInt64();
                            byte[] o = Unsafe.Read<byte[]>(ptr);
                            Limbo.SInt += o.Length;
                        }
                    }
                }
                ), true, 1)

                .Add(new MicroBenchmark<Memory<byte>>("fast alloc / return byte array", 100000, (state, iterations) =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        byte[] data = MemoryHelper.FastAlloc<byte>(pool_test_size);
                        MemoryHelper.FastFree(data);
                    }
                }
                ), true, 190107)

                .Add(new MicroBenchmark<Memory<byte>>("fast alloc byte array without return", 100000, (state, iterations) =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        byte[] data = MemoryHelper.FastAlloc<byte>(pool_test_size);
                    }
                }
                ), true, 190107)

                .Add(new MicroBenchmark<Memory<byte>>("fast alloc / return memory", 100000, (state, iterations) =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        Memory<byte> data = MemoryHelper.FastAllocMemory<byte>(pool_test_size);
                        MemoryHelper.FastFree(data);
                    }
                }
                ), true, 190107)

                .Add(new MicroBenchmark<Memory<byte>>("fast alloc byte array without return", 100000, (state, iterations) =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        Memory<byte> data = MemoryHelper.FastAllocMemory<byte>(pool_test_size);
                    }
                }
                ), true, 190107)

                .Add(new MicroBenchmark<Memory<byte>>("read/write thread static", 100000, (state, iterations) =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        ClassA.ThreadInt++;
                    }
                }
                ), true, 190105)

                .Add(new MicroBenchmark<Memory<byte>>("read/write thread local", 100000, (state, iterations) =>
                {
                    ThreadLocal<int> x = new ThreadLocal<int>();
                    for (int i = 0; i < iterations; i++)
                    {
                        x.Value++;
                    }
                }
                ), true, 190105)

                .Add(new MicroBenchmark<Memory<byte>>("shared array pool with return", 100000, (state, iterations) =>
                {
                    var pool = ArrayPool<byte>.Shared;

                    for (int i = 0; i < iterations; i++)
                    {
                        byte[] data = pool.Rent(pool_test_size);

                        pool.Return(data);
                    }
                }
                ), true, 190106)

                .Add(new MicroBenchmark<Memory<byte>>("shared array pool without return", 100000, (state, iterations) =>
                {
                    var pool = ArrayPool<byte>.Shared;

                    for (int i = 0; i < iterations; i++)
                    {
                        byte[] data = pool.Rent(pool_test_size);
                    }
                }
                ), true, 190106)

                .Add(new MicroBenchmark<Memory<byte>>("new", 100000, (state, iterations) =>
                {
                    var pool = ArrayPool<byte>.Shared;

                    for (int i = 0; i < iterations; i++)
                    {
                        byte[] data = new byte[pool_test_size];
                    }
                }
                ), true, 190107)

                .Add(new MicroBenchmark<Memory<byte>>("call virtual functions", 100000, (state, iterations) =>
                {
                    ClassA class_a = new ClassB();
                    for (int i = 0; i < iterations; i++)
                    {
                        class_a.TestVirtual();
                    }
                }
                ), true, 190106)

                .Add(new MicroBenchmark<Memory<byte>>("call real functions", 100000, (state, iterations) =>
                {
                    ClassA class_a = new ClassB();
                    for (int i = 0; i < iterations; i++)
                    {
                        class_a.TestReal();
                    }
                }
                ), true, 190106)

                .Add(new MicroBenchmark<Memory<byte>>("new byte[1]", 256, (state, iterations) =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        var x = new byte[1];
                    }
                }
                ), true, 2)

                .Add(new MicroBenchmark<Memory<byte>>("MemoryBuffer WriteSInt32", 32, (state, iterations) =>
                {
                    var buf = state.AsMemoryBuffer();
                    for (int i = 0; i < iterations; i++)
                    {
                        buf.WriteSInt32(i);
                    }
                },
                () =>
                {
                    byte[] data = new byte[128];
                    return data.AsMemory();
                }
                ), true)

                .Add(new MicroBenchmark<Memory<byte>>("SpanBuffer WriteSInt32", 32,
                (state, iterations) =>
                {
                    var buf = state.AsSpanBuffer();
                    for (int i = 0; i < iterations; i++)
                    {
                        buf.WriteSInt32(i);
                    }
                },
                () =>
                {
                    byte[] data = new byte[128];
                    return data.AsMemory();
                }
                ), true)

                .Add(new MicroBenchmark<Memory<byte>>("BinaryPrimitives.WriteInt32LittleEndian", 32,
                (state, iterations) =>
                {
                    byte[] x = new byte[32];
                    BinaryPrimitives.WriteInt32LittleEndian(x, 32); BinaryPrimitives.WriteInt32LittleEndian(x, 32); BinaryPrimitives.WriteInt32LittleEndian(x, 32); BinaryPrimitives.WriteInt32LittleEndian(x, 32);
                    BinaryPrimitives.WriteInt32LittleEndian(x, 32); BinaryPrimitives.WriteInt32LittleEndian(x, 32); BinaryPrimitives.WriteInt32LittleEndian(x, 32); BinaryPrimitives.WriteInt32LittleEndian(x, 32);
                    BinaryPrimitives.WriteInt32LittleEndian(x, 32); BinaryPrimitives.WriteInt32LittleEndian(x, 32); BinaryPrimitives.WriteInt32LittleEndian(x, 32); BinaryPrimitives.WriteInt32LittleEndian(x, 32);
                    BinaryPrimitives.WriteInt32LittleEndian(x, 32); BinaryPrimitives.WriteInt32LittleEndian(x, 32); BinaryPrimitives.WriteInt32LittleEndian(x, 32); BinaryPrimitives.WriteInt32LittleEndian(x, 32);
                    BinaryPrimitives.WriteInt32LittleEndian(x, 32); BinaryPrimitives.WriteInt32LittleEndian(x, 32); BinaryPrimitives.WriteInt32LittleEndian(x, 32); BinaryPrimitives.WriteInt32LittleEndian(x, 32);
                    BinaryPrimitives.WriteInt32LittleEndian(x, 32); BinaryPrimitives.WriteInt32LittleEndian(x, 32); BinaryPrimitives.WriteInt32LittleEndian(x, 32); BinaryPrimitives.WriteInt32LittleEndian(x, 32);
                    BinaryPrimitives.WriteInt32LittleEndian(x, 32); BinaryPrimitives.WriteInt32LittleEndian(x, 32); BinaryPrimitives.WriteInt32LittleEndian(x, 32); BinaryPrimitives.WriteInt32LittleEndian(x, 32);
                    BinaryPrimitives.WriteInt32LittleEndian(x, 32); BinaryPrimitives.WriteInt32LittleEndian(x, 32); BinaryPrimitives.WriteInt32LittleEndian(x, 32); BinaryPrimitives.WriteInt32LittleEndian(x, 32);
                }), true)


                .Add(new MicroBenchmark<Memory<byte>>("byte[].SetSInt32", 32,
                (state, iterations) =>
                {
                    byte[] x = new byte[32];
                    x.SetSInt32(32); x.SetSInt32(32); x.SetSInt32(32); x.SetSInt32(32); x.SetSInt32(32); x.SetSInt32(32); x.SetSInt32(32); x.SetSInt32(32);
                    x.SetSInt32(32); x.SetSInt32(32); x.SetSInt32(32); x.SetSInt32(32); x.SetSInt32(32); x.SetSInt32(32); x.SetSInt32(32); x.SetSInt32(32);
                    x.SetSInt32(32); x.SetSInt32(32); x.SetSInt32(32); x.SetSInt32(32); x.SetSInt32(32); x.SetSInt32(32); x.SetSInt32(32); x.SetSInt32(32);
                    x.SetSInt32(32); x.SetSInt32(32); x.SetSInt32(32); x.SetSInt32(32); x.SetSInt32(32); x.SetSInt32(32); x.SetSInt32(32); x.SetSInt32(32);
                }), true)



#pragma warning disable CS1998 // 非同期メソッドは、'await' 演算子がないため、同期的に実行されます
                .Add(new MicroBenchmark<Memory<byte>>("call Task", 256,
                (state, iterations) =>
                {
                    async Task<int> test2(int x)
                    {
                        return x + 1;
                    }

                    async Task<int> test1(int x)
                    {
                        int ret = x;
                        for (int i = 0; i < iterations; i++)
                            ret += await test2(ret);
                        return ret;
                    }
                    test1(123).Wait();
                }), true, 1)



                .Add(new MicroBenchmark<Memory<byte>>("call ValueTask", 256,
                (state, iterations) =>
                {
                    async ValueTask<int> test2(int x)
                    {
                        return x + 1;
                    }

                    async Task<int> test1(int x)
                    {
                        int ret = x;
                        for (int i = 0; i < iterations; i++)
                            ret += await test2(ret);
                        return ret;
                    }
                    test1(123).Wait();
                }), true, 1)
#pragma warning restore CS1998 // 非同期メソッドは、'await' 演算子がないため、同期的に実行されます


                ;

            q.Run();

            WriteLine();

            return;


            WriteLine($"{SizeOfStruct<Struct1>()}");

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            var dataslot = Thread.AllocateDataSlot();
            Thread.SetData(dataslot, 1);

            Test1 t1 = new Test1();


            WriteLine("Test: " + do_test(10000000, count =>
            {
                Span<byte> ba = new byte[16];
                for (int i = 0; i < count; i++)
                {
                    unsafe
                    {
                        fixed (byte* ptr = ba)
                        {
                            int* u = (int*)(ptr + 1);
                            Util.Volatile += *u;
                        }
                    }
                }
            }).ToString("#,0"));


            {
                byte[] data = new byte[128];
                var memory = data.AsMemory();
                byte[] tmp = new byte[1000];

                Func<int, int> tmp_proc = (x) => x + 1;
                WriteLine("Memory Walk: " + do_test(30000000, count =>
                {
                    var span = memory.Span;

                    ref byte xx = ref MemoryMarshal.GetReference(span);
//                    a(count).Wait();

                    for (int i = 0; i < count; i++)
                    {
                        if (false)
                        {
                            var span2 = span;
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                            BinaryPrimitives.ReadInt32LittleEndian(span2);
                        }
                        else if (true)
                        {
                            var buf = memory.AsMemoryBuffer();

                            if (true)
                            {
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);

                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                                buf.WriteSInt32(123);
                            }
                            else
                            {
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                                buf.ReadUInt8();
                            }
                        }
                        else if (true)
                        {
                            if (false)
                            {
                                var buf = span.AsSpanBuffer();

                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);

                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                                buf.WriteSInt8(123);
                            }
                            else
                            {
                                var buf = span.AsSpanBuffer();

                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                                buf.ReadSInt32();
                            }
                        }
                        else if (false)
                        {
                            unsafe
                            {
                                fixed (byte* p = data)
                                {
                                    uint* p2 = (uint*)p;
                                    uint r = (*p2);
                                    Util.Volatile += (int)r;
                                }
                            }
                        }
                        else
                        {
                            //var memory2 = memory;
                            //var memory3 = memory2.Slice(64);

                            Util.Volatile += (int)(BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(12)));

                            //Util.Volatile += proc(123);

                            //span.Slice(1).CopyTo(tmp);
                            //span.Slice(1).CopyTo(tmp);

                            //memory2.Slice(8);
                            //Util.Volatile += (int)(BinaryPrimitives.ReadUInt32LittleEndian(memory3.Span));
                            //memory3.Slice(8);
                        }
                    }
                }).ToString("#,0"));
            }

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

            WriteLine("GetStructWithSafe (memory direct 2): " + do_test(10000000, count =>
            {
                Memory<byte> memory = t1.Data.AsMemory();
                for (int i = 0; i < count; i++)
                {
                    Util.Volatile += memory.Span.Slice(0, SizeOfStruct<Struct1>())[0].AsStruct<Struct1>().b1;
                    Util.Volatile += memory.Span.Slice(0, SizeOfStruct<Struct1>())[0].AsStruct<Struct1>().b2;
                    Util.Volatile += memory.Span.Slice(0, SizeOfStruct<Struct1>())[0].AsStruct<Struct1>().b3;
                    Util.Volatile += memory.Span.Slice(0, SizeOfStruct<Struct1>())[0].AsStruct<Struct1>().b4;
                }
            }).ToString("#,0"));


            WriteLine("GetStructWithSafe (span in func): " + do_test(100000, count =>
            {
                for (int i = 0; i < count; i++)
                {
                    Util.Volatile += t1.GetStructWithSafe().b1;
                    Util.Volatile += t1.GetStructWithSafe().b2;
                    Util.Volatile += t1.GetStructWithSafe().b3;
                    Util.Volatile += t1.GetStructWithSafe().b4;
                }
            }).ToString("#,0"));

            WriteLine("span advance 1: " + do_test(10000000, count =>
            {
                var span = t1.Data.AsSpan();
                for (int i = 0; i < count; i++)
                {
                    var span2 = span;
                    int offset = i % (span.Length - 2) + 1;
                    Span<byte> s1 = span2.Slice(0, offset);
                    span2 = span2.Slice(offset);
                    Util.Volatile += s1[0];
                    Util.Volatile += span2[0];
                }
            }).ToString("#,0"));

            WriteLine("span advance 2: " + do_test(10000000, count =>
            {
                var span = t1.Data.AsSpan();
                for (int i = 0; i < count; i++)
                {
                    var span2 = span;
                    span2.Advance(i % (span.Length - 2) + 1);
                    Util.Volatile += span2[0];
                }
            }).ToString("#,0"));

            WriteLine("span access: " + do_test(10000000, count =>
            {
                byte[] a = new byte[1000];
                var span = a.AsSpan();
                for (int i = 0; i < count; i++)
                {
                    Util.Volatile += span[i % span.Length];
                }
            }).ToString("#,0"));

            WriteLine("memory access: " + do_test(10000000, count =>
            {
                byte[] a = new byte[1000];
                var memory = a.AsMemory();
                for (int i = 0; i < count; i++)
                {
                    Util.Volatile += memory.Span[i % memory.Length];
                }
            }).ToString("#,0"));

            List<byte[]> keys = new List<byte[]>();
            Dictionary<byte[], int> hash = new Dictionary<byte[], int>();
            for (int i = 0; i < 65536; i++)
            {
                byte[] key = new byte[] { (byte)(i / 256), (byte)(i % 256) };
                keys.Add(key);
                hash[key] = i;
            }

            WriteLine("Hashtable lookup: " + do_test(10000000, count =>
            {
                int num_keys = keys.Count;
                var keys2 = keys.ToArray();
                for (int i = 0; i < count; i++)
                {
                    var key = keys[i % num_keys];
                    Util.Volatile += hash[key];
                }
            }).ToString("#,0"));

            WriteLine("Hashtable scan: " + do_test(10000, count =>
            {
                int num_keys = keys.Count;
                var keys2 = keys.ToArray();
                var values_array = new int[hash.Keys.Count];
                for (int i = 0; i < count; i++)
                {
                    hash.Values.CopyTo(values_array, 0);
                }
            }).ToString("#,0"));


            WriteLine("stopwatch read: " + do_test(10000000, count =>
            {
                Stopwatch w = new Stopwatch();
                w.Start();
                for (int i = 0; i < count; i++)
                {
                    Util.Volatile += (int)w.ElapsedTicks;
                }
            }).ToString("#,0"));

            WriteLine("TickCount read: " + do_test(10000000, count =>
            {
                for (int i = 0; i < count; i++)
                {
                    Util.Volatile += (int)Environment.TickCount;
                }
            }).ToString("#,0"));

            WriteLine("DateTime read: " + do_test(10000000, count =>
            {
                for (int i = 0; i < count; i++)
                {
                    Util.Volatile += (int)DateTime.Now.Ticks;
                }
            }).ToString("#,0"));


            WriteLine("copy advance: " + do_test(10000000, count =>
            {
                for (int i = 0; i < count; i++)
                {
                    int offset = i % (t1.Data.Length - 2) + 1;
                    byte[] aa = new byte[t1.Data.Length - offset];
                    Array.Copy(t1.Data, offset, aa, 0, t1.Data.Length - offset);
                    Util.Volatile += aa[0];
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

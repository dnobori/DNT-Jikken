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

    public class BenchProperty1
    {
        public long Value { get; private set; }

        public void Test()
        {
            for (int i = 0; i < 1000; i++)
            {
                Value += Limbo.SInt;
            }
        }
    }

    public class BenchProperty2
    {
        public long Value;

        public void Test()
        {
            for (int i = 0; i < 1000; i++)
            {
                Value += Limbo.SInt;
            }
        }
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


        static void Main(string[] args)
        {
            WriteLine("Started.");
            WriteLine();

            
            var q = new MicroBenchmarkQueue()

                .Add(new MicroBenchmark<Memory<byte>>("BenchProperty1", 256, (state, iterations) =>
                {
                    var x = new BenchProperty1();
                    for (int i = 0; i < iterations; i++)
                    {
                        x.Test();
                        Limbo.SInt += x.Value;
                    }
                }
                ), true, 10)

                .Add(new MicroBenchmark<Memory<byte>>("BenchProperty2", 256, (state, iterations) =>
                {
                    var x = new BenchProperty2();
                    for (int i = 0; i < iterations; i++)
                    {
                        x.Test();
                        Limbo.SInt += x.Value;
                    }
                }
                ), true, 10)

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

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
using System.Buffers.Binary;
using System.IO;

namespace cs_struct_bench1
{
    public ref struct SpanBuffer<T>
    {
        Span<T> InternalBuffer;
        public int CurrentPosition { get; private set; }
        public int Size { get; private set; }

        public Span<T> Span { get => InternalBuffer.Slice(0, Size); }
        public Span<T> SpanBefore { get => Span.Slice(0, CurrentPosition); }
        public Span<T> SpanAfter { get => Span.Slice(CurrentPosition); }

        public SpanBuffer(Span<T> base_span)
        {
            InternalBuffer = base_span;
            CurrentPosition = 0;
            Size = base_span.Length;
        }

        public Span<T> Walk(int size)
        {
            int new_size = checked(CurrentPosition + size);

            if (InternalBuffer.Length < new_size)
            {
                EnsureInternalBufferReserved(new_size);
            }
            var ret = InternalBuffer.Slice(CurrentPosition, size);
            Size = Math.Max(new_size, Size);
            CurrentPosition += size;
            return ret;
        }

        public void Write(Memory<T> data) => Write(data.Span);
        public void Write(ReadOnlyMemory<T> data) => Write(data.Span);

        public void Write(Span<T> data)
        {
            var span = Walk(data.Length);
            data.CopyTo(span);
        }

        public void Write(ReadOnlySpan<T> data)
        {
            var span = Walk(data.Length);
            data.CopyTo(span);
        }

        public ReadOnlySpan<T> Read(int size, bool allow_partial = false)
        {
            int size_read = size;
            if (checked(CurrentPosition + size) > Size)
            {
                if (allow_partial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                size_read = Size - CurrentPosition;
            }

            Span<T> ret = InternalBuffer.Slice(CurrentPosition, size_read);
            CurrentPosition += size_read;
            return ret;
        }

        public void Seek(int offset, SeekOrigin mode)
        {
            int new_position = 0;
            if (mode == SeekOrigin.Current)
                new_position = checked(CurrentPosition + offset);
            else if (mode == SeekOrigin.End)
                new_position = checked(Size + offset);
            else
                new_position = offset;

            if (new_position < 0) throw new ArgumentOutOfRangeException("new_position < 0");
            if (new_position > Size) throw new ArgumentOutOfRangeException("new_position > Size");

            CurrentPosition = new_position;
        }

        public void SeekToBegin() => Seek(0, SeekOrigin.Begin);

        public void SeekToEnd() => Seek(0, SeekOrigin.End);

        public int Read(int size, Span<T> dest)
        {
            if (dest.Length < size) throw new ArgumentException("dest.Length < size");
            var span = Read(size);
            span.CopyTo(dest);
            return span.Length;
        }

        public void EnsureInternalBufferReserved(int new_size)
        {
            if (InternalBuffer.Length >= new_size) return;

            int new_internal_size = InternalBuffer.Length;
            while (new_internal_size < new_size)
                new_internal_size = checked(Math.Max(new_internal_size, 1) * 2);

            InternalBuffer = InternalBuffer.ReAlloc(new_internal_size);
        }

        public void Clear()
        {
            InternalBuffer = new Span<T>();
            CurrentPosition = 0;
            Size = 0;
        }
    }


    public ref struct ReadOnlySpanBuffer<T>
    {
        ReadOnlySpan<T> InternalBuffer;
        public int CurrentPosition { get; private set; }
        public int Size { get; private set; }

        public ReadOnlySpan<T> ReadOnlySpan { get => InternalBuffer.Slice(0, Size); }
        public ReadOnlySpan<T> ReadOnlySpanBefore { get => ReadOnlySpan.Slice(0, CurrentPosition); }
        public ReadOnlySpan<T> ReadOnlySpanAfter { get => ReadOnlySpan.Slice(CurrentPosition); }

        public ReadOnlySpanBuffer(ReadOnlySpan<T> base_span)
        {
            InternalBuffer = base_span;
            CurrentPosition = 0;
            Size = base_span.Length;
        }

        public ReadOnlySpan<T> Read(int size, bool allow_partial = false)
        {
            int size_read = size;
            if (checked(CurrentPosition + size) > Size)
            {
                if (allow_partial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                size_read = Size - CurrentPosition;
            }

            ReadOnlySpan<T> ret = InternalBuffer.Slice(CurrentPosition, size_read);
            CurrentPosition += size_read;
            return ret;
        }

        public void Seek(int offset, SeekOrigin mode)
        {
            int new_position = 0;
            if (mode == SeekOrigin.Current)
                new_position = checked(CurrentPosition + offset);
            else if (mode == SeekOrigin.End)
                new_position = checked(Size + offset);
            else
                new_position = offset;

            if (new_position < 0) throw new ArgumentOutOfRangeException("new_position < 0");
            if (new_position > Size) throw new ArgumentOutOfRangeException("new_position > Size");

            CurrentPosition = new_position;
        }

        public void SeekToBegin() => Seek(0, SeekOrigin.Begin);

        public void SeekToEnd() => Seek(0, SeekOrigin.End);

        public int Read(int size, Span<T> dest)
        {
            if (dest.Length < size) throw new ArgumentException("dest.Length < size");
            var span = Read(size);
            span.CopyTo(dest);
            return span.Length;
        }

        public void Clear()
        {
            InternalBuffer = new ReadOnlySpan<T>();
            CurrentPosition = 0;
            Size = 0;
        }
    }





    public ref struct MemoryBuffer<T>
    {
        Memory<T> InternalBuffer;
        Span<T> InternalSpan;
        public int CurrentPosition { get; private set; }
        public int Size { get; private set; }

        public Memory<T> Memory { get => InternalBuffer.Slice(0, Size); }
        public Memory<T> MemoryBefore { get => Memory.Slice(0, CurrentPosition); }
        public Memory<T> MemoryAfter { get => Memory.Slice(CurrentPosition); }

        public MemoryBuffer(Memory<T> base_span)
        {
            InternalBuffer = base_span;
            CurrentPosition = 0;
            Size = base_span.Length;
            InternalSpan = InternalBuffer.Span;
        }

        public Span<T> Walk(int size)
        {
            int new_size = checked(CurrentPosition + size);
            if (InternalBuffer.Length < new_size)
            {
                EnsureInternalBufferReserved(new_size);
            }
            var ret = InternalSpan.Slice(CurrentPosition, size);
            Size = Math.Max(new_size, Size);
            CurrentPosition += size;
            return ret;
        }

        public void Write(Memory<T> data) => Write(data.Span);
        public void Write(ReadOnlyMemory<T> data) => Write(data.Span);

        public void Write(Span<T> data)
        {
            var span = Walk(data.Length);
            data.CopyTo(span);
        }

        public void Write(ReadOnlySpan<T> data)
        {
            var span = Walk(data.Length);
            data.CopyTo(span);
        }

        public ReadOnlySpan<T> Read(int size, bool allow_partial = false)
        {
            int size_read = size;
            if (checked(CurrentPosition + size) > Size)
            {
                if (allow_partial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                size_read = Size - CurrentPosition;
            }

            ReadOnlySpan<T> ret = InternalSpan.Slice(CurrentPosition, size_read);
            CurrentPosition += size_read;
            return ret;
        }

        public ReadOnlyMemory<T> ReadAsMemory(int size, bool allow_partial = false)
        {
            int size_read = size;
            if (checked(CurrentPosition + size) > Size)
            {
                if (allow_partial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                size_read = Size - CurrentPosition;
            }

            ReadOnlyMemory<T> ret = InternalBuffer.Slice(CurrentPosition, size_read);
            CurrentPosition += size_read;
            return ret;
        }

        public void Seek(int offset, SeekOrigin mode)
        {
            int new_position = 0;
            if (mode == SeekOrigin.Current)
                new_position = checked(CurrentPosition + offset);
            else if (mode == SeekOrigin.End)
                new_position = checked(Size + offset);
            else
                new_position = offset;

            if (new_position < 0) throw new ArgumentOutOfRangeException("new_position < 0");
            if (new_position > Size) throw new ArgumentOutOfRangeException("new_position > Size");

            CurrentPosition = new_position;
        }

        public void SeekToBegin() => Seek(0, SeekOrigin.Begin);

        public void SeekToEnd() => Seek(0, SeekOrigin.End);

        public int Read(int size, Span<T> dest)
        {
            if (dest.Length < size) throw new ArgumentException("dest.Length < size");
            var span = Read(size);
            span.CopyTo(dest);
            return span.Length;
        }

        public int Read(int size, Memory<T> dest)
        {
            if (dest.Length < size) throw new ArgumentException("dest.Length < size");
            var span = ReadAsMemory(size);
            span.CopyTo(dest);
            return span.Length;
        }

        public void EnsureInternalBufferReserved(int new_size)
        {
            if (InternalBuffer.Length >= new_size) return;

            int new_internal_size = InternalBuffer.Length;
            while (new_internal_size < new_size)
                new_internal_size = checked(Math.Max(new_internal_size, 1) * 2);

            InternalBuffer = InternalBuffer.ReAlloc(new_internal_size);
            InternalSpan = InternalBuffer.Span;
        }

        public void Clear()
        {
            InternalBuffer = new Memory<T>();
            CurrentPosition = 0;
            Size = 0;
            InternalSpan = new Span<T>();
        }
    }







    public ref struct ReadOnlyMemoryBuffer<T>
    {
        ReadOnlyMemory<T> InternalBuffer;
        ReadOnlySpan<T> InternalSpan;
        public int CurrentPosition { get; private set; }
        public int Size { get; private set; }

        public ReadOnlyMemory<T> ReadOnlyMemory { get => InternalBuffer.Slice(0, Size); }
        public ReadOnlyMemory<T> ReadOnlyMemoryBefore { get => ReadOnlyMemory.Slice(0, CurrentPosition); }
        public ReadOnlyMemory<T> ReadOnlyMemoryAfter { get => ReadOnlyMemory.Slice(CurrentPosition); }

        public ReadOnlyMemoryBuffer(ReadOnlyMemory<T> base_span)
        {
            InternalBuffer = base_span;
            CurrentPosition = 0;
            Size = base_span.Length;
            InternalSpan = InternalBuffer.Span;
        }

        public ReadOnlySpan<T> Read(int size, bool allow_partial = false)
        {
            int size_read = size;
            if (checked(CurrentPosition + size) > Size)
            {
                if (allow_partial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                size_read = Size - CurrentPosition;
            }

            ReadOnlySpan<T> ret = InternalSpan.Slice(CurrentPosition, size_read);
            CurrentPosition += size_read;
            return ret;
        }

        public ReadOnlyMemory<T> ReadAsMemory(int size, bool allow_partial = false)
        {
            int size_read = size;
            if (checked(CurrentPosition + size) > Size)
            {
                if (allow_partial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                size_read = Size - CurrentPosition;
            }

            ReadOnlyMemory<T> ret = InternalBuffer.Slice(CurrentPosition, size_read);
            CurrentPosition += size_read;
            return ret;
        }

        public void Seek(int offset, SeekOrigin mode)
        {
            int new_position = 0;
            if (mode == SeekOrigin.Current)
                new_position = checked(CurrentPosition + offset);
            else if (mode == SeekOrigin.End)
                new_position = checked(Size + offset);
            else
                new_position = offset;

            if (new_position < 0) throw new ArgumentOutOfRangeException("new_position < 0");
            if (new_position > Size) throw new ArgumentOutOfRangeException("new_position > Size");

            CurrentPosition = new_position;
        }

        public void SeekToBegin() => Seek(0, SeekOrigin.Begin);

        public void SeekToEnd() => Seek(0, SeekOrigin.End);

        public int Read(int size, Span<T> dest)
        {
            if (dest.Length < size) throw new ArgumentException("dest.Length < size");
            var span = Read(size);
            span.CopyTo(dest);
            return span.Length;
        }

        public int Read(int size, Memory<T> dest)
        {
            if (dest.Length < size) throw new ArgumentException("dest.Length < size");
            var span = ReadAsMemory(size);
            span.CopyTo(dest);
            return span.Length;
        }

        public void Clear()
        {
            InternalBuffer = new ReadOnlyMemory<T>();
            CurrentPosition = 0;
            Size = 0;
            InternalSpan = new Span<T>();
        }
    }












    public static class SpanMemoryBufferHelper
    {
        public static SpanBuffer<T> AsSpanBuffer<T>(this Span<T> span) => new SpanBuffer<T>(span);
        public static SpanBuffer<T> AsSpanBuffer<T>(this Memory<T> memory) => new SpanBuffer<T>(memory.Span);
        public static SpanBuffer<T> AsSpanBuffer<T>(this T[] data, int offset, int size) => new SpanBuffer<T>(data.AsSpan(offset, size));

        public static void WriteUInt8(this ref SpanBuffer<byte> buf, byte value) => value.ToBinaryUInt8(buf.Walk(1));
        public static void WriteUInt16(this ref SpanBuffer<byte> buf, ushort value) => value.ToBinaryUInt16(buf.Walk(2));
        public static void WriteUInt32(this ref SpanBuffer<byte> buf, uint value) => value.ToBinaryUInt32(buf.Walk(4));
        public static void WriteUInt64(this ref SpanBuffer<byte> buf, ulong value) => value.ToBinaryUInt64(buf.Walk(8));
        public static void WriteSInt8(this ref SpanBuffer<byte> buf, sbyte value) => value.ToBinarySInt8(buf.Walk(1));
        public static void WriteSInt16(this ref SpanBuffer<byte> buf, short value) => value.ToBinarySInt16(buf.Walk(2));
        public static void WriteSInt32(this ref SpanBuffer<byte> buf, int value) => value.ToBinarySInt32(buf.Walk(4));
        public static void WriteSInt64(this ref SpanBuffer<byte> buf, long value) => value.ToBinarySInt64(buf.Walk(8));

        public static byte ReadUInt8(ref this SpanBuffer<byte> buf) => buf.Read(1).ToUInt8();
        public static ushort ReadUInt16(ref this SpanBuffer<byte> buf) => buf.Read(2).ToUInt16();
        public static uint ReadUInt32(ref this SpanBuffer<byte> buf) => buf.Read(4).ToUInt32();
        public static ulong ReadUInt64(ref this SpanBuffer<byte> buf) => buf.Read(8).ToUInt64();
        public static sbyte ReadSInt8(ref this SpanBuffer<byte> buf) => buf.Read(1).ToSInt8();
        public static short ReadSInt16(ref this SpanBuffer<byte> buf) => buf.Read(2).ToSInt16();
        public static int ReadSInt32(ref this SpanBuffer<byte> buf) => buf.Read(4).ToSInt32();
        public static long ReadSInt64(ref this SpanBuffer<byte> buf) => buf.Read(8).ToSInt64();

        public static ReadOnlySpanBuffer<T> AsReadOnlySpanBuffer<T>(this ReadOnlySpan<T> span) => new ReadOnlySpanBuffer<T>(span);
        public static ReadOnlySpanBuffer<T> AsReadOnlySpanBuffer<T>(this ReadOnlyMemory<T> memory) => new ReadOnlySpanBuffer<T>(memory.Span);
        public static ReadOnlySpanBuffer<T> AsReadOnlySpanBuffer<T>(this T[] data, int offset, int size) => new ReadOnlySpanBuffer<T>(data.AsReadOnlySpan(offset, size));

        public static byte ReadUInt8(ref this ReadOnlySpanBuffer<byte> buf) => buf.Read(1).ToUInt8();
        public static ushort ReadUInt16(ref this ReadOnlySpanBuffer<byte> buf) => buf.Read(2).ToUInt16();
        public static uint ReadUInt32(ref this ReadOnlySpanBuffer<byte> buf) => buf.Read(4).ToUInt32();
        public static ulong ReadUInt64(ref this ReadOnlySpanBuffer<byte> buf) => buf.Read(8).ToUInt64();
        public static sbyte ReadSInt8(ref this ReadOnlySpanBuffer<byte> buf) => buf.Read(1).ToSInt8();
        public static short ReadSInt16(ref this ReadOnlySpanBuffer<byte> buf) => buf.Read(2).ToSInt16();
        public static int ReadSInt32(ref this ReadOnlySpanBuffer<byte> buf) => buf.Read(4).ToSInt32();
        public static long ReadSInt64(ref this ReadOnlySpanBuffer<byte> buf) => buf.Read(8).ToSInt64();



        public static MemoryBuffer<T> AsMemoryBuffer<T>(this Memory<T> memory) => new MemoryBuffer<T>(memory);
        public static MemoryBuffer<T> AsMemoryBuffer<T>(this T[] data, int offset, int size) => new MemoryBuffer<T>(data.AsMemory(offset, size));

        public static void WriteUInt8(this ref MemoryBuffer<byte> buf, byte value) => value.ToBinaryUInt8(buf.Walk(1));
        public static void WriteUInt16(this ref MemoryBuffer<byte> buf, ushort value) => value.ToBinaryUInt16(buf.Walk(2));
        public static void WriteUInt32(this ref MemoryBuffer<byte> buf, uint value) => value.ToBinaryUInt32(buf.Walk(4));
        public static void WriteUInt64(this ref MemoryBuffer<byte> buf, ulong value) => value.ToBinaryUInt64(buf.Walk(8));
        public static void WriteSInt8(this ref MemoryBuffer<byte> buf, sbyte value) => value.ToBinarySInt8(buf.Walk(1));
        public static void WriteSInt16(this ref MemoryBuffer<byte> buf, short value) => value.ToBinarySInt16(buf.Walk(2));
        public static void WriteSInt32(this ref MemoryBuffer<byte> buf, int value) => value.ToBinarySInt32(buf.Walk(4));
        public static void WriteSInt64(this ref MemoryBuffer<byte> buf, long value) => value.ToBinarySInt64(buf.Walk(8));

        public static byte ReadUInt8(ref this MemoryBuffer<byte> buf) => buf.Read(1).ToUInt8();
        public static ushort ReadUInt16(ref this MemoryBuffer<byte> buf) => buf.Read(2).ToUInt16();
        public static uint ReadUInt32(ref this MemoryBuffer<byte> buf) => buf.Read(4).ToUInt32();
        public static ulong ReadUInt64(ref this MemoryBuffer<byte> buf) => buf.Read(8).ToUInt64();
        public static sbyte ReadSInt8(ref this MemoryBuffer<byte> buf) => buf.Read(1).ToSInt8();
        public static short ReadSInt16(ref this MemoryBuffer<byte> buf) => buf.Read(2).ToSInt16();
        public static int ReadSInt32(ref this MemoryBuffer<byte> buf) => buf.Read(4).ToSInt32();
        public static long ReadSInt64(ref this MemoryBuffer<byte> buf) => buf.Read(8).ToSInt64();

        public static ReadOnlyMemoryBuffer<T> AsReadOnlyMemoryBuffer<T>(this ReadOnlyMemory<T> memory) => new ReadOnlyMemoryBuffer<T>(memory);
        public static ReadOnlyMemoryBuffer<T> AsReadOnlyMemoryBuffer<T>(this T[] data, int offset, int size) => new ReadOnlyMemoryBuffer<T>(data.AsReadOnlyMemory(offset, size));

        public static byte ReadUInt8(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(1).ToUInt8();
        public static ushort ReadUInt16(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(2).ToUInt16();
        public static uint ReadUInt32(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(4).ToUInt32();
        public static ulong ReadUInt64(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(8).ToUInt64();
        public static sbyte ReadSInt8(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(1).ToSInt8();
        public static short ReadSInt16(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(2).ToSInt16();
        public static int ReadSInt32(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(4).ToSInt32();
        public static long ReadSInt64(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(8).ToSInt64();

    }

    static class Helper
    {
        public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this Memory<T> memory) => memory;
        public static ReadOnlySpan<T> AsReadOnlySpan<T>(this Span<T> span) => span;

        public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this ArraySegment<T> segment) => segment.AsMemory();
        public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this ArraySegment<T> segment, int start) => segment.AsMemory(start);
        public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this ArraySegment<T> segment, int start, int length) => segment.AsMemory(start, length);
        public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this T[] array) => array.AsMemory();
        public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this T[] array, int start) => array.AsMemory(start);
        public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this T[] array, int start, int length) => array.AsMemory(start, length);
        public static ReadOnlySpan<T> AsReadOnlySpan<T>(this T[] array, int start) => array.AsSpan(start);
        public static ReadOnlySpan<T> AsReadOnlySpan<T>(this T[] array) => array.AsSpan();
        public static ReadOnlySpan<T> AsReadOnlySpan<T>(this ArraySegment<T> segment, int start, int length) => segment.AsSpan(start, length);
        public static ReadOnlySpan<T> AsReadOnlySpan<T>(this ArraySegment<T> segment, int start) => segment.AsSpan(start);
        public static ReadOnlySpan<T> AsReadOnlySpan<T>(this T[] array, int start, int length) => array.AsSpan(start, length);

        public static ushort Endian16(this ushort v) => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(v) : v;
        public static short Endian16(this short v) => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(v) : v;
        public static uint Endian32(this uint v) => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(v) : v;
        public static int Endian32(this int v) => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(v) : v;
        public static ulong Endian64(this ulong v) => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(v) : v;
        public static long Endian64(this long v) => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(v) : v;

        public static ushort ReverseEndian16(this ushort v) => BinaryPrimitives.ReverseEndianness(v);
        public static short ReverseEndian16(this short v) => BinaryPrimitives.ReverseEndianness(v);
        public static uint ReverseEndian32(this uint v) => BinaryPrimitives.ReverseEndianness(v);
        public static int ReverseEndian32(this int v) => BinaryPrimitives.ReverseEndianness(v);
        public static ulong ReverseEndian64(this ulong v) => BinaryPrimitives.ReverseEndianness(v);
        public static long ReverseEndian64(this long v) => BinaryPrimitives.ReverseEndianness(v);




        public static unsafe byte ToUInt8(this byte[] data, int offset = 0)
        {
            return (byte)data[offset];
        }

        public static unsafe sbyte ToSInt8(this byte[] data, int offset = 0)
        {
            return (sbyte)data[offset];
        }

        public static unsafe ushort ToUInt16(this byte[] data, int offset = 0)
        {
            if (checked(offset + sizeof(ushort)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ushort*)ptr)) : *((ushort*)ptr);
        }

        public static unsafe short ToSInt16(this byte[] data, int offset = 0)
        {
            if (checked(offset + sizeof(short)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((short*)ptr)) : *((short*)ptr);
        }

        public static unsafe uint ToUInt32(this byte[] data, int offset = 0)
        {
            if (checked(offset + sizeof(uint)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((uint*)ptr)) : *((uint*)ptr);
        }

        public static unsafe int ToSInt32(this byte[] data, int offset = 0)
        {
            if (checked(offset + sizeof(int)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((int*)ptr)) : *((int*)ptr);
        }

        public static unsafe ulong ToUInt64(this byte[] data, int offset = 0)
        {
            if (checked(offset + sizeof(ulong)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ulong*)ptr)) : *((ulong*)ptr);
        }

        public static unsafe long ToSInt64(this byte[] data, int offset = 0)
        {
            if (checked(offset + sizeof(long)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((long*)ptr)) : *((long*)ptr);
        }

        public static unsafe byte ToUInt8(this Span<byte> span)
        {
            return (byte)span[0];
        }

        public static unsafe sbyte ToSInt8(this Span<byte> span)
        {
            return (sbyte)span[0];
        }

        public static unsafe ushort ToUInt16(this Span<byte> span)
        {
            if (span.Length < sizeof(ushort)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ushort*)ptr)) : *((ushort*)ptr);
        }

        public static unsafe short ToSInt16(this Span<byte> span)
        {
            if (span.Length < sizeof(short)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((short*)ptr)) : *((short*)ptr);
        }

        public static unsafe uint ToUInt32(this Span<byte> span)
        {
            if (span.Length < sizeof(uint)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((uint*)ptr)) : *((uint*)ptr);
        }

        public static unsafe int ToSInt32(this Span<byte> span)
        {
            if (span.Length < sizeof(int)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((int*)ptr)) : *((int*)ptr);
        }

        public static unsafe ulong ToUInt64(this Span<byte> span)
        {
            if (span.Length < sizeof(ulong)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ulong*)ptr)) : *((ulong*)ptr);
        }

        public static unsafe long ToSInt64(this Span<byte> span)
        {
            if (span.Length < sizeof(long)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((long*)ptr)) : *((long*)ptr);
        }

        public static unsafe byte ToUInt8(this ReadOnlySpan<byte> span)
        {
            return (byte)span[0];
        }

        public static unsafe sbyte ToSInt8(this ReadOnlySpan<byte> span)
        {
            return (sbyte)span[0];
        }

        public static unsafe ushort ToUInt16(this ReadOnlySpan<byte> span)
        {
            if (span.Length < sizeof(ushort)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ushort*)ptr)) : *((ushort*)ptr);
        }

        public static unsafe short ToSInt16(this ReadOnlySpan<byte> span)
        {
            if (span.Length < sizeof(short)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((short*)ptr)) : *((short*)ptr);
        }

        public static unsafe uint ToUInt32(this ReadOnlySpan<byte> span)
        {
            if (span.Length < sizeof(uint)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((uint*)ptr)) : *((uint*)ptr);
        }

        public static unsafe int ToSInt32(this ReadOnlySpan<byte> span)
        {
            if (span.Length < sizeof(int)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((int*)ptr)) : *((int*)ptr);
        }

        public static unsafe ulong ToUInt64(this ReadOnlySpan<byte> span)
        {
            if (span.Length < sizeof(ulong)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ulong*)ptr)) : *((ulong*)ptr);
        }

        public static unsafe long ToSInt64(this ReadOnlySpan<byte> span)
        {
            if (span.Length < sizeof(long)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((long*)ptr)) : *((long*)ptr);
        }

        public static unsafe byte ToUInt8(this Memory<byte> memory)
        {
            return (byte)memory.Span[0];
        }

        public static unsafe sbyte ToSInt8(this Memory<byte> memory)
        {
            return (sbyte)memory.Span[0];
        }

        public static unsafe ushort ToUInt16(this Memory<byte> memory)
        {
            if (memory.Length < sizeof(ushort)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ushort*)ptr)) : *((ushort*)ptr);
        }

        public static unsafe short ToSInt16(this Memory<byte> memory)
        {
            if (memory.Length < sizeof(short)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((short*)ptr)) : *((short*)ptr);
        }

        public static unsafe uint ToUInt32(this Memory<byte> memory)
        {
            if (memory.Length < sizeof(uint)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((uint*)ptr)) : *((uint*)ptr);
        }

        public static unsafe int ToSInt32(this Memory<byte> memory)
        {
            if (memory.Length < sizeof(int)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((int*)ptr)) : *((int*)ptr);
        }

        public static unsafe ulong ToUInt64(this Memory<byte> memory)
        {
            if (memory.Length < sizeof(ulong)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ulong*)ptr)) : *((ulong*)ptr);
        }

        public static unsafe long ToSInt64(this Memory<byte> memory)
        {
            if (memory.Length < sizeof(long)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((long*)ptr)) : *((long*)ptr);
        }

        public static unsafe byte ToUInt8(this ReadOnlyMemory<byte> memory)
        {
            return (byte)memory.Span[0];
        }

        public static unsafe sbyte ToSInt8(this ReadOnlyMemory<byte> memory)
        {
            return (sbyte)memory.Span[0];
        }

        public static unsafe ushort ToUInt16(this ReadOnlyMemory<byte> memory)
        {
            if (memory.Length < sizeof(ushort)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ushort*)ptr)) : *((ushort*)ptr);
        }

        public static unsafe short ToSInt16(this ReadOnlyMemory<byte> memory)
        {
            if (memory.Length < sizeof(short)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((short*)ptr)) : *((short*)ptr);
        }

        public static unsafe uint ToUInt32(this ReadOnlyMemory<byte> memory)
        {
            if (memory.Length < sizeof(uint)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((uint*)ptr)) : *((uint*)ptr);
        }

        public static unsafe int ToSInt32(this ReadOnlyMemory<byte> memory)
        {
            if (memory.Length < sizeof(int)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((int*)ptr)) : *((int*)ptr);
        }

        public static unsafe ulong ToUInt64(this ReadOnlyMemory<byte> memory)
        {
            if (memory.Length < sizeof(ulong)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ulong*)ptr)) : *((ulong*)ptr);
        }

        public static unsafe long ToSInt64(this ReadOnlyMemory<byte> memory)
        {
            if (memory.Length < sizeof(long)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((long*)ptr)) : *((long*)ptr);
        }


        public static unsafe void ToBinaryUInt8(this byte value, byte[] data, int offset = 0)
        {
            data[offset] = (byte)value;
        }

        public static unsafe void ToBinarySInt8(this sbyte value, byte[] data, int offset = 0)
        {
            data[offset] = (byte)value;
        }

        public static unsafe void ToBinaryUInt16(this ushort value, byte[] data, int offset = 0)
        {
            if (checked(offset + sizeof(ushort)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((ushort*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void ToBinarySInt16(this short value, byte[] data, int offset = 0)
        {
            if (checked(offset + sizeof(short)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((short*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void ToBinaryUInt32(this uint value, byte[] data, int offset = 0)
        {
            if (checked(offset + sizeof(uint)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((uint*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void ToBinarySInt32(this int value, byte[] data, int offset = 0)
        {
            if (checked(offset + sizeof(int)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((int*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void ToBinaryUInt64(this ulong value, byte[] data, int offset = 0)
        {
            if (checked(offset + sizeof(ulong)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((ulong*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void ToBinarySInt64(this long value, byte[] data, int offset = 0)
        {
            if (checked(offset + sizeof(long)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((long*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void ToBinaryUInt8(this byte value, Span<byte> span)
        {
            span[0] = (byte)value;
        }

        public static unsafe void ToBinarySInt8(this sbyte value, Span<byte> span)
        {
            span[0] = (byte)value;
        }

        public static unsafe void ToBinaryUInt16(this ushort value, Span<byte> span)
        {
            if (span.Length < sizeof(ushort)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((ushort*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void ToBinarySInt16(this short value, Span<byte> span)
        {
            if (span.Length < sizeof(short)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((short*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void ToBinaryUInt32(this uint value, Span<byte> span)
        {
            if (span.Length < sizeof(uint)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((uint*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void ToBinarySInt32(this int value, Span<byte> span)
        {
            if (span.Length < sizeof(int)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((int*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void ToBinaryUInt64(this ulong value, Span<byte> span)
        {
            if (span.Length < sizeof(ulong)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((ulong*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void ToBinarySInt64(this long value, Span<byte> span)
        {
            if (span.Length < sizeof(long)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((long*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void ToBinaryUInt8(this byte value, Memory<byte> memory)
        {
            memory.Span[0] = (byte)value;
        }

        public static unsafe void ToBinarySInt8(this sbyte value, Memory<byte> memory)
        {
            memory.Span[0] = (byte)value;
        }

        public static unsafe void ToBinaryUInt16(this ushort value, Memory<byte> memory)
        {
            if (memory.Length < sizeof(ushort)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((ushort*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void ToBinarySInt16(this short value, Memory<byte> memory)
        {
            if (memory.Length < sizeof(short)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((short*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void ToBinaryUInt32(this uint value, Memory<byte> memory)
        {
            if (memory.Length < sizeof(uint)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((uint*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void ToBinarySInt32(this int value, Memory<byte> memory)
        {
            if (memory.Length < sizeof(int)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((int*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void ToBinaryUInt64(this ulong value, Memory<byte> memory)
        {
            if (memory.Length < sizeof(ulong)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((ulong*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void ToBinarySInt64(this long value, Memory<byte> memory)
        {
            if (memory.Length < sizeof(long)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((long*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }


        public static byte[] ToBinaryUInt8(this byte value) { byte[] r = new byte[1]; value.ToBinaryUInt8(r); return r; }
        public static byte[] ToBinarySInt8(this sbyte value) { byte[] r = new byte[1]; value.ToBinarySInt8(r); return r; }
        public static byte[] ToBinaryUInt16(this ushort value) { byte[] r = new byte[2]; value.ToBinaryUInt16(r); return r; }
        public static byte[] ToBinarySInt16(this short value) { byte[] r = new byte[2]; value.ToBinarySInt16(r); return r; }
        public static byte[] ToBinaryUInt32(this uint value) { byte[] r = new byte[4]; value.ToBinaryUInt32(r); return r; }
        public static byte[] ToBinarySInt32(this int value) { byte[] r = new byte[4]; value.ToBinarySInt32(r); return r; }
        public static byte[] ToBinaryUInt64(this ulong value) { byte[] r = new byte[8]; value.ToBinaryUInt64(r); return r; }
        public static byte[] ToBinarySInt64(this long value) { byte[] r = new byte[8]; value.ToBinarySInt64(r); return r; }




        public static void WalkWrite<T>(ref this Span<T> span, Span<T> data) => data.CopyTo(span.Walk(data.Length));


        public static ReadOnlySpan<T> Walk<T>(ref this ReadOnlySpan<T> span, int size)
        {
            if (size == 0) return Span<T>.Empty;
            if (size < 0) throw new ArgumentOutOfRangeException("size");
            var original = span;
            span = span.Slice(size);
            return original.Slice(0, size);
        }


        public static Span<T> Walk<T>(ref this Span<T> span, int size)
        {
            if (size == 0) return Span<T>.Empty;
            if (size < 0) throw new ArgumentOutOfRangeException("size");
            var original = span;
            span = span.Slice(size);
            return original.Slice(0, size);
        }

        public static void WalkWriteUInt8(ref this Span<byte> span, byte value) => value.ToBinaryUInt8(span.Walk(1));
        public static void WalkWriteUInt16(ref this Span<byte> span, ushort value) => value.ToBinaryUInt16(span.Walk(2));
        public static void WalkWriteUInt32(ref this Span<byte> span, uint value) => value.ToBinaryUInt32(span.Walk(4));
        public static void WalkWriteUInt64(ref this Span<byte> span, ulong value) => value.ToBinaryUInt64(span.Walk(8));
        public static void WalkWriteSInt8(ref this Span<byte> span, sbyte value) => value.ToBinarySInt8(span.Walk(1));
        public static void WalkWriteSInt16(ref this Span<byte> span, short value) => value.ToBinarySInt16(span.Walk(2));
        public static void WalkWriteSInt32(ref this Span<byte> span, int value) => value.ToBinarySInt32(span.Walk(4));
        public static void WalkWriteSInt64(ref this Span<byte> span, long value) => value.ToBinarySInt64(span.Walk(8));

        public static Span<T> WalkRead<T>(ref this Span<T> span, int size) => span.Walk(size);

        public static ReadOnlySpan<T> WalkRead<T>(ref this ReadOnlySpan<T> span, int size) => span.Walk(size);

        public static byte WalkReadUInt8(ref this Span<byte> span) => span.WalkRead(1).ToUInt8();
        public static ushort WalkReadUInt16(ref this Span<byte> span) => span.WalkRead(2).ToUInt16();
        public static uint WalkReadUInt32(ref this Span<byte> span) => span.WalkRead(4).ToUInt32();
        public static ulong WalkReadUInt64(ref this Span<byte> span) => span.WalkRead(8).ToUInt64();
        public static sbyte WalkReadSInt8(ref this Span<byte> span) => span.WalkRead(1).ToSInt8();
        public static short WalkReadSInt16(ref this Span<byte> span) => span.WalkRead(2).ToSInt16();
        public static int WalkReadSInt32(ref this Span<byte> span) => span.WalkRead(4).ToSInt32();
        public static long WalkReadSInt64(ref this Span<byte> span) => span.WalkRead(8).ToSInt64();

        public static byte WalkReadUInt8(ref this ReadOnlySpan<byte> span) => span.WalkRead(1).ToUInt8();
        public static ushort WalkReadUInt16(ref this ReadOnlySpan<byte> span) => span.WalkRead(2).ToUInt16();
        public static uint WalkReadUInt32(ref this ReadOnlySpan<byte> span) => span.WalkRead(4).ToUInt32();
        public static ulong WalkReadUInt64(ref this ReadOnlySpan<byte> span) => span.WalkRead(8).ToUInt64();
        public static sbyte WalkReadSInt8(ref this ReadOnlySpan<byte> span) => span.WalkRead(1).ToSInt8();
        public static short WalkReadSInt16(ref this ReadOnlySpan<byte> span) => span.WalkRead(2).ToSInt16();
        public static int WalkReadSInt32(ref this ReadOnlySpan<byte> span) => span.WalkRead(4).ToSInt32();
        public static long WalkReadSInt64(ref this ReadOnlySpan<byte> span) => span.WalkRead(8).ToSInt64();


        public static Memory<T> Walk<T>(ref this Memory<T> memory, int size)
        {
            if (size == 0) return Memory<T>.Empty;
            if (size < 0) throw new ArgumentOutOfRangeException("size");
            var original = memory;
            memory = memory.Slice(size);
            return original.Slice(0, size);
        }


        public static ReadOnlyMemory<T> Walk<T>(ref this ReadOnlyMemory<T> memory, int size)
        {
            if (size == 0) return ReadOnlyMemory<T>.Empty;
            if (size < 0) throw new ArgumentOutOfRangeException("size");
            var original = memory;
            memory = memory.Slice(size);
            return original.Slice(0, size);
        }

        public static int WalkGetPin<T>(this Memory<T> memory) => WalkGetPin(memory.AsReadOnlyMemory());
        public static int WalkGetPin<T>(this ReadOnlyMemory<T> memory) => memory.AsSegment().Offset;

        public static int WalkGetCurrentLength<T>(this Memory<T> memory, int compare_target_pin) => WalkGetCurrentLength(memory.AsReadOnlyMemory(), compare_target_pin);


        public static int WalkGetCurrentLength<T>(this ReadOnlyMemory<T> memory, int compare_target_pin)
        {
            int current_pin = memory.WalkGetPin();
            if (current_pin < compare_target_pin) throw new ArgumentOutOfRangeException("current_pin < compare_target_pin");
            return current_pin - compare_target_pin;
        }


        public static Memory<T> SliceWithPin<T>(this Memory<T> memory, int pin, int? size = null)
        {
            if (size == 0) return Memory<T>.Empty;
            if (pin < 0) throw new ArgumentOutOfRangeException("pin");

            ArraySegment<T> a = memory.AsSegment();
            if (size == null)
            {
                size = a.Offset + a.Count - pin;
            }
            if (size < 0) throw new ArgumentOutOfRangeException("size");
            if ((a.Offset + a.Count) < pin)
            {
                throw new ArgumentOutOfRangeException("(a.Offset + a.Count) < pin");
            }
            if ((a.Offset + a.Count) < (pin + size))
            {
                throw new ArgumentOutOfRangeException("(a.Offset + a.Count) < (pin + size)");
            }

            ArraySegment<T> b = new ArraySegment<T>(a.Array, pin, size ?? 0);
            return b.AsMemory();
        }


        public static ReadOnlyMemory<T> SliceWithPin<T>(this ReadOnlyMemory<T> memory, int pin, int? size = null)
        {
            if (size == 0) return Memory<T>.Empty;
            if (pin < 0) throw new ArgumentOutOfRangeException("pin");

            ArraySegment<T> a = memory.AsSegment();
            if (size == null)
            {
                size = a.Offset + a.Count - pin;
            }
            if (size < 0) throw new ArgumentOutOfRangeException("size");
            if ((a.Offset + a.Count) < pin)
            {
                throw new ArgumentOutOfRangeException("(a.Offset + a.Count) < pin");
            }
            if ((a.Offset + a.Count) < (pin + size))
            {
                throw new ArgumentOutOfRangeException("(a.Offset + a.Count) < (pin + size)");
            }

            ArraySegment<T> b = new ArraySegment<T>(a.Array, pin, size ?? 0);
            return b.AsMemory();
        }

        public static void WalkAutoRynamicEnsureReserveBuffer<T>(ref this Memory<T> memory, int size) => memory.WalkAutoInternal(size, false, true);
        public static Memory<T> WalkAutoDynamic<T>(ref this Memory<T> memory, int size) => memory.WalkAutoInternal(size, false, false);
        public static Memory<T> WalkAutoStatic<T>(ref this Memory<T> memory, int size) => memory.WalkAutoInternal(size, true, false);


        static Memory<T> WalkAutoInternal<T>(ref this Memory<T> memory, int size, bool no_realloc, bool no_step)
        {
            if (size == 0) return Memory<T>.Empty;
            if (size < 0) throw new ArgumentOutOfRangeException("size");
            if (memory.Length >= size)
            {
                return memory.Walk(size);
            }

            if (((long)memory.Length + (long)size) > int.MaxValue) throw new OverflowException("size");

            ArraySegment<T> a = memory.AsSegment();
            long required_len = (long)a.Offset + (long)a.Count + (long)size;
            if (required_len > int.MaxValue) throw new OverflowException("size");

            int new_len = a.Array.Length;
            while (new_len < required_len)
            {
                new_len = (int)Math.Min(Math.Max((long)new_len, 1) * 128L, int.MaxValue);
            }

            T[] new_array = a.Array;
            if (new_array.Length < new_len)
            {
                if (no_realloc)
                {
                    throw new ArgumentOutOfRangeException("Internal byte array overflow: array.Length < new_len");
                }
                new_array = a.Array.ReAlloc(new_len);
            }

            if (no_step == false)
            {
                a = new ArraySegment<T>(new_array, a.Offset, Math.Max(a.Count, size));
            }
            else
            {
                a = new ArraySegment<T>(new_array, a.Offset, a.Count);
            }

            var m = a.AsMemory();

            if (no_step == false)
            {
                var ret = m.Walk(size);
                memory = m;
                return ret;
            }
            else
            {
                memory = m;
                return Memory<T>.Empty;
            }
        }

        public static void WalkWriteUInt8(ref this Memory<byte> memory, byte value) => value.ToBinaryUInt8(memory.Walk(1));
        public static void WalkWriteUInt16(ref this Memory<byte> memory, ushort value) => value.ToBinaryUInt16(memory.Walk(2));
        public static void WalkWriteUInt32(ref this Memory<byte> memory, uint value) => value.ToBinaryUInt32(memory.Walk(4));
        public static void WalkWriteUInt64(ref this Memory<byte> memory, ulong value) => value.ToBinaryUInt64(memory.Walk(8));
        public static void WalkWriteSInt8(ref this Memory<byte> memory, sbyte value) => value.ToBinarySInt8(memory.Walk(1));
        public static void WalkWriteSInt16(ref this Memory<byte> memory, short value) => value.ToBinarySInt16(memory.Walk(2));
        public static void WalkWriteSInt32(ref this Memory<byte> memory, int value) => value.ToBinarySInt32(memory.Walk(4));
        public static void WalkWriteSInt64(ref this Memory<byte> memory, long value) => value.ToBinarySInt64(memory.Walk(8));
        public static void WalkWrite<T>(ref this Memory<T> memory, Memory<T> data) => data.CopyTo(memory.Walk(data.Length));
        public static void WalkWrite<T>(ref this Memory<T> memory, Span<T> data) => data.CopyTo(memory.Walk(data.Length).Span);
        public static void WalkWrite<T>(ref this Memory<T> memory, T[] data) => data.CopyTo(memory.Walk(data.Length).Span);

        public static void WalkAutoDynamicWriteUInt8(ref this Memory<byte> memory, byte value) => value.ToBinaryUInt8(memory.WalkAutoDynamic(1));
        public static void WalkAutoDynamicWriteUInt16(ref this Memory<byte> memory, ushort value) => value.ToBinaryUInt16(memory.WalkAutoDynamic(2));
        public static void WalkAutoDynamicWriteUInt32(ref this Memory<byte> memory, uint value) => value.ToBinaryUInt32(memory.WalkAutoDynamic(4));
        public static void WalkAutoDynamicWriteUInt64(ref this Memory<byte> memory, ulong value) => value.ToBinaryUInt64(memory.WalkAutoDynamic(8));
        public static void WalkAutoDynamicWriteSInt8(ref this Memory<byte> memory, sbyte value) => value.ToBinarySInt8(memory.WalkAutoDynamic(1));
        public static void WalkAutoDynamicWriteSInt16(ref this Memory<byte> memory, short value) => value.ToBinarySInt16(memory.WalkAutoDynamic(2));
        public static void WalkAutoDynamicWriteSInt32(ref this Memory<byte> memory, int value) => value.ToBinarySInt32(memory.WalkAutoDynamic(4));
        public static void WalkAutoDynamicWriteSInt64(ref this Memory<byte> memory, long value) => value.ToBinarySInt64(memory.WalkAutoDynamic(8));
        public static void WalkAutoDynamicWrite<T>(ref this Memory<T> memory, Memory<T> data) => data.CopyTo(memory.WalkAutoDynamic(data.Length));
        public static void WalkAutoDynamicWrite<T>(ref this Memory<T> memory, Span<T> data) => data.CopyTo(memory.WalkAutoDynamic(data.Length).Span);
        public static void WalkAutoDynamicWrite<T>(ref this Memory<T> memory, T[] data) => data.CopyTo(memory.WalkAutoDynamic(data.Length).Span);

        public static void WalkAutoStaticWriteUInt8(ref this Memory<byte> memory, byte value) => value.ToBinaryUInt8(memory.WalkAutoStatic(1));
        public static void WalkAutoStaticWriteUInt16(ref this Memory<byte> memory, ushort value) => value.ToBinaryUInt16(memory.WalkAutoStatic(2));
        public static void WalkAutoStaticWriteUInt32(ref this Memory<byte> memory, uint value) => value.ToBinaryUInt32(memory.WalkAutoStatic(4));
        public static void WalkAutoStaticWriteUInt64(ref this Memory<byte> memory, ulong value) => value.ToBinaryUInt64(memory.WalkAutoStatic(8));
        public static void WalkAutoStaticWriteSInt8(ref this Memory<byte> memory, sbyte value) => value.ToBinarySInt8(memory.WalkAutoStatic(1));
        public static void WalkAutoStaticWriteSInt16(ref this Memory<byte> memory, short value) => value.ToBinarySInt16(memory.WalkAutoStatic(2));
        public static void WalkAutoStaticWriteSInt32(ref this Memory<byte> memory, int value) => value.ToBinarySInt32(memory.WalkAutoStatic(4));
        public static void WalkAutoStaticWriteSInt64(ref this Memory<byte> memory, long value) => value.ToBinarySInt64(memory.WalkAutoStatic(8));
        public static void WalkAutoStaticWrite<T>(ref this Memory<T> memory, Memory<T> data) => data.CopyTo(memory.WalkAutoStatic(data.Length));
        public static void WalkAutoStaticWrite<T>(ref this Memory<T> memory, Span<T> data) => data.CopyTo(memory.WalkAutoStatic(data.Length).Span);
        public static void WalkAutoStaticWrite<T>(ref this Memory<T> memory, T[] data) => data.CopyTo(memory.WalkAutoStatic(data.Length).Span);

        public static ReadOnlyMemory<T> WalkRead<T>(ref this ReadOnlyMemory<T> memory, int size) => memory.Walk(size);
        public static Memory<T> WalkRead<T>(ref this Memory<T> memory, int size) => memory.Walk(size);

        public static byte WalkReadUInt8(ref this Memory<byte> memory) => memory.WalkRead(1).ToUInt8();
        public static ushort WalkReadUInt16(ref this Memory<byte> memory) => memory.WalkRead(2).ToUInt16();
        public static uint WalkReadUInt32(ref this Memory<byte> memory) => memory.WalkRead(4).ToUInt32();
        public static ulong WalkReadUInt64(ref this Memory<byte> memory) => memory.WalkRead(8).ToUInt64();
        public static sbyte WalkReadSInt8(ref this Memory<byte> memory) => memory.WalkRead(1).ToSInt8();
        public static short WalkReadSInt16(ref this Memory<byte> memory) => memory.WalkRead(2).ToSInt16();
        public static int WalkReadSInt32(ref this Memory<byte> memory) => memory.WalkRead(4).ToSInt32();
        public static long WalkReadSInt64(ref this Memory<byte> memory) => memory.WalkRead(8).ToSInt64();

        public static byte WalkReadUInt8(ref this ReadOnlyMemory<byte> memory) => memory.WalkRead(1).ToUInt8();
        public static ushort WalkReadUInt16(ref this ReadOnlyMemory<byte> memory) => memory.WalkRead(2).ToUInt16();
        public static uint WalkReadUInt32(ref this ReadOnlyMemory<byte> memory) => memory.WalkRead(4).ToUInt32();
        public static ulong WalkReadUInt64(ref this ReadOnlyMemory<byte> memory) => memory.WalkRead(8).ToUInt64();
        public static sbyte WalkReadSInt8(ref this ReadOnlyMemory<byte> memory) => memory.WalkRead(1).ToSInt8();
        public static short WalkReadSInt16(ref this ReadOnlyMemory<byte> memory) => memory.WalkRead(2).ToSInt16();
        public static int WalkReadSInt32(ref this ReadOnlyMemory<byte> memory) => memory.WalkRead(4).ToSInt32();
        public static long WalkReadSInt64(ref this ReadOnlyMemory<byte> memory) => memory.WalkRead(8).ToSInt64();



        public static ArraySegment<T> AsSegment<T>(this Memory<T> memory)
        {
            if (MemoryMarshal.TryGetArray(memory, out ArraySegment<T> seg) == false)
                throw new ArgumentException("Memory<T> cannot be converted to ArraySegment<T>.");

            return seg;
        }


        public static ArraySegment<T> AsSegment<T>(this ReadOnlyMemory<T> memory)
        {
            if (MemoryMarshal.TryGetArray(memory, out ArraySegment<T> seg) == false)
                throw new ArgumentException("Memory<T> cannot be converted to ArraySegment<T>.");

            return seg;
        }


        public static T[] ReAlloc<T>(this T[] src, int new_size)
        {
            if (new_size < 0) throw new ArgumentOutOfRangeException("new_size");
            if (new_size == src.Length)
            {
                return src;
            }

            T[] ret = src;
            Array.Resize(ref ret, new_size);
            return ret;
        }

        public static Span<T> ReAlloc<T>(this Span<T> src, int new_size)
        {
            if (new_size < 0) throw new ArgumentOutOfRangeException("new_size");
            if (new_size == src.Length)
            {
                return src;
            }
            else
            {
                T[] ret = new T[new_size];
                src.Slice(0, Math.Min(src.Length, ret.Length)).CopyTo(ret);
                return ret.AsSpan();
            }
        }

        public static Memory<T> ReAlloc<T>(this Memory<T> src, int new_size)
        {
            if (new_size < 0) throw new ArgumentOutOfRangeException("new_size");
            if (new_size == src.Length)
            {
                return src;
            }
            else
            {
                T[] ret = new T[new_size];
                src.Slice(0, Math.Min(src.Length, ret.Length)).CopyTo(ret);
                return ret.AsMemory();
            }
        }
    }
}

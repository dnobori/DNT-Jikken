﻿using System;
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
using System.Linq;
using System.IO;
using System.Security.Cryptography;

namespace cs_struct_bench1
{
    #region for_benchmark

    public ref struct SpanBuffer<T>
    {
        Span<T> InternalSpan;
        public int CurrentPosition { get; private set; }
        public int Length { get; private set; }

        public Span<T> Span { get => InternalSpan.Slice(0, Length); }
        public Span<T> SpanBefore { get => Span.Slice(0, CurrentPosition); }
        public Span<T> SpanAfter { get => Span.Slice(CurrentPosition); }

        public SpanBuffer(Span<T> base_span)
        {
            InternalSpan = base_span;
            CurrentPosition = 0;
            Length = base_span.Length;
        }

        public static implicit operator SpanBuffer<T>(Span<T> span) => new SpanBuffer<T>(span);
        public static implicit operator SpanBuffer<T>(Memory<T> memory) => new SpanBuffer<T>(memory.Span);
        public static implicit operator SpanBuffer<T>(T[] array) => new SpanBuffer<T>(array.AsSpan());
        public static implicit operator Span<T>(SpanBuffer<T> buf) => buf.Span;
        public static implicit operator ReadOnlySpan<T>(SpanBuffer<T> buf) => buf.Span;
        public static implicit operator ReadOnlySpanBuffer<T>(SpanBuffer<T> buf) => buf.AsReadOnly();

        public SpanBuffer<T> SliceAfter() => Slice(CurrentPosition);
        public SpanBuffer<T> SliceBefore() => Slice(0, CurrentPosition);
        public SpanBuffer<T> Slice(int start) => Slice(start, this.Length - start);
        public SpanBuffer<T> Slice(int start, int length)
        {
            if (start < 0) throw new ArgumentOutOfRangeException("start < 0");
            if (length < 0) throw new ArgumentOutOfRangeException("length < 0");
            if (start > Length) throw new ArgumentOutOfRangeException("start > Size");
            if (checked(start + length) > Length) throw new ArgumentOutOfRangeException("length > Size");
            SpanBuffer<T> ret = new SpanBuffer<T>(this.InternalSpan.Slice(start, length));
            ret.Length = length;
            ret.CurrentPosition = Math.Max(checked(CurrentPosition - start), 0);
            return ret;
        }

        public SpanBuffer<T> Clone()
        {
            SpanBuffer<T> ret = new SpanBuffer<T>(InternalSpan.ToArray());
            ret.Length = Length;
            ret.CurrentPosition = CurrentPosition;
            return ret;
        }

        public ReadOnlySpanBuffer<T> AsReadOnly()
        {
            ReadOnlySpanBuffer<T> ret = new ReadOnlySpanBuffer<T>(Span);
            ret.Seek(CurrentPosition, SeekOrigin.Begin);
            return ret;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public Span<T> Walk(int size, bool no_move = false)
        {
            int new_size = checked(CurrentPosition + size);

            if (InternalSpan.Length < new_size)
            {
                EnsureInternalBufferReserved(new_size);
            }
            var ret = InternalSpan.Slice(CurrentPosition, size);
            Length = Math.Max(new_size, Length);
            if (no_move == false) CurrentPosition += size;
            return ret;
        }

        public void Write(Memory<T> data) => Write(data.Span);
        public void Write(ReadOnlyMemory<T> data) => Write(data.Span);
        public void Write(T[] data, int offset = 0, int? length = null) => Write(data.AsSpan(offset, length ?? data.Length - offset));

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
            if (checked(CurrentPosition + size) > Length)
            {
                if (allow_partial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                size_read = Length - CurrentPosition;
            }

            Span<T> ret = InternalSpan.Slice(CurrentPosition, size_read);
            CurrentPosition += size_read;
            return ret;
        }

        public ReadOnlySpan<T> Peek(int size, bool allow_partial = false)
        {
            int size_read = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allow_partial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                size_read = Length - CurrentPosition;
            }

            Span<T> ret = InternalSpan.Slice(CurrentPosition, size_read);
            return ret;
        }

        public void Seek(int offset, SeekOrigin mode)
        {
            int new_position = 0;
            if (mode == SeekOrigin.Current)
                new_position = checked(CurrentPosition + offset);
            else if (mode == SeekOrigin.End)
                new_position = checked(Length + offset);
            else
                new_position = offset;

            if (new_position < 0) throw new ArgumentOutOfRangeException("new_position < 0");
            if (new_position > Length) throw new ArgumentOutOfRangeException("new_position > Size");

            CurrentPosition = new_position;
        }

        public void SeekToBegin() => Seek(0, SeekOrigin.Begin);

        public void SeekToEnd() => Seek(0, SeekOrigin.End);

        public int Read(Span<T> dest, int size)
        {
            if (dest.Length < size) throw new ArgumentException("dest.Length < size");
            var span = Read(size);
            span.CopyTo(dest);
            return span.Length;
        }

        public int Peek(Span<T> dest, int size)
        {
            if (dest.Length < size) throw new ArgumentException("dest.Length < size");
            var span = Peek(size);
            span.CopyTo(dest);
            return span.Length;
        }

        public int Read(T[] dest, int offset, int size) => Read(dest.AsSpan(offset, size), size);
        public int Peek(T[] dest, int offset, int size) => Peek(dest.AsSpan(offset, size), size);

        public void EnsureInternalBufferReserved(int new_size)
        {
            if (InternalSpan.Length >= new_size) return;

            int new_internal_size = InternalSpan.Length;
            while (new_internal_size < new_size)
                new_internal_size = checked(Math.Max(new_internal_size, 128) * 2);

            InternalSpan = InternalSpan.ReAlloc(new_internal_size);
        }

        public void Clear()
        {
            InternalSpan = new Span<T>();
            CurrentPosition = 0;
            Length = 0;
        }
    }

    public ref struct ReadOnlySpanBuffer<T>
    {
        ReadOnlySpan<T> InternalSpan;
        public int CurrentPosition { get; private set; }
        public int Length { get; private set; }

        public ReadOnlySpan<T> Span { get => InternalSpan.Slice(0, Length); }
        public ReadOnlySpan<T> SpanBefore { get => Span.Slice(0, CurrentPosition); }
        public ReadOnlySpan<T> SpanAfter { get => Span.Slice(CurrentPosition); }

        public static implicit operator ReadOnlySpanBuffer<T>(ReadOnlySpan<T> span) => new ReadOnlySpanBuffer<T>(span);
        public static implicit operator ReadOnlySpanBuffer<T>(ReadOnlyMemory<T> memory) => new ReadOnlySpanBuffer<T>(memory.Span);
        public static implicit operator ReadOnlySpanBuffer<T>(T[] array) => new ReadOnlySpanBuffer<T>(array.AsSpan());
        public static implicit operator ReadOnlySpan<T>(ReadOnlySpanBuffer<T> buf) => buf.Span;

        public ReadOnlySpanBuffer<T> SliceAfter() => Slice(CurrentPosition);
        public ReadOnlySpanBuffer<T> SliceBefore() => Slice(0, CurrentPosition);
        public ReadOnlySpanBuffer<T> Slice(int start) => Slice(start, this.Length - start);
        public ReadOnlySpanBuffer<T> Slice(int start, int length)
        {
            if (start < 0) throw new ArgumentOutOfRangeException("start < 0");
            if (length < 0) throw new ArgumentOutOfRangeException("length < 0");
            if (start > Length) throw new ArgumentOutOfRangeException("start > Size");
            if (checked(start + length) > Length) throw new ArgumentOutOfRangeException("length > Size");
            ReadOnlySpanBuffer<T> ret = new ReadOnlySpanBuffer<T>(this.InternalSpan.Slice(start, length));
            ret.Length = length;
            ret.CurrentPosition = Math.Max(checked(CurrentPosition - start), 0);
            return ret;
        }

        public ReadOnlySpanBuffer<T> Clone()
        {
            ReadOnlySpanBuffer<T> ret = new ReadOnlySpanBuffer<T>(InternalSpan.ToArray());
            ret.Length = Length;
            ret.CurrentPosition = CurrentPosition;
            return ret;
        }

        public SpanBuffer<T> CloneAsWritable()
        {
            SpanBuffer<T> ret = new SpanBuffer<T>(Span.ToArray());
            ret.Seek(CurrentPosition, SeekOrigin.Begin);
            return ret;
        }

        public ReadOnlySpanBuffer(ReadOnlySpan<T> base_span)
        {
            InternalSpan = base_span;
            CurrentPosition = 0;
            Length = base_span.Length;
        }

        public ReadOnlySpan<T> Read(int size, bool allow_partial = false)
        {
            int size_read = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allow_partial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                size_read = Length - CurrentPosition;
            }

            ReadOnlySpan<T> ret = InternalSpan.Slice(CurrentPosition, size_read);
            CurrentPosition += size_read;
            return ret;
        }

        public ReadOnlySpan<T> Peek(int size, bool allow_partial = false)
        {
            int size_read = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allow_partial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                size_read = Length - CurrentPosition;
            }

            ReadOnlySpan<T> ret = InternalSpan.Slice(CurrentPosition, size_read);
            return ret;
        }

        public void Seek(int offset, SeekOrigin mode)
        {
            int new_position = 0;
            if (mode == SeekOrigin.Current)
                new_position = checked(CurrentPosition + offset);
            else if (mode == SeekOrigin.End)
                new_position = checked(Length + offset);
            else
                new_position = offset;

            if (new_position < 0) throw new ArgumentOutOfRangeException("new_position < 0");
            if (new_position > Length) throw new ArgumentOutOfRangeException("new_position > Size");

            CurrentPosition = new_position;
        }

        public void SeekToBegin() => Seek(0, SeekOrigin.Begin);

        public void SeekToEnd() => Seek(0, SeekOrigin.End);

        public int Read(Span<T> dest, int size)
        {
            if (dest.Length < size) throw new ArgumentException("dest.Length < size");
            var span = Read(size);
            span.CopyTo(dest);
            return span.Length;
        }

        public int Peek(Span<T> dest, int size)
        {
            if (dest.Length < size) throw new ArgumentException("dest.Length < size");
            var span = Peek(size);
            span.CopyTo(dest);
            return span.Length;
        }

        public int Read(T[] dest, int offset, int size) => Read(dest.AsSpan(offset, size), size);
        public int Peek(T[] dest, int offset, int size) => Peek(dest.AsSpan(offset, size), size);


        public void Clear()
        {
            InternalSpan = new ReadOnlySpan<T>();
            CurrentPosition = 0;
            Length = 0;
        }
    }

    public ref struct FastMemoryBuffer<T>
    {
        Memory<T> InternalBuffer;
        Span<T> InternalSpan;
        public int CurrentPosition { get; private set; }
        public int Length { get; private set; }

        public Memory<T> Memory { get => InternalBuffer.Slice(0, Length); }
        public Memory<T> MemoryBefore { get => Memory.Slice(0, CurrentPosition); }
        public Memory<T> MemoryAfter { get => Memory.Slice(CurrentPosition); }

        public Span<T> Span { get => InternalBuffer.Slice(0, Length).Span; }
        public Span<T> SpanBefore { get => Memory.Slice(0, CurrentPosition).Span; }
        public Span<T> SpanAfter { get => Memory.Slice(CurrentPosition).Span; }

        public FastMemoryBuffer(Memory<T> base_memory)
        {
            InternalBuffer = base_memory;
            CurrentPosition = 0;
            Length = base_memory.Length;
            InternalSpan = InternalBuffer.Span;
        }

        public static implicit operator FastMemoryBuffer<T>(Memory<T> memory) => new FastMemoryBuffer<T>(memory);
        public static implicit operator FastMemoryBuffer<T>(T[] array) => new FastMemoryBuffer<T>(array.AsMemory());
        public static implicit operator Memory<T>(FastMemoryBuffer<T> buf) => buf.Memory;
        public static implicit operator Span<T>(FastMemoryBuffer<T> buf) => buf.Span;
        public static implicit operator ReadOnlyMemory<T>(FastMemoryBuffer<T> buf) => buf.Memory;
        public static implicit operator ReadOnlySpan<T>(FastMemoryBuffer<T> buf) => buf.Span;
        public static implicit operator FastReadOnlyMemoryBuffer<T>(FastMemoryBuffer<T> buf) => buf.AsReadOnly();
        public static implicit operator SpanBuffer<T>(FastMemoryBuffer<T> buf) => buf.AsSpanBuffer();
        public static implicit operator ReadOnlySpanBuffer<T>(FastMemoryBuffer<T> buf) => buf.AsReadOnlySpanBuffer();

        public FastMemoryBuffer<T> SliceAfter() => Slice(CurrentPosition);
        public FastMemoryBuffer<T> SliceBefore() => Slice(0, CurrentPosition);
        public FastMemoryBuffer<T> Slice(int start) => Slice(start, this.Length - start);
        public FastMemoryBuffer<T> Slice(int start, int length)
        {
            if (start < 0) throw new ArgumentOutOfRangeException("start < 0");
            if (length < 0) throw new ArgumentOutOfRangeException("length < 0");
            if (start > Length) throw new ArgumentOutOfRangeException("start > Size");
            if (checked(start + length) > Length) throw new ArgumentOutOfRangeException("length > Size");
            FastMemoryBuffer<T> ret = new FastMemoryBuffer<T>(this.InternalBuffer.Slice(start, length));
            ret.Length = length;
            ret.CurrentPosition = Math.Max(checked(CurrentPosition - start), 0);
            return ret;
        }

        public FastMemoryBuffer<T> Clone()
        {
            FastMemoryBuffer<T> ret = new FastMemoryBuffer<T>(InternalSpan.ToArray());
            ret.Length = Length;
            ret.CurrentPosition = CurrentPosition;
            return ret;
        }

        public FastReadOnlyMemoryBuffer<T> AsReadOnly()
        {
            FastReadOnlyMemoryBuffer<T> ret = new FastReadOnlyMemoryBuffer<T>(Memory);
            ret.Seek(CurrentPosition, SeekOrigin.Begin);
            return ret;
        }

        public SpanBuffer<T> AsSpanBuffer()
        {
            SpanBuffer<T> ret = new SpanBuffer<T>(Span);
            ret.Seek(CurrentPosition, SeekOrigin.Begin);
            return ret;
        }

        public ReadOnlySpanBuffer<T> AsReadOnlySpanBuffer()
        {
            ReadOnlySpanBuffer<T> ret = new ReadOnlySpanBuffer<T>(Span);
            ret.Seek(CurrentPosition, SeekOrigin.Begin);
            return ret;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public Span<T> Walk(int size, bool no_move = false)
        {
            int new_size = checked(CurrentPosition + size);
            if (InternalBuffer.Length < new_size)
            {
                EnsureInternalBufferReserved(new_size);
            }
            var ret = InternalSpan.Slice(CurrentPosition, size);
            Length = Math.Max(new_size, Length);
            if (no_move == false) CurrentPosition += size;
            return ret;
        }

        public void Write(T[] data, int offset = 0, int? length = null) => Write(data.AsSpan(offset, length ?? data.Length - offset));
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
            if (checked(CurrentPosition + size) > Length)
            {
                if (allow_partial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                size_read = Length - CurrentPosition;
            }

            ReadOnlySpan<T> ret = InternalSpan.Slice(CurrentPosition, size_read);
            CurrentPosition += size_read;
            return ret;
        }

        public ReadOnlySpan<T> Peek(int size, bool allow_partial = false)
        {
            int size_read = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allow_partial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                size_read = Length - CurrentPosition;
            }

            Span<T> ret = InternalSpan.Slice(CurrentPosition, size_read);
            return ret;
        }

        public ReadOnlyMemory<T> ReadAsMemory(int size, bool allow_partial = false)
        {
            int size_read = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allow_partial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                size_read = Length - CurrentPosition;
            }

            ReadOnlyMemory<T> ret = InternalBuffer.Slice(CurrentPosition, size_read);
            CurrentPosition += size_read;
            return ret;
        }

        public ReadOnlyMemory<T> PeekAsMemory(int size, bool allow_partial = false)
        {
            int size_read = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allow_partial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                size_read = Length - CurrentPosition;
            }

            ReadOnlyMemory<T> ret = InternalBuffer.Slice(CurrentPosition, size_read);
            return ret;
        }

        public void Seek(int offset, SeekOrigin mode)
        {
            int new_position = 0;
            if (mode == SeekOrigin.Current)
                new_position = checked(CurrentPosition + offset);
            else if (mode == SeekOrigin.End)
                new_position = checked(Length + offset);
            else
                new_position = offset;

            if (new_position < 0) throw new ArgumentOutOfRangeException("new_position < 0");
            if (new_position > Length) throw new ArgumentOutOfRangeException("new_position > Size");

            CurrentPosition = new_position;
        }

        public void SeekToBegin() => Seek(0, SeekOrigin.Begin);

        public void SeekToEnd() => Seek(0, SeekOrigin.End);

        public int Read(Span<T> dest, int size)
        {
            if (dest.Length < size) throw new ArgumentException("dest.Length < size");
            var span = Read(size);
            span.CopyTo(dest);
            return span.Length;
        }

        public int Peek(Span<T> dest, int size)
        {
            if (dest.Length < size) throw new ArgumentException("dest.Length < size");
            var span = Peek(size);
            span.CopyTo(dest);
            return span.Length;
        }

        public int Read(T[] dest, int offset, int size) => Read(dest.AsSpan(offset, size), size);
        public int Peek(T[] dest, int offset, int size) => Peek(dest.AsSpan(offset, size), size);

        public int Read(Memory<T> dest, int size)
        {
            if (dest.Length < size) throw new ArgumentException("dest.Length < size");
            var span = PeekAsMemory(size);
            span.CopyTo(dest);
            return span.Length;
        }

        public int Peek(Memory<T> dest, int size)
        {
            if (dest.Length < size) throw new ArgumentException("dest.Length < size");
            var span = PeekAsMemory(size);
            span.CopyTo(dest);
            return span.Length;
        }

        public void EnsureInternalBufferReserved(int new_size)
        {
            if (InternalBuffer.Length >= new_size) return;

            int new_internal_size = InternalBuffer.Length;
            while (new_internal_size < new_size)
                new_internal_size = checked(Math.Max(new_internal_size, 128) * 2);

            InternalBuffer = InternalBuffer.ReAlloc(new_internal_size);
            InternalSpan = InternalBuffer.Span;
        }

        public void Clear()
        {
            InternalBuffer = new Memory<T>();
            CurrentPosition = 0;
            Length = 0;
            InternalSpan = new Span<T>();
        }
    }

    public ref struct FastReadOnlyMemoryBuffer<T>
    {
        ReadOnlyMemory<T> InternalBuffer;
        ReadOnlySpan<T> InternalSpan;
        public int CurrentPosition { get; private set; }
        public int Length { get; private set; }

        public ReadOnlyMemory<T> Memory { get => InternalBuffer.Slice(0, Length); }
        public ReadOnlyMemory<T> MemoryBefore { get => Memory.Slice(0, CurrentPosition); }
        public ReadOnlyMemory<T> MemoryAfter { get => Memory.Slice(CurrentPosition); }

        public ReadOnlySpan<T> Span { get => InternalBuffer.Slice(0, Length).Span; }
        public ReadOnlySpan<T> SpanBefore { get => Memory.Slice(0, CurrentPosition).Span; }
        public ReadOnlySpan<T> SpanAfter { get => Memory.Slice(CurrentPosition).Span; }

        public FastReadOnlyMemoryBuffer(ReadOnlyMemory<T> base_memory)
        {
            InternalBuffer = base_memory;
            CurrentPosition = 0;
            Length = base_memory.Length;
            InternalSpan = InternalBuffer.Span;
        }

        public static implicit operator FastReadOnlyMemoryBuffer<T>(ReadOnlyMemory<T> memory) => new FastReadOnlyMemoryBuffer<T>(memory);
        public static implicit operator FastReadOnlyMemoryBuffer<T>(T[] array) => new FastReadOnlyMemoryBuffer<T>(array.AsMemory());
        public static implicit operator ReadOnlyMemory<T>(FastReadOnlyMemoryBuffer<T> buf) => buf.Memory;
        public static implicit operator ReadOnlySpan<T>(FastReadOnlyMemoryBuffer<T> buf) => buf.Span;
        public static implicit operator ReadOnlySpanBuffer<T>(FastReadOnlyMemoryBuffer<T> buf) => buf.AsReadOnlySpanBuffer();

        public FastReadOnlyMemoryBuffer<T> SliceAfter() => Slice(CurrentPosition);
        public FastReadOnlyMemoryBuffer<T> SliceBefore() => Slice(0, CurrentPosition);
        public FastReadOnlyMemoryBuffer<T> Slice(int start) => Slice(start, this.Length - start);
        public FastReadOnlyMemoryBuffer<T> Slice(int start, int length)
        {
            if (start < 0) throw new ArgumentOutOfRangeException("start < 0");
            if (length < 0) throw new ArgumentOutOfRangeException("length < 0");
            if (start > Length) throw new ArgumentOutOfRangeException("start > Size");
            if (checked(start + length) > Length) throw new ArgumentOutOfRangeException("length > Size");
            FastReadOnlyMemoryBuffer<T> ret = new FastReadOnlyMemoryBuffer<T>(this.InternalBuffer.Slice(start, length));
            ret.Length = length;
            ret.CurrentPosition = Math.Max(checked(CurrentPosition - start), 0);
            return ret;
        }

        public FastReadOnlyMemoryBuffer<T> Clone()
        {
            FastReadOnlyMemoryBuffer<T> ret = new FastReadOnlyMemoryBuffer<T>(InternalSpan.ToArray());
            ret.Length = Length;
            ret.CurrentPosition = CurrentPosition;
            return ret;
        }

        public FastMemoryBuffer<T> CloneAsWritable()
        {
            FastMemoryBuffer<T> ret = new FastMemoryBuffer<T>(Span.ToArray());
            ret.Seek(CurrentPosition, SeekOrigin.Begin);
            return ret;
        }

        public ReadOnlySpanBuffer<T> AsReadOnlySpanBuffer()
        {
            ReadOnlySpanBuffer<T> ret = new ReadOnlySpanBuffer<T>(Span);
            ret.Seek(CurrentPosition, SeekOrigin.Begin);
            return ret;
        }

        public ReadOnlySpan<T> Read(int size, bool allow_partial = false)
        {
            int size_read = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allow_partial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                size_read = Length - CurrentPosition;
            }

            ReadOnlySpan<T> ret = InternalSpan.Slice(CurrentPosition, size_read);
            CurrentPosition += size_read;
            return ret;
        }

        public ReadOnlySpan<T> Peek(int size, bool allow_partial = false)
        {
            int size_read = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allow_partial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                size_read = Length - CurrentPosition;
            }

            ReadOnlySpan<T> ret = InternalSpan.Slice(CurrentPosition, size_read);
            return ret;
        }

        public ReadOnlyMemory<T> ReadAsMemory(int size, bool allow_partial = false)
        {
            int size_read = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allow_partial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                size_read = Length - CurrentPosition;
            }

            ReadOnlyMemory<T> ret = InternalBuffer.Slice(CurrentPosition, size_read);
            CurrentPosition += size_read;
            return ret;
        }

        public ReadOnlyMemory<T> PeekAsMemory(int size, bool allow_partial = false)
        {
            int size_read = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allow_partial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                size_read = Length - CurrentPosition;
            }

            ReadOnlyMemory<T> ret = InternalBuffer.Slice(CurrentPosition, size_read);
            return ret;
        }

        public void Seek(int offset, SeekOrigin mode)
        {
            int new_position = 0;
            if (mode == SeekOrigin.Current)
                new_position = checked(CurrentPosition + offset);
            else if (mode == SeekOrigin.End)
                new_position = checked(Length + offset);
            else
                new_position = offset;

            if (new_position < 0) throw new ArgumentOutOfRangeException("new_position < 0");
            if (new_position > Length) throw new ArgumentOutOfRangeException("new_position > Size");

            CurrentPosition = new_position;
        }

        public void SeekToBegin() => Seek(0, SeekOrigin.Begin);

        public void SeekToEnd() => Seek(0, SeekOrigin.End);

        public int Read(Span<T> dest, int size)
        {
            if (dest.Length < size) throw new ArgumentException("dest.Length < size");
            var span = Read(size);
            span.CopyTo(dest);
            return span.Length;
        }

        public int Peek(Span<T> dest, int size)
        {
            if (dest.Length < size) throw new ArgumentException("dest.Length < size");
            var span = Peek(size);
            span.CopyTo(dest);
            return span.Length;
        }

        public int Read(T[] dest, int offset, int size) => Read(dest.AsSpan(offset, size), size);
        public int Peek(T[] dest, int offset, int size) => Peek(dest.AsSpan(offset, size), size);

        public int Read(Memory<T> dest, int size)
        {
            if (dest.Length < size) throw new ArgumentException("dest.Length < size");
            var span = ReadAsMemory(size);
            span.CopyTo(dest);
            return span.Length;
        }

        public int Peek(Memory<T> dest, int size)
        {
            if (dest.Length < size) throw new ArgumentException("dest.Length < size");
            var span = PeekAsMemory(size);
            span.CopyTo(dest);
            return span.Length;
        }

        public void Clear()
        {
            InternalBuffer = new ReadOnlyMemory<T>();
            CurrentPosition = 0;
            Length = 0;
            InternalSpan = new Span<T>();
        }
    }


    public class MemoryBuffer<T>
    {
        Memory<T> InternalBuffer;
        public int CurrentPosition { get; private set; }
        public int Length { get; private set; }

        public Memory<T> Memory { get => InternalBuffer.Slice(0, Length); }
        public Memory<T> MemoryBefore { get => Memory.Slice(0, CurrentPosition); }
        public Memory<T> MemoryAfter { get => Memory.Slice(CurrentPosition); }

        public Span<T> Span { get => InternalBuffer.Slice(0, Length).Span; }
        public Span<T> SpanBefore { get => Memory.Slice(0, CurrentPosition).Span; }
        public Span<T> SpanAfter { get => Memory.Slice(CurrentPosition).Span; }

        public MemoryBuffer() : this(Memory<T>.Empty) { }

        public MemoryBuffer(Memory<T> base_memory)
        {
            InternalBuffer = base_memory;
            CurrentPosition = 0;
            Length = base_memory.Length;
        }

        public static implicit operator MemoryBuffer<T>(Memory<T> memory) => new MemoryBuffer<T>(memory);
        public static implicit operator MemoryBuffer<T>(T[] array) => new MemoryBuffer<T>(array.AsMemory());
        public static implicit operator Memory<T>(MemoryBuffer<T> buf) => buf.Memory;
        public static implicit operator Span<T>(MemoryBuffer<T> buf) => buf.Span;
        public static implicit operator ReadOnlyMemory<T>(MemoryBuffer<T> buf) => buf.Memory;
        public static implicit operator ReadOnlySpan<T>(MemoryBuffer<T> buf) => buf.Span;
        public static implicit operator ReadOnlyMemoryBuffer<T>(MemoryBuffer<T> buf) => buf.AsReadOnly();
        public static implicit operator SpanBuffer<T>(MemoryBuffer<T> buf) => buf.AsSpanBuffer();
        public static implicit operator ReadOnlySpanBuffer<T>(MemoryBuffer<T> buf) => buf.AsReadOnlySpanBuffer();

        public MemoryBuffer<T> SliceAfter() => Slice(CurrentPosition);
        public MemoryBuffer<T> SliceBefore() => Slice(0, CurrentPosition);
        public MemoryBuffer<T> Slice(int start) => Slice(start, this.Length - start);
        public MemoryBuffer<T> Slice(int start, int length)
        {
            if (start < 0) throw new ArgumentOutOfRangeException("start < 0");
            if (length < 0) throw new ArgumentOutOfRangeException("length < 0");
            if (start > Length) throw new ArgumentOutOfRangeException("start > Size");
            if (checked(start + length) > Length) throw new ArgumentOutOfRangeException("length > Size");
            MemoryBuffer<T> ret = new MemoryBuffer<T>(this.InternalBuffer.Slice(start, length));
            ret.Length = length;
            ret.CurrentPosition = Math.Max(checked(CurrentPosition - start), 0);
            return ret;
        }

        public MemoryBuffer<T> Clone()
        {
            MemoryBuffer<T> ret = new MemoryBuffer<T>(InternalBuffer.ToArray());
            ret.Length = Length;
            ret.CurrentPosition = CurrentPosition;
            return ret;
        }

        public ReadOnlyMemoryBuffer<T> AsReadOnly()
        {
            ReadOnlyMemoryBuffer<T> ret = new ReadOnlyMemoryBuffer<T>(Memory);
            ret.Seek(CurrentPosition, SeekOrigin.Begin);
            return ret;
        }

        public SpanBuffer<T> AsSpanBuffer()
        {
            SpanBuffer<T> ret = new SpanBuffer<T>(Span);
            ret.Seek(CurrentPosition, SeekOrigin.Begin);
            return ret;
        }

        public ReadOnlySpanBuffer<T> AsReadOnlySpanBuffer()
        {
            ReadOnlySpanBuffer<T> ret = new ReadOnlySpanBuffer<T>(Span);
            ret.Seek(CurrentPosition, SeekOrigin.Begin);
            return ret;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public Span<T> Walk(int size, bool no_move = false)
        {
            int new_size = checked(CurrentPosition + size);
            if (InternalBuffer.Length < new_size)
            {
                EnsureInternalBufferReserved(new_size);
            }
            var ret = InternalBuffer.Span.Slice(CurrentPosition, size);
            Length = Math.Max(new_size, Length);
            if (no_move == false) CurrentPosition += size;
            return ret;
        }

        public void Write(T[] data, int offset = 0, int? length = null) => Write(data.AsSpan(offset, length ?? data.Length - offset));
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
            if (checked(CurrentPosition + size) > Length)
            {
                if (allow_partial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                size_read = Length - CurrentPosition;
            }

            ReadOnlySpan<T> ret = InternalBuffer.Span.Slice(CurrentPosition, size_read);
            CurrentPosition += size_read;
            return ret;
        }

        public ReadOnlySpan<T> Peek(int size, bool allow_partial = false)
        {
            int size_read = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allow_partial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                size_read = Length - CurrentPosition;
            }

            Span<T> ret = InternalBuffer.Span.Slice(CurrentPosition, size_read);
            return ret;
        }

        public ReadOnlyMemory<T> ReadAsMemory(int size, bool allow_partial = false)
        {
            int size_read = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allow_partial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                size_read = Length - CurrentPosition;
            }

            ReadOnlyMemory<T> ret = InternalBuffer.Slice(CurrentPosition, size_read);
            CurrentPosition += size_read;
            return ret;
        }

        public ReadOnlyMemory<T> PeekAsMemory(int size, bool allow_partial = false)
        {
            int size_read = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allow_partial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                size_read = Length - CurrentPosition;
            }

            ReadOnlyMemory<T> ret = InternalBuffer.Slice(CurrentPosition, size_read);
            return ret;
        }

        public void Seek(int offset, SeekOrigin mode)
        {
            int new_position = 0;
            if (mode == SeekOrigin.Current)
                new_position = checked(CurrentPosition + offset);
            else if (mode == SeekOrigin.End)
                new_position = checked(Length + offset);
            else
                new_position = offset;

            if (new_position < 0) throw new ArgumentOutOfRangeException("new_position < 0");
            if (new_position > Length) throw new ArgumentOutOfRangeException("new_position > Size");

            CurrentPosition = new_position;
        }

        public void SeekToBegin() => Seek(0, SeekOrigin.Begin);

        public void SeekToEnd() => Seek(0, SeekOrigin.End);

        public int Read(Span<T> dest, int size)
        {
            if (dest.Length < size) throw new ArgumentException("dest.Length < size");
            var span = Read(size);
            span.CopyTo(dest);
            return span.Length;
        }

        public int Peek(Span<T> dest, int size)
        {
            if (dest.Length < size) throw new ArgumentException("dest.Length < size");
            var span = Peek(size);
            span.CopyTo(dest);
            return span.Length;
        }

        public int Read(T[] dest, int offset, int size) => Read(dest.AsSpan(offset, size), size);
        public int Peek(T[] dest, int offset, int size) => Peek(dest.AsSpan(offset, size), size);

        public int Read(Memory<T> dest, int size)
        {
            if (dest.Length < size) throw new ArgumentException("dest.Length < size");
            var span = PeekAsMemory(size);
            span.CopyTo(dest);
            return span.Length;
        }

        public int Peek(Memory<T> dest, int size)
        {
            if (dest.Length < size) throw new ArgumentException("dest.Length < size");
            var span = PeekAsMemory(size);
            span.CopyTo(dest);
            return span.Length;
        }

        public void EnsureInternalBufferReserved(int new_size)
        {
            if (InternalBuffer.Length >= new_size) return;

            int new_internal_size = InternalBuffer.Length;
            while (new_internal_size < new_size)
                new_internal_size = checked(Math.Max(new_internal_size, 128) * 2);

            InternalBuffer = InternalBuffer.ReAlloc(new_internal_size);
        }

        public void Clear()
        {
            InternalBuffer = new Memory<T>();
            CurrentPosition = 0;
            Length = 0;
        }
    }

    public class ReadOnlyMemoryBuffer<T>
    {
        ReadOnlyMemory<T> InternalBuffer;
        public int CurrentPosition { get; private set; }
        public int Length { get; private set; }

        public ReadOnlyMemory<T> Memory { get => InternalBuffer.Slice(0, Length); }
        public ReadOnlyMemory<T> MemoryBefore { get => Memory.Slice(0, CurrentPosition); }
        public ReadOnlyMemory<T> MemoryAfter { get => Memory.Slice(CurrentPosition); }

        public ReadOnlySpan<T> Span { get => InternalBuffer.Slice(0, Length).Span; }
        public ReadOnlySpan<T> SpanBefore { get => Memory.Slice(0, CurrentPosition).Span; }
        public ReadOnlySpan<T> SpanAfter { get => Memory.Slice(CurrentPosition).Span; }

        public ReadOnlyMemoryBuffer() : this(ReadOnlyMemory<T>.Empty) { }

        public ReadOnlyMemoryBuffer(ReadOnlyMemory<T> base_memory)
        {
            InternalBuffer = base_memory;
            CurrentPosition = 0;
            Length = base_memory.Length;
        }

        public static implicit operator ReadOnlyMemoryBuffer<T>(ReadOnlyMemory<T> memory) => new ReadOnlyMemoryBuffer<T>(memory);
        public static implicit operator ReadOnlyMemoryBuffer<T>(T[] array) => new ReadOnlyMemoryBuffer<T>(array.AsMemory());
        public static implicit operator ReadOnlyMemory<T>(ReadOnlyMemoryBuffer<T> buf) => buf.Memory;
        public static implicit operator ReadOnlySpan<T>(ReadOnlyMemoryBuffer<T> buf) => buf.Span;
        public static implicit operator ReadOnlySpanBuffer<T>(ReadOnlyMemoryBuffer<T> buf) => buf.AsReadOnlySpanBuffer();

        public ReadOnlyMemoryBuffer<T> SliceAfter() => Slice(CurrentPosition);
        public ReadOnlyMemoryBuffer<T> SliceBefore() => Slice(0, CurrentPosition);
        public ReadOnlyMemoryBuffer<T> Slice(int start) => Slice(start, this.Length - start);
        public ReadOnlyMemoryBuffer<T> Slice(int start, int length)
        {
            if (start < 0) throw new ArgumentOutOfRangeException("start < 0");
            if (length < 0) throw new ArgumentOutOfRangeException("length < 0");
            if (start > Length) throw new ArgumentOutOfRangeException("start > Size");
            if (checked(start + length) > Length) throw new ArgumentOutOfRangeException("length > Size");
            ReadOnlyMemoryBuffer<T> ret = new ReadOnlyMemoryBuffer<T>(this.InternalBuffer.Slice(start, length));
            ret.Length = length;
            ret.CurrentPosition = Math.Max(checked(CurrentPosition - start), 0);
            return ret;
        }

        public ReadOnlyMemoryBuffer<T> Clone()
        {
            ReadOnlyMemoryBuffer<T> ret = new ReadOnlyMemoryBuffer<T>(InternalBuffer.ToArray());
            ret.Length = Length;
            ret.CurrentPosition = CurrentPosition;
            return ret;
        }

        public MemoryBuffer<T> CloneAsWritable()
        {
            MemoryBuffer<T> ret = new MemoryBuffer<T>(Span.ToArray());
            ret.Seek(CurrentPosition, SeekOrigin.Begin);
            return ret;
        }

        public ReadOnlySpanBuffer<T> AsReadOnlySpanBuffer()
        {
            ReadOnlySpanBuffer<T> ret = new ReadOnlySpanBuffer<T>(Span);
            ret.Seek(CurrentPosition, SeekOrigin.Begin);
            return ret;
        }

        public ReadOnlySpan<T> Read(int size, bool allow_partial = false)
        {
            int size_read = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allow_partial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                size_read = Length - CurrentPosition;
            }

            ReadOnlySpan<T> ret = InternalBuffer.Span.Slice(CurrentPosition, size_read);
            CurrentPosition += size_read;
            return ret;
        }

        public ReadOnlySpan<T> Peek(int size, bool allow_partial = false)
        {
            int size_read = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allow_partial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                size_read = Length - CurrentPosition;
            }

            ReadOnlySpan<T> ret = InternalBuffer.Span.Slice(CurrentPosition, size_read);
            return ret;
        }

        public ReadOnlyMemory<T> ReadAsMemory(int size, bool allow_partial = false)
        {
            int size_read = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allow_partial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                size_read = Length - CurrentPosition;
            }

            ReadOnlyMemory<T> ret = InternalBuffer.Slice(CurrentPosition, size_read);
            CurrentPosition += size_read;
            return ret;
        }

        public ReadOnlyMemory<T> PeekAsMemory(int size, bool allow_partial = false)
        {
            int size_read = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allow_partial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                size_read = Length - CurrentPosition;
            }

            ReadOnlyMemory<T> ret = InternalBuffer.Slice(CurrentPosition, size_read);
            return ret;
        }

        public void Seek(int offset, SeekOrigin mode)
        {
            int new_position = 0;
            if (mode == SeekOrigin.Current)
                new_position = checked(CurrentPosition + offset);
            else if (mode == SeekOrigin.End)
                new_position = checked(Length + offset);
            else
                new_position = offset;

            if (new_position < 0) throw new ArgumentOutOfRangeException("new_position < 0");
            if (new_position > Length) throw new ArgumentOutOfRangeException("new_position > Size");

            CurrentPosition = new_position;
        }

        public void SeekToBegin() => Seek(0, SeekOrigin.Begin);

        public void SeekToEnd() => Seek(0, SeekOrigin.End);

        public int Read(Span<T> dest, int size)
        {
            if (dest.Length < size) throw new ArgumentException("dest.Length < size");
            var span = Read(size);
            span.CopyTo(dest);
            return span.Length;
        }

        public int Peek(Span<T> dest, int size)
        {
            if (dest.Length < size) throw new ArgumentException("dest.Length < size");
            var span = Peek(size);
            span.CopyTo(dest);
            return span.Length;
        }

        public int Read(T[] dest, int offset, int size) => Read(dest.AsSpan(offset, size), size);
        public int Peek(T[] dest, int offset, int size) => Peek(dest.AsSpan(offset, size), size);

        public int Read(Memory<T> dest, int size)
        {
            if (dest.Length < size) throw new ArgumentException("dest.Length < size");
            var span = ReadAsMemory(size);
            span.CopyTo(dest);
            return span.Length;
        }

        public int Peek(Memory<T> dest, int size)
        {
            if (dest.Length < size) throw new ArgumentException("dest.Length < size");
            var span = PeekAsMemory(size);
            span.CopyTo(dest);
            return span.Length;
        }

        public void Clear()
        {
            InternalBuffer = new ReadOnlyMemory<T>();
            CurrentPosition = 0;
            Length = 0;
        }
    }


    public static class SpanMemoryBufferHelper
    {
        public static SpanBuffer<T> AsSpanBuffer<T>(this Span<T> span) => new SpanBuffer<T>(span);
        public static SpanBuffer<T> AsSpanBuffer<T>(this Memory<T> memory) => new SpanBuffer<T>(memory.Span);
        public static SpanBuffer<T> AsSpanBuffer<T>(this T[] data, int offset, int size) => new SpanBuffer<T>(data.AsSpan(offset, size));

        public static void WriteBool8(this ref SpanBuffer<byte> buf, bool value) => value.SetBool8(buf.Walk(1, false));
        public static void WriteUInt8(this ref SpanBuffer<byte> buf, byte value) => value.SetUInt8(buf.Walk(1, false));
        public static void WriteUInt16(this ref SpanBuffer<byte> buf, ushort value) => value.SetUInt16(buf.Walk(2, false));
        public static void WriteUInt32(this ref SpanBuffer<byte> buf, uint value) => value.SetUInt32(buf.Walk(4, false));
        public static void WriteUInt64(this ref SpanBuffer<byte> buf, ulong value) => value.SetUInt64(buf.Walk(8, false));
        public static void WriteSInt8(this ref SpanBuffer<byte> buf, sbyte value) => value.SetSInt8(buf.Walk(1, false));
        public static void WriteSInt16(this ref SpanBuffer<byte> buf, short value) => value.SetSInt16(buf.Walk(2, false));
        public static void WriteSInt32(this ref SpanBuffer<byte> buf, int value) => value.SetSInt32(buf.Walk(4, false));
        public static void WriteSInt64(this ref SpanBuffer<byte> buf, long value) => value.SetSInt64(buf.Walk(8, false));

        public static void SetBool8(this ref SpanBuffer<byte> buf, bool value) => value.SetBool8(buf.Walk(1, true));
        public static void SetUInt8(this ref SpanBuffer<byte> buf, byte value) => value.SetUInt8(buf.Walk(1, true));
        public static void SetUInt16(this ref SpanBuffer<byte> buf, ushort value) => value.SetUInt16(buf.Walk(2, true));
        public static void SetUInt32(this ref SpanBuffer<byte> buf, uint value) => value.SetUInt32(buf.Walk(4, true));
        public static void SetUInt64(this ref SpanBuffer<byte> buf, ulong value) => value.SetUInt64(buf.Walk(8, true));
        public static void SetSInt8(this ref SpanBuffer<byte> buf, sbyte value) => value.SetSInt8(buf.Walk(1, true));
        public static void SetSInt16(this ref SpanBuffer<byte> buf, short value) => value.SetSInt16(buf.Walk(2, true));
        public static void SetSInt32(this ref SpanBuffer<byte> buf, int value) => value.SetSInt32(buf.Walk(4, true));
        public static void SetSInt64(this ref SpanBuffer<byte> buf, long value) => value.SetSInt64(buf.Walk(8, true));

        public static bool ReadBool8(ref this SpanBuffer<byte> buf) => buf.Read(1).GetBool8();
        public static byte ReadUInt8(ref this SpanBuffer<byte> buf) => buf.Read(1).GetUInt8();
        public static ushort ReadUInt16(ref this SpanBuffer<byte> buf) => buf.Read(2).GetUInt16();
        public static uint ReadUInt32(ref this SpanBuffer<byte> buf) => buf.Read(4).GetUInt32();
        public static ulong ReadUInt64(ref this SpanBuffer<byte> buf) => buf.Read(8).GetUInt64();
        public static sbyte ReadSInt8(ref this SpanBuffer<byte> buf) => buf.Read(1).GetSInt8();
        public static short ReadSInt16(ref this SpanBuffer<byte> buf) => buf.Read(2).GetSInt16();
        public static int ReadSInt32(ref this SpanBuffer<byte> buf) => buf.Read(4).GetSInt32();
        public static long ReadSInt64(ref this SpanBuffer<byte> buf) => buf.Read(8).GetSInt64();

        public static bool PeekBool8(ref this SpanBuffer<byte> buf) => buf.Peek(1).GetBool8();
        public static byte PeekUInt8(ref this SpanBuffer<byte> buf) => buf.Peek(1).GetUInt8();
        public static ushort PeekUInt16(ref this SpanBuffer<byte> buf) => buf.Peek(2).GetUInt16();
        public static uint PeekUInt32(ref this SpanBuffer<byte> buf) => buf.Peek(4).GetUInt32();
        public static ulong PeekUInt64(ref this SpanBuffer<byte> buf) => buf.Peek(8).GetUInt64();
        public static sbyte PeekSInt8(ref this SpanBuffer<byte> buf) => buf.Peek(1).GetSInt8();
        public static short PeekSInt16(ref this SpanBuffer<byte> buf) => buf.Peek(2).GetSInt16();
        public static int PeekSInt32(ref this SpanBuffer<byte> buf) => buf.Peek(4).GetSInt32();
        public static long PeekSInt64(ref this SpanBuffer<byte> buf) => buf.Peek(8).GetSInt64();



        public static ReadOnlySpanBuffer<T> AsReadOnlySpanBuffer<T>(this ReadOnlySpan<T> span) => new ReadOnlySpanBuffer<T>(span);
        public static ReadOnlySpanBuffer<T> AsReadOnlySpanBuffer<T>(this ReadOnlyMemory<T> memory) => new ReadOnlySpanBuffer<T>(memory.Span);
        public static ReadOnlySpanBuffer<T> AsReadOnlySpanBuffer<T>(this T[] data, int offset, int size) => new ReadOnlySpanBuffer<T>(data.AsReadOnlySpan(offset, size));

        public static bool ReadBool8(ref this ReadOnlySpanBuffer<byte> buf) => buf.Read(1).GetBool8();
        public static byte ReadUInt8(ref this ReadOnlySpanBuffer<byte> buf) => buf.Read(1).GetUInt8();
        public static ushort ReadUInt16(ref this ReadOnlySpanBuffer<byte> buf) => buf.Read(2).GetUInt16();
        public static uint ReadUInt32(ref this ReadOnlySpanBuffer<byte> buf) => buf.Read(4).GetUInt32();
        public static ulong ReadUInt64(ref this ReadOnlySpanBuffer<byte> buf) => buf.Read(8).GetUInt64();
        public static sbyte ReadSInt8(ref this ReadOnlySpanBuffer<byte> buf) => buf.Read(1).GetSInt8();
        public static short ReadSInt16(ref this ReadOnlySpanBuffer<byte> buf) => buf.Read(2).GetSInt16();
        public static int ReadSInt32(ref this ReadOnlySpanBuffer<byte> buf) => buf.Read(4).GetSInt32();
        public static long ReadSInt64(ref this ReadOnlySpanBuffer<byte> buf) => buf.Read(8).GetSInt64();

        public static bool PeekBool8(ref this ReadOnlySpanBuffer<byte> buf) => buf.Peek(1).GetBool8();
        public static byte PeekUInt8(ref this ReadOnlySpanBuffer<byte> buf) => buf.Peek(1).GetUInt8();
        public static ushort PeekUInt16(ref this ReadOnlySpanBuffer<byte> buf) => buf.Peek(2).GetUInt16();
        public static uint PeekUInt32(ref this ReadOnlySpanBuffer<byte> buf) => buf.Peek(4).GetUInt32();
        public static ulong PeekUInt64(ref this ReadOnlySpanBuffer<byte> buf) => buf.Peek(8).GetUInt64();
        public static sbyte PeekSInt8(ref this ReadOnlySpanBuffer<byte> buf) => buf.Peek(1).GetSInt8();
        public static short PeekSInt16(ref this ReadOnlySpanBuffer<byte> buf) => buf.Peek(2).GetSInt16();
        public static int PeekSInt32(ref this ReadOnlySpanBuffer<byte> buf) => buf.Peek(4).GetSInt32();
        public static long PeekSInt64(ref this ReadOnlySpanBuffer<byte> buf) => buf.Peek(8).GetSInt64();



        public static FastMemoryBuffer<T> AsFastMemoryBuffer<T>(this Memory<T> memory) => new FastMemoryBuffer<T>(memory);
        public static FastMemoryBuffer<T> AsFastMemoryBuffer<T>(this T[] data, int offset, int size) => new FastMemoryBuffer<T>(data.AsMemory(offset, size));

        public static void WriteBool8(this ref FastMemoryBuffer<byte> buf, bool value) => value.SetBool8(buf.Walk(1, false));
        public static void WriteUInt8(this ref FastMemoryBuffer<byte> buf, byte value) => value.SetUInt8(buf.Walk(1, false));
        public static void WriteUInt16(this ref FastMemoryBuffer<byte> buf, ushort value) => value.SetUInt16(buf.Walk(2, false));
        public static void WriteUInt32(this ref FastMemoryBuffer<byte> buf, uint value) => value.SetUInt32(buf.Walk(4, false));
        public static void WriteUInt64(this ref FastMemoryBuffer<byte> buf, ulong value) => value.SetUInt64(buf.Walk(8, false));
        public static void WriteSInt8(this ref FastMemoryBuffer<byte> buf, sbyte value) => value.SetSInt8(buf.Walk(1, false));
        public static void WriteSInt16(this ref FastMemoryBuffer<byte> buf, short value) => value.SetSInt16(buf.Walk(2, false));
        public static void WriteSInt32(this ref FastMemoryBuffer<byte> buf, int value) => value.SetSInt32(buf.Walk(4, false));
        public static void WriteSInt64(this ref FastMemoryBuffer<byte> buf, long value) => value.SetSInt64(buf.Walk(8, false));

        public static void SetBool8(this ref FastMemoryBuffer<byte> buf, bool value) => value.SetBool8(buf.Walk(1, true));
        public static void SetUInt8(this ref FastMemoryBuffer<byte> buf, byte value) => value.SetUInt8(buf.Walk(1, true));
        public static void SetUInt16(this ref FastMemoryBuffer<byte> buf, ushort value) => value.SetUInt16(buf.Walk(2, true));
        public static void SetUInt32(this ref FastMemoryBuffer<byte> buf, uint value) => value.SetUInt32(buf.Walk(4, true));
        public static void SetUInt64(this ref FastMemoryBuffer<byte> buf, ulong value) => value.SetUInt64(buf.Walk(8, true));
        public static void SetSInt8(this ref FastMemoryBuffer<byte> buf, sbyte value) => value.SetSInt8(buf.Walk(1, true));
        public static void SetSInt16(this ref FastMemoryBuffer<byte> buf, short value) => value.SetSInt16(buf.Walk(2, true));
        public static void SetSInt32(this ref FastMemoryBuffer<byte> buf, int value) => value.SetSInt32(buf.Walk(4, true));
        public static void SetSInt64(this ref FastMemoryBuffer<byte> buf, long value) => value.SetSInt64(buf.Walk(8, true));

        public static bool ReadBool8(ref this FastMemoryBuffer<byte> buf) => buf.Read(1).GetBool8();
        public static byte ReadUInt8(ref this FastMemoryBuffer<byte> buf) => buf.Read(1).GetUInt8();
        public static ushort ReadUInt16(ref this FastMemoryBuffer<byte> buf) => buf.Read(2).GetUInt16();
        public static uint ReadUInt32(ref this FastMemoryBuffer<byte> buf) => buf.Read(4).GetUInt32();
        public static ulong ReadUInt64(ref this FastMemoryBuffer<byte> buf) => buf.Read(8).GetUInt64();
        public static sbyte ReadSInt8(ref this FastMemoryBuffer<byte> buf) => buf.Read(1).GetSInt8();
        public static short ReadSInt16(ref this FastMemoryBuffer<byte> buf) => buf.Read(2).GetSInt16();
        public static int ReadSInt32(ref this FastMemoryBuffer<byte> buf) => buf.Read(4).GetSInt32();
        public static long ReadSInt64(ref this FastMemoryBuffer<byte> buf) => buf.Read(8).GetSInt64();

        public static bool PeekBool8(ref this FastMemoryBuffer<byte> buf) => buf.Peek(1).GetBool8();
        public static byte PeekUInt8(ref this FastMemoryBuffer<byte> buf) => buf.Peek(1).GetUInt8();
        public static ushort PeekUInt16(ref this FastMemoryBuffer<byte> buf) => buf.Peek(2).GetUInt16();
        public static uint PeekUInt32(ref this FastMemoryBuffer<byte> buf) => buf.Peek(4).GetUInt32();
        public static ulong PeekUInt64(ref this FastMemoryBuffer<byte> buf) => buf.Peek(8).GetUInt64();
        public static sbyte PeekSInt8(ref this FastMemoryBuffer<byte> buf) => buf.Peek(1).GetSInt8();
        public static short PeekSInt16(ref this FastMemoryBuffer<byte> buf) => buf.Peek(2).GetSInt16();
        public static int PeekSInt32(ref this FastMemoryBuffer<byte> buf) => buf.Peek(4).GetSInt32();
        public static long PeekSInt64(ref this FastMemoryBuffer<byte> buf) => buf.Peek(8).GetSInt64();

        public static FastReadOnlyMemoryBuffer<T> AsFastReadOnlyMemoryBuffer<T>(this ReadOnlyMemory<T> memory) => new FastReadOnlyMemoryBuffer<T>(memory);
        public static FastReadOnlyMemoryBuffer<T> AsFastReadOnlyMemoryBuffer<T>(this T[] data, int offset, int size) => new FastReadOnlyMemoryBuffer<T>(data.AsReadOnlyMemory(offset, size));

        public static bool ReadBool8(ref this FastReadOnlyMemoryBuffer<byte> buf) => buf.Read(1).GetBool8();
        public static byte ReadUInt8(ref this FastReadOnlyMemoryBuffer<byte> buf) => buf.Read(1).GetUInt8();
        public static ushort ReadUInt16(ref this FastReadOnlyMemoryBuffer<byte> buf) => buf.Read(2).GetUInt16();
        public static uint ReadUInt32(ref this FastReadOnlyMemoryBuffer<byte> buf) => buf.Read(4).GetUInt32();
        public static ulong ReadUInt64(ref this FastReadOnlyMemoryBuffer<byte> buf) => buf.Read(8).GetUInt64();
        public static sbyte ReadSInt8(ref this FastReadOnlyMemoryBuffer<byte> buf) => buf.Read(1).GetSInt8();
        public static short ReadSInt16(ref this FastReadOnlyMemoryBuffer<byte> buf) => buf.Read(2).GetSInt16();
        public static int ReadSInt32(ref this FastReadOnlyMemoryBuffer<byte> buf) => buf.Read(4).GetSInt32();
        public static long ReadSInt64(ref this FastReadOnlyMemoryBuffer<byte> buf) => buf.Read(8).GetSInt64();

        public static bool PeekBool8(ref this FastReadOnlyMemoryBuffer<byte> buf) => buf.Peek(1).GetBool8();
        public static byte PeekUInt8(ref this FastReadOnlyMemoryBuffer<byte> buf) => buf.Peek(1).GetUInt8();
        public static ushort PeekUInt16(ref this FastReadOnlyMemoryBuffer<byte> buf) => buf.Peek(2).GetUInt16();
        public static uint PeekUInt32(ref this FastReadOnlyMemoryBuffer<byte> buf) => buf.Peek(4).GetUInt32();
        public static ulong PeekUInt64(ref this FastReadOnlyMemoryBuffer<byte> buf) => buf.Peek(8).GetUInt64();
        public static sbyte PeekSInt8(ref this FastReadOnlyMemoryBuffer<byte> buf) => buf.Peek(1).GetSInt8();
        public static short PeekSInt16(ref this FastReadOnlyMemoryBuffer<byte> buf) => buf.Peek(2).GetSInt16();
        public static int PeekSInt32(ref this FastReadOnlyMemoryBuffer<byte> buf) => buf.Peek(4).GetSInt32();
        public static long PeekSInt64(ref this FastReadOnlyMemoryBuffer<byte> buf) => buf.Peek(8).GetSInt64();




        public static MemoryBuffer<T> AsMemoryBuffer<T>(this Memory<T> memory) => new MemoryBuffer<T>(memory);
        public static MemoryBuffer<T> AsMemoryBuffer<T>(this T[] data, int offset, int size) => new MemoryBuffer<T>(data.AsMemory(offset, size));

        public static void WriteBool8(this MemoryBuffer<byte> buf, bool value) => value.SetBool8(buf.Walk(1, false));
        public static void WriteUInt8(this MemoryBuffer<byte> buf, byte value) => value.SetUInt8(buf.Walk(1, false));
        public static void WriteUInt16(this MemoryBuffer<byte> buf, ushort value) => value.SetUInt16(buf.Walk(2, false));
        public static void WriteUInt32(this MemoryBuffer<byte> buf, uint value) => value.SetUInt32(buf.Walk(4, false));
        public static void WriteUInt64(this MemoryBuffer<byte> buf, ulong value) => value.SetUInt64(buf.Walk(8, false));
        public static void WriteSInt8(this MemoryBuffer<byte> buf, sbyte value) => value.SetSInt8(buf.Walk(1, false));
        public static void WriteSInt16(this MemoryBuffer<byte> buf, short value) => value.SetSInt16(buf.Walk(2, false));
        public static void WriteSInt32(this MemoryBuffer<byte> buf, int value) => value.SetSInt32(buf.Walk(4, false));
        public static void WriteSInt64(this MemoryBuffer<byte> buf, long value) => value.SetSInt64(buf.Walk(8, false));

        public static void SetBool8(this MemoryBuffer<byte> buf, bool value) => value.SetBool8(buf.Walk(1, true));
        public static void SetUInt8(this MemoryBuffer<byte> buf, byte value) => value.SetUInt8(buf.Walk(1, true));
        public static void SetUInt16(this MemoryBuffer<byte> buf, ushort value) => value.SetUInt16(buf.Walk(2, true));
        public static void SetUInt32(this MemoryBuffer<byte> buf, uint value) => value.SetUInt32(buf.Walk(4, true));
        public static void SetUInt64(this MemoryBuffer<byte> buf, ulong value) => value.SetUInt64(buf.Walk(8, true));
        public static void SetSInt8(this MemoryBuffer<byte> buf, sbyte value) => value.SetSInt8(buf.Walk(1, true));
        public static void SetSInt16(this MemoryBuffer<byte> buf, short value) => value.SetSInt16(buf.Walk(2, true));
        public static void SetSInt32(this MemoryBuffer<byte> buf, int value) => value.SetSInt32(buf.Walk(4, true));
        public static void SetSInt64(this MemoryBuffer<byte> buf, long value) => value.SetSInt64(buf.Walk(8, true));

        public static bool ReadBool8(this MemoryBuffer<byte> buf) => buf.Read(1).GetBool8();
        public static byte ReadUInt8(this MemoryBuffer<byte> buf) => buf.Read(1).GetUInt8();
        public static ushort ReadUInt16(this MemoryBuffer<byte> buf) => buf.Read(2).GetUInt16();
        public static uint ReadUInt32(this MemoryBuffer<byte> buf) => buf.Read(4).GetUInt32();
        public static ulong ReadUInt64(this MemoryBuffer<byte> buf) => buf.Read(8).GetUInt64();
        public static sbyte ReadSInt8(this MemoryBuffer<byte> buf) => buf.Read(1).GetSInt8();
        public static short ReadSInt16(this MemoryBuffer<byte> buf) => buf.Read(2).GetSInt16();
        public static int ReadSInt32(this MemoryBuffer<byte> buf) => buf.Read(4).GetSInt32();
        public static long ReadSInt64(this MemoryBuffer<byte> buf) => buf.Read(8).GetSInt64();

        public static bool PeekBool8(this MemoryBuffer<byte> buf) => buf.Peek(1).GetBool8();
        public static byte PeekUInt8(this MemoryBuffer<byte> buf) => buf.Peek(1).GetUInt8();
        public static ushort PeekUInt16(this MemoryBuffer<byte> buf) => buf.Peek(2).GetUInt16();
        public static uint PeekUInt32(this MemoryBuffer<byte> buf) => buf.Peek(4).GetUInt32();
        public static ulong PeekUInt64(this MemoryBuffer<byte> buf) => buf.Peek(8).GetUInt64();
        public static sbyte PeekSInt8(this MemoryBuffer<byte> buf) => buf.Peek(1).GetSInt8();
        public static short PeekSInt16(this MemoryBuffer<byte> buf) => buf.Peek(2).GetSInt16();
        public static int PeekSInt32(this MemoryBuffer<byte> buf) => buf.Peek(4).GetSInt32();
        public static long PeekSInt64(this MemoryBuffer<byte> buf) => buf.Peek(8).GetSInt64();

        public static ReadOnlyMemoryBuffer<T> AsReadOnlyMemoryBuffer<T>(this ReadOnlyMemory<T> memory) => new ReadOnlyMemoryBuffer<T>(memory);
        public static ReadOnlyMemoryBuffer<T> AsReadOnlyMemoryBuffer<T>(this T[] data, int offset, int size) => new ReadOnlyMemoryBuffer<T>(data.AsReadOnlyMemory(offset, size));

        public static bool ReadBool8(this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(1).GetBool8();
        public static byte ReadUInt8(this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(1).GetUInt8();
        public static ushort ReadUInt16(this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(2).GetUInt16();
        public static uint ReadUInt32(this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(4).GetUInt32();
        public static ulong ReadUInt64(this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(8).GetUInt64();
        public static sbyte ReadSInt8(this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(1).GetSInt8();
        public static short ReadSInt16(this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(2).GetSInt16();
        public static int ReadSInt32(this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(4).GetSInt32();
        public static long ReadSInt64(this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(8).GetSInt64();

        public static bool PeekBool8(this ReadOnlyMemoryBuffer<byte> buf) => buf.Peek(1).GetBool8();
        public static byte PeekUInt8(this ReadOnlyMemoryBuffer<byte> buf) => buf.Peek(1).GetUInt8();
        public static ushort PeekUInt16(this ReadOnlyMemoryBuffer<byte> buf) => buf.Peek(2).GetUInt16();
        public static uint PeekUInt32(this ReadOnlyMemoryBuffer<byte> buf) => buf.Peek(4).GetUInt32();
        public static ulong PeekUInt64(this ReadOnlyMemoryBuffer<byte> buf) => buf.Peek(8).GetUInt64();
        public static sbyte PeekSInt8(this ReadOnlyMemoryBuffer<byte> buf) => buf.Peek(1).GetSInt8();
        public static short PeekSInt16(this ReadOnlyMemoryBuffer<byte> buf) => buf.Peek(2).GetSInt16();
        public static int PeekSInt32(this ReadOnlyMemoryBuffer<byte> buf) => buf.Peek(4).GetSInt32();
        public static long PeekSInt64(this ReadOnlyMemoryBuffer<byte> buf) => buf.Peek(8).GetSInt64();
    }

    static class MemoryHelper
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




        #region AutoGenerated

        public static unsafe bool GetBool8(this byte[] data, int offset = 0)
        {
            return (data[offset] == 0) ? false : true;
        }

        public static unsafe byte GetUInt8(this byte[] data, int offset = 0)
        {
            return (byte)data[offset];
        }

        public static unsafe sbyte GetSInt8(this byte[] data, int offset = 0)
        {
            return (sbyte)data[offset];
        }

        public static unsafe ushort GetUInt16(this byte[] data, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(ushort)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ushort*)(ptr + offset))) : *((ushort*)(ptr + offset));
        }

        public static unsafe short GetSInt16(this byte[] data, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(short)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((short*)(ptr + offset))) : *((short*)(ptr + offset));
        }

        public static unsafe uint GetUInt32(this byte[] data, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(uint)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((uint*)(ptr + offset))) : *((uint*)(ptr + offset));
        }

        public static unsafe int GetSInt32(this byte[] data, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(int)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((int*)(ptr + offset))) : *((int*)(ptr + offset));
        }

        public static unsafe ulong GetUInt64(this byte[] data, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(ulong)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ulong*)(ptr + offset))) : *((ulong*)(ptr + offset));
        }

        public static unsafe long GetSInt64(this byte[] data, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(long)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((long*)(ptr + offset))) : *((long*)(ptr + offset));
        }

        public static unsafe bool GetBool8(this Span<byte> span)
        {
            return (span[0] == 0) ? false : true;
        }

        public static unsafe byte GetUInt8(this Span<byte> span)
        {
            return (byte)span[0];
        }

        public static unsafe sbyte GetSInt8(this Span<byte> span)
        {
            return (sbyte)span[0];
        }

        public static unsafe ushort GetUInt16(this Span<byte> span)
        {
            if (span.Length < sizeof(ushort)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ushort*)(ptr))) : *((ushort*)(ptr));
        }

        public static unsafe short GetSInt16(this Span<byte> span)
        {
            if (span.Length < sizeof(short)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((short*)(ptr))) : *((short*)(ptr));
        }

        public static unsafe uint GetUInt32(this Span<byte> span)
        {
            if (span.Length < sizeof(uint)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((uint*)(ptr))) : *((uint*)(ptr));
        }

        public static unsafe int GetSInt32(this Span<byte> span)
        {
            if (span.Length < sizeof(int)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((int*)(ptr))) : *((int*)(ptr));
        }

        public static unsafe ulong GetUInt64(this Span<byte> span)
        {
            if (span.Length < sizeof(ulong)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ulong*)(ptr))) : *((ulong*)(ptr));
        }

        public static unsafe long GetSInt64(this Span<byte> span)
        {
            if (span.Length < sizeof(long)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((long*)(ptr))) : *((long*)(ptr));
        }

        public static unsafe bool GetBool8(this ReadOnlySpan<byte> span)
        {
            return (span[0] == 0) ? false : true;
        }

        public static unsafe byte GetUInt8(this ReadOnlySpan<byte> span)
        {
            return (byte)span[0];
        }

        public static unsafe sbyte GetSInt8(this ReadOnlySpan<byte> span)
        {
            return (sbyte)span[0];
        }

        public static unsafe ushort GetUInt16(this ReadOnlySpan<byte> span)
        {
            if (span.Length < sizeof(ushort)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ushort*)(ptr))) : *((ushort*)(ptr));
        }

        public static unsafe short GetSInt16(this ReadOnlySpan<byte> span)
        {
            if (span.Length < sizeof(short)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((short*)(ptr))) : *((short*)(ptr));
        }

        public static unsafe uint GetUInt32(this ReadOnlySpan<byte> span)
        {
            if (span.Length < sizeof(uint)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((uint*)(ptr))) : *((uint*)(ptr));
        }

        public static unsafe int GetSInt32(this ReadOnlySpan<byte> span)
        {
            if (span.Length < sizeof(int)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((int*)(ptr))) : *((int*)(ptr));
        }

        public static unsafe ulong GetUInt64(this ReadOnlySpan<byte> span)
        {
            if (span.Length < sizeof(ulong)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ulong*)(ptr))) : *((ulong*)(ptr));
        }

        public static unsafe long GetSInt64(this ReadOnlySpan<byte> span)
        {
            if (span.Length < sizeof(long)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((long*)(ptr))) : *((long*)(ptr));
        }

        public static unsafe bool GetBool8(this Memory<byte> memory)
        {
            return (memory.Span[0] == 0) ? false : true;
        }

        public static unsafe byte GetUInt8(this Memory<byte> memory)
        {
            return (byte)memory.Span[0];
        }

        public static unsafe sbyte GetSInt8(this Memory<byte> memory)
        {
            return (sbyte)memory.Span[0];
        }

        public static unsafe ushort GetUInt16(this Memory<byte> memory)
        {
            if (memory.Length < sizeof(ushort)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ushort*)(ptr))) : *((ushort*)(ptr));
        }

        public static unsafe short GetSInt16(this Memory<byte> memory)
        {
            if (memory.Length < sizeof(short)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((short*)(ptr))) : *((short*)(ptr));
        }

        public static unsafe uint GetUInt32(this Memory<byte> memory)
        {
            if (memory.Length < sizeof(uint)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((uint*)(ptr))) : *((uint*)(ptr));
        }

        public static unsafe int GetSInt32(this Memory<byte> memory)
        {
            if (memory.Length < sizeof(int)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((int*)(ptr))) : *((int*)(ptr));
        }

        public static unsafe ulong GetUInt64(this Memory<byte> memory)
        {
            if (memory.Length < sizeof(ulong)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ulong*)(ptr))) : *((ulong*)(ptr));
        }

        public static unsafe long GetSInt64(this Memory<byte> memory)
        {
            if (memory.Length < sizeof(long)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((long*)(ptr))) : *((long*)(ptr));
        }

        public static unsafe bool GetBool8(this ReadOnlyMemory<byte> memory)
        {
            return (memory.Span[0] == 0) ? false : true;
        }

        public static unsafe byte GetUInt8(this ReadOnlyMemory<byte> memory)
        {
            return (byte)memory.Span[0];
        }

        public static unsafe sbyte GetSInt8(this ReadOnlyMemory<byte> memory)
        {
            return (sbyte)memory.Span[0];
        }

        public static unsafe ushort GetUInt16(this ReadOnlyMemory<byte> memory)
        {
            if (memory.Length < sizeof(ushort)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ushort*)(ptr))) : *((ushort*)(ptr));
        }

        public static unsafe short GetSInt16(this ReadOnlyMemory<byte> memory)
        {
            if (memory.Length < sizeof(short)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((short*)(ptr))) : *((short*)(ptr));
        }

        public static unsafe uint GetUInt32(this ReadOnlyMemory<byte> memory)
        {
            if (memory.Length < sizeof(uint)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((uint*)(ptr))) : *((uint*)(ptr));
        }

        public static unsafe int GetSInt32(this ReadOnlyMemory<byte> memory)
        {
            if (memory.Length < sizeof(int)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((int*)(ptr))) : *((int*)(ptr));
        }

        public static unsafe ulong GetUInt64(this ReadOnlyMemory<byte> memory)
        {
            if (memory.Length < sizeof(ulong)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((ulong*)(ptr))) : *((ulong*)(ptr));
        }

        public static unsafe long GetSInt64(this ReadOnlyMemory<byte> memory)
        {
            if (memory.Length < sizeof(long)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((long*)(ptr))) : *((long*)(ptr));
        }


        public static unsafe void SetBool8(this bool value, byte[] data, int offset = 0)
        {
            data[offset] = (byte)(value ? 1 : 0);
        }

        public static unsafe void SetBool8(this byte[] data, bool value, int offset = 0)
        {
            data[offset] = (byte)(value ? 1 : 0);
        }

        public static unsafe void SetUInt8(this byte value, byte[] data, int offset = 0)
        {
            data[offset] = (byte)value;
        }

        public static unsafe void SetUInt8(this byte[] data, byte value, int offset = 0)
        {
            data[offset] = (byte)value;
        }

        public static unsafe void SetSInt8(this sbyte value, byte[] data, int offset = 0)
        {
            data[offset] = (byte)value;
        }

        public static unsafe void SetSInt8(this byte[] data, sbyte value, int offset = 0)
        {
            data[offset] = (byte)value;
        }

        public static unsafe void SetUInt16(this ushort value, byte[] data, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(ushort)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((ushort*)(ptr + offset)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetUInt16(this byte[] data, ushort value, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(ushort)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((ushort*)(ptr + offset)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetSInt16(this short value, byte[] data, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(short)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((short*)(ptr + offset)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetSInt16(this byte[] data, short value, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(short)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((short*)(ptr + offset)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetUInt32(this uint value, byte[] data, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(uint)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((uint*)(ptr + offset)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetUInt32(this byte[] data, uint value, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(uint)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((uint*)(ptr + offset)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetSInt32(this int value, byte[] data, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(int)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((int*)(ptr + offset)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetSInt32(this byte[] data, int value, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(int)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((int*)(ptr + offset)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetUInt64(this ulong value, byte[] data, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(ulong)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((ulong*)(ptr + offset)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetUInt64(this byte[] data, ulong value, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(ulong)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((ulong*)(ptr + offset)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetSInt64(this long value, byte[] data, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(long)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((long*)(ptr + offset)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetSInt64(this byte[] data, long value, int offset = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException("offset < 0");
            if (checked(offset + sizeof(long)) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((long*)(ptr + offset)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetBool8(this bool value, Span<byte> span)
        {
            span[0] = (byte)(value ? 1 : 0);
        }

        public static unsafe void SetBool8(this Span<byte> span, bool value)
        {
            span[0] = (byte)(value ? 1 : 0);
        }

        public static unsafe void SetUInt8(this byte value, Span<byte> span)
        {
            span[0] = (byte)value;
        }

        public static unsafe void SetUInt8(this Span<byte> span, byte value)
        {
            span[0] = (byte)value;
        }

        public static unsafe void SetSInt8(this sbyte value, Span<byte> span)
        {
            span[0] = (byte)value;
        }

        public static unsafe void SetSInt8(this Span<byte> span, sbyte value)
        {
            span[0] = (byte)value;
        }

        public static unsafe void SetUInt16(this ushort value, Span<byte> span)
        {
            if (span.Length < sizeof(ushort)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((ushort*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetUInt16(this Span<byte> span, ushort value)
        {
            if (span.Length < sizeof(ushort)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((ushort*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetSInt16(this short value, Span<byte> span)
        {
            if (span.Length < sizeof(short)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((short*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetSInt16(this Span<byte> span, short value)
        {
            if (span.Length < sizeof(short)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((short*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetUInt32(this uint value, Span<byte> span)
        {
            if (span.Length < sizeof(uint)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((uint*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetUInt32(this Span<byte> span, uint value)
        {
            if (span.Length < sizeof(uint)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((uint*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetSInt32(this int value, Span<byte> span)
        {
            if (span.Length < sizeof(int)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((int*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetSInt32(this Span<byte> span, int value)
        {
            if (span.Length < sizeof(int)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((int*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetUInt64(this ulong value, Span<byte> span)
        {
            if (span.Length < sizeof(ulong)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((ulong*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetUInt64(this Span<byte> span, ulong value)
        {
            if (span.Length < sizeof(ulong)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((ulong*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetSInt64(this long value, Span<byte> span)
        {
            if (span.Length < sizeof(long)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((long*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetSInt64(this Span<byte> span, long value)
        {
            if (span.Length < sizeof(long)) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((long*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetBool8(this bool value, Memory<byte> memory)
        {
            memory.Span[0] = (byte)(value ? 1 : 0);
        }

        public static unsafe void SetBool8(this Memory<byte> memory, bool value)
        {
            memory.Span[0] = (byte)(value ? 1 : 0);
        }

        public static unsafe void SetUInt8(this byte value, Memory<byte> memory)
        {
            memory.Span[0] = (byte)value;
        }

        public static unsafe void SetUInt8(this Memory<byte> memory, byte value)
        {
            memory.Span[0] = (byte)value;
        }

        public static unsafe void SetSInt8(this sbyte value, Memory<byte> memory)
        {
            memory.Span[0] = (byte)value;
        }

        public static unsafe void SetSInt8(this Memory<byte> memory, sbyte value)
        {
            memory.Span[0] = (byte)value;
        }

        public static unsafe void SetUInt16(this ushort value, Memory<byte> memory)
        {
            if (memory.Length < sizeof(ushort)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((ushort*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetUInt16(this Memory<byte> memory, ushort value)
        {
            if (memory.Length < sizeof(ushort)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((ushort*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetSInt16(this short value, Memory<byte> memory)
        {
            if (memory.Length < sizeof(short)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((short*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetSInt16(this Memory<byte> memory, short value)
        {
            if (memory.Length < sizeof(short)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((short*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetUInt32(this uint value, Memory<byte> memory)
        {
            if (memory.Length < sizeof(uint)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((uint*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetUInt32(this Memory<byte> memory, uint value)
        {
            if (memory.Length < sizeof(uint)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((uint*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetSInt32(this int value, Memory<byte> memory)
        {
            if (memory.Length < sizeof(int)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((int*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetSInt32(this Memory<byte> memory, int value)
        {
            if (memory.Length < sizeof(int)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((int*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetUInt64(this ulong value, Memory<byte> memory)
        {
            if (memory.Length < sizeof(ulong)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((ulong*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetUInt64(this Memory<byte> memory, ulong value)
        {
            if (memory.Length < sizeof(ulong)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((ulong*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetSInt64(this long value, Memory<byte> memory)
        {
            if (memory.Length < sizeof(long)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((long*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void SetSInt64(this Memory<byte> memory, long value)
        {
            if (memory.Length < sizeof(long)) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((long*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }


        public static unsafe byte[] GetBool8(this bool value)
        {
            byte[] data = new byte[1];
            data[0] = (byte)(value ? 1 : 0);
            return data;
        }

        public static unsafe byte[] GetUInt8(this byte value)
        {
            byte[] data = new byte[1];
            data[0] = (byte)value;
            return data;
        }

        public static unsafe byte[] GetSInt8(this sbyte value)
        {
            byte[] data = new byte[1];
            data[0] = (byte)value;
            return data;
        }

        public static unsafe byte[] GetUInt16(this ushort value)
        {
            byte[] data = new byte[2];
            fixed (byte* ptr = data)
                *((ushort*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
            return data;
        }

        public static unsafe byte[] GetSInt16(this short value)
        {
            byte[] data = new byte[2];
            fixed (byte* ptr = data)
                *((short*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
            return data;
        }

        public static unsafe byte[] GetUInt32(this uint value)
        {
            byte[] data = new byte[4];
            fixed (byte* ptr = data)
                *((uint*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
            return data;
        }

        public static unsafe byte[] GetSInt32(this int value)
        {
            byte[] data = new byte[4];
            fixed (byte* ptr = data)
                *((int*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
            return data;
        }

        public static unsafe byte[] GetUInt64(this ulong value)
        {
            byte[] data = new byte[8];
            fixed (byte* ptr = data)
                *((ulong*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
            return data;
        }

        public static unsafe byte[] GetSInt64(this long value)
        {
            byte[] data = new byte[8];
            fixed (byte* ptr = data)
                *((long*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
            return data;
        }



        #endregion



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

        public static void WalkWriteBool8(ref this Span<byte> span, bool value) => value.SetBool8(span.Walk(1));
        public static void WalkWriteUInt8(ref this Span<byte> span, byte value) => value.SetUInt8(span.Walk(1));
        public static void WalkWriteUInt16(ref this Span<byte> span, ushort value) => value.SetUInt16(span.Walk(2));
        public static void WalkWriteUInt32(ref this Span<byte> span, uint value) => value.SetUInt32(span.Walk(4));
        public static void WalkWriteUInt64(ref this Span<byte> span, ulong value) => value.SetUInt64(span.Walk(8));
        public static void WalkWriteSInt8(ref this Span<byte> span, sbyte value) => value.SetSInt8(span.Walk(1));
        public static void WalkWriteSInt16(ref this Span<byte> span, short value) => value.SetSInt16(span.Walk(2));
        public static void WalkWriteSInt32(ref this Span<byte> span, int value) => value.SetSInt32(span.Walk(4));
        public static void WalkWriteSInt64(ref this Span<byte> span, long value) => value.SetSInt64(span.Walk(8));

        public static Span<T> WalkRead<T>(ref this Span<T> span, int size) => span.Walk(size);

        public static ReadOnlySpan<T> WalkRead<T>(ref this ReadOnlySpan<T> span, int size) => span.Walk(size);

        public static bool WalkReadBool8(ref this Span<byte> span) => span.WalkRead(1).GetBool8();
        public static byte WalkReadUInt8(ref this Span<byte> span) => span.WalkRead(1).GetUInt8();
        public static ushort WalkReadUInt16(ref this Span<byte> span) => span.WalkRead(2).GetUInt16();
        public static uint WalkReadUInt32(ref this Span<byte> span) => span.WalkRead(4).GetUInt32();
        public static ulong WalkReadUInt64(ref this Span<byte> span) => span.WalkRead(8).GetUInt64();
        public static sbyte WalkReadSInt8(ref this Span<byte> span) => span.WalkRead(1).GetSInt8();
        public static short WalkReadSInt16(ref this Span<byte> span) => span.WalkRead(2).GetSInt16();
        public static int WalkReadSInt32(ref this Span<byte> span) => span.WalkRead(4).GetSInt32();
        public static long WalkReadSInt64(ref this Span<byte> span) => span.WalkRead(8).GetSInt64();

        public static bool WalkReadBool8(ref this ReadOnlySpan<byte> span) => span.WalkRead(1).GetBool8();
        public static byte WalkReadUInt8(ref this ReadOnlySpan<byte> span) => span.WalkRead(1).GetUInt8();
        public static ushort WalkReadUInt16(ref this ReadOnlySpan<byte> span) => span.WalkRead(2).GetUInt16();
        public static uint WalkReadUInt32(ref this ReadOnlySpan<byte> span) => span.WalkRead(4).GetUInt32();
        public static ulong WalkReadUInt64(ref this ReadOnlySpan<byte> span) => span.WalkRead(8).GetUInt64();
        public static sbyte WalkReadSInt8(ref this ReadOnlySpan<byte> span) => span.WalkRead(1).GetSInt8();
        public static short WalkReadSInt16(ref this ReadOnlySpan<byte> span) => span.WalkRead(2).GetSInt16();
        public static int WalkReadSInt32(ref this ReadOnlySpan<byte> span) => span.WalkRead(4).GetSInt32();
        public static long WalkReadSInt64(ref this ReadOnlySpan<byte> span) => span.WalkRead(8).GetSInt64();


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
                new_len = (int)Math.Min(Math.Max((long)new_len, 128) * 2, int.MaxValue);
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

        public static void WalkWriteBool8(ref this Memory<byte> memory, bool value) => value.SetBool8(memory.Walk(1));
        public static void WalkWriteUInt8(ref this Memory<byte> memory, byte value) => value.SetUInt8(memory.Walk(1));
        public static void WalkWriteUInt16(ref this Memory<byte> memory, ushort value) => value.SetUInt16(memory.Walk(2));
        public static void WalkWriteUInt32(ref this Memory<byte> memory, uint value) => value.SetUInt32(memory.Walk(4));
        public static void WalkWriteUInt64(ref this Memory<byte> memory, ulong value) => value.SetUInt64(memory.Walk(8));
        public static void WalkWriteSInt8(ref this Memory<byte> memory, sbyte value) => value.SetSInt8(memory.Walk(1));
        public static void WalkWriteSInt16(ref this Memory<byte> memory, short value) => value.SetSInt16(memory.Walk(2));
        public static void WalkWriteSInt32(ref this Memory<byte> memory, int value) => value.SetSInt32(memory.Walk(4));
        public static void WalkWriteSInt64(ref this Memory<byte> memory, long value) => value.SetSInt64(memory.Walk(8));
        public static void WalkWrite<T>(ref this Memory<T> memory, Memory<T> data) => data.CopyTo(memory.Walk(data.Length));
        public static void WalkWrite<T>(ref this Memory<T> memory, Span<T> data) => data.CopyTo(memory.Walk(data.Length).Span);
        public static void WalkWrite<T>(ref this Memory<T> memory, T[] data) => data.CopyTo(memory.Walk(data.Length).Span);

        public static void WalkAutoDynamicWriteBool8(ref this Memory<byte> memory, bool value) => value.SetBool8(memory.WalkAutoDynamic(1));
        public static void WalkAutoDynamicWriteUInt8(ref this Memory<byte> memory, byte value) => value.SetUInt8(memory.WalkAutoDynamic(1));
        public static void WalkAutoDynamicWriteUInt16(ref this Memory<byte> memory, ushort value) => value.SetUInt16(memory.WalkAutoDynamic(2));
        public static void WalkAutoDynamicWriteUInt32(ref this Memory<byte> memory, uint value) => value.SetUInt32(memory.WalkAutoDynamic(4));
        public static void WalkAutoDynamicWriteUInt64(ref this Memory<byte> memory, ulong value) => value.SetUInt64(memory.WalkAutoDynamic(8));
        public static void WalkAutoDynamicWriteSInt8(ref this Memory<byte> memory, sbyte value) => value.SetSInt8(memory.WalkAutoDynamic(1));
        public static void WalkAutoDynamicWriteSInt16(ref this Memory<byte> memory, short value) => value.SetSInt16(memory.WalkAutoDynamic(2));
        public static void WalkAutoDynamicWriteSInt32(ref this Memory<byte> memory, int value) => value.SetSInt32(memory.WalkAutoDynamic(4));
        public static void WalkAutoDynamicWriteSInt64(ref this Memory<byte> memory, long value) => value.SetSInt64(memory.WalkAutoDynamic(8));
        public static void WalkAutoDynamicWrite<T>(ref this Memory<T> memory, Memory<T> data) => data.CopyTo(memory.WalkAutoDynamic(data.Length));
        public static void WalkAutoDynamicWrite<T>(ref this Memory<T> memory, Span<T> data) => data.CopyTo(memory.WalkAutoDynamic(data.Length).Span);
        public static void WalkAutoDynamicWrite<T>(ref this Memory<T> memory, T[] data) => data.CopyTo(memory.WalkAutoDynamic(data.Length).Span);

        public static void WalkAutoStaticWriteBool8(ref this Memory<byte> memory, bool value) => value.SetBool8(memory.WalkAutoStatic(1));
        public static void WalkAutoStaticWriteUInt8(ref this Memory<byte> memory, byte value) => value.SetUInt8(memory.WalkAutoStatic(1));
        public static void WalkAutoStaticWriteUInt16(ref this Memory<byte> memory, ushort value) => value.SetUInt16(memory.WalkAutoStatic(2));
        public static void WalkAutoStaticWriteUInt32(ref this Memory<byte> memory, uint value) => value.SetUInt32(memory.WalkAutoStatic(4));
        public static void WalkAutoStaticWriteUInt64(ref this Memory<byte> memory, ulong value) => value.SetUInt64(memory.WalkAutoStatic(8));
        public static void WalkAutoStaticWriteSInt8(ref this Memory<byte> memory, sbyte value) => value.SetSInt8(memory.WalkAutoStatic(1));
        public static void WalkAutoStaticWriteSInt16(ref this Memory<byte> memory, short value) => value.SetSInt16(memory.WalkAutoStatic(2));
        public static void WalkAutoStaticWriteSInt32(ref this Memory<byte> memory, int value) => value.SetSInt32(memory.WalkAutoStatic(4));
        public static void WalkAutoStaticWriteSInt64(ref this Memory<byte> memory, long value) => value.SetSInt64(memory.WalkAutoStatic(8));
        public static void WalkAutoStaticWrite<T>(ref this Memory<T> memory, Memory<T> data) => data.CopyTo(memory.WalkAutoStatic(data.Length));
        public static void WalkAutoStaticWrite<T>(ref this Memory<T> memory, Span<T> data) => data.CopyTo(memory.WalkAutoStatic(data.Length).Span);
        public static void WalkAutoStaticWrite<T>(ref this Memory<T> memory, T[] data) => data.CopyTo(memory.WalkAutoStatic(data.Length).Span);

        public static ReadOnlyMemory<T> WalkRead<T>(ref this ReadOnlyMemory<T> memory, int size) => memory.Walk(size);
        public static Memory<T> WalkRead<T>(ref this Memory<T> memory, int size) => memory.Walk(size);

        public static bool WalkReadBool8(ref this Memory<byte> memory) => memory.WalkRead(1).GetBool8();
        public static byte WalkReadUInt8(ref this Memory<byte> memory) => memory.WalkRead(1).GetUInt8();
        public static ushort WalkReadUInt16(ref this Memory<byte> memory) => memory.WalkRead(2).GetUInt16();
        public static uint WalkReadUInt32(ref this Memory<byte> memory) => memory.WalkRead(4).GetUInt32();
        public static ulong WalkReadUInt64(ref this Memory<byte> memory) => memory.WalkRead(8).GetUInt64();
        public static sbyte WalkReadSInt8(ref this Memory<byte> memory) => memory.WalkRead(1).GetSInt8();
        public static short WalkReadSInt16(ref this Memory<byte> memory) => memory.WalkRead(2).GetSInt16();
        public static int WalkReadSInt32(ref this Memory<byte> memory) => memory.WalkRead(4).GetSInt32();
        public static long WalkReadSInt64(ref this Memory<byte> memory) => memory.WalkRead(8).GetSInt64();

        public static bool WalkReadBool8(ref this ReadOnlyMemory<byte> memory) => memory.WalkRead(1).GetBool8();
        public static byte WalkReadUInt8(ref this ReadOnlyMemory<byte> memory) => memory.WalkRead(1).GetUInt8();
        public static ushort WalkReadUInt16(ref this ReadOnlyMemory<byte> memory) => memory.WalkRead(2).GetUInt16();
        public static uint WalkReadUInt32(ref this ReadOnlyMemory<byte> memory) => memory.WalkRead(4).GetUInt32();
        public static ulong WalkReadUInt64(ref this ReadOnlyMemory<byte> memory) => memory.WalkRead(8).GetUInt64();
        public static sbyte WalkReadSInt8(ref this ReadOnlyMemory<byte> memory) => memory.WalkRead(1).GetSInt8();
        public static short WalkReadSInt16(ref this ReadOnlyMemory<byte> memory) => memory.WalkRead(2).GetSInt16();
        public static int WalkReadSInt32(ref this ReadOnlyMemory<byte> memory) => memory.WalkRead(4).GetSInt32();
        public static long WalkReadSInt64(ref this ReadOnlyMemory<byte> memory) => memory.WalkRead(8).GetSInt64();


        static Action InternalFastThrowVitalException = new Action(() => { throw new ApplicationException("Vital Error"); });
        public static void FastThrowVitalError()
        {
            InternalFastThrowVitalException();
        }

        public static ArraySegment<T> AsSegmentSlow<T>(this Memory<T> memory)
        {
            if (MemoryMarshal.TryGetArray(memory, out ArraySegment<T> seg) == false)
            {
                FastThrowVitalError();
            }

            return seg;
        }

        public static ArraySegment<T> AsSegmentSlow<T>(this ReadOnlyMemory<T> memory)
        {
            if (MemoryMarshal.TryGetArray(memory, out ArraySegment<T> seg) == false)
            {
                FastThrowVitalError();
            }

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

        public const int MemoryUsePoolThreshold = 1024;

        public static T[] FastAlloc<T>(int minimum_size)
        {
            if (minimum_size < MemoryUsePoolThreshold)
                return new T[minimum_size];
            else
                return ArrayPool<T>.Shared.Rent(minimum_size);
        }

        public static Memory<T> FastAllocMemory<T>(int size)
        {
            if (size < MemoryUsePoolThreshold)
                return new T[size];
            else
                return new Memory<T>(FastAlloc<T>(size)).Slice(0, size);
        }

        public static void FastFree<T>(this T[] a)
        {
            if (a.Length >= MemoryUsePoolThreshold)
                ArrayPool<T>.Shared.Return(a);
        }

        public static void FastFree<T>(this Memory<T> memory) => memory.GetInternalArray().FastFree();


        static readonly long _memory_object_offset;
        static readonly long _memory_index_offset;
        static readonly long _memory_length_offset;
        static readonly bool _use_fast = false;

        static unsafe MemoryHelper()
        {
            try
            {
                _memory_object_offset = Marshal.OffsetOf<Memory<byte>>("_object").ToInt64();
                _memory_index_offset = Marshal.OffsetOf<Memory<byte>>("_index").ToInt64();
                _memory_length_offset = Marshal.OffsetOf<Memory<byte>>("_length").ToInt64();

                if (_memory_object_offset != Marshal.OffsetOf<ReadOnlyMemory<DummyValueType>>("_object").ToInt64() ||
                    _memory_index_offset != Marshal.OffsetOf<ReadOnlyMemory<DummyValueType>>("_index").ToInt64() ||
                    _memory_length_offset != Marshal.OffsetOf<ReadOnlyMemory<DummyValueType>>("_length").ToInt64())
                {
                    throw new Exception();
                }

                _use_fast = true;
            }
            catch
            {
                Random r = new Random();
                bool ok = true;

                for (int i = 0; i < 32; i++)
                {
                    int a = r.Next(96) + 32;
                    int b = r.Next(a / 2);
                    int c = r.Next(a / 2);
                    if (ValidateMemoryStructureLayoutForSecurity(a, b, c) == false ||
                        ValidateReadOnlyMemoryStructureLayoutForSecurity(a, b,c ) == false)
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok)
                {
                    _memory_object_offset = 0;
                    _memory_index_offset = sizeof(void*);
                    _memory_length_offset = _memory_index_offset + sizeof(int);

                    _use_fast = true;
                }
            }
        }

        unsafe struct DummyValueType
        {
            public fixed char fixedBuffer[96];
        }

        static unsafe bool ValidateMemoryStructureLayoutForSecurity(int a, int b, int c)
        {
            try
            {
                DummyValueType[] obj = new DummyValueType[a];
                Memory<DummyValueType> mem = new Memory<DummyValueType>(obj, b, c);

                void* mem_ptr = Unsafe.AsPointer(ref mem);

                byte* p = (byte*)mem_ptr;
                DummyValueType[] array = Unsafe.Read<DummyValueType[]>(p);
                if (array == obj)
                {
                    p += sizeof(void*);
                    if (Unsafe.Read<int>(p) == b)
                    {
                        p += sizeof(int);
                        if (Unsafe.Read<int>(p) == c)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        static unsafe bool ValidateReadOnlyMemoryStructureLayoutForSecurity(int a, int b, int c)
        {
            try
            {
                DummyValueType[] obj = new DummyValueType[a];
                ReadOnlyMemory<DummyValueType> mem = new ReadOnlyMemory<DummyValueType>(obj, b, c);

                void* mem_ptr = Unsafe.AsPointer(ref mem);

                byte* p = (byte*)mem_ptr;
                DummyValueType[] array = Unsafe.Read<DummyValueType[]>(p);
                if (array == obj)
                {
                    p += sizeof(void*);
                    if (Unsafe.Read<int>(p) == b)
                    {
                        p += sizeof(int);
                        if (Unsafe.Read<int>(p) == c)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }


        public static T[] GetInternalArray<T>(this Memory<T> memory)
        {
            unsafe
            {
                byte* ptr = (byte*)Unsafe.AsPointer(ref memory);
                ptr += _memory_object_offset;
                T[] o = Unsafe.Read<T[]>(ptr);
                return o;
            }
        }
        public static int GetInternalArrayLength<T>(this Memory<T> memory) => GetInternalArray(memory).Length;

        public static ArraySegment<T> AsSegment<T>(this Memory<T> memory)
        {
            if (_use_fast == false) return AsSegmentSlow(memory);

            unsafe
            {
                byte* ptr = (byte*)Unsafe.AsPointer(ref memory);
                return new ArraySegment<T>(
                    Unsafe.Read<T[]>(ptr + _memory_object_offset),
                    *((int*)(ptr + _memory_index_offset)),
                    *((int*)(ptr + _memory_length_offset))
                    );
            }
        }

        public static ArraySegment<T> AsSegment<T>(this ReadOnlyMemory<T> memory)
        {
            if (_use_fast == false) return AsSegmentSlow(memory);

            unsafe
            {
                byte* ptr = (byte*)Unsafe.AsPointer(ref memory);
                return new ArraySegment<T>(
                    Unsafe.Read<T[]>(ptr + _memory_object_offset),
                    *((int*)(ptr + _memory_index_offset)),
                    *((int*)(ptr + _memory_length_offset))
                    );
            }
        }
    }

    public sealed class FastMemoryAllocator<T>
    {
        Memory<T> Pool;
        int CurrentPos;
        int MinReserveSize;

        public FastMemoryAllocator(int initial_size = 0)
        {
            initial_size = Math.Min(initial_size, 1);
            Pool = new T[initial_size];
            MinReserveSize = initial_size;
        }

        public Memory<T> Reserve(int max_size)
        {
            checked
            {
                if (max_size < 0) throw new ArgumentOutOfRangeException("size");
                if (max_size == 0) return Memory<T>.Empty;

                Debug.Assert((Pool.Length - CurrentPos) >= 0);

                if ((Pool.Length - CurrentPos) < max_size)
                {
                    MinReserveSize = Math.Max(MinReserveSize, max_size * 5);
                    Pool = new T[MinReserveSize];
                    CurrentPos = 0;
                }

                var ret = Pool.Slice(CurrentPos, max_size);
                CurrentPos += max_size;
                return ret;
            }
        }

        public void Commit(ref Memory<T> reserved_memory, int commit_size)
        {
            reserved_memory = Commit(reserved_memory, commit_size);
        }

        public Memory<T> Commit(Memory<T> reserved_memory, int commit_size)
        {
            checked
            {
                int return_size = reserved_memory.Length - commit_size;
                Debug.Assert(return_size >= 0);
                if (return_size == 0) return reserved_memory;

                CurrentPos -= return_size;
                Debug.Assert(CurrentPos >= 0);

                if (commit_size >= 1)
                    return reserved_memory.Slice(0, commit_size);
                else
                    return Memory<T>.Empty;
            }
        }
    }

    public static class FastTick64
    {
        public static long Now { get => GetTick64() - Base; }
        static readonly long Base = GetTick64() - 1;

        static volatile uint state = 0;

        static long GetTick64()
        {
            uint value = (uint)Environment.TickCount;
            uint value_16bit = (value >> 16) & 0xFFFF;

            uint state_copy = state;

            uint state_16bit = (state_copy >> 16) & 0xFFFF;
            uint rotate_16bit = state_copy & 0xFFFF;

            if (value_16bit <= 0x1000 && state_16bit >= 0xF000)
            {
                rotate_16bit++;
            }

            uint state_new = (value_16bit << 16) & 0xFFFF0000 | rotate_16bit & 0x0000FFFF;

            state = state_new;

            return (long)value + 0x100000000L * (long)rotate_16bit;
        }
    }

    public static class FastHashHelper
    {
        public static int ComputeHash32(this string data, StringComparison cmp = StringComparison.Ordinal)
            => data.GetHashCode(cmp);

        public static int ComputeHash32(this ReadOnlySpan<byte> data)
            => Marvin.ComputeHash32(data);

        public static int ComputeHash32(this Span<byte> data)
            => Marvin.ComputeHash32(data);

        public static int ComputeHash32(this byte[] data, int offset, int size)
            => Marvin.ComputeHash32(data.AsReadOnlySpan(offset, size));

        public static int ComputeHash32(this byte[] data, int offset)
            => Marvin.ComputeHash32(data.AsReadOnlySpan(offset));

        public static int ComputeHash32(this byte[] data)
            => Marvin.ComputeHash32(data.AsReadOnlySpan());

        public static int ComputeHash32<TStruct>(this ref TStruct data) where TStruct : unmanaged
        {
            unsafe
            {
                void* ptr = Unsafe.AsPointer(ref data);
                Span<byte> span = new Span<byte>(ptr, sizeof(TStruct));
                return ComputeHash32(span);
            }
        }

        public static int ComputeHash32<TStruct>(this ReadOnlySpan<TStruct> data) where TStruct: unmanaged
        {
            var span = MemoryMarshal.Cast<TStruct, byte>(data);
            return ComputeHash32(span);
        }

        public static int ComputeHash32<TStruct>(this Span<TStruct> data) where TStruct : unmanaged
        {
            var span = MemoryMarshal.Cast<TStruct, byte>(data);
            return ComputeHash32(span);
        }

        public static int ComputeHash32<TStruct>(this TStruct[] data, int offset, int size) where TStruct : unmanaged
            => ComputeHash32(data.AsReadOnlySpan(offset, size));

        public static int ComputeHash32<TStruct>(this TStruct[] data, int offset) where TStruct : unmanaged
            => ComputeHash32(data.AsReadOnlySpan(offset));

        public static int ComputeHash32<TStruct>(this TStruct[] data) where TStruct : unmanaged
            => ComputeHash32(data.AsReadOnlySpan());


        #region AutoGenerated

        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Xor(int a) => (a);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Xor(int a, int b) => (a ^ b);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Xor(int a, int b, int c) => (a ^ b ^ c);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Xor(int a, int b, int c, int d) => (a ^ b ^ c ^ d);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Xor(int a, int b, int c, int d, int e) => (a ^ b ^ c ^ d ^ e);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Xor(int a, int b, int c, int d, int e, int f) => (a ^ b ^ c ^ d ^ e ^ f);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Xor(int a, int b, int c, int d, int e, int f, int g) => (a ^ b ^ c ^ d ^ e ^ f ^ g);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Xor(int a, int b, int c, int d, int e, int f, int g, int h) => (a ^ b ^ c ^ d ^ e ^ f ^ g ^ h);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Xor(int a, int b, int c, int d, int e, int f, int g, int h, int i) => (a ^ b ^ c ^ d ^ e ^ f ^ g ^ h ^ i);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Xor(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j) => (a ^ b ^ c ^ d ^ e ^ f ^ g ^ h ^ i ^ j);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Xor(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j, int k) => (a ^ b ^ c ^ d ^ e ^ f ^ g ^ h ^ i ^ j ^ k);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Xor(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j, int k, int l) => (a ^ b ^ c ^ d ^ e ^ f ^ g ^ h ^ i ^ j ^ k ^ l);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Xor(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j, int k, int l, int m) => (a ^ b ^ c ^ d ^ e ^ f ^ g ^ h ^ i ^ j ^ k ^ l ^ m);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Xor(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j, int k, int l, int m, int n) => (a ^ b ^ c ^ d ^ e ^ f ^ g ^ h ^ i ^ j ^ k ^ l ^ m ^ n);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Xor(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j, int k, int l, int m, int n, int o) => (a ^ b ^ c ^ d ^ e ^ f ^ g ^ h ^ i ^ j ^ k ^ l ^ m ^ n ^ o);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Xor(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j, int k, int l, int m, int n, int o, int p) => (a ^ b ^ c ^ d ^ e ^ f ^ g ^ h ^ i ^ j ^ k ^ l ^ m ^ n ^ o ^ p);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Xor(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j, int k, int l, int m, int n, int o, int p, int q) => (a ^ b ^ c ^ d ^ e ^ f ^ g ^ h ^ i ^ j ^ k ^ l ^ m ^ n ^ o ^ p ^ q);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Xor(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j, int k, int l, int m, int n, int o, int p, int q, int r) => (a ^ b ^ c ^ d ^ e ^ f ^ g ^ h ^ i ^ j ^ k ^ l ^ m ^ n ^ o ^ p ^ q ^ r);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Xor(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j, int k, int l, int m, int n, int o, int p, int q, int r, int s) => (a ^ b ^ c ^ d ^ e ^ f ^ g ^ h ^ i ^ j ^ k ^ l ^ m ^ n ^ o ^ p ^ q ^ r ^ s);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Xor(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j, int k, int l, int m, int n, int o, int p, int q, int r, int s, int t) => (a ^ b ^ c ^ d ^ e ^ f ^ g ^ h ^ i ^ j ^ k ^ l ^ m ^ n ^ o ^ p ^ q ^ r ^ s ^ t);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Xor(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j, int k, int l, int m, int n, int o, int p, int q, int r, int s, int t, int u) => (a ^ b ^ c ^ d ^ e ^ f ^ g ^ h ^ i ^ j ^ k ^ l ^ m ^ n ^ o ^ p ^ q ^ r ^ s ^ t ^ u);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Xor(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j, int k, int l, int m, int n, int o, int p, int q, int r, int s, int t, int u, int v) => (a ^ b ^ c ^ d ^ e ^ f ^ g ^ h ^ i ^ j ^ k ^ l ^ m ^ n ^ o ^ p ^ q ^ r ^ s ^ t ^ u ^ v);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Xor(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j, int k, int l, int m, int n, int o, int p, int q, int r, int s, int t, int u, int v, int w) => (a ^ b ^ c ^ d ^ e ^ f ^ g ^ h ^ i ^ j ^ k ^ l ^ m ^ n ^ o ^ p ^ q ^ r ^ s ^ t ^ u ^ v ^ w);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Xor(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j, int k, int l, int m, int n, int o, int p, int q, int r, int s, int t, int u, int v, int w, int x) => (a ^ b ^ c ^ d ^ e ^ f ^ g ^ h ^ i ^ j ^ k ^ l ^ m ^ n ^ o ^ p ^ q ^ r ^ s ^ t ^ u ^ v ^ w ^ x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Xor(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j, int k, int l, int m, int n, int o, int p, int q, int r, int s, int t, int u, int v, int w, int x, int y) => (a ^ b ^ c ^ d ^ e ^ f ^ g ^ h ^ i ^ j ^ k ^ l ^ m ^ n ^ o ^ p ^ q ^ r ^ s ^ t ^ u ^ v ^ w ^ x ^ y);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Xor(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j, int k, int l, int m, int n, int o, int p, int q, int r, int s, int t, int u, int v, int w, int x, int y, int z) => (a ^ b ^ c ^ d ^ e ^ f ^ g ^ h ^ i ^ j ^ k ^ l ^ m ^ n ^ o ^ p ^ q ^ r ^ s ^ t ^ u ^ v ^ w ^ x ^ y ^ z);

        #endregion

        public static int Xor(params int[] hash_list)
        {
            int ret = 0;
            foreach (var i in hash_list)
                ret ^= i;
            return ret;
        }

        #region Marvin
        public static class Marvin
        {
            // From: https://github.com/dotnet/corefx/blob/master/src/Common/src/System/Marvin.cs
            /* The MIT License (MIT)
             * Copyright (c) .NET Foundation and Contributors
             * All rights reserved.
             * Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal
             * in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
             * copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
             * The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
             * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
             * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
             * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
             * SOFTWARE. */

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int ComputeHash32(ReadOnlySpan<byte> data, ulong seed)
            {
                long hash64 = ComputeHash(data, seed);
                return ((int)(hash64 >> 32)) ^ (int)hash64;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int ComputeHash32(ReadOnlySpan<byte> data) => ComputeHash32(data, DefaultSeed);

            public static long ComputeHash(ReadOnlySpan<byte> data, ulong seed)
            {
                uint p0 = (uint)seed;
                uint p1 = (uint)(seed >> 32);

                if (data.Length >= sizeof(uint))
                {
                    ReadOnlySpan<uint> uData = MemoryMarshal.Cast<byte, uint>(data);

                    for (int i = 0; i < uData.Length; i++)
                    {
                        p0 += uData[i];
                        Block(ref p0, ref p1);
                    }

                    int byteOffset = data.Length & (~3);
                    data = data.Slice(byteOffset);
                }

                switch (data.Length)
                {
                    case 0:
                        p0 += 0x80u;
                        break;

                    case 1:
                        p0 += 0x8000u | data[0];
                        break;

                    case 2:
                        p0 += 0x800000u | MemoryMarshal.Cast<byte, ushort>(data)[0];
                        break;

                    case 3:
                        p0 += 0x80000000u | (((uint)data[2]) << 16) | (uint)(MemoryMarshal.Cast<byte, ushort>(data)[0]);
                        break;
                }

                Block(ref p0, ref p1);
                Block(ref p0, ref p1);

                return (((long)p1) << 32) | p0;
            }

            public static long ComputeHash(ReadOnlySpan<byte> data) => ComputeHash(data, DefaultSeed);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void Block(ref uint rp0, ref uint rp1)
            {
                uint p0 = rp0;
                uint p1 = rp1;

                p1 ^= p0;
                p0 = _rotl(p0, 20);

                p0 += p1;
                p1 = _rotl(p1, 9);

                p1 ^= p0;
                p0 = _rotl(p0, 27);

                p0 += p1;
                p1 = _rotl(p1, 19);

                rp0 = p0;
                rp1 = p1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static uint _rotl(uint value, int shift)
            {
                return (value << shift) | (value >> (32 - shift));
            }

            public static ulong DefaultSeed { get; } = 0;

            private static ulong GenerateSeed()
            {
                using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                {
                    var bytes = new byte[sizeof(ulong)];
                    rng.GetBytes(bytes);
                    return BitConverter.ToUInt64(bytes, 0);
                }
            }
        }
        #endregion

    }



    #endregion
}

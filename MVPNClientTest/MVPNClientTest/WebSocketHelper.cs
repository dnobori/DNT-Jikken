using System;
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

#pragma warning disable CS0162

namespace SoftEther.WebSocket.Helper
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

    public ref struct MemoryBuffer<T>
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

        public MemoryBuffer(Memory<T> base_memory)
        {
            InternalBuffer = base_memory;
            CurrentPosition = 0;
            Length = base_memory.Length;
            InternalSpan = InternalBuffer.Span;
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
            MemoryBuffer<T> ret = new MemoryBuffer<T>(InternalSpan.ToArray());
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

    public ref struct ReadOnlyMemoryBuffer<T>
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

        public ReadOnlyMemoryBuffer(ReadOnlyMemory<T> base_memory)
        {
            InternalBuffer = base_memory;
            CurrentPosition = 0;
            Length = base_memory.Length;
            InternalSpan = InternalBuffer.Span;
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
            ReadOnlyMemoryBuffer<T> ret = new ReadOnlyMemoryBuffer<T>(InternalSpan.ToArray());
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



        public static MemoryBuffer<T> AsMemoryBuffer<T>(this Memory<T> memory) => new MemoryBuffer<T>(memory);
        public static MemoryBuffer<T> AsMemoryBuffer<T>(this T[] data, int offset, int size) => new MemoryBuffer<T>(data.AsMemory(offset, size));

        public static void WriteBool8(this ref MemoryBuffer<byte> buf, bool value) => value.SetBool8(buf.Walk(1, false));
        public static void WriteUInt8(this ref MemoryBuffer<byte> buf, byte value) => value.SetUInt8(buf.Walk(1, false));
        public static void WriteUInt16(this ref MemoryBuffer<byte> buf, ushort value) => value.SetUInt16(buf.Walk(2, false));
        public static void WriteUInt32(this ref MemoryBuffer<byte> buf, uint value) => value.SetUInt32(buf.Walk(4, false));
        public static void WriteUInt64(this ref MemoryBuffer<byte> buf, ulong value) => value.SetUInt64(buf.Walk(8, false));
        public static void WriteSInt8(this ref MemoryBuffer<byte> buf, sbyte value) => value.SetSInt8(buf.Walk(1, false));
        public static void WriteSInt16(this ref MemoryBuffer<byte> buf, short value) => value.SetSInt16(buf.Walk(2, false));
        public static void WriteSInt32(this ref MemoryBuffer<byte> buf, int value) => value.SetSInt32(buf.Walk(4, false));
        public static void WriteSInt64(this ref MemoryBuffer<byte> buf, long value) => value.SetSInt64(buf.Walk(8, false));

        public static void SetBool8(this ref MemoryBuffer<byte> buf, bool value) => value.SetBool8(buf.Walk(1, true));
        public static void SetUInt8(this ref MemoryBuffer<byte> buf, byte value) => value.SetUInt8(buf.Walk(1, true));
        public static void SetUInt16(this ref MemoryBuffer<byte> buf, ushort value) => value.SetUInt16(buf.Walk(2, true));
        public static void SetUInt32(this ref MemoryBuffer<byte> buf, uint value) => value.SetUInt32(buf.Walk(4, true));
        public static void SetUInt64(this ref MemoryBuffer<byte> buf, ulong value) => value.SetUInt64(buf.Walk(8, true));
        public static void SetSInt8(this ref MemoryBuffer<byte> buf, sbyte value) => value.SetSInt8(buf.Walk(1, true));
        public static void SetSInt16(this ref MemoryBuffer<byte> buf, short value) => value.SetSInt16(buf.Walk(2, true));
        public static void SetSInt32(this ref MemoryBuffer<byte> buf, int value) => value.SetSInt32(buf.Walk(4, true));
        public static void SetSInt64(this ref MemoryBuffer<byte> buf, long value) => value.SetSInt64(buf.Walk(8, true));

        public static bool ReadBool8(ref this MemoryBuffer<byte> buf) => buf.Read(1).GetBool8();
        public static byte ReadUInt8(ref this MemoryBuffer<byte> buf) => buf.Read(1).GetUInt8();
        public static ushort ReadUInt16(ref this MemoryBuffer<byte> buf) => buf.Read(2).GetUInt16();
        public static uint ReadUInt32(ref this MemoryBuffer<byte> buf) => buf.Read(4).GetUInt32();
        public static ulong ReadUInt64(ref this MemoryBuffer<byte> buf) => buf.Read(8).GetUInt64();
        public static sbyte ReadSInt8(ref this MemoryBuffer<byte> buf) => buf.Read(1).GetSInt8();
        public static short ReadSInt16(ref this MemoryBuffer<byte> buf) => buf.Read(2).GetSInt16();
        public static int ReadSInt32(ref this MemoryBuffer<byte> buf) => buf.Read(4).GetSInt32();
        public static long ReadSInt64(ref this MemoryBuffer<byte> buf) => buf.Read(8).GetSInt64();

        public static bool PeekBool8(ref this MemoryBuffer<byte> buf) => buf.Peek(1).GetBool8();
        public static byte PeekUInt8(ref this MemoryBuffer<byte> buf) => buf.Peek(1).GetUInt8();
        public static ushort PeekUInt16(ref this MemoryBuffer<byte> buf) => buf.Peek(2).GetUInt16();
        public static uint PeekUInt32(ref this MemoryBuffer<byte> buf) => buf.Peek(4).GetUInt32();
        public static ulong PeekUInt64(ref this MemoryBuffer<byte> buf) => buf.Peek(8).GetUInt64();
        public static sbyte PeekSInt8(ref this MemoryBuffer<byte> buf) => buf.Peek(1).GetSInt8();
        public static short PeekSInt16(ref this MemoryBuffer<byte> buf) => buf.Peek(2).GetSInt16();
        public static int PeekSInt32(ref this MemoryBuffer<byte> buf) => buf.Peek(4).GetSInt32();
        public static long PeekSInt64(ref this MemoryBuffer<byte> buf) => buf.Peek(8).GetSInt64();

        public static ReadOnlyMemoryBuffer<T> AsReadOnlyMemoryBuffer<T>(this ReadOnlyMemory<T> memory) => new ReadOnlyMemoryBuffer<T>(memory);
        public static ReadOnlyMemoryBuffer<T> AsReadOnlyMemoryBuffer<T>(this T[] data, int offset, int size) => new ReadOnlyMemoryBuffer<T>(data.AsReadOnlyMemory(offset, size));

        public static bool ReadBool8(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(1).GetBool8();
        public static byte ReadUInt8(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(1).GetUInt8();
        public static ushort ReadUInt16(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(2).GetUInt16();
        public static uint ReadUInt32(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(4).GetUInt32();
        public static ulong ReadUInt64(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(8).GetUInt64();
        public static sbyte ReadSInt8(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(1).GetSInt8();
        public static short ReadSInt16(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(2).GetSInt16();
        public static int ReadSInt32(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(4).GetSInt32();
        public static long ReadSInt64(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(8).GetSInt64();

        public static bool PeekBool8(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Peek(1).GetBool8();
        public static byte PeekUInt8(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Peek(1).GetUInt8();
        public static ushort PeekUInt16(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Peek(2).GetUInt16();
        public static uint PeekUInt32(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Peek(4).GetUInt32();
        public static ulong PeekUInt64(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Peek(8).GetUInt64();
        public static sbyte PeekSInt8(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Peek(1).GetSInt8();
        public static short PeekSInt16(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Peek(2).GetSInt16();
        public static int PeekSInt32(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Peek(4).GetSInt32();
        public static long PeekSInt64(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Peek(8).GetSInt64();
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
                        ValidateReadOnlyMemoryStructureLayoutForSecurity(a, b, c) == false)
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
        public static long Now { get => GetTick64(); }

        static volatile uint state = 0;

        static uint GetTickCount()
        {
            uint ret = (uint)Environment.TickCount;
            if (ret == 0) ret = 1;
            return ret;
        }

        static long GetTick64()
        {
            uint value = GetTickCount();
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

        public static int ComputeHash32<TStruct>(this ReadOnlySpan<TStruct> data) where TStruct : unmanaged
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


    // ---------------

    public class RefInt
    {
        public RefInt() : this(0) { }
        public RefInt(int value)
        {
            this.Value = value;
        }
        public int Value;
        public void Set(int value) => this.Value = value;
        public int Get() => this.Value;
        public override string ToString() => this.Value.ToString();
        public int Increment() => Interlocked.Increment(ref this.Value);
        public int Decrement() => Interlocked.Decrement(ref this.Value);

        public override bool Equals(object obj) => obj is RefInt x && this.Value == x.Value;
        public override int GetHashCode() => HashCode.Combine(Value);
        public static bool operator ==(RefInt left, int right) => left.Value == right;
        public static bool operator !=(RefInt left, int right) => left.Value != right;
        public static implicit operator int(RefInt r) => r.Value;
        public static implicit operator RefInt(int value) => new RefInt(value);
    }

    public class RefBool
    {
        public RefBool() : this(false) { }
        public RefBool(bool value)
        {
            this.Value = value;
        }
        public bool Value;
        public void Set(bool value) => this.Value = value;
        public bool Get() => this.Value;
        public override string ToString() => this.Value.ToString();

        public override bool Equals(object obj) => obj is RefBool x && this.Value == x.Value;
        public override int GetHashCode() => HashCode.Combine(Value);
        public static bool operator ==(RefBool left, bool right) => left.Value == right;
        public static bool operator !=(RefBool left, bool right) => left.Value != right;
        public static implicit operator bool(RefBool r) => r.Value;
        public static implicit operator RefBool(bool value) => new RefBool(value);
    }

    public class Ref<T>
    {
        public Ref() : this(default(T)) { }
        public Ref(T value)
        {
            Value = value;
        }

        public T Value { get; set; }
        public void Set(T value) => this.Value = value;
        public T Get() => this.Value;
        public bool IsTrue()
        {
            switch (this.Value)
            {
                case bool b:
                    return b;
                case int i:
                    return (i != 0);
                case string s:
                    return bool.TryParse(s, out bool ret2) ? ret2 : false;
            }
            return bool.TryParse(this.Value.ToString(), out bool ret) ? ret : false;
        }
        public override string ToString() => Value?.ToString() ?? null;

        public override bool Equals(object obj)
        {
            var @ref = obj as Ref<T>;
            return @ref != null &&
                   EqualityComparer<T>.Default.Equals(Value, @ref.Value);
        }

        public override int GetHashCode()
        {
            return -1937169414 + EqualityComparer<T>.Default.GetHashCode(Value);
        }

        public static bool operator true(Ref<T> r) { return r.IsTrue(); }
        public static bool operator false(Ref<T> r) { return !r.IsTrue(); }
        public static bool operator ==(Ref<T> r, bool b) { return r.IsTrue() == b; }
        public static bool operator !=(Ref<T> r, bool b) { return r.IsTrue() != b; }
        public static bool operator !(Ref<T> r) { return !r.IsTrue(); }
    }

    // Buffer
    public class Buf
    {
        MemoryStream buf;

        public Buf()
        {
            init(new byte[0]);
        }
        public Buf(byte[] data)
        {
            init(data);
        }
        void init(byte[] data)
        {
            buf = new MemoryStream();
            Write(data);
            SeekToBegin();
        }

        public void Clear()
        {
            buf.SetLength(0);
        }

        public void WriteByte(byte data)
        {
            buf.WriteByte(data);
        }
        public void Write(byte[] data)
        {
            buf.Write(data, 0, data.Length);
        }
        public void Write(byte[] data, int pos, int len)
        {
            buf.Write(data, pos, len);
        }

        public uint Size
        {
            get
            {
                return (uint)buf.Length;
            }
        }

        public uint Pos
        {
            get
            {
                return (uint)buf.Position;
            }
        }

        public byte[] ByteData
        {
            get
            {
                return buf.ToArray();
            }
        }

        public byte this[uint i]
        {
            get
            {
                return buf.GetBuffer()[i];
            }

            set
            {
                buf.GetBuffer()[i] = value;
            }
        }

        public byte[] Read()
        {
            return Read(this.Size);
        }
        public byte[] Read(uint size)
        {
            byte[] tmp = new byte[size];
            int i = buf.Read(tmp, 0, (int)size);

            byte[] ret = new byte[i];
            Array.Copy(tmp, 0, ret, 0, i);

            return ret;
        }
        public byte ReadByte()
        {
            byte[] a = Read(1);

            return a[0];
        }

        public void SeekToBegin()
        {
            Seek(0);
        }
        public void SeekToEnd()
        {
            Seek(0, SeekOrigin.End);
        }
        public void Seek(uint offset)
        {
            Seek(offset, SeekOrigin.Begin);
        }
        public void Seek(uint offset, SeekOrigin mode)
        {
            buf.Seek(offset, mode);
        }

        public ushort ReadShort()
        {
            byte[] data = Read(2);
            if (data.Length != 2)
            {
                return 0;
            }
            if (WebSocketHelper.IsLittleEndian)
            {
                Array.Reverse(data);
            }
            return BitConverter.ToUInt16(data, 0);
        }
        public ushort RawReadShort()
        {
            byte[] data = Read(2);
            if (data.Length != 2)
            {
                return 0;
            }
            return BitConverter.ToUInt16(data, 0);
        }

        public uint ReadInt()
        {
            byte[] data = Read(4);
            if (data.Length != 4)
            {
                return 0;
            }
            if (WebSocketHelper.IsLittleEndian)
            {
                Array.Reverse(data);
            }
            return BitConverter.ToUInt32(data, 0);
        }
        public uint RawReadInt()
        {
            byte[] data = Read(4);
            if (data.Length != 4)
            {
                return 0;
            }
            return BitConverter.ToUInt32(data, 0);
        }

        public ulong ReadInt64()
        {
            byte[] data = Read(8);
            if (data.Length != 8)
            {
                return 0;
            }
            if (WebSocketHelper.IsLittleEndian)
            {
                Array.Reverse(data);
            }
            return BitConverter.ToUInt64(data, 0);
        }
        public ulong RawReadInt64()
        {
            byte[] data = Read(8);
            if (data.Length != 8)
            {
                return 0;
            }
            return BitConverter.ToUInt64(data, 0);
        }

        public void WriteShort(ushort shortValue)
        {
            byte[] data = BitConverter.GetBytes(shortValue);
            if (WebSocketHelper.IsLittleEndian)
            {
                Array.Reverse(data);
            }
            Write(data);
        }
        public void RawWriteShort(ushort shortValue)
        {
            byte[] data = BitConverter.GetBytes(shortValue);
            Write(data);
        }

        public void WriteInt(uint intValue)
        {
            byte[] data = BitConverter.GetBytes(intValue);
            if (WebSocketHelper.IsLittleEndian)
            {
                Array.Reverse(data);
            }
            Write(data);
        }
        public void RawWriteInt(uint intValue)
        {
            byte[] data = BitConverter.GetBytes(intValue);
            Write(data);
        }

        public void WriteInt64(ulong int64Value)
        {
            byte[] data = BitConverter.GetBytes(int64Value);
            if (WebSocketHelper.IsLittleEndian)
            {
                Array.Reverse(data);
            }
            Write(data);
        }
        public void RawWriteInt64(ulong int64Value)
        {
            byte[] data = BitConverter.GetBytes(int64Value);
            Write(data);
        }
        public static Buf ReadFromStream(Stream st)
        {
            Buf ret = new Buf();
            int size = 32767;

            while (true)
            {
                byte[] tmp = new byte[size];
                int i = st.Read(tmp, 0, tmp.Length);

                if (i <= 0)
                {
                    break;
                }

                Array.Resize<byte>(ref tmp, i);

                ret.Write(tmp);
            }

            ret.SeekToBegin();

            return ret;
        }
    }

    // FIFO
    public class Fifo
    {
        byte[] p;
        int pos, size;
        public int Size
        {
            get { return size; }
        }
        public byte[] Data
        {
            get
            {
                return this.p;
            }
        }
        public int DataOffset
        {
            get
            {
                return this.pos;
            }
        }
        public int PhysicalSize
        {
            get
            {
                return p.Length;
            }
        }

        int reallocMemSize;
        public const int FifoInitMemSize = 4096;
        public const int FifoReallocMemSize = 65536;
        public const int FifoReallocMemSizeSmall = 65536;

        long totalWriteSize = 0, totalReadSize = 0;

        public long TotalReadSize
        {
            get { return totalReadSize; }
        }
        public long TotalWriteSize
        {
            get { return totalWriteSize; }
        }

        public Fifo()
        {
            init(0);
        }
        public Fifo(int reallocMemSize)
        {
            init(reallocMemSize);
        }

        void init(int reallocMemSize)
        {
            if (reallocMemSize == 0)
            {
                reallocMemSize = FifoReallocMemSize;
            }

            this.size = this.pos = 0;
            this.reallocMemSize = reallocMemSize;

            this.p = new byte[FifoInitMemSize];
        }

        public void Write(Buf buf)
        {
            Write(buf.ByteData);
        }
        public void Write(byte[] src)
        {
            Write(src, src.Length);
        }
        public void SkipWrite(int size)
        {
            Write(null, size);
        }
        public void Write(byte[] src, int size)
        {
            Write(src, 0, size);
        }
        public void Write(byte[] src, int offset, int size)
        {
            checked
            {
                int i, need_size;
                bool realloc_flag;

                i = this.size;
                this.size += size;
                need_size = this.pos + this.size;
                realloc_flag = false;

                int memsize = p.Length;
                while (need_size > memsize)
                {
                    memsize = Math.Max(memsize, FifoInitMemSize) * 3;
                    realloc_flag = true;
                }

                if (realloc_flag)
                {
                    byte[] new_p = new byte[memsize];
                    WebSocketHelper.CopyByte(new_p, 0, this.p, 0, this.p.Length);
                    this.p = new_p;
                }

                if (src != null)
                {
                    WebSocketHelper.CopyByte(this.p, this.pos + i, src, offset, size);
                }

                totalWriteSize += size;
            }
        }

        public byte[] Read()
        {
            return Read(this.Size);
        }
        public void ReadToBuf(Buf buf, int size)
        {
            byte[] data = Read(size);

            buf.Write(data);
        }
        public Buf ReadToBuf(int size)
        {
            byte[] data = Read(size);

            return new Buf(data);
        }
        public byte[] Read(int size)
        {
            byte[] ret = new byte[size];
            int read_size = Read(ret);
            Array.Resize<byte>(ref ret, read_size);

            return ret;
        }
        public int Read(byte[] dst)
        {
            return Read(dst, dst.Length);
        }
        public int SkipRead(int size)
        {
            return Read(null, size);
        }
        public int Read(byte[] dst, int size)
        {
            return Read(dst, 0, size);
        }
        public int Read(byte[] dst, int offset, int size)
        {
            checked
            {
                int read_size;

                read_size = Math.Min(size, this.size);
                if (read_size == 0)
                {
                    return 0;
                }
                if (dst != null)
                {
                    WebSocketHelper.CopyByte(dst, offset, this.p, this.pos, read_size);
                }
                this.pos += read_size;
                this.size -= read_size;

                if (this.size == 0)
                {
                    this.pos = 0;
                }

                if (this.pos >= FifoInitMemSize &&
                    this.p.Length >= this.reallocMemSize &&
                    (this.p.Length / 2) > this.size)
                {
                    byte[] new_p;
                    int new_size;

                    new_size = Math.Max(this.p.Length / 2, FifoInitMemSize);
                    new_p = new byte[new_size];
                    WebSocketHelper.CopyByte(new_p, 0, this.p, this.pos, this.size);

                    this.p = new_p;

                    this.pos = 0;
                }

                totalReadSize += read_size;

                return read_size;
            }
        }

        public Span<byte> Span
        {
            get
            {
                return this.Data.AsSpan(this.pos, this.size);
            }
        }
    }

    public sealed class AsyncLock : IDisposable
    {
        public sealed class LockHolder : IDisposable
        {
            AsyncLock parent;
            internal LockHolder(AsyncLock parent)
            {
                this.parent = parent;
            }

            Once dispose_flag;
            public void Dispose()
            {
                if (dispose_flag.IsFirstCall())
                {
                    this.parent.Unlock();
                }
            }
        }

        SemaphoreSlim semaphone = new SemaphoreSlim(1, 1);
        Once dispose_flag;

        public async Task<LockHolder> LockWithAwait()
        {
            await LockAsync();

            return new LockHolder(this);
        }

        public Task LockAsync() => semaphone.WaitAsync();
        public void Unlock() => semaphone.Release();

        public void Dispose()
        {
            if (dispose_flag.IsFirstCall())
            {
                semaphone.DisposeSafe();
                semaphone = null;
            }
        }
    }

    public static class Dbg
    {
        static object LockObj = new object();

        public static long Where(object msg_obj = null, [CallerFilePath] string filename = "", [CallerLineNumber] int line = 0, [CallerMemberName] string caller = null, long last_tick = 0)
        {
            string msg = "";
            if (msg_obj != null) msg = msg_obj.ToString();
            lock (LockObj)
            {
                long now = DateTime.Now.Ticks;
                long diff = now - last_tick;
                WriteLine($"{Path.GetFileName(filename)}:{line} in {caller}()" + (last_tick == 0 ? "" : $" (took {diff} msecs) ") + (string.IsNullOrEmpty(msg) == false ? (": " + msg) : ""));
                return now;
            }
        }
    }

    public static class WebSocketHelper
    {
        public static bool IsLittleEndian { get; }
        public static bool IsBigEndian => !IsLittleEndian;

        static readonly Random random = new Random();

        public const int DefaultMaxDepth = 8;

        public static string NonNull(this string s) { if (s == null) return ""; else return s; }
        public static bool IsEmpty(this string str)
        {
            if (str == null || str.Trim().Length == 0)
                return true;
            else
                return false;
        }
        public static bool IsFilled(this string str) => !IsEmpty(str);

        public static string ObjectToJson(this object obj, bool include_null = false, bool escape_html = false, int? max_depth = DefaultMaxDepth, bool compact = false, bool reference_handling = false) => Serialize(obj, include_null, escape_html, max_depth, compact, reference_handling);
        public static T JsonToObject<T>(this string str, bool include_null = false, int? max_depth = DefaultMaxDepth) => Deserialize<T>(str, include_null, max_depth);
        public static object JsonToObject(this string str, Type type, bool include_null = false, int? max_depth = DefaultMaxDepth) => Deserialize(str, type, include_null, max_depth);

        public static string Serialize(object obj, bool include_null = false, bool escape_html = false, int? max_depth = DefaultMaxDepth, bool compact = false, bool reference_handling = false)
        {
            JsonSerializerSettings setting = new JsonSerializerSettings()
            {
                MaxDepth = max_depth,
                NullValueHandling = include_null ? NullValueHandling.Include : NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Error,
                PreserveReferencesHandling = reference_handling ? PreserveReferencesHandling.All : PreserveReferencesHandling.None,
                StringEscapeHandling = escape_html ? StringEscapeHandling.EscapeHtml : StringEscapeHandling.Default,
            };
            return JsonConvert.SerializeObject(obj, compact ? Formatting.None : Formatting.Indented, setting);
        }

        public static T Deserialize<T>(string str, bool include_null = false, int? max_depth = DefaultMaxDepth)
            => (T)Deserialize(str, typeof(T), include_null, max_depth);

        public static object Deserialize(string str, Type type, bool include_null = false, int? max_depth = DefaultMaxDepth)
        {
            JsonSerializerSettings setting = new JsonSerializerSettings()
            {
                MaxDepth = max_depth,
                NullValueHandling = include_null ? NullValueHandling.Include : NullValueHandling.Ignore,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                ReferenceLoopHandling = ReferenceLoopHandling.Error,
            };
            return JsonConvert.DeserializeObject(str, type, setting);
        }

        public static void Print(this object o)
        {
            string str = o.ObjectToJson();

            if (o is string) str = (string)o;

            Console.WriteLine(str);
        }

        static WebSocketHelper()
        {
            IsLittleEndian = BitConverter.IsLittleEndian;
        }

        public static bool CanUdpSocketErrorBeIgnored(SocketException e)
        {
            switch (e.SocketErrorCode)
            {
                case SocketError.ConnectionReset:
                case SocketError.NetworkReset:
                case SocketError.MessageSize:
                case SocketError.HostUnreachable:
                case SocketError.NetworkUnreachable:
                case SocketError.NoBufferSpaceAvailable:
                case SocketError.AddressNotAvailable:
                case SocketError.ConnectionRefused:
                case SocketError.Interrupted:
                case SocketError.WouldBlock:
                case SocketError.TryAgain:
                case SocketError.InProgress:
                case SocketError.InvalidArgument:
                case (SocketError)12: // ENOMEM
                case (SocketError)10068: // WSAEUSERS
                    return true;
            }
            return false;
        }

        static readonly IPEndPoint udp_ep_ipv4 = new IPEndPoint(IPAddress.Any, 0);
        static readonly IPEndPoint udp_ep_ipv6 = new IPEndPoint(IPAddress.IPv6Any, 0);
        const int udp_max_retry_on_ignore_error = 1000;
        public static async Task<SocketReceiveFromResult> ReceiveFromSafeUdpErrorAsync(this Socket socket, ArraySegment<byte> buffer, SocketFlags socketFlags)
        {
            int num_retry = 0;

            LABEL_RETRY:

            try
            {
                Task<SocketReceiveFromResult> t = socket.ReceiveFromAsync(buffer, socketFlags, socket.AddressFamily == AddressFamily.InterNetworkV6 ? udp_ep_ipv6 : udp_ep_ipv4);
                if (t.IsCompleted == false)
                {
                    num_retry = 0;
                    await t;
                }
                SocketReceiveFromResult ret = t.Result;
                if (ret.ReceivedBytes <= 0) throw new SocketDisconnectedException();
                return ret;
            }
            catch (SocketException e) when (CanUdpSocketErrorBeIgnored(e) || socket.Available >= 1)
            {
                num_retry++;
                if (num_retry >= udp_max_retry_on_ignore_error)
                {
                    throw;
                }
                await Task.Yield();
                goto LABEL_RETRY;
            }
        }
        public static Task<SocketReceiveFromResult> ReceiveFromSafeUdpErrorAsync(this Socket socket, Memory<byte> buffer, SocketFlags socketFlags)
            => ReceiveFromSafeUdpErrorAsync(socket, buffer.AsSegment(), socketFlags);

        public static async Task<int> SendToSafeUdpErrorAsync(this Socket socket, ArraySegment<byte> buffer, SocketFlags socketFlags, EndPoint remoteEP)
        {
            try
            {
                Task<int> t = socket.SendToAsync(buffer, socketFlags, remoteEP);
                if (t.IsCompleted == false)
                {
                    await t;
                }
                int ret = t.Result;
                if (ret <= 0) throw new SocketDisconnectedException();
                return ret;
            }
            catch (SocketException e) when (CanUdpSocketErrorBeIgnored(e))
            {
                return buffer.Count;
            }
        }
        public static Task<int> SendToSafeUdpErrorAsync(this Socket socket, Memory<byte> buffer, SocketFlags socketFlags, EndPoint remoteEP)
            => SendToSafeUdpErrorAsync(socket, buffer.AsSegment(), socketFlags, remoteEP);

        public static async Task ConnectAsync(this TcpClient tc, string host, int port,
            int timeout = Timeout.Infinite, CancellationToken cancel = default(CancellationToken), params CancellationToken[] cancel_tokens)
        {
            await DoAsyncWithTimeout(
            main_proc: async c =>
            {
                await tc.ConnectAsync(host, port);
                return 0;
            },
            cancel_proc: () =>
            {
                tc.DisposeSafe();
            },
            timeout: timeout,
            cancel: cancel,
            cancel_tokens: cancel_tokens);
        }

        public static void DisposeSafe(this IDisposable obj)
        {
            try
            {
                if (obj != null) obj.Dispose();
            }
            catch { }
        }

        public static byte[] Rand(int size) { byte[] r = new byte[size]; Rand(r); return r; }

        public static void Rand(Span<byte> dest)
        {
            lock (random)
            {
                random.NextBytes(dest);
            }
        }

        public static byte RandUInt8()
        {
            Span<byte> mem = stackalloc byte[1];
            Rand(mem);
            return mem.GetUInt8();
        }

        public static ushort RandUInt16()
        {
            Span<byte> mem = stackalloc byte[2];
            Rand(mem);
            return mem.GetUInt16();
        }

        public static uint RandUInt32()
        {
            Span<byte> mem = stackalloc byte[4];
            Rand(mem);
            return mem.GetUInt32();
        }

        public static ulong RandUInt64()
        {
            Span<byte> mem = stackalloc byte[8];
            Rand(mem);
            return mem.GetUInt64();
        }

        public static byte RandUInt7()
        {
            Span<byte> mem = stackalloc byte[1];
            Rand(mem);
            mem[0] &= 0x7F;
            return mem.GetUInt8();
        }

        public static ushort RandUInt15()
        {
            Span<byte> mem = stackalloc byte[2];
            Rand(mem);
            mem[0] &= 0x7F;
            return mem.GetUInt16();
        }

        public static uint RandUInt31()
        {
            Span<byte> mem = stackalloc byte[4];
            Rand(mem);
            mem[0] &= 0x7F;
            return mem.GetUInt32();
        }

        public static ulong RandUInt63()
        {
            Span<byte> mem = stackalloc byte[8];
            Rand(mem);
            mem[0] &= 0x7F;
            return mem.GetUInt64();
        }

        public static sbyte RandSInt8()
        {
            Span<byte> mem = stackalloc byte[1];
            Rand(mem);
            return mem.GetSInt8();
        }

        public static short RandSInt16()
        {
            Span<byte> mem = stackalloc byte[2];
            Rand(mem);
            return mem.GetSInt16();
        }

        public static int RandSInt32()
        {
            Span<byte> mem = stackalloc byte[4];
            Rand(mem);
            return mem.GetSInt32();
        }

        public static long RandSInt64()
        {
            Span<byte> mem = stackalloc byte[8];
            Rand(mem);
            return mem.GetSInt64();
        }

        public static sbyte RandSInt7()
        {
            Span<byte> mem = stackalloc byte[1];
            Rand(mem);
            mem[0] &= 0x7F;
            return mem.GetSInt8();
        }

        public static short RandSInt15()
        {
            Span<byte> mem = stackalloc byte[2];
            Rand(mem);
            mem[0] &= 0x7F;
            return mem.GetSInt16();
        }

        public static int RandSInt31()
        {
            Span<byte> mem = stackalloc byte[4];
            Rand(mem);
            mem[0] &= 0x7F;
            return mem.GetSInt32();
        }

        public static long RandSInt63()
        {
            Span<byte> mem = stackalloc byte[8];
            Rand(mem);
            mem[0] &= 0x7F;
            return mem.GetSInt64();
        }

        public static bool RandBool()
        {
            return (RandUInt32() % 2) == 0;
        }

        public static int GenRandInterval(int min, int max)
        {
            int a = Math.Min(min, max);
            int b = Math.Max(min, max);

            if (a == b)
            {
                return a;
            }

            return (RandSInt31() % (b - 1)) + a;
        }

        public static string ByteToHex(byte[] data)
        {
            return ByteToHex(data, "");
        }
        public static string ByteToHex(byte[] data, string paddingStr)
        {
            StringBuilder ret = new StringBuilder();

            int i;
            for (i = 0; i < data.Length; i++)
            {
                byte b = data[i];

                string s = b.ToString("X");
                if (s.Length == 1)
                {
                    s = "0" + s;
                }

                ret.Append(s);

                if (paddingStr != null)
                {
                    if (i != (data.Length - 1))
                    {
                        ret.Append(paddingStr);
                    }
                }
            }

            return ret.ToString().Trim();
        }

        public static byte[] HexToByte(string str)
        {
            try
            {
                List<byte> o = new List<byte>();
                string tmp = "";
                int i, len;

                str = str.ToUpper().Trim();
                len = str.Length;

                for (i = 0; i < len; i++)
                {
                    char c = str[i];
                    if (('0' <= c && c <= '9') || ('A' <= c && c <= 'F'))
                    {
                        tmp += c;
                        if (tmp.Length == 2)
                        {
                            byte b = Convert.ToByte(tmp, 16);
                            o.Add(b);
                            tmp = "";
                        }
                    }
                    else if (c == ' ' || c == ',' || c == '-' || c == ';')
                    {
                    }
                    else
                    {
                        break;
                    }
                }

                return o.ToArray();
            }
            catch
            {
                return new byte[0];
            }
        }

        public static byte[] CopyByte(byte[] src)
        {
            return (byte[])src.Clone();
        }
        public static byte[] CopyByte(byte[] src, int srcOffset)
        {
            return CopyByte(src, srcOffset, src.Length - srcOffset);
        }
        public static byte[] CopyByte(byte[] src, int srcOffset, int size)
        {
            byte[] ret = new byte[size];
            CopyByte(ret, 0, src, srcOffset, size);
            return ret;
        }
        public static void CopyByte(byte[] dst, byte[] src, int srcOffset, int size)
        {
            CopyByte(dst, 0, src, srcOffset, size);
        }
        public static void CopyByte(byte[] dst, int dstOffset, byte[] src)
        {
            CopyByte(dst, dstOffset, src, 0, src.Length);
        }
        public static void CopyByte(byte[] dst, int dstOffset, byte[] src, int srcOffset, int size)
        {
            Array.Copy(src, srcOffset, dst, dstOffset, size);
        }

        public static byte[] AsciiToByteArray(this object o)
        {
            return Encoding.ASCII.GetBytes(o.ToString());
        }

        public static string ByteArrayToAscii(this byte[] d)
        {
            return Encoding.ASCII.GetString(d);
        }

        public static void TryCloseNonBlock(this Stream stream)
        {
            new Task(() =>
            {
                try
                {
                    stream.Close();
                }
                catch
                {
                }
            }).Start();
        }

        public static Task WhenCanceled(CancellationToken cancel, out CancellationTokenRegistration registration)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            registration = cancel.Register(() =>
            {
                tcs.SetResult(true);
            });

            return tcs.Task;
        }

        public static async Task CancelAsync(this CancellationTokenSource cts, bool throwOnFirstException = false)
        {
            await Task.Run(() => cts.Cancel(throwOnFirstException));
        }

        public static async Task TryCancelAsync(this CancellationTokenSource cts)
        {
            await Task.Run(() => TryCancel(cts));
        }

        public static void TryCancel(this CancellationTokenSource cts)
        {
            try
            {
                cts.Cancel();
            }
            catch
            {
            }
        }

        public static async Task TryWaitAsync(this Task t)
        {
            if (t == null) return;
            try
            {
                await t;
            }
            catch (Exception ex)
            {
                WriteLine("Task exception: " + ex.ToString());
            }
        }

        public static void TryWait(this Task t)
        {
            try
            {
                t.Wait();
            }
            catch { }
        }

        public static async Task<byte[]> ReadAsyncWithTimeout(this Stream stream, int max_size = 65536, int? timeout = null, bool? read_all = false, CancellationToken cancel = default(CancellationToken))
        {
            byte[] tmp = new byte[max_size];
            int ret = await stream.ReadAsyncWithTimeout(tmp, 0, tmp.Length, timeout,
                read_all: read_all,
                cancel: cancel);
            return CopyByte(tmp, 0, ret);
        }

        public static async Task<int> ReadAsyncWithTimeout(this Stream stream, byte[] buffer, int offset = 0, int? count = null, int? timeout = null, bool? read_all = false, CancellationToken cancel = default(CancellationToken), params CancellationToken[] cancel_tokens)
        {
            if (timeout == null) timeout = stream.ReadTimeout;
            if (timeout <= 0) timeout = Timeout.Infinite;
            int target_read_size = count ?? (buffer.Length - offset);
            if (target_read_size == 0) return 0;

            try
            {
                int ret = await DoAsyncWithTimeout(async (cancel_for_proc) =>
                {
                    if (read_all == false)
                    {
                        return await stream.ReadAsync(buffer, offset, target_read_size, cancel_for_proc);
                    }
                    else
                    {
                        int current_read_size = 0;

                        while (current_read_size != target_read_size)
                        {
                            int sz = await stream.ReadAsync(buffer, offset + current_read_size, target_read_size - current_read_size, cancel_for_proc);
                            if (sz == 0)
                            {
                                return 0;
                            }

                            current_read_size += sz;
                        }

                        return current_read_size;
                    }
                },
                timeout: (int)timeout,
                cancel: cancel,
                cancel_tokens: cancel_tokens);

                if (ret <= 0)
                {
                    throw new EndOfStreamException("The NetworkStream is disconnected.");
                }

                return ret;
            }
            catch
            {
                stream.TryCloseNonBlock();
                throw;
            }
        }

        public static async Task WriteAsyncWithTimeout(this Stream stream, byte[] buffer, int offset = 0, int? count = null, int? timeout = null, CancellationToken cancel = default(CancellationToken), params CancellationToken[] cancel_tokens)
        {
            if (timeout == null) timeout = stream.WriteTimeout;
            if (timeout <= 0) timeout = Timeout.Infinite;
            int target_write_size = count ?? (buffer.Length - offset);
            if (target_write_size == 0) return;

            try
            {
                await DoAsyncWithTimeout(async (cancel_for_proc) =>
                {
                    await stream.WriteAsync(buffer, offset, target_write_size, cancel_for_proc);
                    return 0;
                },
                timeout: (int)timeout,
                cancel: cancel,
                cancel_tokens: cancel_tokens);

            }
            catch
            {
                stream.TryCloseNonBlock();
                throw;
            }
        }

        public static async Task<TResult> DoAsyncWithTimeout<TResult>(Func<CancellationToken, Task<TResult>> main_proc, Action cancel_proc = null, int timeout = Timeout.Infinite, CancellationToken cancel = default(CancellationToken), params CancellationToken[] cancel_tokens)
        {
            if (timeout < 0) timeout = Timeout.Infinite;
            if (timeout == 0) throw new TimeoutException("timeout == 0");

            List<Task> wait_tasks = new List<Task>();
            List<IDisposable> disposes = new List<IDisposable>();
            Task timeout_task = null;
            CancellationTokenSource timeout_cts = null;
            CancellationTokenSource cancel_for_proc = new CancellationTokenSource();

            if (timeout != Timeout.Infinite)
            {
                timeout_cts = new CancellationTokenSource();
                timeout_task = Task.Delay(timeout, timeout_cts.Token);
                disposes.Add(timeout_cts);

                wait_tasks.Add(timeout_task);
            }

            try
            {
                if (cancel.CanBeCanceled)
                {
                    cancel.ThrowIfCancellationRequested();

                    Task t = WhenCanceled(cancel, out CancellationTokenRegistration reg);
                    disposes.Add(reg);
                    wait_tasks.Add(t);
                }

                foreach (CancellationToken c in cancel_tokens)
                {
                    if (c.CanBeCanceled)
                    {
                        c.ThrowIfCancellationRequested();

                        Task t = WhenCanceled(c, out CancellationTokenRegistration reg);
                        disposes.Add(reg);
                        wait_tasks.Add(t);
                    }
                }

                Task<TResult> proc_task = main_proc(cancel_for_proc.Token);

                if (proc_task.IsCompleted)
                {
                    return proc_task.Result;
                }

                wait_tasks.Add(proc_task);

                await Task.WhenAny(wait_tasks.ToArray());

                foreach (CancellationToken c in cancel_tokens)
                {
                    c.ThrowIfCancellationRequested();
                }

                cancel.ThrowIfCancellationRequested();

                if (proc_task.IsCompleted)
                {
                    return proc_task.Result;
                }

                throw new TimeoutException();
            }
            catch
            {
                try
                {
                    cancel_for_proc.Cancel();
                }
                catch { }
                try
                {
                    if (cancel_proc != null) cancel_proc();
                }
                catch
                {
                }
                throw;
            }
            finally
            {
                if (timeout_cts != null)
                {
                    try
                    {
                        timeout_cts.Cancel();
                    }
                    catch
                    {
                    }
                }
                foreach (IDisposable i in disposes)
                {
                    i.DisposeSafe();
                }
            }
        }

        public static void LaissezFaire(this Task task)
        {
            new Task(async () =>
            {
                try
                {
                    await task;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("LaissezFaire: " + ex.ToString());
                }
            }).Start();
        }

        public static IAsyncResult AsApm<T>(this Task<T> task,
                                            AsyncCallback callback,
                                            object state)
        {
            if (task == null)
                throw new ArgumentNullException("task");

            var tcs = new TaskCompletionSource<T>(state);
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    tcs.TrySetException(t.Exception.InnerExceptions);
                else if (t.IsCanceled)
                    tcs.TrySetCanceled();
                else
                    tcs.TrySetResult(t.Result);

                if (callback != null)
                    callback(tcs.Task);
            }, TaskScheduler.Default);
            return tcs.Task;
        }

        public static IAsyncResult AsApm(this Task task,
                                            AsyncCallback callback,
                                            object state)
        {
            if (task == null)
                throw new ArgumentNullException("task");

            var tcs = new TaskCompletionSource<int>(state);
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    tcs.TrySetException(t.Exception.InnerExceptions);
                else if (t.IsCanceled)
                    tcs.TrySetCanceled();
                else
                    tcs.TrySetResult(0);

                if (callback != null)
                    callback(tcs.Task);
            }, TaskScheduler.Default);
            return tcs.Task;
        }



        public static T ClonePublics<T>(this T o)
        {
            byte[] data = ObjectToXml(o);

            return (T)XmlToObject(data, o.GetType());
        }

        public static byte[] ObjectToXml(object o)
        {
            if (o == null)
            {
                return null;
            }
            Type t = o.GetType();

            return ObjectToXml(o, t);
        }
        public static byte[] ObjectToXml(object o, Type t)
        {
            if (o == null)
            {
                return null;
            }

            MemoryStream ms = new MemoryStream();
            XmlSerializer x = new XmlSerializer(t);

            x.Serialize(ms, o);

            return ms.ToArray();
        }

        public static object XmlToObject(string str, Type t)
        {
            byte[] data = Encoding.UTF8.GetBytes(str);

            return XmlToObject(data, t);
        }
        public static object XmlToObject(byte[] data, Type t)
        {
            if (data == null || data.Length == 0)
            {
                return null;
            }

            MemoryStream ms = new MemoryStream();
            ms.Write(data, 0, data.Length);
            ms.Position = 0;

            XmlSerializer x = new XmlSerializer(t);

            return x.Deserialize(ms);
        }

        public static async Task WaitObjectsAsync(Task[] tasks = null, CancellationToken[] cancels = null, AsyncAutoResetEvent[] auto_events = null,
            AsyncManualResetEvent[] manual_events = null, int timeout = Timeout.Infinite)
        {
            if (tasks == null) tasks = new Task[0];
            if (cancels == null) cancels = new CancellationToken[0];
            if (auto_events == null) auto_events = new AsyncAutoResetEvent[0];
            if (manual_events == null) manual_events = new AsyncManualResetEvent[0];
            if (timeout == 0) return;

            List<Task> task_list = new List<Task>();
            List<CancellationTokenRegistration> reg_list = new List<CancellationTokenRegistration>();
            List<Action> undo_list = new List<Action>();

            foreach (Task t in tasks)
            {
                if (t != null)
                {
                    task_list.Add(t);
                }
            }

            foreach (CancellationToken c in cancels)
            {
                task_list.Add(WhenCanceled(c, out CancellationTokenRegistration reg));
                reg_list.Add(reg);
            }

            foreach (AsyncAutoResetEvent ev in auto_events)
            {
                if (ev != null)
                {
                    task_list.Add(ev.WaitOneAsync(out Action undo));
                    undo_list.Add(undo);
                }
            }

            foreach (AsyncManualResetEvent ev in manual_events)
            {
                if (ev != null)
                {
                    task_list.Add(ev.WaitAsync());
                }
            }

            CancellationTokenSource delay_cancel = new CancellationTokenSource();

            if (timeout >= 1)
            {
                task_list.Add(Task.Delay(timeout, delay_cancel.Token));
            }

            try
            {
                await Task.WhenAny(task_list.ToArray());
            }
            catch { }
            finally
            {
                foreach (Action undo in undo_list)
                    undo();

                foreach (CancellationTokenRegistration reg in reg_list)
                {
                    reg.Dispose();
                }

                if (delay_cancel != null)
                {
                    delay_cancel.Cancel();
                    delay_cancel.Dispose();
                }
            }
        }

        public static Exception GetSingleException(this Exception ex)
        {
            if (ex == null) return null;
            var aex = ex as AggregateException;
            if (aex != null) return aex.Flatten().InnerExceptions[0];
            return ex;
        }

        public const int AeadChaCha20Poly1305MacSize = 16;
        public const int AeadChaCha20Poly1305NonceSize = 12;
        public const int AeadChaCha20Poly1305KeySize = 32;

        static readonly byte[] zero15 = new byte[15];

        static bool crypto_aead_chacha20poly1305_ietf_decrypt_detached(Memory<byte> m, ReadOnlyMemory<byte> c, ReadOnlyMemory<byte> mac, ReadOnlyMemory<byte> ad, ReadOnlyMemory<byte> npub, ReadOnlyMemory<byte> k)
        {
            var kk = k.AsSegment();
            var nn = npub.AsSegment();
            var cc = c.AsSegment();
            var aa = ad.AsSegment();
            var mm = m.AsSegment();

            byte[] block0 = new byte[64];

            ChaCha7539Engine ctx = new ChaCha7539Engine();
            ctx.Init(true, new ParametersWithIV(new KeyParameter(kk.Array, kk.Offset, kk.Count), nn.Array, nn.Offset, nn.Count));
            ctx.ProcessBytes(block0, 0, block0.Length, block0, 0);

            Poly1305 state = new Poly1305();
            state.Init(new KeyParameter(block0, 0, AeadChaCha20Poly1305KeySize));

            state.BlockUpdate(aa.Array, aa.Offset, aa.Count);
            if ((aa.Count % 16) != 0)
                state.BlockUpdate(zero15, 0, 16 - (aa.Count % 16));

            state.BlockUpdate(cc.Array, cc.Offset, cc.Count);
            if ((cc.Count % 16) != 0)
                state.BlockUpdate(zero15, 0, 16 - (cc.Count % 16));

            byte[] slen = BitConverter.GetBytes((ulong)aa.Count);
            if (IsBigEndian) Array.Reverse(slen);
            state.BlockUpdate(slen, 0, slen.Length);

            byte[] mlen = BitConverter.GetBytes((ulong)cc.Count);
            if (IsBigEndian) Array.Reverse(mlen);
            state.BlockUpdate(mlen, 0, mlen.Length);

            byte[] computed_mac = new byte[AeadChaCha20Poly1305MacSize];
            state.DoFinal(computed_mac, 0);

            if (computed_mac.AsSpan().SequenceEqual(mac.Span) == false)
            {
                return false;
            }

            ctx.ProcessBytes(cc.Array, cc.Offset, cc.Count, mm.Array, mm.Offset);

            return true;
        }

        static void crypto_aead_chacha20poly1305_ietf_encrypt_detached(Memory<byte> c, ReadOnlyMemory<byte> mac, ReadOnlyMemory<byte> m, ReadOnlyMemory<byte> ad, ReadOnlyMemory<byte> npub, ReadOnlyMemory<byte> k)
        {
            var kk = k.AsSegment();
            var nn = npub.AsSegment();
            var cc = c.AsSegment();
            var aa = ad.AsSegment();
            var mm = m.AsSegment();

            byte[] block0 = new byte[64];

            ChaCha7539Engine ctx = new ChaCha7539Engine();
            ctx.Init(true, new ParametersWithIV(new KeyParameter(kk.Array, kk.Offset, kk.Count), nn.Array, nn.Offset, nn.Count));
            ctx.ProcessBytes(block0, 0, block0.Length, block0, 0);

            Poly1305 state = new Poly1305();
            state.Init(new KeyParameter(block0, 0, AeadChaCha20Poly1305KeySize));

            state.BlockUpdate(aa.Array, aa.Offset, aa.Count);
            if ((aa.Count % 16) != 0)
                state.BlockUpdate(zero15, 0, 16 - (aa.Count % 16));

            ctx.ProcessBytes(mm.Array, mm.Offset, mm.Count, cc.Array, cc.Offset);

            state.BlockUpdate(cc.Array, cc.Offset, cc.Count);
            if ((cc.Count % 16) != 0)
                state.BlockUpdate(zero15, 0, 16 - (cc.Count % 16));

            byte[] slen = BitConverter.GetBytes((ulong)aa.Count);
            if (IsBigEndian) Array.Reverse(slen);
            state.BlockUpdate(slen, 0, slen.Length);

            byte[] mlen = BitConverter.GetBytes((ulong)mm.Count);
            if (IsBigEndian) Array.Reverse(mlen);
            state.BlockUpdate(mlen, 0, mlen.Length);

            var macmac = mac.AsSegment();
            state.DoFinal(macmac.Array, macmac.Offset);
        }

        static void crypto_aead_chacha20poly1305_ietf_encrypt(Memory<byte> c, ReadOnlyMemory<byte> m, ReadOnlyMemory<byte> ad, ReadOnlyMemory<byte> npub, ReadOnlyMemory<byte> k)
        {
            crypto_aead_chacha20poly1305_ietf_encrypt_detached(c.Slice(0, c.Length - AeadChaCha20Poly1305MacSize),
                c.Slice(c.Length - AeadChaCha20Poly1305MacSize, AeadChaCha20Poly1305MacSize),
                m, ad, npub, k);
        }

        static bool crypto_aead_chacha20poly1305_ietf_decrypt(Memory<byte> m, ReadOnlyMemory<byte> c, ReadOnlyMemory<byte> ad, ReadOnlyMemory<byte> npub, ReadOnlyMemory<byte> k)
        {
            //return crypto_aead_chacha20poly1305_ietf_decrypt_detached(m.Slice(0, c.Length - AeadChaCha20Poly1305MacSize), c.Slice(0, c.Length - AeadChaCha20Poly1305MacSize),
            //    c.Slice(c.Length - AeadChaCha20Poly1305MacSize, AeadChaCha20Poly1305MacSize),
            //    ad, npub, k);
            return crypto_aead_chacha20poly1305_ietf_decrypt_detached(m, c.Slice(0, c.Length - AeadChaCha20Poly1305MacSize),
                c.Slice(c.Length - AeadChaCha20Poly1305MacSize, AeadChaCha20Poly1305MacSize),
                ad, npub, k);
        }

        public static void Aead_ChaCha20Poly1305_Ietf_Encrypt(Memory<byte> dest, ReadOnlyMemory<byte> src, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> nonce, ReadOnlyMemory<byte> aad)
        {
            crypto_aead_chacha20poly1305_ietf_encrypt(dest, src, aad, nonce, key);
        }

        public static bool Aead_ChaCha20Poly1305_Ietf_Decrypt(Memory<byte> dest, ReadOnlyMemory<byte> src, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> nonce, ReadOnlyMemory<byte> aad)
        {
            return crypto_aead_chacha20poly1305_ietf_decrypt(dest, src, aad, nonce, key);
        }

        public static void Aead_ChaCha20Poly1305_Ietf_Test()
        {
            string nonce_hex = "07 00 00 00 40 41 42 43 44 45 46 47";
            string plaintext_hex =
                "4c 61 64 69 65 73 20 61 6e 64 20 47 65 6e 74 6c " +
                "65 6d 65 6e 20 6f 66 20 74 68 65 20 63 6c 61 73 " +
                "73 20 6f 66 20 27 39 39 3a 20 49 66 20 49 20 63 " +
                "6f 75 6c 64 20 6f 66 66 65 72 20 79 6f 75 20 6f " +
                "6e 6c 79 20 6f 6e 65 20 74 69 70 20 66 6f 72 20 " +
                "74 68 65 20 66 75 74 75 72 65 2c 20 73 75 6e 73 " +
                "63 72 65 65 6e 20 77 6f 75 6c 64 20 62 65 20 69 " +
                "74 2e";
            string aad_hex = "50 51 52 53 c0 c1 c2 c3 c4 c5 c6 c7";
            string key_hex = "80 81 82 83 84 85 86 87 88 89 8a 8b 8c 8d 8e 8f " +
                "90 91 92 93 94 95 96 97 98 99 9a 9b 9c 9d 9e 9f";

            string rfc_mac = "1a:e1:0b:59:4f:09:e2:6a:7e:90:2e:cb:d0:60:06:91".Replace(':', ' ');
            string rfc_enc = "d3 1a 8d 34 64 8e 60 db 7b 86 af bc 53 ef 7e c2 " +
                "a4 ad ed 51 29 6e 08 fe a9 e2 b5 a7 36 ee 62 d6 " +
                "3d be a4 5e 8c a9 67 12 82 fa fb 69 da 92 72 8b " +
                "1a 71 de 0a 9e 06 0b 29 05 d6 a5 b6 7e cd 3b 36 " +
                "92 dd bd 7f 2d 77 8b 8c 98 03 ae e3 28 09 1b 58 " +
                "fa b3 24 e4 fa d6 75 94 55 85 80 8b 48 31 d7 bc " +
                "3f f4 de f0 8e 4b 7a 9d e5 76 d2 65 86 ce c6 4b " +
                "61 16";

            var nonce = HexToByte(nonce_hex).AsMemory();
            var plaintext = HexToByte(plaintext_hex).AsMemory();
            var aad = HexToByte(aad_hex).AsMemory();
            var key = HexToByte(key_hex).AsMemory();
            var encrypted = new byte[plaintext.Length + AeadChaCha20Poly1305MacSize].AsMemory();
            var decrypted = new byte[plaintext.Length].AsMemory();

            Console.WriteLine("Aead_ChaCha20Poly1305_Ietf_Test()");

            Aead_ChaCha20Poly1305_Ietf_Encrypt(encrypted, plaintext, key, nonce, aad);

            string encrypted_hex = ByteToHex(encrypted.Slice(0, plaintext.Length).ToArray(), " ");
            string mac_hex = ByteToHex(encrypted.Slice(plaintext.Length, AeadChaCha20Poly1305MacSize).ToArray(), " ");

            Console.WriteLine($"Encrypted:\n{encrypted_hex}\n");

            Console.WriteLine($"MAC:\n{mac_hex}\n");

            var a = HexToByte(rfc_enc);
            if (encrypted.Slice(0, plaintext.Length).Span.SequenceEqual(a) == false)
            {
                throw new ApplicationException("encrypted != rfc_enc");
            }

            Console.WriteLine("Check OK.");

            if (Aead_ChaCha20Poly1305_Ietf_Decrypt(decrypted, encrypted, key, nonce, aad) == false)
            {
                throw new ApplicationException("Decrypt failed.");
            }
            else
            {
                Console.WriteLine("Decrypt OK.");

                if (plaintext.Span.SequenceEqual(decrypted.Span))
                {
                    Console.WriteLine("Same OK.");
                }
                else
                {
                    throw new ApplicationException("Different !!!");
                }
            }
        }

        public static int GetMinTimeout(params int[] values)
        {
            long min_value = long.MaxValue;
            foreach (int v in values)
            {
                long vv;
                if (v < 0)
                    vv = long.MaxValue;
                else
                    vv = v;
                min_value = Math.Min(min_value, vv);
            }
            if (min_value == long.MaxValue)
                return Timeout.Infinite;
            else
                return (int)min_value;
        }
    }

    public struct FastReadList<T>
    {
        static object GlobalWriteLock = new object();
        static volatile int IdSeed = 0;

        SortedDictionary<int, T> Hash;

        volatile T[] InternalFastList;

        public T[] GetListFast() => InternalFastList;

        public int Add(T value)
        {
            lock (GlobalWriteLock)
            {
                if (Hash == null)
                    Hash = new SortedDictionary<int, T>();

                int id = ++IdSeed;
                Hash.Add(id, value);
                Update();
                return id;
            }
        }

        public bool Delete(int id)
        {
            lock (GlobalWriteLock)
            {
                if (Hash == null)
                    return false;

                bool ret = Hash.Remove(id);
                if (ret)
                {
                    Update();
                }
                return ret;
            }
        }

        void Update()
        {
            if (Hash.Count == 0)
                InternalFastList = null;
            else
                InternalFastList = Hash.Values.ToArray();
        }
    }

    public struct Once
    {
        volatile private int flag;
        public bool IsFirstCall() => (Interlocked.CompareExchange(ref this.flag, 1, 0) == 0);
        public bool IsSet => (this.flag != 0);
        public void Clear() => flag = 0;
        public void ThrowDisposedExceptionIfSet()
        {
            if (IsSet)
                throw new ObjectDisposedException("object");
        }
    }

    public sealed class TimeoutDetector : IDisposable
    {
        Task main_loop;

        object LockObj = new object();

        public long Timeout { get; }

        long NextTimeout;

        AsyncAutoResetEvent ev = new AsyncAutoResetEvent();

        CancellationTokenSource halt = new CancellationTokenSource();

        CancelWatcher watcher;
        AutoResetEvent event_auto;
        ManualResetEvent event_manual;

        CancellationTokenSource cts = new CancellationTokenSource();
        public CancellationToken Cancel { get => cts.Token; }
        public Task TaskWaitMe { get => this.main_loop; }

        Action callme;

        public TimeoutDetector(int timeout, CancelWatcher watcher = null, AutoResetEvent event_auto = null, ManualResetEvent event_manual = null, Action callme = null)
        {
            if (timeout == System.Threading.Timeout.Infinite || timeout == int.MaxValue)
            {
                return;
            }

            this.Timeout = timeout;
            this.watcher = watcher;
            this.event_auto = event_auto;
            this.event_manual = event_manual;
            this.callme = callme;

            NextTimeout = FastTick64.Now + this.Timeout;
            main_loop = timeout_detector_main_loop();
        }

        public void Keep()
        {
            Interlocked.Exchange(ref this.NextTimeout, FastTick64.Now + this.Timeout);
        }

        async Task timeout_detector_main_loop()
        {
            using (LeakChecker.EnterShared())
            {
                while (halt.IsCancellationRequested == false)
                {
                    long next_timeout = Interlocked.Read(ref this.NextTimeout);

                    long now = FastTick64.Now;

                    long remain_time = next_timeout - now;

                    if (remain_time <= 0)
                    {
                        break;
                    }
                    else
                    {
                        await WebSocketHelper.WaitObjectsAsync(
                            auto_events: new AsyncAutoResetEvent[] { ev },
                            cancels: new CancellationToken[] { halt.Token },
                            timeout: (int)remain_time);
                    }
                }

                cts.TryCancelAsync().LaissezFaire();
                if (this.watcher != null) this.watcher.Cancel();
                if (this.event_auto != null) this.event_auto.Set();
                if (this.event_manual != null) this.event_manual.Set();
                if (this.callme != null)
                {
                    new Task(() =>
                    {
                        try
                        {
                            this.callme();
                        }
                        catch { }
                    }).Start();
                }
            }
        }

        Once dispose_flag;
        public void Dispose()
        {
            if (dispose_flag.IsFirstCall())
            {
                halt.TryCancelAsync().LaissezFaire();
            }
        }
    }

    public sealed class CancelWatcher : IDisposable
    {
        CancellationTokenSource cts = new CancellationTokenSource();
        public CancellationToken CancelToken { get => cts.Token; }
        public Task TaskWaitMe { get; }
        public AsyncManualResetEvent EventWaitMe { get; } = new AsyncManualResetEvent();
        public bool Canceled { get; private set; } = false;

        CancellationTokenSource canceller = new CancellationTokenSource();

        AsyncAutoResetEvent ev = new AsyncAutoResetEvent();
        volatile bool halt = false;

        HashSet<CancellationToken> target_list = new HashSet<CancellationToken>();
        List<Task> task_list = new List<Task>();

        object LockObj = new object();

        public CancelWatcher(params CancellationToken[] cancels)
        {
            AddWatch(canceller.Token);
            AddWatch(cancels);

            this.TaskWaitMe = cancel_watch_mainloop();
        }

        public void Cancel()
        {
            canceller.TryCancelAsync().LaissezFaire();
            this.Canceled = true;
        }

        async Task cancel_watch_mainloop()
        {
            using (LeakChecker.EnterShared())
            {
                while (true)
                {
                    List<CancellationToken> cancels = new List<CancellationToken>();

                    lock (LockObj)
                    {
                        foreach (CancellationToken c in target_list)
                            cancels.Add(c);
                    }

                    await WebSocketHelper.WaitObjectsAsync(
                        cancels: cancels.ToArray(),
                        auto_events: new AsyncAutoResetEvent[] { ev });

                    bool canceled = false;

                    lock (LockObj)
                    {
                        foreach (CancellationToken c in target_list)
                        {
                            if (c.IsCancellationRequested)
                            {
                                canceled = true;
                                break;
                            }
                        }
                    }

                    if (halt)
                    {
                        canceled = true;
                    }

                    if (canceled)
                    {
                        this.cts.TryCancelAsync().LaissezFaire();
                        this.EventWaitMe.Set();
                        this.Canceled = true;
                        break;
                    }
                }
            }
        }

        public bool AddWatch(params CancellationToken[] cancels)
        {
            bool ret = false;

            lock (LockObj)
            {
                foreach (CancellationToken cancel in cancels)
                {
                    if (cancel != CancellationToken.None)
                    {
                        if (this.target_list.Contains(cancel) == false)
                        {
                            this.target_list.Add(cancel);
                            ret = true;
                        }
                    }
                }
            }

            if (ret)
            {
                this.ev.Set();
            }

            return ret;
        }

        Once dispose_flag;

        public void Dispose()
        {
            if (dispose_flag.IsFirstCall())
            {
                this.halt = true;
                this.Canceled = true;
                this.ev.Set();
                this.cts.TryCancelAsync().LaissezFaire();
                this.EventWaitMe.Set();
                this.TaskWaitMe.Wait();
            }
        }
    }

    public class AsyncAutoResetEvent
    {
        object lockobj = new object();
        List<AsyncManualResetEvent> event_queue = new List<AsyncManualResetEvent>();
        bool is_set = false;

        public Task WaitOneAsync(out Action cancel)
        {
            lock (lockobj)
            {
                if (is_set)
                {
                    is_set = false;
                    cancel = () => { };
                    return Task.CompletedTask;
                }

                AsyncManualResetEvent e = new AsyncManualResetEvent();

                Task ret = e.WaitAsync();

                event_queue.Add(e);

                cancel = () =>
                {
                    lock (lockobj)
                    {
                        event_queue.Remove(e);
                    }
                };

                return ret;
            }
        }

        volatile int lazy_queued_set = 0;


        public void SetLazy() => Interlocked.Exchange(ref lazy_queued_set, 1);


        public void SetIfLazyQueued(bool softly = false)
        {
            if (Interlocked.CompareExchange(ref lazy_queued_set, 0, 1) == 1)
            {
                Set(softly);
            }
        }

        public void Set(bool softly = false)
        {
            AsyncManualResetEvent ev = null;
            lock (lockobj)
            {
                if (event_queue.Count >= 1)
                {
                    ev = event_queue[event_queue.Count - 1];
                    event_queue.Remove(ev);
                }

                if (ev == null)
                {
                    is_set = true;
                }
            }

            if (ev != null)
            {
                ev.Set(softly);
            }
        }
    }

    public class AsyncManualResetEvent
    {
        object lockobj = new object();
        volatile TaskCompletionSource<bool> tcs;
        bool is_set = false;

        public AsyncManualResetEvent()
        {
            init();
        }

        void init()
        {
            this.tcs = new TaskCompletionSource<bool>();
        }

        public bool IsSet
        {
            get
            {
                lock (lockobj)
                {
                    return this.is_set;
                }
            }
        }

        public Task WaitAsync()
        {
            lock (lockobj)
            {
                if (is_set)
                {
                    return Task.CompletedTask;
                }
                else
                {
                    return tcs.Task;
                }
            }
        }

        public void Set(bool softly = false)
        {
            if (softly)
            {
                Task.Factory.StartNew(() => Set());
                return;
            }

            lock (lockobj)
            {
                if (is_set == false)
                {
                    is_set = true;
                    tcs.TrySetResult(true);
                }
            }
        }

        public void Reset()
        {
            lock (lockobj)
            {
                if (is_set)
                {
                    is_set = false;
                    init();
                }
            }
        }
    }

    class TimeHelper
    {
        internal Stopwatch Sw;
        internal long Freq;
        internal DateTimeOffset FirstDateTimeOffset;

        public TimeHelper()
        {
            FirstDateTimeOffset = DateTimeOffset.Now;
            //FirstDateTimeOffset = 
            Sw = new Stopwatch();
            Sw.Start();
            Freq = Stopwatch.Frequency;
        }

        public DateTimeOffset GetDateTimeOffset()
        {
            return FirstDateTimeOffset + this.Sw.Elapsed;
        }
    }

    public static class Time
    {
        static TimeHelper h = new TimeHelper();
        static TimeSpan baseTimeSpan = new TimeSpan(0, 0, 1);

        static public TimeSpan NowTimeSpan
        {
            get
            {
                return h.Sw.Elapsed.Add(baseTimeSpan);
            }
        }

        static public long NowLong100Usecs
        {
            get
            {
                return NowTimeSpan.Ticks;
            }
        }

        static public long NowLongMillisecs
        {
            get
            {
                return NowLong100Usecs / 10000;
            }
        }

        static public long Tick64
        {
            get
            {
                return NowLongMillisecs;
            }
        }

        static public double NowDouble
        {
            get
            {
                return (double)NowLong100Usecs / (double)10000000.0;
            }
        }

        static public DateTime NowDateTimeLocal
        {
            get
            {
                return h.GetDateTimeOffset().LocalDateTime;
            }
        }


        static public DateTime NowDateTimeUtc
        {
            get
            {
                return h.GetDateTimeOffset().UtcDateTime;
            }
        }

        static public DateTimeOffset NowDateTimeOffset
        {
            get
            {
                return h.GetDateTimeOffset();
            }
        }
    }

    public class Datagram
    {
        public Memory<byte> Data;
        public EndPoint EndPoint;
        public byte Flag;

        public IPEndPoint IPEndPoint { get => (IPEndPoint)EndPoint; set => EndPoint = value; }

        public Datagram(Memory<byte> data, EndPoint end_point, byte flag = 0)
        {
            Data = data;
            EndPoint = end_point;
            Flag = flag;
        }
    }

    public sealed class NonBlockSocket : IDisposable
    {
        public Socket Sock { get; }
        public bool IsStream { get; }
        public bool IsDisconnected { get => Watcher.Canceled; }
        public CancellationToken CancelToken { get => Watcher.CancelToken; }

        public AsyncAutoResetEvent EventSendReady { get; } = new AsyncAutoResetEvent();
        public AsyncAutoResetEvent EventRecvReady { get; } = new AsyncAutoResetEvent();
        public AsyncAutoResetEvent EventSendNow { get; } = new AsyncAutoResetEvent();

        CancelWatcher Watcher;
        byte[] TmpRecvBuffer;

        public Fifo RecvTcpFifo { get; } = new Fifo();
        public Fifo SendTcpFifo { get; } = new Fifo();

        public Queue<Datagram> RecvUdpQueue { get; } = new Queue<Datagram>();
        public Queue<Datagram> SendUdpQueue { get; } = new Queue<Datagram>();

        int MaxRecvFifoSize;
        public int MaxRecvUdpQueueSize { get; }

        Task RecvLoopTask = null;
        Task SendLoopTask = null;

        AsyncBulkReceiver<Datagram, int> UdpBulkReader;

        public NonBlockSocket(Socket s, CancellationToken cancel = default(CancellationToken), int tmp_buffer_size = 65536, int max_recv_buffer_size = 65536, int max_recv_udp_queue_size = 4096)
        {
            if (tmp_buffer_size < 65536) tmp_buffer_size = 65536;
            TmpRecvBuffer = new byte[tmp_buffer_size];
            MaxRecvFifoSize = max_recv_buffer_size;
            MaxRecvUdpQueueSize = max_recv_udp_queue_size;

            EventSendReady.Set();
            EventRecvReady.Set();

            Sock = s;
            IsStream = (s.SocketType == SocketType.Stream);
            Watcher = new CancelWatcher(cancel);

            if (IsStream)
            {
                RecvLoopTask = TCP_RecvLoop();
                SendLoopTask = TCP_SendLoop();
            }
            else
            {
                UdpBulkReader = new AsyncBulkReceiver<Datagram, int>(async x =>
                {
                    SocketReceiveFromResult ret = await Sock.ReceiveFromSafeUdpErrorAsync(TmpRecvBuffer, SocketFlags.None);
                    return new ValueOrClosed<Datagram>(new Datagram(TmpRecvBuffer.AsSpan().Slice(0, ret.ReceivedBytes).ToArray(), (IPEndPoint)ret.RemoteEndPoint));
                });

                RecvLoopTask = UDP_RecvLoop();
                SendLoopTask = UDP_SendLoop();
            }
        }

        async Task TCP_RecvLoop()
        {
            try
            {
                await WebSocketHelper.DoAsyncWithTimeout(async (cancel) =>
                {
                    while (cancel.IsCancellationRequested == false)
                    {
                        int r = await Sock.ReceiveAsync(TmpRecvBuffer, SocketFlags.None, cancel);
                        if (r <= 0) break;

                        while (cancel.IsCancellationRequested == false)
                        {
                            lock (RecvTcpFifo)
                            {
                                if (RecvTcpFifo.Size <= MaxRecvFifoSize)
                                {
                                    RecvTcpFifo.Write(TmpRecvBuffer, r);
                                    break;
                                }
                            }

                            await WebSocketHelper.WaitObjectsAsync(cancels: new CancellationToken[] { cancel },
                                timeout: 10);
                        }

                        EventRecvReady.Set();
                    }

                    return 0;
                },
                cancel: Watcher.CancelToken
                );
            }
            finally
            {
                this.Watcher.Cancel();
                EventSendReady.Set();
                EventRecvReady.Set();
            }
        }

        async Task UDP_RecvLoop()
        {
            try
            {
                await WebSocketHelper.DoAsyncWithTimeout(async (cancel) =>
                {
                    while (cancel.IsCancellationRequested == false)
                    {
                        Datagram[] recv_packets = await UdpBulkReader.Recv(cancel);

                        bool full_queue = false;
                        bool pkt_received = false;

                        lock (RecvUdpQueue)
                        {
                            foreach (Datagram p in recv_packets)
                            {
                                if (RecvUdpQueue.Count <= MaxRecvUdpQueueSize)
                                {
                                    RecvUdpQueue.Enqueue(p);
                                    pkt_received = true;
                                }
                                else
                                {
                                    full_queue = true;
                                    break;
                                }
                            }
                        }

                        if (full_queue)
                        {
                            await WebSocketHelper.WaitObjectsAsync(cancels: new CancellationToken[] { cancel },
                                timeout: 10);
                        }

                        if (pkt_received)
                        {
                            EventRecvReady.Set();
                        }
                    }

                    return 0;
                },
                cancel: Watcher.CancelToken
                );
            }
            finally
            {
                this.Watcher.Cancel();
                EventSendReady.Set();
                EventRecvReady.Set();
            }
        }

        async Task TCP_SendLoop()
        {
            try
            {
                await WebSocketHelper.DoAsyncWithTimeout(async (cancel) =>
                {
                    while (cancel.IsCancellationRequested == false)
                    {
                        byte[] send_data = null;

                        while (cancel.IsCancellationRequested == false)
                        {
                            lock (SendTcpFifo)
                            {
                                send_data = SendTcpFifo.Read();
                            }

                            if (send_data != null && send_data.Length >= 1)
                            {
                                break;
                            }

                            await WebSocketHelper.WaitObjectsAsync(cancels: new CancellationToken[] { cancel },
                                auto_events: new AsyncAutoResetEvent[] { EventSendNow });
                        }

                        int r = await Sock.SendAsync(send_data, SocketFlags.None, cancel);
                        if (r <= 0) break;

                        EventSendReady.Set();
                    }

                    return 0;
                },
                cancel: Watcher.CancelToken
                );
            }
            finally
            {
                this.Watcher.Cancel();
                EventSendReady.Set();
                EventRecvReady.Set();
            }
        }

        async Task UDP_SendLoop()
        {
            try
            {
                await WebSocketHelper.DoAsyncWithTimeout(async (cancel) =>
                {
                    while (cancel.IsCancellationRequested == false)
                    {
                        Datagram pkt = null;

                        while (cancel.IsCancellationRequested == false)
                        {
                            lock (SendUdpQueue)
                            {
                                if (SendUdpQueue.Count >= 1)
                                {
                                    pkt = SendUdpQueue.Dequeue();
                                }
                            }

                            if (pkt != null)
                            {
                                break;
                            }

                            await WebSocketHelper.WaitObjectsAsync(cancels: new CancellationToken[] { cancel },
                                auto_events: new AsyncAutoResetEvent[] { EventSendNow });
                        }

                        int r = await Sock.SendToSafeUdpErrorAsync(pkt.Data.AsSegment(), SocketFlags.None, pkt.IPEndPoint);
                        if (r <= 0) break;

                        EventSendReady.Set();
                    }

                    return 0;
                },
                cancel: Watcher.CancelToken
                );
            }
            catch (Exception ex)
            {
                Dbg.Where(ex);
            }
            finally
            {
                this.Watcher.Cancel();
                EventSendReady.Set();
                EventRecvReady.Set();
            }
        }

        public Datagram RecvFromNonBlock()
        {
            if (IsDisconnected) return null;
            lock (RecvUdpQueue)
            {
                if (RecvUdpQueue.TryDequeue(out Datagram ret))
                {
                    return ret;
                }
            }
            return null;
        }

        Once dispose;
        public void Dispose()
        {
            if (dispose.IsFirstCall())
            {
                Watcher.DisposeSafe();
                Sock.DisposeSafe();
            }
        }
    }

    public struct ValueOrClosed<T>
    {
        bool InternalIsOpen;
        public T Value;

        public bool IsOpen { get => InternalIsOpen; }
        public bool IsClosed { get => !InternalIsOpen; }

        public ValueOrClosed(T value)
        {
            InternalIsOpen = true;
            Value = value;
        }
    }

    public class AsyncBulkReceiver<TUserReturnElement, TUserState>
    {
        public delegate Task<ValueOrClosed<TUserReturnElement>> AsyncReceiveProcDelegate(TUserState state);

        public int DefaultMaxCount { get; } = 1024;

        AsyncReceiveProcDelegate AsyncReceiveProc;

        public AsyncBulkReceiver(AsyncReceiveProcDelegate async_receive_proc, int default_max_count = 1024)
        {
            DefaultMaxCount = default_max_count;
            AsyncReceiveProc = async_receive_proc;
        }

        Task<ValueOrClosed<TUserReturnElement>> pushed_user_task = null;

        public async Task<TUserReturnElement[]> Recv(CancellationToken cancel, TUserState state = default(TUserState), int? max_count = null)
        {
            if (max_count == null) max_count = DefaultMaxCount;
            if (max_count <= 0) max_count = int.MaxValue;
            List<TUserReturnElement> ret = new List<TUserReturnElement>();

            while (true)
            {
                cancel.ThrowIfCancellationRequested();

                Task<ValueOrClosed<TUserReturnElement>> user_task;
                if (pushed_user_task != null)
                {
                    user_task = pushed_user_task;
                    pushed_user_task = null;
                }
                else
                {
                    user_task = AsyncReceiveProc(state);
                }
                if (user_task.IsCompleted == false)
                {
                    if (ret.Count >= 1)
                    {
                        pushed_user_task = user_task;
                        break;
                    }
                    else
                    {
                        await WebSocketHelper.WaitObjectsAsync(
                            tasks: new Task[] { user_task },
                            cancels: new CancellationToken[] { cancel });

                        cancel.ThrowIfCancellationRequested();

                        if (user_task.Result.IsOpen)
                        {
                            ret.Add(user_task.Result.Value);
                        }
                        else
                        {
                            pushed_user_task = user_task;
                            break;
                        }
                    }
                }
                else
                {
                    if (user_task.Result.IsOpen)
                    {
                        ret.Add(user_task.Result.Value);
                    }
                    else
                    {
                        break;
                    }
                }
                if (ret.Count >= max_count) break;
            }

            if (ret.Count >= 1)
                return ret.ToArray();
            else
                return null; // Disconnected
        }
    }

    public class SharedExceptionQueue
    {
        public const int MaxItems = 128;
        SharedQueue<Exception> Queue = new SharedQueue<Exception>(MaxItems);

        public void Raise(Exception ex) => Add(ex, true);

        public void Add(Exception ex, bool raise_first_exception = false)
        {
            if (ex == null)
                ex = new Exception("null exception");

            lock (SharedQueue<Exception>.GlobalLock)
            {
                AggregateException aex = ex as AggregateException;
                if (aex != null)
                {
                    var exp = aex.Flatten().InnerExceptions;
                    foreach (var expi in exp)
                        Queue.Enqueue(expi);
                }
                else
                {
                    Queue.Enqueue(ex);
                }
                if (raise_first_exception)
                    throw Queue.ItemsReadOnly[0];
            }
        }

        public void Encounter(SharedExceptionQueue other) => this.Queue.Encounter(other.Queue);

        public Exception[] GetExceptions() => this.Queue.ItemsReadOnly;
        public Exception[] Exceptions => GetExceptions();
        public Exception FirstException => Exceptions.FirstOrDefault();

        public bool HasError => Exceptions.Length != 0;
        public bool IsOk => !HasError;
    }

    public class SharedQueue<T>
    {
        class QueueBody
        {
            static long global_timestamp;

            public QueueBody Next;

            public SortedList<long, T> List = new SortedList<long, T>();
            public readonly int MaxItems;

            public QueueBody(int max_items)
            {
                if (max_items <= 0) max_items = int.MaxValue;
                MaxItems = max_items;
            }

            public void Enqueue(T item)
            {
                lock (List)
                {
                    if (List.Count > MaxItems) return;
                    long ts = Interlocked.Increment(ref global_timestamp);
                    List.Add(ts, item);
                }
            }

            public T Dequeue()
            {
                lock (List)
                {
                    if (List.Count == 0) return default(T);
                    long ts = List.Keys[0];
                    T ret = List[ts];
                    List.Remove(ts);
                    return ret;
                }
            }

            public T[] ToArray()
            {
                lock (List)
                {
                    return List.Values.ToArray();
                }
            }

            public static void Merge(QueueBody q1, QueueBody q2)
            {
                if (q1 == q2) return;
                lock (q1.List)
                {
                    lock (q2.List)
                    {
                        Debug.Assert(q1.List != null);
                        Debug.Assert(q2.List != null);
                        QueueBody q3 = new QueueBody(Math.Max(q1.MaxItems, q2.MaxItems));
                        foreach (long ts in q1.List.Keys)
                            q3.List.Add(ts, q1.List[ts]);
                        foreach (long ts in q2.List.Keys)
                            q3.List.Add(ts, q2.List[ts]);
                        if (q3.List.Count > q3.MaxItems)
                        {
                            int num = 0;
                            List<long> remove_list = new List<long>();
                            foreach (long ts in q3.List.Keys)
                            {
                                num++;
                                if (num > q3.MaxItems)
                                    remove_list.Add(ts);
                            }
                            foreach (long ts in remove_list)
                                q3.List.Remove(ts);
                        }
                        q1.List = null;
                        q2.List = null;
                        Debug.Assert(q1.Next == null);
                        Debug.Assert(q2.Next == null);
                        q1.Next = q3;
                        q2.Next = q3;
                    }
                }
            }

            public QueueBody GetLast()
            {
                if (Next == null)
                    return this;
                else
                    return Next.GetLast();
            }
        }

        QueueBody First;

        public static readonly object GlobalLock = new object();

        public SharedQueue(int max_items = 0)
        {
            First = new QueueBody(max_items);
        }

        public void Encounter(SharedQueue<T> other)
        {
            if (this == other) return;

            lock (GlobalLock)
            {
                QueueBody last1 = this.First.GetLast();
                QueueBody last2 = other.First.GetLast();
                if (last1 == last2) return;

                QueueBody.Merge(last1, last2);
            }
        }

        public void Enqueue(T value)
        {
            lock (GlobalLock)
                this.First.GetLast().Enqueue(value);
        }

        public T Dequeue()
        {
            lock (GlobalLock)
                return this.First.GetLast().Dequeue();
        }

        public T[] ToArray()
        {
            lock (GlobalLock)
                return this.First.GetLast().ToArray();
        }

        public int CountFast
        {
            get
            {
                var q = this.First.GetLast();
                var list = q.List;
                if (list == null) return 0;
                lock (list)
                    return list.Count;
            }
        }

        public T[] ItemsReadOnly { get => ToArray(); }
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
            foreach (var a in List.OrderBy(x => x.index).ThenBy(x => -x.priority).Select(x => x.bm))
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

    public class LeakChecker
    {
        static public LeakChecker Shared { get; } = new LeakChecker();

        Dictionary<long, string> List = new Dictionary<long, string>();
        long CurrentId = 0;

        public Holder Enter([CallerFilePath] string filename = "", [CallerLineNumber] int line = 0, [CallerMemberName] string caller = null)
            => new Holder(this, $"{caller}() - {Path.GetFileName(filename)}:{line}");

        public static Holder EnterShared([CallerFilePath] string filename = "", [CallerLineNumber] int line = 0, [CallerMemberName] string caller = null)
            => new Holder(Shared, $"{caller}() - {Path.GetFileName(filename)}:{line}");

        public int Count
        {
            get
            {
                lock (List)
                    return List.Count;
            }
        }

        public void Print()
        {
            lock (List)
            {
                if (Count == 0)
                {
                    Console.WriteLine("@@@ No leaks @@@");
                }
                else
                {
                    Console.WriteLine("*** Leaked !!! ***");
                    Console.WriteLine("-----");
                    Console.Write(this.ToString());
                    Console.WriteLine("-----");
                }
            }
        }

        public override string ToString()
        {
            StringWriter w = new StringWriter();
            lock (List)
            {
                foreach (var v in List.OrderBy(x => x.Key))
                    w.WriteLine($"{v.Key}: {v.Value}");
            }
            return w.ToString();
        }

        public sealed class Holder : IDisposable
        {
            LeakChecker Checker;
            long Id;

            internal Holder(LeakChecker checker, string name)
            {
                if (string.IsNullOrEmpty(name)) name = "<untitled>";
                Checker = checker;
                Id = Interlocked.Increment(ref checker.CurrentId);
                lock (checker.List)
                    checker.List.Add(Id, name);
            }

            Once dispose_flag;
            public void Dispose()
            {
                if (dispose_flag.IsFirstCall())
                {
                    lock (Checker.List)
                    {
                        Debug.Assert(Checker.List.ContainsKey(Id));
                        Checker.List.Remove(Id);
                    }
                }
            }
        }
    }

    public class FastLinkedListNode<T>
    {
        public T Value;
        public FastLinkedListNode<T> Next, Previous;
    }

    public class FastLinkedList<T>
    {
        public int Count;
        public FastLinkedListNode<T> First, Last;

        public void Clear()
        {
            Count = 0;
            First = Last = null;
        }

        public FastLinkedListNode<T> AddFirst(T value)
        {
            if (First == null)
            {
                Debug.Assert(Last == null);
                Debug.Assert(Count == 0);
                First = Last = new FastLinkedListNode<T>() { Value = value, Next = null, Previous = null };
                Count++;
                return First;
            }
            else
            {
                Debug.Assert(Last != null);
                Debug.Assert(Count >= 1);
                var old_first = First;
                var nn = new FastLinkedListNode<T>() { Value = value, Next = old_first, Previous = null };
                Debug.Assert(old_first.Previous == null);
                old_first.Previous = nn;
                First = nn;
                Count++;
                return nn;
            }
        }

        public void AddFirst(FastLinkedListNode<T> chain_first, FastLinkedListNode<T> chain_last, int chained_count)
        {
            if (First == null)
            {
                Debug.Assert(Last == null);
                Debug.Assert(Count == 0);
                First = chain_first;
                Last = chain_last;
                chain_first.Previous = null;
                chain_last.Next = null;
                Count = chained_count;
            }
            else
            {
                Debug.Assert(Last != null);
                Debug.Assert(Count >= 1);
                var old_first = First;
                Debug.Assert(old_first.Previous == null);
                old_first.Previous = chain_last;
                First = chain_first;
                Count += chained_count;
            }
        }

        public FastLinkedListNode<T> AddLast(T value)
        {
            if (Last == null)
            {
                Debug.Assert(First == null);
                Debug.Assert(Count == 0);
                First = Last = new FastLinkedListNode<T>() { Value = value, Next = null, Previous = null };
                Count++;
                return Last;
            }
            else
            {
                Debug.Assert(First != null);
                Debug.Assert(Count >= 1);
                var old_last = Last;
                var nn = new FastLinkedListNode<T>() { Value = value, Next = null, Previous = old_last };
                Debug.Assert(old_last.Next == null);
                old_last.Next = nn;
                Last = nn;
                Count++;
                return nn;
            }
        }

        public void AddLast(FastLinkedListNode<T> chain_first, FastLinkedListNode<T> chain_last, int chained_count)
        {
            if (Last == null)
            {
                Debug.Assert(First == null);
                Debug.Assert(Count == 0);
                First = chain_first;
                Last = chain_last;
                chain_first.Previous = null;
                chain_last.Next = null;
                Count = chained_count;
            }
            else
            {
                Debug.Assert(First != null);
                Debug.Assert(Count >= 1);
                var old_last = Last;
                Debug.Assert(old_last.Next == null);
                old_last.Next = chain_first;
                Last = chain_last;
                Count += chained_count;
            }
        }

        public FastLinkedListNode<T> AddAfter(FastLinkedListNode<T> prev_node, T value)
        {
            var next_node = prev_node.Next;
            Debug.Assert(First != null && Last != null);
            Debug.Assert(next_node != null || Last == prev_node);
            Debug.Assert(next_node == null || next_node.Previous == prev_node);
            var nn = new FastLinkedListNode<T>() { Value = value, Next = next_node, Previous = prev_node };
            prev_node.Next = nn;
            if (next_node != null) next_node.Previous = nn;
            if (Last == prev_node) Last = nn;
            Count++;
            return nn;
        }

        public void AddAfter(FastLinkedListNode<T> prev_node, FastLinkedListNode<T> chain_first, FastLinkedListNode<T> chain_last, int chained_count)
        {
            var next_node = prev_node.Next;
            Debug.Assert(First != null && Last != null);
            Debug.Assert(next_node != null || Last == prev_node);
            Debug.Assert(next_node == null || next_node.Previous == prev_node);
            prev_node.Next = chain_first;
            chain_first.Previous = prev_node;
            if (next_node != null) next_node.Previous = chain_last;
            chain_last.Previous = next_node;
            if (Last == prev_node) Last = chain_last;
            Count += chained_count;
        }

        public FastLinkedListNode<T> AddBefore(FastLinkedListNode<T> next_node, T value)
        {
            var prev_node = next_node.Previous;
            Debug.Assert(First != null && Last != null);
            Debug.Assert(prev_node != null || First == next_node);
            Debug.Assert(prev_node == null || prev_node.Next == next_node);
            var nn = new FastLinkedListNode<T>() { Value = value, Next = next_node, Previous = prev_node };
            next_node.Previous = nn;
            if (prev_node != null) prev_node.Next = nn;
            if (First == next_node) First = nn;
            Count++;
            return nn;
        }

        public void AddBefore(FastLinkedListNode<T> next_node, FastLinkedListNode<T> chain_first, FastLinkedListNode<T> chain_last, int chained_count)
        {
            var prev_node = next_node.Previous;
            Debug.Assert(First != null && Last != null);
            Debug.Assert(prev_node != null || First == next_node);
            Debug.Assert(prev_node == null || prev_node.Next == next_node);
            next_node.Previous = chain_last;
            chain_last.Next = next_node;
            if (prev_node != null) prev_node.Next = chain_first;
            chain_first.Previous = prev_node;
            if (First == next_node) First = chain_first;
            Count += chained_count;
        }

        public void Remove(FastLinkedListNode<T> node)
        {
            Debug.Assert(First != null && Last != null);

            if (node.Previous != null && node.Next != null)
            {
                Debug.Assert(First != null);
                Debug.Assert(Last != null);
                Debug.Assert(First != node);
                Debug.Assert(Last != node);

                node.Previous.Next = node.Next;
                node.Next.Previous = node.Previous;

                Count--;
            }
            else if (node.Previous == null && node.Next == null)
            {
                Debug.Assert(First == node);
                Debug.Assert(Last == node);

                First = Last = null;

                Count--;
            }
            else if (node.Previous != null)
            {
                Debug.Assert(First != null);
                Debug.Assert(First != node);
                Debug.Assert(Last == node);

                node.Previous.Next = null;
                Last = node.Previous;

                Count--;
            }
            else
            {
                Debug.Assert(Last != null);
                Debug.Assert(Last != node);
                Debug.Assert(First == node);

                node.Next.Previous = null;
                First = node.Next;

                Count--;
            }
        }

        public T[] ItemsReadOnly
        {
            get
            {
                List<T> ret = new List<T>();
                var node = First;
                while (node != null)
                {
                    ret.Add(node.Value);
                    node = node.Next;
                }
                return ret.ToArray();
            }
        }
    }

    public sealed class LocalTimer
    {
        SortedSet<long> List = new SortedSet<long>();
        HashSet<long> Hash = new HashSet<long>();
        public long Now { get; private set; } = FastTick64.Now;
        public bool AutomaticUpdateNow { get; }

        public LocalTimer(bool automatic_update_now = true)
        {
            AutomaticUpdateNow = automatic_update_now;
        }

        public void UpdateNow() => Now = FastTick64.Now;
        public void UpdateNow(long now_tick) => Now = now_tick;

        public void AddTick(long tick)
        {
            if (Hash.Add(tick))
                List.Add(tick);
        }

        public long AddTimeout(int interval)
        {
            if (interval < 0) return long.MaxValue;
            interval = Math.Max(interval, 0);
            if (AutomaticUpdateNow) UpdateNow();
            long v = Now + interval;
            AddTick(v);
            return v;
        }

        public int GetNextInterval()
        {
            int ret = Timeout.Infinite;
            if (AutomaticUpdateNow) UpdateNow();
            long now = Now;
            List<long> delete_list = null;

            foreach (long v in List)
            {
                if (now >= v)
                {
                    ret = 0;
                    if (delete_list == null) delete_list = new List<long>();
                    delete_list.Add(v);
                }
                else
                {
                    break;
                }
            }

            if (delete_list != null)
            {
                foreach (long v in delete_list)
                {
                    List.Remove(v);
                    Hash.Remove(v);
                }
            }

            if (ret == Timeout.Infinite)
            {
                if (List.Count >= 1)
                {
                    long v = List.First();
                    ret = (int)(v - now);
                    Debug.Assert(ret > 0);
                    if (ret <= 0) ret = 0;
                }
            }

            return ret;
        }
    }

    public class HostNetworkInfo : IEquatable<HostNetworkInfo>
    {
        public int Version;
        public long LastChangedTick;
        public string HostName;
        public string DomainName;
        public string FqdnHostName => HostName + (string.IsNullOrEmpty(DomainName) ? "" : "." + DomainName);
        public bool IsIPv4Supported;
        public bool IsIPv6Supported;
        public List<IPAddress> IPAddressList = new List<IPAddress>();

        public static HostNetworkInfo CurrentInfo => HostNetworkInfoManager.CurrentInfo;

        public bool Equals(HostNetworkInfo other)
        {
            if (string.Equals(this.HostName, other.HostName) == false) return false;
            if (string.Equals(this.DomainName, other.DomainName) == false) return false;
            if (this.IsIPv4Supported != other.IsIPv4Supported) return false;
            if (this.IsIPv6Supported != other.IsIPv6Supported) return false;
            if (this.IPAddressListBinary.Span.SequenceEqual(other.IPAddressListBinary.Span) == false) return false;
            return true;
        }

        public Memory<byte> IPAddressListBinary
        {
            get
            {
                MemoryBuffer<byte> ret = new MemoryBuffer<byte>();
                foreach (IPAddress addr in IPAddressList)
                {
                    ret.WriteSInt32((int)addr.AddressFamily);
                    ret.Write(addr.GetAddressBytes());
                    if (addr.AddressFamily == AddressFamily.InterNetworkV6)
                        ret.WriteSInt64(addr.ScopeId);
                }
                return ret;
            }
        }
    }

    public static class HostNetworkInfoManager
    {
        public static HostNetworkInfo CurrentInfo { get; private set; }

        static HostNetworkInfoManager()
        {
            try
            {
                CurrentInfo = GetCurrentSnapshotByNetInfoApiSlowAsync().Result;
            }
            catch
            {
                CurrentInfo = new HostNetworkInfo()
                {
                    HostName = "localhost",
                    DomainName = "",
                    IsIPv4Supported = true,
                    IsIPv6Supported = true,
                };
                CurrentInfo.IPAddressList.Add(IPAddress.Any);
                CurrentInfo.IPAddressList.Add(IPAddress.IPv6Any);
            }

            Thread t = new Thread(PollingThread);
            t.Name = "HostNetworkInfoManager Polling Thread";
            t.Priority = ThreadPriority.Lowest;
            t.IsBackground = true;
            t.Start();

            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
            NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;
        }

        private static void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e) => PollingThreadSignal.Set();

        private static void NetworkChange_NetworkAddressChanged(object sender, EventArgs e) => PollingThreadSignal.Set();

        public static void Flush() => PollingThreadSignal.Set();

        const long InitialPollingInterval = 987;
        const long MaxPollingInterval = 60 * 1000 + 987;

        static AutoResetEvent PollingThreadSignal = new AutoResetEvent(false);

        public static bool IsUnix { get; } = (Environment.OSVersion.Platform != PlatformID.Win32NT);

        static void PollingThread()
        {
            int num = 0;
            while (true)
            {
                try
                {
                    HostNetworkInfo info = GetCurrentSnapshotByNetInfoApiSlowAsync().Result;

                    if (Update(info))
                    {
                        num = 1;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    num = 1;
                }

                long interval = Math.Min(InitialPollingInterval * num, MaxPollingInterval);
                num++;

                if (PollingThreadSignal.WaitOne((int)interval))
                {
                    num = 0;
                }
            }
        }

        static bool Update(HostNetworkInfo info)
        {
            if (CurrentInfo.Equals(info) == false)
            {
                info.Version = CurrentInfo.Version + 1;
                info.LastChangedTick = FastTick64.Now;

                CurrentInfo = info;

                AsyncAutoResetEvent[] ev_list;
                lock (EventList)
                    ev_list = EventList.ToArray();

                foreach (var ev in ev_list)
                    ev.Set(true);

                return true;
            }
            return false;
        }

        static HashSet<AsyncAutoResetEvent> EventList = new HashSet<AsyncAutoResetEvent>();

        public static void RegisterNotificationEvent(AsyncAutoResetEvent ev)
        {
            lock (EventList)
                EventList.Add(ev);
        }

        public static void UnregisterNotificationEvent(AsyncAutoResetEvent ev)
        {
            lock (EventList)
                EventList.Remove(ev);
        }

        static IPAddress[] GetLocalIPAddressBySocketApi() => Dns.GetHostAddresses(Dns.GetHostName());

        class ByteComparer : IComparer<byte[]>
        {
            public int Compare(byte[] x, byte[] y) => x.AsSpan().SequenceCompareTo(y.AsSpan());
        }

        public static async Task<HostNetworkInfo> GetCurrentSnapshotByNetInfoApiSlowAsync()
        {
            HostNetworkInfo ret = new HostNetworkInfo();
            ret.LastChangedTick = FastTick64.Now;
            IPGlobalProperties prop = IPGlobalProperties.GetIPGlobalProperties();
            ret.HostName = prop.HostName;
            ret.DomainName = prop.DomainName;
            HashSet<IPAddress> hash = new HashSet<IPAddress>();

            if (IsUnix)
            {
                UnicastIPAddressInformationCollection info = await prop.GetUnicastAddressesAsync();
                foreach (UnicastIPAddressInformation ip in info)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork || ip.Address.AddressFamily == AddressFamily.InterNetworkV6)
                        hash.Add(ip.Address);
                }
            }
            else
            {
                try
                {
                    IPAddress[] info = GetLocalIPAddressBySocketApi();
                    if (info.Length >= 1)
                    {
                        foreach (IPAddress ip in info)
                        {
                            if (ip.AddressFamily == AddressFamily.InterNetwork || ip.AddressFamily == AddressFamily.InterNetworkV6)
                                hash.Add(ip);
                        }
                    }
                }
                catch { }
            }

            if (Socket.OSSupportsIPv4)
            {
                ret.IsIPv4Supported = true;
                hash.Add(IPAddress.Any);
                hash.Add(IPAddress.Loopback);
            }
            if (Socket.OSSupportsIPv6)
            {
                ret.IsIPv6Supported = true;
                hash.Add(IPAddress.IPv6Any);
                hash.Add(IPAddress.IPv6Loopback);
            }

            try
            {
                var cmp = new ByteComparer();
                ret.IPAddressList = hash.OrderBy(x => x.AddressFamily)
                    .ThenBy(x => x.GetAddressBytes(), cmp)
                    .ThenBy(x => (x.AddressFamily == AddressFamily.InterNetworkV6 ? x.ScopeId : 0))
                    .ToList();
            }
            catch { }

            return ret;
        }

        public static IPAddress GetLocalIPForDestinationHost(IPAddress dest)
        {
            try
            {
                using (Socket sock = new Socket(dest.AddressFamily, SocketType.Dgram, ProtocolType.IP))
                {
                    sock.Connect(dest, 65530);
                    IPEndPoint ep = sock.LocalEndPoint as IPEndPoint;
                    return ep.Address;
                }
            }
            catch { }

            using (Socket sock = new Socket(dest.AddressFamily, SocketType.Dgram, ProtocolType.Udp))
            {
                sock.Connect(dest, 65531);
                IPEndPoint ep = sock.LocalEndPoint as IPEndPoint;
                return ep.Address;
            }
        }

        public static async Task<IPAddress> GetLocalIPv4ForInternetAsync()
        {
            try
            {
                return GetLocalIPForDestinationHost(IPAddress.Parse("8.8.8.8"));
            }
            catch { }

            try
            {
                using (Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    var hostent = await Dns.GetHostEntryAsync("www.msftncsi.com");
                    var addr = hostent.AddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork).First();
                    await sock.ConnectAsync(addr, 443);
                    IPEndPoint ep = sock.LocalEndPoint as IPEndPoint;
                    return ep.Address;
                }
            }
            catch { }

            try
            {
                using (Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    var hostent = await Dns.GetHostEntryAsync("www.msftncsi.com");
                    var addr = hostent.AddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork).First();
                    await sock.ConnectAsync(addr, 80);
                    IPEndPoint ep = sock.LocalEndPoint as IPEndPoint;
                    return ep.Address;
                }
            }
            catch { }

            try
            {
                return CurrentInfo.IPAddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork)
                    .Where(x => IPAddress.IsLoopback(x) == false).Where(x => x != IPAddress.Any).First();
            }
            catch { }

            return IPAddress.Any;
        }
    }

    public abstract class BackgroundStateData : IEquatable<BackgroundStateData>
    {
        static long GlobalVersionSeed = 0;
        public long Version { get; }

        public BackgroundStateData()
        {
            Version = Interlocked.Increment(ref GlobalVersionSeed);
        }

        public abstract bool Equals(BackgroundStateData other);
    }

    public class BackgroundState<TData>
        where TData : BackgroundStateData
    {
        private BackgroundState() { }

        public static TData State
        {
            get
            {
                return null;
            }
        }
    }

    public enum IPVersion
    {
        IPv4 = 0,
        IPv6 = 1,
    }

    public enum ListenStatus
    {
        Trying,
        Listening,
        Stopped,
    }

    public sealed class TcpListenManager : IDisposable
    {
        public class Listener
        {
            public IPVersion IPVersion { get; }
            public IPAddress IPAddress { get; }
            public int Port { get; }

            public ListenStatus Status { get; internal set; }
            public Exception LastError { get; internal set; }

            internal Task InternalTask { get; }

            internal CancellationTokenSource InternalSelfCancelSource { get; }
            internal CancellationToken InternalSelfCancelToken { get => InternalSelfCancelSource.Token; }

            public TcpListenManager Manager { get; }

            public const long RetryIntervalStandard = 1 * 512;
            public const long RetryIntervalMax = 60 * 1000;

            internal Listener(TcpListenManager manager, IPVersion ver, IPAddress addr, int port)
            {
                Manager = manager;
                IPVersion = ver;
                IPAddress = addr;
                Port = port;
                LastError = null;
                Status = ListenStatus.Trying;
                InternalSelfCancelSource = new CancellationTokenSource();

                InternalTask = ListenLoop();
            }

            static internal string MakeHashKey(IPVersion ip_ver, IPAddress ip_address, int port)
            {
                return $"{port} / {ip_address} / {ip_address.AddressFamily} / {ip_ver}";
            }

            async Task ListenLoop()
            {
                AsyncAutoResetEvent network_changed_event = new AsyncAutoResetEvent();
                HostNetworkInfoManager.RegisterNotificationEvent(network_changed_event);

                Status = ListenStatus.Trying;

                int num_retry = 0;
                long last_network_info_ver = HostNetworkInfo.CurrentInfo.Version;

                try
                {
                    while (InternalSelfCancelToken.IsCancellationRequested == false)
                    {
                        Status = ListenStatus.Trying;
                        InternalSelfCancelToken.ThrowIfCancellationRequested();

                        int sleep_delay = (int)Math.Min(RetryIntervalStandard * num_retry, RetryIntervalMax);
                        if (sleep_delay >= 1)
                            sleep_delay = WebSocketHelper.RandSInt31() % sleep_delay;
                        await WebSocketHelper.WaitObjectsAsync(timeout: sleep_delay,
                            cancels: new CancellationToken[] { InternalSelfCancelToken },
                            auto_events: new AsyncAutoResetEvent[] { network_changed_event } );
                        num_retry++;

                        long network_info_ver = HostNetworkInfo.CurrentInfo.Version;
                        if (last_network_info_ver != network_info_ver)
                        {
                            last_network_info_ver = network_info_ver;
                            num_retry = 0;
                        }

                        InternalSelfCancelToken.ThrowIfCancellationRequested();

                        try
                        {
                            TcpListener listener = new TcpListener(IPAddress, Port);
                            listener.ExclusiveAddressUse = true;
                            listener.Start();

                            var reg = InternalSelfCancelToken.Register(() =>
                            {
                                try { listener.Stop(); } catch { };
                            });

                            try
                            {
                                Status = ListenStatus.Listening;

                                try
                                {
                                    while (true)
                                    {
                                        InternalSelfCancelToken.ThrowIfCancellationRequested();

                                        var socket = await listener.AcceptSocketAsync();

                                        Manager.SocketAccepted(this, socket);
                                    }
                                }
                                finally
                                {
                                    try { listener.Stop(); } catch { };
                                }
                            }
                            finally
                            {
                                reg.DisposeSafe();
                            }
                        }
                        catch (Exception ex)
                        {
                            LastError = ex;
                        }
                    }
                }
                finally
                {
                    HostNetworkInfoManager.UnregisterNotificationEvent(network_changed_event);
                    Status = ListenStatus.Stopped;
                }
            }

            internal async Task InternalStopAsync()
            {
                await InternalSelfCancelSource.TryCancelAsync();
                try
                {
                    await InternalTask;
                }
                catch { }
            }
        }

        readonly object LockObj = new object();

        readonly Dictionary<string, Listener> List = new Dictionary<string, Listener>();

        readonly Dictionary<Task, Socket> RunningAcceptedTasks = new Dictionary<Task, Socket>();

        readonly CancellationTokenSource CancelSource = new CancellationTokenSource();

        Func<TcpListenManager, Listener, Socket, Task> AcceptedProc { get; }

        public int CurrentConnections
        {
            get
            {
                lock (RunningAcceptedTasks)
                    return RunningAcceptedTasks.Count;
            }
        }

        public TcpListenManager(Func<TcpListenManager, Listener, Socket, Task> accepted_proc)
        {
            AcceptedProc = accepted_proc;
        }

        public Listener Add(int port, IPVersion? ip_ver = null, IPAddress addr = null)
        {
            if (addr == null)
                addr = ((ip_ver ?? IPVersion.IPv4) == IPVersion.IPv4) ? IPAddress.Any : IPAddress.IPv6Any;
            if (ip_ver == null)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                    ip_ver = IPVersion.IPv4;
                else if (addr.AddressFamily == AddressFamily.InterNetworkV6)
                    ip_ver = IPVersion.IPv6;
                else
                    throw new ArgumentException("Unsupported AddressFamily.");
            }
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException("Port number is out of range.");

            lock (LockObj)
            {
                if (dispose_flag.IsSet) throw new ObjectDisposedException("TcpListenManager");

                var s = Search(Listener.MakeHashKey((IPVersion)ip_ver, addr, port));
                if (s != null)
                    return s;
                s = new Listener(this, (IPVersion)ip_ver, addr, port);
                List.Add(Listener.MakeHashKey((IPVersion)ip_ver, addr, port), s);
                return s;
            }
        }

        public async Task<bool> DeleteAsync(Listener listener)
        {
            Listener s;
            lock (LockObj)
            {
                string hash_key = Listener.MakeHashKey(listener.IPVersion, listener.IPAddress, listener.Port);
                s = Search(hash_key);
                if (s == null)
                    return false;
                List.Remove(hash_key);
            }
            await s.InternalStopAsync();
            return true;
        }

        Listener Search(string hash_key)
        {
            if (List.TryGetValue(hash_key, out Listener ret) == false)
                return null;
            return ret;
        }

        private void SocketAccepted(Listener listener, Socket s)
        {
            try
            {
                Task t = AcceptedProc(this, listener, s);

                if (t.IsCompleted)
                {
                    s.DisposeSafe();
                }
                else
                {
                    lock (LockObj)
                        RunningAcceptedTasks.Add(t, s);
                    t.ContinueWith(x =>
                    {
                        s.DisposeSafe();
                        lock (LockObj)
                            RunningAcceptedTasks.Remove(t);
                    });
                }
            }
            catch { }
        }

        public Listener[] Listeners
        {
            get
            {
                lock (LockObj)
                    return List.Values.ToArray();
            }
        }

        bool disposed = false;
        Once dispose_flag;
        public void Dispose()
        {
            if (dispose_flag.IsFirstCall())
            {
                List<Listener> o = new List<Listener>();
                lock (LockObj)
                {
                    List.Values.ToList().ForEach(x => o.Add(x));
                    List.Clear();
                }
                foreach (Listener s in o)
                    s.InternalStopAsync().Wait();

                lock (LockObj)
                {
                    foreach (var v in RunningAcceptedTasks)
                    {
                        v.Value.DisposeSafe();
                        try { v.Key.Wait(); } catch { }
                    }
                    RunningAcceptedTasks.Clear();
                }

                Debug.Assert(CurrentConnections == 0);
                disposed = true;
            }
        }

        public bool Disposed => disposed;
    }

    public class DisconnectedException : Exception { }
    public class FastBufferDisconnectedException : DisconnectedException { }
    public class SocketDisconnectedException : DisconnectedException { }

    public delegate void FastEventCallback<TCaller, TEventType>(TCaller buffer, TEventType type, object user_state);

    public class FastEvent<TCaller, TEventType>
    {
        public FastEventCallback<TCaller, TEventType> Proc { get; }
        public object UserState { get; }

        public FastEvent(FastEventCallback<TCaller, TEventType> proc, object user_state)
        {
            this.Proc = proc;
            this.UserState = user_state;
        }

        public void CallSafe(TCaller buffer, TEventType type)
        {
            try
            {
                this.Proc(buffer, type, UserState);
            }
            catch { }
        }
    }

    public class FastEventListenerList<TCaller, TEventType>
    {
        FastReadList<FastEvent<TCaller, TEventType>> ListenerList;

        public int Register(FastEventCallback<TCaller, TEventType> proc, object user_state = null)
        {
            if (proc == null) return 0;
            return ListenerList.Add(new FastEvent<TCaller, TEventType>(proc, user_state));
        }

        public bool Unregister(int id)
        {
            return ListenerList.Delete(id);
        }

        public void Fire(TCaller buffer, TEventType type)
        {
            var list = ListenerList.GetListFast();
            if (list != null)
                foreach (var e in list)
                    e.CallSafe(buffer, type);
        }
    }

    public enum FastBufferCallbackEventType
    {
        Init,
        Written,
        Read,
        Disconnected,
    }

    public interface IFastBufferState
    {
        long Id { get; }

        long PinHead { get; }
        long PinTail { get; }
        long Length { get; }

        SharedExceptionQueue ExceptionQueue { get; }

        object LockObj { get; }

        bool IsReadyToWrite { get; }
        bool IsReadyToRead { get; }
        bool IsEventsEnabled { get; }
        AsyncAutoResetEvent EventWriteReady { get; }
        AsyncAutoResetEvent EventReadReady { get; }

        FastEventListenerList<IFastBufferState, FastBufferCallbackEventType> EventListeners { get; }

        void CompleteRead();
        void CompleteWrite(bool check_disconnect = true);
    }

    public interface IFastBuffer<T> : IFastBufferState
    {
        void Clear();
        void Enqueue(T item);
        void EnqueueAll(Span<T> item_list);
        void EnqueueAllWithLock(Span<T> item_list);
        List<T> Dequeue(long min_read_size, out long total_read_size, bool allow_split_segments_slow = true);
        List<T> DequeueAll(out long total_read_size);
        List<T> DequeueAllWithLock(out long total_read_size);
        long DequeueAllAndEnqueueToOther(IFastBuffer<T> other);
    }

    public readonly struct FastBufferSegment<T>
    {
        public readonly T Item;
        public readonly long Pin;
        public readonly long RelativeOffset;

        public FastBufferSegment(T item, long pin, long relative_offset)
        {
            Item = item;
            Pin = pin;
            RelativeOffset = relative_offset;
        }
    }

    public class Fifo<T>
    {
        public T[] PhysicalData { get; private set; }
        public int Size { get; private set; }
        public int Position { get; private set; }
        public int PhysicalSize { get => PhysicalData.Length; }

        public const int FifoInitSize = 32;
        public const int FifoReAllocSize = 1024;
        public const int FifoReAllocSizeSmall = 1024;

        public int ReAllocMemSize { get; }

        public Fifo(int realloc_mem_size = FifoReAllocSize)
        {
            ReAllocMemSize = realloc_mem_size;
            Size = Position = 0;
            PhysicalData = new T[FifoInitSize];
        }

        public void Clear()
        {
            Size = Position = 0;
        }

        public void Write(Span<T> data)
        {
            WriteInternal(data, data.Length);
        }

        public void Write(T data)
        {
            WriteInternal(data);
        }

        public void WriteSkip(int length)
        {
            WriteInternal(null, length);
        }

        void WriteInternal(Span<T> src, int size)
        {
            checked
            {
                int old_size, new_size, need_size;

                old_size = Size;
                new_size = old_size + size;
                need_size = Position + new_size;

                bool realloc_flag = false;
                int new_physical_size = PhysicalData.Length;
                while (need_size > new_physical_size)
                {
                    new_physical_size = Math.Max(new_physical_size, FifoInitSize) * 3;
                    realloc_flag = true;
                }

                if (realloc_flag)
                    PhysicalData = MemoryHelper.ReAlloc(PhysicalData, new_physical_size);

                if (src != null)
                    src.CopyTo(PhysicalData.AsSpan().Slice(Position + old_size));

                Size = new_size;
            }
        }

        void WriteInternal(T src)
        {
            checked
            {
                int old_size, new_size, need_size;

                old_size = Size;
                new_size = old_size + 1;
                need_size = Position + new_size;

                bool realloc_flag = false;
                int new_physical_size = PhysicalData.Length;
                while (need_size > new_physical_size)
                {
                    new_physical_size = Math.Max(new_physical_size, FifoInitSize) * 3;
                    realloc_flag = true;
                }

                if (realloc_flag)
                    PhysicalData = MemoryHelper.ReAlloc(PhysicalData, new_physical_size);

                if (src != null)
                    PhysicalData[Position + old_size] = src;

                Size = new_size;
            }
        }


        public int Read(Span<T> dest)
        {
            return ReadInternal(dest, dest.Length);
        }

        public T[] Read(int size)
        {
            int read_size = Math.Min(this.Size, size);
            T[] ret = new T[read_size];
            Read(ret);
            return ret;
        }

        public T[] Read() => Read(this.Size);

        int ReadInternal(Span<T> dest, int size)
        {
            checked
            {
                int read_size;

                read_size = Math.Min(size, Size);
                if (read_size == 0)
                {
                    return 0;
                }
                if (dest != null)
                {
                    PhysicalData.AsSpan(this.Position, size).CopyTo(dest);
                }
                Position += read_size;
                Size -= read_size;

                if (Size == 0)
                {
                    Position = 0;
                }

                if (this.Position >= FifoInitSize &&
                    this.PhysicalData.Length >= this.ReAllocMemSize &&
                    (this.PhysicalData.Length / 2) > this.Size)
                {
                    int new_physical_size;

                    new_physical_size = Math.Max(this.PhysicalData.Length / 2, FifoInitSize);

                    T[] new_p = new T[new_physical_size];
                    this.PhysicalData.AsSpan(this.Position, this.Size).CopyTo(new_p);
                    this.PhysicalData = new_p;

                    this.Position = 0;
                }

                return read_size;
            }
        }

        public Span<T> Span { get => this.PhysicalData.AsSpan(this.Position, this.Size); }
    }

    static internal class FastBufferGlobalIdCounter
    {
        static long id = 0;
        public static long NewId() => Interlocked.Increment(ref id);
    }

    public class FastStreamBuffer<T> : IFastBuffer<Memory<T>>
    {
        FastLinkedList<Memory<T>> List = new FastLinkedList<Memory<T>>();
        public long PinHead { get; private set; } = 0;
        public long PinTail { get; private set; } = 0;
        public long Length { get { long ret = checked(PinTail - PinHead); Debug.Assert(ret >= 0); return ret; } }
        public long Threshold { get; set; }
        public long Id { get; }

        public FastEventListenerList<IFastBufferState, FastBufferCallbackEventType> EventListeners { get; }
            = new FastEventListenerList<IFastBufferState, FastBufferCallbackEventType>();

        public bool IsReadyToWrite
        {
            get
            {
                if (IsDisconnected) return true;
                if (Length <= Threshold) return true;
                CompleteWrite(false);
                return false;
            }
        }

        public bool IsReadyToRead
        {
            get
            {
                if (IsDisconnected) return true;
                if (Length >= 1) return true;
                CompleteRead();
                return false;
            }
        }
        public bool IsEventsEnabled { get; }

        Once InternalDisconnectedFlag;
        public bool IsDisconnected { get => InternalDisconnectedFlag.IsSet; }

        public AsyncAutoResetEvent EventWriteReady { get; } = null;
        public AsyncAutoResetEvent EventReadReady { get; } = null;

        public const long DefaultThreshold = 524288;

        public List<Action> OnDisconnected { get; } = new List<Action>();

        public object LockObj { get; } = new object();

        public SharedExceptionQueue ExceptionQueue { get; } = new SharedExceptionQueue();

        public FastStreamBuffer(bool enable_events = false, long? threshold_length = null)
        {
            if (threshold_length < 0) throw new ArgumentOutOfRangeException("threshold_length < 0");

            Threshold = threshold_length ?? DefaultThreshold;
            IsEventsEnabled = enable_events;
            if (IsEventsEnabled)
            {
                EventWriteReady = new AsyncAutoResetEvent();
                EventReadReady = new AsyncAutoResetEvent();
            }

            Id = FastBufferGlobalIdCounter.NewId();

            EventListeners.Fire(this, FastBufferCallbackEventType.Init);
        }

        bool LastReadyToWrite = false;

        public void CheckDisconnected()
        {
            if (IsDisconnected) ExceptionQueue.Raise(new FastBufferDisconnectedException());
        }

        public void Disconnect()
        {
            if (InternalDisconnectedFlag.IsFirstCall())
            {
                foreach (var ev in OnDisconnected)
                {
                    try
                    {
                        ev();
                    }
                    catch { }
                }
                EventReadReady.Set();
                EventWriteReady.Set();

                EventListeners.Fire(this, FastBufferCallbackEventType.Disconnected);
            }
        }

        public void CompleteRead()
        {
            if (IsEventsEnabled)
            {
                bool set_flag = false;
                lock (LockObj)
                {
                    bool current = IsReadyToWrite;
                    if (current != LastReadyToWrite)
                    {
                        LastReadyToWrite = current;
                        if (current)
                        {
                            set_flag = true;
                        }
                    }
                }
                if (set_flag)
                {
                    EventWriteReady.Set();
                }
            }
        }

        long LastTailPin = long.MinValue;

        public void CompleteWrite(bool check_disconnect = true)
        {
            if (IsEventsEnabled)
            {
                bool set_flag = false;
                lock (LockObj)
                {
                    long current = PinTail;
                    if (LastTailPin != current)
                    {
                        LastTailPin = current;
                        set_flag = true;
                    }
                }
                if (set_flag)
                {
                    EventReadReady.Set();
                }
            }
            if (check_disconnect)
                CheckDisconnected();
        }

        public void Clear()
        {
            checked
            {
                List.Clear();
                PinTail = PinHead;
            }
        }

        public void InsertBefore(Memory<T> item)
        {
            CheckDisconnected();
            checked
            {
                if (item.IsEmpty) return;
                List.AddFirst(item);
                PinHead -= item.Length;
            }
        }

        public void InsertHead(Memory<T> item)
        {
            CheckDisconnected();
            checked
            {
                if (item.IsEmpty) return;
                List.AddFirst(item);
                PinTail += item.Length;
            }
        }

        public void InsertTail(Memory<T> item)
        {
            CheckDisconnected();
            checked
            {
                if (item.IsEmpty) return;
                List.AddLast(item);
                PinTail += item.Length;
            }
        }

        public void Insert(long pin, Memory<T> item, bool append_if_overrun = false)
        {
            CheckDisconnected();
            checked
            {
                if (item.IsEmpty) return;

                if (List.First == null)
                {
                    InsertHead(item);
                    return;
                }

                if (append_if_overrun)
                {
                    if (pin < PinHead)
                        InsertBefore(new T[PinHead - pin]);

                    if (pin > PinTail)
                        InsertTail(new T[pin - PinTail]);
                }
                else
                {
                    if (List.First == null) throw new ArgumentOutOfRangeException("Buffer is empty.");
                    if (pin < PinHead) throw new ArgumentOutOfRangeException("pin_start < PinHead");
                    if (pin > PinTail) throw new ArgumentOutOfRangeException("pin > PinTail");
                }

                var node = GetNodeWithPin(pin, out int offset_in_segment, out _);
                Debug.Assert(node != null);
                if (offset_in_segment == 0)
                {
                    var new_node = List.AddBefore(node, item);
                    PinTail += item.Length;
                }
                else if (node.Value.Length == offset_in_segment)
                {
                    var new_node = List.AddAfter(node, item);
                    PinTail += item.Length;
                }
                else
                {
                    Memory<T> slice_before = node.Value.Slice(0, offset_in_segment);
                    Memory<T> slice_after = node.Value.Slice(offset_in_segment);

                    node.Value = slice_before;
                    var new_node = List.AddAfter(node, item);
                    List.AddAfter(new_node, slice_after);
                    PinTail += item.Length;
                }
            }
        }

        FastLinkedListNode<Memory<T>> GetNodeWithPin(long pin, out int offset_in_segment, out long node_pin)
        {
            checked
            {
                offset_in_segment = 0;
                node_pin = 0;
                if (List.First == null)
                {
                    if (pin != PinHead) throw new ArgumentOutOfRangeException("List.First == null, but pin != PinHead");
                    return null;
                }
                if (pin < PinHead) throw new ArgumentOutOfRangeException("pin < PinHead");
                if (pin == PinHead)
                {
                    node_pin = pin;
                    return List.First;
                }
                if (pin > PinTail) throw new ArgumentOutOfRangeException("pin > PinTail");
                if (pin == PinTail)
                {
                    var last = List.Last;
                    if (last != null)
                    {
                        offset_in_segment = last.Value.Length;
                        node_pin = PinTail - last.Value.Length;
                    }
                    else
                    {
                        node_pin = PinTail;
                    }
                    return last;
                }
                long current_pin = PinHead;
                FastLinkedListNode<Memory<T>> node = List.First;
                while (node != null)
                {
                    if (pin >= current_pin && pin < (current_pin + node.Value.Length))
                    {
                        offset_in_segment = (int)(pin - current_pin);
                        node_pin = current_pin;
                        return node;
                    }
                    current_pin += node.Value.Length;
                    node = node.Next;
                }
                throw new ApplicationException("GetNodeWithPin: Bug!");
            }
        }

        void GetOverlappedNodes(long pin_start, long pin_end,
            out FastLinkedListNode<Memory<T>> first_node, out int first_node_offset_in_segment, out long first_node_pin,
            out FastLinkedListNode<Memory<T>> last_node, out int last_node_offset_in_segment, out long last_node_pin,
            out int node_counts, out int lack_remain_length)
        {
            checked
            {
                if (pin_start > pin_end) throw new ArgumentOutOfRangeException("pin_start > pin_end");

                first_node = GetNodeWithPin(pin_start, out first_node_offset_in_segment, out first_node_pin);

                if (pin_end > PinTail)
                {
                    lack_remain_length = (int)checked(pin_end - PinTail);
                    pin_end = PinTail;
                }

                FastLinkedListNode<Memory<T>> node = first_node;
                long current_pin = pin_start - first_node_offset_in_segment;
                node_counts = 0;
                while (true)
                {
                    Debug.Assert(node != null, "node == null");

                    node_counts++;
                    if (pin_end <= (current_pin + node.Value.Length))
                    {
                        last_node_offset_in_segment = (int)(pin_end - current_pin);
                        last_node = node;
                        lack_remain_length = 0;
                        last_node_pin = current_pin;

                        Debug.Assert(first_node_offset_in_segment != first_node.Value.Length);
                        Debug.Assert(last_node_offset_in_segment != 0);

                        return;
                    }
                    current_pin += node.Value.Length;
                    node = node.Next;
                }
            }
        }

        public FastBufferSegment<Memory<T>>[] GetSegmentsFast(long pin, long size, out long read_size, bool allow_partial = false)
        {
            checked
            {
                if (size < 0) throw new ArgumentOutOfRangeException("read_size < 0");
                if (size == 0)
                {
                    read_size = 0;
                    return new FastBufferSegment<Memory<T>>[0];
                }
                if (pin > PinTail)
                {
                    throw new ArgumentOutOfRangeException("pin > PinTail");
                }
                if ((pin + size) > PinTail)
                {
                    if (allow_partial == false)
                        throw new ArgumentOutOfRangeException("(pin + size) > PinTail");
                    size = PinTail - pin;
                }

                FastBufferSegment<Memory<T>>[] ret = GetUncontiguousSegments(pin, pin + size, false);
                read_size = size;
                return ret;
            }
        }

        public FastBufferSegment<Memory<T>>[] ReadForwardFast(ref long pin, long size, out long read_size, bool allow_partial = false)
        {
            checked
            {
                FastBufferSegment<Memory<T>>[] ret = GetSegmentsFast(pin, size, out read_size, allow_partial);
                pin += read_size;
                return ret;
            }
        }

        public Memory<T> GetContiguous(long pin, long size, bool allow_partial = false)
        {
            checked
            {
                if (size < 0) throw new ArgumentOutOfRangeException("read_size < 0");
                if (size == 0)
                {
                    return new Memory<T>();
                }
                if (pin > PinTail)
                {
                    throw new ArgumentOutOfRangeException("pin > PinTail");
                }
                if ((pin + size) > PinTail)
                {
                    if (allow_partial == false)
                        throw new ArgumentOutOfRangeException("(pin + size) > PinTail");
                    size = PinTail - pin;
                }
                Memory<T> ret = GetContiguousMemory(pin, pin + size, false, false);
                return ret;
            }
        }

        public Memory<T> ReadForwardContiguous(ref long pin, long size, bool allow_partial = false)
        {
            checked
            {
                Memory<T> ret = GetContiguous(pin, size, allow_partial);
                pin += ret.Length;
                return ret;
            }
        }

        public Memory<T> PutContiguous(long pin, long size, bool append_if_overrun = false)
        {
            checked
            {
                if (size < 0) throw new ArgumentOutOfRangeException("read_size < 0");
                if (size == 0)
                {
                    return new Memory<T>();
                }
                Memory<T> ret = GetContiguousMemory(pin, pin + size, append_if_overrun, false);
                return ret;
            }
        }

        public Memory<T> WriteForwardContiguous(ref long pin, long size, bool append_if_overrun = false)
        {
            checked
            {
                Memory<T> ret = PutContiguous(pin, size, append_if_overrun);
                pin += ret.Length;
                return ret;
            }
        }

        public void Enqueue(Memory<T> item)
        {
            CheckDisconnected();
            if (item.Length == 0) return;
            InsertTail(item);
            EventListeners.Fire(this, FastBufferCallbackEventType.Written);
        }

        public void EnqueueAllWithLock(Span<Memory<T>> item_list)
        {
            lock (LockObj)
                EnqueueAll(item_list);
        }

        public void EnqueueAll(Span<Memory<T>> item_list)
        {
            CheckDisconnected();
            checked
            {
                int num = 0;
                foreach (Memory<T> t in item_list)
                {
                    if (t.Length != 0)
                    {
                        List.AddLast(t);
                        PinTail += t.Length;
                        num++;
                    }
                }
                if (num >= 1)
                    EventListeners.Fire(this, FastBufferCallbackEventType.Written);
            }
        }

        public int DequeueContiguousSlow(Memory<T> dest, int size = int.MaxValue)
        {
            if (IsDisconnected && this.Length == 0) CheckDisconnected();
            checked
            {
                if (size < 0) throw new ArgumentOutOfRangeException("size < 0");
                size = Math.Min(size, dest.Length);
                Debug.Assert(size >= 0);
                if (size == 0) return 0;
                var memarray = Dequeue(size, out long total_size, true);
                Debug.Assert(total_size <= size);
                if (total_size > int.MaxValue) throw new IndexOutOfRangeException("total_size > int.MaxValue");
                if (dest.Length < total_size) throw new ArgumentOutOfRangeException("dest.Length < total_size");
                int pos = 0;
                foreach (var mem in memarray)
                {
                    mem.CopyTo(dest.Slice(pos, mem.Length));
                    pos += mem.Length;
                }
                Debug.Assert(pos == total_size);
                EventListeners.Fire(this, FastBufferCallbackEventType.Read);
                return (int)total_size;
            }
        }

        public Memory<T> DequeueContiguousSlow(int size = int.MaxValue)
        {
            if (IsDisconnected && this.Length == 0) CheckDisconnected();
            checked
            {
                if (size < 0) throw new ArgumentOutOfRangeException("size < 0");
                if (size == 0) return Memory<T>.Empty;
                int read_size = (int)Math.Min(size, Length);
                Memory<T> ret = new T[read_size];
                int r = DequeueContiguousSlow(ret, read_size);
                Debug.Assert(r <= read_size);
                ret = ret.Slice(0, r);
                EventListeners.Fire(this, FastBufferCallbackEventType.Read);
                return ret;
            }
        }

        public List<Memory<T>> DequeueAllWithLock(out long total_read_size)
        {
            lock (this.LockObj)
                return DequeueAll(out total_read_size);
        }
        public List<Memory<T>> DequeueAll(out long total_read_size) => Dequeue(long.MaxValue, out total_read_size);
        public List<Memory<T>> Dequeue(long min_read_size, out long total_read_size, bool allow_split_segments_slow = true)
        {
            if (IsDisconnected && this.Length == 0) CheckDisconnected();
            checked
            {
                if (min_read_size < 1) throw new ArgumentOutOfRangeException("size < 1");

                total_read_size = 0;
                if (List.First == null)
                {
                    return new List<Memory<T>>();
                }

                FastLinkedListNode<Memory<T>> node = List.First;
                List<Memory<T>> ret = new List<Memory<T>>();
                while (true)
                {
                    if ((total_read_size + node.Value.Length) >= min_read_size)
                    {
                        if (allow_split_segments_slow && (total_read_size + node.Value.Length) > min_read_size)
                        {
                            int last_segment_read_size = (int)(min_read_size - total_read_size);
                            Debug.Assert(last_segment_read_size <= node.Value.Length);
                            ret.Add(node.Value.Slice(0, last_segment_read_size));
                            if (last_segment_read_size == node.Value.Length)
                                List.Remove(node);
                            else
                                node.Value = node.Value.Slice(last_segment_read_size);
                            total_read_size += last_segment_read_size;
                            PinHead += total_read_size;
                            Debug.Assert(min_read_size >= total_read_size);
                            EventListeners.Fire(this, FastBufferCallbackEventType.Read);
                            return ret;
                        }
                        else
                        {
                            ret.Add(node.Value);
                            total_read_size += node.Value.Length;
                            List.Remove(node);
                            PinHead += total_read_size;
                            Debug.Assert(min_read_size <= total_read_size);
                            EventListeners.Fire(this, FastBufferCallbackEventType.Read);
                            return ret;
                        }
                    }
                    else
                    {
                        ret.Add(node.Value);
                        total_read_size += node.Value.Length;

                        FastLinkedListNode<Memory<T>> delete_node = node;
                        node = node.Next;

                        List.Remove(delete_node);

                        if (node == null)
                        {
                            PinHead += total_read_size;
                            EventListeners.Fire(this, FastBufferCallbackEventType.Read);
                            return ret;
                        }
                    }
                }
            }
        }

        public long DequeueAllAndEnqueueToOther(IFastBuffer<Memory<T>> other) => DequeueAllAndEnqueueToOther((FastStreamBuffer<T>)other);

        public long DequeueAllAndEnqueueToOther(FastStreamBuffer<T> other)
        {
            if (IsDisconnected && this.Length == 0) CheckDisconnected();
            other.CheckDisconnected();
            checked
            {
                if (this == other) throw new ArgumentException("this == other");

                if (this.Length == 0)
                {
                    Debug.Assert(this.List.Count == 0);
                    return 0;
                }

                if (other.Length == 0)
                {
                    long length = this.Length;
                    Debug.Assert(other.List.Count == 0);
                    other.List = this.List;
                    this.List = new FastLinkedList<Memory<T>>();
                    this.PinHead = this.PinTail;
                    other.PinTail += length;
                    EventListeners.Fire(this, FastBufferCallbackEventType.Read);
                    other.EventListeners.Fire(other, FastBufferCallbackEventType.Written);
                    return length;
                }
                else
                {
                    long length = this.Length;
                    var chain_first = this.List.First;
                    var chain_last = this.List.Last;
                    other.List.AddLast(this.List.First, this.List.Last, this.List.Count);
                    this.List.Clear();
                    this.PinHead = this.PinTail;
                    other.PinTail += length;
                    EventListeners.Fire(this, FastBufferCallbackEventType.Read);
                    other.EventListeners.Fire(other, FastBufferCallbackEventType.Written);
                    return length;
                }
            }
        }

        FastBufferSegment<Memory<T>>[] GetUncontiguousSegments(long pin_start, long pin_end, bool append_if_overrun)
        {
            checked
            {
                if (pin_start == pin_end) return new FastBufferSegment<Memory<T>>[0];
                if (pin_start > pin_end) throw new ArgumentOutOfRangeException("pin_start > pin_end");

                if (append_if_overrun)
                {
                    if (List.First == null)
                    {
                        InsertHead(new T[pin_end - pin_start]);
                        PinHead = pin_start;
                        PinTail = pin_end;
                    }

                    if (pin_start < PinHead)
                        InsertBefore(new T[PinHead - pin_start]);

                    if (pin_end > PinTail)
                        InsertTail(new T[pin_end - PinTail]);
                }
                else
                {
                    if (List.First == null) throw new ArgumentOutOfRangeException("Buffer is empty.");
                    if (pin_start < PinHead) throw new ArgumentOutOfRangeException("pin_start < PinHead");
                    if (pin_end > PinTail) throw new ArgumentOutOfRangeException("pin_end > PinTail");
                }

                GetOverlappedNodes(pin_start, pin_end,
                    out FastLinkedListNode<Memory<T>> first_node, out int first_node_offset_in_segment, out long first_node_pin,
                    out FastLinkedListNode<Memory<T>> last_node, out int last_node_offset_in_segment, out long last_node_pin,
                    out int node_counts, out int lack_remain_length);

                Debug.Assert(lack_remain_length == 0, "lack_remain_length != 0");

                if (first_node == last_node)
                    return new FastBufferSegment<Memory<T>>[1]{ new FastBufferSegment<Memory<T>>(
                    first_node.Value.Slice(first_node_offset_in_segment, last_node_offset_in_segment - first_node_offset_in_segment), pin_start, 0) };

                FastBufferSegment<Memory<T>>[] ret = new FastBufferSegment<Memory<T>>[node_counts];

                FastLinkedListNode<Memory<T>> prev_node = first_node.Previous;
                FastLinkedListNode<Memory<T>> next_node = last_node.Next;

                FastLinkedListNode<Memory<T>> node = first_node;
                int count = 0;
                long current_offset = 0;

                while (true)
                {
                    Debug.Assert(node != null, "node == null");

                    int slice_start = (node == first_node) ? first_node_offset_in_segment : 0;
                    int slice_length = (node == last_node) ? last_node_offset_in_segment : node.Value.Length - slice_start;

                    ret[count] = new FastBufferSegment<Memory<T>>(node.Value.Slice(slice_start, slice_length), current_offset + pin_start, current_offset);
                    count++;

                    Debug.Assert(count <= node_counts, "count > node_counts");

                    current_offset += slice_length;

                    if (node == last_node)
                    {
                        Debug.Assert(count == ret.Length, "count != ret.Length");
                        break;
                    }

                    node = node.Next;
                }

                return ret;
            }
        }

        public void Remove(long pin_start, long length)
        {
            checked
            {
                if (length == 0) return;
                if (length < 0) throw new ArgumentOutOfRangeException("length < 0");
                long pin_end = checked(pin_start + length);
                if (List.First == null) throw new ArgumentOutOfRangeException("Buffer is empty.");
                if (pin_start < PinHead) throw new ArgumentOutOfRangeException("pin_start < PinHead");
                if (pin_end > PinTail) throw new ArgumentOutOfRangeException("pin_end > PinTail");

                GetOverlappedNodes(pin_start, pin_end,
                    out FastLinkedListNode<Memory<T>> first_node, out int first_node_offset_in_segment, out long first_node_pin,
                    out FastLinkedListNode<Memory<T>> last_node, out int last_node_offset_in_segment, out long last_node_pin,
                    out int node_counts, out int lack_remain_length);

                Debug.Assert(lack_remain_length == 0, "lack_remain_length != 0");

                if (first_node == last_node)
                {
                    Debug.Assert(first_node_offset_in_segment < last_node_offset_in_segment);
                    if (first_node_offset_in_segment == 0 && last_node_offset_in_segment == last_node.Value.Length)
                    {
                        Debug.Assert(first_node.Value.Length == length, "first_node.Value.Length != length");
                        List.Remove(first_node);
                        PinTail -= length;
                        return;
                    }
                    else
                    {
                        Debug.Assert((last_node_offset_in_segment - first_node_offset_in_segment) == length);
                        Memory<T> slice1 = first_node.Value.Slice(0, first_node_offset_in_segment);
                        Memory<T> slice2 = first_node.Value.Slice(last_node_offset_in_segment);
                        Debug.Assert(slice1.Length != 0 || slice2.Length != 0);
                        if (slice1.Length == 0)
                        {
                            first_node.Value = slice2;
                        }
                        else if (slice2.Length == 0)
                        {
                            first_node.Value = slice1;
                        }
                        else
                        {
                            first_node.Value = slice1;
                            List.AddAfter(first_node, slice2);
                        }
                        PinTail -= length;
                        return;
                    }
                }
                else
                {
                    first_node.Value = first_node.Value.Slice(0, first_node_offset_in_segment);
                    last_node.Value = last_node.Value.Slice(last_node_offset_in_segment);

                    var node = first_node.Next;
                    while (node != last_node)
                    {
                        var node_to_delete = node;

                        Debug.Assert(node.Next != null);
                        node = node.Next;

                        List.Remove(node_to_delete);
                    }

                    if (last_node.Value.Length == 0)
                        List.Remove(last_node);

                    if (first_node.Value.Length == 0)
                        List.Remove(first_node);

                    PinTail -= length;
                    return;
                }
            }
        }

        public T[] ToArray() => GetContiguousMemory(PinHead, PinTail, false, true).ToArray();

        public T[] ItemsSlow { get => ToArray(); }

        Memory<T> GetContiguousMemory(long pin_start, long pin_end, bool append_if_overrun, bool no_replace)
        {
            checked
            {
                if (pin_start == pin_end) return new Memory<T>();
                if (pin_start > pin_end) throw new ArgumentOutOfRangeException("pin_start > pin_end");

                if (append_if_overrun)
                {
                    if (List.First == null)
                    {
                        InsertHead(new T[pin_end - pin_start]);
                        PinHead = pin_start;
                        PinTail = pin_end;
                    }

                    if (pin_start < PinHead)
                        InsertBefore(new T[PinHead - pin_start]);

                    if (pin_end > PinTail)
                        InsertTail(new T[pin_end - PinTail]);
                }
                else
                {
                    if (List.First == null) throw new ArgumentOutOfRangeException("Buffer is empty.");
                    if (pin_start < PinHead) throw new ArgumentOutOfRangeException("pin_start < PinHead");
                    if (pin_end > PinTail) throw new ArgumentOutOfRangeException("pin_end > PinTail");
                }

                GetOverlappedNodes(pin_start, pin_end,
                    out FastLinkedListNode<Memory<T>> first_node, out int first_node_offset_in_segment, out long first_node_pin,
                    out FastLinkedListNode<Memory<T>> last_node, out int last_node_offset_in_segment, out long last_node_pin,
                    out int node_counts, out int lack_remain_length);

                Debug.Assert(lack_remain_length == 0, "lack_remain_length != 0");

                if (first_node == last_node)
                    return first_node.Value.Slice(first_node_offset_in_segment, last_node_offset_in_segment - first_node_offset_in_segment);

                FastLinkedListNode<Memory<T>> prev_node = first_node.Previous;
                FastLinkedListNode<Memory<T>> next_node = last_node.Next;

                Memory<T> new_memory = new T[last_node_pin + last_node.Value.Length - first_node_pin];
                FastLinkedListNode<Memory<T>> node = first_node;
                int current_write_pointer = 0;

                while (true)
                {
                    Debug.Assert(node != null, "node == null");

                    bool finish = false;
                    node.Value.CopyTo(new_memory.Slice(current_write_pointer));

                    if (node == last_node) finish = true;

                    FastLinkedListNode<Memory<T>> node_to_delete = node;
                    current_write_pointer += node.Value.Length;

                    node = node.Next;

                    if (no_replace == false)
                        List.Remove(node_to_delete);

                    if (finish) break;
                }

                if (no_replace == false)
                {
                    if (prev_node != null)
                        List.AddAfter(prev_node, new_memory);
                    else if (next_node != null)
                        List.AddBefore(next_node, new_memory);
                    else
                        List.AddFirst(new_memory);
                }

                var ret = new_memory.Slice(first_node_offset_in_segment, new_memory.Length - (last_node.Value.Length - last_node_offset_in_segment) - first_node_offset_in_segment);
                Debug.Assert(ret.Length == (pin_end - pin_start), "ret.Length");
                return ret;
            }
        }

        public static implicit operator FastStreamBuffer<T>(Memory<T> memory)
        {
            FastStreamBuffer<T> ret = new FastStreamBuffer<T>(false, null);
            ret.Enqueue(memory);
            return ret;
        }

        public static implicit operator FastStreamBuffer<T>(Span<T> span) => span.ToArray().AsMemory();
        public static implicit operator FastStreamBuffer<T>(T[] data) => data.AsMemory();
    }





    public class FastDatagramBuffer<T> : IFastBuffer<T>
    {
        Fifo<T> Fifo = new Fifo<T>();

        public long PinHead { get; private set; } = 0;
        public long PinTail { get; private set; } = 0;
        public long Length { get { long ret = checked(PinTail - PinHead); Debug.Assert(ret >= 0); return ret; } }
        public long Threshold { get; set; }
        public long Id { get; }

        public FastEventListenerList<IFastBufferState, FastBufferCallbackEventType> EventListeners { get; }
            = new FastEventListenerList<IFastBufferState, FastBufferCallbackEventType>();

        public bool IsReadyToWrite
        {
            get
            {
                if (IsDisconnected) return true;
                if (Length <= Threshold) return true;
                CompleteWrite(false);
                return false;
            }
        }

        public bool IsReadyToRead
        {
            get
            {
                if (IsDisconnected) return true;
                if (Length >= 1) return true;
                CompleteRead();
                return false;
            }
        }

        public bool IsEventsEnabled { get; }

        public AsyncAutoResetEvent EventWriteReady { get; } = null;
        public AsyncAutoResetEvent EventReadReady { get; } = null;

        Once InternalDisconnectedFlag;
        public bool IsDisconnected { get => InternalDisconnectedFlag.IsSet; }

        public List<Action> OnDisconnected { get; } = new List<Action>();

        public const long DefaultThreshold = 65536;

        public object LockObj { get; } = new object();

        public SharedExceptionQueue ExceptionQueue { get; } = new SharedExceptionQueue();

        public FastDatagramBuffer(bool enable_events = false, long? threshold_length = null)
        {
            if (threshold_length < 0) throw new ArgumentOutOfRangeException("threshold_length < 0");

            Threshold = threshold_length ?? DefaultThreshold;
            IsEventsEnabled = enable_events;
            if (IsEventsEnabled)
            {
                EventWriteReady = new AsyncAutoResetEvent();
                EventReadReady = new AsyncAutoResetEvent();
            }

            Id = FastBufferGlobalIdCounter.NewId();

            EventListeners.Fire(this, FastBufferCallbackEventType.Init);
        }

        public void CheckDisconnected()
        {
            if (IsDisconnected) ExceptionQueue.Raise(new FastBufferDisconnectedException());
        }

        public void Disconnect()
        {
            if (InternalDisconnectedFlag.IsFirstCall())
            {
                foreach (var ev in OnDisconnected)
                {
                    try
                    {
                        ev();
                    }
                    catch { }
                }
                EventReadReady.Set();
                EventWriteReady.Set();

                EventListeners.Fire(this, FastBufferCallbackEventType.Disconnected);
            }
        }

        bool LastReadyToWrite = false;

        public void CompleteRead()
        {
            if (IsEventsEnabled)
            {
                bool set_flag = false;

                lock (LockObj)
                {
                    bool current = IsReadyToWrite;
                    if (current != LastReadyToWrite)
                    {
                        LastReadyToWrite = current;
                        if (current)
                            set_flag = true;
                    }
                }

                if (set_flag)
                {
                    EventWriteReady.Set();
                }
            }
        }

        long LastTailPin = long.MinValue;

        public void CompleteWrite(bool check_disconnect = true)
        {
            if (IsEventsEnabled)
            {
                bool set_flag = false;

                lock (LockObj)
                {
                    long current = PinTail;
                    if (LastTailPin != current)
                    {
                        LastTailPin = current;
                        set_flag = true;
                    }
                }

                if (set_flag)
                {
                    EventReadReady.Set();
                }
            }
            if (check_disconnect)
                CheckDisconnected();
        }

        public void Clear()
        {
            checked
            {
                Fifo.Clear();
                PinTail = PinHead;
            }
        }

        public void Enqueue(T item)
        {
            CheckDisconnected();
            checked
            {
                Fifo.Write(item);
                PinTail++;
                EventListeners.Fire(this, FastBufferCallbackEventType.Written);
            }
        }

        public void EnqueueAllWithLock(Span<T> item_list)
        {
            lock (LockObj)
                EnqueueAll(item_list);
        }

        public void EnqueueAll(Span<T> item_list)
        {
            CheckDisconnected();
            checked
            {
                Fifo.Write(item_list);
                PinTail += item_list.Length;
                EventListeners.Fire(this, FastBufferCallbackEventType.Written);
            }
        }

        public List<T> Dequeue(long min_read_size, out long total_read_size, bool allow_split_segments_slow = true)
        {
            if (IsDisconnected && this.Length == 0) CheckDisconnected();
            checked
            {
                if (min_read_size < 1) throw new ArgumentOutOfRangeException("size < 1");
                if (min_read_size >= int.MaxValue) min_read_size = int.MaxValue;

                total_read_size = 0;
                if (Fifo.Size == 0)
                {
                    return new List<T>();
                }

                T[] tmp = Fifo.Read((int)min_read_size);

                total_read_size = tmp.Length;
                List<T> ret = new List<T>(tmp);

                PinHead += total_read_size;

                EventListeners.Fire(this, FastBufferCallbackEventType.Read);

                return ret;
            }
        }

        public List<T> DequeueAll(out long total_read_size) => Dequeue(long.MaxValue, out total_read_size);

        public List<T> DequeueAllWithLock(out long total_read_size)
        {
            lock (LockObj)
                return DequeueAll(out total_read_size);
        }

        public long DequeueAllAndEnqueueToOther(IFastBuffer<T> other) => DequeueAllAndEnqueueToOther((FastDatagramBuffer<T>)other);

        public long DequeueAllAndEnqueueToOther(FastDatagramBuffer<T> other)
        {
            if (IsDisconnected && this.Length == 0) CheckDisconnected();
            other.CheckDisconnected();
            checked
            {
                if (this == other) throw new ArgumentException("this == other");

                if (this.Length == 0)
                {
                    Debug.Assert(this.Fifo.Size == 0);
                    return 0;
                }

                if (other.Length == 0)
                {
                    long length = this.Length;
                    Debug.Assert(other.Fifo.Size == 0);
                    other.Fifo = this.Fifo;
                    this.Fifo = new Fifo<T>();
                    this.PinHead = this.PinTail;
                    other.PinTail += length;
                    EventListeners.Fire(this, FastBufferCallbackEventType.Read);
                    other.EventListeners.Fire(other, FastBufferCallbackEventType.Written);
                    return length;
                }
                else
                {
                    long length = this.Length;
                    var data = this.Fifo.Read();
                    other.Fifo.Write(data);
                    this.PinHead = this.PinTail;
                    other.PinTail += length;
                    EventListeners.Fire(this, FastBufferCallbackEventType.Read);
                    other.EventListeners.Fire(other, FastBufferCallbackEventType.Written);
                    return length;
                }
            }
        }


        public T[] ToArray() => Fifo.Span.ToArray();

        public T[] ItemsSlow { get => ToArray(); }
    }

    public class FastStreamFifo : FastStreamBuffer<byte>
    {
        public FastStreamFifo(bool enable_events = false, long? threshold_length = null)
            : base(enable_events, threshold_length) { }
    }

    public class FastDatagramFifo : FastDatagramBuffer<Datagram>
    {
        public FastDatagramFifo(bool enable_events = false, long? threshold_length = null)
            : base(enable_events, threshold_length) { }
    }

    public static class FastPipeHelper
    {
        public static async Task WaitForReadyToWrite(this IFastBufferState writer, CancellationToken cancel, int timeout)
        {
            LocalTimer timer = new LocalTimer();

            timer.AddTimeout(FastPipeGlobalConfig.PollingTimeout);
            long timeout_tick = timer.AddTimeout(timeout);

            while (writer.IsReadyToWrite == false)
            {
                if (FastTick64.Now >= timeout_tick) throw new TimeoutException();
                cancel.ThrowIfCancellationRequested();

                await WebSocketHelper.WaitObjectsAsync(
                    cancels: new CancellationToken[] { cancel },
                    auto_events: new AsyncAutoResetEvent[] { writer.EventWriteReady },
                    timeout: timer.GetNextInterval()
                    );
            }

            cancel.ThrowIfCancellationRequested();
        }

        public static async Task WaitForReadyToRead(this IFastBufferState reader, CancellationToken cancel, int timeout)
        {
            LocalTimer timer = new LocalTimer();

            timer.AddTimeout(FastPipeGlobalConfig.PollingTimeout);
            long timeout_tick = timer.AddTimeout(timeout);

            while (reader.IsReadyToRead == false)
            {
                if (FastTick64.Now >= timeout_tick) throw new TimeoutException();
                cancel.ThrowIfCancellationRequested();

                await WebSocketHelper.WaitObjectsAsync(
                    cancels: new CancellationToken[] { cancel },
                    auto_events: new AsyncAutoResetEvent[] { reader.EventReadReady },
                    timeout: timer.GetNextInterval()
                    );
            }

            cancel.ThrowIfCancellationRequested();
        }
    }

    public static class FastPipeGlobalConfig
    {
        public static int MaxStreamBufferLength = int.MaxValue;
        public static int MaxDatagramQueueLength = 65536;
        public static int MaxPollingTimeout = 256;

        public static void ApplyHeavyLoadServerConfig()
        {
            MaxStreamBufferLength = 65536;
            MaxPollingTimeout = 4321;
            MaxDatagramQueueLength = 1024;
        }

        public static int PollingTimeout
        {
            get
            {
                int v = MaxPollingTimeout / 10;
                if (v != 0)
                    v = WebSocketHelper.RandSInt31() % v;
                return MaxPollingTimeout - v;
            }
        }
    }

    public class FastPipe : IDisposable
    {
        public CancelWatcher CancelWatcher { get; }

        FastStreamFifo StreamAtoB;
        FastStreamFifo StreamBtoA;
        FastDatagramFifo DatagramAtoB;
        FastDatagramFifo DatagramBtoA;

        public SharedExceptionQueue ExceptionQueue { get; } = new SharedExceptionQueue();

        public FastPipeEnd A { get; }
        public FastPipeEnd B { get; }

        public List<Action> OnDisconnected { get; } = new List<Action>();

        Once InternalDisconnectedFlag;
        public bool IsDisconnected { get => InternalDisconnectedFlag.IsSet; }

        public FastPipe(CancellationToken cancel = default(CancellationToken), long? threshold_length_stream = null, long? threshold_length_datagram = null)
        {
            CancelWatcher = new CancelWatcher(cancel);

            if (threshold_length_stream == null) threshold_length_stream = FastPipeGlobalConfig.MaxStreamBufferLength;
            if (threshold_length_datagram == null) threshold_length_datagram = FastPipeGlobalConfig.MaxDatagramQueueLength;

            StreamAtoB = new FastStreamFifo(true, threshold_length_stream);
            StreamBtoA = new FastStreamFifo(true, threshold_length_stream);

            DatagramAtoB = new FastDatagramFifo(true, threshold_length_datagram);
            DatagramBtoA = new FastDatagramFifo(true, threshold_length_datagram);

            StreamAtoB.ExceptionQueue.Encounter(ExceptionQueue);
            StreamBtoA.ExceptionQueue.Encounter(ExceptionQueue);

            DatagramAtoB.ExceptionQueue.Encounter(ExceptionQueue);
            DatagramBtoA.ExceptionQueue.Encounter(ExceptionQueue);

            StreamAtoB.OnDisconnected.Add(() => Disconnect());
            StreamBtoA.OnDisconnected.Add(() => Disconnect());

            DatagramAtoB.OnDisconnected.Add(() => Disconnect());
            DatagramBtoA.OnDisconnected.Add(() => Disconnect());

            A = new FastPipeEnd(this, CancelWatcher, StreamAtoB, StreamBtoA, DatagramAtoB, DatagramBtoA);
            B = new FastPipeEnd(this, CancelWatcher, StreamBtoA, StreamAtoB, DatagramBtoA, DatagramAtoB);

            CancelWatcher.CancelToken.Register(() =>
            {
                Disconnect(new OperationCanceledException());
            });
        }

        public void Disconnect(Exception ex = null)
        {
            if (InternalDisconnectedFlag.IsFirstCall())
            {
                if (ex != null)
                {
                    ExceptionQueue.Add(ex);
                }

                Action[] ev_list;
                lock (OnDisconnected)
                    ev_list = OnDisconnected.ToArray();

                foreach (var ev in ev_list)
                {
                    try
                    {
                        ev();
                    }
                    catch { }
                }

                StreamAtoB.Disconnect();
                StreamBtoA.Disconnect();

                DatagramAtoB.Disconnect();
                DatagramBtoA.Disconnect();
            }
        }

        Once dispose_flag;
        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing)
        {
            if (dispose_flag.IsFirstCall() && disposing)
            {
                Disconnect();
                CancelWatcher.DisposeSafe();
            }
        }
    }

    public class FastPipeEnd
    {
        FastPipe Pipe { get; }

        public CancelWatcher CancelWatcher { get; }
        public FastStreamFifo StreamWriter { get; }
        public FastStreamFifo StreamReader { get; }
        public FastDatagramFifo DatagramWriter { get; }
        public FastDatagramFifo DatagramReader { get; }

        public SharedExceptionQueue ExceptionQueue { get => Pipe.ExceptionQueue; }

        public bool IsDisconnected { get => this.Pipe.IsDisconnected; }
        public void Disconnect() { this.Pipe.Disconnect(); }
        public void AddOnDisconnected(Action action)
        {
            lock (Pipe.OnDisconnected)
                Pipe.OnDisconnected.Add(action);
        }

        internal FastPipeEnd(FastPipe pipe,
            CancelWatcher cancel_watcher,
            FastStreamFifo stream_to_write, FastStreamFifo stream_to_read,
            FastDatagramFifo datagram_write, FastDatagramFifo datagram_read)
        {
            this.Pipe = pipe;
            this.CancelWatcher = cancel_watcher;
            this.StreamWriter = stream_to_write;
            this.StreamReader = stream_to_read;
            this.DatagramWriter = datagram_write;
            this.DatagramReader = datagram_read;
        }

        public sealed class FastPipeEndAttachHandle : IDisposable
        {
            public FastPipeEnd PipeEnd { get; }
            public object UserState { get; }
            LeakChecker.Holder Leak;
            object LockObj = new object();

            public FastPipeEndAttachHandle(FastPipeEnd end, object user_state = null)
            {
                lock (end.AttachHandleLock)
                {
                    if (end.CurrentAttachHandle != null)
                        throw new ApplicationException("The FastPipeEnd is already attached.");

                    this.UserState = user_state;
                    this.PipeEnd = end;
                    this.PipeEnd.CurrentAttachHandle = this;

                    Leak = LeakChecker.EnterShared();
                }
            }

            int receive_timeout_proc_id = 0;
            TimeoutDetector receive_timeout_detector = null;

            public void SetStreamReceiveTimeout(int timeout = Timeout.Infinite)
            {
                lock (LockObj)
                {
                    if (timeout < 0 || timeout == int.MaxValue)
                    {
                        if (receive_timeout_proc_id != 0)
                        {
                            PipeEnd.StreamReader.EventListeners.Unregister(receive_timeout_proc_id);
                            receive_timeout_proc_id = 0;
                            receive_timeout_detector.DisposeSafe();
                        }
                    }
                    else
                    {
                        receive_timeout_detector = new TimeoutDetector(timeout, callme: () =>
                        {
                            PipeEnd.Pipe.Disconnect(new TimeoutException("StreamReceiveTimeout"));
                        });

                        receive_timeout_proc_id = PipeEnd.StreamReader.EventListeners.Register((buffer, type, state) =>
                        {
                            if (type == FastBufferCallbackEventType.Written)
                                receive_timeout_detector.Keep();
                        });
                    }
                }
            }

            int send_timeout_proc_id = 0;
            TimeoutDetector send_timeout_detector = null;

            public void SetStreamSendTimeout(int timeout = Timeout.Infinite)
            {
                lock (LockObj)
                {
                    if (timeout < 0 || timeout == int.MaxValue)
                    {
                        if (send_timeout_proc_id != 0)
                        {
                            PipeEnd.StreamReader.EventListeners.Unregister(send_timeout_proc_id);
                            send_timeout_proc_id = 0;
                            send_timeout_detector.DisposeSafe();
                        }
                    }
                    else
                    {
                        send_timeout_detector = new TimeoutDetector(timeout, callme: () =>
                        {
                            PipeEnd.Pipe.Disconnect(new TimeoutException("StreamSendTimeout"));
                        });

                        send_timeout_proc_id = PipeEnd.StreamReader.EventListeners.Register((buffer, type, state) =>
                        {
                            if (type == FastBufferCallbackEventType.Read)
                                send_timeout_detector.Keep();
                        });
                    }
                }
            }

            Once dispose_flag;
            public void Dispose()
            {
                if (dispose_flag.IsFirstCall())
                {
                    lock (LockObj)
                    {
                        SetStreamReceiveTimeout(Timeout.Infinite);
                        SetStreamSendTimeout(Timeout.Infinite);
                    }

                    lock (PipeEnd.AttachHandleLock)
                    {
                        PipeEnd.CurrentAttachHandle = null;
                    }

                    receive_timeout_detector.DisposeSafe();
                    send_timeout_detector.DisposeSafe();

                    Leak.Dispose();
                }
            }
        }

        object AttachHandleLock = new object();
        FastPipeEndAttachHandle CurrentAttachHandle = null;

        public FastPipeEndAttachHandle Attach(object user_state = null) => new FastPipeEndAttachHandle(this, user_state);

        public FastPipeEndStream GetStream(bool auto_flush = true) => new FastPipeEndStream(this, auto_flush);
    }

    public class FastPipeEndStream : Stream, IDisposable
    {
        public bool AutoFlush { get; set; }
        public FastPipeEnd End { get; }
        public FastPipeEnd.FastPipeEndAttachHandle AttachHandle { get; }

        public FastPipeEndStream(FastPipeEnd end, bool auto_flush = true)
        {
            End = end;
            AutoFlush = auto_flush;

            AttachHandle = end.Attach();
        }


        #region Stream
        public bool IsReadyToSend => End.StreamReader.IsReadyToWrite;
        public bool IsReadyToReceive => End.StreamReader.IsReadyToRead;
        public bool IsReadyToSendTo => End.DatagramReader.IsReadyToWrite;
        public bool IsReadyToReceiveFrom => End.DatagramReader.IsReadyToRead;
        public bool IsDisconnected => End.StreamReader.IsDisconnected || End.DatagramReader.IsDisconnected;

        public void CheckDisconnect()
        {
            End.StreamReader.CheckDisconnected();
            End.DatagramReader.CheckDisconnected();
        }

        public Task WaitReadyToSendAsync(CancellationToken cancel, int timeout)
        {
            cancel.ThrowIfCancellationRequested();

            if (End.StreamWriter.IsReadyToWrite) return Task.CompletedTask;

            return End.StreamWriter.WaitForReadyToWrite(cancel, timeout);
        }

        public Task WaitReadyToReceiveAsync(CancellationToken cancel, int timeout)
        {
            cancel.ThrowIfCancellationRequested();

            if (End.StreamReader.IsReadyToRead) return Task.CompletedTask;

            return End.StreamReader.WaitForReadyToRead(cancel, timeout);
        }

        public async Task FastSendAsync(Memory<Memory<byte>> items, CancellationToken cancel = default(CancellationToken), bool flush = true)
        {
            await WaitReadyToSendAsync(cancel, WriteTimeout);

            End.StreamWriter.EnqueueAllWithLock(items.Span);

            if (flush) FastFlush(true, false);
        }

        public async Task FastSendAsync(Memory<byte> item, CancellationToken cancel = default(CancellationToken), bool flush = true)
        {
            await WaitReadyToSendAsync(cancel, WriteTimeout);

            lock (End.StreamWriter.LockObj)
            {
                End.StreamWriter.Enqueue(item);
            }

            if (flush) FastFlush(true, false);
        }

        public async Task SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancel = default(CancellationToken))
        {
            Memory<byte> send_data = buffer.ToArray();

            await FastSendAsync(send_data, cancel);

            if (AutoFlush) FastFlush(true, false);
        }

        public void Send(ReadOnlyMemory<byte> buffer, CancellationToken cancel = default(CancellationToken))
            => SendAsync(buffer, cancel).Wait();

        public async Task ReceiveAllAsync(Memory<byte> buffer, CancellationToken cancel = default(CancellationToken))
        {
            while (buffer.Length >= 1)
            {
                int r = await ReceiveAsync(buffer, cancel);
                if (r <= 0)
                {
                    End.StreamReader.CheckDisconnected();
                    End.StreamReader.ExceptionQueue.Raise(new FastBufferDisconnectedException());
                }
                buffer.Walk(r);
            }
        }

        public async Task<Memory<byte>> ReceiveAllAsync(int max_size, CancellationToken cancel = default(CancellationToken))
        {
            Memory<byte> buffer = new byte[max_size];
            await ReceiveAllAsync(buffer, cancel);
            return buffer;
        }

        public async Task<int> ReceiveAsync(Memory<byte> buffer, CancellationToken cancel = default(CancellationToken))
        {
            try
            {
                LABEL_RETRY:
                await WaitReadyToReceiveAsync(cancel, ReadTimeout);

                int ret = 0;

                lock (End.StreamReader.LockObj)
                    ret = End.StreamReader.DequeueContiguousSlow(buffer);

                if (ret == 0)
                {
                    await Task.Yield();
                    goto LABEL_RETRY;
                }

                Debug.Assert(ret <= buffer.Length);

                End.StreamReader.CompleteRead();

                return ret;
            }
            catch (DisconnectedException)
            {
                return 0;
            }
        }

        public async Task<Memory<byte>> ReceiveAsync(int max_size = int.MaxValue, CancellationToken cancel = default(CancellationToken))
        {
            try
            {
                LABEL_RETRY:
                await WaitReadyToReceiveAsync(cancel, ReadTimeout);

                Memory<byte> ret;

                lock (End.StreamReader.LockObj)
                    ret = End.StreamReader.DequeueContiguousSlow(max_size);

                if (ret.Length == 0)
                {
                    await Task.Yield();
                    goto LABEL_RETRY;
                }

                End.StreamReader.CompleteRead();

                return ret;
            }
            catch (DisconnectedException)
            {
                return Memory<byte>.Empty;
            }
        }

        public int Receive(Memory<byte> buffer, CancellationToken cancel = default(CancellationToken))
            => ReceiveAsync(buffer, cancel).Result;

        public Memory<byte> Receive(int max_size = int.MaxValue, CancellationToken cancel = default(CancellationToken))
            => ReceiveAsync(max_size, cancel).Result;

        public async Task<List<Memory<byte>>> FastReceiveAsync(CancellationToken cancel = default(CancellationToken))
        {
            try
            {
                LABEL_RETRY:
                await WaitReadyToReceiveAsync(cancel, ReadTimeout);

                var ret = End.StreamReader.DequeueAllWithLock(out long total_read_size);

                if (total_read_size == 0)
                {
                    await Task.Yield();
                    goto LABEL_RETRY;
                }

                End.StreamReader.CompleteRead();

                return ret;
            }
            catch (DisconnectedException)
            {
                return new List<Memory<byte>>();
            }
        }

        public async Task<List<Memory<byte>>> FastPeekAsync(int max_size = int.MaxValue, CancellationToken cancel = default(CancellationToken))
        {
            LABEL_RETRY:
            CheckDisconnect();
            await WaitReadyToReceiveAsync(cancel, ReadTimeout);
            CheckDisconnect();

            long total_read_size;
            FastBufferSegment<Memory<byte>>[] tmp;
            lock (End.StreamReader.LockObj)
            {
                tmp = End.StreamReader.GetSegmentsFast(End.StreamReader.PinHead, max_size, out total_read_size, true);
            }

            if (total_read_size == 0)
            {
                await Task.Yield();
                goto LABEL_RETRY;
            }

            List<Memory<byte>> ret = new List<Memory<byte>>();
            foreach (FastBufferSegment<Memory<byte>> item in tmp)
                ret.Add(item.Item);

            return ret;
        }

        public async Task<Memory<byte>> FastPeekContiguousAsync(int max_size = int.MaxValue, CancellationToken cancel = default(CancellationToken))
        {
            LABEL_RETRY:
            CheckDisconnect();
            await WaitReadyToReceiveAsync(cancel, ReadTimeout);
            CheckDisconnect();

            Memory<byte> ret;

            lock (End.StreamReader.LockObj)
            {
                 ret = End.StreamReader.GetContiguous(End.StreamReader.PinHead, max_size, true);
            }

            if (ret.Length == 0)
            {
                await Task.Yield();
                goto LABEL_RETRY;
            }

            return ret;
        }

        public async Task<Memory<byte>> PeekAsync(int max_size = int.MaxValue, CancellationToken cancel = default(CancellationToken))
            => (await FastPeekContiguousAsync(max_size, cancel)).ToArray();

        public async Task<int> PeekAsync(Memory<byte> buffer, CancellationToken cancel = default(CancellationToken))
        {
            var tmp = await PeekAsync(buffer.Length, cancel);
            tmp.CopyTo(buffer);
            return tmp.Length;
        }

        #endregion


        #region Datagram
        public Task WaitReadyToSendToAsync(CancellationToken cancel, int timeout)
        {
            cancel.ThrowIfCancellationRequested();

            if (End.DatagramWriter.IsReadyToWrite) return Task.CompletedTask;

            return End.DatagramWriter.WaitForReadyToWrite(cancel, timeout);
        }

        public Task WaitReadyToReceiveFromAsync(CancellationToken cancel, int timeout)
        {
            cancel.ThrowIfCancellationRequested();

            if (End.DatagramReader.IsReadyToRead) return Task.CompletedTask;

            return End.DatagramReader.WaitForReadyToRead(cancel, timeout);
        }

        public async Task FastSendToAsync(Memory<Datagram> items, CancellationToken cancel = default(CancellationToken), bool flush = true)
        {
            await WaitReadyToSendToAsync(cancel, WriteTimeout);

            End.DatagramWriter.EnqueueAllWithLock(items.Span);

            if (flush) FastFlush(false, true);
        }

        public async Task FastSendToAsync(Datagram item, CancellationToken cancel = default(CancellationToken), bool flush = true)
        {
            await WaitReadyToSendToAsync(cancel, WriteTimeout);

            lock (End.StreamWriter.LockObj)
            {
                End.DatagramWriter.Enqueue(item);
            }

            if (flush) FastFlush(false, true);
        }

        public async Task SendToAsync(ReadOnlyMemory<byte> buffer, EndPoint remote_endpoint, CancellationToken cancel = default(CancellationToken))
        {
            Datagram send_data = new Datagram(buffer.Span.ToArray(), remote_endpoint);

            await FastSendToAsync(send_data, cancel);

            if (AutoFlush) FastFlush(false, true);
        }

        public void SendTo(ReadOnlyMemory<byte> buffer, EndPoint remote_endpoint, CancellationToken cancel = default(CancellationToken))
            => SendToAsync(buffer, remote_endpoint, cancel).Wait();

        public async Task<List<Datagram>> FastReceiveFromAsync(CancellationToken cancel = default(CancellationToken))
        {
            LABEL_RETRY:
            await WaitReadyToReceiveFromAsync(cancel, ReadTimeout);

            var ret = End.DatagramReader.DequeueAllWithLock(out long total_read_size);
            if (total_read_size == 0)
            {
                await Task.Yield();
                goto LABEL_RETRY;
            }

            End.DatagramReader.CompleteRead();

            return ret;
        }

        public async Task<Datagram> ReceiveFromAsync(CancellationToken cancel = default(CancellationToken))
        {
            LABEL_RETRY:
            await WaitReadyToReceiveFromAsync(cancel, ReadTimeout);

            List<Datagram> data_list;

            long total_read_size;

            lock (End.DatagramReader.LockObj)
            {
                data_list = End.DatagramReader.Dequeue(1, out total_read_size);
            }

            if (total_read_size == 0)
            {
                await Task.Yield();
                goto LABEL_RETRY;
            }

            Debug.Assert(data_list.Count == 1);

            End.DatagramReader.CompleteRead();

            return data_list[0];
        }

        public async Task<SocketReceiveFromResult> ReceiveFromAsync(Memory<byte> buffer, CancellationToken cancel = default(CancellationToken))
        {
            var datagram = await ReceiveFromAsync(cancel);
            datagram.Data.CopyTo(buffer);

            SocketReceiveFromResult ret = new SocketReceiveFromResult();
            ret.ReceivedBytes = datagram.Data.Length;
            ret.RemoteEndPoint = datagram.EndPoint;
            return ret;
        }

        public int ReceiveFrom(Memory<byte> buffer, out EndPoint remote_endpoint, CancellationToken cancel = default(CancellationToken))
        {
            SocketReceiveFromResult r = ReceiveFromAsync(buffer, cancel).Result;

            remote_endpoint = r.RemoteEndPoint;

            return r.ReceivedBytes;
        }


        #endregion



        public void FastFlush(bool stream = true, bool datagram = true)
        {
            if (stream)
                End.StreamWriter.CompleteWrite();

            if (datagram)
                End.DatagramWriter.CompleteWrite();
        }

        public void Disconnect() => End.Disconnect();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotImplementedException();
        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();
        public override void SetLength(long value) => throw new NotImplementedException();

        public override bool CanTimeout => true;
        public override int ReadTimeout { get; set; } = Timeout.Infinite;
        public override int WriteTimeout { get; set; } = Timeout.Infinite;

        public override void Flush() => FastFlush();

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            Flush();
            return Task.CompletedTask;
        }


        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => SendAsync(buffer.AsReadOnlyMemory(offset, count), cancellationToken);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReceiveAsync(buffer.AsMemory(offset, count), cancellationToken);

        public override void Write(byte[] buffer, int offset, int count) => WriteAsync(buffer, offset, count, CancellationToken.None).Wait();
        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer, offset, count, CancellationToken.None).Result;

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            => ReadAsync(buffer, offset, count, CancellationToken.None).AsApm(callback, state);
        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            => WriteAsync(buffer, offset, count, CancellationToken.None).AsApm(callback, state);
        public override int EndRead(IAsyncResult asyncResult) => ((Task<int>)asyncResult).Result;
        public override void EndWrite(IAsyncResult asyncResult) => ((Task)asyncResult).Wait();

        Once dispose_flag;
        protected override void Dispose(bool disposing)
        {
            if (dispose_flag.IsFirstCall() && disposing)
            {
                AttachHandle.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public class FastPipeNonblockStateHelper
    {
        byte[] LastState = new byte[0];

        public FastPipeNonblockStateHelper() { }
        public FastPipeNonblockStateHelper(IFastBufferState reader, IFastBufferState writer, CancellationToken cancel = default(CancellationToken)) : this()
        {
            AddWatchReader(reader);
            AddWatchWriter(writer);
            AddCancel(cancel);
        }

        List<IFastBufferState> ReaderList = new List<IFastBufferState>();
        List<IFastBufferState> WriterList = new List<IFastBufferState>();
        List<AsyncAutoResetEvent> WaitEventList = new List<AsyncAutoResetEvent>();
        List<CancellationToken> WaitCancelList = new List<CancellationToken>();

        public void AddWatchReader(IFastBufferState obj)
        {
            if (ReaderList.Contains(obj) == false)
                ReaderList.Add(obj);
            AddEvent(obj.EventReadReady);
        }

        public void AddWatchWriter(IFastBufferState obj)
        {
            if (WriterList.Contains(obj) == false)
                WriterList.Add(obj);
            AddEvent(obj.EventWriteReady);
        }

        public void AddEvent(AsyncAutoResetEvent ev) => AddEvents(new AsyncAutoResetEvent[] { ev });
        public void AddEvents(params AsyncAutoResetEvent[] events)
        {
            foreach (var ev in events)
                if (WaitEventList.Contains(ev) == false)
                    WaitEventList.Add(ev);
        }

        public void AddCancel(CancellationToken c) => AddCancels(new CancellationToken[] { c });
        public void AddCancels(params CancellationToken[] cancels)
        {
            foreach (var cancel in cancels)
                if (cancel.CanBeCanceled)
                    if (WaitCancelList.Contains(cancel) == false)
                        WaitCancelList.Add(cancel);
        }

        public byte[] SnapshotState(long salt = 0)
        {
            SpanBuffer<byte> ret = new SpanBuffer<byte>();
            ret.WriteSInt64(salt);
            foreach (var s in ReaderList)
            {
                lock (s.LockObj)
                {
                    ret.WriteUInt8((byte)(s.IsReadyToRead ? 1 : 0));
                    ret.WriteSInt64(s.PinTail);
                }
            }
            foreach (var s in WriterList)
            {
                lock (s.LockObj)
                {
                    ret.WriteUInt8((byte)(s.IsReadyToWrite ? 1 : 0));
                    ret.WriteSInt64(s.PinHead);
                }
            }
            return ret.Span.ToArray();
        }

        public bool IsStateChanged(int salt = 0)
        {
            byte[] new_state = SnapshotState(salt);
            if (LastState.SequenceEqual(new_state))
                return false;
            LastState = new_state;
            return true;
        }

        public async Task<bool> WaitIfNothingChanged(int timeout = Timeout.Infinite, int salt = 0)
        {
            timeout = WebSocketHelper.GetMinTimeout(timeout, FastPipeGlobalConfig.PollingTimeout);
            if (timeout == 0) return false;
            if (IsStateChanged(salt)) return false;

            await WebSocketHelper.WaitObjectsAsync(
                cancels: WaitCancelList.ToArray(),
                auto_events: WaitEventList.ToArray(),
                timeout: timeout);

            return true;
        }
    }

    [Flags]
    public enum PipeSupportedDataTypes
    {
        Stream = 1,
        Datagram = 2,
    }

    public abstract class FastPipeEndAsyncObjectWrapper : IDisposable
    {
        public CancelWatcher CancelWatcher { get; }
        public FastPipeEnd PipeEnd { get; }
        public abstract PipeSupportedDataTypes SupportedDataTypes { get; }
        Task LoopCompletedTask = Task.CompletedTask;

        public SharedExceptionQueue ExceptionQueue { get => PipeEnd.ExceptionQueue; }

        public FastPipeEndAsyncObjectWrapper(FastPipeEnd pipe_end, CancellationToken cancel = default(CancellationToken))
        {
            PipeEnd = pipe_end;
            CancelWatcher = new CancelWatcher(cancel);
        }

        Once connect_flag;
        public Task StartLoopAsync()
        {
            if (connect_flag.IsFirstCall())
            {
                LoopCompletedTask = ConnectAndWaitAsync();
            }

            return LoopCompletedTask;
        }

        async Task ConnectAndWaitAsync()
        {
            try
            {
                using (PipeEnd.Attach())
                {
                    List<Task> tasks = new List<Task>();

                    if (SupportedDataTypes.HasFlag(PipeSupportedDataTypes.Stream))
                    {
                        tasks.Add(StreamReadFromPipeLoopAsync());
                        tasks.Add(StreamWriteToPipeLoopAsync());
                    }

                    if (SupportedDataTypes.HasFlag(PipeSupportedDataTypes.Datagram))
                    {
                        tasks.Add(DatagramReadFromPipeLoopAsync());
                        tasks.Add(DatagramWriteToPipeLoopAsync());
                    }

                    await Task.WhenAll(tasks.ToArray());
                }
            }
            finally
            {
                Disconnect();
            }
        }

        List<Action> OnDisconnectedList = new List<Action>();
        public void AddOnDisconnected(Action proc) => OnDisconnectedList.Add(proc);

        protected abstract Task StreamWriteToObject(FastStreamFifo fifo, CancellationToken cancel);
        protected abstract Task StreamReadFromObject(FastStreamFifo fifo, CancellationToken cancel);

        protected abstract Task DatagramWriteToObject(FastDatagramFifo fifo, CancellationToken cancel);
        protected abstract Task DatagramReadFromObject(FastDatagramFifo fifo, CancellationToken cancel);

        async Task StreamReadFromPipeLoopAsync()
        {
            using (LeakChecker.EnterShared())
            {
                try
                {
                    var reader = PipeEnd.StreamReader;
                    while (true)
                    {
                        bool state_changed;
                        do
                        {
                            state_changed = false;

                            CancelWatcher.CancelToken.ThrowIfCancellationRequested();

                            while (reader.IsReadyToRead)
                            {
                                await StreamWriteToObject(reader, CancelWatcher.CancelToken);
                                state_changed = true;
                            }
                        }
                        while (state_changed);

                        await WebSocketHelper.WaitObjectsAsync(
                            auto_events: new AsyncAutoResetEvent[] { reader.EventReadReady },
                            cancels: new CancellationToken[] { CancelWatcher.CancelToken },
                            timeout: FastPipeGlobalConfig.PollingTimeout
                            );
                    }
                }
                catch (Exception ex)
                {
                    ExceptionQueue.Raise(ex);
                }
                finally
                {
                    PipeEnd.Disconnect();
                    Disconnect();
                }
            }
        }

        async Task StreamWriteToPipeLoopAsync()
        {
            using (LeakChecker.EnterShared())
            {
                try
                {
                    var writer = PipeEnd.StreamWriter;
                    while (true)
                    {
                        bool state_changed;
                        do
                        {
                            state_changed = false;

                            CancelWatcher.CancelToken.ThrowIfCancellationRequested();

                            if (writer.IsReadyToWrite)
                            {
                                long last_tail = writer.PinTail;
                                await StreamReadFromObject(writer, CancelWatcher.CancelToken);
                                if (writer.PinTail != last_tail)
                                {
                                    state_changed = true;
                                }
                            }

                        }
                        while (state_changed);

                        await WebSocketHelper.WaitObjectsAsync(
                            auto_events: new AsyncAutoResetEvent[] { writer.EventWriteReady },
                            cancels: new CancellationToken[] { CancelWatcher.CancelToken },
                            timeout: FastPipeGlobalConfig.PollingTimeout
                            );
                    }
                }
                catch (Exception ex)
                {
                    ExceptionQueue.Raise(ex);
                }
                finally
                {
                    PipeEnd.Disconnect();
                }
            }
        }

        async Task DatagramReadFromPipeLoopAsync()
        {
            using (LeakChecker.EnterShared())
            {
                try
                {
                    var reader = PipeEnd.DatagramReader;
                    while (true)
                    {
                        bool state_changed;
                        do
                        {
                            state_changed = false;

                            CancelWatcher.CancelToken.ThrowIfCancellationRequested();

                            while (reader.IsReadyToRead)
                            {
                                await DatagramWriteToObject(reader, CancelWatcher.CancelToken);
                                state_changed = true;
                            }
                        }
                        while (state_changed);

                        await WebSocketHelper.WaitObjectsAsync(
                            auto_events: new AsyncAutoResetEvent[] { reader.EventReadReady },
                            cancels: new CancellationToken[] { CancelWatcher.CancelToken },
                            timeout: FastPipeGlobalConfig.PollingTimeout
                            );
                    }
                }
                catch (Exception ex)
                {
                    ExceptionQueue.Raise(ex);
                }
                finally
                {
                    PipeEnd.Disconnect();
                    Disconnect();
                }
            }
        }

        async Task DatagramWriteToPipeLoopAsync()
        {
            using (LeakChecker.EnterShared())
            {
                try
                {
                    var writer = PipeEnd.DatagramWriter;
                    while (true)
                    {
                        bool state_changed;
                        do
                        {
                            state_changed = false;

                            CancelWatcher.CancelToken.ThrowIfCancellationRequested();

                            if (writer.IsReadyToWrite)
                            {
                                long last_tail = writer.PinTail;
                                await DatagramReadFromObject(writer, CancelWatcher.CancelToken);
                                if (writer.PinTail != last_tail)
                                {
                                    state_changed = true;
                                }
                            }

                        }
                        while (state_changed);

                        await WebSocketHelper.WaitObjectsAsync(
                            auto_events: new AsyncAutoResetEvent[] { writer.EventWriteReady },
                            cancels: new CancellationToken[] { CancelWatcher.CancelToken },
                            timeout: FastPipeGlobalConfig.PollingTimeout
                            );
                    }
                }
                catch (Exception ex)
                {
                    ExceptionQueue.Raise(ex);
                }
                finally
                {
                    PipeEnd.Disconnect();
                }
            }
        }

        Once disconnect_flag;
        public void Disconnect()
        {
            if (disconnect_flag.IsFirstCall())
            {
                this.PipeEnd.Disconnect();
                CancelWatcher.Cancel();

                foreach (var proc in OnDisconnectedList) try { proc(); } catch { };
            }
        }

        Once dispose_flag;
        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing)
        {
            if (dispose_flag.IsFirstCall() && disposing)
            {
                Disconnect();
                CancelWatcher.DisposeSafe();
            }
        }
    }

    public class FastPipeEndSocketWrapper : FastPipeEndAsyncObjectWrapper, IDisposable
    {
        public Socket Socket { get; }
        public int RecvTmpBufferSize { get; private set; }
        public override PipeSupportedDataTypes SupportedDataTypes { get; }

        static bool UseDontLingerOption = true;

        public FastPipeEndSocketWrapper(FastPipeEnd pipe_end, Socket socket, CancellationToken cancel = default(CancellationToken)) : base(pipe_end, cancel)
        {
            this.Socket = socket;
            SupportedDataTypes = (Socket.SocketType == SocketType.Stream) ? PipeSupportedDataTypes.Stream : PipeSupportedDataTypes.Datagram;
            if (Socket.SocketType == SocketType.Stream)
            {
                Socket.LingerState = new LingerOption(false, 0);
                try
                {
                    if (UseDontLingerOption)
                        Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
                }
                catch
                {
                    UseDontLingerOption = false;
                }
                Socket.NoDelay = true;
            }
            Socket.ReceiveTimeout = Socket.SendTimeout = Timeout.Infinite;
            this.AddOnDisconnected(() => Socket.DisposeSafe());
        }

        protected override async Task StreamWriteToObject(FastStreamFifo fifo, CancellationToken cancel)
        {
            if (SupportedDataTypes.HasFlag(PipeSupportedDataTypes.Stream) == false) throw new NotSupportedException();

            List<Memory<byte>> send_array;

            send_array = fifo.DequeueAllWithLock(out long _);

            List<ArraySegment<byte>> send_array2 = new List<ArraySegment<byte>>();
            foreach (Memory<byte> mem in send_array)
            {
                send_array2.Add(mem.AsSegment());
            }

            await WebSocketHelper.DoAsyncWithTimeout(
                async c =>
                {
                    await Socket.SendAsync(send_array2, SocketFlags.None);
                    return 0;
                },
                cancel: cancel);

            fifo.CompleteRead();
        }

        FastMemoryAllocator<byte> FastMemoryAllocatorForStream = new FastMemoryAllocator<byte>();

        AsyncBulkReceiver<Memory<byte>, FastPipeEndSocketWrapper> StreamBulkReceiver = new AsyncBulkReceiver<Memory<byte>, FastPipeEndSocketWrapper>(async me =>
        {
            if (me.RecvTmpBufferSize == 0)
            {
                int i = me.Socket.ReceiveBufferSize;
                if (i <= 0) i = 65536;
                me.RecvTmpBufferSize = Math.Min(i, FastPipeGlobalConfig.MaxStreamBufferLength);
            }

            Memory<byte> tmp = me.FastMemoryAllocatorForStream.Reserve(me.RecvTmpBufferSize);
            int r = await me.Socket.ReceiveAsync(tmp, SocketFlags.None);
            if (r < 0) throw new SocketDisconnectedException();
            me.FastMemoryAllocatorForStream.Commit(ref tmp, r);
            if (r == 0) return new ValueOrClosed<Memory<byte>>();
            return new ValueOrClosed<Memory<byte>>(tmp);
        });

        protected override async Task StreamReadFromObject(FastStreamFifo fifo, CancellationToken cancel)
        {
            if (SupportedDataTypes.HasFlag(PipeSupportedDataTypes.Stream) == false) throw new NotSupportedException();

            Memory<byte>[] recv_list = await StreamBulkReceiver.Recv(cancel, this);

            if (recv_list == null)
            {
                // disconnected
                fifo.Disconnect();
                return;
            }

            fifo.EnqueueAllWithLock(recv_list);

            fifo.CompleteWrite();
        }

        protected override async Task DatagramWriteToObject(FastDatagramFifo fifo, CancellationToken cancel)
        {
            if (SupportedDataTypes.HasFlag(PipeSupportedDataTypes.Datagram) == false) throw new NotSupportedException();

            List<Datagram> send_list;

            send_list = fifo.DequeueAllWithLock(out _);

            await WebSocketHelper.DoAsyncWithTimeout(
                async c =>
                {
                    foreach (Datagram data in send_list)
                    {
                        cancel.ThrowIfCancellationRequested();
                        await Socket.SendToSafeUdpErrorAsync(data.Data.AsSegment(), SocketFlags.None, data.EndPoint);
                    }
                    return 0;
                },
                cancel: cancel);

            fifo.CompleteRead();
        }

        FastMemoryAllocator<byte> FastMemoryAllocatorForDatagram = new FastMemoryAllocator<byte>();

        AsyncBulkReceiver<Datagram, FastPipeEndSocketWrapper> DatagramBulkReceiver = new AsyncBulkReceiver<Datagram, FastPipeEndSocketWrapper>(async me =>
        {
            Memory<byte> tmp = me.FastMemoryAllocatorForDatagram.Reserve(65536);

            var ret = await me.Socket.ReceiveFromSafeUdpErrorAsync(tmp, SocketFlags.None);

            me.FastMemoryAllocatorForDatagram.Commit(ref tmp, ret.ReceivedBytes);

            Datagram pkt = new Datagram(tmp, ret.RemoteEndPoint);
            return new ValueOrClosed<Datagram>(pkt);
        });

        protected override async Task DatagramReadFromObject(FastDatagramFifo fifo, CancellationToken cancel)
        {
            if (SupportedDataTypes.HasFlag(PipeSupportedDataTypes.Datagram) == false) throw new NotSupportedException();

            Datagram[] pkts = await DatagramBulkReceiver.Recv(cancel, this);

            fifo.EnqueueAllWithLock(pkts);

            fifo.CompleteWrite();
        }

        Once dispose_flag;
        protected override void Dispose(bool disposing)
        {
            if (dispose_flag.IsFirstCall() && disposing)
            {
                Socket.DisposeSafe();
            }
            base.Dispose(disposing);
        }
    }

    public class FastTcpPipe : FastPipe, IDisposable
    {
        public Socket Socket { get; }
        public IPEndPoint LocalEndPoint { get; }
        public IPEndPoint RemoteEndPoint { get; }
        public FastPipeEnd LocalPipeEnd { get => this.B; }
        public const int DefaultConnectTimeout = 15 * 1000;

        FastPipeEndSocketWrapper SocketWrapper;
        Task SocketWrapperLoopTask;

        public FastTcpPipe(Socket socket, CancellationToken cancel = default(CancellationToken), long? threshold_length_stream = null)
            : base(cancel, threshold_length_stream)
        {
            Socket = socket;
            LocalEndPoint = (IPEndPoint)Socket.LocalEndPoint;
            RemoteEndPoint = (IPEndPoint)Socket.RemoteEndPoint;

            SocketWrapper = new FastPipeEndSocketWrapper(this.A, this.Socket, cancel);
            SocketWrapperLoopTask = SocketWrapper.StartLoopAsync();
        }

        public async Task WaitForLoopFinish()
        {
            try
            {
                await SocketWrapperLoopTask;
            }
            catch { }
        }

        public FastPipeEndStream GetStream(bool auto_flush = true) => LocalPipeEnd.GetStream(auto_flush);

        public static async Task<FastTcpPipe> ConnectAsync(string host, int port, AddressFamily? address_family = null, CancellationToken cancel = default(CancellationToken), int connect_timeout = DefaultConnectTimeout)
        {
            if (IPAddress.TryParse(host, out IPAddress ip))
            {
                if (address_family != null && ip.AddressFamily != address_family)
                    throw new ArgumentException("ip.AddressFamily != address_family");
            }
            else
            {
                ip = await WebSocketHelper.DoAsyncWithTimeout(async c =>
                {
                    return (await Dns.GetHostAddressesAsync(host))
                        .Where(x => x.AddressFamily == AddressFamily.InterNetwork || x.AddressFamily == AddressFamily.InterNetworkV6)
                        .Where(x => address_family == null || x.AddressFamily == address_family).First();
                },
                timeout: connect_timeout,
                cancel: cancel);
            }

            return await ConnectAsync(ip, port, cancel, connect_timeout);
        }

        public static Task<FastTcpPipe> ConnectAsync(IPAddress ip, int port, CancellationToken cancel = default(CancellationToken), int connect_timeout = DefaultConnectTimeout)
            => ConnectAsync(new IPEndPoint(ip, port), cancel, connect_timeout);

        public static async Task<FastTcpPipe> ConnectAsync(IPEndPoint endpoint, CancellationToken cancel = default(CancellationToken), int connect_timeout = DefaultConnectTimeout)
        {
            if (!(endpoint.AddressFamily == AddressFamily.InterNetwork || endpoint.AddressFamily == AddressFamily.InterNetworkV6))
                throw new ArgumentException("dest.AddressFamily");

            Socket s = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await WebSocketHelper.DoAsyncWithTimeout(async (c) =>
                {
                    await s.ConnectAsync(endpoint);
                    return 0;
                },
                cancel_proc: () =>
                {
                    s.DisposeSafe();
                },
                timeout: connect_timeout,
                cancel: cancel);
            }
            catch
            {
                s.DisposeSafe();
                throw;
            }

            FastTcpPipe pipe = new FastTcpPipe(s, cancel);
            return pipe;
        }

        Once dispose_flag;
        protected override void Dispose(bool disposing)
        {
            if (dispose_flag.IsFirstCall() && disposing)
            {
                Socket.DisposeSafe();
            }
            base.Dispose(disposing);
        }
    }

    public sealed class FastPipeTcpListener : IDisposable
    {
        public TcpListenManager ListenerManager { get; }
        public int? QueueThresholdLengthStream = null;
        public object UserState;

        CancellationTokenSource CancelSource = new CancellationTokenSource();

        public delegate Task FastPipeTcpListenerAcceptProc(FastPipeTcpListener listener, FastTcpPipe pipe, FastPipeEnd end);

        FastPipeTcpListenerAcceptProc AcceptProc;

        public FastPipeTcpListener(FastPipeTcpListenerAcceptProc accept_proc, object user_state = null)
        {
            this.UserState = user_state;
            this.AcceptProc = accept_proc;

            ListenerManager = new TcpListenManager(ListenManagerAcceptProc);
        }

        async Task ListenManagerAcceptProc(TcpListenManager manager, TcpListenManager.Listener listener, Socket socket)
        {
            using (LeakChecker.EnterShared())
            {
                using (FastTcpPipe p = new FastTcpPipe(socket, CancelSource.Token, QueueThresholdLengthStream))
                {
                    try
                    {
                        await AcceptProc(this, p, p.LocalPipeEnd);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("FastPipeTcpListener AcceptProc exception: " + ex.ToString());
                    }
                    finally
                    {
                        p.CancelWatcher.Cancel();
                        p.Disconnect();
                        await p.WaitForLoopFinish();
                    }
                }
            }
        }

        Once dispose_flag;
        public void Dispose()
        {
            if (dispose_flag.IsFirstCall())
            {
                CancelSource.TryCancel();

                ListenerManager.DisposeSafe();
            }
        }
    }
}


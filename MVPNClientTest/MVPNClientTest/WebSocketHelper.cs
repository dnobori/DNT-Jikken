using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;
using System.Text;
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
using System.Buffers.Binary;

#pragma warning disable CS0162, CS1998

namespace SoftEther.WebSocket.Helper
{
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
            MemoryBuffer <T> ret = new MemoryBuffer<T>(this.InternalBuffer.Slice(start, length));
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

        public static void WriteUInt8(this ref SpanBuffer<byte> buf, byte value) => value.SetUInt8(buf.Walk(1, false));
        public static void WriteUInt16(this ref SpanBuffer<byte> buf, ushort value) => value.SetUInt16(buf.Walk(2, false));
        public static void WriteUInt32(this ref SpanBuffer<byte> buf, uint value) => value.SetUInt32(buf.Walk(4, false));
        public static void WriteUInt64(this ref SpanBuffer<byte> buf, ulong value) => value.SetUInt64(buf.Walk(8, false));
        public static void WriteSInt8(this ref SpanBuffer<byte> buf, sbyte value) => value.SetSInt8(buf.Walk(1, false));
        public static void WriteSInt16(this ref SpanBuffer<byte> buf, short value) => value.SetSInt16(buf.Walk(2, false));
        public static void WriteSInt32(this ref SpanBuffer<byte> buf, int value) => value.SetSInt32(buf.Walk(4, false));
        public static void WriteSInt64(this ref SpanBuffer<byte> buf, long value) => value.SetSInt64(buf.Walk(8, false));

        public static void SetUInt8(this ref SpanBuffer<byte> buf, byte value) => value.SetUInt8(buf.Walk(1, true));
        public static void SetUInt16(this ref SpanBuffer<byte> buf, ushort value) => value.SetUInt16(buf.Walk(2, true));
        public static void SetUInt32(this ref SpanBuffer<byte> buf, uint value) => value.SetUInt32(buf.Walk(4, true));
        public static void SetUInt64(this ref SpanBuffer<byte> buf, ulong value) => value.SetUInt64(buf.Walk(8, true));
        public static void SetSInt8(this ref SpanBuffer<byte> buf, sbyte value) => value.SetSInt8(buf.Walk(1, true));
        public static void SetSInt16(this ref SpanBuffer<byte> buf, short value) => value.SetSInt16(buf.Walk(2, true));
        public static void SetSInt32(this ref SpanBuffer<byte> buf, int value) => value.SetSInt32(buf.Walk(4, true));
        public static void SetSInt64(this ref SpanBuffer<byte> buf, long value) => value.SetSInt64(buf.Walk(8, true));

        public static byte ReadUInt8(ref this SpanBuffer<byte> buf) => buf.Read(1).GetUInt8();
        public static ushort ReadUInt16(ref this SpanBuffer<byte> buf) => buf.Read(2).GetUInt16();
        public static uint ReadUInt32(ref this SpanBuffer<byte> buf) => buf.Read(4).GetUInt32();
        public static ulong ReadUInt64(ref this SpanBuffer<byte> buf) => buf.Read(8).GetUInt64();
        public static sbyte ReadSInt8(ref this SpanBuffer<byte> buf) => buf.Read(1).GetSInt8();
        public static short ReadSInt16(ref this SpanBuffer<byte> buf) => buf.Read(2).GetSInt16();
        public static int ReadSInt32(ref this SpanBuffer<byte> buf) => buf.Read(4).GetSInt32();
        public static long ReadSInt64(ref this SpanBuffer<byte> buf) => buf.Read(8).GetSInt64();

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

        public static byte ReadUInt8(ref this ReadOnlySpanBuffer<byte> buf) => buf.Read(1).GetUInt8();
        public static ushort ReadUInt16(ref this ReadOnlySpanBuffer<byte> buf) => buf.Read(2).GetUInt16();
        public static uint ReadUInt32(ref this ReadOnlySpanBuffer<byte> buf) => buf.Read(4).GetUInt32();
        public static ulong ReadUInt64(ref this ReadOnlySpanBuffer<byte> buf) => buf.Read(8).GetUInt64();
        public static sbyte ReadSInt8(ref this ReadOnlySpanBuffer<byte> buf) => buf.Read(1).GetSInt8();
        public static short ReadSInt16(ref this ReadOnlySpanBuffer<byte> buf) => buf.Read(2).GetSInt16();
        public static int ReadSInt32(ref this ReadOnlySpanBuffer<byte> buf) => buf.Read(4).GetSInt32();
        public static long ReadSInt64(ref this ReadOnlySpanBuffer<byte> buf) => buf.Read(8).GetSInt64();

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

        public static void WriteUInt8(this ref MemoryBuffer<byte> buf, byte value) => value.SetUInt8(buf.Walk(1, false));
        public static void WriteUInt16(this ref MemoryBuffer<byte> buf, ushort value) => value.SetUInt16(buf.Walk(2, false));
        public static void WriteUInt32(this ref MemoryBuffer<byte> buf, uint value) => value.SetUInt32(buf.Walk(4, false));
        public static void WriteUInt64(this ref MemoryBuffer<byte> buf, ulong value) => value.SetUInt64(buf.Walk(8, false));
        public static void WriteSInt8(this ref MemoryBuffer<byte> buf, sbyte value) => value.SetSInt8(buf.Walk(1, false));
        public static void WriteSInt16(this ref MemoryBuffer<byte> buf, short value) => value.SetSInt16(buf.Walk(2, false));
        public static void WriteSInt32(this ref MemoryBuffer<byte> buf, int value) => value.SetSInt32(buf.Walk(4, false));
        public static void WriteSInt64(this ref MemoryBuffer<byte> buf, long value) => value.SetSInt64(buf.Walk(8, false));

        public static void SetUInt8(this ref MemoryBuffer<byte> buf, byte value) => value.SetUInt8(buf.Walk(1, true));
        public static void SetUInt16(this ref MemoryBuffer<byte> buf, ushort value) => value.SetUInt16(buf.Walk(2, true));
        public static void SetUInt32(this ref MemoryBuffer<byte> buf, uint value) => value.SetUInt32(buf.Walk(4, true));
        public static void SetUInt64(this ref MemoryBuffer<byte> buf, ulong value) => value.SetUInt64(buf.Walk(8, true));
        public static void SetSInt8(this ref MemoryBuffer<byte> buf, sbyte value) => value.SetSInt8(buf.Walk(1, true));
        public static void SetSInt16(this ref MemoryBuffer<byte> buf, short value) => value.SetSInt16(buf.Walk(2, true));
        public static void SetSInt32(this ref MemoryBuffer<byte> buf, int value) => value.SetSInt32(buf.Walk(4, true));
        public static void SetSInt64(this ref MemoryBuffer<byte> buf, long value) => value.SetSInt64(buf.Walk(8, true));

        public static byte ReadUInt8(ref this MemoryBuffer<byte> buf) => buf.Read(1).GetUInt8();
        public static ushort ReadUInt16(ref this MemoryBuffer<byte> buf) => buf.Read(2).GetUInt16();
        public static uint ReadUInt32(ref this MemoryBuffer<byte> buf) => buf.Read(4).GetUInt32();
        public static ulong ReadUInt64(ref this MemoryBuffer<byte> buf) => buf.Read(8).GetUInt64();
        public static sbyte ReadSInt8(ref this MemoryBuffer<byte> buf) => buf.Read(1).GetSInt8();
        public static short ReadSInt16(ref this MemoryBuffer<byte> buf) => buf.Read(2).GetSInt16();
        public static int ReadSInt32(ref this MemoryBuffer<byte> buf) => buf.Read(4).GetSInt32();
        public static long ReadSInt64(ref this MemoryBuffer<byte> buf) => buf.Read(8).GetSInt64();

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

        public static byte ReadUInt8(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(1).GetUInt8();
        public static ushort ReadUInt16(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(2).GetUInt16();
        public static uint ReadUInt32(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(4).GetUInt32();
        public static ulong ReadUInt64(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(8).GetUInt64();
        public static sbyte ReadSInt8(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(1).GetSInt8();
        public static short ReadSInt16(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(2).GetSInt16();
        public static int ReadSInt32(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(4).GetSInt32();
        public static long ReadSInt64(ref this ReadOnlyMemoryBuffer<byte> buf) => buf.Read(8).GetSInt64();

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

        public static byte WalkReadUInt8(ref this Span<byte> span) => span.WalkRead(1).GetUInt8();
        public static ushort WalkReadUInt16(ref this Span<byte> span) => span.WalkRead(2).GetUInt16();
        public static uint WalkReadUInt32(ref this Span<byte> span) => span.WalkRead(4).GetUInt32();
        public static ulong WalkReadUInt64(ref this Span<byte> span) => span.WalkRead(8).GetUInt64();
        public static sbyte WalkReadSInt8(ref this Span<byte> span) => span.WalkRead(1).GetSInt8();
        public static short WalkReadSInt16(ref this Span<byte> span) => span.WalkRead(2).GetSInt16();
        public static int WalkReadSInt32(ref this Span<byte> span) => span.WalkRead(4).GetSInt32();
        public static long WalkReadSInt64(ref this Span<byte> span) => span.WalkRead(8).GetSInt64();

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

        public static byte WalkReadUInt8(ref this Memory<byte> memory) => memory.WalkRead(1).GetUInt8();
        public static ushort WalkReadUInt16(ref this Memory<byte> memory) => memory.WalkRead(2).GetUInt16();
        public static uint WalkReadUInt32(ref this Memory<byte> memory) => memory.WalkRead(4).GetUInt32();
        public static ulong WalkReadUInt64(ref this Memory<byte> memory) => memory.WalkRead(8).GetUInt64();
        public static sbyte WalkReadSInt8(ref this Memory<byte> memory) => memory.WalkRead(1).GetSInt8();
        public static short WalkReadSInt16(ref this Memory<byte> memory) => memory.WalkRead(2).GetSInt16();
        public static int WalkReadSInt32(ref this Memory<byte> memory) => memory.WalkRead(4).GetSInt32();
        public static long WalkReadSInt64(ref this Memory<byte> memory) => memory.WalkRead(8).GetSInt64();

        public static byte WalkReadUInt8(ref this ReadOnlyMemory<byte> memory) => memory.WalkRead(1).GetUInt8();
        public static ushort WalkReadUInt16(ref this ReadOnlyMemory<byte> memory) => memory.WalkRead(2).GetUInt16();
        public static uint WalkReadUInt32(ref this ReadOnlyMemory<byte> memory) => memory.WalkRead(4).GetUInt32();
        public static ulong WalkReadUInt64(ref this ReadOnlyMemory<byte> memory) => memory.WalkRead(8).GetUInt64();
        public static sbyte WalkReadSInt8(ref this ReadOnlyMemory<byte> memory) => memory.WalkRead(1).GetSInt8();
        public static short WalkReadSInt16(ref this ReadOnlyMemory<byte> memory) => memory.WalkRead(2).GetSInt16();
        public static int WalkReadSInt32(ref this ReadOnlyMemory<byte> memory) => memory.WalkRead(4).GetSInt32();
        public static long WalkReadSInt64(ref this ReadOnlyMemory<byte> memory) => memory.WalkRead(8).GetSInt64();



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
            int read_size;

            read_size = Math.Min(size, this.size);
            if (read_size == 0)
            {
                return 0;
            }
            if (dst != null)
            {
                WebSocketHelper.CopyByte(dst, offset, this.p, this.pos, size);
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

        public Span<byte> Span
        {
            get
            {
                return this.Data.AsSpan(this.pos, this.size);
            }
        }
    }

    public class AsyncLock : IDisposable
    {
        public class LockHolder : IDisposable
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
                if (ret.ReceivedBytes <= 0) throw new ApplicationException("UDP socket is disconnected.");
                return ret;
            }
            catch (SocketException e) when (CanUdpSocketErrorBeIgnored(e) || socket.Available >= 1)
            {
                num_retry++;
                if (num_retry >= udp_max_retry_on_ignore_error)
                {
                    throw;
                }
                goto LABEL_RETRY;
            }
        }

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
                if (ret <= 0) throw new ApplicationException("UDP socket is disconnected.");
                return ret;
            }
            catch (SocketException e) when (CanUdpSocketErrorBeIgnored(e))
            {
                return buffer.Count;
            }
        }

        public static async Task ConnectAsync(this TcpClient tc, string host, int port,
            int timeout = Timeout.Infinite, CancellationToken cancel = default(CancellationToken), params CancellationToken[] cancel_tokens)
        {
            await DoAsyncWithTimeout<int>(
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
                int ret = await DoAsyncWithTimeout<int>(async (cancel_for_proc) =>
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
                await DoAsyncWithTimeout<int>(async (cancel_for_proc) =>
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

        public static void LaissezFaire(this Task task) { }

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
    }

    public struct Once
    {
        volatile private int flag;
        public bool IsFirstCall() => (Interlocked.CompareExchange(ref this.flag, 1, 0) == 0);
        public bool IsSet => (this.flag != 0);
    }

    public class TimeoutDetector : IDisposable
    {
        Task main_loop;

        object LockObj = new object();

        Stopwatch sw = new Stopwatch();
        long tick { get => this.sw.ElapsedMilliseconds; }

        public long Timeout { get; }

        long next_timeout;

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
            this.Timeout = timeout;
            this.watcher = watcher;
            this.event_auto = event_auto;
            this.event_manual = event_manual;
            this.callme = callme;

            sw.Start();
            next_timeout = tick + this.Timeout;
            main_loop = timeout_detector_main_loop();
        }

        public void Keep()
        {
            lock (LockObj)
            {
                this.next_timeout = tick + this.Timeout;
            }
        }

        async Task timeout_detector_main_loop()
        {
            while (halt.IsCancellationRequested == false)
            {
                long now, remain_time;

                lock (LockObj)
                {
                    now = tick;
                    remain_time = next_timeout - now;
                }

                Dbg.Where($"remain_time = {remain_time}");

                if (remain_time < 0)
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

        Once dispose_flag;
        public void Dispose()
        {
            if (dispose_flag.IsFirstCall())
            {
                halt.TryCancelAsync().LaissezFaire();
            }
        }
    }

    public class CancelWatcher : IDisposable
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

        public bool AddWatch(params CancellationToken[] cancels)
        {
            bool ret = false;

            lock (LockObj)
            {
                foreach (CancellationToken cancel in cancels)
                {
                    if (this.target_list.Contains(cancel) == false)
                    {
                        this.target_list.Add(cancel);
                        ret = true;
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

    public class UdpPacket
    {
        public byte[] Data;
        public IPEndPoint EndPoint;
        public byte Flag;

        public UdpPacket(byte[] data, IPEndPoint ep, byte flag = 0)
        {
            this.Data = data;
            this.EndPoint = ep;
            this.Flag = flag;
        }
    }

    public class NonBlockSocket : IDisposable
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

        public Queue<UdpPacket> RecvUdpQueue { get; } = new Queue<UdpPacket>();
        public Queue<UdpPacket> SendUdpQueue { get; } = new Queue<UdpPacket>();

        int MaxRecvFifoSize;
        public int MaxRecvUdpQueueSize { get; }

        Task RecvLoopTask = null;
        Task SendLoopTask = null;

        AsyncBulkReceiver<UdpPacket, int> UdpBulkReader;

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
                UdpBulkReader = new AsyncBulkReceiver<UdpPacket, int>(async x =>
                {
                    SocketReceiveFromResult ret = await Sock.ReceiveFromSafeUdpErrorAsync(TmpRecvBuffer, SocketFlags.None);
                    return new UdpPacket(TmpRecvBuffer.AsSpan().Slice(0, ret.ReceivedBytes).ToArray(), (IPEndPoint)ret.RemoteEndPoint);
                });

                RecvLoopTask = UDP_RecvLoop();
                SendLoopTask = UDP_SendLoop();
            }
        }

        async Task TCP_RecvLoop()
        {
            try
            {
                await WebSocketHelper.DoAsyncWithTimeout<int>(async (cancel) =>
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
                await WebSocketHelper.DoAsyncWithTimeout<int>(async (cancel) =>
                {
                    while (cancel.IsCancellationRequested == false)
                    {
                        UdpPacket[] recv_packets = await UdpBulkReader.Read();

                        bool full_queue = false;
                        bool pkt_received = false;

                        lock (RecvUdpQueue)
                        {
                            foreach (UdpPacket p in recv_packets)
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
                await WebSocketHelper.DoAsyncWithTimeout<int>(async (cancel) =>
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
                await WebSocketHelper.DoAsyncWithTimeout<int>(async (cancel) =>
                {
                    while (cancel.IsCancellationRequested == false)
                    {
                        UdpPacket pkt = null;

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

                        int r = await Sock.SendToSafeUdpErrorAsync(pkt.Data, SocketFlags.None, pkt.EndPoint);
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

        public UdpPacket RecvFromNonBlock()
        {
            if (IsDisconnected) return null;
            lock (RecvUdpQueue)
            {
                if (RecvUdpQueue.TryDequeue(out UdpPacket ret))
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

    public class AsyncBulkReceiver<TUserReturnElement, TUserState>
    {
        public delegate Task<TUserReturnElement> AsyncReceiveProcDelegate(TUserState state);

        public int DefaultMaxCount { get; } = 1024;

        AsyncReceiveProcDelegate AsyncReceiveProc;

        public AsyncBulkReceiver(AsyncReceiveProcDelegate async_receive_proc, int default_max_count = 1024)
        {
            DefaultMaxCount = default_max_count;
            AsyncReceiveProc = async_receive_proc;
        }

        Task<TUserReturnElement> pushed_user_task = null;

        public async Task<TUserReturnElement[]> Read(TUserState state = default(TUserState), int? max_count = null)
        {
            if (max_count == null) max_count = DefaultMaxCount;
            if (max_count <= 0) max_count = int.MaxValue;
            List<TUserReturnElement> ret = new List<TUserReturnElement>();

            while (true)
            {
                Task<TUserReturnElement> user_task;
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
                        await user_task;
                        ret.Add(user_task.Result);
                    }
                }
                else
                {
                    ret.Add(user_task.Result);
                }
                if (ret.Count >= max_count) break;
            }

            return ret.ToArray();
        }
    }
}


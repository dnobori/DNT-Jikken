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
using System.Runtime.ExceptionServices;
using System.Collections.Concurrent;
using System.Security.Authentication;

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

        public SpanBuffer(Span<T> baseSpan)
        {
            InternalSpan = baseSpan;
            CurrentPosition = 0;
            Length = baseSpan.Length;
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
        public Span<T> Walk(int size, bool noMove = false)
        {
            int newSize = checked(CurrentPosition + size);

            if (InternalSpan.Length < newSize)
            {
                EnsureInternalBufferReserved(newSize);
            }
            var ret = InternalSpan.Slice(CurrentPosition, size);
            Length = Math.Max(newSize, Length);
            if (noMove == false) CurrentPosition += size;
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

        public ReadOnlySpan<T> Read(int size, bool allowPartial = false)
        {
            int sizeRead = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allowPartial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                sizeRead = Length - CurrentPosition;
            }

            Span<T> ret = InternalSpan.Slice(CurrentPosition, sizeRead);
            CurrentPosition += sizeRead;
            return ret;
        }

        public ReadOnlySpan<T> Peek(int size, bool allowPartial = false)
        {
            int sizeRead = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allowPartial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                sizeRead = Length - CurrentPosition;
            }

            Span<T> ret = InternalSpan.Slice(CurrentPosition, sizeRead);
            return ret;
        }

        public void Seek(int offset, SeekOrigin mode)
        {
            int newPosition = 0;
            if (mode == SeekOrigin.Current)
                newPosition = checked(CurrentPosition + offset);
            else if (mode == SeekOrigin.End)
                newPosition = checked(Length + offset);
            else
                newPosition = offset;

            if (newPosition < 0) throw new ArgumentOutOfRangeException("new_position < 0");
            if (newPosition > Length) throw new ArgumentOutOfRangeException("new_position > Size");

            CurrentPosition = newPosition;
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

        public void EnsureInternalBufferReserved(int newSize)
        {
            if (InternalSpan.Length >= newSize) return;

            int newInternalSize = InternalSpan.Length;
            while (newInternalSize < newSize)
                newInternalSize = checked(Math.Max(newInternalSize, 128) * 2);

            InternalSpan = InternalSpan.ReAlloc(newInternalSize);
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

        public ReadOnlySpanBuffer(ReadOnlySpan<T> baseSpan)
        {
            InternalSpan = baseSpan;
            CurrentPosition = 0;
            Length = baseSpan.Length;
        }

        public ReadOnlySpan<T> Read(int size, bool allowPartial = false)
        {
            int sizeRead = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allowPartial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                sizeRead = Length - CurrentPosition;
            }

            ReadOnlySpan<T> ret = InternalSpan.Slice(CurrentPosition, sizeRead);
            CurrentPosition += sizeRead;
            return ret;
        }

        public ReadOnlySpan<T> Peek(int size, bool allowPartial = false)
        {
            int sizeRead = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allowPartial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                sizeRead = Length - CurrentPosition;
            }

            ReadOnlySpan<T> ret = InternalSpan.Slice(CurrentPosition, sizeRead);
            return ret;
        }

        public void Seek(int offset, SeekOrigin mode)
        {
            int newPosition = 0;
            if (mode == SeekOrigin.Current)
                newPosition = checked(CurrentPosition + offset);
            else if (mode == SeekOrigin.End)
                newPosition = checked(Length + offset);
            else
                newPosition = offset;

            if (newPosition < 0) throw new ArgumentOutOfRangeException("newPosition < 0");
            if (newPosition > Length) throw new ArgumentOutOfRangeException("newPosition > Size");

            CurrentPosition = newPosition;
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

        public FastMemoryBuffer(Memory<T> baseMemory)
        {
            InternalBuffer = baseMemory;
            CurrentPosition = 0;
            Length = baseMemory.Length;
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
        public Span<T> Walk(int size, bool noMove = false)
        {
            int newSize = checked(CurrentPosition + size);
            if (InternalBuffer.Length < newSize)
            {
                EnsureInternalBufferReserved(newSize);
            }
            var ret = InternalSpan.Slice(CurrentPosition, size);
            Length = Math.Max(newSize, Length);
            if (noMove == false) CurrentPosition += size;
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

        public ReadOnlySpan<T> Read(int size, bool allowPartial = false)
        {
            int sizeRead = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allowPartial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                sizeRead = Length - CurrentPosition;
            }

            ReadOnlySpan<T> ret = InternalSpan.Slice(CurrentPosition, sizeRead);
            CurrentPosition += sizeRead;
            return ret;
        }

        public ReadOnlySpan<T> Peek(int size, bool allowPartial = false)
        {
            int sizeRead = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allowPartial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                sizeRead = Length - CurrentPosition;
            }

            Span<T> ret = InternalSpan.Slice(CurrentPosition, sizeRead);
            return ret;
        }

        public ReadOnlyMemory<T> ReadAsMemory(int size, bool allowPartial = false)
        {
            int sizeRead = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allowPartial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                sizeRead = Length - CurrentPosition;
            }

            ReadOnlyMemory<T> ret = InternalBuffer.Slice(CurrentPosition, sizeRead);
            CurrentPosition += sizeRead;
            return ret;
        }

        public ReadOnlyMemory<T> PeekAsMemory(int size, bool allowPartial = false)
        {
            int sizeRead = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allowPartial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                sizeRead = Length - CurrentPosition;
            }

            ReadOnlyMemory<T> ret = InternalBuffer.Slice(CurrentPosition, sizeRead);
            return ret;
        }

        public void Seek(int offset, SeekOrigin mode)
        {
            int newPosition = 0;
            if (mode == SeekOrigin.Current)
                newPosition = checked(CurrentPosition + offset);
            else if (mode == SeekOrigin.End)
                newPosition = checked(Length + offset);
            else
                newPosition = offset;

            if (newPosition < 0) throw new ArgumentOutOfRangeException("newPosition < 0");
            if (newPosition > Length) throw new ArgumentOutOfRangeException("newPosition > Size");

            CurrentPosition = newPosition;
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

        public void EnsureInternalBufferReserved(int newSize)
        {
            if (InternalBuffer.Length >= newSize) return;

            int newInternalSize = InternalBuffer.Length;
            while (newInternalSize < newSize)
                newInternalSize = checked(Math.Max(newInternalSize, 128) * 2);

            InternalBuffer = InternalBuffer.ReAlloc(newInternalSize);
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

        public FastReadOnlyMemoryBuffer(ReadOnlyMemory<T> baseMemory)
        {
            InternalBuffer = baseMemory;
            CurrentPosition = 0;
            Length = baseMemory.Length;
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

        public ReadOnlySpan<T> Read(int size, bool allowPartial = false)
        {
            int sizeRead = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allowPartial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                sizeRead = Length - CurrentPosition;
            }

            ReadOnlySpan<T> ret = InternalSpan.Slice(CurrentPosition, sizeRead);
            CurrentPosition += sizeRead;
            return ret;
        }

        public ReadOnlySpan<T> Peek(int size, bool allowPartial = false)
        {
            int sizeRead = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allowPartial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                sizeRead = Length - CurrentPosition;
            }

            ReadOnlySpan<T> ret = InternalSpan.Slice(CurrentPosition, sizeRead);
            return ret;
        }

        public ReadOnlyMemory<T> ReadAsMemory(int size, bool allowPartial = false)
        {
            int sizeRead = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allowPartial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                sizeRead = Length - CurrentPosition;
            }

            ReadOnlyMemory<T> ret = InternalBuffer.Slice(CurrentPosition, sizeRead);
            CurrentPosition += sizeRead;
            return ret;
        }

        public ReadOnlyMemory<T> PeekAsMemory(int size, bool allowPartial = false)
        {
            int sizeRead = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allowPartial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                sizeRead = Length - CurrentPosition;
            }

            ReadOnlyMemory<T> ret = InternalBuffer.Slice(CurrentPosition, sizeRead);
            return ret;
        }

        public void Seek(int offset, SeekOrigin mode)
        {
            int newPosition = 0;
            if (mode == SeekOrigin.Current)
                newPosition = checked(CurrentPosition + offset);
            else if (mode == SeekOrigin.End)
                newPosition = checked(Length + offset);
            else
                newPosition = offset;

            if (newPosition < 0) throw new ArgumentOutOfRangeException("newPosition < 0");
            if (newPosition > Length) throw new ArgumentOutOfRangeException("newPosition > Size");

            CurrentPosition = newPosition;
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

        public MemoryBuffer(Memory<T> baseMemory)
        {
            InternalBuffer = baseMemory;
            CurrentPosition = 0;
            Length = baseMemory.Length;
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
        public Span<T> Walk(int size, bool noMove = false)
        {
            int newSize = checked(CurrentPosition + size);
            if (InternalBuffer.Length < newSize)
            {
                EnsureInternalBufferReserved(newSize);
            }
            var ret = InternalBuffer.Span.Slice(CurrentPosition, size);
            Length = Math.Max(newSize, Length);
            if (noMove == false) CurrentPosition += size;
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

        public ReadOnlySpan<T> Read(int size, bool allowPartial = false)
        {
            int sizeRead = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allowPartial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                sizeRead = Length - CurrentPosition;
            }

            ReadOnlySpan<T> ret = InternalBuffer.Span.Slice(CurrentPosition, sizeRead);
            CurrentPosition += sizeRead;
            return ret;
        }

        public ReadOnlySpan<T> Peek(int size, bool allowPartial = false)
        {
            int sizeRead = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allowPartial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                sizeRead = Length - CurrentPosition;
            }

            Span<T> ret = InternalBuffer.Span.Slice(CurrentPosition, sizeRead);
            return ret;
        }

        public ReadOnlyMemory<T> ReadAsMemory(int size, bool allowPartial = false)
        {
            int sizeRead = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allowPartial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                sizeRead = Length - CurrentPosition;
            }

            ReadOnlyMemory<T> ret = InternalBuffer.Slice(CurrentPosition, sizeRead);
            CurrentPosition += sizeRead;
            return ret;
        }

        public ReadOnlyMemory<T> PeekAsMemory(int size, bool allowPartial = false)
        {
            int sizeRead = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allowPartial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                sizeRead = Length - CurrentPosition;
            }

            ReadOnlyMemory<T> ret = InternalBuffer.Slice(CurrentPosition, sizeRead);
            return ret;
        }

        public void Seek(int offset, SeekOrigin mode)
        {
            int newPosition = 0;
            if (mode == SeekOrigin.Current)
                newPosition = checked(CurrentPosition + offset);
            else if (mode == SeekOrigin.End)
                newPosition = checked(Length + offset);
            else
                newPosition = offset;

            if (newPosition < 0) throw new ArgumentOutOfRangeException("newPosition < 0");
            if (newPosition > Length) throw new ArgumentOutOfRangeException("newPosition > Size");

            CurrentPosition = newPosition;
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

        public void EnsureInternalBufferReserved(int newSize)
        {
            if (InternalBuffer.Length >= newSize) return;

            int newInternalSize = InternalBuffer.Length;
            while (newInternalSize < newSize)
                newInternalSize = checked(Math.Max(newInternalSize, 128) * 2);

            InternalBuffer = InternalBuffer.ReAlloc(newInternalSize);
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

        public ReadOnlyMemoryBuffer(ReadOnlyMemory<T> baseMemory)
        {
            InternalBuffer = baseMemory;
            CurrentPosition = 0;
            Length = baseMemory.Length;
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

        public ReadOnlySpan<T> Read(int size, bool allowPartial = false)
        {
            int sizeRead = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allowPartial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                sizeRead = Length - CurrentPosition;
            }

            ReadOnlySpan<T> ret = InternalBuffer.Span.Slice(CurrentPosition, sizeRead);
            CurrentPosition += sizeRead;
            return ret;
        }

        public ReadOnlySpan<T> Peek(int size, bool allowPartial = false)
        {
            int sizeRead = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allowPartial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                sizeRead = Length - CurrentPosition;
            }

            ReadOnlySpan<T> ret = InternalBuffer.Span.Slice(CurrentPosition, sizeRead);
            return ret;
        }

        public ReadOnlyMemory<T> ReadAsMemory(int size, bool allowPartial = false)
        {
            int sizeRead = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allowPartial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                sizeRead = Length - CurrentPosition;
            }

            ReadOnlyMemory<T> ret = InternalBuffer.Slice(CurrentPosition, sizeRead);
            CurrentPosition += sizeRead;
            return ret;
        }

        public ReadOnlyMemory<T> PeekAsMemory(int size, bool allowPartial = false)
        {
            int sizeRead = size;
            if (checked(CurrentPosition + size) > Length)
            {
                if (allowPartial == false) throw new ArgumentOutOfRangeException("(CurrentPosition + size) > Size");
                sizeRead = Length - CurrentPosition;
            }

            ReadOnlyMemory<T> ret = InternalBuffer.Slice(CurrentPosition, sizeRead);
            return ret;
        }

        public void Seek(int offset, SeekOrigin mode)
        {
            int newPosition = 0;
            if (mode == SeekOrigin.Current)
                newPosition = checked(CurrentPosition + offset);
            else if (mode == SeekOrigin.End)
                newPosition = checked(Length + offset);
            else
                newPosition = offset;

            if (newPosition < 0) throw new ArgumentOutOfRangeException("newPosition < 0");
            if (newPosition > Length) throw new ArgumentOutOfRangeException("newPosition > Size");

            CurrentPosition = newPosition;
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

        public static int WalkGetCurrentLength<T>(this Memory<T> memory, int compareTargetPin) => WalkGetCurrentLength(memory.AsReadOnlyMemory(), compareTargetPin);


        public static int WalkGetCurrentLength<T>(this ReadOnlyMemory<T> memory, int compareTargetPin)
        {
            int currentPin = memory.WalkGetPin();
            if (currentPin < compareTargetPin) throw new ArgumentOutOfRangeException("currentPin < compareTargetPin");
            return currentPin - compareTargetPin;
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


        static Memory<T> WalkAutoInternal<T>(ref this Memory<T> memory, int size, bool noReAlloc, bool noStep)
        {
            if (size == 0) return Memory<T>.Empty;
            if (size < 0) throw new ArgumentOutOfRangeException("size");
            if (memory.Length >= size)
            {
                return memory.Walk(size);
            }

            if (((long)memory.Length + (long)size) > int.MaxValue) throw new OverflowException("size");

            ArraySegment<T> a = memory.AsSegment();
            long requiredLen = (long)a.Offset + (long)a.Count + (long)size;
            if (requiredLen > int.MaxValue) throw new OverflowException("size");

            int newLen = a.Array.Length;
            while (newLen < requiredLen)
            {
                newLen = (int)Math.Min(Math.Max((long)newLen, 128) * 2, int.MaxValue);
            }

            T[] newArray = a.Array;
            if (newArray.Length < newLen)
            {
                if (noReAlloc)
                {
                    throw new ArgumentOutOfRangeException("Internal byte array overflow: array.Length < newLen");
                }
                newArray = a.Array.ReAlloc(newLen);
            }

            if (noStep == false)
            {
                a = new ArraySegment<T>(newArray, a.Offset, Math.Max(a.Count, size));
            }
            else
            {
                a = new ArraySegment<T>(newArray, a.Offset, a.Count);
            }

            var m = a.AsMemory();

            if (noStep == false)
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

        public static T[] ReAlloc<T>(this T[] src, int newSize)
        {
            if (newSize < 0) throw new ArgumentOutOfRangeException("newSize");
            if (newSize == src.Length)
            {
                return src;
            }

            T[] ret = src;
            Array.Resize(ref ret, newSize);
            return ret;
        }

        public static Span<T> ReAlloc<T>(this Span<T> src, int newSize)
        {
            if (newSize < 0) throw new ArgumentOutOfRangeException("newSize");
            if (newSize == src.Length)
            {
                return src;
            }
            else
            {
                T[] ret = new T[newSize];
                src.Slice(0, Math.Min(src.Length, ret.Length)).CopyTo(ret);
                return ret.AsSpan();
            }
        }

        public static Memory<T> ReAlloc<T>(this Memory<T> src, int newSize)
        {
            if (newSize < 0) throw new ArgumentOutOfRangeException("newSize");
            if (newSize == src.Length)
            {
                return src;
            }
            else
            {
                T[] ret = new T[newSize];
                src.Slice(0, Math.Min(src.Length, ret.Length)).CopyTo(ret);
                return ret.AsMemory();
            }
        }

        public const int MemoryUsePoolThreshold = 1024;

        public static T[] FastAlloc<T>(int minimumSize)
        {
            if (minimumSize < MemoryUsePoolThreshold)
                return new T[minimumSize];
            else
                return ArrayPool<T>.Shared.Rent(minimumSize);
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


        static readonly long _MemoryObjectOffset;
        static readonly long _MemoryIndexOffset;
        static readonly long _MemoryLengthOffset;
        static readonly bool _UseFast = false;

        static unsafe MemoryHelper()
        {
            try
            {
                _MemoryObjectOffset = Marshal.OffsetOf<Memory<byte>>("_object").ToInt64();
                _MemoryIndexOffset = Marshal.OffsetOf<Memory<byte>>("_index").ToInt64();
                _MemoryLengthOffset = Marshal.OffsetOf<Memory<byte>>("_length").ToInt64();

                if (_MemoryObjectOffset != Marshal.OffsetOf<ReadOnlyMemory<DummyValueType>>("_object").ToInt64() ||
                    _MemoryIndexOffset != Marshal.OffsetOf<ReadOnlyMemory<DummyValueType>>("_index").ToInt64() ||
                    _MemoryLengthOffset != Marshal.OffsetOf<ReadOnlyMemory<DummyValueType>>("_length").ToInt64())
                {
                    throw new Exception();
                }

                _UseFast = true;
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
                    _MemoryObjectOffset = 0;
                    _MemoryIndexOffset = sizeof(void*);
                    _MemoryLengthOffset = _MemoryIndexOffset + sizeof(int);

                    _UseFast = true;
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

                void* memPtr = Unsafe.AsPointer(ref mem);

                byte* p = (byte*)memPtr;
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

                void* memPtr = Unsafe.AsPointer(ref mem);

                byte* p = (byte*)memPtr;
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
                ptr += _MemoryObjectOffset;
                T[] o = Unsafe.Read<T[]>(ptr);
                return o;
            }
        }
        public static int GetInternalArrayLength<T>(this Memory<T> memory) => GetInternalArray(memory).Length;

        public static ArraySegment<T> AsSegment<T>(this Memory<T> memory)
        {
            if (_UseFast == false) return AsSegmentSlow(memory);

            unsafe
            {
                byte* ptr = (byte*)Unsafe.AsPointer(ref memory);
                return new ArraySegment<T>(
                    Unsafe.Read<T[]>(ptr + _MemoryObjectOffset),
                    *((int*)(ptr + _MemoryIndexOffset)),
                    *((int*)(ptr + _MemoryLengthOffset))
                    );
            }
        }

        public static ArraySegment<T> AsSegment<T>(this ReadOnlyMemory<T> memory)
        {
            if (_UseFast == false) return AsSegmentSlow(memory);

            unsafe
            {
                byte* ptr = (byte*)Unsafe.AsPointer(ref memory);
                return new ArraySegment<T>(
                    Unsafe.Read<T[]>(ptr + _MemoryObjectOffset),
                    *((int*)(ptr + _MemoryIndexOffset)),
                    *((int*)(ptr + _MemoryLengthOffset))
                    );
            }
        }

        public static List<List<Memory<T>>> SplitMemoryArray<T>(IEnumerable<Memory<T>> src, int elementMaxSize)
        {
            elementMaxSize = Math.Max(1, elementMaxSize);

            int currentSize = 0;
            List<List<Memory<T>>> ret = new List<List<Memory<T>>>();
            List<Memory<T>> currentList = new List<Memory<T>>();

            foreach (Memory<T> mSrc in src)
            {
                Memory<T> m = mSrc;

                LABEL_START:

                if (m.Length >= 1)
                {
                    int overSize = (currentSize + m.Length) - elementMaxSize;
                    if (overSize >= 0)
                    {
                        Memory<T> mAdd = m.Slice(0, m.Length - overSize);

                        currentList.Add(mAdd);
                        ret.Add(currentList);
                        currentList = new List<Memory<T>>();
                        currentSize = 0;

                        m = m.Slice(mAdd.Length);

                        goto LABEL_START;
                    }
                    else
                    {
                        currentList.Add(m);
                        currentSize += m.Length;
                    }
                }
            }

            if (currentList.Count >= 1)
                ret.Add(currentList);

            return ret;
        }

        public static List<List<ArraySegment<T>>> SplitMemoryArrayToArraySegment<T>(IEnumerable<Memory<T>> src, int elementMaxSize)
        {
            elementMaxSize = Math.Max(1, elementMaxSize);

            int currentSize = 0;
            List<List<ArraySegment<T>>> ret = new List<List<ArraySegment<T>>>();
            List<ArraySegment<T>> currentList = new List<ArraySegment<T>>();

            foreach (Memory<T> mSrc in src)
            {
                Memory<T> m = mSrc;

                LABEL_START:

                if (m.Length >= 1)
                {
                    int overSize = (currentSize + m.Length) - elementMaxSize;
                    if (overSize >= 0)
                    {
                        Memory<T> mAdd = m.Slice(0, m.Length - overSize);

                        currentList.Add(mAdd.AsSegment());
                        ret.Add(currentList);
                        currentList = new List<ArraySegment<T>>();
                        currentSize = 0;

                        m = m.Slice(mAdd.Length);

                        goto LABEL_START;
                    }
                    else
                    {
                        currentList.Add(m.AsSegment());
                        currentSize += m.Length;
                    }
                }
            }

            if (currentList.Count >= 1)
                ret.Add(currentList);

            return ret;
        }
    }

    public sealed class FastMemoryAllocator<T>
    {
        Memory<T> Pool;
        int CurrentPos;
        int MinReserveSize;

        public FastMemoryAllocator(int initialSize = 0)
        {
            initialSize = Math.Min(initialSize, 1);
            Pool = new T[initialSize];
            MinReserveSize = initialSize;
        }

        public Memory<T> Reserve(int maxSize)
        {
            checked
            {
                if (maxSize < 0) throw new ArgumentOutOfRangeException("size");
                if (maxSize == 0) return Memory<T>.Empty;

                Debug.Assert((Pool.Length - CurrentPos) >= 0);

                if ((Pool.Length - CurrentPos) < maxSize)
                {
                    MinReserveSize = Math.Max(MinReserveSize, maxSize * 5);
                    Pool = new T[MinReserveSize];
                    CurrentPos = 0;
                }

                var ret = Pool.Slice(CurrentPos, maxSize);
                CurrentPos += maxSize;
                return ret;
            }
        }

        public void Commit(ref Memory<T> reservedMemory, int commitSize)
        {
            reservedMemory = Commit(reservedMemory, commitSize);
        }

        public Memory<T> Commit(Memory<T> reservedMemory, int commitSize)
        {
            checked
            {
                int returnSize = reservedMemory.Length - commitSize;
                Debug.Assert(returnSize >= 0);
                if (returnSize == 0) return reservedMemory;

                CurrentPos -= returnSize;
                Debug.Assert(CurrentPos >= 0);

                if (commitSize >= 1)
                    return reservedMemory.Slice(0, commitSize);
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
            uint value16bit = (value >> 16) & 0xFFFF;

            uint stateCopy = state;

            uint state16bit = (stateCopy >> 16) & 0xFFFF;
            uint rotate16bit = stateCopy & 0xFFFF;

            if (value16bit <= 0x1000 && state16bit >= 0xF000)
            {
                rotate16bit++;
            }

            uint stateNew = (value16bit << 16) & 0xFFFF0000 | rotate16bit & 0x0000FFFF;

            state = stateNew;

            return (long)value + 0x100000000L * (long)rotate16bit;
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

        public static int Xor(params int[] hashList)
        {
            int ret = 0;
            foreach (var i in hashList)
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

    public class RefInt : IEquatable<RefInt>, IComparable<RefInt>
    {
        public RefInt() : this(0) { }
        public RefInt(int value)
        {
            this.Value = value;
        }
        public volatile int Value;
        public void Set(int value) => this.Value = value;
        public int Get() => this.Value;
        public override string ToString() => this.Value.ToString();
        public int Increment() => Interlocked.Increment(ref this.Value);
        public int Decrement() => Interlocked.Decrement(ref this.Value);

        public override bool Equals(object obj) => obj is RefInt x && this.Value == x.Value;
        public override int GetHashCode() => Value.GetHashCode();

        public bool Equals(RefInt other) => this.Value.Equals(other.Value);
        public int CompareTo(RefInt other) => this.Value.CompareTo(other.Value);

        public static bool operator ==(RefInt left, int right) => left.Value == right;
        public static bool operator !=(RefInt left, int right) => left.Value != right;
        public static implicit operator int(RefInt r) => r.Value;
        public static implicit operator RefInt(int value) => new RefInt(value);
    }

    public class RefLong : IEquatable<RefLong>, IComparable<RefLong>
    {
        public RefLong() : this(0) { }
        public RefLong(long value)
        {
            this.Value = value;
        }
        long _value;
        public long Value { get => Get(); set => Set(value); }
        public void Set(long value) => Interlocked.Exchange(ref this._value, value);
        public long Get() => Interlocked.Read(ref this._value);
        public override string ToString() => this.Value.ToString();
        public long Increment() => Interlocked.Increment(ref this._value);
        public long Decrement() => Interlocked.Decrement(ref this._value);

        public override bool Equals(object obj) => obj is RefLong x && this.Value == x.Value;
        public override int GetHashCode() => Value.GetHashCode();

        public bool Equals(RefLong other) => this.Value.Equals(other.Value);
        public int CompareTo(RefLong other) => this.Value.CompareTo(other.Value);

        public static bool operator ==(RefLong left, long right) => left.Value == right;
        public static bool operator !=(RefLong left, long right) => left.Value != right;
        public static implicit operator long(RefLong r) => r.Value;
        public static implicit operator RefLong(long value) => new RefLong(value);
    }

    public class RefBool : IEquatable<RefBool>, IComparable<RefBool>
    {
        public RefBool() : this(false) { }
        public RefBool(bool value)
        {
            this.Value = value;
        }
        public volatile bool Value;
        public void Set(bool value) => this.Value = value;
        public bool Get() => this.Value;
        public override string ToString() => this.Value.ToString();

        public override bool Equals(object obj) => obj is RefBool x && this.Value == x.Value;
        public override int GetHashCode() => Value.GetHashCode();

        public bool Equals(RefBool other) => this.Value.Equals(other.Value);
        public int CompareTo(RefBool other) => this.Value.CompareTo(other.Value);

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
                int i, needSize;
                bool reallocFlag;

                i = this.size;
                this.size += size;
                needSize = this.pos + this.size;
                reallocFlag = false;

                int memsize = p.Length;
                while (needSize > memsize)
                {
                    memsize = Math.Max(memsize, FifoInitMemSize) * 3;
                    reallocFlag = true;
                }

                if (reallocFlag)
                {
                    byte[] newArray = new byte[memsize];
                    WebSocketHelper.CopyByte(newArray, 0, this.p, 0, this.p.Length);
                    this.p = newArray;
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
            int readSize = Read(ret);
            Array.Resize<byte>(ref ret, readSize);

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
                int readSize;

                readSize = Math.Min(size, this.size);
                if (readSize == 0)
                {
                    return 0;
                }
                if (dst != null)
                {
                    WebSocketHelper.CopyByte(dst, offset, this.p, this.pos, readSize);
                }
                this.pos += readSize;
                this.size -= readSize;

                if (this.size == 0)
                {
                    this.pos = 0;
                }

                if (this.pos >= FifoInitMemSize &&
                    this.p.Length >= this.reallocMemSize &&
                    (this.p.Length / 2) > this.size)
                {
                    byte[] newArray;
                    int newSize;

                    newSize = Math.Max(this.p.Length / 2, FifoInitMemSize);
                    newArray = new byte[newSize];
                    WebSocketHelper.CopyByte(newArray, 0, this.p, this.pos, this.size);

                    this.p = newArray;

                    this.pos = 0;
                }

                totalReadSize += readSize;

                return readSize;
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

            Once DisposeFlag;
            public void Dispose()
            {
                if (DisposeFlag.IsFirstCall())
                {
                    this.parent.Unlock();
                }
            }
        }

        SemaphoreSlim semaphone = new SemaphoreSlim(1, 1);
        Once DisposeFlag;

        public async Task<LockHolder> LockWithAwait()
        {
            await LockAsync();

            return new LockHolder(this);
        }

        public Task LockAsync() => semaphone.WaitAsync();
        public void Unlock() => semaphone.Release();

        public void Dispose()
        {
            if (DisposeFlag.IsFirstCall())
            {
                semaphone.DisposeSafe();
                semaphone = null;
            }
        }
    }

    public static class Dbg
    {
        static object LockObj = new object();

        public static long Where(object msgObj = null, [CallerFilePath] string filename = "", [CallerLineNumber] int line = 0, [CallerMemberName] string caller = null, long lastTick = 0)
        {
            string msg = "";
            if (msgObj != null) msg = msgObj.ToString();
            lock (LockObj)
            {
                long now = DateTime.Now.Ticks;
                long diff = now - lastTick;
                WriteLine($"{FastTick64.Now}  {Path.GetFileName(filename)}:{line} in {caller}()" + (lastTick == 0 ? "" : $" (took {diff} msecs) ") + (string.IsNullOrEmpty(msg) == false ? (": " + msg) : ""));
                return now;
            }
        }
    }

    [Flags]
    public enum ExceptionWhen
    {
        None = 0,
        TaskException = 1,
        CancelException = 2,
        TimeoutException = 4,
        All = 0x7FFFFFFF,
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

        public static string ObjectToJson(this object obj, bool includeNull = false, bool escapeHtml = false, int? maxDepth = DefaultMaxDepth, bool compact = false, bool referenceHandling = false) => Serialize(obj, includeNull, escapeHtml, maxDepth, compact, referenceHandling);
        public static T JsonToObject<T>(this string str, bool includeNull = false, int? maxDepth = DefaultMaxDepth) => Deserialize<T>(str, includeNull, maxDepth);
        public static object JsonToObject(this string str, Type type, bool includeNull = false, int? maxDepth = DefaultMaxDepth) => Deserialize(str, type, includeNull, maxDepth);

        public static string Serialize(object obj, bool includeNull = false, bool escapeHtml = false, int? maxDepth = DefaultMaxDepth, bool compact = false, bool referenceHandling = false)
        {
            JsonSerializerSettings setting = new JsonSerializerSettings()
            {
                MaxDepth = maxDepth,
                NullValueHandling = includeNull ? NullValueHandling.Include : NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Error,
                PreserveReferencesHandling = referenceHandling ? PreserveReferencesHandling.All : PreserveReferencesHandling.None,
                StringEscapeHandling = escapeHtml ? StringEscapeHandling.EscapeHtml : StringEscapeHandling.Default,
            };
            return JsonConvert.SerializeObject(obj, compact ? Formatting.None : Formatting.Indented, setting);
        }

        public static T Deserialize<T>(string str, bool includeNull = false, int? maxDepth = DefaultMaxDepth)
            => (T)Deserialize(str, typeof(T), includeNull, maxDepth);

        public static object Deserialize(string str, Type type, bool includeNull = false, int? maxDepth = DefaultMaxDepth)
        {
            JsonSerializerSettings setting = new JsonSerializerSettings()
            {
                MaxDepth = maxDepth,
                NullValueHandling = includeNull ? NullValueHandling.Include : NullValueHandling.Ignore,
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

        public static T[] ToSingleArray<T>(this T t) => new T[] { t };

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

        static readonly IPEndPoint StaticUdpEndPointIPv4 = new IPEndPoint(IPAddress.Any, 0);
        static readonly IPEndPoint StaticUdpEndPointIPv6 = new IPEndPoint(IPAddress.IPv6Any, 0);
        const int UdpMaxRetryOnIgnoreError = 1000;
        public static async Task<SocketReceiveFromResult> ReceiveFromSafeUdpErrorAsync(this Socket socket, ArraySegment<byte> buffer, SocketFlags socketFlags)
        {
            int numRetry = 0;

            LABEL_RETRY:

            try
            {
                Task<SocketReceiveFromResult> t = socket.ReceiveFromAsync(buffer, socketFlags, socket.AddressFamily == AddressFamily.InterNetworkV6 ? StaticUdpEndPointIPv6 : StaticUdpEndPointIPv4);
                if (t.IsCompleted == false)
                {
                    numRetry = 0;
                    await t;
                }
                SocketReceiveFromResult ret = t.Result;
                if (ret.ReceivedBytes <= 0) throw new SocketDisconnectedException();
                return ret;
            }
            catch (SocketException e) when (CanUdpSocketErrorBeIgnored(e) || socket.Available >= 1)
            {
                numRetry++;
                if (numRetry >= UdpMaxRetryOnIgnoreError)
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
            int timeout = Timeout.Infinite, CancellationToken cancel = default(CancellationToken), params CancellationToken[] cancelTokens)
        {
            await DoAsyncWithTimeout(
            mainProc: async c =>
            {
                await tc.ConnectAsync(host, port);
                return 0;
            },
            cancelProc: () =>
            {
                tc.DisposeSafe();
            },
            timeout: timeout,
            cancel: cancel,
            cancelTokens: cancelTokens);
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
            Task.Run(() =>
            {
                try
                {
                    stream.Close();
                }
                catch
                {
                }
            });
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

        public static async Task TryWaitAsync(this Task t, bool noDebugMessage = false)
        {
            if (t == null) return;
            try
            {
                await t;
            }
            catch (Exception ex)
            {
                if (noDebugMessage == false)
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

        public static async Task<byte[]> ReadAsyncWithTimeout(this Stream stream, int maxSize = 65536, int? timeout = null, bool? readAll = false, CancellationToken cancel = default(CancellationToken))
        {
            byte[] tmp = new byte[maxSize];
            int ret = await stream.ReadAsyncWithTimeout(tmp, 0, tmp.Length, timeout,
                readAll: readAll,
                cancel: cancel);
            return CopyByte(tmp, 0, ret);
        }

        public static async Task<int> ReadAsyncWithTimeout(this Stream stream, byte[] buffer, int offset = 0, int? count = null, int? timeout = null, bool? readAll = false, CancellationToken cancel = default(CancellationToken), params CancellationToken[] cancelTokens)
        {
            if (timeout == null) timeout = stream.ReadTimeout;
            if (timeout <= 0) timeout = Timeout.Infinite;
            int targetReadSize = count ?? (buffer.Length - offset);
            if (targetReadSize == 0) return 0;

            try
            {
                int ret = await DoAsyncWithTimeout(async (cancelLocal) =>
                {
                    if (readAll == false)
                    {
                        return await stream.ReadAsync(buffer, offset, targetReadSize, cancelLocal);
                    }
                    else
                    {
                        int currentReadSize = 0;

                        while (currentReadSize != targetReadSize)
                        {
                            int sz = await stream.ReadAsync(buffer, offset + currentReadSize, targetReadSize - currentReadSize, cancelLocal);
                            if (sz == 0)
                            {
                                return 0;
                            }

                            currentReadSize += sz;
                        }

                        return currentReadSize;
                    }
                },
                timeout: (int)timeout,
                cancel: cancel,
                cancelTokens: cancelTokens);

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

        public static async Task WriteAsyncWithTimeout(this Stream stream, byte[] buffer, int offset = 0, int? count = null, int? timeout = null, CancellationToken cancel = default(CancellationToken), params CancellationToken[] cancelTokens)
        {
            if (timeout == null) timeout = stream.WriteTimeout;
            if (timeout <= 0) timeout = Timeout.Infinite;
            int targetWriteSize = count ?? (buffer.Length - offset);
            if (targetWriteSize == 0) return;

            try
            {
                await DoAsyncWithTimeout(async (cancelLocal) =>
                {
                    await stream.WriteAsync(buffer, offset, targetWriteSize, cancelLocal);
                    return 0;
                },
                timeout: (int)timeout,
                cancel: cancel,
                cancelTokens: cancelTokens);

            }
            catch
            {
                stream.TryCloseNonBlock();
                throw;
            }
        }

        public static async Task<TResult> DoAsyncWithTimeout<TResult>(Func<CancellationToken, Task<TResult>> mainProc, Action cancelProc = null, int timeout = Timeout.Infinite, CancellationToken cancel = default(CancellationToken), params CancellationToken[] cancelTokens)
        {
            if (timeout < 0) timeout = Timeout.Infinite;
            if (timeout == 0) throw new TimeoutException("timeout == 0");

            List<Task> waitTasks = new List<Task>();
            List<IDisposable> disposes = new List<IDisposable>();
            Task timeoutTask = null;
            CancellationTokenSource timeoutCancelSources = null;
            CancellationTokenSource cancelLocal = new CancellationTokenSource();

            if (timeout != Timeout.Infinite)
            {
                timeoutCancelSources = new CancellationTokenSource();
                timeoutTask = Task.Delay(timeout, timeoutCancelSources.Token);
                disposes.Add(timeoutCancelSources);

                waitTasks.Add(timeoutTask);
            }

            try
            {
                if (cancel.CanBeCanceled)
                {
                    cancel.ThrowIfCancellationRequested();

                    Task t = WhenCanceled(cancel, out CancellationTokenRegistration reg);
                    disposes.Add(reg);
                    waitTasks.Add(t);
                }

                foreach (CancellationToken c in cancelTokens)
                {
                    if (c.CanBeCanceled)
                    {
                        c.ThrowIfCancellationRequested();

                        Task t = WhenCanceled(c, out CancellationTokenRegistration reg);
                        disposes.Add(reg);
                        waitTasks.Add(t);
                    }
                }

                Task<TResult> procTask = mainProc(cancelLocal.Token);

                if (procTask.IsCompleted)
                {
                    return procTask.Result;
                }

                waitTasks.Add(procTask);

                await Task.WhenAny(waitTasks.ToArray());

                foreach (CancellationToken c in cancelTokens)
                {
                    c.ThrowIfCancellationRequested();
                }

                cancel.ThrowIfCancellationRequested();

                if (procTask.IsCompleted)
                {
                    return procTask.Result;
                }

                throw new TimeoutException();
            }
            catch
            {
                try
                {
                    cancelLocal.Cancel();
                }
                catch { }
                try
                {
                    if (cancelProc != null) cancelProc();
                }
                catch
                {
                }
                throw;
            }
            finally
            {
                if (timeoutCancelSources != null)
                {
                    try
                    {
                        timeoutCancelSources.Cancel();
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
            Task.Run(async () =>
            {
                try
                {
                    await task;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("LaissezFaire: " + ex.ToString());
                }
            });
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

        public static void ThrowIfErrorOrCanceled(this Task task)
        {
            if (task == null) return;
            if (task.IsFaulted) task.Exception.ReThrow();
            if (task.IsCanceled) throw new TaskCanceledException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Bit<T>(this T value, T flag) where T: Enum
            => value.HasFlag(flag);

        public static async Task WaitObjectsAsync(Task[] tasks = null, CancellationToken[] cancels = null, AsyncAutoResetEvent[] events = null,
            AsyncManualResetEvent[] manualEvents = null, int timeout = Timeout.Infinite,
            ExceptionWhen exceptions = ExceptionWhen.None)
        {
            if (tasks == null) tasks = new Task[0];
            if (cancels == null) cancels = new CancellationToken[0];
            if (events == null) events = new AsyncAutoResetEvent[0];
            if (manualEvents == null) manualEvents = new AsyncManualResetEvent[0];
            if (timeout == 0)
            {
                if (exceptions.Bit(ExceptionWhen.TimeoutException))
                    throw new TimeoutException();
                return;
            }

            if (exceptions.Bit(ExceptionWhen.TaskException))
            {
                foreach (Task t in tasks)
                {
                    if (t != null)
                    {
                        if (t.IsFaulted) t.Exception.ReThrow();
                        if (t.IsCanceled) throw new TaskCanceledException();
                    }
                }
            }

            if (exceptions.Bit(ExceptionWhen.CancelException))
            {
                foreach (CancellationToken c in cancels)
                    c.ThrowIfCancellationRequested();
            }

            List<Task> taskList = new List<Task>();
            List<CancellationTokenRegistration> regList = new List<CancellationTokenRegistration>();
            List<Action> undoList = new List<Action>();

            foreach (Task t in tasks)
            {
                if (t != null)
                {
                    taskList.Add(t);
                }
            }

            foreach (CancellationToken c in cancels)
            {
                taskList.Add(WhenCanceled(c, out CancellationTokenRegistration reg));
                regList.Add(reg);
            }

            foreach (AsyncAutoResetEvent ev in events)
            {
                if (ev != null)
                {
                    taskList.Add(ev.WaitOneAsync(out Action undo));
                    undoList.Add(undo);
                }
            }

            foreach (AsyncManualResetEvent ev in manualEvents)
            {
                if (ev != null)
                {
                    taskList.Add(ev.WaitAsync());
                }
            }

            CancellationTokenSource delayCancel = new CancellationTokenSource();

            Task timeoutTask = null;
            bool timedOut = false;

            if (timeout >= 1)
            {
                timeoutTask = Task.Delay(timeout, delayCancel.Token);
                taskList.Add(timeoutTask);
            }

            try
            {
                Task r = await Task.WhenAny(taskList.ToArray());
                if (r == timeoutTask) timedOut = true;
            }
            catch { }
            finally
            {
                foreach (Action undo in undoList)
                    undo();

                foreach (CancellationTokenRegistration reg in regList)
                {
                    reg.Dispose();
                }

                if (delayCancel != null)
                {
                    delayCancel.Cancel();
                    delayCancel.Dispose();
                }

                if (exceptions.Bit(ExceptionWhen.TimeoutException))
                    if (timedOut)
                        throw new TimeoutException();

                if (exceptions.Bit(ExceptionWhen.TaskException))
                {
                    foreach (Task t in tasks)
                    {
                        if (t != null)
                        {
                            if (t.IsFaulted) t.Exception.ReThrow();
                            if (t.IsCanceled) throw new TaskCanceledException();
                        }
                    }
                }

                if (exceptions.Bit(ExceptionWhen.CancelException))
                {
                    foreach (CancellationToken c in cancels)
                        c.ThrowIfCancellationRequested();
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

        public static void ReThrow(this Exception ex)
        {
            if (ex == null) throw ex;
            ExceptionDispatchInfo.Capture(ex.GetSingleException()).Throw();
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
            long minValue = long.MaxValue;
            foreach (int v in values)
            {
                long vv;
                if (v < 0)
                    vv = long.MaxValue;
                else
                    vv = v;
                minValue = Math.Min(minValue, vv);
            }
            if (minValue == long.MaxValue)
                return Timeout.Infinite;
            else
                return (int)minValue;
        }

        public static T NewWithoutConstructor<T>()
            => (T)NewWithoutConstructor(typeof(T));

        public static object NewWithoutConstructor(Type t)
            => System.Runtime.Serialization.FormatterServices.GetUninitializedObject(t);
    }

    public sealed class WhenAll : IDisposable
    {
        public Task WaitMe { get; }
        public bool AllOk { get; private set; } = false;
        public bool HasError { get => !AllOk; }

        CancellationTokenSource CancelSource = new CancellationTokenSource();

        public WhenAll(IEnumerable<Task> tasks, bool throwException = false) : this(throwException, tasks.ToArray()) { }

        public WhenAll(Task t, bool throwException = false) : this(throwException, t.ToSingleArray()) { }

        public static Task Await(IEnumerable<Task> tasks, bool throwException = false)
            => Await(throwException, tasks.ToArray());

        public static Task Await(Task t, bool throwException = false)
            => Await(throwException, t.ToSingleArray());

        public static async Task Await(bool throwException = false, params Task[] tasks)
        {
            using (var w = new WhenAll(throwException, tasks))
                await w.WaitMe;
        }

        public WhenAll(bool throwException = false, params Task[] tasks)
        {
            this.WaitMe = WaitMain(tasks, throwException);
        }

        async Task WaitMain(Task[] tasks, bool throwException)
        {
            Task cancelTask = WebSocketHelper.WhenCanceled(CancelSource.Token, out CancellationTokenRegistration reg);
            using (reg)
            {
                bool allOk = true;
                foreach (Task t in tasks)
                {
                    if (t != null)
                    {
                        try
                        {
                            await Task.WhenAny(t, cancelTask);
                        }
                        catch { }

                        if (throwException)
                        {
                            if (t.IsFaulted)
                                t.Exception.ReThrow();
                            if (t.IsCanceled)
                                throw new TaskCanceledException();
                        }

                        if (t.IsCompletedSuccessfully == false)
                            allOk = false;

                        if (CancelSource.Token.IsCancellationRequested)
                        {
                            allOk = false;
                            return;
                        }
                    }
                }

                AllOk = allOk;
            }
        }

        Once DisposeFlag;
        public void Dispose()
        {
            if (DisposeFlag.IsFirstCall())
            {
                CancelSource.Cancel();
            }
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

    public class AsyncCleanuperLady
    {
        ConcurrentQueue<AsyncCleanuper> CleanuperQueue = new ConcurrentQueue<AsyncCleanuper>();
        ConcurrentQueue<Task> TaskQueue = new ConcurrentQueue<Task>();

        public void Add(IAsyncCleanupable cleanupable) => Add(cleanupable.AsyncCleanuper);

        public void Add(AsyncCleanuper cleanuper)
        {
            if (cleanuper != null)
                CleanuperQueue.Enqueue(cleanuper);
        }

        public void Add(Task t)
        {
            if (t != null)
                TaskQueue.Enqueue(t);
        }

        public async Task CleanupAsync()
        {
            var q1 = CleanuperQueue.ToArray().Reverse();

            foreach (var c in q1)
                await c;

            var q2 = TaskQueue.ToArray().Reverse();
            foreach (var t in q2)
            {
                try
                {
                    await t;
                }
                catch { }
            }
        }

        public TaskAwaiter GetAwaiter()
            => CleanupAsync().GetAwaiter();
    }

    public interface IAsyncCleanupable : IDisposable
    {
        AsyncCleanuper AsyncCleanuper { get; }
        Task _CleanupAsyncInternal();
    }

    public class AsyncCleanuper
    {
        IAsyncCleanupable Target { get; }

        public AsyncCleanuper(IAsyncCleanupable targetObject)
        {
            Target = targetObject;
        }

        Task internalCleanupTask = null;
        object lockObj = new object();

        public Task CleanupAsync()
        {
            Target.DisposeSafe();

            lock (lockObj)
            {
                if (internalCleanupTask == null)
                    internalCleanupTask = Target._CleanupAsyncInternal().TryWaitAsync(true);
            }

            return internalCleanupTask;
        }

        public TaskAwaiter GetAwaiter()
            => CleanupAsync().GetAwaiter();
    }

    public delegate bool TimeoutDetectorCallback(TimeoutDetector detector);

    public sealed class TimeoutDetector : IAsyncCleanupable
    {
        Task mainLoop;

        object LockObj = new object();

        public long Timeout { get; }

        long NextTimeout;

        AsyncAutoResetEvent ev = new AsyncAutoResetEvent();

        CancellationTokenSource halt = new CancellationTokenSource();

        CancelWatcher cancelWatcher;
        AutoResetEvent eventAuto;
        ManualResetEvent eventManual;

        CancellationTokenSource cts = new CancellationTokenSource();
        public CancellationToken Cancel { get => cts.Token; }
        public Task TaskWaitMe { get => this.mainLoop; }

        public object UserState { get; }

        public bool TimedOut { get; private set; } = false;

        public AsyncCleanuper AsyncCleanuper { get; }

        TimeoutDetectorCallback Callback;

        public TimeoutDetector(int timeout, CancelWatcher watcher = null, AutoResetEvent eventAuto = null, ManualResetEvent eventManual = null,
            TimeoutDetectorCallback callback = null, object userState = null)
        {
            if (timeout == System.Threading.Timeout.Infinite || timeout == int.MaxValue)
            {
                return;
            }

            this.Timeout = timeout;
            this.cancelWatcher = watcher;
            this.eventAuto = eventAuto;
            this.eventManual = eventManual;
            this.Callback = callback;
            this.UserState = userState;

            NextTimeout = FastTick64.Now + this.Timeout;
            mainLoop = TimeoutDetectorMainLooop();

            AsyncCleanuper = new AsyncCleanuper(this);
        }

        public void Keep()
        {
            Interlocked.Exchange(ref this.NextTimeout, FastTick64.Now + this.Timeout);
        }

        async Task TimeoutDetectorMainLooop()
        {
            using (LeakChecker.Enter())
            {
                while (true)
                {
                    long nextTimeout = Interlocked.Read(ref this.NextTimeout);

                    long now = FastTick64.Now;

                    long remainTime = nextTimeout - now;

                    if (remainTime <= 0)
                    {
                        if (Callback != null && Callback(this))
                        {
                            Keep();
                        }
                        else
                        {
                            this.TimedOut = true;
                        }
                    }

                    if (this.TimedOut || halt.IsCancellationRequested)
                    {
                        cts.TryCancelAsync().LaissezFaire();

                        if (this.cancelWatcher != null) this.cancelWatcher.Cancel();
                        if (this.eventAuto != null) this.eventAuto.Set();
                        if (this.eventManual != null) this.eventManual.Set();

                        return;
                    }
                    else
                    {
                        await WebSocketHelper.WaitObjectsAsync(
                            events: new AsyncAutoResetEvent[] { ev },
                            cancels: new CancellationToken[] { halt.Token },
                            timeout: (int)remainTime);
                    }
                }
            }
        }

        Once DisposeFlag;
        public void Dispose()
        {
            if (DisposeFlag.IsFirstCall())
            {
                cancelWatcher.DisposeSafe();
                halt.TryCancelAsync().LaissezFaire();
            }
        }

        public async Task _CleanupAsyncInternal()
        {
            await mainLoop.TryWaitAsync(true);

            if (cancelWatcher != null)
                await cancelWatcher.AsyncCleanuper;
        }
    }

    public sealed class CancelWatcher : IAsyncCleanupable
    {
        CancellationTokenSource cts = new CancellationTokenSource();
        public CancellationToken CancelToken { get => cts.Token; }
        public AsyncManualResetEvent EventWaitMe { get; } = new AsyncManualResetEvent();
        public bool Canceled { get; private set; } = false;

        Task mainLoop;

        public AsyncCleanuper AsyncCleanuper { get; }

        CancellationTokenSource canceller = new CancellationTokenSource();

        AsyncAutoResetEvent ev = new AsyncAutoResetEvent();
        volatile bool halt = false;

        HashSet<CancellationToken> targetList = new HashSet<CancellationToken>();
        List<Task> taskList = new List<Task>();

        object LockObj = new object();

        public CancelWatcher(params CancellationToken[] cancels)
        {
            AddWatch(canceller.Token);
            AddWatch(cancels);

            this.mainLoop = CancelWatcherMainLoop();

            AsyncCleanuper = new AsyncCleanuper(this);
        }

        public void Cancel()
        {
            canceller.TryCancelAsync().LaissezFaire();
            this.Canceled = true;
        }

        async Task CancelWatcherMainLoop()
        {
            using (LeakChecker.Enter())
            {
                while (true)
                {
                    List<CancellationToken> cancels = new List<CancellationToken>();

                    lock (LockObj)
                    {
                        foreach (CancellationToken c in targetList)
                            cancels.Add(c);
                    }

                    await WebSocketHelper.WaitObjectsAsync(
                        cancels: cancels.ToArray(),
                        events: new AsyncAutoResetEvent[] { ev });

                    bool canceled = false;

                    lock (LockObj)
                    {
                        foreach (CancellationToken c in targetList)
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
                        this.EventWaitMe.Set(true);
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
                        if (this.targetList.Contains(cancel) == false)
                        {
                            this.targetList.Add(cancel);
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

        Once DisposeFlag;

        public void Dispose()
        {
            if (DisposeFlag.IsFirstCall())
            {
                this.halt = true;
                this.Canceled = true;
                this.ev.Set();
                this.cts.TryCancelAsync().LaissezFaire();
                this.EventWaitMe.Set(true);
            }
        }

        public async Task _CleanupAsyncInternal()
        {
            await this.mainLoop.TryWaitAsync(true);
        }
    }

    public class AsyncAutoResetEvent
    {
        object lockobj = new object();
        List<AsyncManualResetEvent> eventQueue = new List<AsyncManualResetEvent>();
        bool isSet = false;

        public AsyncCallbackList CallbackList { get; } = new AsyncCallbackList();

        public Task WaitOneAsync(out Action cancel)
        {
            lock (lockobj)
            {
                if (isSet)
                {
                    isSet = false;
                    cancel = () => { };
                    return Task.CompletedTask;
                }

                AsyncManualResetEvent e = new AsyncManualResetEvent();

                Task ret = e.WaitAsync();

                eventQueue.Add(e);

                cancel = () =>
                {
                    lock (lockobj)
                    {
                        eventQueue.Remove(e);
                    }
                };

                return ret;
            }
        }

        volatile int lazyQueuedSet = 0;


        public void SetLazy() => Interlocked.Exchange(ref lazyQueuedSet, 1);


        public void SetIfLazyQueued(bool softly = false)
        {
            if (Interlocked.CompareExchange(ref lazyQueuedSet, 0, 1) == 1)
            {
                Set(softly);
            }
        }

        public void Set(bool softly = false)
        {
            AsyncManualResetEvent ev = null;
            lock (lockobj)
            {
                if (eventQueue.Count >= 1)
                {
                    ev = eventQueue[eventQueue.Count - 1];
                    eventQueue.Remove(ev);
                }

                if (ev == null)
                {
                    isSet = true;
                }
            }

            if (ev != null)
            {
                ev.Set(softly);
            }

            CallbackList.Invoke();
        }
    }

    public class Holder<T> : IDisposable
    {
        public T UserData { get; }
        Action<T> DisposeProc;
        LeakChecker.Holder Leak;

        public Holder(Action<T> disposeProc, T userData = default(T))
        {
            this.UserData = userData;
            this.DisposeProc = disposeProc;

            Leak = LeakChecker.Enter();
        }

        Once DisposeFlag;
        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing)
        {
            if (DisposeFlag.IsFirstCall() && disposing)
            {
                try
                {
                    DisposeProc(UserData);
                }
                finally
                {
                    Leak.DisposeSafe();
                }
            }
        }
    }

    public class Holder : IDisposable
    {
        Action DisposeProc;
        LeakChecker.Holder Leak;

        public Holder(Action disposeProc)
        {
            this.DisposeProc = disposeProc;

            Leak = LeakChecker.Enter();
        }

        Once DisposeFlag;
        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing)
        {
            if (DisposeFlag.IsFirstCall() && disposing)
            {
                try
                {
                    DisposeProc();
                }
                finally
                {
                    Leak.DisposeSafe();
                }
            }
        }
    }

    public class AsyncHolder<T> : IAsyncCleanupable
    {
        public AsyncCleanuper AsyncCleanuper { get; }

        public T UserData { get; }
        Action<T> DisposeProc;
        Func<T, Task> AsyncCleanupProc;
        LeakChecker.Holder Leak;
        LeakChecker.Holder Leak2;

        public AsyncHolder(Func<T, Task> asyncCleanupProc, Action<T> disposeProc = null, T userData = default(T))
        {
            this.UserData = userData;
            this.DisposeProc = disposeProc;
            this.AsyncCleanupProc = asyncCleanupProc;

            Leak = LeakChecker.Enter();
            Leak2 = LeakChecker.Enter();
            AsyncCleanuper = new AsyncCleanuper(this);
        }

        Once DisposeFlag;

        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing)
        {
            if (DisposeFlag.IsFirstCall() && disposing)
            {
                try
                {
                    if (DisposeProc != null)
                        DisposeProc(UserData);
                }
                finally
                {
                    Leak.DisposeSafe();
                }
            }
        }

        public async Task _CleanupAsyncInternal()
        {
            try
            {
                await AsyncCleanupProc(UserData);
            }
            finally
            {
                Leak2.DisposeSafe();
            }
        }

        public TaskAwaiter GetAwaiter()
            => AsyncCleanuper.GetAwaiter();
    }

    public class AsyncHolder : IAsyncCleanupable
    {
        public AsyncCleanuper AsyncCleanuper { get; }

        Action DisposeProc;
        Func<Task> AsyncCleanupProc;
        LeakChecker.Holder Leak;
        LeakChecker.Holder Leak2;

        public AsyncHolder(Func<Task> asyncCleanupProc, Action disposeProc = null)
        {
            this.DisposeProc = disposeProc;
            this.AsyncCleanupProc = asyncCleanupProc;

            Leak = LeakChecker.Enter();
            Leak2 = LeakChecker.Enter();
            AsyncCleanuper = new AsyncCleanuper(this);
        }

        Once DisposeFlag;

        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing)
        {
            if (DisposeFlag.IsFirstCall() && disposing)
            {
                try
                {
                    if (DisposeProc != null)
                        DisposeProc();
                }
                finally
                {
                    Leak.DisposeSafe();
                }
            }
        }

        public async Task _CleanupAsyncInternal()
        {
            try
            {
                await AsyncCleanupProc();
            }
            finally
            {
                Leak2.DisposeSafe();
            }
        }

        public TaskAwaiter GetAwaiter()
            => AsyncCleanuper.GetAwaiter();
    }

    public sealed class GroupManager<TKey, TGroupContext> : IDisposable
    {
        public class GroupHandle : Holder<GroupInstance>
        {
            public TKey Key { get; }
            public TGroupContext Context { get; }
            public GroupInstance Instance { get; }

            internal GroupHandle(Action<GroupInstance> disposeProc, GroupInstance groupInstance, TKey key) : base(disposeProc, groupInstance)
            {
                this.Instance = groupInstance;
                this.Context = this.Instance.Context;
                this.Key = key;
            }
        }

        public class GroupInstance
        {
            public TKey Key;
            public TGroupContext Context;
            public int Num;
        }

        public delegate TGroupContext NewGroupContextCallback(TKey key, object userState);
        public delegate void DeleteGroupContextCallback(TKey key, TGroupContext groupContext, object userState);

        public object UserState { get; }

        NewGroupContextCallback NewGroupContextProc;
        DeleteGroupContextCallback DeleteGroupContextProc;

        Dictionary<TKey, GroupInstance> Hash = new Dictionary<TKey, GroupInstance>();

        object LockObj = new object();

        public GroupManager(NewGroupContextCallback onNewGroup, DeleteGroupContextCallback onDeleteGroup, object userState = null)
        {
            NewGroupContextProc = onNewGroup;
            DeleteGroupContextProc = onDeleteGroup;
            UserState = userState;
        }

        public GroupHandle Enter(TKey key)
        {
            lock (LockObj)
            {
                GroupInstance g = null;
                if (Hash.TryGetValue(key, out g) == false)
                {
                    var context = NewGroupContextProc(key, UserState);
                    g = new GroupInstance()
                    {
                        Key = key,
                        Context = context,
                        Num = 0,
                    };
                    Hash.Add(key, g);
                }

                Debug.Assert(g.Num >= 0);
                g.Num++;

                return new GroupHandle(x =>
                {
                    lock (LockObj)
                    {
                        x.Num--;
                        Debug.Assert(x.Num >= 0);

                        if (x.Num == 0)
                        {
                            Hash.Remove(x.Key);

                            DeleteGroupContextProc(x.Key, x.Context, this.UserState);
                        }
                    }
                }, g, key);
            }
        }

        Once DisposeFlag;
        public void Dispose()
        {
            if (DisposeFlag.IsFirstCall() == false)
                return;

            lock (LockObj)
            {
                foreach (var v in Hash.Values)
                {
                    try
                    {
                        DeleteGroupContextProc(v.Key, v.Context, this.UserState);
                    }
                    catch { }
                }

                Hash.Clear();
            }
        }
    }

    public sealed class DelayAction : IAsyncCleanupable
    {
        public Action<object> Action { get; }
        public object UserState { get; }
        public int Timeout { get; }

        Task MainTask;

        public bool IsCompleted = false;
        public bool IsCompletedSuccessfully = false;
        public bool IsCanceled = false;

        public Exception Exception { get; private set; } = null;

        public AsyncCleanuper AsyncCleanuper { get; }

        CancellationTokenSource CancelSource = new CancellationTokenSource();

        public DelayAction(int timeout, Action<object> action, object userState = null)
        {
            if (timeout < 0 || timeout == int.MaxValue) timeout = System.Threading.Timeout.Infinite;

            this.Timeout = timeout;
            this.Action = action;
            this.UserState = userState;

            this.MainTask = MainTaskProc();

            this.AsyncCleanuper = new AsyncCleanuper(this);
        }

        void InternalInvokeAction()
        {
            try
            {
                this.Action(this.UserState);

                IsCompleted = true;
                IsCompletedSuccessfully = true;
            }
            catch (Exception ex)
            {
                IsCompleted = true;
                IsCompletedSuccessfully = false;

                Exception = ex;
            }
        }

        async Task MainTaskProc()
        {
            using (LeakChecker.Enter())
            {
                try
                {
                    await WebSocketHelper.WaitObjectsAsync(timeout: this.Timeout,
                        cancels: CancelSource.Token.ToSingleArray(),
                        exceptions: ExceptionWhen.CancelException);

                    InternalInvokeAction();
                }
                catch
                {
                    IsCompleted = true;
                    IsCanceled = true;
                    IsCompletedSuccessfully = false;
                }
            }
        }

        public void Cancel() => Dispose();

        Once DisposeFlag;
        public void Dispose()
        {
            if (DisposeFlag.IsFirstCall() == false)
                return;

            CancelSource.Cancel();
        }

        public async Task _CleanupAsyncInternal()
        {
            await MainTask.TryWaitAsync(true);
        }
    }

    public class AsyncCallbackList
    {
        List<(Action<object> action, object state)> HardCallbackList = new List<(Action<object> action, object state)>();
        List<(Action<object> action, object state)> SoftCallbackList = new List<(Action<object> action, object state)>();

        public void AddHardCallback(Action<object> action, object state = null)
        {
            lock (HardCallbackList)
                HardCallbackList.Add((action, state));
        }

        public void AddSoftCallback(Action<object> action, object state = null)
        {
            lock (SoftCallbackList)
                SoftCallbackList.Add((action, state));
        }

        public void Invoke()
        {
            (Action<object> action, object state)[] arrayCopy;

            if (HardCallbackList.Count >= 1)
            {
                lock (HardCallbackList)
                {
                    arrayCopy = HardCallbackList.ToArray();
                }
                foreach (var v in arrayCopy)
                {
                    try
                    {
                        v.action(v.state);
                    }
                    catch { }
                }
            }

            if (SoftCallbackList.Count >= 1)
            {
                lock (SoftCallbackList)
                {
                    arrayCopy = SoftCallbackList.ToArray();
                }
                foreach (var v in arrayCopy)
                {
                    try
                    {
                        Task.Factory.StartNew(() =>
                        {
                            try
                            {
                                v.action(v.state);
                            }
                            catch { }
                        });
                    }
                    catch { }
                }
            }
        }
    }

    public class AsyncManualResetEvent
    {
        object lockobj = new object();
        volatile TaskCompletionSource<bool> tcs;
        bool isSet = false;

        public AsyncCallbackList CallbackList { get; } = new AsyncCallbackList();

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
                    return this.isSet;
                }
            }
        }

        public Task WaitAsync()
        {
            lock (lockobj)
            {
                if (isSet)
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
                if (isSet == false)
                {
                    Task.Factory.StartNew(() => Set(false));
                }
            }
            else
            {
                lock (lockobj)
                {
                    if (isSet == false)
                    {
                        isSet = true;
                        tcs.TrySetResult(true);

                        this.CallbackList.Invoke();
                    }
                }
            }
        }

        public void Reset()
        {
            lock (lockobj)
            {
                if (isSet)
                {
                    isSet = false;
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

        public Datagram(Memory<byte> data, EndPoint endPoint, byte flag = 0)
        {
            Data = data;
            EndPoint = endPoint;
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

        public NonBlockSocket(Socket s, CancellationToken cancel = default(CancellationToken), int tmpBufferSize = 65536, int maxRecvBufferSize = 65536, int maxRecvUdpQueueSize = 4096)
        {
            if (tmpBufferSize < 65536) tmpBufferSize = 65536;
            TmpRecvBuffer = new byte[tmpBufferSize];
            MaxRecvFifoSize = maxRecvBufferSize;
            MaxRecvUdpQueueSize = maxRecvUdpQueueSize;

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
                        Datagram[] recvPackets = await UdpBulkReader.Recv(cancel);

                        bool fullQueue = false;
                        bool pktReceived = false;

                        lock (RecvUdpQueue)
                        {
                            foreach (Datagram p in recvPackets)
                            {
                                if (RecvUdpQueue.Count <= MaxRecvUdpQueueSize)
                                {
                                    RecvUdpQueue.Enqueue(p);
                                    pktReceived = true;
                                }
                                else
                                {
                                    fullQueue = true;
                                    break;
                                }
                            }
                        }

                        if (fullQueue)
                        {
                            await WebSocketHelper.WaitObjectsAsync(cancels: new CancellationToken[] { cancel },
                                timeout: 10);
                        }

                        if (pktReceived)
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
                        byte[] sendData = null;

                        while (cancel.IsCancellationRequested == false)
                        {
                            lock (SendTcpFifo)
                            {
                                sendData = SendTcpFifo.Read();
                            }

                            if (sendData != null && sendData.Length >= 1)
                            {
                                break;
                            }

                            await WebSocketHelper.WaitObjectsAsync(cancels: new CancellationToken[] { cancel },
                                events: new AsyncAutoResetEvent[] { EventSendNow });
                        }

                        int r = await Sock.SendAsync(sendData, SocketFlags.None, cancel);
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
                                events: new AsyncAutoResetEvent[] { EventSendNow });
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
        public delegate Task<ValueOrClosed<TUserReturnElement>> AsyncReceiveCallback(TUserState state);

        public int DefaultMaxCount { get; } = 1024;

        AsyncReceiveCallback AsyncReceiveProc;

        public AsyncBulkReceiver(AsyncReceiveCallback asyncReceiveProc, int defaultMaxCount = 1024)
        {
            DefaultMaxCount = defaultMaxCount;
            AsyncReceiveProc = asyncReceiveProc;
        }

        Task<ValueOrClosed<TUserReturnElement>> pushedUserTask = null;

        public async Task<TUserReturnElement[]> Recv(CancellationToken cancel, TUserState state = default(TUserState), int? maxCount = null)
        {
            if (maxCount == null) maxCount = DefaultMaxCount;
            if (maxCount <= 0) maxCount = int.MaxValue;
            List<TUserReturnElement> ret = new List<TUserReturnElement>();

            while (true)
            {
                cancel.ThrowIfCancellationRequested();

                Task<ValueOrClosed<TUserReturnElement>> userTask;
                if (pushedUserTask != null)
                {
                    userTask = pushedUserTask;
                    pushedUserTask = null;
                }
                else
                {
                    userTask = AsyncReceiveProc(state);
                }
                if (userTask.IsCompleted == false)
                {
                    if (ret.Count >= 1)
                    {
                        pushedUserTask = userTask;
                        break;
                    }
                    else
                    {
                        await WebSocketHelper.WaitObjectsAsync(
                            tasks: new Task[] { userTask },
                            cancels: new CancellationToken[] { cancel });

                        cancel.ThrowIfCancellationRequested();

                        if (userTask.Result.IsOpen)
                        {
                            ret.Add(userTask.Result.Value);
                        }
                        else
                        {
                            pushedUserTask = userTask;
                            break;
                        }
                    }
                }
                else
                {
                    if (userTask.Result.IsOpen)
                    {
                        ret.Add(userTask.Result.Value);
                    }
                    else
                    {
                        break;
                    }
                }
                if (ret.Count >= maxCount) break;
            }

            if (ret.Count >= 1)
                return ret.ToArray();
            else
                return null; // Disconnected
        }
    }

    public class ExceptionQueue
    {
        public const int MaxItems = 128;
        SharedQueue<Exception> Queue = new SharedQueue<Exception>(MaxItems, true);
        public AsyncManualResetEvent WhenExceptionAdded { get; } = new AsyncManualResetEvent();

        HashSet<Task> WatchedTasks = new HashSet<Task>();

        public void Raise(Exception ex) => Add(ex, true);

        public void Add(Exception ex, bool raiseFirstException = false, bool doNotCheckWatchedTasks = false)
        {
            if (ex == null)
                ex = new Exception("null exception");

            if (doNotCheckWatchedTasks == false)
                CheckWatchedTasks();

            Exception throwingException = null;

            AggregateException aex = ex as AggregateException;

            if (aex != null)
            {
                var exp = aex.Flatten().InnerExceptions;

                lock (SharedQueue<Exception>.GlobalLock)
                {
                    foreach (var expi in exp)
                        Queue.Enqueue(expi);

                    if (raiseFirstException)
                        throwingException = Queue.ItemsReadOnly[0];
                }
            }
            else
            {
                lock (SharedQueue<Exception>.GlobalLock)
                {
                    Queue.Enqueue(ex);
                    if (raiseFirstException)
                        throwingException = Queue.ItemsReadOnly[0];
                }
            }

            WhenExceptionAdded.Set(true);

            if (throwingException != null)
                throwingException.ReThrow();
        }

        public void Encounter(ExceptionQueue other) => this.Queue.Encounter(other.Queue);

        public Exception[] GetExceptions()
        {
            CheckWatchedTasks();
            return this.Queue.ItemsReadOnly;
        }
        public Exception[] Exceptions => GetExceptions();
        public Exception FirstException => Exceptions.FirstOrDefault();

        public void ThrowFirstExceptionIfExists()
        {
            Exception ex = null;
            lock (SharedQueue<Exception>.GlobalLock)
            {
                if (HasError)
                    ex = FirstException;
            }

            if (ex != null)
                ex.ReThrow();
        }

        public bool HasError => Exceptions.Length != 0;
        public bool IsOk => !HasError;

        public bool RegisterWatchedTask(Task t)
        {
            if (t.IsCompleted)
            {
                if (t.IsFaulted)
                    Add(t.Exception);
                else if (t.IsCanceled)
                    Add(new TaskCanceledException());

                return true;
            }

            bool ret;

            lock (SharedQueue<Exception>.GlobalLock)
            {
                ret = WatchedTasks.Add(t);
            }

            t.ContinueWith(x =>
            {
                CheckWatchedTasks();
            });

            return ret;
        }

        public bool UnregisterWatchedTask(Task t)
        {
            lock (SharedQueue<Exception>.GlobalLock)
                return WatchedTasks.Remove(t);
        }

        void CheckWatchedTasks()
        {
            List<Task> o = new List<Task>();

            List<Exception> expList = new List<Exception>();

            lock (SharedQueue<Exception>.GlobalLock)
            {
                foreach (Task t in WatchedTasks)
                {
                    if (t.IsCompleted)
                    {
                        if (t.IsFaulted)
                            expList.Add(t.Exception);
                        else if (t.IsCanceled)
                            expList.Add(new TaskCanceledException());

                        o.Add(t);
                    }
                }

                foreach (Task t in o)
                    WatchedTasks.Remove(t);
            }

            foreach (Exception ex in expList)
                Add(ex, doNotCheckWatchedTasks: true);
        }
    }

    public class HierarchyPosition : RefInt
    {
        public HierarchyPosition() : base(-1) { }
        public bool IsInstalled { get => (this.Value != -1); }
    }

    public class SharedHierarchy<T>
    {
        public class HierarchyBodyItem : IComparable<HierarchyBodyItem>, IEquatable<HierarchyBodyItem>
        {
            public HierarchyPosition Position;
            public T Value;
            public HierarchyBodyItem(HierarchyPosition position, T value)
            {
                this.Position = position;
                this.Value = value;
            }

            public int CompareTo(HierarchyBodyItem other) => this.Position.CompareTo(other.Position);
            public bool Equals(HierarchyBodyItem other) => this.Position.Equals(other.Position);
            public override bool Equals(object obj) => (obj is HierarchyBodyItem) ? this.Position.Equals(obj as HierarchyBodyItem) : false;
            public override int GetHashCode() => this.Position.GetHashCode();
        }
        
        class HierarchyBody
        {
            public List<HierarchyBodyItem> _InternalList = new List<HierarchyBodyItem>();

            public HierarchyBody Next = null;

            public HierarchyBody() { }

            public HierarchyBodyItem[] ToArray()
            {
                lock (_InternalList)
                    return _InternalList.ToArray();
            }

            public HierarchyBodyItem Join(HierarchyPosition targetPosition, bool joinAsSuperior, T value, HierarchyPosition myPosition)
            {
                lock (_InternalList)
                {
                    if (targetPosition == null)
                    {
                        var me = new HierarchyBodyItem(myPosition, value);
                        var current = _InternalList;

                        current = current.Append(me).ToList();

                        int positionIncrement = 0;
                        current.ForEach(x => x.Position.Set(++positionIncrement));

                        _InternalList.Clear();
                        _InternalList.AddRange(current);

                        return me;
                    }
                    else
                    {
                        var current = _InternalList;

                        var inferiors = current.Where(x => joinAsSuperior ? (x.Position <= targetPosition) : (x.Position < targetPosition));
                        var me = new HierarchyBodyItem(myPosition, value);
                        var superiors = current.Where(x => joinAsSuperior ? (x.Position > targetPosition) : (x.Position >= targetPosition));

                        current = inferiors.Append(me).Concat(superiors).ToList();

                        int positionIncrement = 0;
                        current.ForEach(x => x.Position.Set(++positionIncrement));

                        _InternalList.Clear();
                        _InternalList.AddRange(current);

                        return me;
                    }
                }
            }

            public void Resign(HierarchyBodyItem me)
            {
                lock (_InternalList)
                {
                    var current = _InternalList;

                    if (current.Contains(me))
                    {
                        current.Remove(me);

                        int positionIncrement = 0;
                        current.ForEach(x => x.Position.Set(++positionIncrement));

                        Debug.Assert(me.Position.IsInstalled);

                        me.Position.Set(-1);
                    }
                    else
                    {
                        Debug.Assert(false);
                    }
                }
            }

            public static void Merge(HierarchyBody inferiors, HierarchyBody superiors)
            {
                if (inferiors == superiors) return;

                Debug.Assert(inferiors._InternalList != null);
                Debug.Assert(superiors._InternalList != null);

                lock (inferiors._InternalList)
                {
                    lock (superiors._InternalList)
                    {
                        HierarchyBody merged = new HierarchyBody();
                        merged._InternalList.AddRange(inferiors._InternalList.Concat(superiors._InternalList));

                        int positionIncrement = 0;
                        merged._InternalList.ForEach(x => x.Position.Set(++positionIncrement));

                        inferiors._InternalList = superiors._InternalList = null;
                        Debug.Assert(inferiors.Next == null); Debug.Assert(superiors.Next == null);
                        inferiors.Next = superiors.Next = merged;
                    }
                }
            }

            public HierarchyBody GetLast()
            {
                if (Next == null)
                    return this;
                else
                    return Next.GetLast();
            }
        }

        HierarchyBody First;

        public static readonly object GlobalLock = new object();

        public SharedHierarchy()
        {
            First = new HierarchyBody();
        }

        public void Encounter(SharedHierarchy<T> inferiors)
        {
            if (this == inferiors) return;

            lock (GlobalLock)
            {
                HierarchyBody inferiorsBody = inferiors.First.GetLast();
                HierarchyBody superiorsBody = this.First.GetLast();
                if (inferiorsBody == superiorsBody) return;

                HierarchyBody.Merge(inferiorsBody, superiorsBody);
            }
        }

        public HierarchyBodyItem Join(HierarchyPosition targetPosition, bool joinAsSuperior, T value, HierarchyPosition myPosition)
        {
            Debug.Assert(myPosition.IsInstalled == false);

            lock (GlobalLock)
                return this.First.GetLast().Join(targetPosition, joinAsSuperior, value, myPosition);
        }

        public void Resign(HierarchyBodyItem me)
        {
            Debug.Assert(me.Position.IsInstalled);

            lock (GlobalLock)
                this.First.GetLast().Resign(me);
        }

        public HierarchyBodyItem[] ToArrayWithPositions()
        {
            lock (GlobalLock)
                return this.First.GetLast().ToArray();
        }

        public HierarchyBodyItem[] ItemsWithPositionsReadOnly { get => ToArrayWithPositions(); }

        public T[] ToArray() => ToArrayWithPositions().Select(x => x.Value).ToArray();
        public T[] ItemsReadOnly { get => ToArray(); }
    }

    public abstract class LayerInfoBase
    {
        public HierarchyPosition Position { get; } = new HierarchyPosition();
        internal SharedHierarchy<LayerInfoBase>.HierarchyBodyItem _InternalHierarchyBodyItem = null;
        internal LayerStack _InternalLayerStack = null;

        public FastProtocolStackBase ProtocolStack { get; private set; } = null;

        public bool IsInstalled => Position.IsInstalled;

        public void Install(LayerStack stack, LayerInfoBase targetLayer, bool joinAsSuperior)
            => stack.Install(this, targetLayer, joinAsSuperior);

        public void Uninstall()
            => _InternalLayerStack.Uninstall(this);

        internal void _InternalSetProtocolStack(FastProtocolStackBase protocolStack)
            => ProtocolStack = protocolStack;
    }

    public interface ILayerInfoSsl
    {
        bool IsServerMode { get; }
        string SslProtocol { get; }
        string CipherAlgorithm { get; }
        int CipherStrength { get; }
        string HashAlgorithm { get; }
        int HashStrength { get; }
        string KeyExchangeAlgorithm { get; }
        int KeyExchangeStrength { get; }
        X509Certificate LocalCertificate { get; }
        X509Certificate RemoteCertificate { get; }
    }

    public interface ILayerInfoIpEndPoint
    {
        IPAddress LocalIPAddress { get; }
        IPAddress RemoteIPAddress { get; }
    }

    public interface ILayerInfoTcpEndPoint : ILayerInfoIpEndPoint
    {
        int LocalPort { get; }
        int RemotePort { get; }
    }

    public class LayerStack
    {
        SharedHierarchy<LayerInfoBase> Hierarchy = new SharedHierarchy<LayerInfoBase>();

        public void Install(LayerInfoBase info, LayerInfoBase targetLayer, bool joinAsSuperior)
        {
            Debug.Assert(info.IsInstalled == false); Debug.Assert(info._InternalHierarchyBodyItem == null); Debug.Assert(info._InternalLayerStack == null);

            info._InternalHierarchyBodyItem = Hierarchy.Join(targetLayer?.Position, joinAsSuperior, info, info.Position);
            info._InternalLayerStack = this;

            Debug.Assert(info.IsInstalled); Debug.Assert(info._InternalHierarchyBodyItem != null);
        }

        public void Uninstall(LayerInfoBase info)
        {
            Debug.Assert(info.IsInstalled); Debug.Assert(info._InternalHierarchyBodyItem != null); Debug.Assert(info._InternalLayerStack != null);

            Hierarchy.Resign(info._InternalHierarchyBodyItem);

            info._InternalHierarchyBodyItem = null;
            info._InternalLayerStack = null;

            Debug.Assert(info.IsInstalled == false);
        }

        public void Encounter(LayerStack other) => this.Hierarchy.Encounter(other.Hierarchy);
    }

    public class SharedQueue<T>
    {
        class QueueBody
        {
            static long globalTimestamp;

            public QueueBody Next;

            public SortedList<long, T> _InternalList = new SortedList<long, T>();
            public readonly int MaxItems;

            public QueueBody(int maxItems)
            {
                if (maxItems <= 0) maxItems = int.MaxValue;
                MaxItems = maxItems;
            }

            public void Enqueue(T item, bool distinct = false)
            {
                lock (_InternalList)
                {
                    if (_InternalList.Count > MaxItems) return;
                    if (distinct && _InternalList.ContainsValue(item)) return;
                    long ts = Interlocked.Increment(ref globalTimestamp);
                    _InternalList.Add(ts, item);
                }
            }

            public T Dequeue()
            {
                lock (_InternalList)
                {
                    if (_InternalList.Count == 0) return default(T);
                    long ts = _InternalList.Keys[0];
                    T ret = _InternalList[ts];
                    _InternalList.Remove(ts);
                    return ret;
                }
            }

            public T[] ToArray()
            {
                lock (_InternalList)
                {
                    return _InternalList.Values.ToArray();
                }
            }

            public static void Merge(QueueBody q1, QueueBody q2)
            {
                if (q1 == q2) return;
                Debug.Assert(q1._InternalList != null);
                Debug.Assert(q2._InternalList != null);

                lock (q1._InternalList)
                {
                    lock (q2._InternalList)
                    {
                        QueueBody q3 = new QueueBody(Math.Max(q1.MaxItems, q2.MaxItems));
                        foreach (long ts in q1._InternalList.Keys)
                            q3._InternalList.Add(ts, q1._InternalList[ts]);
                        foreach (long ts in q2._InternalList.Keys)
                            q3._InternalList.Add(ts, q2._InternalList[ts]);
                        if (q3._InternalList.Count > q3.MaxItems)
                        {
                            int num = 0;
                            List<long> removeList = new List<long>();
                            foreach (long ts in q3._InternalList.Keys)
                            {
                                num++;
                                if (num > q3.MaxItems)
                                    removeList.Add(ts);
                            }
                            foreach (long ts in removeList)
                                q3._InternalList.Remove(ts);
                        }
                        q1._InternalList = null;
                        q2._InternalList = null;
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

        public bool Distinct { get; }

        public SharedQueue(int maxItems = 0, bool distinct = false)
        {
            Distinct = distinct;
            First = new QueueBody(maxItems);
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
                this.First.GetLast().Enqueue(value, Distinct);
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
                var list = q._InternalList;
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

        double MeasureInternal(int duration, TUserVariable state, Action<TUserVariable, int> proc, int interationsPassValue)
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
                if (Init == null && interationsPassValue == 0)
                {
                    DummyLoopProc(state, Iterations);
                }
                else
                {
                    proc(state, interationsPassValue);
                    if (interationsPassValue == 0)
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
            double nanoPerCall = nano / (double)count;

            return nanoPerCall;
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

    public static class LeakChecker
    {
        static Dictionary<long, Holder> List = new Dictionary<long, Holder>();
        static long CurrentId = 0;

        public static Holder Enter([CallerFilePath] string filename = "", [CallerLineNumber] int line = 0, [CallerMemberName] string caller = null)
            => new Holder($"{caller}() - {Path.GetFileName(filename)}:{line}", Environment.StackTrace);

        public static int Count
        {
            get
            {
                lock (List)
                    return List.Count;
            }
        }

        public static void Print()
        {
            lock (List)
            {
                if (Count == 0)
                {
                    Console.WriteLine("@@@ No leaks @@@");
                }
                else
                {
                    Console.WriteLine($"*** Leaked !!! count = {Count} ***");
                    Console.WriteLine($"--- Leaked list  count = {Count} ---");
                    Console.Write(GetString());
                    Console.WriteLine($"--- End of leaked list  count = {Count} --");
                }
            }
        }

        public static string GetString()
        {
            StringWriter w = new StringWriter();
            int num = 0;
            lock (List)
            {
                foreach (var v in List.OrderBy(x => x.Key))
                {
                    num++;
                    w.WriteLine($"#{num}: {v.Key}: {v.Value.Name}");
                    if (string.IsNullOrEmpty(v.Value.StackTrace) == false)
                    {
                        w.WriteLine(v.Value.StackTrace);
                        w.WriteLine("---");
                    }
                }
            }
            return w.ToString();
        }

        public sealed class Holder : IDisposable
        {
            long Id;
            public string Name { get; }
            public string StackTrace { get; }

            internal Holder(string name, string stackTrace)
            {
                stackTrace = "";

                if (string.IsNullOrEmpty(name)) name = "<untitled>";

                Id = Interlocked.Increment(ref LeakChecker.CurrentId);
                Name = name;
                StackTrace = string.IsNullOrEmpty(stackTrace) ? "" : stackTrace;

                lock (LeakChecker.List)
                    LeakChecker.List.Add(Id, this);
            }

            Once DisposeFlag;
            public void Dispose()
            {
                if (DisposeFlag.IsFirstCall())
                {
                    lock (LeakChecker.List)
                    {
                        Debug.Assert(LeakChecker.List.ContainsKey(Id));
                        LeakChecker.List.Remove(Id);
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
                var oldFirst = First;
                var nn = new FastLinkedListNode<T>() { Value = value, Next = oldFirst, Previous = null };
                Debug.Assert(oldFirst.Previous == null);
                oldFirst.Previous = nn;
                First = nn;
                Count++;
                return nn;
            }
        }

        public void AddFirst(FastLinkedListNode<T> chainFirst, FastLinkedListNode<T> chainLast, int chainedCount)
        {
            if (First == null)
            {
                Debug.Assert(Last == null);
                Debug.Assert(Count == 0);
                First = chainFirst;
                Last = chainLast;
                chainFirst.Previous = null;
                chainLast.Next = null;
                Count = chainedCount;
            }
            else
            {
                Debug.Assert(Last != null);
                Debug.Assert(Count >= 1);
                var oldFirst = First;
                Debug.Assert(oldFirst.Previous == null);
                oldFirst.Previous = chainLast;
                First = chainFirst;
                Count += chainedCount;
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
                var oldLast = Last;
                var nn = new FastLinkedListNode<T>() { Value = value, Next = null, Previous = oldLast };
                Debug.Assert(oldLast.Next == null);
                oldLast.Next = nn;
                Last = nn;
                Count++;
                return nn;
            }
        }

        public void AddLast(FastLinkedListNode<T> chainFirst, FastLinkedListNode<T> chainLast, int chainedCount)
        {
            if (Last == null)
            {
                Debug.Assert(First == null);
                Debug.Assert(Count == 0);
                First = chainFirst;
                Last = chainLast;
                chainFirst.Previous = null;
                chainLast.Next = null;
                Count = chainedCount;
            }
            else
            {
                Debug.Assert(First != null);
                Debug.Assert(Count >= 1);
                var oldLast = Last;
                Debug.Assert(oldLast.Next == null);
                oldLast.Next = chainFirst;
                Last = chainLast;
                Count += chainedCount;
            }
        }

        public FastLinkedListNode<T> AddAfter(FastLinkedListNode<T> prevNode, T value)
        {
            var nextNode = prevNode.Next;
            Debug.Assert(First != null && Last != null);
            Debug.Assert(nextNode != null || Last == prevNode);
            Debug.Assert(nextNode == null || nextNode.Previous == prevNode);
            var nn = new FastLinkedListNode<T>() { Value = value, Next = nextNode, Previous = prevNode };
            prevNode.Next = nn;
            if (nextNode != null) nextNode.Previous = nn;
            if (Last == prevNode) Last = nn;
            Count++;
            return nn;
        }

        public void AddAfter(FastLinkedListNode<T> prevNode, FastLinkedListNode<T> chainFirst, FastLinkedListNode<T> chainLast, int chainedCount)
        {
            var nextNode = prevNode.Next;
            Debug.Assert(First != null && Last != null);
            Debug.Assert(nextNode != null || Last == prevNode);
            Debug.Assert(nextNode == null || nextNode.Previous == prevNode);
            prevNode.Next = chainFirst;
            chainFirst.Previous = prevNode;
            if (nextNode != null) nextNode.Previous = chainLast;
            chainLast.Previous = nextNode;
            if (Last == prevNode) Last = chainLast;
            Count += chainedCount;
        }

        public FastLinkedListNode<T> AddBefore(FastLinkedListNode<T> nextNode, T value)
        {
            var prevNode = nextNode.Previous;
            Debug.Assert(First != null && Last != null);
            Debug.Assert(prevNode != null || First == nextNode);
            Debug.Assert(prevNode == null || prevNode.Next == nextNode);
            var nn = new FastLinkedListNode<T>() { Value = value, Next = nextNode, Previous = prevNode };
            nextNode.Previous = nn;
            if (prevNode != null) prevNode.Next = nn;
            if (First == nextNode) First = nn;
            Count++;
            return nn;
        }

        public void AddBefore(FastLinkedListNode<T> nextNode, FastLinkedListNode<T> chainFirst, FastLinkedListNode<T> chainLast, int chainedCount)
        {
            var prevNode = nextNode.Previous;
            Debug.Assert(First != null && Last != null);
            Debug.Assert(prevNode != null || First == nextNode);
            Debug.Assert(prevNode == null || prevNode.Next == nextNode);
            nextNode.Previous = chainLast;
            chainLast.Next = nextNode;
            if (prevNode != null) prevNode.Next = chainFirst;
            chainFirst.Previous = prevNode;
            if (First == nextNode) First = chainFirst;
            Count += chainedCount;
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

        public LocalTimer(bool automaticUpdateNow = true)
        {
            AutomaticUpdateNow = automaticUpdateNow;
        }

        public void UpdateNow() => Now = FastTick64.Now;
        public void UpdateNow(long nowTick) => Now = nowTick;

        public long AddTick(long tick)
        {
            if (Hash.Add(tick))
                List.Add(tick);

            return tick;
        }

        public long AddTimeout(int interval)
        {
            if (interval == Timeout.Infinite) return long.MaxValue;
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
            List<long> deleteList = null;

            foreach (long v in List)
            {
                if (now >= v)
                {
                    ret = 0;
                    if (deleteList == null) deleteList = new List<long>();
                    deleteList.Add(v);
                }
                else
                {
                    break;
                }
            }

            if (deleteList != null)
            {
                foreach (long v in deleteList)
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

    public class HostNetInfo : BackgroundStateDataBase
    {
        public override BackgroundStateDataUpdatePolicy DataUpdatePolicy =>
            new BackgroundStateDataUpdatePolicy(300, 6000, 2000);

        public string HostName;
        public string DomainName;
        public string FqdnHostName => HostName + (string.IsNullOrEmpty(DomainName) ? "" : "." + DomainName);
        public bool IsIPv4Supported;
        public bool IsIPv6Supported;
        public List<IPAddress> IPAddressList = new List<IPAddress>();

        public static bool IsUnix { get; } = (Environment.OSVersion.Platform != PlatformID.Win32NT);

        static IPAddress[] GetLocalIPAddressBySocketApi() => Dns.GetHostAddresses(Dns.GetHostName());

        class ByteComparer : IComparer<byte[]>
        {
            public int Compare(byte[] x, byte[] y) => x.AsSpan().SequenceCompareTo(y.AsSpan());
        }

        public HostNetInfo()
        {
            IPGlobalProperties prop = IPGlobalProperties.GetIPGlobalProperties();
            this.HostName = prop.HostName;
            this.DomainName = prop.DomainName;
            HashSet<IPAddress> hash = new HashSet<IPAddress>();

            if (IsUnix)
            {
                UnicastIPAddressInformationCollection info = prop.GetUnicastAddresses();
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
                this.IsIPv4Supported = true;
                hash.Add(IPAddress.Any);
                hash.Add(IPAddress.Loopback);
            }
            if (Socket.OSSupportsIPv6)
            {
                this.IsIPv6Supported = true;
                hash.Add(IPAddress.IPv6Any);
                hash.Add(IPAddress.IPv6Loopback);
            }

            try
            {
                var cmp = new ByteComparer();
                this.IPAddressList = hash.OrderBy(x => x.AddressFamily)
                    .ThenBy(x => x.GetAddressBytes(), cmp)
                    .ThenBy(x => (x.AddressFamily == AddressFamily.InterNetworkV6 ? x.ScopeId : 0))
                    .ToList();
            }
            catch { }
        }

        public Memory<byte> IPAddressListBinary
        {
            get
            {
                FastMemoryBuffer<byte> ret = new FastMemoryBuffer<byte>();
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

        public override bool Equals(BackgroundStateDataBase otherArg)
        {
            HostNetInfo other = otherArg as HostNetInfo;
            if (string.Equals(this.HostName, other.HostName) == false) return false;
            if (string.Equals(this.DomainName, other.DomainName) == false) return false;
            if (this.IsIPv4Supported != other.IsIPv4Supported) return false;
            if (this.IsIPv6Supported != other.IsIPv6Supported) return false;
            if (this.IPAddressListBinary.Span.SequenceEqual(other.IPAddressListBinary.Span) == false) return false;
            return true;
        }

        Action callMeCache = null;

        public override void RegisterSystemStateChangeNotificationCallbackOnlyOnce(Action callMe)
        {
            callMeCache = callMe;

            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
            NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;
        }

        private void NetworkChange_NetworkAddressChanged(object sender, EventArgs e)
        {
            callMeCache();

            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
        }

        private void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            callMeCache();

            NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;
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
                return BackgroundState<HostNetInfo>.Current.Data.IPAddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork)
                    .Where(x => IPAddress.IsLoopback(x) == false).Where(x => x != IPAddress.Any).First();
            }
            catch { }

            return IPAddress.Any;
        }

    }

    public readonly struct BackgroundStateDataUpdatePolicy
    {
        public readonly int InitialPollingInterval;
        public readonly int MaxPollingInterval;
        public readonly int IdleTimeoutToFreeThreadInterval;

        public const int DefaultInitialPollingInterval = 1 * 1000;
        public const int DefaultMaxPollingInterval = 60 * 1000;
        public const int DefaultIdleTimeoutToFreeThreadInterval = 180 * 1000;

        public BackgroundStateDataUpdatePolicy(int initialPollingInterval = DefaultInitialPollingInterval,
            int maxPollingInterval = DefaultMaxPollingInterval,
            int timeoutToStopThread = DefaultIdleTimeoutToFreeThreadInterval)
        {
            InitialPollingInterval = initialPollingInterval;
            MaxPollingInterval = maxPollingInterval;
            IdleTimeoutToFreeThreadInterval = timeoutToStopThread;
        }

        public static BackgroundStateDataUpdatePolicy Default { get; }
            = new BackgroundStateDataUpdatePolicy(1 * 1000, 60 * 1000, 30 * 1000);

        public BackgroundStateDataUpdatePolicy SafeValue
        {
            get
            {
                return new BackgroundStateDataUpdatePolicy(
                    Math.Max(this.InitialPollingInterval, 1 * 100),
                    Math.Max(this.MaxPollingInterval, 1 * 500),
                    Math.Max(Math.Max(this.IdleTimeoutToFreeThreadInterval, 1 * 500), this.MaxPollingInterval)
                    );
            }
        }
    }

    public abstract class BackgroundStateDataBase : IEquatable<BackgroundStateDataBase>
    {
        public DateTimeOffset TimeStamp { get; } = DateTimeOffset.Now;
        public long TickTimeStamp { get; } = FastTick64.Now;

        public abstract BackgroundStateDataUpdatePolicy DataUpdatePolicy { get; }

        public abstract bool Equals(BackgroundStateDataBase other);

        public abstract void RegisterSystemStateChangeNotificationCallbackOnlyOnce(Action callMe);
    }

    public static class BackgroundState<TData>
        where TData : BackgroundStateDataBase, new()
    {
        public struct CurrentData
        {
            public int Version;
            public TData Data;
        }

        public static CurrentData Current
        {
            get
            {
                CurrentData d = new CurrentData();
                d.Data = GetState();
                d.Version = InternalVersion;
                return d;
            }
        }

        static volatile TData CacheData = null;

        static volatile int NumRead = 0;

        static volatile int InternalVersion = 0;

        static bool CallbackIsRegistered = false;

        static object LockObj = new object();
        static Thread thread = null;
        static AutoResetEvent threadSignal = new AutoResetEvent(false);
        static bool callbackIsCalled = false;

        public static FastEventListenerList<TData, int> EventListener { get; } = new FastEventListenerList<TData, int>();

        static TData TryGetTData()
        {
            try
            {
                TData ret = new TData();

                if (CallbackIsRegistered == false)
                {
                    try
                    {
                        ret.RegisterSystemStateChangeNotificationCallbackOnlyOnce(() =>
                        {
                            callbackIsCalled = true;
                            GetState();
                            threadSignal.Set();
                        });

                        CallbackIsRegistered = true;
                    }
                    catch { }
                }

                return ret;
            }
            catch
            {
                return null;
            }
        }

        static TData GetState()
        {
            NumRead++;

            if (CacheData != null)
            {
                if (thread == null)
                {
                    EnsureStartThreadIfStopped(CacheData.DataUpdatePolicy);
                }

                return CacheData;
            }
            else
            {
                BackgroundStateDataUpdatePolicy updatePolicy = BackgroundStateDataUpdatePolicy.Default;
                TData data = TryGetTData();
                if (data != null)
                {
                    updatePolicy = data.DataUpdatePolicy;

                    bool inc = false;
                    if (CacheData == null)
                    {
                        inc = true;
                    }
                    else
                    {
                        if (CacheData.Equals(data) == false)
                            inc = true;
                    }
                    CacheData = data;

                    if (inc)
                    {
                        InternalVersion++;
                        EventListener.Fire(CacheData, 0);
                    }
                }

                EnsureStartThreadIfStopped(updatePolicy);

                return CacheData;
            }
        }

        static void EnsureStartThreadIfStopped(BackgroundStateDataUpdatePolicy updatePolicy)
        {
            lock (LockObj)
            {
                if (thread == null)
                {
                    thread = new Thread(MaintainThread);
                    thread.IsBackground = true;
                    thread.Priority = ThreadPriority.BelowNormal;
                    thread.Name = $"MaintainThread for BackgroundState<{typeof(TData).ToString()}>";
                    thread.Start(updatePolicy);
                }
            }
        }

        static int nextInterval = 0;

        static void MaintainThread(object param)
        {
            BackgroundStateDataUpdatePolicy policy = (BackgroundStateDataUpdatePolicy)param;
            policy = policy.SafeValue;

            LocalTimer tm = new LocalTimer();

            if (nextInterval == 0)
            {
                nextInterval = policy.InitialPollingInterval;
            }

            long nextGetDataTick = tm.AddTimeout(nextInterval);

            long nextIdleDetectTick = tm.AddTimeout(policy.IdleTimeoutToFreeThreadInterval);

            int lastNumRead = NumRead;

            while (true)
            {
                if (FastTick64.Now >= nextGetDataTick || callbackIsCalled)
                {
                    TData data = TryGetTData();

                    nextInterval = Math.Min(nextInterval + policy.InitialPollingInterval, policy.MaxPollingInterval);
                    bool inc = false;

                    if (data != null)
                    {
                        if (data.Equals(CacheData) == false)
                        {
                            nextInterval = policy.InitialPollingInterval;
                            inc = true;
                        }
                        CacheData = data;
                    }
                    else
                    {
                        nextInterval = policy.InitialPollingInterval;
                    }

                    if (callbackIsCalled)
                    {
                        nextInterval = policy.InitialPollingInterval;
                    }

                    if (inc)
                    {
                        InternalVersion++;
                        EventListener.Fire(CacheData, 0);
                    }

                    nextGetDataTick = tm.AddTimeout(nextInterval);

                    callbackIsCalled = false;
                }

                if (FastTick64.Now >= nextIdleDetectTick)
                {
                    int numread = NumRead;
                    if (lastNumRead != numread)
                    {
                        lastNumRead = numread;
                        nextIdleDetectTick = tm.AddTimeout(policy.IdleTimeoutToFreeThreadInterval);
                    }
                    else
                    {
                        thread = null;
                        return;
                    }
                }

                int i = tm.GetNextInterval();

                i = Math.Max(i, 100);

                threadSignal.WaitOne(i);
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

    public sealed class TcpListenManager : IAsyncCleanupable
    {
        public class Listener
        {
            public IPVersion IPVersion { get; }
            public IPAddress IPAddress { get; }
            public int Port { get; }

            public ListenStatus Status { get; internal set; }
            public Exception LastError { get; internal set; }

            internal Task _InternalTask { get; }

            internal CancellationTokenSource _InternalSelfCancelSource { get; }
            internal CancellationToken _InternalSelfCancelToken { get => _InternalSelfCancelSource.Token; }

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
                _InternalSelfCancelSource = new CancellationTokenSource();

                _InternalTask = ListenLoop();
            }

            static internal string MakeHashKey(IPVersion ipVer, IPAddress ipAddress, int port)
            {
                return $"{port} / {ipAddress} / {ipAddress.AddressFamily} / {ipVer}";
            }

            async Task ListenLoop()
            {
                AsyncAutoResetEvent networkChangedEvent = new AsyncAutoResetEvent();
                int eventRegisterId = BackgroundState<HostNetInfo>.EventListener.RegisterAsyncEvent(networkChangedEvent);

                Status = ListenStatus.Trying;

                int numRetry = 0;
                int lastNetworkInfoVer = BackgroundState<HostNetInfo>.Current.Version;

                try
                {
                    while (_InternalSelfCancelToken.IsCancellationRequested == false)
                    {
                        Status = ListenStatus.Trying;
                        _InternalSelfCancelToken.ThrowIfCancellationRequested();

                        int sleepDelay = (int)Math.Min(RetryIntervalStandard * numRetry, RetryIntervalMax);
                        if (sleepDelay >= 1)
                            sleepDelay = WebSocketHelper.RandSInt31() % sleepDelay;
                        await WebSocketHelper.WaitObjectsAsync(timeout: sleepDelay,
                            cancels: new CancellationToken[] { _InternalSelfCancelToken },
                            events: new AsyncAutoResetEvent[] { networkChangedEvent } );
                        numRetry++;

                        int networkInfoVer = BackgroundState<HostNetInfo>.Current.Version;
                        if (lastNetworkInfoVer != networkInfoVer)
                        {
                            lastNetworkInfoVer = networkInfoVer;
                            numRetry = 0;
                        }

                        _InternalSelfCancelToken.ThrowIfCancellationRequested();

                        try
                        {
                            TcpListener listener = new TcpListener(IPAddress, Port);
                            listener.ExclusiveAddressUse = true;
                            listener.Start();

                            var reg = _InternalSelfCancelToken.Register(() =>
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
                                        _InternalSelfCancelToken.ThrowIfCancellationRequested();

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
                    BackgroundState<HostNetInfo>.EventListener.UnregisterAsyncEvent(eventRegisterId);
                    Status = ListenStatus.Stopped;
                }
            }

            internal async Task _InternalStopAsync()
            {
                await _InternalSelfCancelSource.TryCancelAsync();
                try
                {
                    await _InternalTask;
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

        public TcpListenManager(Func<TcpListenManager, Listener, Socket, Task> acceptedProc)
        {
            AcceptedProc = acceptedProc;
            AsyncCleanuper = new AsyncCleanuper(this);
        }

        public Listener Add(int port, IPVersion? ipVer = null, IPAddress addr = null)
        {
            if (addr == null)
                addr = ((ipVer ?? IPVersion.IPv4) == IPVersion.IPv4) ? IPAddress.Any : IPAddress.IPv6Any;
            if (ipVer == null)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                    ipVer = IPVersion.IPv4;
                else if (addr.AddressFamily == AddressFamily.InterNetworkV6)
                    ipVer = IPVersion.IPv6;
                else
                    throw new ArgumentException("Unsupported AddressFamily.");
            }
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException("Port number is out of range.");

            lock (LockObj)
            {
                if (DisposeFlag.IsSet) throw new ObjectDisposedException("TcpListenManager");

                var s = Search(Listener.MakeHashKey((IPVersion)ipVer, addr, port));
                if (s != null)
                    return s;
                s = new Listener(this, (IPVersion)ipVer, addr, port);
                List.Add(Listener.MakeHashKey((IPVersion)ipVer, addr, port), s);
                return s;
            }
        }

        public async Task<bool> DeleteAsync(Listener listener)
        {
            Listener s;
            lock (LockObj)
            {
                string hashKey = Listener.MakeHashKey(listener.IPVersion, listener.IPAddress, listener.Port);
                s = Search(hashKey);
                if (s == null)
                    return false;
                List.Remove(hashKey);
            }
            await s._InternalStopAsync();
            return true;
        }

        Listener Search(string hashKey)
        {
            if (List.TryGetValue(hashKey, out Listener ret) == false)
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
            catch (Exception ex)
            {
                Console.WriteLine("AcceptedProc error: " + ex.ToString());
            }
        }

        public Listener[] Listeners
        {
            get
            {
                lock (LockObj)
                    return List.Values.ToArray();
            }
        }

        Once DisposeFlag;
        public void Dispose()
        {
            if (DisposeFlag.IsFirstCall())
            {
            }
        }

        public async Task _CleanupAsyncInternal()
        {
            List<Listener> o = new List<Listener>();
            lock (LockObj)
            {
                List.Values.ToList().ForEach(x => o.Add(x));
                List.Clear();
            }

            foreach (Listener s in o)
                await s._InternalStopAsync().TryWaitAsync();

            List<Task> waitTasks = new List<Task>();
            List<Socket> disconnectSockets = new List<Socket>();

            lock (LockObj)
            {
                foreach (var v in RunningAcceptedTasks)
                {
                    disconnectSockets.Add(v.Value);
                    waitTasks.Add(v.Key);
                }
                RunningAcceptedTasks.Clear();
            }

            foreach (var sock in disconnectSockets)
                sock.DisposeSafe();

            foreach (var task in waitTasks)
                await task.TryWaitAsync();

            Debug.Assert(CurrentConnections == 0);
        }

        public AsyncCleanuper AsyncCleanuper { get; }
    }

    public class DisconnectedException : Exception { }
    public class FastBufferDisconnectedException : DisconnectedException { }
    public class SocketDisconnectedException : DisconnectedException { }
    public class BaseStreamDisconnectedException : DisconnectedException { }

    public delegate void FastEventCallback<TCaller, TEventType>(TCaller buffer, TEventType type, object userState);

    public class FastEvent<TCaller, TEventType>
    {
        public FastEventCallback<TCaller, TEventType> Proc { get; }
        public object UserState { get; }

        public FastEvent(FastEventCallback<TCaller, TEventType> proc, object userState)
        {
            this.Proc = proc;
            this.UserState = userState;
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
        FastReadList<AutoResetEvent> EventList;
        FastReadList<AsyncAutoResetEvent> AsyncEventList;

        public int RegisterCallback(FastEventCallback<TCaller, TEventType> proc, object userState = null)
        {
            if (proc == null) return 0;
            return ListenerList.Add(new FastEvent<TCaller, TEventType>(proc, userState));
        }

        public bool UnregisterCallback(int id)
        {
            return ListenerList.Delete(id);
        }

        public int RegisterEvent(AutoResetEvent ev)
        {
            if (ev == null) return 0;
            return EventList.Add(ev);
        }

        public bool UnregisterEvent(int id)
        {
            return EventList.Delete(id);
        }

        public int RegisterAsyncEvent(AsyncAutoResetEvent ev)
        {
            if (ev == null) return 0;
            return AsyncEventList.Add(ev);
        }

        public bool UnregisterAsyncEvent(int id)
        {
            return AsyncEventList.Delete(id);
        }

        public void Fire(TCaller caller, TEventType type)
        {
            var listenerList = ListenerList.GetListFast();
            if (listenerList != null)
                foreach (var e in listenerList)
                    e.CallSafe(caller, type);

            var eventList = EventList.GetListFast();
            if (eventList != null)
                foreach (var e in eventList)
                    e.Set();

            var asyncEventList = AsyncEventList.GetListFast();
            if (asyncEventList != null)
                foreach (var e in asyncEventList)
                    e.Set();
        }
    }

    public enum FastBufferCallbackEventType
    {
        Init,
        Written,
        Read,
        PartialProcessReadData,
        EmptyToNonEmpty,
        NonEmptyToEmpty,
        Disconnected,
    }

    public interface IFastBufferState
    {
        long Id { get; }

        long PinHead { get; }
        long PinTail { get; }
        long Length { get; }

        ExceptionQueue ExceptionQueue { get; }
        LayerStack LayerStack { get; }

        object LockObj { get; }

        bool IsReadyToWrite { get; }
        bool IsReadyToRead { get; }
        bool IsEventsEnabled { get; }
        AsyncAutoResetEvent EventWriteReady { get; }
        AsyncAutoResetEvent EventReadReady { get; }

        FastEventListenerList<IFastBufferState, FastBufferCallbackEventType> EventListeners { get; }

        void CompleteRead();
        void CompleteWrite(bool checkDisconnect = true);
    }

    public interface IFastBuffer<T> : IFastBufferState
    {
        void Clear();
        void Enqueue(T item);
        void EnqueueAll(Span<T> itemList);
        void EnqueueAllWithLock(Span<T> itemList);
        List<T> Dequeue(long minReadSize, out long totalReadSize, bool allowSplitSegments = true);
        List<T> DequeueAll(out long totalReadSize);
        List<T> DequeueAllWithLock(out long totalReadSize);
        long DequeueAllAndEnqueueToOther(IFastBuffer<T> other);
    }

    public readonly struct FastBufferSegment<T>
    {
        public readonly T Item;
        public readonly long Pin;
        public readonly long RelativeOffset;

        public FastBufferSegment(T item, long pin, long relativeOffset)
        {
            Item = item;
            Pin = pin;
            RelativeOffset = relativeOffset;
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

        public Fifo(int reAllocMemSize = FifoReAllocSize)
        {
            ReAllocMemSize = reAllocMemSize;
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
                int oldSize, newSize, needSize;

                oldSize = Size;
                newSize = oldSize + size;
                needSize = Position + newSize;

                bool reallocFlag = false;
                int newPhysicalSize = PhysicalData.Length;
                while (needSize > newPhysicalSize)
                {
                    newPhysicalSize = Math.Max(newPhysicalSize, FifoInitSize) * 3;
                    reallocFlag = true;
                }

                if (reallocFlag)
                    PhysicalData = MemoryHelper.ReAlloc(PhysicalData, newPhysicalSize);

                if (src != null)
                    src.CopyTo(PhysicalData.AsSpan().Slice(Position + oldSize));

                Size = newSize;
            }
        }

        void WriteInternal(T src)
        {
            checked
            {
                int oldSize, newSize, needSize;

                oldSize = Size;
                newSize = oldSize + 1;
                needSize = Position + newSize;

                bool reallocFlag = false;
                int newPhysicalSize = PhysicalData.Length;
                while (needSize > newPhysicalSize)
                {
                    newPhysicalSize = Math.Max(newPhysicalSize, FifoInitSize) * 3;
                    reallocFlag = true;
                }

                if (reallocFlag)
                    PhysicalData = MemoryHelper.ReAlloc(PhysicalData, newPhysicalSize);

                if (src != null)
                    PhysicalData[Position + oldSize] = src;

                Size = newSize;
            }
        }


        public int Read(Span<T> dest)
        {
            return ReadInternal(dest, dest.Length);
        }

        public T[] Read(int size)
        {
            int readSize = Math.Min(this.Size, size);
            T[] ret = new T[readSize];
            Read(ret);
            return ret;
        }

        public T[] Read() => Read(this.Size);

        int ReadInternal(Span<T> dest, int size)
        {
            checked
            {
                int readSize;

                readSize = Math.Min(size, Size);
                if (readSize == 0)
                {
                    return 0;
                }
                if (dest != null)
                {
                    PhysicalData.AsSpan(this.Position, size).CopyTo(dest);
                }
                Position += readSize;
                Size -= readSize;

                if (Size == 0)
                {
                    Position = 0;
                }

                if (this.Position >= FifoInitSize &&
                    this.PhysicalData.Length >= this.ReAllocMemSize &&
                    (this.PhysicalData.Length / 2) > this.Size)
                {
                    int newPhysicalSize;

                    newPhysicalSize = Math.Max(this.PhysicalData.Length / 2, FifoInitSize);

                    T[] newArray = new T[newPhysicalSize];
                    this.PhysicalData.AsSpan(this.Position, this.Size).CopyTo(newArray);
                    this.PhysicalData = newArray;

                    this.Position = 0;
                }

                return readSize;
            }
        }

        public Span<T> Span { get => this.PhysicalData.AsSpan(this.Position, this.Size); }
    }

    static internal class FastBufferGlobalIdCounter
    {
        static long Id = 0;
        public static long NewId() => Interlocked.Increment(ref Id);
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

        Once internalDisconnectedFlag;
        public bool IsDisconnected { get => internalDisconnectedFlag.IsSet; }

        public AsyncAutoResetEvent EventWriteReady { get; } = null;
        public AsyncAutoResetEvent EventReadReady { get; } = null;

        public const long DefaultThreshold = 524288;

        public List<Action> OnDisconnected { get; } = new List<Action>();

        public object LockObj { get; } = new object();

        public ExceptionQueue ExceptionQueue { get; } = new ExceptionQueue();
        public LayerStack LayerStack { get; } = new LayerStack();

        public FastStreamBuffer(bool enableEvents = false, long? thresholdLength = null)
        {
            if (thresholdLength < 0) throw new ArgumentOutOfRangeException("thresholdLength < 0");

            Threshold = thresholdLength ?? DefaultThreshold;
            IsEventsEnabled = enableEvents;
            if (IsEventsEnabled)
            {
                EventWriteReady = new AsyncAutoResetEvent();
                EventReadReady = new AsyncAutoResetEvent();
            }

            Id = FastBufferGlobalIdCounter.NewId();

            EventListeners.Fire(this, FastBufferCallbackEventType.Init);
        }

        Once checkDisconnectFlag;

        public void CheckDisconnected()
        {
            if (IsDisconnected)
            {
                if (checkDisconnectFlag.IsFirstCall())
                {
                    ExceptionQueue.Raise(new FastBufferDisconnectedException());
                }
                else
                {
                    ExceptionQueue.ThrowFirstExceptionIfExists();
                    throw new FastBufferDisconnectedException();
                }
            }
        }

        public void Disconnect()
        {
            if (internalDisconnectedFlag.IsFirstCall())
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

        long LastHeadPin = long.MinValue;

        public void CompleteRead()
        {
            if (IsEventsEnabled)
            {
                bool setFlag = false;

                lock (LockObj)
                {
                    long current = PinHead;
                    if (LastHeadPin != current)
                    {
                        LastHeadPin = current;
                        if (IsReadyToWrite)
                            setFlag = true;
                    }
                    if (IsDisconnected)
                        setFlag = true;
                }

                if (setFlag)
                {
                    EventWriteReady.Set();
                }
            }
        }

        long LastTailPin = long.MinValue;

        public void CompleteWrite(bool checkDisconnect = true)
        {
            if (IsEventsEnabled)
            {
                bool setFlag = false;
                lock (LockObj)
                {
                    long current = PinTail;
                    if (LastTailPin != current)
                    {
                        LastTailPin = current;
                        setFlag = true;
                    }
                    if (IsDisconnected)
                        setFlag = true;
                }
                if (setFlag)
                {
                    EventReadReady.Set();
                }
            }

            if (checkDisconnect)
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

        public void Insert(long pin, Memory<T> item, bool appendIfOverrun = false)
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

                if (appendIfOverrun)
                {
                    if (pin < PinHead)
                        InsertBefore(new T[PinHead - pin]);

                    if (pin > PinTail)
                        InsertTail(new T[pin - PinTail]);
                }
                else
                {
                    if (List.First == null) throw new ArgumentOutOfRangeException("Buffer is empty.");
                    if (pin < PinHead) throw new ArgumentOutOfRangeException("pin < PinHead");
                    if (pin > PinTail) throw new ArgumentOutOfRangeException("pin > PinTail");
                }

                var node = GetNodeWithPin(pin, out int offsetInSegment, out _);
                Debug.Assert(node != null);
                if (offsetInSegment == 0)
                {
                    var newNode = List.AddBefore(node, item);
                    PinTail += item.Length;
                }
                else if (node.Value.Length == offsetInSegment)
                {
                    var newNode = List.AddAfter(node, item);
                    PinTail += item.Length;
                }
                else
                {
                    Memory<T> sliceBefore = node.Value.Slice(0, offsetInSegment);
                    Memory<T> sliceAfter = node.Value.Slice(offsetInSegment);

                    node.Value = sliceBefore;
                    var newNode = List.AddAfter(node, item);
                    List.AddAfter(newNode, sliceAfter);
                    PinTail += item.Length;
                }
            }
        }

        FastLinkedListNode<Memory<T>> GetNodeWithPin(long pin, out int offsetInSegment, out long nodePin)
        {
            checked
            {
                offsetInSegment = 0;
                nodePin = 0;
                if (List.First == null)
                {
                    if (pin != PinHead) throw new ArgumentOutOfRangeException("List.First == null, but pin != PinHead");
                    return null;
                }
                if (pin < PinHead) throw new ArgumentOutOfRangeException("pin < PinHead");
                if (pin == PinHead)
                {
                    nodePin = pin;
                    return List.First;
                }
                if (pin > PinTail) throw new ArgumentOutOfRangeException("pin > PinTail");
                if (pin == PinTail)
                {
                    var last = List.Last;
                    if (last != null)
                    {
                        offsetInSegment = last.Value.Length;
                        nodePin = PinTail - last.Value.Length;
                    }
                    else
                    {
                        nodePin = PinTail;
                    }
                    return last;
                }
                long currentPin = PinHead;
                FastLinkedListNode<Memory<T>> node = List.First;
                while (node != null)
                {
                    if (pin >= currentPin && pin < (currentPin + node.Value.Length))
                    {
                        offsetInSegment = (int)(pin - currentPin);
                        nodePin = currentPin;
                        return node;
                    }
                    currentPin += node.Value.Length;
                    node = node.Next;
                }
                throw new ApplicationException("GetNodeWithPin: Bug!");
            }
        }

        void GetOverlappedNodes(long pinStart, long pinEnd,
            out FastLinkedListNode<Memory<T>> firstNode, out int firstNodeOffsetInSegment, out long firstNodePin,
            out FastLinkedListNode<Memory<T>> lastNode, out int lastNodeOffsetInSegment, out long lastNodePin,
            out int nodeCounts, out int lackRemainLength)
        {
            checked
            {
                if (pinStart > pinEnd) throw new ArgumentOutOfRangeException("pinStart > pinEnd");

                firstNode = GetNodeWithPin(pinStart, out firstNodeOffsetInSegment, out firstNodePin);

                if (pinEnd > PinTail)
                {
                    lackRemainLength = (int)checked(pinEnd - PinTail);
                    pinEnd = PinTail;
                }

                FastLinkedListNode<Memory<T>> node = firstNode;
                long currentPin = pinStart - firstNodeOffsetInSegment;
                nodeCounts = 0;
                while (true)
                {
                    Debug.Assert(node != null, "node == null");

                    nodeCounts++;
                    if (pinEnd <= (currentPin + node.Value.Length))
                    {
                        lastNodeOffsetInSegment = (int)(pinEnd - currentPin);
                        lastNode = node;
                        lackRemainLength = 0;
                        lastNodePin = currentPin;

                        Debug.Assert(firstNodeOffsetInSegment != firstNode.Value.Length);
                        Debug.Assert(lastNodeOffsetInSegment != 0);

                        return;
                    }
                    currentPin += node.Value.Length;
                    node = node.Next;
                }
            }
        }

        public FastBufferSegment<Memory<T>>[] GetSegmentsFast(long pin, long size, out long readSize, bool allowPartial = false)
        {
            checked
            {
                if (size < 0) throw new ArgumentOutOfRangeException("size < 0");
                if (size == 0)
                {
                    readSize = 0;
                    return new FastBufferSegment<Memory<T>>[0];
                }
                if (pin > PinTail)
                {
                    throw new ArgumentOutOfRangeException("pin > PinTail");
                }
                if ((pin + size) > PinTail)
                {
                    if (allowPartial == false)
                        throw new ArgumentOutOfRangeException("(pin + size) > PinTail");
                    size = PinTail - pin;
                }

                FastBufferSegment<Memory<T>>[] ret = GetUncontiguousSegments(pin, pin + size, false);
                readSize = size;
                return ret;
            }
        }

        public FastBufferSegment<Memory<T>>[] ReadForwardFast(ref long pin, long size, out long readSize, bool allowPartial = false)
        {
            checked
            {
                FastBufferSegment<Memory<T>>[] ret = GetSegmentsFast(pin, size, out readSize, allowPartial);
                pin += readSize;
                return ret;
            }
        }

        public Memory<T> GetContiguous(long pin, long size, bool allowPartial = false)
        {
            checked
            {
                if (size < 0) throw new ArgumentOutOfRangeException("size < 0");
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
                    if (allowPartial == false)
                        throw new ArgumentOutOfRangeException("(pin + size) > PinTail");
                    size = PinTail - pin;
                }
                Memory<T> ret = GetContiguousMemory(pin, pin + size, false, false);
                return ret;
            }
        }

        public Memory<T> ReadForwardContiguous(ref long pin, long size, bool allowPartial = false)
        {
            checked
            {
                Memory<T> ret = GetContiguous(pin, size, allowPartial);
                pin += ret.Length;
                return ret;
            }
        }

        public Memory<T> PutContiguous(long pin, long size, bool appendIfOverrun = false)
        {
            checked
            {
                if (size < 0) throw new ArgumentOutOfRangeException("size < 0");
                if (size == 0)
                {
                    return new Memory<T>();
                }
                Memory<T> ret = GetContiguousMemory(pin, pin + size, appendIfOverrun, false);
                return ret;
            }
        }

        public Memory<T> WriteForwardContiguous(ref long pin, long size, bool appendIfOverrun = false)
        {
            checked
            {
                Memory<T> ret = PutContiguous(pin, size, appendIfOverrun);
                pin += ret.Length;
                return ret;
            }
        }

        public void Enqueue(Memory<T> item)
        {
            CheckDisconnected();
            long oldLen = Length;
            if (item.Length == 0) return;
            InsertTail(item);
            EventListeners.Fire(this, FastBufferCallbackEventType.Written);
            if (Length != 0 && oldLen == 0)
                EventListeners.Fire(this, FastBufferCallbackEventType.EmptyToNonEmpty);
        }

        public void EnqueueAllWithLock(Span<Memory<T>> itemList)
        {
            lock (LockObj)
                EnqueueAll(itemList);
        }

        public void EnqueueAll(Span<Memory<T>> itemList)
        {
            CheckDisconnected();
            checked
            {
                int num = 0;
                long oldLen = Length;
                foreach (Memory<T> t in itemList)
                {
                    if (t.Length != 0)
                    {
                        List.AddLast(t);
                        PinTail += t.Length;
                        num++;
                    }
                }
                if (num >= 1)
                {
                    EventListeners.Fire(this, FastBufferCallbackEventType.Written);

                    if (Length != 0 && oldLen == 0)
                        EventListeners.Fire(this, FastBufferCallbackEventType.EmptyToNonEmpty);
                }
            }
        }

        public int DequeueContiguousSlow(Memory<T> dest, int size = int.MaxValue)
        {
            if (IsDisconnected && this.Length == 0) CheckDisconnected();
            checked
            {
                long oldLen = Length;
                if (size < 0) throw new ArgumentOutOfRangeException("size < 0");
                size = Math.Min(size, dest.Length);
                Debug.Assert(size >= 0);
                if (size == 0) return 0;
                var memarray = Dequeue(size, out long totalSize, true);
                Debug.Assert(totalSize <= size);
                if (totalSize > int.MaxValue) throw new IndexOutOfRangeException("totalSize > int.MaxValue");
                if (dest.Length < totalSize) throw new ArgumentOutOfRangeException("dest.Length < totalSize");
                int pos = 0;
                foreach (var mem in memarray)
                {
                    mem.CopyTo(dest.Slice(pos, mem.Length));
                    pos += mem.Length;
                }
                Debug.Assert(pos == totalSize);
                EventListeners.Fire(this, FastBufferCallbackEventType.Read);
                if (Length == 0 && oldLen != 0)
                    EventListeners.Fire(this, FastBufferCallbackEventType.NonEmptyToEmpty);
                return (int)totalSize;
            }
        }

        public Memory<T> DequeueContiguousSlow(int size = int.MaxValue)
        {
            if (IsDisconnected && this.Length == 0) CheckDisconnected();
            checked
            {
                long oldLen = Length;
                if (size < 0) throw new ArgumentOutOfRangeException("size < 0");
                if (size == 0) return Memory<T>.Empty;
                int readSize = (int)Math.Min(size, Length);
                Memory<T> ret = new T[readSize];
                int r = DequeueContiguousSlow(ret, readSize);
                Debug.Assert(r <= readSize);
                ret = ret.Slice(0, r);
                EventListeners.Fire(this, FastBufferCallbackEventType.Read);
                if (Length == 0 && oldLen != 0)
                    EventListeners.Fire(this, FastBufferCallbackEventType.NonEmptyToEmpty);
                return ret;
            }
        }

        public List<Memory<T>> DequeueAllWithLock(out long totalReadSize)
        {
            lock (this.LockObj)
                return DequeueAll(out totalReadSize);
        }
        public List<Memory<T>> DequeueAll(out long totalReadSize) => Dequeue(long.MaxValue, out totalReadSize);
        public List<Memory<T>> Dequeue(long minReadSize, out long totalReadSize, bool allowSplitSegments = true)
        {
            if (IsDisconnected && this.Length == 0) CheckDisconnected();
            checked
            {
                if (minReadSize < 1) throw new ArgumentOutOfRangeException("size < 1");

                totalReadSize = 0;
                if (List.First == null)
                {
                    return new List<Memory<T>>();
                }

                long oldLen = Length;

                FastLinkedListNode<Memory<T>> node = List.First;
                List<Memory<T>> ret = new List<Memory<T>>();
                while (true)
                {
                    if ((totalReadSize + node.Value.Length) >= minReadSize)
                    {
                        if (allowSplitSegments && (totalReadSize + node.Value.Length) > minReadSize)
                        {
                            int lastSegmentReadSize = (int)(minReadSize - totalReadSize);
                            Debug.Assert(lastSegmentReadSize <= node.Value.Length);
                            ret.Add(node.Value.Slice(0, lastSegmentReadSize));
                            if (lastSegmentReadSize == node.Value.Length)
                                List.Remove(node);
                            else
                                node.Value = node.Value.Slice(lastSegmentReadSize);
                            totalReadSize += lastSegmentReadSize;
                            PinHead += totalReadSize;
                            Debug.Assert(minReadSize >= totalReadSize);
                            EventListeners.Fire(this, FastBufferCallbackEventType.Read);
                            if (Length == 0 && oldLen != 0)
                                EventListeners.Fire(this, FastBufferCallbackEventType.NonEmptyToEmpty);
                            return ret;
                        }
                        else
                        {
                            ret.Add(node.Value);
                            totalReadSize += node.Value.Length;
                            List.Remove(node);
                            PinHead += totalReadSize;
                            Debug.Assert(minReadSize <= totalReadSize);
                            EventListeners.Fire(this, FastBufferCallbackEventType.Read);
                            if (Length == 0 && oldLen != 0)
                                EventListeners.Fire(this, FastBufferCallbackEventType.NonEmptyToEmpty);
                            return ret;
                        }
                    }
                    else
                    {
                        ret.Add(node.Value);
                        totalReadSize += node.Value.Length;

                        FastLinkedListNode<Memory<T>> deleteNode = node;
                        node = node.Next;

                        List.Remove(deleteNode);

                        if (node == null)
                        {
                            PinHead += totalReadSize;
                            EventListeners.Fire(this, FastBufferCallbackEventType.Read);
                            if (Length == 0 && oldLen != 0)
                                EventListeners.Fire(this, FastBufferCallbackEventType.NonEmptyToEmpty);
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
                    long otherOldLen = other.Length;
                    long oldLen = Length;
                    Debug.Assert(other.List.Count == 0);
                    other.List = this.List;
                    this.List = new FastLinkedList<Memory<T>>();
                    this.PinHead = this.PinTail;
                    other.PinTail += length;
                    EventListeners.Fire(this, FastBufferCallbackEventType.Read);
                    if (Length == 0 && oldLen != 0)
                        EventListeners.Fire(this, FastBufferCallbackEventType.NonEmptyToEmpty);
                    other.EventListeners.Fire(other, FastBufferCallbackEventType.Written);
                    if (other.Length != 0 && otherOldLen == 0)
                        other.EventListeners.Fire(other, FastBufferCallbackEventType.EmptyToNonEmpty);
                    return length;
                }
                else
                {
                    long length = this.Length;
                    long oldLen = Length;
                    long otherOldLen = other.Length;
                    var chainFirst = this.List.First;
                    var chainLast = this.List.Last;
                    other.List.AddLast(this.List.First, this.List.Last, this.List.Count);
                    this.List.Clear();
                    this.PinHead = this.PinTail;
                    other.PinTail += length;
                    EventListeners.Fire(this, FastBufferCallbackEventType.Read);
                    if (Length == 0 && oldLen != 0)
                        EventListeners.Fire(this, FastBufferCallbackEventType.NonEmptyToEmpty);
                    other.EventListeners.Fire(other, FastBufferCallbackEventType.Written);
                    if (other.Length != 0 && otherOldLen == 0)
                        other.EventListeners.Fire(other, FastBufferCallbackEventType.EmptyToNonEmpty);
                    return length;
                }
            }
        }

        FastBufferSegment<Memory<T>>[] GetUncontiguousSegments(long pinStart, long pinEnd, bool appendIfOverrun)
        {
            checked
            {
                if (pinStart == pinEnd) return new FastBufferSegment<Memory<T>>[0];
                if (pinStart > pinEnd) throw new ArgumentOutOfRangeException("pinStart > pinEnd");

                if (appendIfOverrun)
                {
                    if (List.First == null)
                    {
                        InsertHead(new T[pinEnd - pinStart]);
                        PinHead = pinStart;
                        PinTail = pinEnd;
                    }

                    if (pinStart < PinHead)
                        InsertBefore(new T[PinHead - pinStart]);

                    if (pinEnd > PinTail)
                        InsertTail(new T[pinEnd - PinTail]);
                }
                else
                {
                    if (List.First == null) throw new ArgumentOutOfRangeException("Buffer is empty.");
                    if (pinStart < PinHead) throw new ArgumentOutOfRangeException("pinStart < PinHead");
                    if (pinEnd > PinTail) throw new ArgumentOutOfRangeException("pinEnd > PinTail");
                }

                GetOverlappedNodes(pinStart, pinEnd,
                    out FastLinkedListNode<Memory<T>> firstNode, out int firstNodeOffsetInSegment, out long firstNodePin,
                    out FastLinkedListNode<Memory<T>> lastNode, out int lastNodeOffsetInSegment, out long lastNodePin,
                    out int nodeCounts, out int lackRemainLength);

                Debug.Assert(lackRemainLength == 0, "lackRemainLength != 0");

                if (firstNode == lastNode)
                    return new FastBufferSegment<Memory<T>>[1]{ new FastBufferSegment<Memory<T>>(
                    firstNode.Value.Slice(firstNodeOffsetInSegment, lastNodeOffsetInSegment - firstNodeOffsetInSegment), pinStart, 0) };

                FastBufferSegment<Memory<T>>[] ret = new FastBufferSegment<Memory<T>>[nodeCounts];

                FastLinkedListNode<Memory<T>> prevNode = firstNode.Previous;
                FastLinkedListNode<Memory<T>> nextNode = lastNode.Next;

                FastLinkedListNode<Memory<T>> node = firstNode;
                int count = 0;
                long currentOffset = 0;

                while (true)
                {
                    Debug.Assert(node != null, "node == null");

                    int sliceStart = (node == firstNode) ? firstNodeOffsetInSegment : 0;
                    int sliceLength = (node == lastNode) ? lastNodeOffsetInSegment : node.Value.Length - sliceStart;

                    ret[count] = new FastBufferSegment<Memory<T>>(node.Value.Slice(sliceStart, sliceLength), currentOffset + pinStart, currentOffset);
                    count++;

                    Debug.Assert(count <= nodeCounts, "count > nodeCounts");

                    currentOffset += sliceLength;

                    if (node == lastNode)
                    {
                        Debug.Assert(count == ret.Length, "count != ret.Length");
                        break;
                    }

                    node = node.Next;
                }

                return ret;
            }
        }

        public void Remove(long pinStart, long length)
        {
            checked
            {
                if (length == 0) return;
                if (length < 0) throw new ArgumentOutOfRangeException("length < 0");
                long pinEnd = checked(pinStart + length);
                if (List.First == null) throw new ArgumentOutOfRangeException("Buffer is empty.");
                if (pinStart < PinHead) throw new ArgumentOutOfRangeException("pinStart < PinHead");
                if (pinEnd > PinTail) throw new ArgumentOutOfRangeException("pinEnd > PinTail");

                GetOverlappedNodes(pinStart, pinEnd,
                    out FastLinkedListNode<Memory<T>> firstNode, out int firstNodeOffsetInSegment, out long firstNodePin,
                    out FastLinkedListNode<Memory<T>> lastNode, out int lastNodeOffsetInSegment, out long lastNodePin,
                    out int nodeCounts, out int lackRemainLength);

                Debug.Assert(lackRemainLength == 0, "lackRemainLength != 0");

                if (firstNode == lastNode)
                {
                    Debug.Assert(firstNodeOffsetInSegment < lastNodeOffsetInSegment);
                    if (firstNodeOffsetInSegment == 0 && lastNodeOffsetInSegment == lastNode.Value.Length)
                    {
                        Debug.Assert(firstNode.Value.Length == length, "firstNode.Value.Length != length");
                        List.Remove(firstNode);
                        PinTail -= length;
                        return;
                    }
                    else
                    {
                        Debug.Assert((lastNodeOffsetInSegment - firstNodeOffsetInSegment) == length);
                        Memory<T> slice1 = firstNode.Value.Slice(0, firstNodeOffsetInSegment);
                        Memory<T> slice2 = firstNode.Value.Slice(lastNodeOffsetInSegment);
                        Debug.Assert(slice1.Length != 0 || slice2.Length != 0);
                        if (slice1.Length == 0)
                        {
                            firstNode.Value = slice2;
                        }
                        else if (slice2.Length == 0)
                        {
                            firstNode.Value = slice1;
                        }
                        else
                        {
                            firstNode.Value = slice1;
                            List.AddAfter(firstNode, slice2);
                        }
                        PinTail -= length;
                        return;
                    }
                }
                else
                {
                    firstNode.Value = firstNode.Value.Slice(0, firstNodeOffsetInSegment);
                    lastNode.Value = lastNode.Value.Slice(lastNodeOffsetInSegment);

                    var node = firstNode.Next;
                    while (node != lastNode)
                    {
                        var nodeToDelete = node;

                        Debug.Assert(node.Next != null);
                        node = node.Next;

                        List.Remove(nodeToDelete);
                    }

                    if (lastNode.Value.Length == 0)
                        List.Remove(lastNode);

                    if (firstNode.Value.Length == 0)
                        List.Remove(firstNode);

                    PinTail -= length;
                    return;
                }
            }
        }

        public T[] ToArray() => GetContiguousMemory(PinHead, PinTail, false, true).ToArray();

        public T[] ItemsSlow { get => ToArray(); }

        Memory<T> GetContiguousMemory(long pinStart, long pinEnd, bool appendIfOverrun, bool noReplace)
        {
            checked
            {
                if (pinStart == pinEnd) return new Memory<T>();
                if (pinStart > pinEnd) throw new ArgumentOutOfRangeException("pinStart > pinEnd");

                if (appendIfOverrun)
                {
                    if (List.First == null)
                    {
                        InsertHead(new T[pinEnd - pinStart]);
                        PinHead = pinStart;
                        PinTail = pinEnd;
                    }

                    if (pinStart < PinHead)
                        InsertBefore(new T[PinHead - pinStart]);

                    if (pinEnd > PinTail)
                        InsertTail(new T[pinEnd - PinTail]);
                }
                else
                {
                    if (List.First == null) throw new ArgumentOutOfRangeException("Buffer is empty.");
                    if (pinStart < PinHead) throw new ArgumentOutOfRangeException("pinStart < PinHead");
                    if (pinEnd > PinTail) throw new ArgumentOutOfRangeException("pinEnd > PinTail");
                }

                GetOverlappedNodes(pinStart, pinEnd,
                    out FastLinkedListNode<Memory<T>> firstNode, out int firstNodeOffsetInSegment, out long firstNodePin,
                    out FastLinkedListNode<Memory<T>> lastNode, out int lastNodeOffsetInSegment, out long lastNodePin,
                    out int nodeCounts, out int lackRemainLength);

                Debug.Assert(lackRemainLength == 0, "lackRemainLength != 0");

                if (firstNode == lastNode)
                    return firstNode.Value.Slice(firstNodeOffsetInSegment, lastNodeOffsetInSegment - firstNodeOffsetInSegment);

                FastLinkedListNode<Memory<T>> prevNode = firstNode.Previous;
                FastLinkedListNode<Memory<T>> nextNode = lastNode.Next;

                Memory<T> newMemory = new T[lastNodePin + lastNode.Value.Length - firstNodePin];
                FastLinkedListNode<Memory<T>> node = firstNode;
                int currentWritePointer = 0;

                while (true)
                {
                    Debug.Assert(node != null, "node == null");

                    bool finish = false;
                    node.Value.CopyTo(newMemory.Slice(currentWritePointer));

                    if (node == lastNode) finish = true;

                    FastLinkedListNode<Memory<T>> nodeToDelete = node;
                    currentWritePointer += node.Value.Length;

                    node = node.Next;

                    if (noReplace == false)
                        List.Remove(nodeToDelete);

                    if (finish) break;
                }

                if (noReplace == false)
                {
                    if (prevNode != null)
                        List.AddAfter(prevNode, newMemory);
                    else if (nextNode != null)
                        List.AddBefore(nextNode, newMemory);
                    else
                        List.AddFirst(newMemory);
                }

                var ret = newMemory.Slice(firstNodeOffsetInSegment, newMemory.Length - (lastNode.Value.Length - lastNodeOffsetInSegment) - firstNodeOffsetInSegment);
                Debug.Assert(ret.Length == (pinEnd - pinStart), "ret.Length");
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

        Once internalDisconnectedFlag;
        public bool IsDisconnected { get => internalDisconnectedFlag.IsSet; }

        public List<Action> OnDisconnected { get; } = new List<Action>();

        public const long DefaultThreshold = 65536;

        public object LockObj { get; } = new object();

        public ExceptionQueue ExceptionQueue { get; } = new ExceptionQueue();
        public LayerStack LayerStack { get; } = new LayerStack();

        public FastDatagramBuffer(bool enableEvents = false, long? thresholdLength = null)
        {
            if (thresholdLength < 0) throw new ArgumentOutOfRangeException("thresholdLength < 0");

            Threshold = thresholdLength ?? DefaultThreshold;
            IsEventsEnabled = enableEvents;
            if (IsEventsEnabled)
            {
                EventWriteReady = new AsyncAutoResetEvent();
                EventReadReady = new AsyncAutoResetEvent();
            }

            Id = FastBufferGlobalIdCounter.NewId();

            EventListeners.Fire(this, FastBufferCallbackEventType.Init);
        }

        Once checkDisconnectFlag;

        public void CheckDisconnected()
        {
            if (IsDisconnected)
            {
                if (checkDisconnectFlag.IsFirstCall())
                    ExceptionQueue.Raise(new FastBufferDisconnectedException());
                else
                {
                    ExceptionQueue.ThrowFirstExceptionIfExists();
                    throw new FastBufferDisconnectedException();
                }
            }
        }

        public void Disconnect()
        {
            if (internalDisconnectedFlag.IsFirstCall())
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

        long LastHeadPin = long.MinValue;

        public void CompleteRead()
        {
            if (IsEventsEnabled)
            {
                bool setFlag = false;

                lock (LockObj)
                {
                    long current = PinHead;
                    if (LastHeadPin != current)
                    {
                        LastHeadPin = current;
                        if (IsReadyToWrite)
                            setFlag = true;
                    }
                    if (IsDisconnected)
                        setFlag = true;
                }

                if (setFlag)
                {
                    EventWriteReady.Set();
                }
            }
        }

        long LastTailPin = long.MinValue;

        public void CompleteWrite(bool checkDisconnect = true)
        {
            if (IsEventsEnabled)
            {
                bool setFlag = false;

                lock (LockObj)
                {
                    long current = PinTail;
                    if (LastTailPin != current)
                    {
                        LastTailPin = current;
                        setFlag = true;
                    }
                }

                if (setFlag)
                {
                    EventReadReady.Set();
                }
            }
            if (checkDisconnect)
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
                long oldLen = Length;
                Fifo.Write(item);
                PinTail++;
                EventListeners.Fire(this, FastBufferCallbackEventType.Written);
                if (Length != 0 && oldLen == 0)
                    EventListeners.Fire(this, FastBufferCallbackEventType.EmptyToNonEmpty);
            }
        }

        public void EnqueueAllWithLock(Span<T> itemList)
        {
            lock (LockObj)
                EnqueueAll(itemList);
        }

        public void EnqueueAll(Span<T> itemList)
        {
            CheckDisconnected();
            checked
            {
                long oldLen = Length;
                Fifo.Write(itemList);
                PinTail += itemList.Length;
                EventListeners.Fire(this, FastBufferCallbackEventType.Written);
                if (Length != 0 && oldLen == 0)
                    EventListeners.Fire(this, FastBufferCallbackEventType.EmptyToNonEmpty);
            }
        }

        public List<T> Dequeue(long minReadSize, out long totalReadSize, bool allowSplitSegments = true)
        {
            if (IsDisconnected && this.Length == 0) CheckDisconnected();
            checked
            {
                if (minReadSize < 1) throw new ArgumentOutOfRangeException("size < 1");
                if (minReadSize >= int.MaxValue) minReadSize = int.MaxValue;

                long oldLen = Length;

                totalReadSize = 0;
                if (Fifo.Size == 0)
                {
                    return new List<T>();
                }

                T[] tmp = Fifo.Read((int)minReadSize);

                totalReadSize = tmp.Length;
                List<T> ret = new List<T>(tmp);

                PinHead += totalReadSize;

                EventListeners.Fire(this, FastBufferCallbackEventType.Read);

                if (Length == 0 && oldLen != 0)
                    EventListeners.Fire(this, FastBufferCallbackEventType.NonEmptyToEmpty);

                return ret;
            }
        }

        public List<T> DequeueAll(out long totalReadSize) => Dequeue(long.MaxValue, out totalReadSize);

        public List<T> DequeueAllWithLock(out long totalReadSize)
        {
            lock (LockObj)
                return DequeueAll(out totalReadSize);
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
                    long oldLen = Length;
                    long length = this.Length;
                    Debug.Assert(other.Fifo.Size == 0);
                    other.Fifo = this.Fifo;
                    this.Fifo = new Fifo<T>();
                    this.PinHead = this.PinTail;
                    other.PinTail += length;
                    EventListeners.Fire(this, FastBufferCallbackEventType.Read);
                    other.EventListeners.Fire(other, FastBufferCallbackEventType.Written);
                    if (Length != 0 && oldLen == 0)
                        EventListeners.Fire(this, FastBufferCallbackEventType.EmptyToNonEmpty);
                    return length;
                }
                else
                {
                    long oldLen = Length;
                    long length = this.Length;
                    var data = this.Fifo.Read();
                    other.Fifo.Write(data);
                    this.PinHead = this.PinTail;
                    other.PinTail += length;
                    EventListeners.Fire(this, FastBufferCallbackEventType.Read);
                    other.EventListeners.Fire(other, FastBufferCallbackEventType.Written);
                    if (Length != 0 && oldLen == 0)
                        EventListeners.Fire(this, FastBufferCallbackEventType.EmptyToNonEmpty);
                    return length;
                }
            }
        }


        public T[] ToArray() => Fifo.Span.ToArray();

        public T[] ItemsSlow { get => ToArray(); }
    }

    public class FastStreamFifo : FastStreamBuffer<byte>
    {
        public FastStreamFifo(bool enableEvents = false, long? thresholdLength = null)
            : base(enableEvents, thresholdLength) { }
    }

    public class FastDatagramFifo : FastDatagramBuffer<Datagram>
    {
        public FastDatagramFifo(bool enableEvents = false, long? thresholdLength = null)
            : base(enableEvents, thresholdLength) { }
    }

    public static class FastPipeHelper
    {
        public static async Task WaitForReadyToWrite(this IFastBufferState writer, CancellationToken cancel, int timeout)
        {
            LocalTimer timer = new LocalTimer();

            timer.AddTimeout(FastPipeGlobalConfig.PollingTimeout);
            long timeoutTick = timer.AddTimeout(timeout);

            while (writer.IsReadyToWrite == false)
            {
                if (FastTick64.Now >= timeoutTick) throw new TimeoutException();
                cancel.ThrowIfCancellationRequested();

                await WebSocketHelper.WaitObjectsAsync(
                    cancels: new CancellationToken[] { cancel },
                    events: new AsyncAutoResetEvent[] { writer.EventWriteReady },
                    timeout: timer.GetNextInterval()
                    );
            }

            cancel.ThrowIfCancellationRequested();
        }

        public static async Task WaitForReadyToRead(this IFastBufferState reader, CancellationToken cancel, int timeout)
        {
            LocalTimer timer = new LocalTimer();

            timer.AddTimeout(FastPipeGlobalConfig.PollingTimeout);
            long timeoutTick = timer.AddTimeout(timeout);

            while (reader.IsReadyToRead == false)
            {
                if (FastTick64.Now >= timeoutTick) throw new TimeoutException();
                cancel.ThrowIfCancellationRequested();

                await WebSocketHelper.WaitObjectsAsync(
                    cancels: new CancellationToken[] { cancel },
                    events: new AsyncAutoResetEvent[] { reader.EventReadReady },
                    timeout: timer.GetNextInterval()
                    );
            }

            cancel.ThrowIfCancellationRequested();
        }
    }

    public static class FastPipeGlobalConfig
    {
        public static int MaxStreamBufferLength = 4 * 65536;
        public static int MaxDatagramQueueLength = 65536;
        public static int MaxPollingTimeout = 256 * 1000;

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

    public sealed class FastPipe : IAsyncCleanupable
    {
        public CancelWatcher CancelWatcher { get; }

        FastStreamFifo StreamAtoB;
        FastStreamFifo StreamBtoA;
        FastDatagramFifo DatagramAtoB;
        FastDatagramFifo DatagramBtoA;

        public ExceptionQueue ExceptionQueue { get; } = new ExceptionQueue();
        public LayerStack LayerStack { get; } = new LayerStack();

        public FastPipeEnd A_LowerSide { get; }
        public FastPipeEnd B_UpperSide { get; }

        public List<Action> OnDisconnected { get; } = new List<Action>();

        public AsyncManualResetEvent OnDisconnectedEvent { get; } = new AsyncManualResetEvent();

        Once internalDisconnectedFlag;
        public bool IsDisconnected { get => internalDisconnectedFlag.IsSet; }

        public AsyncCleanuper AsyncCleanuper { get; }

        public FastPipe(CancellationToken cancel = default(CancellationToken), long? thresholdLengthStream = null, long? thresholdLengthDatagram = null)
        {
            CancelWatcher = new CancelWatcher(cancel);

            if (thresholdLengthStream == null) thresholdLengthStream = FastPipeGlobalConfig.MaxStreamBufferLength;
            if (thresholdLengthDatagram == null) thresholdLengthDatagram = FastPipeGlobalConfig.MaxDatagramQueueLength;

            StreamAtoB = new FastStreamFifo(true, thresholdLengthStream);
            StreamBtoA = new FastStreamFifo(true, thresholdLengthStream);

            DatagramAtoB = new FastDatagramFifo(true, thresholdLengthDatagram);
            DatagramBtoA = new FastDatagramFifo(true, thresholdLengthDatagram);

            StreamAtoB.ExceptionQueue.Encounter(ExceptionQueue);
            StreamBtoA.ExceptionQueue.Encounter(ExceptionQueue);

            DatagramAtoB.ExceptionQueue.Encounter(ExceptionQueue);
            DatagramBtoA.ExceptionQueue.Encounter(ExceptionQueue);

            StreamAtoB.LayerStack.Encounter(LayerStack);
            StreamBtoA.LayerStack.Encounter(LayerStack);

            DatagramAtoB.LayerStack.Encounter(LayerStack);
            DatagramBtoA.LayerStack.Encounter(LayerStack);

            StreamAtoB.OnDisconnected.Add(() => Disconnect());
            StreamBtoA.OnDisconnected.Add(() => Disconnect());

            DatagramAtoB.OnDisconnected.Add(() => Disconnect());
            DatagramBtoA.OnDisconnected.Add(() => Disconnect());

            A_LowerSide = new FastPipeEnd(this, FastPipeEndSide.A_LowerSide, CancelWatcher, StreamAtoB, StreamBtoA, DatagramAtoB, DatagramBtoA);
            B_UpperSide = new FastPipeEnd(this, FastPipeEndSide.B_UpperSide, CancelWatcher, StreamBtoA, StreamAtoB, DatagramBtoA, DatagramAtoB);

            A_LowerSide._InternalSetCounterPart(B_UpperSide);
            B_UpperSide._InternalSetCounterPart(A_LowerSide);

            CancelWatcher.CancelToken.Register(() =>
            {
                Disconnect(new OperationCanceledException());
            });

            AsyncCleanuper = new AsyncCleanuper(this);
        }

        object LayerInfoLock = new object();

        public LayerInfoBase LayerInfo_A_LowerSide { get; private set; } = null;
        public LayerInfoBase LayerInfo_B_UpperSide { get; private set; } = null;

        public class InstalledLayerHolder : Holder<LayerInfoBase>
        {
            internal InstalledLayerHolder(Action<LayerInfoBase> disposeProc, LayerInfoBase userData = null) : base(disposeProc, userData) { }
        }

        internal InstalledLayerHolder _InternalInstallLayerInfo(FastPipeEndSide side, LayerInfoBase info)
        {
            if (info == null)
                throw new ArgumentNullException("info");

            lock (LayerInfoLock)
            {
                if (side == FastPipeEndSide.A_LowerSide)
                {
                    if (LayerInfo_A_LowerSide != null) throw new ApplicationException("LayerInfo_A_LowerSide is already installed.");
                    LayerStack.Install(info, LayerInfo_B_UpperSide, false);
                    LayerInfo_A_LowerSide = info;
                }
                else
                {
                    if (LayerInfo_B_UpperSide != null) throw new ApplicationException("LayerInfo_B_UpperSide is already installed.");
                    LayerStack.Install(info, LayerInfo_A_LowerSide, true);
                    LayerInfo_B_UpperSide = info;
                }

                return new InstalledLayerHolder(x =>
                {
                    lock (LayerInfoLock)
                    {
                        if (side == FastPipeEndSide.A_LowerSide)
                        {
                            Debug.Assert(LayerInfo_A_LowerSide != null);
                            LayerStack.Uninstall(LayerInfo_A_LowerSide);
                            LayerInfo_A_LowerSide = null;
                        }
                        else
                        {
                            Debug.Assert(LayerInfo_B_UpperSide != null);
                            LayerStack.Uninstall(LayerInfo_B_UpperSide);
                            LayerInfo_B_UpperSide = null;
                        }
                    }
                },
                info);
            }
        }

        public void Disconnect(Exception ex = null)
        {
            if (internalDisconnectedFlag.IsFirstCall())
            {
                if (ex != null)
                {
                    ExceptionQueue.Add(ex);
                }

                Action[] evList;
                lock (OnDisconnected)
                    evList = OnDisconnected.ToArray();

                foreach (var ev in evList)
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

                OnDisconnectedEvent.Set(true);
            }
        }

        Once DisposeFlag;
        public void Dispose()
        {
            if (DisposeFlag.IsFirstCall())
            {
                Disconnect();
                CancelWatcher.DisposeSafe();
            }
        }

        public async Task _CleanupAsyncInternal()
        {
            await CancelWatcher.AsyncCleanuper;
        }

        public void CheckDisconnected()
        {
            StreamAtoB.CheckDisconnected();
            StreamBtoA.CheckDisconnected();
            DatagramAtoB.CheckDisconnected();
            DatagramBtoA.CheckDisconnected();
        }
    }

    [Flags]
    public enum FastPipeEndSide
    {
        A_LowerSide,
        B_UpperSide,
    }

    [Flags]
    public enum FastPipeEndAttachDirection
    {
        NoAttach,
        FromLowerToA_LowerSide,
        FromUpperToB_UpperSide,
    }

    public class FastPipeEnd
    {
        FastPipe Pipe { get; }

        public FastPipeEndSide Side { get; }

        public CancelWatcher CancelWatcher { get; }
        public FastStreamFifo StreamWriter { get; }
        public FastStreamFifo StreamReader { get; }
        public FastDatagramFifo DatagramWriter { get; }
        public FastDatagramFifo DatagramReader { get; }

        public FastPipeEnd CounterPart { get; private set; }

        public AsyncManualResetEvent OnDisconnectedEvent { get => Pipe.OnDisconnectedEvent; }

        public ExceptionQueue ExceptionQueue { get => Pipe.ExceptionQueue; }
        public LayerStack LayerStack { get => Pipe.LayerStack; }

        public bool IsDisconnected { get => this.Pipe.IsDisconnected; }
        public void Disconnect(Exception ex = null) { this.Pipe.Disconnect(ex); }
        public void AddOnDisconnected(Action action)
        {
            lock (Pipe.OnDisconnected)
                Pipe.OnDisconnected.Add(action);
        }

        internal FastPipeEnd(FastPipe pipe, FastPipeEndSide side,
            CancelWatcher cancelWatcher,
            FastStreamFifo streamToWrite, FastStreamFifo streamToRead,
            FastDatagramFifo datagramToWrite, FastDatagramFifo datagramToRead)
        {
            this.Side = side;
            this.Pipe = pipe;
            this.CancelWatcher = cancelWatcher;
            this.StreamWriter = streamToWrite;
            this.StreamReader = streamToRead;
            this.DatagramWriter = datagramToWrite;
            this.DatagramReader = datagramToRead;
        }

        internal void _InternalSetCounterPart(FastPipeEnd p)
            => this.CounterPart = p;

        public sealed class AttachHandle : IAsyncCleanupable
        {
            public FastPipeEnd PipeEnd { get; }
            public object UserState { get; }
            public FastPipeEndAttachDirection Direction { get; }

            public AsyncCleanuper AsyncCleanuper { get; }

            FastPipe.InstalledLayerHolder InstalledLayerHolder = null;

            LeakChecker.Holder Leak;
            object LockObj = new object();

            public AttachHandle(FastPipeEnd end, FastPipeEndAttachDirection attachDirection, object userState = null)
            {
                if (end.Side == FastPipeEndSide.A_LowerSide)
                    Direction = FastPipeEndAttachDirection.FromLowerToA_LowerSide;
                else
                    Direction = FastPipeEndAttachDirection.FromUpperToB_UpperSide;

                if (attachDirection != Direction)
                    throw new ArgumentException($"attachDirection ({attachDirection}) != {Direction}");

                end.CheckDisconnected();

                lock (end.AttachHandleLock)
                {
                    if (end.CurrentAttachHandle != null)
                        throw new ApplicationException("The FastPipeEnd is already attached.");

                    this.UserState = userState;
                    this.PipeEnd = end;
                    this.PipeEnd.CurrentAttachHandle = this;
                }

                Leak = LeakChecker.Enter();

                AsyncCleanuper = new AsyncCleanuper(this);
            }

            public void SetLayerInfo(LayerInfoBase info, FastProtocolStackBase protocolStack = null)
            {
                lock (LockObj)
                {
                    if (DisposeFlag.IsSet) return;

                    if (InstalledLayerHolder != null)
                        throw new ApplicationException("LayerInfo is already set.");

                    info._InternalSetProtocolStack(protocolStack);

                    InstalledLayerHolder = PipeEnd.Pipe._InternalInstallLayerInfo(PipeEnd.Side, info);
                }
            }

            int receiveTimeoutProcId = 0;
            TimeoutDetector receiveTimeoutDetector = null;

            public void SetStreamTimeout(int recvTimeout = Timeout.Infinite, int sendTimeout = Timeout.Infinite)
            {
                SetStreamReceiveTimeout(recvTimeout);
                SetStreamSendTimeout(sendTimeout);
            }

            public void SetStreamReceiveTimeout(int timeout = Timeout.Infinite)
            {
                if (Direction == FastPipeEndAttachDirection.FromLowerToA_LowerSide)
                    throw new ApplicationException("The attachment direction is From_Lower_To_A_LowerSide.");

                lock (LockObj)
                {
                    if (DisposeFlag.IsSet) return;

                    if (timeout < 0 || timeout == int.MaxValue)
                    {
                        if (receiveTimeoutProcId != 0)
                        {
                            PipeEnd.StreamReader.EventListeners.UnregisterCallback(receiveTimeoutProcId);
                            receiveTimeoutProcId = 0;
                            receiveTimeoutDetector.DisposeSafe();
                        }
                    }
                    else
                    {
                        SetStreamReceiveTimeout(Timeout.Infinite);

                        receiveTimeoutDetector = new TimeoutDetector(timeout, callback: (x) =>
                        {
                            if (PipeEnd.StreamReader.IsReadyToWrite == false)
                                return true;
                            PipeEnd.Pipe.Disconnect(new TimeoutException("StreamReceiveTimeout"));
                            return false;
                        });

                        receiveTimeoutProcId = PipeEnd.StreamReader.EventListeners.RegisterCallback((buffer, type, state) =>
                        {
                            if (type == FastBufferCallbackEventType.Written || type == FastBufferCallbackEventType.NonEmptyToEmpty)
                                receiveTimeoutDetector.Keep();
                        });
                    }
                }
            }

            int sendTimeoutProcId = 0;
            TimeoutDetector sendTimeoutDetector = null;

            public void SetStreamSendTimeout(int timeout = Timeout.Infinite)
            {
                if (Direction == FastPipeEndAttachDirection.FromLowerToA_LowerSide)
                    throw new ApplicationException("The attachment direction is From_Lower_To_A_LowerSide.");

                lock (LockObj)
                {
                    if (DisposeFlag.IsSet) return;

                    if (timeout < 0 || timeout == int.MaxValue)
                    {
                        if (sendTimeoutProcId != 0)
                        {
                            PipeEnd.StreamWriter.EventListeners.UnregisterCallback(sendTimeoutProcId);
                            sendTimeoutProcId = 0;
                            sendTimeoutDetector.DisposeSafe();
                        }
                    }
                    else
                    {
                        SetStreamSendTimeout(Timeout.Infinite);

                        sendTimeoutDetector = new TimeoutDetector(timeout, callback: (x) =>
                        {
                            if (PipeEnd.StreamWriter.IsReadyToRead == false)
                                return true;

                            PipeEnd.Pipe.Disconnect(new TimeoutException("StreamSendTimeout"));
                            return false;
                        });

                        sendTimeoutProcId = PipeEnd.StreamWriter.EventListeners.RegisterCallback((buffer, type, state) =>
                        {
//                            WriteLine($"{type}  {buffer.Length}  {buffer.IsReadyToWrite}");
                            if (type == FastBufferCallbackEventType.Read || type == FastBufferCallbackEventType.EmptyToNonEmpty || type == FastBufferCallbackEventType.PartialProcessReadData)
                                sendTimeoutDetector.Keep();
                        });
                    }
                }
            }

            public FastPipeEndStream GetStream(bool autoFlush = true)
                => PipeEnd._InternalGetStream(autoFlush);

            Once DisposeFlag;
            public void Dispose()
            {
                if (DisposeFlag.IsFirstCall())
                {
                    lock (LockObj)
                    {
                        if (Direction == FastPipeEndAttachDirection.FromUpperToB_UpperSide)
                        {
                            SetStreamReceiveTimeout(Timeout.Infinite);
                            SetStreamSendTimeout(Timeout.Infinite);
                        }

                        if (InstalledLayerHolder != null)
                            InstalledLayerHolder.Dispose();
                        InstalledLayerHolder = null;
                    }

                    lock (PipeEnd.AttachHandleLock)
                    {
                        PipeEnd.CurrentAttachHandle = null;
                    }

                    receiveTimeoutDetector.DisposeSafe();
                    sendTimeoutDetector.DisposeSafe();

                    Leak.Dispose();
                }
            }

            public async Task _CleanupAsyncInternal()
            {
                await receiveTimeoutDetector.AsyncCleanuper;
                await sendTimeoutDetector.AsyncCleanuper;
            }
        }

        object AttachHandleLock = new object();
        AttachHandle CurrentAttachHandle = null;

        public AttachHandle Attach(FastPipeEndAttachDirection attachDirection, object userState = null) => new AttachHandle(this, attachDirection, userState);

        internal FastPipeEndStream _InternalGetStream(bool autoFlush = true)
            => FastPipeEndStream._InternalNew(this, autoFlush);

        public FastAppProtocolStub GetFastAppProtocolStub(CancellationToken cancel = default(CancellationToken))
            => new FastAppProtocolStub(this, cancel);

        public void CheckDisconnected() => Pipe.CheckDisconnected();
    }

    public sealed class FastPipeEndStream : NetworkStream
    {
        public bool AutoFlush { get; set; }
        public FastPipeEnd End { get; private set; }

        private FastPipeEndStream() : base(null) { }

        internal void _InternalInit(FastPipeEnd end, bool autoFlush = true)
        {
            End = end;
            AutoFlush = autoFlush;

            ReadTimeout = Timeout.Infinite;
            WriteTimeout = Timeout.Infinite;
        }

        internal static FastPipeEndStream _InternalNew(FastPipeEnd end, bool autoFlush = true)
        {
            end.CheckDisconnected();

            FastPipeEndStream ret = WebSocketHelper.NewWithoutConstructor<FastPipeEndStream>();

            ret._InternalInit(end, autoFlush);

            return ret;
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
            Memory<byte> sendData = buffer.ToArray();

            await FastSendAsync(sendData, cancel);

            if (AutoFlush) FastFlush(true, false);
        }

        public void Send(ReadOnlyMemory<byte> buffer, CancellationToken cancel = default(CancellationToken))
            => SendAsync(buffer, cancel).Wait();

        Once receiveAllAsyncRaiseExceptionFlag;

        public async Task ReceiveAllAsync(Memory<byte> buffer, CancellationToken cancel = default(CancellationToken))
        {
            while (buffer.Length >= 1)
            {
                int r = await ReceiveAsync(buffer, cancel);
                if (r <= 0)
                {
                    End.StreamReader.CheckDisconnected();

                    if (receiveAllAsyncRaiseExceptionFlag.IsFirstCall())
                    {
                        End.StreamReader.ExceptionQueue.Raise(new FastBufferDisconnectedException());
                    }
                    else
                    {
                        End.StreamReader.ExceptionQueue.ThrowFirstExceptionIfExists();
                        throw new FastBufferDisconnectedException();
                    }
                }
                buffer.Walk(r);
            }
        }

        public async Task<Memory<byte>> ReceiveAllAsync(int size, CancellationToken cancel = default(CancellationToken))
        {
            Memory<byte> buffer = new byte[size];
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

        public async Task<Memory<byte>> ReceiveAsync(int maxSize = int.MaxValue, CancellationToken cancel = default(CancellationToken))
        {
            try
            {
                LABEL_RETRY:
                await WaitReadyToReceiveAsync(cancel, ReadTimeout);

                Memory<byte> ret;

                lock (End.StreamReader.LockObj)
                    ret = End.StreamReader.DequeueContiguousSlow(maxSize);

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

        public void ReceiveAll(Memory<byte> buffer, CancellationToken cancel = default(CancellationToken))
            => ReceiveAllAsync(buffer, cancel).Wait();

        public Memory<byte> ReceiveAll(int size, CancellationToken cancel = default(CancellationToken))
            => ReceiveAllAsync(size, cancel).Result;

        public int Receive(Memory<byte> buffer, CancellationToken cancel = default(CancellationToken))
            => ReceiveAsync(buffer, cancel).Result;

        public Memory<byte> Receive(int maxSize = int.MaxValue, CancellationToken cancel = default(CancellationToken))
            => ReceiveAsync(maxSize, cancel).Result;

        public async Task<List<Memory<byte>>> FastReceiveAsync(CancellationToken cancel = default(CancellationToken), RefInt totalRecvSize = null)
        {
            try
            {
                LABEL_RETRY:
                await WaitReadyToReceiveAsync(cancel, ReadTimeout);

                var ret = End.StreamReader.DequeueAllWithLock(out long totalReadSize);

                if (totalRecvSize != null)
                    totalRecvSize.Set((int)totalReadSize);

                if (totalReadSize == 0)
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

        public async Task<List<Memory<byte>>> FastPeekAsync(int maxSize = int.MaxValue, CancellationToken cancel = default(CancellationToken), RefInt totalRecvSize = null)
        {
            LABEL_RETRY:
            CheckDisconnect();
            await WaitReadyToReceiveAsync(cancel, ReadTimeout);
            CheckDisconnect();

            long totalReadSize;
            FastBufferSegment<Memory<byte>>[] tmp;
            lock (End.StreamReader.LockObj)
            {
                tmp = End.StreamReader.GetSegmentsFast(End.StreamReader.PinHead, maxSize, out totalReadSize, true);
            }

            if (totalRecvSize != null)
                totalRecvSize.Set((int)totalReadSize);

            if (totalReadSize == 0)
            {
                await Task.Yield();
                goto LABEL_RETRY;
            }

            List<Memory<byte>> ret = new List<Memory<byte>>();
            foreach (FastBufferSegment<Memory<byte>> item in tmp)
                ret.Add(item.Item);

            return ret;
        }

        public async Task<Memory<byte>> FastPeekContiguousAsync(int maxSize = int.MaxValue, CancellationToken cancel = default(CancellationToken))
        {
            LABEL_RETRY:
            CheckDisconnect();
            await WaitReadyToReceiveAsync(cancel, ReadTimeout);
            CheckDisconnect();

            Memory<byte> ret;

            lock (End.StreamReader.LockObj)
            {
                 ret = End.StreamReader.GetContiguous(End.StreamReader.PinHead, maxSize, true);
            }

            if (ret.Length == 0)
            {
                await Task.Yield();
                goto LABEL_RETRY;
            }

            return ret;
        }

        public async Task<Memory<byte>> PeekAsync(int maxSize = int.MaxValue, CancellationToken cancel = default(CancellationToken))
            => (await FastPeekContiguousAsync(maxSize, cancel)).ToArray();

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

        public async Task SendToAsync(ReadOnlyMemory<byte> buffer, EndPoint remoteEndPoint, CancellationToken cancel = default(CancellationToken))
        {
            Datagram sendData = new Datagram(buffer.Span.ToArray(), remoteEndPoint);

            await FastSendToAsync(sendData, cancel);

            if (AutoFlush) FastFlush(false, true);
        }

        public void SendTo(ReadOnlyMemory<byte> buffer, EndPoint remoteEndPoint, CancellationToken cancel = default(CancellationToken))
            => SendToAsync(buffer, remoteEndPoint, cancel).Wait();

        public async Task<List<Datagram>> FastReceiveFromAsync(CancellationToken cancel = default(CancellationToken))
        {
            LABEL_RETRY:
            await WaitReadyToReceiveFromAsync(cancel, ReadTimeout);

            var ret = End.DatagramReader.DequeueAllWithLock(out long totalReadSize);
            if (totalReadSize == 0)
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

            List<Datagram> dataList;

            long totalReadSize;

            lock (End.DatagramReader.LockObj)
            {
                dataList = End.DatagramReader.Dequeue(1, out totalReadSize);
            }

            if (totalReadSize == 0)
            {
                await Task.Yield();
                goto LABEL_RETRY;
            }

            Debug.Assert(dataList.Count == 1);

            End.DatagramReader.CompleteRead();

            return dataList[0];
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

        public int ReceiveFrom(Memory<byte> buffer, out EndPoint remoteEndPoint, CancellationToken cancel = default(CancellationToken))
        {
            SocketReceiveFromResult r = ReceiveFromAsync(buffer, cancel).Result;

            remoteEndPoint = r.RemoteEndPoint;

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
        public override int ReadTimeout { get; set; }
        public override int WriteTimeout { get; set; }

        public override bool DataAvailable => IsReadyToReceive;

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

        Once DisposeFlag;
        protected override void Dispose(bool disposing)
        {
            if (DisposeFlag.IsFirstCall() && disposing)
            {
            }
        }

        public override bool Equals(object obj) => object.Equals(this, obj);

        public override int GetHashCode() => 0;

        public override string ToString() => "FastPipeEndStream";

        public override object InitializeLifetimeService() => base.InitializeLifetimeService();

        public override void Close() => Dispose(true);

        public override void CopyTo(Stream destination, int bufferSize)
        {
            byte[] array = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                int count;
                while ((count = this.Read(array, 0, array.Length)) != 0)
                {
                    destination.Write(array, 0, count);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(array, false);
            }
        }

        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                for (; ; )
                {
                    int num = await this.ReadAsync(new Memory<byte>(buffer), cancellationToken).ConfigureAwait(false);
                    int num2 = num;
                    if (num2 == 0)
                    {
                        break;
                    }
                    await destination.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, num2), cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer, false);
            }
        }

        [Obsolete]
        protected override WaitHandle CreateWaitHandle() => new ManualResetEvent(false);

        [Obsolete]
        protected override void ObjectInvariant() { }

        public override int Read(Span<byte> buffer)
        {
            byte[] array = ArrayPool<byte>.Shared.Rent(buffer.Length);
            int result;
            try
            {
                int num = this.Read(array, 0, buffer.Length);
                if ((ulong)num > (ulong)((long)buffer.Length))
                {
                    throw new IOException("StreamTooLong");
                }
                new Span<byte>(array, 0, num).CopyTo(buffer);
                result = num;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(array, false);
            }
            return result;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => await ReceiveAsync(buffer, cancellationToken);

        public override int ReadByte()
        {
            byte[] array = new byte[1];
            if (this.Read(array, 0, 1) == 0)
            {
                return -1;
            }
            return (int)array[0];
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            byte[] array = ArrayPool<byte>.Shared.Rent(buffer.Length);
            try
            {
                buffer.CopyTo(array);
                this.Write(array, 0, buffer.Length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(array, false);
            }
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => await this.SendAsync(buffer, cancellationToken);

        public override void WriteByte(byte value)
            => this.Write(new byte[] { value }, 0, 1);

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
            byte[] newState = SnapshotState(salt);
            if (LastState.SequenceEqual(newState))
                return false;
            LastState = newState;
            return true;
        }

        public async Task<bool> WaitIfNothingChanged(int timeout = Timeout.Infinite, int salt = 0)
        {
            timeout = WebSocketHelper.GetMinTimeout(timeout, FastPipeGlobalConfig.PollingTimeout);
            if (timeout == 0) return false;
            if (IsStateChanged(salt)) return false;

            await WebSocketHelper.WaitObjectsAsync(
                cancels: WaitCancelList.ToArray(),
                events: WaitEventList.ToArray(),
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

    public abstract class FastPipeEndAsyncObjectWrapperBase : IAsyncCleanupable
    {
        public CancelWatcher CancelWatcher { get; }
        public FastPipeEnd PipeEnd { get; }
        public abstract PipeSupportedDataTypes SupportedDataTypes { get; }
        Task MainLoopTask = Task.CompletedTask;

        public ExceptionQueue ExceptionQueue { get => PipeEnd.ExceptionQueue; }
        public LayerStack LayerStack { get => PipeEnd.LayerStack; }

        public AsyncCleanuper AsyncCleanuper { get; }

        public FastPipeEndAsyncObjectWrapperBase(FastPipeEnd pipeEnd, CancellationToken cancel = default(CancellationToken))
        {
            PipeEnd = pipeEnd;
            CancelWatcher = new CancelWatcher(cancel);
            AsyncCleanuper = new AsyncCleanuper(this);
        }

        public async Task _CleanupAsyncInternal()
        {
            await MainLoopTask.TryWaitAsync(true);

            await CancelWatcher.AsyncCleanuper;
        }

        Once ConnectedFlag;
        protected void BaseStart()
        {
            if (ConnectedFlag.IsFirstCall())
                MainLoopTask = MainLoopAsync();
        }

        async Task MainLoopAsync()
        {
            try
            {
                List<Task> tasks = new List<Task>();

                if (SupportedDataTypes.Bit(PipeSupportedDataTypes.Stream))
                {
                    tasks.Add(StreamReadFromPipeLoopAsync());
                    tasks.Add(StreamWriteToPipeLoopAsync());
                }

                if (SupportedDataTypes.Bit(PipeSupportedDataTypes.Datagram))
                {
                    tasks.Add(DatagramReadFromPipeLoopAsync());
                    tasks.Add(DatagramWriteToPipeLoopAsync());
                }

                await Task.WhenAll(tasks.ToArray());
            }
            catch (Exception ex)
            {
                Disconnect(ex);
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
            using (LeakChecker.Enter())
            {
                try
                {
                    var reader = PipeEnd.StreamReader;
                    while (true)
                    {
                        bool stateChanged;
                        do
                        {
                            stateChanged = false;

                            CancelWatcher.CancelToken.ThrowIfCancellationRequested();

                            while (reader.IsReadyToRead)
                            {
                                await StreamWriteToObject(reader, CancelWatcher.CancelToken);
                                stateChanged = true;
                            }
                        }
                        while (stateChanged);

                        await WebSocketHelper.WaitObjectsAsync(
                            events: new AsyncAutoResetEvent[] { reader.EventReadReady },
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
            using (LeakChecker.Enter())
            {
                try
                {
                    var writer = PipeEnd.StreamWriter;
                    while (true)
                    {
                        bool stateChanged;
                        do
                        {
                            stateChanged = false;

                            CancelWatcher.CancelToken.ThrowIfCancellationRequested();

                            if (writer.IsReadyToWrite)
                            {
                                long lastTail = writer.PinTail;
                                await StreamReadFromObject(writer, CancelWatcher.CancelToken);
                                if (writer.PinTail != lastTail)
                                {
                                    stateChanged = true;
                                }
                            }

                        }
                        while (stateChanged);

                        await WebSocketHelper.WaitObjectsAsync(
                            events: new AsyncAutoResetEvent[] { writer.EventWriteReady },
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
            using (LeakChecker.Enter())
            {
                try
                {
                    var reader = PipeEnd.DatagramReader;
                    while (true)
                    {
                        bool stateChanged;
                        do
                        {
                            stateChanged = false;

                            CancelWatcher.CancelToken.ThrowIfCancellationRequested();

                            while (reader.IsReadyToRead)
                            {
                                await DatagramWriteToObject(reader, CancelWatcher.CancelToken);
                                stateChanged = true;
                            }
                        }
                        while (stateChanged);

                        await WebSocketHelper.WaitObjectsAsync(
                            events: new AsyncAutoResetEvent[] { reader.EventReadReady },
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
            using (LeakChecker.Enter())
            {
                try
                {
                    var writer = PipeEnd.DatagramWriter;
                    while (true)
                    {
                        bool stateChanged;
                        do
                        {
                            stateChanged = false;

                            CancelWatcher.CancelToken.ThrowIfCancellationRequested();

                            if (writer.IsReadyToWrite)
                            {
                                long lastTail = writer.PinTail;
                                await DatagramReadFromObject(writer, CancelWatcher.CancelToken);
                                if (writer.PinTail != lastTail)
                                {
                                    stateChanged = true;
                                }
                            }

                        }
                        while (stateChanged);

                        await WebSocketHelper.WaitObjectsAsync(
                            events: new AsyncAutoResetEvent[] { writer.EventWriteReady },
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

        Once DisconnectedFlag;
        public void Disconnect(Exception ex = null)
        {
            if (DisconnectedFlag.IsFirstCall())
            {
                this.PipeEnd.Disconnect(ex);
                CancelWatcher.Cancel();

                foreach (var proc in OnDisconnectedList) try { proc(); } catch { };
            }
        }

        Once DisposeFlag;
        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing)
        {
            if (DisposeFlag.IsFirstCall() && disposing)
            {
                Disconnect();
                CancelWatcher.DisposeSafe();
            }
        }
    }

    public sealed class FastPipeEndSocketWrapper : FastPipeEndAsyncObjectWrapperBase
    {
        public Socket Socket { get; }
        public int RecvTmpBufferSize { get; private set; }
        public override PipeSupportedDataTypes SupportedDataTypes { get; }

        static bool UseDontLingerOption = true;

        public FastPipeEndSocketWrapper(FastPipeEnd pipeEnd, Socket socket, CancellationToken cancel = default(CancellationToken)) : base(pipeEnd, cancel)
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

            this.BaseStart();
        }

        protected override async Task StreamWriteToObject(FastStreamFifo fifo, CancellationToken cancel)
        {
            if (SupportedDataTypes.Bit(PipeSupportedDataTypes.Stream) == false) throw new NotSupportedException();

            List<Memory<byte>> sendArray;

            sendArray = fifo.DequeueAllWithLock(out long totalReadSize);
            fifo.CompleteRead();

            List<ArraySegment<byte>> sendArray2 = new List<ArraySegment<byte>>();
            foreach (Memory<byte> mem in sendArray)
            {
                sendArray2.Add(mem.AsSegment());
            }

            //List<List<ArraySegment<byte>>> send_array3 = MemoryHelper.SplitMemoryArrayToArraySegment(send_array, int.MaxValue);

            await WebSocketHelper.DoAsyncWithTimeout(
                async c =>
                {
                    //foreach (var send_group in send_array3)
                    //{
                    //    await Socket.SendAsync(send_group, SocketFlags.None);
                    //    fifo.EventListeners.Fire(fifo, FastBufferCallbackEventType.PartialProcessReadData);
                    //}

                    int ret = await Socket.SendAsync(sendArray2, SocketFlags.None);
                    return 0;
                },
                cancel: cancel);
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
            if (SupportedDataTypes.Bit(PipeSupportedDataTypes.Stream) == false) throw new NotSupportedException();

            Memory<byte>[] recvList = await StreamBulkReceiver.Recv(cancel, this);

            if (recvList == null)
            {
                // disconnected
                fifo.Disconnect();
                return;
            }

            fifo.EnqueueAllWithLock(recvList);

            fifo.CompleteWrite();
        }

        protected override async Task DatagramWriteToObject(FastDatagramFifo fifo, CancellationToken cancel)
        {
            if (SupportedDataTypes.Bit(PipeSupportedDataTypes.Datagram) == false) throw new NotSupportedException();

            List<Datagram> sendList;

            sendList = fifo.DequeueAllWithLock(out _);
            fifo.CompleteRead();

            await WebSocketHelper.DoAsyncWithTimeout(
                async c =>
                {
                    foreach (Datagram data in sendList)
                    {
                        cancel.ThrowIfCancellationRequested();
                        await Socket.SendToSafeUdpErrorAsync(data.Data.AsSegment(), SocketFlags.None, data.EndPoint);
                    }
                    return 0;
                },
                cancel: cancel);
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
            if (SupportedDataTypes.Bit(PipeSupportedDataTypes.Datagram) == false) throw new NotSupportedException();

            Datagram[] pkts = await DatagramBulkReceiver.Recv(cancel, this);

            fifo.EnqueueAllWithLock(pkts);

            fifo.CompleteWrite();
        }

        Once DisposeFlag;
        protected override void Dispose(bool disposing)
        {
            if (DisposeFlag.IsFirstCall() && disposing)
            {
                Socket.DisposeSafe();
            }
            base.Dispose(disposing);
        }
    }

    public sealed class FastPipeEndStreamWrapper : FastPipeEndAsyncObjectWrapperBase
    {
        public Stream Stream { get; }
        public int RecvTmpBufferSize { get; private set; }
        public const int SendTmpBufferSize = 65536;
        public override PipeSupportedDataTypes SupportedDataTypes { get; }

        Memory<byte> SendTmpBuffer = new byte[SendTmpBufferSize];

        //static bool UseDontLingerOption = true;

        public FastPipeEndStreamWrapper(FastPipeEnd pipeEnd, Stream stream, CancellationToken cancel = default(CancellationToken)) : base(pipeEnd, cancel)
        {
            this.Stream = stream;
            SupportedDataTypes = PipeSupportedDataTypes.Stream;

            NetworkStream net = Stream as NetworkStream;
            if (net != null)
            {

                //Socket socket = null;

                //Stream.LingerState = new LingerOption(false, 0);
                //try
                //{
                //    if (UseDontLingerOption)
                //        Stream.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
                //}
                //catch
                //{
                //    UseDontLingerOption = false;
                //}
                //Stream.NoDelay = true;
            }
            Stream.ReadTimeout = Stream.WriteTimeout = Timeout.Infinite;
            this.AddOnDisconnected(() => Stream.DisposeSafe());

            this.BaseStart();
        }

        protected override async Task StreamWriteToObject(FastStreamFifo fifo, CancellationToken cancel)
        {
            if (SupportedDataTypes.Bit(PipeSupportedDataTypes.Stream) == false) throw new NotSupportedException();

            await WebSocketHelper.DoAsyncWithTimeout(
                async c =>
                {
                    while (true)
                    {
                        int size = fifo.DequeueContiguousSlow(SendTmpBuffer, SendTmpBuffer.Length);
                        if (size == 0)
                            break;

                        await Stream.WriteAsync(SendTmpBuffer.Slice(0, size));
                    }

                    return 0;
                },
                cancel: cancel);
        }

        FastMemoryAllocator<byte> FastMemoryAllocatorForStream = new FastMemoryAllocator<byte>();

        AsyncBulkReceiver<Memory<byte>, FastPipeEndStreamWrapper> StreamBulkReceiver = new AsyncBulkReceiver<Memory<byte>, FastPipeEndStreamWrapper>(async me =>
        {
            if (me.RecvTmpBufferSize == 0)
            {
                int i = 65536;
                me.RecvTmpBufferSize = Math.Min(i, FastPipeGlobalConfig.MaxStreamBufferLength);
            }

            Memory<byte> tmp = me.FastMemoryAllocatorForStream.Reserve(me.RecvTmpBufferSize);
            int r = await me.Stream.ReadAsync(tmp);
            if (r < 0) throw new BaseStreamDisconnectedException();
            me.FastMemoryAllocatorForStream.Commit(ref tmp, r);
            if (r == 0) return new ValueOrClosed<Memory<byte>>();
            return new ValueOrClosed<Memory<byte>>(tmp);
        });

        protected override async Task StreamReadFromObject(FastStreamFifo fifo, CancellationToken cancel)
        {
            if (SupportedDataTypes.Bit(PipeSupportedDataTypes.Stream) == false) throw new NotSupportedException();

            Memory<byte>[] recvList = await StreamBulkReceiver.Recv(cancel, this);

            if (recvList == null)
            {
                // disconnected
                fifo.Disconnect();
                return;
            }

            fifo.EnqueueAllWithLock(recvList);

            fifo.CompleteWrite();
        }

        protected override Task DatagramWriteToObject(FastDatagramFifo fifo, CancellationToken cancel)
            => throw new NotSupportedException();

        protected override Task DatagramReadFromObject(FastDatagramFifo fifo, CancellationToken cancel)
            => throw new NotSupportedException();

        Once DisposeFlag;
        protected override void Dispose(bool disposing)
        {
            if (DisposeFlag.IsFirstCall() && disposing)
            {
                Stream.DisposeSafe();
            }
            base.Dispose(disposing);
        }
    }

    public abstract class FastProtocolOptionsBase { }

    public abstract class FastProtocolStackBase : IAsyncCleanupable
    {
        public virtual AsyncCleanuper AsyncCleanuper { get; }

        public CancelWatcher CancelWatcher;

        public AsyncManualResetEvent InitSuccessOrFailEvent = new AsyncManualResetEvent();

        public FastProtocolOptionsBase ProtocolOptions { get; }

        public FastProtocolStackBase(FastProtocolOptionsBase options, CancellationToken cancel = default(CancellationToken))
        {
            ProtocolOptions = options;
            CancelWatcher = new CancelWatcher(cancel);
            AsyncCleanuper = new AsyncCleanuper(this);
        }

        Task rootMainTask;

        Once startLoopFlag;

        protected void BaseStart()
        {
            if (startLoopFlag.IsFirstCall() == false) return;

            rootMainTask = RootMainAsync();
        }

        protected void CheckPointInitSuccessOrFail() => InitSuccessOrFailEvent.Set(true);

        public Task WaitInitSuccessOrFailAsync() => InitSuccessOrFailEvent.WaitAsync();

        async Task RootMainAsync()
        {
            using (LeakChecker.Enter())
            {
                try
                {
                    await MiddleMainAsync();
                }
                finally
                {
                    CheckPointInitSuccessOrFail();
                }
            }
        }

        protected abstract Task MiddleMainAsync();

        Once DisposeFlag;

        public void Dispose() => Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
            if (DisposeFlag.IsFirstCall() && disposing)
            {
                CancelWatcher.DisposeSafe();
            }
        }

        public virtual async Task _CleanupAsyncInternal()
        {
            await rootMainTask.TryWaitAsync(true);

            await CancelWatcher.AsyncCleanuper;
        }
    }

    public abstract class FastTopProtocolOptionsBase : FastProtocolOptionsBase { }

    public abstract class FastTopProtocolStubBase : FastProtocolStackBase
    {
        public FastPipeEnd Lower { get; }

        public FastTopProtocolOptionsBase TopOptions { get; }

        public FastTopProtocolStubBase(FastPipeEnd lower, FastTopProtocolOptionsBase options, CancellationToken cancel = default) : base(options, cancel)
        {
            TopOptions = options;
            Lower = lower;
        }

        public abstract void Init(FastPipeEnd.AttachHandle attachHandle);

        public abstract void Free();

        protected sealed override async Task MiddleMainAsync()
        {
            AsyncCleanuperLady lady = new AsyncCleanuperLady();

            try
            {
                using (var lowerAttach = Lower.Attach(FastPipeEndAttachDirection.FromUpperToB_UpperSide))
                {
                    lady.Add(lowerAttach);

                    CheckPointInitSuccessOrFail();

                    Init(lowerAttach);

                    await WebSocketHelper.WaitObjectsAsync(
                        cancels: CancelWatcher.CancelToken.ToSingleArray(),
                        manualEvents: new AsyncManualResetEvent[] { Lower.OnDisconnectedEvent },
                        exceptions: ExceptionWhen.CancelException
                        );
                }
            }
            catch (Exception ex)
            {
                Lower.Disconnect(ex);

                throw ex;
            }
            finally
            {
                CheckPointInitSuccessOrFail();

                Lower.Disconnect();

                await lady;
            }
        }
    }

    public class FastAppProtocolOptions : FastTopProtocolOptionsBase { }

    public class FastAppProtocolStub : FastTopProtocolStubBase
    {
        public FastAppProtocolStub(FastPipeEnd lower, CancellationToken cancel = default, FastAppProtocolOptions options = null) : base(lower, options, cancel)
        {
            BaseStart();
        }

        object LockObj = new object();

        FastPipeEndStream StreamCache = null;

        FastPipeEnd.AttachHandle AttachHandleCache = null;

        public override void Init(FastPipeEnd.AttachHandle attachHandle)
        {
            AttachHandleCache = attachHandle;
        }

        public override void Free() { }

        public async Task<FastPipeEndStream> GetStreamAsync(bool autoFlash = true)
        {
            await WaitInitSuccessOrFailAsync();
            Lower.CheckDisconnected();

            lock (LockObj)
            {
                if (StreamCache == null)
                    StreamCache = AttachHandleCache.GetStream(autoFlash);

                return StreamCache;
            }
        }

        public async Task<FastPipeEnd> GetPipeEndAsync()
        {
            await WaitInitSuccessOrFailAsync();
            Lower.CheckDisconnected();

            return Lower;
        }

        public async Task<FastPipeEnd.AttachHandle> GetAttachHandleAsync()
        {
            await WaitInitSuccessOrFailAsync();
            Lower.CheckDisconnected();

            return AttachHandleCache;
        }


        Once DisposeFlag;
        protected override void Dispose(bool disposing)
        {
            if (DisposeFlag.IsFirstCall() && disposing)
            {
                StreamCache.DisposeSafe();
            }
            base.Dispose(disposing);
        }
    }

    public abstract class FastBottomProtocolOptionsBase : FastProtocolOptionsBase { }

    public abstract class FastBottomProtocolStubBase : FastProtocolStackBase
    {
        public FastPipeEnd Upper { get; }

        public FastBottomProtocolOptionsBase BottomOptions { get; }

        public FastBottomProtocolStubBase(FastPipeEnd upper, FastBottomProtocolOptionsBase options, CancellationToken cancel = default(CancellationToken))
            : base(options, cancel)
        {
            BottomOptions = options;
            Upper = upper;
        }

        public abstract Task<AsyncHolder<LayerInfoBase>> ConnectAsync(FastPipeEnd.AttachHandle upperAttach, FastBottomProtocolOptionsBase options);

        protected sealed override async Task MiddleMainAsync()
        {
            AsyncCleanuperLady lady = new AsyncCleanuperLady();

            try
            {
                using (var upperAttach = Upper.Attach(FastPipeEndAttachDirection.FromLowerToA_LowerSide))
                {
                    lady.Add(upperAttach);

                    using (var holder = await ConnectAsync(upperAttach, BottomOptions))
                    {
                        lady.Add(holder);

                        upperAttach.SetLayerInfo(holder.UserData, this);

                        CheckPointInitSuccessOrFail();

                        await WebSocketHelper.WaitObjectsAsync(
                            cancels: CancelWatcher.CancelToken.ToSingleArray(),
                            manualEvents: new AsyncManualResetEvent[] { Upper.OnDisconnectedEvent },
                            exceptions: ExceptionWhen.CancelException
                            );
                    }
                }
            }
            catch (Exception ex)
            {
                Upper.Disconnect(ex);

                throw ex;
            }
            finally
            {
                CheckPointInitSuccessOrFail();

                Upper.Disconnect();

                await lady;
            }
        }
    }

    public abstract class FastTcpProtocolOptionsBase : FastBottomProtocolOptionsBase
    {
        public const int DefaultTcpConnectTimeout = 15 * 1000;

        public IPEndPoint LocalEndPoint { get; set; } = null;
        public IPEndPoint RemoteEndPoint { get; set; } = null;
        public int ConnectTimeout { get; set; } = DefaultTcpConnectTimeout;
    }

    public abstract class FastTcpProtocolStubBase : FastBottomProtocolStubBase
    {
        public FastTcpProtocolStubBase(FastPipeEnd upper, FastTcpProtocolOptionsBase options, CancellationToken cancel = default) : base(upper, options, cancel) { }
    }

    public enum FastTcpSockProtocolInitMode
    {
        ByRemoteEndPoint,
        ByConnectedSocketObject,
    }

    public class FastTcpSockProtocolOptions : FastTcpProtocolOptionsBase
    {
        public FastTcpSockProtocolInitMode InitMode { get; set; } = FastTcpSockProtocolInitMode.ByRemoteEndPoint;
        public Socket SocketObject { get; set; } = null;
    }

    public sealed class FastTcpSockProtocolStub : FastTcpProtocolStubBase
    {
        public class LayerInfo : LayerInfoBase, ILayerInfoTcpEndPoint
        {
            public int LocalPort { get; set; }
            public int RemotePort { get; set; }
            public IPAddress LocalIPAddress { get; set; }
            public IPAddress RemoteIPAddress { get; set; }
        }

        public FastTcpSockProtocolStub(FastPipeEnd upper, FastTcpSockProtocolOptions options, CancellationToken cancel = default) : base(upper, options, cancel)
        {
            BaseStart();
        }

        public override async Task<AsyncHolder<LayerInfoBase>> ConnectAsync(FastPipeEnd.AttachHandle upperAttach, FastBottomProtocolOptionsBase options)
        {
            FastTcpSockProtocolOptions opt = (FastTcpSockProtocolOptions)options;

            Socket s = null;

            if (opt.InitMode == FastTcpSockProtocolInitMode.ByRemoteEndPoint)
            {
                if (!(opt.RemoteEndPoint.AddressFamily == AddressFamily.InterNetwork || opt.RemoteEndPoint.AddressFamily == AddressFamily.InterNetworkV6))
                    throw new ArgumentException("RemoteEndPoint.AddressFamily");

                s = new Socket(opt.RemoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await WebSocketHelper.DoAsyncWithTimeout(async (c) =>
                    {
                        await s.ConnectAsync(opt.RemoteEndPoint);
                        return 0;
                    },
                    cancelProc: () =>
                    {
                        s.DisposeSafe();
                    },
                    timeout: opt.ConnectTimeout,
                    cancel: CancelWatcher.CancelToken);
                }
                catch
                {
                    s.DisposeSafe();
                    throw;
                }
            }
            else
            {
                s = opt.SocketObject;
            }

            var socketWrapper = new FastPipeEndSocketWrapper(upperAttach.PipeEnd, s, CancelWatcher.CancelToken);

            return new AsyncHolder<LayerInfoBase>(async x =>
            {
                s.DisposeSafe();
                await socketWrapper.AsyncCleanuper;
            },
            userData: new LayerInfo()
            {
                LocalPort = ((IPEndPoint)s.LocalEndPoint).Port,
                LocalIPAddress = ((IPEndPoint)s.LocalEndPoint).Address,
                RemotePort = ((IPEndPoint)s.RemoteEndPoint).Port,
                RemoteIPAddress = ((IPEndPoint)s.RemoteEndPoint).Address,
            });
        }
    }

    public sealed class FastTcpSock : IAsyncCleanupable
    {
        public AsyncCleanuper AsyncCleanuper { get; }
        public FastPipe Pipe { get; }
        
        public FastPipeEnd UpperSidePipeEnd { get => Pipe.B_UpperSide; }

        FastTcpSockProtocolStub Stub;

        private FastTcpSock(FastPipe pipe, FastTcpSockProtocolStub stub)
        {
            this.Pipe = pipe;
            this.Stub = stub;

            this.Pipe.CheckDisconnected();

            AsyncCleanuper = new AsyncCleanuper(this);
        }

        Once DisposeFlag;
        public void Dispose()
        {
            if (DisposeFlag.IsFirstCall() == false) return;

            this.Pipe.DisposeSafe();
            this.Stub.DisposeSafe();
        }

        public async Task _CleanupAsyncInternal()
        {
            await this.Pipe.AsyncCleanuper;
            await this.Stub.AsyncCleanuper;
        }

        public FastAppProtocolStub GetFastAppProtocolStub(CancellationToken cancel = default(CancellationToken))
            => new FastAppProtocolStub(this.UpperSidePipeEnd, cancel);


        public static async Task<FastTcpSock> ConnectAsync(string host, int port, AddressFamily? addressFamily = null, CancellationToken cancel = default(CancellationToken), int timeout = FastTcpProtocolOptionsBase.DefaultTcpConnectTimeout)
            => await ConnectAsync(await GetIPFromHostName(host, addressFamily, cancel, timeout), port, cancel, timeout);

        public static Task<FastTcpSock> ConnectAsync(IPAddress ip, int port, CancellationToken cancel = default(CancellationToken), int connectTimeout = FastTcpProtocolOptionsBase.DefaultTcpConnectTimeout)
            => ConnectAsync(new IPEndPoint(ip, port), cancel, connectTimeout);

        public static async Task<FastTcpSock> ConnectAsync(IPEndPoint endPoint, CancellationToken cancel = default(CancellationToken), int connectTimeout = FastTcpProtocolOptionsBase.DefaultTcpConnectTimeout)
        {
            var options = new FastTcpSockProtocolOptions()
            {
                InitMode = FastTcpSockProtocolInitMode.ByRemoteEndPoint,
                ConnectTimeout = connectTimeout,
                RemoteEndPoint = endPoint,
            };

            var pipe = new FastPipe(cancel);
            try
            {
                var stub = new FastTcpSockProtocolStub(pipe.A_LowerSide, options, cancel);
                try
                {
                    await stub.WaitInitSuccessOrFailAsync();
                    pipe.CheckDisconnected();
                    return new FastTcpSock(pipe, stub);
                }
                catch
                {
                    await stub.AsyncCleanuper;
                    throw;
                }
            }
            catch
            {
                await pipe.AsyncCleanuper;
                throw;
            }
        }

        public static async Task<FastTcpSock> FromSocketAsync(Socket socketObject, CancellationToken cancel = default(CancellationToken))
        {
            var options = new FastTcpSockProtocolOptions()
            {
                InitMode = FastTcpSockProtocolInitMode.ByConnectedSocketObject,
                SocketObject = socketObject,
            };

            var pipe = new FastPipe(cancel);
            try
            {
                var stub = new FastTcpSockProtocolStub(pipe.A_LowerSide, options, cancel);
                try
                {
                    await stub.WaitInitSuccessOrFailAsync();
                    pipe.CheckDisconnected();
                    return new FastTcpSock(pipe, stub);
                }
                catch
                {
                    await stub.AsyncCleanuper;
                    throw;
                }
            }
            catch
            {
                await pipe.AsyncCleanuper;
                throw;
            }
        }

        public static async Task<IPAddress> GetIPFromHostName(string host, AddressFamily? addressFamily = null, CancellationToken cancel = default(CancellationToken),
            int timeout = FastTcpProtocolOptionsBase.DefaultTcpConnectTimeout)
        {
            if (IPAddress.TryParse(host, out IPAddress ip))
            {
                if (addressFamily != null && ip.AddressFamily != addressFamily)
                    throw new ArgumentException("ip.AddressFamily != addressFamily");
            }
            else
            {
                ip = await WebSocketHelper.DoAsyncWithTimeout(async c =>
                {
                    return (await Dns.GetHostAddressesAsync(host))
                        .Where(x => x.AddressFamily == AddressFamily.InterNetwork || x.AddressFamily == AddressFamily.InterNetworkV6)
                        .Where(x => addressFamily == null || x.AddressFamily == addressFamily).First();
                },
                timeout: timeout,
                cancel: cancel);
            }

            return ip;
        }
    }

    public abstract class FastLinearMiddleProtocolOptionsBase : FastProtocolOptionsBase
    {
        public int LowerReceiveTimeoutOnInit { get; set; } = 5 * 1000;
        public int LowerSendTimeoutOnInit { get; set; } = 60 * 1000;

        public int LowerReceiveTimeoutAfterInit { get; set; } = Timeout.Infinite;
        public int LowerSendTimeoutAfterInit { get; set; } = Timeout.Infinite;
    }

    public abstract class FastLinearMiddleProtocolStackBase : FastProtocolStackBase
    {
        public FastPipeEnd Lower { get; }
        public FastPipeEnd Upper { get; }

        public FastLinearMiddleProtocolOptionsBase MiddleOptions { get; }

        public abstract Task<AsyncHolder<LayerInfoBase>> ConnectAsync(FastPipeEnd.AttachHandle lowerAttach, FastPipeEnd.AttachHandle upperAttach, FastLinearMiddleProtocolOptionsBase options);

        public FastLinearMiddleProtocolStackBase(FastPipeEnd lower, FastPipeEnd upper, FastLinearMiddleProtocolOptionsBase options, CancellationToken cancel = default(CancellationToken))
            : base(options, cancel)
        {
            MiddleOptions = options;
            Lower = lower;
            Upper = upper;
        }

        protected sealed override async Task MiddleMainAsync()
        {
            try
            {
                Lower.ExceptionQueue.Encounter(Upper.ExceptionQueue);
                Lower.LayerStack.Encounter(Upper.LayerStack);
                Lower.AddOnDisconnected(() => Upper.Disconnect());
                Upper.AddOnDisconnected(() => Lower.Disconnect());

                AsyncCleanuperLady lady = new AsyncCleanuperLady();

                try
                {
                    using (var upperAttach = Upper.Attach(FastPipeEndAttachDirection.FromLowerToA_LowerSide))
                    {
                        lady.Add(upperAttach);

                        using (var lowerAttach = Lower.Attach(FastPipeEndAttachDirection.FromUpperToB_UpperSide))
                        {
                            lady.Add(lowerAttach);

                            lowerAttach.SetStreamTimeout(MiddleOptions.LowerReceiveTimeoutOnInit, MiddleOptions.LowerSendTimeoutOnInit);

                            using (AsyncHolder<LayerInfoBase> holder = await ConnectAsync(lowerAttach, upperAttach, MiddleOptions))
                            {
                                lady.Add(holder);

                                lowerAttach.SetStreamTimeout(MiddleOptions.LowerReceiveTimeoutAfterInit, MiddleOptions.LowerSendTimeoutAfterInit);

                                lowerAttach.SetLayerInfo(holder.UserData, this);

                                CheckPointInitSuccessOrFail();

                                await WebSocketHelper.WaitObjectsAsync(
                                    cancels: CancelWatcher.CancelToken.ToSingleArray(),
                                    manualEvents: new AsyncManualResetEvent[] { Lower.OnDisconnectedEvent, Upper.OnDisconnectedEvent },
                                    exceptions: ExceptionWhen.CancelException
                                    );
                            }
                        }
                    }
                }
                finally
                {
                    await lady;
                }
            }
            catch (Exception ex)
            {
                Lower.Disconnect(ex);
                Upper.Disconnect(ex);

                CheckPointInitSuccessOrFail();

                throw ex;
            }
            finally
            {
                Lower.Disconnect();
                Upper.Disconnect();
            }
        }
    }

    public class FastSslProtocolOptions : FastLinearMiddleProtocolOptionsBase
    {
        public bool IsServerMode { get; set; } = false;
        public SslClientAuthenticationOptions SslClientOptions { get; set; } = new SslClientAuthenticationOptions();
    }

    public class FastSslProtocolStack : FastLinearMiddleProtocolStackBase
    {
        public class LayerInfo : LayerInfoBase, ILayerInfoSsl
        {
            public bool IsServerMode { get; internal set; }
            public string SslProtocol { get; internal set; }
            public string CipherAlgorithm { get; internal set; }
            public int CipherStrength { get; internal set; }
            public string HashAlgorithm { get; internal set; }
            public int HashStrength { get; internal set; }
            public string KeyExchangeAlgorithm { get; internal set; }
            public int KeyExchangeStrength { get; internal set; }
            public X509Certificate LocalCertificate { get; internal set; }
            public X509Certificate RemoteCertificate { get; internal set; }
        }

        public bool IsServerMode { get => SslOptions.IsServerMode; }

        public FastSslProtocolOptions SslOptions { get; }

        public FastSslProtocolStack(FastPipeEnd lower, FastPipeEnd upper, FastSslProtocolOptions options,
            CancellationToken cancel = default(CancellationToken)) : base(lower, upper, options, cancel)
        {
            SslOptions = options;

            BaseStart();
        }

        public override async Task<AsyncHolder<LayerInfoBase>> ConnectAsync(FastPipeEnd.AttachHandle lowerAttach, FastPipeEnd.AttachHandle upperAttach, FastLinearMiddleProtocolOptionsBase options)
        {
            var lowerStream = lowerAttach.GetStream(autoFlush: false);
            try
            {
                var ssl = new SslStream(lowerStream, true);

                try
                {
                    await ssl.AuthenticateAsClientAsync(SslOptions.SslClientOptions, CancelWatcher.CancelToken);

                    LayerInfo info = new LayerInfo()
                    {
                        IsServerMode = this.IsServerMode,
                        SslProtocol = ssl.SslProtocol.ToString(),
                        CipherAlgorithm = ssl.CipherAlgorithm.ToString(),
                        CipherStrength = ssl.CipherStrength,
                        HashAlgorithm = ssl.HashAlgorithm.ToString(),
                        HashStrength = ssl.HashStrength,
                        KeyExchangeAlgorithm = ssl.KeyExchangeAlgorithm.ToString(),
                        KeyExchangeStrength = ssl.KeyExchangeStrength,
                        LocalCertificate = ssl.LocalCertificate,
                        RemoteCertificate = ssl.RemoteCertificate,
                    };

                    var upperStreamWrapper = new FastPipeEndStreamWrapper(upperAttach.PipeEnd, ssl, CancelWatcher.CancelToken);

                    return new AsyncHolder<LayerInfoBase>(async x =>
                    {
                        ssl.DisposeSafe();
                        await upperStreamWrapper.AsyncCleanuper;
                    },
                    userData: info);
                }
                catch
                {
                    ssl.DisposeSafe();
                    throw;
                }
            }
            catch
            {
                lowerStream.DisposeSafe();
                throw;
            }
        }
    }

    public sealed class FastPipeTcpListener : IAsyncCleanupable
    {
        public TcpListenManager ListenerManager { get; }

        public AsyncCleanuper AsyncCleanuper { get; }

        public int? QueueThresholdLengthStream = null;
        public object UserState;

        CancellationTokenSource CancelSource = new CancellationTokenSource();

        public delegate Task FastPipeTcpListenerAcceptCallback(FastPipeTcpListener listener, FastTcpSock sock);

        FastPipeTcpListenerAcceptCallback AcceptProc;

        public FastPipeTcpListener(FastPipeTcpListenerAcceptCallback acceptProc, object userState = null)
        {
            this.UserState = userState;
            this.AcceptProc = acceptProc;

            ListenerManager = new TcpListenManager(ListenManagerAcceptProc);

            this.AsyncCleanuper = new AsyncCleanuper(this);
        }

        async Task ListenManagerAcceptProc(TcpListenManager manager, TcpListenManager.Listener listener, Socket socket)
        {
            try
            {
                using (LeakChecker.Enter())
                {
                    using (FastTcpSock sock = await FastTcpSock.FromSocketAsync(socket, CancelSource.Token))
                    {
                        try
                        {
                            await AcceptProc(this, sock);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("FastPipeTcpListener AcceptProc exception: " + ex.ToString());
                            throw;
                        }
                        finally
                        {
                            await sock.AsyncCleanuper;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("FastPipeTcpListener AcceptProc exception: " + ex.ToString());
                throw;
            }
        }

        Once DisposeFlag;
        public void Dispose()
        {
            if (DisposeFlag.IsFirstCall())
            {
                CancelSource.TryCancel();

                ListenerManager.DisposeSafe();
            }
        }

        public async Task _CleanupAsyncInternal()
        {
            await ListenerManager.AsyncCleanuper;
        }
    }
}


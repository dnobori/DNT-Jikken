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

    ref struct SpanBuffer<T>
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

    ref struct ReadOnlySpanBuffer<T>
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

    ref struct FastMemoryBuffer<T>
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

    ref struct FastReadOnlyMemoryBuffer<T>
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


    class MemoryBuffer<T>
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

    class ReadOnlyMemoryBuffer<T>
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


    static class SpanMemoryBufferHelper
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

    class FastMemoryAllocator<T>
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

    static class FastTick64
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

    static class FastHashHelper
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

    class RefInt : IEquatable<RefInt>, IComparable<RefInt>
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

    class RefLong : IEquatable<RefLong>, IComparable<RefLong>
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

    class RefBool : IEquatable<RefBool>, IComparable<RefBool>
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

    class Ref<T>
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
        public static implicit operator T(Ref<T> r) => r.Value;
        public static implicit operator Ref<T>(T value) => new Ref<T>(value);
    }

    // Buffer
    class Buf
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
    class Fifo
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

    class CachedProperty<T>
    {
        object LockObj = new object();
        bool IsCached = false;
        T CachedValue;

        Func<T> Getter;
        Func<T, T> Setter;
        Func<T, T> Normalizer;

        public CachedProperty(Func<T, T> setter = null, Func<T> getter = null, Func<T, T> normalizer = null)
        {
            Setter = setter;
            Getter = getter;
            Normalizer = normalizer;
        }

        public void Set(T value)
        {
            if (Setter == null) throw new NotImplementedException();
            if (Normalizer != null)
                value = Normalizer(value);

            lock (LockObj)
            {
                value = Setter(value);
                CachedValue = value;
                IsCached = true;
            }
        }

        public T Get()
        {
            lock (LockObj)
            {
                if (IsCached)
                    return CachedValue;

                if (Getter == null) throw new NotImplementedException("The value is undefined yet.");

                T value = Getter();

                if (Normalizer != null)
                    value = Normalizer(value);

                CachedValue = value;
                IsCached = true;

                return value;
            }
        }

        public void Flush()
        {
            lock (LockObj)
            {
                IsCached = false;
                CachedValue = default;
            }
        }

        public T GetFast()
        {
            if (IsCached)
                return CachedValue;

            return Get();
        }

        public T Value { get => Get(); set => Set(value); }
        public T ValueFast { get => GetFast(); }

        public static implicit operator T(CachedProperty<T> r) => r.Value;
    }

    class AsyncLock : IDisposable
    {
        public class LockHolder : IDisposable
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

        public async Task<LockHolder> LockWithAwait()
        {
            await LockAsync();

            return new LockHolder(this);
        }

        public Task LockAsync() => semaphone.WaitAsync();
        public void Unlock() => semaphone.Release();

        public void Dispose() => Dispose(true);
        Once DisposeFlag;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || DisposeFlag.IsFirstCall() == false) return;
            semaphone.DisposeSafe();
            semaphone = null;
        }
    }

    static class Dbg
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
    enum ExceptionWhen
    {
        None = 0,
        TaskException = 1,
        CancelException = 2,
        TimeoutException = 4,
        All = 0x7FFFFFFF,
    }

    static class WebSocketHelper
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

        public static async Task ConnectAsync(this TcpClient tc, string host, int port,
            int timeout = Timeout.Infinite, CancellationToken cancel = default, params CancellationToken[] cancelTokens)
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

        public static async Task<byte[]> ReadAsyncWithTimeout(this Stream stream, int maxSize = 65536, int? timeout = null, bool? readAll = false, CancellationToken cancel = default)
        {
            byte[] tmp = new byte[maxSize];
            int ret = await stream.ReadAsyncWithTimeout(tmp, 0, tmp.Length, timeout,
                readAll: readAll,
                cancel: cancel);
            return CopyByte(tmp, 0, ret);
        }

        public static async Task<int> ReadAsyncWithTimeout(this Stream stream, byte[] buffer, int offset = 0, int? count = null, int? timeout = null, bool? readAll = false, CancellationToken cancel = default, params CancellationToken[] cancelTokens)
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

        public static async Task WriteAsyncWithTimeout(this Stream stream, byte[] buffer, int offset = 0, int? count = null, int? timeout = null, CancellationToken cancel = default, params CancellationToken[] cancelTokens)
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

        public static async Task<TResult> DoAsyncWithTimeout<TResult>(Func<CancellationToken, Task<TResult>> mainProc, Action cancelProc = null, int timeout = Timeout.Infinite, CancellationToken cancel = default, params CancellationToken[] cancelTokens)
        {
            if (timeout < 0) timeout = Timeout.Infinite;
            if (timeout == 0) throw new TimeoutException("timeout == 0");

            if (timeout == Timeout.Infinite && cancel == null && (cancelTokens == null || cancelTokens.Length == 0))
                return await mainProc(default);

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

    class WhenAll : IDisposable
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

        public void Dispose() => Dispose(true);
        Once DisposeFlag;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || DisposeFlag.IsFirstCall() == false) return;
            CancelSource.Cancel();
        }
    }

    struct Once
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

    enum CancelWatcherCallbackEventType
    {
        Canceled,
    }

    class CancelWatcher : AsyncCleanupable
    {
        CancellationTokenSource cts = new CancellationTokenSource();
        public CancellationToken CancelToken { get => cts.Token; }
        public AsyncManualResetEvent EventWaitMe { get; } = new AsyncManualResetEvent();
        public bool Canceled { get; private set; } = false;

        public FastEventListenerList<CancelWatcher, CancelWatcherCallbackEventType> EventList { get; } = new FastEventListenerList<CancelWatcher, CancelWatcherCallbackEventType>();

        Task mainLoop;

        CancellationTokenSource canceller = new CancellationTokenSource();

        AsyncAutoResetEvent ev = new AsyncAutoResetEvent();
        volatile bool halt = false;

        HashSet<CancellationToken> targetList = new HashSet<CancellationToken>();
        List<Task> taskList = new List<Task>();

        object LockObj = new object();

        public CancelWatcher(params CancellationToken[] cancels) : this(null, cancels) { }

        public CancelWatcher(AsyncCleanuperLady parentLady, params CancellationToken[] cancels)
            : base(new AsyncCleanuperLady())
        {
            AddWatch(canceller.Token);
            AddWatch(cancels);

            if (parentLady != null)
                parentLady.Add(this);

            this.mainLoop = CancelWatcherMainLoop().AddToLady(this);
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
                        EventList.Fire(this, CancelWatcherCallbackEventType.Canceled);
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
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!disposing || DisposeFlag.IsFirstCall() == false) return;
                this.halt = true;
                this.Canceled = true;
                this.ev.Set();
                this.cts.TryCancelAsync().LaissezFaire();
                this.EventWaitMe.Set(true);
            }
            finally { base.Dispose(disposing); }
        }
    }

    class AsyncAutoResetEvent
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

    class AsyncCallbackList
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

    class AsyncManualResetEvent
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

    static class Time
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

    class MicroBenchmarkQueue
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

    static class Limbo
    {
        public static long SInt = 0;
        public static ulong UInt = 0;
        public volatile static object ObjectSlow = null;
    }

    class MicroBenchmarkGlobalParam
    {
        public static int DefaultDurationMSecs = 250;
    }

    interface IMicroBenchmark
    {
        double Start(int duration = 0);
        double StartAndPrint(int duration = 0);
    }

    class MicroBenchmark<TUserVariable> : IMicroBenchmark
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

    static class LeakChecker
    {
        internal static Dictionary<long, LeakCheckerHolder> _InternalList = new Dictionary<long, LeakCheckerHolder>();
        internal static long _InternalCurrentId = 0;

        static public AsyncCleanuperLady SuperGrandLady { get; } = new AsyncCleanuperLady();

        public static LeakCheckerHolder Enter([CallerFilePath] string filename = "", [CallerLineNumber] int line = 0, [CallerMemberName] string caller = null)
            => new LeakCheckerHolder($"{caller}() - {Path.GetFileName(filename)}:{line}", Environment.StackTrace);

        public static int Count
        {
            get
            {
                lock (_InternalList)
                    return _InternalList.Count;
            }
        }

        public static void Print()
        {
            SuperGrandLady.CleanupAsync().TryWait();

            lock (_InternalList)
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
            lock (_InternalList)
            {
                foreach (var v in _InternalList.OrderBy(x => x.Key))
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
    }

    class LeakCheckerHolder : IDisposable
    {
        long Id;
        public string Name { get; }
        public string StackTrace { get; }

        internal LeakCheckerHolder(string name, string stackTrace)
        {
            stackTrace = "";

            if (string.IsNullOrEmpty(name)) name = "<untitled>";

            Id = Interlocked.Increment(ref LeakChecker._InternalCurrentId);
            Name = name;
            StackTrace = string.IsNullOrEmpty(stackTrace) ? "" : stackTrace;

            lock (LeakChecker._InternalList)
                LeakChecker._InternalList.Add(Id, this);
        }

        public void Dispose() => Dispose(true);
        Once DisposeFlag;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || DisposeFlag.IsFirstCall() == false) return;
            lock (LeakChecker._InternalList)
            {
                Debug.Assert(LeakChecker._InternalList.ContainsKey(Id));
                LeakChecker._InternalList.Remove(Id);
            }
        }
    }

    class CriticalSection { }

}


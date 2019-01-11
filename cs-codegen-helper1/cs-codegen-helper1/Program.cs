using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using static System.Console;

namespace cs_codegen_helper1
{
    static class TestDummy
    {
        public static unsafe byte ToUInt8(this byte[] data, int offset = 0)
        {
            return (byte)data[offset];
        }

        public static unsafe uint ToUInt32(this byte[] data, int offset = 0)
        {
            if (checked(offset + 4) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((uint*)ptr)) : *((uint*)ptr);
        }

        public static unsafe byte ToUInt8(this Span<byte> span)
        {
            return (byte)span[0];
        }

        public static unsafe uint ToUInt32(this Span<byte> span)
        {
            if (span.Length < 4) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((uint*)ptr)) : *((uint*)ptr);
        }

        public static unsafe byte ToUInt8(this ReadOnlySpan<byte> span)
        {
            return (byte)span[0];
        }

        public static unsafe uint ToUInt32(this ReadOnlySpan<byte> span)
        {
            if (span.Length < 4) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((uint*)ptr)) : *((uint*)ptr);
        }

        public static unsafe byte ToUInt8(this Memory<byte> memory)
        {
            return (byte)memory.Span[0];
        }

        public static unsafe uint ToUInt32(this Memory<byte> memory)
        {
            if (memory.Length < 4) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((uint*)ptr)) : *((uint*)ptr);
        }

        public static unsafe byte ToUInt8(this ReadOnlyMemory<byte> memory)
        {
            return (byte)memory.Span[0];
        }

        public static unsafe uint ToUInt32(this ReadOnlyMemory<byte> memory)
        {
            if (memory.Length < 4) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*((uint*)ptr)) : *((uint*)ptr);
        }


        public static unsafe void ToBinaryUInt8(this byte value, byte[] data, int offset = 0)
        {
            if (checked(offset + 1) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((byte*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void ToBinaryUInt32(this uint value, byte[] data, int offset = 0)
        {
            if (checked(offset + 4) > data.Length) throw new ArgumentOutOfRangeException("data.Length is too small");
            fixed (byte* ptr = data)
                *((uint*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void ToBinaryUInt8(this byte value, Span<byte> span)
        {
            if (span.Length < 1) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((byte*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void ToBinaryUInt32(this uint value, Span<byte> span)
        {
            if (span.Length < 4) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((uint*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void ToBinaryUInt8(this byte value, ReadOnlySpan<byte> span)
        {
            if (span.Length < 1) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((byte*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void ToBinaryUInt32(this uint value, ReadOnlySpan<byte> span)
        {
            if (span.Length < 4) throw new ArgumentOutOfRangeException("span.Length is too small");
            fixed (byte* ptr = span)
                *((uint*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void ToBinaryUInt8(this byte value, Memory<byte> memory)
        {
            if (memory.Length < 1) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((byte*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void ToBinaryUInt32(this uint value, Memory<byte> memory)
        {
            if (memory.Length < 4) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((uint*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void ToBinaryUInt8(this byte value, ReadOnlyMemory<byte> memory)
        {
            if (memory.Length < 1) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((byte*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }

        public static unsafe void ToBinaryUInt32(this uint value, ReadOnlyMemory<byte> memory)
        {
            if (memory.Length < 4) throw new ArgumentOutOfRangeException("memory.Length is too small");
            fixed (byte* ptr = memory.Span)
                *((uint*)ptr) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }
    }

    class IntegerType
    {
        public string TypeName;
        public string FriendlyName;
        public int DataSize;
    }

    class ByteType
    {
        public string TypeName;
        public string ValueName;
        public string AdditionalArguments = "";
        public string AdditionalAccess = "";
    }

    class Program
    {
        static void Main(string[] args)
        {
            IntegerType[] integer_types = new IntegerType[]
                {
                    new IntegerType{ TypeName = "bool", FriendlyName = "Bool8", DataSize = 1 },
                    new IntegerType{ TypeName = "byte", FriendlyName = "UInt8", DataSize = 1 },
                    new IntegerType{ TypeName = "sbyte", FriendlyName = "SInt8", DataSize = 1 },
                    new IntegerType{ TypeName = "ushort", FriendlyName = "UInt16", DataSize = 2 },
                    new IntegerType{ TypeName = "short", FriendlyName = "SInt16", DataSize = 2 },
                    new IntegerType{ TypeName = "uint", FriendlyName = "UInt32", DataSize = 4 },
                    new IntegerType{ TypeName = "int", FriendlyName = "SInt32", DataSize = 4 },
                    new IntegerType{ TypeName = "ulong", FriendlyName = "UInt64", DataSize = 8 },
                    new IntegerType{ TypeName = "long", FriendlyName = "SInt64", DataSize = 8 },
                };

            ByteType[] byte_types = new ByteType[]
                {
                    new ByteType{ TypeName = "byte[]", ValueName = "data", AdditionalArguments = ", int offset = 0" },
                    new ByteType{ TypeName = "Span<byte>", ValueName = "span" },
                    new ByteType{ TypeName = "ReadOnlySpan<byte>", ValueName = "span" },
                    new ByteType{ TypeName = "Memory<byte>", ValueName = "memory", AdditionalAccess = ".Span" },
                    new ByteType{ TypeName = "ReadOnlyMemory<byte>", ValueName = "memory", AdditionalAccess = ".Span" },
                };

            StringWriter toint = new StringWriter();
            StringWriter tobin1 = new StringWriter();
            StringWriter tobin2 = new StringWriter();

            foreach (var t2 in byte_types)
            {
                foreach (var t1 in integer_types)
                {
                    toint.WriteLine($"public static unsafe {t1.TypeName} Get{t1.FriendlyName}(this {t2.TypeName} {t2.ValueName}{t2.AdditionalArguments})");
                    toint.WriteLine("{");
                    if (t1.DataSize == 1)
                    {
                        if (t1.TypeName != "bool")
                            toint.WriteLine($"    return ({t1.TypeName}){t2.ValueName}{t2.AdditionalAccess}[{(string.IsNullOrEmpty(t2.AdditionalArguments) ? "0" : "offset")}];");
                        else
                            toint.WriteLine($"    return ({t2.ValueName}{t2.AdditionalAccess}[{(string.IsNullOrEmpty(t2.AdditionalArguments) ? "0" : "offset")}] == 0) ? false : true;");
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(t2.AdditionalArguments))
                        {
                            toint.WriteLine($"    if ({t2.ValueName}.Length < sizeof({t1.TypeName})) throw new ArgumentOutOfRangeException(\"{t2.ValueName}.Length is too small\");");
                        }
                        else
                        {
                            toint.WriteLine($"    if (offset < 0) throw new ArgumentOutOfRangeException(\"offset < 0\");");
                            toint.WriteLine($"    if (checked(offset + sizeof({t1.TypeName})) > {t2.ValueName}.Length) throw new ArgumentOutOfRangeException(\"{t2.ValueName}.Length is too small\");");
                        }
                        toint.WriteLine($"    fixed (byte* ptr = {t2.ValueName}{t2.AdditionalAccess})");
                        toint.WriteLine($"        return BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(*(({t1.TypeName}*)(ptr{(string.IsNullOrEmpty(t2.AdditionalArguments) ? "" : " + offset")}))) : *(({t1.TypeName}*)(ptr{(string.IsNullOrEmpty(t2.AdditionalArguments) ? "" : " + offset")}));");
                    }
                    toint.WriteLine("}");
                    toint.WriteLine();

                    if (t2.TypeName.IndexOf("readonly", StringComparison.CurrentCultureIgnoreCase) == -1)
                    {
                        tobin1.WriteLine($"public static unsafe void Set{t1.FriendlyName}(this {t1.TypeName} value, {t2.TypeName} {t2.ValueName}{t2.AdditionalArguments})");
                        tobin1.WriteLine("{");
                        if (t1.DataSize == 1)
                        {
                            if (t1.TypeName != "bool")
                                tobin1.WriteLine($"    {t2.ValueName}{t2.AdditionalAccess}[{(string.IsNullOrEmpty(t2.AdditionalArguments) ? "0" : "offset")}] = (byte)value;");
                            else
                                tobin1.WriteLine($"    {t2.ValueName}{t2.AdditionalAccess}[{(string.IsNullOrEmpty(t2.AdditionalArguments) ? "0" : "offset")}] = (byte)(value ? 1 : 0);");
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(t2.AdditionalArguments))
                            {
                                tobin1.WriteLine($"    if ({t2.ValueName}.Length < sizeof({t1.TypeName})) throw new ArgumentOutOfRangeException(\"{t2.ValueName}.Length is too small\");");
                            }
                            else
                            {
                                tobin1.WriteLine($"    if (offset < 0) throw new ArgumentOutOfRangeException(\"offset < 0\");");
                                tobin1.WriteLine($"    if (checked(offset + sizeof({t1.TypeName})) > {t2.ValueName}.Length) throw new ArgumentOutOfRangeException(\"{t2.ValueName}.Length is too small\");");
                            }
                            tobin1.WriteLine($"    fixed (byte* ptr = {t2.ValueName}{t2.AdditionalAccess})");
                            tobin1.WriteLine($"        *(({t1.TypeName}*)(ptr{(string.IsNullOrEmpty(t2.AdditionalArguments) ? "" : " + offset")})) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;");
                        }
                        tobin1.WriteLine("}");
                        tobin1.WriteLine();


                        tobin1.WriteLine($"public static unsafe void Set{t1.FriendlyName}(this {t2.TypeName} {t2.ValueName}, {t1.TypeName} value{t2.AdditionalArguments})");
                        tobin1.WriteLine("{");
                        if (t1.DataSize == 1)
                        {
                            if (t1.TypeName != "bool")
                                tobin1.WriteLine($"    {t2.ValueName}{t2.AdditionalAccess}[{(string.IsNullOrEmpty(t2.AdditionalArguments) ? "0" : "offset")}] = (byte)value;");
                            else
                                tobin1.WriteLine($"    {t2.ValueName}{t2.AdditionalAccess}[{(string.IsNullOrEmpty(t2.AdditionalArguments) ? "0" : "offset")}] = (byte)(value ? 1 : 0);");
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(t2.AdditionalArguments))
                            {
                                tobin1.WriteLine($"    if ({t2.ValueName}.Length < sizeof({t1.TypeName})) throw new ArgumentOutOfRangeException(\"{t2.ValueName}.Length is too small\");");
                            }
                            else
                            {
                                tobin1.WriteLine($"    if (offset < 0) throw new ArgumentOutOfRangeException(\"offset < 0\");");
                                tobin1.WriteLine($"    if (checked(offset + sizeof({t1.TypeName})) > {t2.ValueName}.Length) throw new ArgumentOutOfRangeException(\"{t2.ValueName}.Length is too small\");");
                            }
                            tobin1.WriteLine($"    fixed (byte* ptr = {t2.ValueName}{t2.AdditionalAccess})");
                            tobin1.WriteLine($"        *(({t1.TypeName}*)(ptr{(string.IsNullOrEmpty(t2.AdditionalArguments) ? "" : " + offset")})) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;");
                        }
                        tobin1.WriteLine("}");
                        tobin1.WriteLine();
                    }
                }
            }

            foreach (var t1 in integer_types)
            {
                tobin2.WriteLine($"public static unsafe byte[] Get{t1.FriendlyName}(this {t1.TypeName} value)");
                tobin2.WriteLine("{");
                tobin2.WriteLine($"    byte[] data = new byte[{t1.DataSize}];");

                var t2 = byte_types.Where(x => x.TypeName == "byte[]").First();

                if (t1.DataSize == 1)
                {
                    if (t1.TypeName != "bool")
                        tobin2.WriteLine($"    {t2.ValueName}{t2.AdditionalAccess}[0] = (byte)value;");
                    else
                        tobin2.WriteLine($"    {t2.ValueName}{t2.AdditionalAccess}[0] = (byte)(value ? 1 : 0);");
                }
                else
                {
                    tobin2.WriteLine($"    fixed (byte* ptr = {t2.ValueName}{t2.AdditionalAccess})");
                    tobin2.WriteLine($"        *(({t1.TypeName}*)(ptr)) = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;");
                }
                tobin2.WriteLine("    return data;");
                tobin2.WriteLine("}");
                tobin2.WriteLine();

            }

            WriteLine("#region AutoGenerated");
            WriteLine();
            WriteLine(toint.ToString());
            WriteLine(tobin1.ToString());
            WriteLine(tobin2.ToString());
            WriteLine();
            WriteLine("#endregion");

            WriteLine("#region AutoGenerated");
            WriteLine();
            for (int i = 1; i < 27; i++)
            {
                string aa = "";
                string bb = "";
                for (int j = 0; j < i; j++)
                {
                    if (j != 0)
                    {
                        aa += ", ";
                        bb += " ^ ";
                    }
                    aa += "int " + (char)('a' + j);
                    bb += (char)('a' + j);
                }
                Console.WriteLine($"[MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Xor({aa}) => ({bb});");
            }
            WriteLine();
            WriteLine("#endregion");
        }
    }
}

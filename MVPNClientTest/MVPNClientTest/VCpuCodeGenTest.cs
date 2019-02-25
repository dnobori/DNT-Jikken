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
    [Flags]
    public enum CodeGenTargetEnum
    {
        CSharp,
        C,
    }

    [Flags]
    public enum FastCheckTypeEnum
    {
        NoCheck,
        Fast1,
        Fast2,
    }

    [Flags]
    public enum AsmExceptionType
    {
        InvalidJumpTarget = 1,
    }

    public static class VConsts
    {
        public const uint PageSize = 4096;
        public const uint NumPages = (uint)(0x100000000 / PageSize);
        public const uint Magic_Return = 0xdeadbeef;

        public const bool Test_InterpreterMode = false;
        public const bool Test_AllLabel = false;
        public const bool Test_DualCode = false;
        public const bool Test_Interrupt = false;

        public const AddressingMode Addressing = AddressingMode.Contiguous;
        public const FastCheckTypeEnum FastCheckType = FastCheckTypeEnum.Fast1;

        public static CodeGenTargetEnum CodeGenTarget = CodeGenTargetEnum.CSharp;

        public static readonly string[] RegisterList = {
            "eax", "ebx", "ecx", "edx", "esi", "edi", "esp", "ebp", };
    }

    public unsafe struct VPageTableEntry
    {
        public byte* RealMemory;
        public bool CanRead;
        public bool CanWrite;
    }

    public unsafe class VMemory : IDisposable
    {
        public VPageTableEntry* PageTableEntry;

        public VMemory()
        {
            PageTableEntry = (VPageTableEntry*)Marshal.AllocCoTaskMem((int)(sizeof(VPageTableEntry) * VConsts.NumPages));
            for (int i = 0; i < VConsts.NumPages; i++)
            {
                PageTableEntry[i].RealMemory = null;
                PageTableEntry[i].CanRead = PageTableEntry[i].CanWrite = false;
            }
        }

        public void Write(uint address, uint value)
        {
            if (VConsts.Addressing == AddressingMode.Paging)
            {
                VPageTableEntry* pte = PageTableEntry;
                uint vaddr = address;
                uint vaddr1_index = vaddr / VConsts.PageSize;
                uint vaddr1_offset = vaddr - vaddr1_index * VConsts.PageSize;
                if (pte[vaddr1_index].CanWrite == false)
                {
                    throw new ApplicationException($"Access violation to 0x{vaddr:x}.");
                }
                byte* realaddr1 = (byte*)(pte[vaddr1_index].RealMemory + vaddr1_offset);
                if ((vaddr1_offset + 4) > VConsts.PageSize)
                {
                    uint size1 = VConsts.PageSize - vaddr1_offset;
                    uint size2 = 4 - size1;
                    uint vaddr2 = vaddr + size1;
                    uint vaddr2_index = vaddr2 / VConsts.PageSize;
                    if (pte[vaddr2_index].CanWrite == false)
                    {
                        throw new ApplicationException($"Access violation to 0x{vaddr2:x}.");
                    }
                    byte* realaddr2 = (byte*)(pte[vaddr2_index].RealMemory);
                    uint set_value = value;
                    byte* set_ptr = (byte*)set_value;
                    if (size1 == 1) { realaddr1[0] = set_ptr[0]; realaddr2[0] = set_ptr[1]; realaddr2[1] = set_ptr[2]; realaddr2[2] = set_ptr[3]; }
                    else if (size1 == 2) { realaddr1[0] = set_ptr[0]; realaddr1[1] = set_ptr[1]; realaddr2[0] = set_ptr[2]; realaddr2[1] = set_ptr[3]; }
                    else if (size1 == 3) { realaddr1[0] = set_ptr[0]; realaddr1[1] = set_ptr[1]; realaddr1[2] = set_ptr[2]; realaddr2[0] = set_ptr[3]; }
                }
                else
                {
                    *((uint*)realaddr1) = value;
                }
            }
            else
            {
                if (address < this.ContiguousStart || address >= this.ContiguousEnd)
                {
                    throw new ApplicationException($"Access violation to 0x{address:x}.");
                }
                *((uint*)(byte*)(this.ContiguousMemory + address - this.ContiguousStart)) = value;
            }
        }

        public uint Read(uint address)
        {
            if (VConsts.Addressing == AddressingMode.Paging)
            {
                VPageTableEntry* pte = PageTableEntry;
                uint vaddr = address;
                uint vaddr1_index = vaddr / VConsts.PageSize;
                uint vaddr1_offset = vaddr - vaddr1_index * VConsts.PageSize;
                if (pte[vaddr1_index].CanRead == false)
                {
                    throw new ApplicationException($"Access violation to 0x{vaddr:x}.");
                }
                byte* realaddr1 = (byte*)(pte[vaddr1_index].RealMemory + vaddr1_offset);
                if ((vaddr1_offset + 4) > VConsts.PageSize)
                {
                    uint size1 = VConsts.PageSize - vaddr1_offset;
                    uint size2 = 4 - size1;
                    uint vaddr2 = vaddr + size1;
                    uint vaddr2_index = vaddr2 / VConsts.PageSize;
                    if (pte[vaddr2_index].CanRead == false)
                    {
                        throw new ApplicationException($"Access violation to 0x{vaddr2:x}.");
                    }
                    byte* realaddr2 = (byte*)(pte[vaddr2_index].RealMemory);
                    uint get_value = 0;
                    byte* get_ptr = (byte*)get_value;
                    if (size1 == 1) { get_ptr[0] = realaddr1[0]; get_ptr[1] = realaddr2[0]; get_ptr[2] = realaddr2[1]; get_ptr[3] = realaddr2[2]; }
                    else if (size1 == 2) { get_ptr[0] = realaddr1[0]; get_ptr[1] = realaddr1[1]; get_ptr[2] = realaddr2[0]; get_ptr[3] = realaddr2[1]; }
                    else if (size1 == 3) { get_ptr[0] = realaddr1[0]; get_ptr[1] = realaddr1[1]; get_ptr[2] = realaddr1[2]; get_ptr[3] = realaddr2[0]; }
                    return get_value;
                }
                else
                {
                    return *((uint*)realaddr1);
                }
            }
            else
            {
                if (address < this.ContiguousStart || address >= this.ContiguousEnd)
                {
                    throw new ApplicationException($"Access violation to 0x{address:x}.");
                }
                return *((uint*)(byte*)(this.ContiguousMemory + address - this.ContiguousStart));
            }
        }

        public void AllocateMemory(uint startAddress, uint size, bool canRead = true, bool canWrite = true)
        {
            if (VConsts.Addressing != AddressingMode.Paging) throw new ApplicationException("Not paging.");

            AllignMemoryToPage(startAddress, size, out uint pageNumberStart, out uint pageCount);
            for (uint i = pageNumberStart; i < pageNumberStart + pageCount; i++)
            {
                VPageTableEntry* e = &PageTableEntry[i];
                if (e->RealMemory != null)
                    throw new ArgumentException($"Page number {pageNumberStart}, count {pageCount} already exists.");
            }
            for (uint i = pageNumberStart; i < pageNumberStart + pageCount; i++)
            {
                VPageTableEntry* e = &PageTableEntry[i];
                e->RealMemory = (byte*)Marshal.AllocCoTaskMem((int)VConsts.PageSize);
                e->CanRead = canRead;
                e->CanWrite = canWrite;
            }
        }

        public byte* ContiguousMemory = null;
        public uint ContiguousStart = 0;
        public uint ContiguousEnd = 0;

        public void AllocateContiguousMemory(uint startAddress, uint size)
        {
            if (ContiguousMemory != null) throw new ApplicationException("Already allocated.");

            ContiguousMemory = (byte*)Marshal.AllocCoTaskMem((int)size);
            ContiguousStart = startAddress;
            ContiguousEnd = size;
        }

        void AllignMemoryToPage(uint startAddress, uint size, out uint pageNumberStart, out uint pageCount)
        {
            pageNumberStart = startAddress / VConsts.PageSize;

            pageCount = (startAddress + size) / VConsts.PageSize - pageNumberStart;
        }

        public uint SizeToPageCount(uint size)
        {
            return ((size + VConsts.PageSize - 1) / VConsts.PageSize);
        }

        public void Dispose() => Dispose(true);
        Once DisposeFlag;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || DisposeFlag.IsFirstCall() == false) return;
            for (int i = 0; i < VConsts.NumPages; i++)
            {
                VPageTableEntry* e = &PageTableEntry[i];
                if (e->RealMemory != null)
                    Marshal.FreeCoTaskMem((IntPtr)e->RealMemory);
            }
            Marshal.FreeCoTaskMem((IntPtr)PageTableEntry);
        }
    }

    public class VCpuState
    {
        public uint Eax, Ebx, Ecx, Edx, Esi, Edi, Ebp, Esp;
        public string ExceptionString;
        public uint ExceptionAddress;
        public volatile bool Interrupt;

        public VProcess Process;
        public VMemory Memory;

        public VCpuState(VProcess process)
        {
            this.Process = process;
            this.Memory = this.Process.Memory;
        }
    }

    public enum AddressingMode
    {
        Paging,
        Contiguous,
    }

    public class VProcess : IDisposable
    {
        public VMemory Memory { get; } = null;

        public VProcess()
        {
            this.Memory = new VMemory();
        }

        public void Dispose() => Dispose(true);
        Once DisposeFlag;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || DisposeFlag.IsFirstCall() == false) return;
            this.Memory.Dispose();
        }
    }

    class AsmCode
    {
        public string PreLines => WriterPreLines.ToString();
        public string PostLines => WriterPostLines.ToString();
        public string RealRegister;
        public bool UseTmp;

        public string RealRegisterFixed
        {
            get
            {
                if (RealRegister.StartsWith("%") == false && RealRegister.StartsWith("(") == false)
                    return "%" + RealRegister;
                return RealRegister;
            }
        }

        public StringWriter WriterPreLines = new StringWriter();
        public StringWriter WriterPostLines = new StringWriter();
    }

    class VCodeOperand
    {
        public bool IsPointer = false;
        public bool IsLabel = false;
        public uint Displacement = 0;
        public bool IsDisplacementNegative = false;
        public string BaseRegister = null;
        public string IndexRegister = null;
        public uint Scaler = 0;

        public VCodeOperand(string src)
        {
            int a = src.IndexOf('(');
            int b = src.IndexOf(')', a + 1);

            string inner;
            string before = null;
            if (a != -1 && b != -1)
            {
                before = src.Substring(0, a);
                inner = src.Substring(a + 1, b - a - 1);

                IsPointer = true;

                string[] tokens = inner.Split(',');
                if (tokens.Length >= 1)
                {
                    BaseRegister = GetRegisterName(tokens[0]);
                    if (tokens.Length >= 2)
                    {
                        IndexRegister = GetRegisterName(tokens[1]);
                        if (tokens.Length >= 3)
                        {
                            Scaler = Convert.ToUInt32(tokens[2], 16);
                            if (tokens.Length >= 4)
                            {
                                throw new ArgumentException($"Invalid operand: '{src}'");
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(before) == false)
                {
                    if (before.StartsWith("-"))
                    {
                        before = before.Substring(1);
                        IsDisplacementNegative = true;
                    }
                    Displacement = Convert.ToUInt32(before, 16);
                }
            }
            else
            {
                if (src.StartsWith("%"))
                {
                    BaseRegister = GetRegisterName(src);
                }
                else if (src.StartsWith("$"))
                {
                    Displacement = Convert.ToUInt32(src.Substring(1), 16);
                }
                else if (src.StartsWith("0x"))
                {
                    Displacement = Convert.ToUInt32(src, 16);
                    IsPointer = true;
                }
                else
                {
                    string[] tokens = src.Split(' ');
                    if (tokens.Length >= 1)
                    {
                        Displacement = Convert.ToUInt32(tokens[0], 16);
                        IsLabel = true;
                    }
                }
            }
        }

        public static string GetRegisterName(string src)
        {
            if (src.StartsWith("%") == false) throw new ArgumentException($"'{src}' is not a register name.");
            return src.Substring(1);
        }

        public string GetAsmRegisterOrImm()
        {
            if (this.IsLabel) throw new ApplicationException("This operand is a label.");
            if (this.IsPointer) throw new ApplicationException("This operand is a pointer.");

            if (this.BaseRegister != null && this.Displacement != 0)
            {
                throw new ApplicationException("this.BaseRegister != null && this.Displacement != 0");
            }

            if (this.BaseRegister != null)
            {
                return "%" + this.BaseRegister;
            }
            else
            {
                return "$" + (this.IsDisplacementNegative ? "-" : "") + "0x" + this.Displacement.ToString("x");
            }
        }

        public string GetCode()
        {
            if (this.IsLabel) throw new ApplicationException("This operand is a label.");
            string str = "";
            if (this.BaseRegister != null)
                str = this.BaseRegister;

            if (VConsts.CodeGenTarget == CodeGenTargetEnum.C)
            {
                switch (str)
                {
                    case "al":
                    case "bl":
                    case "cl":
                    case "dl":
                    case "ah":
                    case "bh":
                    case "ch":
                    case "dh":
                        str = "*" + str;
                        break;
                }
            }

            if (this.IndexRegister != null && this.Scaler != 0)
                str = "(" + str + " + " + this.IndexRegister + " * " + "0x" + this.Scaler.ToString("x") + ")";
            if (this.Displacement != 0)
                str = "(" + str + (this.IsDisplacementNegative ? " -" : " +") + "0x" + this.Displacement.ToString("x") + ")";
            if (str == "") str = "0x" + this.Displacement.ToString("x");
            return str;
        }

        public string GetValueAccessCode()
        {
            if (this.IsPointer) throw new ApplicationException("This operand is a pointer.");
            return GetCode();
        }

        public AsmCode AsmGenerateMemoryWriteCode(uint codeAddress, bool writeMode, string tmpRegister = "r12")
        {
            AsmCode ret = new AsmCode();
            ret.UseTmp = true;

            // calc virtual address
            AsmVirtualRegisterReader baseRegisterReader = new AsmVirtualRegisterReader(this.BaseRegister, "error");
            AsmVirtualRegisterReader indexRegisterReader = this.IndexRegister != null ? new AsmVirtualRegisterReader(this.IndexRegister, "error") : null;
            if (indexRegisterReader != null)
            {
                ret.WriterPreLines.WriteLine($"lea 0x{this.Displacement:X}(%{baseRegisterReader.RealRegister}, %{indexRegisterReader.RealRegister}, {this.Scaler}), %{tmpRegister}d");
            }
            else
            {
                if (this.Displacement == 0)
                {
                    ret.WriterPreLines.WriteLine($"mov %{baseRegisterReader.RealRegister}, %{tmpRegister}d");
                }
                else
                {
                    ret.WriterPreLines.WriteLine($"lea 0x{this.Displacement:X}(%{baseRegisterReader.RealRegister}), %{tmpRegister}d");
                }
            }

            // calc real address
            ret.WriterPreLines.WriteLine($"lea (%{tmpRegister}, %r15, 1), %{tmpRegister}");

            if (writeMode == false)
            {
                // read memory
                ret.RealRegister = $"(%{tmpRegister})";
            }
            else
            {
                // write memory
                ret.RealRegister = $"(%{tmpRegister})";
            }

            StringWriter ww = new StringWriter();
            //AsmVirtualRegisterWriter.WriteSaveRFlags(ww);
            //AsmVirtualRegisterWriter.WriteRestoreRFlags(ww);
            ret.WriterPostLines.WriteLine(ww.ToString());

            return ret;
        }

        public string GenerateMemoryAccessCode(uint codeAddress, bool writeMode, string srcOrDestinationCode, string readModeRetType = "uint")
        {
            StringWriter w = new StringWriter();

            if (VConsts.CodeGenTarget == CodeGenTargetEnum.C)
                readModeRetType = "uint";

            if (VConsts.Addressing == AddressingMode.Paging)
            {
                string memcache_tag = null;

                if (this.IsPointer && this.BaseRegister != null && this.Scaler == 0 && this.IndexRegister == null)
                {
                    memcache_tag = $"memcache_{this.BaseRegister}_0x{this.Displacement:x}";
                }

                if (memcache_tag != null)
                {
                    if (writeMode)
                    {
                    }
                    else
                    {
                        w.WriteLine($"if ({memcache_tag}_pin == {GetCode()}) {srcOrDestinationCode}= ({readModeRetType}) {memcache_tag}_data; else ");
                    }
                }


                w.WriteLine("{");

                w.WriteLine($"vaddr = {GetCode()};");
                w.WriteLine($"vaddr1_index = vaddr / {VConsts.PageSize};");
                w.WriteLine($"vaddr1_offset = vaddr % {VConsts.PageSize};");
                //w.WriteLine($"uint vaddr1_offset = vaddr % VConsts.PageSize;");
                w.WriteLine($"if (vaddr1_index == cache_last_page1)");
                w.WriteLine("{");
                if (writeMode)
                {
                    if (memcache_tag != null)
                    {
                        w.Write($"    {memcache_tag}_data = ");
                    }
                    w.WriteLine($"    *((uint *)(((byte *)cache_last_realaddr1) + vaddr1_offset)) = {srcOrDestinationCode};");
                    if (memcache_tag != null)
                    {
                        w.WriteLine($"    {memcache_tag}_pin = {GetCode()};");
                    }
                }
                else
                {
                    w.Write($"    {srcOrDestinationCode}= ({readModeRetType})( ");
                    if (memcache_tag != null)
                    {
                        w.Write($"    {memcache_tag}_data = ");
                    }
                    w.WriteLine(" *((uint *)(((byte *)cache_last_realaddr1) + vaddr1_offset)) );");
                    if (memcache_tag != null)
                    {
                        w.WriteLine($"    {memcache_tag}_pin = {GetCode()};");
                    }
                }
                w.WriteLine("}");
                w.WriteLine("else if (vaddr1_index == cache_last_page2)");
                w.WriteLine("{");
                if (writeMode)
                {
                    if (memcache_tag != null)
                    {
                        w.Write($"    {memcache_tag}_data = ");
                    }
                    w.WriteLine($"    *((uint *)(((byte *)cache_last_realaddr2) + vaddr1_offset)) = {srcOrDestinationCode};");
                    if (memcache_tag != null)
                    {
                        w.WriteLine($"    {memcache_tag}_pin = {GetCode()};");
                    }
                }
                else
                {
                    w.Write($"    {srcOrDestinationCode}= ({readModeRetType})(");
                    if (memcache_tag != null)
                    {
                        w.Write($"    {memcache_tag}_data = ");
                    }
                    w.WriteLine(" *((uint *)(((byte *)cache_last_realaddr2) + vaddr1_offset)) );");
                    if (memcache_tag != null)
                    {
                        w.WriteLine($"    {memcache_tag}_pin = {GetCode()};");
                    }
                }
                w.WriteLine("}");
                w.WriteLine("else");
                w.WriteLine("{");
                //w.WriteLine($"if (pte[vaddr1_index].{(writeMode ? "CanWrite" : "CanRead")} == false)");
                w.WriteLine($"if (pte[vaddr1_index].{(writeMode ? "CanWrite" : "CanRead")} == false)");
                w.WriteLine("{");

                if (VConsts.CodeGenTarget == CodeGenTargetEnum.CSharp)
                    w.WriteLine("    exception_string = $\"Access violation to 0x{vaddr:x}.\";");
                else
                    w.WriteLine("    sprintf(exception_string, \"Access violation to 0x%x.\", vaddr);");

                w.WriteLine($"    exception_address = 0x{codeAddress:x};");
                w.WriteLine("    goto L_RETURN;");
                w.WriteLine("}");

                w.WriteLine("if (((last_used_cache++) % 2) == 0)");
                w.WriteLine("{");
                w.WriteLine("    cache_last_page1 = vaddr1_index;");
                w.WriteLine("    cache_last_realaddr1 = pte[vaddr1_index].RealMemory;");
                w.WriteLine("} else {");
                w.WriteLine("    cache_last_page2 = vaddr1_index;");
                w.WriteLine("    cache_last_realaddr2 = pte[vaddr1_index].RealMemory;");
                w.WriteLine("}");
                //w.WriteLine($"realaddr1 = (byte *)(pte[vaddr1_index].RealMemory + vaddr1_offset);");


                // single page
                if (writeMode)
                {
                    if (memcache_tag != null)
                    {
                        w.Write($"    {memcache_tag}_data = ");
                    }
                    w.WriteLine($"    *((uint *)((byte *)(pte[vaddr1_index].RealMemory + vaddr1_offset))) = {srcOrDestinationCode};");
                    if (memcache_tag != null)
                    {
                        w.WriteLine($"    {memcache_tag}_pin = {GetCode()};");
                    }
                }
                else
                {
                    w.Write($"    {srcOrDestinationCode}= ({readModeRetType})(");
                    if (memcache_tag != null)
                    {
                        w.Write($"    {memcache_tag}_data = ");
                    }
                    w.WriteLine(" *((uint *)((byte *)(pte[vaddr1_index].RealMemory + vaddr1_offset))) );");
                    if (memcache_tag != null)
                    {
                        w.WriteLine($"    {memcache_tag}_pin = {GetCode()};");
                    }
                }
                w.WriteLine("}");
                w.WriteLine("}");
            }
            else
            {
                w.WriteLine($"vaddr = {GetCode()};");

                if (VConsts.FastCheckType != FastCheckTypeEnum.NoCheck)
                {
                    w.WriteLine("#if !NO_CHECK");

                    if (VConsts.FastCheckType == FastCheckTypeEnum.Fast2)
                    {
                        if (VConsts.CodeGenTarget == CodeGenTargetEnum.CSharp)
                            w.WriteLine("if (vaddr < cont_start || vaddr >= cont_end || vaddr < cont_start2 || vaddr >= cont_end2){");
                        else
                            w.WriteLine("if ((vaddr < cont_start || vaddr >= cont_end || vaddr < cont_start2 || vaddr >= cont_end2)){");
                    }
                    else
                    {
                        if (VConsts.CodeGenTarget == CodeGenTargetEnum.CSharp)
                            w.WriteLine("if (vaddr < cont_start || vaddr >= cont_end){");
                        else
                            w.WriteLine("if ((vaddr < cont_start || vaddr >= cont_end)){");
                    }

                    if (VConsts.CodeGenTarget == CodeGenTargetEnum.CSharp)
                    {
                        w.WriteLine("    exception_string = $\"Access violation to 0x{vaddr:x}.\";");
                        w.WriteLine($"    exception_address = 0x{codeAddress:x};");
                        w.WriteLine("    goto L_RETURN;");
                    }
                    else
                    {
                        w.WriteLine("    sprintf(exception_string, \"Access violation to 0x%x.\", vaddr);");
                        w.WriteLine($"    exception_address = 0x{codeAddress:x};");
                        w.WriteLine("    goto L_RETURN;");
                    }

                    w.WriteLine("}");

                    w.WriteLine("#endif // !NO_CHECK");
                }

                if (writeMode)
                {
                    w.WriteLine($"*((uint*)(byte*)(cont_memory_minus_start + vaddr)) = {srcOrDestinationCode};");
                }
                else
                {
                    w.WriteLine($"{srcOrDestinationCode}= ({readModeRetType})(*((uint*)(byte*)(cont_memory_minus_start + vaddr)) );");
                }
            }

            return w.ToString();
        }
    }

    class AsmVirtualRegisterWriter
    {
        public string PreLines;
        public string PostLines;
        public string RealRegister;
        public bool UseTmp;

        public static void WriteSaveRFlags(StringWriter w)
        {
            // todo: try faster
            w.WriteLine("mov $0, %eax");
            w.WriteLine("seto %al");
            w.WriteLine("lahf");
        }

        public static void WriteRestoreRFlags(StringWriter w)
        {
            w.WriteLine("add $127, %al");
            w.WriteLine("sahf");
        }

        public AsmVirtualRegisterWriter(string virtualTargetRegister, string tmpRegister = "r12")
        {
            StringWriter pre = new StringWriter();
            StringWriter post = new StringWriter();
            string real = null;

            switch (virtualTargetRegister)
            {
                case "eax":
                    real = "r10d";
                    break;

                case "ecx":
                    real = "r14d";
                    break;

                case "edx":
                    real = "r9d";
                    break;

                case "ebx":
                case "ebp":
                case "esi":
                case "edi":
                    real = virtualTargetRegister;
                    break;

                case "esp":
                    real = "r11d";
                    break;

                case "ah":
                    AsmVirtualRegisterWriter.WriteSaveRFlags(post);
                    post.WriteLine($"shl $8, %{tmpRegister}d");
                    post.WriteLine($"and $0xFF00, %{tmpRegister}");
                    post.WriteLine($"and $0xFFFFFFFFFFFF00FF, %r10");
                    post.WriteLine($"or %{tmpRegister}, %r10");
                    AsmVirtualRegisterWriter.WriteRestoreRFlags(post);
                    real = $"{tmpRegister}d";
                    UseTmp = true;
                    break;

                case "al":
                    AsmVirtualRegisterWriter.WriteSaveRFlags(post);
                    post.WriteLine($"and $0xFF, %{tmpRegister}");
                    post.WriteLine($"and $0xFFFFFFFFFFFFFF00, %r10");
                    post.WriteLine($"or %{tmpRegister}, %r10");
                    AsmVirtualRegisterWriter.WriteRestoreRFlags(post);
                    real = $"{tmpRegister}d";
                    UseTmp = true;
                    break;

                case "bh":
                case "bl":
//                case "dh":
                case "dl":
                    real = virtualTargetRegister;
                    break;
            }

            if (real == null)
            {
                throw new ApplicationException($"AsmRegisterOp: Invalid register: '{virtualTargetRegister}'");
            }

            this.PreLines = pre.ToString();
            this.PostLines = post.ToString();
            this.RealRegister = real.ToString();
        }
    }

    class AsmVirtualRegisterReader
    {
        public string PreLines;
        public string PostLines;
        public string RealRegister;
        public bool UseTmp;

        public string RealRegisterOrImm
        {
            get
            {
                if (RealRegister.StartsWith("$") == false)
                {
                    return "%" + RealRegister;
                }
                else
                {
                    return RealRegister;
                }
            }
        }

        public AsmVirtualRegisterReader(VCodeOperand operand, string tmpRegister = "r12", bool forceTmp = false) :
            this(operand.GetAsmRegisterOrImm(), tmpRegister, forceTmp)
        { }

        public AsmVirtualRegisterReader(string virtualTargetRegister, string tmpRegister = "r12", bool forceTmp = false)
        {
            StringWriter pre = new StringWriter();
            StringWriter post = new StringWriter();
            string real = null;

            if (virtualTargetRegister == null) virtualTargetRegister = "";

            if (virtualTargetRegister.StartsWith("%"))
                virtualTargetRegister = virtualTargetRegister.Substring(1);

            if (virtualTargetRegister == "") virtualTargetRegister = "eiz";

            if (virtualTargetRegister.StartsWith("$"))
            {
                real = virtualTargetRegister;
            }
            else
            {
                switch (virtualTargetRegister)
                {
                    case "eax":
                        real = "r10d";
                        break;

                    case "ecx":
                        real = "r14d";
                        break;

                    case "edx":
                        real = "r9d";
                        break;

                    case "ebx":
                    case "ebp":
                    case "esi":
                    case "edi":
                    case "eiz":
                        real = virtualTargetRegister;
                        break;

                    case "esp":
                        real = "r11d";
                        break;

                    case "ah":
                        AsmVirtualRegisterWriter.WriteSaveRFlags(post);
                        pre.WriteLine($"mov %eax, %{tmpRegister}d");
                        pre.WriteLine($"shr $8, %{tmpRegister}d");
                        pre.WriteLine($"and $0xFF, %{tmpRegister}d");
                        AsmVirtualRegisterWriter.WriteRestoreRFlags(post);
                        real = $"{tmpRegister}d";
                        UseTmp = true;
                        break;

                    case "al":
                        AsmVirtualRegisterWriter.WriteSaveRFlags(post);
                        pre.WriteLine($"mov %r10d, %{tmpRegister}d");
                        pre.WriteLine($"and $0xFF, %{tmpRegister}d");
                        AsmVirtualRegisterWriter.WriteRestoreRFlags(post);
                        real = $"{tmpRegister}d";
                        UseTmp = true;
                        break;

                    case "bh":
                    case "bl":
                    case "dh":
                    //case "dl":
                        real = virtualTargetRegister;
                        if (forceTmp)
                        {
                            pre.WriteLine($"movzx %{virtualTargetRegister}, %{tmpRegister}d");
                            UseTmp = true;
                        }
                        break;
                }
            }

            if (real == null)
            {
                throw new ApplicationException($"AsmRegisterOp: Invalid register: '{virtualTargetRegister}'");
            }

            if (forceTmp && UseTmp == false)
            {
                UseTmp = true;
                if (real.StartsWith("$") == false)
                    pre.WriteLine($"mov %{real}, %{tmpRegister}d");
                else
                    pre.WriteLine($"mov {real}, %{tmpRegister}d");
                real = $"{tmpRegister}d";
            }

            this.PreLines = pre.ToString();
            this.PostLines = post.ToString();
            this.RealRegister = real.ToString();
        }
    }

    class VCodeOperation
    {
        void AsmWriteJumpCode(StringWriter asm, string opcode, VCodeOperand operand)
        {
            if (operand.IsLabel == false) throw new ApplicationException("Operand is not a label.");

            if (Gen.CodeLabels.Contains(operand.Displacement))
            {
                asm.WriteLine($"{opcode} L_{operand.Displacement:x};");
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        void WriteJumpCode(StringWriter w, string condition, VCodeOperand operand)
        {
            w.WriteLine($"if ({condition}) {{");

            //if (operand.Displacement < Address)
            {
                WriteTestInterruptCode(w);
            }

            if (operand.IsLabel == false) throw new ApplicationException("Operand is not a label.");

            if (Gen.CodeLabels.Contains(operand.Displacement))
            {
                //w.WriteLine("compare_result = 0;");
                if (VConsts.Test_DualCode == false)
                    w.WriteLine($"    goto L_{operand.Displacement:x};");
                else
                    w.WriteLine($"    goto L_FAST_{operand.Displacement:x};");
            }
            else
            {
                //w.WriteLine("compare_result = 0;");
                //w.WriteLine($"    next_ip = 0x{operand.Displacement:x};");
                //w.WriteLine("    goto L_START;");

                throw new NotImplementedException();
            }

            w.WriteLine("}");
        }

        void WriteTestInterruptCode(StringWriter w)
        {
            if (VConsts.Test_Interrupt)
            {
                if (VConsts.CodeGenTarget == CodeGenTargetEnum.CSharp)
                    w.WriteLine($"if (state.Interrupt)");
                else
                    w.WriteLine($"if (state->Interrupt)");

                w.WriteLine("{");

                //if (VConsts.CodeGenTarget == CodeGenTargetEnum.CSharp)
                //    w.WriteLine("    exception_string = $\"Interrupt at 0x{vaddr:x}.\";");
                //else
                //    w.WriteLine("    sprintf(exception_string, \"Interrupt at 0x%x.\", vaddr);");

                w.WriteLine($"    exception_address = 0x{this.Address:x};");
                w.WriteLine("    goto L_RETURN;");
                w.WriteLine("}");
            }
        }

        public void WriteCode(StringWriter w, StringWriter asm, bool is_dual_fast)
        {
            if (is_dual_fast == false)
            {
                if (Gen.LabelRefs.Contains(Address) || Gen.CallNextRefs.Contains(Address) || VConsts.Test_InterpreterMode || VConsts.Test_AllLabel)
                {
                    w.WriteLine($"L_{Address:x}:");
                }
            }
            else
            {
                if (Gen.LabelRefs.Contains(Address) || Gen.CallNextRefs.Contains(Address))
                {
                    w.WriteLine($"L_FAST_{Address:x}:");
                }
            }

            w.WriteLine("{");


            switch (Opcode)
            {
                case "pushl":
                case "push":
                    {
                        // C
                        w.WriteLine("esp -= 4;");

                        var destMemory = new VCodeOperand("(%esp)");
                        if (Operand1.IsPointer == false)
                        {
                            w.WriteLine(destMemory.GenerateMemoryAccessCode(Address, true, Operand1.GetValueAccessCode()));
                        }
                        else
                        {
                            w.WriteLine("uint tmp1;");
                            w.WriteLine(Operand1.GenerateMemoryAccessCode(Address, false, "tmp1"));
                            w.WriteLine(destMemory.GenerateMemoryAccessCode(Address, true, "tmp1"));
                        }

                        // ASM
                        // esp -= 4
                        var esp1 = new AsmVirtualRegisterReader("esp", "r12");
                        var esp2 = new AsmVirtualRegisterWriter("esp", "r13");

                        asm.Write(esp1.PreLines);
                        asm.WriteLine($"lea -4(%{esp1.RealRegister}), %{esp1.RealRegister}");
                        asm.Write(esp1.PostLines);
                        
                        asm.Write(esp2.PreLines);
                        if (esp1.RealRegister != esp2.RealRegister)
                            asm.WriteLine($"mov %{esp1.RealRegister}, %{esp2.RealRegister}");
                        asm.Write(esp2.PostLines);

                        // push
                        if (Operand1.IsPointer == false)
                        {
                            var valueReader = new AsmVirtualRegisterReader(Operand1.BaseRegister, "r12");

                            asm.Write(valueReader.PreLines);

                            var memoryWriter = destMemory.AsmGenerateMemoryWriteCode(Address, true, "r13");

                            asm.Write(memoryWriter.PreLines);
                            asm.WriteLine($"mov %{valueReader.RealRegister}, {memoryWriter.RealRegisterFixed}");
                            asm.Write(memoryWriter.PostLines);

                            asm.Write(valueReader.PostLines);
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }

                        break;
                    }

                case "pop":
                    {
                        // C
                        var destMemory = new VCodeOperand("(%esp)");
                        w.WriteLine(destMemory.GenerateMemoryAccessCode(Address, false, Operand1.GetValueAccessCode()));
                        w.WriteLine("esp += 4;");

                        // ASM
                        // pop
                        if (Operand1.IsPointer == false)
                        {
                            var valueWriter = new AsmVirtualRegisterWriter(Operand1.BaseRegister, "r12");

                            asm.Write(valueWriter.PreLines);

                            var memoryReader = destMemory.AsmGenerateMemoryWriteCode(Address, false, "r13");

                            asm.Write(memoryReader.PreLines);
                            asm.WriteLine($"mov {memoryReader.RealRegisterFixed}, %{valueWriter.RealRegister}");
                            asm.Write(memoryReader.PostLines);

                            asm.Write(valueWriter.PostLines);
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }

                        // esp += 4
                        var esp1 = new AsmVirtualRegisterReader("esp", "r12");
                        var esp2 = new AsmVirtualRegisterWriter("esp", "r13");

                        asm.Write(esp1.PreLines);
                        asm.WriteLine($"lea 4(%{esp1.RealRegister}), %{esp1.RealRegister}");
                        asm.Write(esp1.PostLines);

                        asm.Write(esp2.PreLines);
                        if (esp1.RealRegister != esp2.RealRegister)
                            asm.WriteLine($"mov %{esp1.RealRegister}, %{esp2.RealRegister}");
                        asm.Write(esp2.PostLines);
                        break;
                    }

                case "xor":
                    {
                        if (Operand2.GetValueAccessCode() == Operand1.GetValueAccessCode())
                        {
                            w.WriteLine($"{Operand2.GetValueAccessCode()} = 0;");
                        }
                        else
                        {
                            throw new NotImplementedException();
                            //w.WriteLine($"{Operand2.GetValueAccessCode()} ^= {Operand1.GetValueAccessCode()};");
                        }

                        var op1 = new AsmVirtualRegisterReader(Operand1, "r12");

                        asm.Write(op1.PreLines);
                        asm.WriteLine($"xor {op1.RealRegisterOrImm}, {op1.RealRegisterOrImm}");
                        asm.Write(op1.PostLines);
                        break;
                    }

                case "mov":
                case "movl":
                    {
                        // C / C#
                        if (Operand1.IsPointer == false && Operand2.IsPointer == false)
                        {
                            w.WriteLine($"{Operand2.GetValueAccessCode()} = {Operand1.GetValueAccessCode()};");
                        }
                        else if (Operand1.IsPointer && Operand2.IsPointer == false)
                        {
                            w.WriteLine(Operand1.GenerateMemoryAccessCode(Address, false, Operand2.GetValueAccessCode()));
                        }
                        else if (Operand2.IsPointer && Operand1.IsPointer == false)
                        {
                            w.WriteLine(Operand2.GenerateMemoryAccessCode(Address, true, Operand1.GetValueAccessCode()));
                        }
                        else
                        {
                            throw new ApplicationException("Both operand1 and operand2 are pointers.");
                        }

                        // ASM
                        if (Operand1.IsPointer == false && Operand2.IsPointer == false)
                        {
                            var op1 = new AsmVirtualRegisterReader(Operand1, "r12");
                            var op2 = new AsmVirtualRegisterReader(Operand2, "r13");

                            asm.Write(op1.PreLines);
                            asm.Write(op2.PreLines);
                            asm.WriteLine($"movl {op1.RealRegisterOrImm}, {op2.RealRegisterOrImm}");
                            asm.Write(op2.PostLines);
                            asm.Write(op1.PostLines);
                        }
                        else if (Operand1.IsPointer && Operand2.IsPointer == false)
                        {
                            var op1 = Operand1.AsmGenerateMemoryWriteCode(this.Address, false, "r12");
                            var op2 = new AsmVirtualRegisterReader(Operand2, "r13");

                            asm.Write(op1.PreLines);
                            asm.Write(op2.PreLines);
                            asm.WriteLine($"movl {op1.RealRegisterFixed}, {op2.RealRegisterOrImm}");
                            asm.Write(op2.PostLines);
                            asm.Write(op1.PostLines);
                        }
                        else if (Operand2.IsPointer && Operand1.IsPointer == false)
                        {
                            var op1 = new AsmVirtualRegisterReader(Operand1, "r13");
                            var op2 = Operand2.AsmGenerateMemoryWriteCode(this.Address, true, "r12");

                            asm.Write(op1.PreLines);
                            asm.Write(op2.PreLines);
                            asm.WriteLine($"movl {op1.RealRegisterOrImm}, {op2.RealRegisterFixed}");
                            asm.Write(op2.PostLines);
                            asm.Write(op1.PostLines);
                        }
                        else
                        {
                            throw new ApplicationException("Both operand1 and operand2 are pointers.");
                        }
                        break;
                    }

                case "test":
                    {
                        // C
                        if (Operand2.GetValueAccessCode() == Operand1.GetValueAccessCode())
                        {
                            w.WriteLine($"compare_result = (uint)({Operand2.GetValueAccessCode()});");
                        }
                        else
                        {
                            w.WriteLine($"compare_result = (uint)({Operand2.GetValueAccessCode()} & {Operand1.GetValueAccessCode()});");
                        }

                        // ASM
                        if (Operand1.IsPointer || Operand2.IsPointer)
                        {
                            throw new ApplicationException();
                        }
                        else
                        {
                            var op1 = new AsmVirtualRegisterReader(Operand1, "r12");
                            var op2 = new AsmVirtualRegisterReader(Operand2, "r13");

                            asm.Write(op1.PreLines);
                            asm.Write(op2.PreLines);
                            asm.WriteLine($"test {op1.RealRegisterOrImm}, {op2.RealRegisterOrImm}");
                            asm.Write(op2.PostLines);
                            asm.Write(op1.PostLines);
                        }
                        break;
                    }

                case "ret":
                    {
                        // C
                        var destMemory = new VCodeOperand("(%esp)");
                        //w.WriteLine(destMemory.GenerateMemoryAccessCode(Address, false, "next_ip"));

                        w.WriteLine(destMemory.GenerateMemoryAccessCode(Address, false, "next_return", "CallRetAddress"));

                        w.WriteLine("esp += 4;");
                        w.WriteLine("goto L_RET_FROM_CALL;");

                        // ASM
                        // pop
                        var memoryReader = destMemory.AsmGenerateMemoryWriteCode(Address, false, "r13");

                        asm.Write(memoryReader.PreLines);
                        asm.WriteLine($"mov {memoryReader.RealRegisterFixed}, %ecx");
                        asm.Write(memoryReader.PostLines);

                        // esp += 4
                        var esp1 = new AsmVirtualRegisterReader("esp", "r12");
                        var esp2 = new AsmVirtualRegisterWriter("esp", "r13");

                        asm.Write(esp1.PreLines);
                        asm.WriteLine($"lea 4(%{esp1.RealRegister}), %{esp1.RealRegister}");
                        asm.Write(esp1.PostLines);

                        asm.Write(esp2.PreLines);
                        if (esp1.RealRegister != esp2.RealRegister)
                            asm.WriteLine($"mov %{esp1.RealRegister}, %{esp2.RealRegister}");
                        asm.Write(esp2.PostLines);


                        // jump
                        asm.WriteLine($"mov %ecx, %r13d");
                        asm.WriteLine("jmp L_JUMP_TABLE");
                    }
                    break;


                case "jle": //TODO
                    {
                        WriteJumpCode(w, "compare_result == 0", Operand1);
                        AsmWriteJumpCode(asm, Opcode, Operand1);
                        break;
                    }

                case "je":
                    {
                        WriteJumpCode(w, "compare_result == 0", Operand1);
                        AsmWriteJumpCode(asm, Opcode, Operand1);
                        break;
                    }

                case "jb":
                    {
                        WriteJumpCode(w, "compare_result >= 0x80000000", Operand1);
                        AsmWriteJumpCode(asm, Opcode, Operand1);
                        break;
                    }

                case "jbe":
                    {
                        WriteJumpCode(w, "compare_result == 0 || compare_result >= 0x80000000", Operand1);
                        AsmWriteJumpCode(asm, Opcode, Operand1);
                        break;
                    }

                case "jae":
                    {
                        WriteJumpCode(w, "compare_result <= 0x80000000", Operand1);
                        AsmWriteJumpCode(asm, Opcode, Operand1);
                        break;
                    }

                case "ja":
                    {
                        WriteJumpCode(w, "compare_result != 0 && compare_result <= 0x80000000", Operand1);
                        AsmWriteJumpCode(asm, Opcode, Operand1);
                        break;
                    }

                case "jne":
                    {
                        WriteJumpCode(w, "compare_result != 0", Operand1);
                        AsmWriteJumpCode(asm, Opcode, Operand1);
                        break;
                    }

                case "jmp":
                    {
                        WriteJumpCode(w, "true", Operand1);
                        AsmWriteJumpCode(asm, Opcode, Operand1);
                        break;
                    }

                case "lea":
                    {
                        // C
                        w.WriteLine($"{Operand2.GetValueAccessCode()} = {Operand1.GetCode()};");

                        // ASM
                        if (Operand1.IsPointer == false || Operand2.IsPointer)
                        {
                            throw new NotImplementedException();
                        }

                        var op1_base_reg = new AsmVirtualRegisterReader(Operand1.BaseRegister, "r12");
                        var op1_index_reg = new AsmVirtualRegisterReader(Operand1.IndexRegister, "r13");
                        var op2 = new AsmVirtualRegisterReader(Operand2, "ecx");

                        asm.Write(op1_base_reg.PreLines);
                        asm.Write(op1_index_reg.PreLines);
                        asm.Write(op2.PreLines);

                        if (Operand1.IndexRegister == "")
                            asm.WriteLine($"lea {(Operand1.IsDisplacementNegative ? "-" : "")}0x{Operand1.Displacement:X}(%{op1_base_reg.RealRegister}, %{op1_index_reg.RealRegister}, {Operand1.Scaler}), {op2.RealRegisterOrImm}");
                        else
                            asm.WriteLine($"lea {(Operand1.IsDisplacementNegative ? "-" : "")}0x{Operand1.Displacement:X}(%{op1_base_reg.RealRegister}), {op2.RealRegisterOrImm}");

                        asm.Write(op2.PostLines);
                        asm.Write(op1_index_reg.PostLines);
                        asm.Write(op1_base_reg.PostLines);

                        break;
                    }

                case "div":
                    {
                        if (Operand1.IsPointer) throw new NotImplementedException();

                        // C
                        w.WriteLine("if (edx != 0) {");

                        w.WriteLine("ulong tmp1 =  (uint)(((ulong)edx << 32) + (ulong)eax);");
                        w.WriteLine($"ulong tmp2 = {Operand1.GetValueAccessCode()};");
                        w.WriteLine("eax = (uint)(tmp1 / tmp2);");
                        w.WriteLine("edx = (uint)(tmp1 - tmp2 * eax);");

                        w.WriteLine("} else");
                        w.WriteLine("{ ");

                        w.WriteLine("uint tmp1 = eax;");
                        w.WriteLine($"uint tmp2 = {Operand1.GetValueAccessCode()};");

                        w.WriteLine("eax = tmp1 / tmp2;");
                        w.WriteLine("edx = tmp1 - tmp2 * eax;");

                        w.WriteLine("}");

                        // ASM
                        var op1 = new AsmVirtualRegisterReader(Operand1.GetAsmRegisterOrImm(), "r12");

                        asm.WriteLine("mov %r10d, %eax");
                        asm.WriteLine("mov %r9d, %edx");
                        asm.WriteLine($"div {op1.RealRegisterOrImm}");
                        asm.WriteLine("mov %eax, %r10d");
                        asm.WriteLine("mov %edx, %r9d");

                        break;
                    }

                case "add":
                    {
                        // C
                        if (Operand1.IsPointer && Operand2.IsPointer == false)
                        {
                            w.WriteLine(Operand1.GenerateMemoryAccessCode(Address, false, Operand2.GetValueAccessCode() + "+"));
                        }
                        else if (Operand1.IsPointer == false && Operand2.IsPointer == false)
                        {
                            w.WriteLine($"{Operand2.GetValueAccessCode()} += {Operand1.GetValueAccessCode()};");
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                        w.WriteLine($"compare_result = {Operand2.GetValueAccessCode()};");

                        // ASM
                        if (Operand1.IsPointer && Operand2.IsPointer == false)
                        {
                            var op1 = Operand1.AsmGenerateMemoryWriteCode(this.Address, false, "r12");
                            var op2 = new AsmVirtualRegisterReader(Operand2, "r13");

                            asm.Write(op1.PreLines);
                            asm.Write(op2.PreLines);
                            asm.WriteLine($"add {op1.RealRegisterFixed}, {op2.RealRegisterOrImm}");
                            asm.Write(op2.PostLines);
                            asm.Write(op1.PostLines);
                        }
                        else if (Operand1.IsPointer == false && Operand2.IsPointer == false)
                        {
                            var op1 = new AsmVirtualRegisterReader(Operand1, "r12");
                            var op2 = new AsmVirtualRegisterReader(Operand2, "r13");

                            asm.Write(op1.PreLines);
                            asm.Write(op2.PreLines);
                            asm.WriteLine($"add {op1.RealRegisterOrImm}, {op2.RealRegisterOrImm}");
                            asm.Write(op2.PostLines);
                            asm.Write(op1.PostLines);
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                        break;
                    }

                case "inc":
                    {
                        // C
                        if (Operand1.IsPointer == false)
                        {
                            w.WriteLine(Operand1.GetValueAccessCode() + "++;");
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                        w.WriteLine($"compare_result = {Operand1.GetValueAccessCode()};");

                        // ASM
                        if (Operand1.IsPointer == false)
                        {
                            var op1 = new AsmVirtualRegisterReader(Operand1, "r12");

                            asm.Write(op1.PreLines);
                            asm.WriteLine($"inc {op1.RealRegisterOrImm}");
                            asm.Write(op1.PostLines);
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                        break;
                    }

                case "sub":
                    {
                        // C
                        w.WriteLine($"{Operand2.GetValueAccessCode()} -= {Operand1.GetValueAccessCode()};");
                        w.WriteLine($"compare_result = {Operand2.GetValueAccessCode()};");

                        // ASM
                        if (Operand1.IsPointer && Operand2.IsPointer == false)
                        {
                            var op1 = Operand1.AsmGenerateMemoryWriteCode(this.Address, false, "r12");
                            var op2 = new AsmVirtualRegisterReader(Operand2, "r13");

                            asm.Write(op1.PreLines);
                            asm.Write(op2.PreLines);
                            asm.WriteLine($"sub {op1.RealRegisterFixed}, {op2.RealRegisterOrImm}");
                            asm.Write(op2.PostLines);
                            asm.Write(op1.PostLines);
                        }
                        else if (Operand1.IsPointer == false && Operand2.IsPointer == false)
                        {
                            var op1 = new AsmVirtualRegisterReader(Operand1, "r12");
                            var op2 = new AsmVirtualRegisterReader(Operand2, "r13");

                            asm.Write(op1.PreLines);
                            asm.Write(op2.PreLines);
                            asm.WriteLine($"sub {op1.RealRegisterOrImm}, {op2.RealRegisterOrImm}");
                            asm.Write(op2.PostLines);
                            asm.Write(op1.PostLines);
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                        break;
                    }

                case "cmp":
                case "cmpl":
                    {
                        // C
                        if (Operand1.IsPointer && Operand2.IsPointer == false)
                        {
                            w.WriteLine("uint cmp1;");
                            w.WriteLine(Operand1.GenerateMemoryAccessCode(Address, false, "cmp1"));
                            w.WriteLine($"compare_result = (uint)({Operand2.GetValueAccessCode()} - cmp1);");
                        }
                        else if (Operand1.IsPointer == false && Operand2.IsPointer)
                        {
                            w.WriteLine("uint cmp1;");
                            w.WriteLine(Operand2.GenerateMemoryAccessCode(Address, false, "cmp1"));
                            w.WriteLine($"compare_result = (uint)(cmp1 - {Operand1.GetValueAccessCode()});");
                        }
                        else if (Operand1.IsPointer == false && Operand2.IsPointer == false)
                        {
                            w.WriteLine($"compare_result = (uint)({Operand2.GetValueAccessCode()} - {Operand1.GetValueAccessCode()});");
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }

                        // ASM
                        if (Operand1.IsPointer || Operand2.IsPointer)
                        {
                            throw new ApplicationException();
                        }
                        else
                        {
                            var op1 = new AsmVirtualRegisterReader(Operand1, "r12");
                            var op2 = new AsmVirtualRegisterReader(Operand2, "r13");

                            asm.Write(op1.PreLines);
                            asm.Write(op2.PreLines);
                            asm.WriteLine($"cmp {op1.RealRegisterOrImm}, {op2.RealRegisterOrImm}");
                            asm.Write(op2.PostLines);
                            asm.Write(op1.PostLines);
                        }
                        break;
                    }

                case "xchg":
                    {
                        break;
                    }

                case "nop":
                case "nopl":
                case "nopw":
                    {
                        break;
                    }

                case "call":
                    {
                        // C
                        w.WriteLine("esp -= 4;");
                        var destMemory = new VCodeOperand("(%esp)");
                        if (VConsts.CodeGenTarget == CodeGenTargetEnum.CSharp)
                            w.WriteLine(destMemory.GenerateMemoryAccessCode(Address, true, $"(uint)CallRetAddress._0x{Next.Address:x}"));
                        else
                            w.WriteLine(destMemory.GenerateMemoryAccessCode(Address, true, $"(uint)CallRetAddress__0x{Next.Address:x}"));

                        WriteJumpCode(w, "true", Operand1);

                        // ASM
                        // esp -= 4
                        var esp1 = new AsmVirtualRegisterReader("esp", "r12");
                        var esp2 = new AsmVirtualRegisterWriter("esp", "r13");

                        asm.Write(esp1.PreLines);
                        asm.WriteLine($"lea -4(%{esp1.RealRegister}), %{esp1.RealRegister}");
                        asm.Write(esp1.PostLines);

                        asm.Write(esp2.PreLines);
                        if (esp1.RealRegister != esp2.RealRegister)
                            asm.WriteLine($"mov %{esp1.RealRegister}, %{esp2.RealRegister}");
                        asm.Write(esp2.PostLines);

                        // push
                        var memoryWriter = destMemory.AsmGenerateMemoryWriteCode(Address, true, "r13");

                        asm.Write(memoryWriter.PreLines);
                        asm.WriteLine($"movl $0x{Next.Address:x}, {memoryWriter.RealRegisterFixed}");
                        asm.Write(memoryWriter.PostLines);

                        // jump
                        AsmWriteJumpCode(asm, "jmp", Operand1);
                        break;
                    }

                default:
                    throw new ApplicationException("Invalid operation: " + ToString());
            }

            if (VConsts.Test_InterpreterMode)
            {
                if (this.Next != null)
                {
                    w.WriteLine($"next_ip = 0x{this.Next.Address:X};");
                    w.WriteLine("goto L_START;");
                }
                else
                {
                    w.WriteLine("goto L_RETURN;");
                }
            }

            w.WriteLine("}");
        }

        public string GetFlagCode(string flag)
        {
            switch (flag)
            {
                case "zf":
                    return "(compare_result == 0)";

                default:
                    throw new NotImplementedException($"flag = {flag}");
            }
        }

        public uint Address;
        public string Opcode;
        public string Arguments;

        VCodeGen Gen;

        public VCodeOperand Operand1, Operand2;

        public VCodeOperation Next, Prev;

        public VCodeOperation(VCodeGen gen, uint address, string opcode, string arguments)
        {
            Gen = gen;
            Address = address;
            Opcode = opcode;
            Arguments = arguments;

            string[] tokens = SplitArguments(Arguments).ToArray();
            if (tokens.Length >= 1)
                Operand1 = new VCodeOperand(tokens[0]);
            if (tokens.Length >= 2)
                Operand2 = new VCodeOperand(tokens[1]);
        }

        public static List<string> SplitArguments(string src)
        {
            int pushedCount = 0;

            string tmp = "";

            List<string> ret = new List<string>();

            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                if (c == '(')
                {
                    pushedCount++;
                }
                else if (c == ')')
                {
                    pushedCount--;
                }

                if (c == ',')
                {
                    if (pushedCount == 0)
                    {
                        ret.Add(tmp);
                        tmp = "";
                    }
                    else
                    {
                        tmp += c;
                    }
                }
                else
                {
                    tmp += c;
                }
            }

            if (string.IsNullOrEmpty(tmp) == false)
                ret.Add(tmp);

            return ret;
        }

        public override string ToString() => $"{Address:x} {Opcode} {Arguments}";

    }

    class VCodeGen
    {
        StringWriter Out = new StringWriter();
        StringWriter Asm = new StringWriter();

        string[] Lines;
        public Dictionary<string, uint> FunctionTable;
        SortedDictionary<uint, VCodeOperation> OperationLines = new SortedDictionary<uint, VCodeOperation>();
        public HashSet<uint> CodeLabels = new HashSet<uint>();
        public HashSet<uint> LabelRefs = new HashSet<uint>();
        public HashSet<uint> CallNextRefs = new HashSet<uint>();
        public HashSet<string> MemCacheTags = new HashSet<string>();

        public override string ToString() => Out.ToString();

        public string GetAsm() => Asm.ToString();

        public VCodeGen(string[] lines, Dictionary<string, uint> functionTable)
        {
            Lines = lines;
            FunctionTable = functionTable;

            foreach (string line in Lines)
            {
                if (line.StartsWith(" "))
                {
                    string tmp1 = line.Trim();
                    int i = tmp1.IndexOf(':');
                    if (i != -1)
                    {
                        string label = tmp1.Substring(0, i);
                        string tmp2 = tmp1.Substring(i + 1).Trim();
                        int j = tmp2.IndexOf("\t");
                        if (j != -1)
                        {
                            string tmp3 = tmp2.Substring(j).Trim();
                            int k = tmp3.IndexOf(' ');
                            string operation;
                            uint address = Convert.ToUInt32(label, 16);
                            string targets;

                            if (k != -1)
                            {
                                targets = tmp3.Substring(k).Trim();
                                operation = tmp3.Substring(0, k);
                            }
                            else
                            {
                                operation = tmp3;
                                targets = "";
                            }
                            OperationLines.Add(address, new VCodeOperation(this, address, operation, targets));
                            CodeLabels.Add(address);
                        }
                    }
                }
            }

            foreach (var op in OperationLines.Values)
            {
                if (op.Operand1 != null)
                {
                    if (op.Operand1.IsLabel)
                    {
                        LabelRefs.Add(op.Operand1.Displacement);
                    }
                }
            }

            foreach (var func in FunctionTable)
            {
                LabelRefs.Add(func.Value);
            }

            VCodeOperation op_prev = null;

            foreach (VCodeOperation op in OperationLines.Values)
            {
                if (op_prev != null)
                {
                    op_prev.Next = op;
                }
                op.Prev = op_prev;

                op_prev = op;
            }

            foreach (var op in OperationLines.Values)
            {
                if (op.Opcode == "call")
                {
                    CallNextRefs.Add(op.Next.Address);
                }
            }
        }

        void WriteFunctionTable()
        {
            if (VConsts.CodeGenTarget == CodeGenTargetEnum.CSharp)
                Out.WriteLine("public enum FunctionTable");
            else
                Out.WriteLine("enum FunctionTable");

            Out.WriteLine("{");
            foreach (string functionName in this.FunctionTable.Keys)
            {
                if (VConsts.CodeGenTarget == CodeGenTargetEnum.CSharp)
                    Out.WriteLine($"    {functionName} = 0x{FunctionTable[functionName]:x},");
                else
                    Out.WriteLine($"    FunctionTable_{functionName} = 0x{FunctionTable[functionName]:x},");
            }
            Out.WriteLine("}");
            if (VConsts.CodeGenTarget == CodeGenTargetEnum.C)
                Out.WriteLine(";");
            Out.WriteLine("");
        }

        void WriteMainFunction()
        {
            if (VConsts.CodeGenTarget == CodeGenTargetEnum.CSharp)
                Out.WriteLine("public enum CallRetAddress {");
            else
                Out.WriteLine("enum CallRetAddress {");

            if (VConsts.CodeGenTarget == CodeGenTargetEnum.CSharp)
                Out.WriteLine($"    _MagicReturn,");
            else
                Out.WriteLine($"    CallRetAddress__MagicReturn,");

            if (VConsts.Test_AllLabel == false)
            {
                foreach (uint faddr in this.CallNextRefs)
                {
                    if (VConsts.CodeGenTarget == CodeGenTargetEnum.CSharp)
                        Out.WriteLine($"    _0x{faddr:x},");
                    else
                        Out.WriteLine($"    CallRetAddress__0x{faddr:x},");
                }
            }
            else
            {
                foreach (uint faddr in this.CodeLabels)
                {
                    if (VConsts.CodeGenTarget == CodeGenTargetEnum.CSharp)
                        Out.WriteLine($"    _0x{faddr:x},");
                    else
                        Out.WriteLine($"    CallRetAddress__0x{faddr:x},");
                }
            }
            Out.WriteLine("}");

            if (VConsts.CodeGenTarget == CodeGenTargetEnum.C)
                Out.WriteLine(";");

            Out.WriteLine();

            if (VConsts.CodeGenTarget == CodeGenTargetEnum.CSharp)
                Out.WriteLine("public static void Iam_The_IntelCPU_HaHaHa(VCpuState state, uint ip)");
            else
            {
                Out.WriteLine("#ifndef  HEADER_ONLY");
                Out.WriteLine("void Iam_The_IntelCPU_HaHaHa(VCpuState *state, uint ip)");
            }

            Out.WriteLine("{");

            if (VConsts.CodeGenTarget == CodeGenTargetEnum.CSharp)
            {
                Out.WriteLine("uint eax = state.Eax;");
                Out.WriteLine("uint ebx = state.Ebx;");
                Out.WriteLine("uint ecx = state.Ecx;");
                Out.WriteLine("uint edx = state.Edx;");
                Out.WriteLine("uint esp = state.Esp; ");
                Out.WriteLine("uint esi = state.Esi; ");
                Out.WriteLine("uint edi = state.Edi; ");
                Out.WriteLine("uint ebp = state.Ebp; ");
            }
            else
            {
                Out.WriteLine("uint eax = state->Eax;");
                Out.WriteLine("uint ebx = state->Ebx;");
                Out.WriteLine("uint ecx = state->Ecx;");
                Out.WriteLine("uint edx = state->Edx;");
                Out.WriteLine("uint esp = state->Esp; ");
                Out.WriteLine("uint esi = state->Esi; ");
                Out.WriteLine("uint edi = state->Edi; ");
                Out.WriteLine("uint ebp = state->Ebp; ");
            }

            Out.WriteLine("uint cache_last_page1 = 0xffffffff;");
            Out.WriteLine("uint last_used_cache = 0;");
            Out.WriteLine("byte *cache_last_realaddr1 = null;");
            Out.WriteLine("uint cache_last_page2 = 0xffffffff;");
            Out.WriteLine("byte *cache_last_realaddr2 = null;");
            Out.WriteLine("uint vaddr = 0, vaddr1_index = 0, vaddr1_offset = 0;");
            Out.WriteLine("uint write_tmp = 0, read_tmp = 0;");
            Out.WriteLine("uint compare_result = 0;");

            if (VConsts.CodeGenTarget == CodeGenTargetEnum.CSharp)
            {
                Out.WriteLine("VMemory Memory = state.Memory;");
                Out.WriteLine("VPageTableEntry* pte = Memory.PageTableEntry;");
                Out.WriteLine("byte *cont_memory = Memory.ContiguousMemory;");
                Out.WriteLine("uint cont_start = Memory.ContiguousStart;");
                Out.WriteLine("uint cont_end = Memory.ContiguousEnd;");
                if (VConsts.FastCheckType == FastCheckTypeEnum.Fast2)
                {
                    Out.WriteLine("uint cont_start2 = Memory.ContiguousStart;");
                    Out.WriteLine("uint cont_end2 = Memory.ContiguousEnd;");
                }
                Out.WriteLine("byte *cont_memory_minus_start = (byte *)(Memory.ContiguousMemory - cont_start);");
            }
            else
            {
                Out.WriteLine("VMemory *Memory = state->Memory;");
                Out.WriteLine("VPageTableEntry* pte = Memory->PageTableEntry;");
                Out.WriteLine("byte *cont_memory = Memory->ContiguousMemory;");
                Out.WriteLine("uint cont_start = Memory->ContiguousStart;");
                Out.WriteLine("uint cont_end = Memory->ContiguousEnd;");
                if (VConsts.FastCheckType == FastCheckTypeEnum.Fast2)
                {
                    Out.WriteLine("uint cont_start2 = Memory->ContiguousStart;");
                    Out.WriteLine("uint cont_end2 = Memory->ContiguousEnd;");
                }
                Out.WriteLine("byte *cont_memory_minus_start = (byte *)(Memory->ContiguousMemory - cont_start);");
            }

            Out.WriteLine("uint next_ip = ip;");

            if (VConsts.CodeGenTarget == CodeGenTargetEnum.CSharp)
                Out.WriteLine("CallRetAddress next_return = (CallRetAddress)0x7fffffff;");
            else
                Out.WriteLine("uint next_return = 0x7fffffff;");

            if (VConsts.CodeGenTarget == CodeGenTargetEnum.CSharp)
            {
                Out.WriteLine("ref ushort al = ref *((ushort*)(&eax) + 0); ref ushort ah = ref *((ushort*)(&eax) + 1);");
                Out.WriteLine("ref ushort bl = ref *((ushort*)(&ebx) + 0); ref ushort bh = ref *((ushort*)(&ebx) + 1);");
                Out.WriteLine("ref ushort cl = ref *((ushort*)(&ecx) + 0); ref ushort ch = ref *((ushort*)(&ecx) + 1);");
                Out.WriteLine("ref ushort dl = ref *((ushort*)(&edx) + 0); ref ushort dh = ref *((ushort*)(&edx) + 1);");
            }
            else
            {
                Out.WriteLine("ushort *al = ((ushort*)(&eax) + 0); ushort *ah = ((ushort*)(&eax) + 1);");
                Out.WriteLine("ushort *bl = ((ushort*)(&ebx) + 0); ushort *bh = ((ushort*)(&ebx) + 1);");
                Out.WriteLine("ushort *cl = ((ushort*)(&ecx) + 0); ushort *ch = ((ushort*)(&ecx) + 1);");
                Out.WriteLine("ushort *dl = ((ushort*)(&edx) + 0); ushort *dh = ((ushort*)(&edx) + 1);");
            }

            Out.WriteLine("const uint eiz = 0; ");

            if (VConsts.CodeGenTarget == CodeGenTargetEnum.CSharp)
                Out.WriteLine("string exception_string = null;");
            else
                Out.WriteLine("char exception_string[256] = {0};");

            Out.WriteLine("uint exception_address = 0;");
            Out.WriteLine("byte *realaddr1 = null;");

            foreach (VCodeOperation op in OperationLines.Values)
            {
                if (op.Opcode != "lea")
                {
                    List<VCodeOperand> operands = new List<VCodeOperand>();
                    operands.Add(op.Operand1); operands.Add(op.Operand2);
                    operands.Add(new VCodeOperand("(%esp)"));
                    foreach (var operand in operands.Where(x => x != null))
                    {
                        if (operand.IsPointer && operand.BaseRegister != null && operand.Scaler == 0 && operand.IndexRegister == null)
                        {
                            MemCacheTags.Add($"memcache_{operand.BaseRegister}_0x{operand.Displacement:x}");
                        }
                    }
                }
            }
            foreach (string tag in MemCacheTags)
            {
                Out.WriteLine($"uint {tag}_pin = 0x7fffffff; uint {tag}_data = 0xcafebeef;");
            }

            Out.WriteLine();

            if (VConsts.CodeGenTarget == CodeGenTargetEnum.C)
            {
                Out.WriteLine(
    @"
if (state->UseAsm)
{
	DYNASM_CPU_STATE dyn = { 0 };

	dyn.ContMemMinusStart = cont_memory_minus_start;
	dyn.Eax = eax;
	dyn.Ebx = ebx;
	dyn.Ecx = ecx;
	dyn.Edx = edx;
	dyn.Esi = esi;
	dyn.Edi = edi;
	dyn.Ebp = ebp;
	dyn.Esp = esp;
	dyn.StartIp = next_ip;

	dynasm(&dyn);

	eax = dyn.Eax;
	ebx = dyn.Ebx;
	ecx = dyn.Ecx;
	edx = dyn.Edx;
	esi = dyn.Esi;
	edi = dyn.Edi;
	ebp = dyn.Ebp;
	esp = dyn.Esp;

	if (dyn.ExceptionType != 0)
	{
		exception_address = dyn.ExceptionAddress;
		sprintf(exception_string, ""ASM ExceptionType: % u"", dyn.ExceptionType);

    }

	goto L_RETURN;
}
");
            }

            Out.WriteLine();

            Out.WriteLine("L_START:");
            Out.WriteLine("switch (next_ip)");
            Out.WriteLine("{");

            if (VConsts.Test_InterpreterMode == false && VConsts.Test_AllLabel == false)
            {
                foreach (var func in FunctionTable.Values)
                {
                    Out.Write($"case 0x{func:x}: ");
                    Out.WriteLine($"goto L_{func:x};");
                }
            }
            else
            {
                foreach (VCodeOperation op in OperationLines.Values)
                {
                    //if (LabelRefs.Contains(op.Address))
                    {
                        Out.Write($"case 0x{op.Address:x}: ");
                        Out.WriteLine($"goto L_{op.Address:x};");
                    }
                }
            }

            //Out.Write($"case 0x{VConsts.Magic_Return:x}: ");
            //Out.WriteLine("goto L_RETURN;");

            Out.WriteLine("default:");
            if (VConsts.CodeGenTarget == CodeGenTargetEnum.CSharp)
                Out.WriteLine($"    exception_string = \"Invalid jump target.\";");
            else
                Out.WriteLine($"    sprintf(exception_string, \"Invalid jump target.\");");
            Out.WriteLine("    exception_address = next_ip;");
            Out.WriteLine("    goto L_RETURN;");

            Out.WriteLine("}");

            Out.WriteLine();

            Out.WriteLine("L_RET_FROM_CALL:");


            Out.WriteLine("switch (next_return)");
            Out.WriteLine("{");

            if (VConsts.CodeGenTarget == CodeGenTargetEnum.CSharp)
                Out.Write($"case CallRetAddress._MagicReturn: ");
            else
                Out.Write($"case CallRetAddress__MagicReturn: ");

            Out.WriteLine("goto L_RETURN;");

            foreach (VCodeOperation op in OperationLines.Values)
            {
                if (CallNextRefs.Contains(op.Address) || VConsts.Test_AllLabel)
                {
                    if (VConsts.CodeGenTarget == CodeGenTargetEnum.CSharp)
                        Out.Write($"case CallRetAddress._0x{op.Address:x}: ");
                    else
                        Out.Write($"case CallRetAddress__0x{op.Address:x}: ");
                    Out.WriteLine($"goto L_{op.Address:x};");
                }
            }


            //Out.WriteLine("switch (next_ip)");
            //Out.WriteLine("{");

            //foreach (VCodeOperation op in OperationLines.Values)
            //{
            //    if (CallNextRefs.Contains(op.Address))
            //    {
            //        Out.Write($"case 0x{op.Address:x}: ");
            //        Out.WriteLine($"goto L_{op.Address:x};");
            //    }
            //}

            //Out.Write($"case 0x{VConsts.Magic_Return:x}: ");
            //Out.WriteLine("goto L_RETURN;");

            Out.WriteLine("default:");
            if (VConsts.CodeGenTarget == CodeGenTargetEnum.CSharp)
                Out.WriteLine($"    exception_string = \"Invalid call return target.\";");
            else
                Out.WriteLine($"    sprintf(exception_string, \"Invalid call return target.\");");
            Out.WriteLine("    exception_address = next_ip;");
            Out.WriteLine("    goto L_RETURN;");

            Out.WriteLine("}");

            Out.WriteLine();

            // ASM jump table
            Asm.WriteLine("L_JUMP_TABLE:");
            Asm.WriteLine("# Jump table");
            AsmVirtualRegisterWriter.WriteSaveRFlags(Asm);

            uint address_min = OperationLines.Values.Select(x => x.Address).Min();
            uint address_max = OperationLines.Values.Select(x => x.Address).Max();
            uint num_address = address_max - address_min + 1;

            Asm.WriteLine($"cmp $0x7fffffff, %r13d");
            Asm.WriteLine($"jne L_RESUME");
            AsmVirtualRegisterWriter.WriteRestoreRFlags(Asm);
            Asm.WriteLine("jmp L_ERROR");

            Asm.WriteLine("L_RESUME:");
            Asm.WriteLine($"cmp $0x{address_min:x}, %r13d");
            Asm.WriteLine($"jb L_JUMP_TABLE_INVALID_ADDRESS");
            Asm.WriteLine($"cmp $0x{address_max:x}, %r13d");
            Asm.WriteLine($"ja L_JUMP_TABLE_INVALID_ADDRESS");

            AsmVirtualRegisterWriter.WriteRestoreRFlags(Asm);

            Asm.WriteLine($"lea -0x{address_min:x}(%r13d), %r14d");
            Asm.WriteLine($"lea 7(%rip), %r12");
            Asm.WriteLine($"mov (%r12, %r14, 8), %r14");
            Asm.WriteLine($"jmp %r14");

            //Asm.WriteLine(".section .rdata");
            //Asm.WriteLine(".align 8");
            Asm.WriteLine("L_JUMP_TABLE_DATA:");
            for (uint relAddress = 0; relAddress < num_address; relAddress++)
            {
                uint virtualAddress = address_min + relAddress;
                if (OperationLines.ContainsKey(virtualAddress) == false)
                {
                    Asm.WriteLine($".quad L_JUMP_TABLE_INVALID_ADDRESS2");
                }
                else
                {
                    Asm.WriteLine($".quad L_{virtualAddress:x}");
                }
            }
            //Asm.WriteLine(".text");

            Asm.WriteLine("L_JUMP_TABLE_INVALID_ADDRESS:");

            AsmVirtualRegisterWriter.WriteRestoreRFlags(Asm);

            Asm.WriteLine("L_JUMP_TABLE_INVALID_ADDRESS2:");

            Asm.WriteLine($"movl ${AsmExceptionType.InvalidJumpTarget}, DYNASM_CPU_STATE_EXCEPTION_TYPE(%r8)");
            Asm.WriteLine("movl %r13d, DYNASM_CPU_STATE_EXCEPTION_ADDRESS(%r8)");
            Asm.WriteLine("jmp L_ERROR");

            Asm.WriteLine();

            foreach (VCodeOperation op in OperationLines.Values)
            {
                if (FunctionTable.Values.Contains(op.Address))
                {
                    Out.WriteLine($"// function {FunctionTable.Where(x => x.Value == op.Address).Select(x => x.Key).Single()}();");
                }

                Out.WriteLine($"// {op.ToString()}");

                Asm.WriteLine($"L_{op.Address:x}:");
                Asm.WriteLine($"# {op.ToString()}");

                op.WriteCode(Out, Asm, false);

                Out.WriteLine();
                Asm.WriteLine();
            }

            if (VConsts.Test_DualCode)
            {
                foreach (VCodeOperation op in OperationLines.Values)
                {
                    if (FunctionTable.Values.Contains(op.Address))
                    {
                        Out.WriteLine($"// function {FunctionTable.Where(x => x.Value == op.Address).Select(x => x.Key).Single()}();");
                    }

                    Out.WriteLine($"// {op.ToString()}");
                    op.WriteCode(Out, Asm, true);
                    Out.WriteLine();
                }
            }

            Out.WriteLine(" // Restore CPU state");
            Out.WriteLine("L_RETURN:");

            if (VConsts.CodeGenTarget == CodeGenTargetEnum.CSharp)
            {
                Out.Write(" state.Eax = eax; ");
                Out.Write(" state.Ebx = ebx; ");
                Out.Write(" state.Ecx = ecx; ");
                Out.WriteLine(" state.Edx = edx; ");
                Out.Write(" state.Esi = esi; ");
                Out.Write(" state.Edi = edi; ");
                Out.Write(" state.Ebp = ebp; ");
                Out.WriteLine(" state.Esp = esp; ");
                Out.WriteLine(" state.ExceptionString = exception_string;");
                Out.WriteLine(" state.ExceptionAddress = exception_address;");
            }
            else
            {
                Out.Write(" state->Eax = eax; ");
                Out.Write(" state->Ebx = ebx; ");
                Out.Write(" state->Ecx = ecx; ");
                Out.WriteLine(" state->Edx = edx; ");
                Out.Write(" state->Esi = esi; ");
                Out.Write(" state->Edi = edi; ");
                Out.Write(" state->Ebp = ebp; ");
                Out.WriteLine(" state->Esp = esp; ");
                Out.WriteLine(" strcpy(state->ExceptionString, exception_string);");
                Out.WriteLine(" state->ExceptionAddress = exception_address;");
            }

            Out.WriteLine("}");

            if (VConsts.CodeGenTarget == CodeGenTargetEnum.C)
            {
                Out.WriteLine("#endif");
            }
        }

        public void DoMain()
        {
            Out = new StringWriter();
            Asm = new StringWriter();

            Out.WriteLine($"// Auto generated by IPA Box Test for {VConsts.CodeGenTarget}");

            if (VConsts.CodeGenTarget == CodeGenTargetEnum.CSharp)
            {
                Out.WriteLine("using System;");
                Out.WriteLine("using System.Runtime.CompilerServices;");
                Out.WriteLine("using SoftEther.WebSocket.Helper;");
                Out.WriteLine();
                Out.WriteLine("#pragma warning disable CS0164, CS0219, CS1717, CS0162, CS0168");
                Out.WriteLine();
                Out.WriteLine("public static unsafe class VCode");
                Out.WriteLine("{");
            }
            else
            {
                Out.WriteLine(
@"

#define GENERATED_CODE_C
#include ""common.h""

");
            }

            string[] asmExceptionTypes = Enum.GetNames(typeof(AsmExceptionType));
            foreach (string asmExceptionName in asmExceptionTypes)
            {
                Asm.WriteLine($"{asmExceptionName} = {(int)Enum.Parse(typeof(AsmExceptionType), asmExceptionName)}");
            }

            StringWriter asmGlobals = new StringWriter();
            foreach (uint vaddr in this.OperationLines.Keys)
            {
                asmGlobals.WriteLine($"    .globl L_{vaddr:x}");
            }

            Asm.WriteLine();

            Asm.WriteLine(
@"
.include ""dynasm_include.s""

	.text
	.globl	dynasm
	.globl	dynasm_begin
	.globl	dynasm_end
    .globl  L_ERROR
    .globl  L_JUMP_TABLE
    .globl  L_JUMP_TABLE_DATA
    .globl  L_JUMP_TABLE_INVALID_ADDRESS
    .globl  L_JUMP_TABLE_INVALID_ADDRESS2
"

+ asmGlobals.ToString() +

@".type	dynasm, @function
dynasm:
	.cfi_startproc
	push	%r12
	push	%r13
	push	%r14
	push	%r15
	push	%rdi
	push	%rsi
	push	%rbx
	push	%rbp
	lea 	-16*10(%rsp), %rsp
	vmovdqu	%xmm6, 16*0(%rsp)
	vmovdqu	%xmm7, 16*1(%rsp)
	vmovdqu	%xmm8, 16*2(%rsp)
	vmovdqu	%xmm9, 16*3(%rsp)
	vmovdqu	%xmm10, 16*4(%rsp)
	vmovdqu	%xmm11, 16*5(%rsp)
	vmovdqu	%xmm12, 16*6(%rsp)
	vmovdqu	%xmm13, 16*7(%rsp)
	vmovdqu	%xmm14, 16*8(%rsp)
	vmovdqu	%xmm15, 16*9(%rsp)

    mov     %rcx, %r8

    mov     DYNASM_CPU_STATE_CONT_MEM_MINUS_START(%r8), %r15
    mov     DYNASM_CPU_STATE_START_IP(%r8), %r13d
");

            foreach (string vReg in VConsts.RegisterList)
            {
                var reader = new AsmVirtualRegisterReader(vReg);
                Asm.WriteLine($"    movl DYNASM_CPU_STATE_{vReg.ToUpper()}(%r8), %{reader.RealRegister}");
            }

            Asm.WriteLine(
@"dynasm_begin:

	# -- BEGIN --

");

            WriteMainFunction();

            Out.WriteLine();

            WriteFunctionTable();

            if (VConsts.CodeGenTarget == CodeGenTargetEnum.CSharp)
            {
                Out.WriteLine("}");
            }

            Asm.WriteLine(
@"

	# -- end --

L_ERROR:

dynasm_end:");

            foreach (string vReg in VConsts.RegisterList)
            {
                var reader = new AsmVirtualRegisterReader(vReg);
                Asm.WriteLine($"    movl %{reader.RealRegister}, DYNASM_CPU_STATE_{vReg.ToUpper()}(%r8)");
            }

            Asm.WriteLine(
@"
	vmovdqu	16*9(%rsp), %xmm15
	vmovdqu	16*8(%rsp), %xmm14
	vmovdqu	16*7(%rsp), %xmm13
	vmovdqu	16*6(%rsp), %xmm12
	vmovdqu	16*5(%rsp), %xmm11
	vmovdqu	16*4(%rsp), %xmm10
	vmovdqu	16*3(%rsp), %xmm9
	vmovdqu	16*2(%rsp), %xmm8
	vmovdqu	16*1(%rsp), %xmm7
	vmovdqu	16*0(%rsp), %xmm6
	lea		16*10(%rsp), %rsp
	pop	%rbp
	pop	%rbx
	pop	%rsi
	pop	%rdi
	pop	%r15
	pop	%r14
	pop	%r13
	pop	%r12
	ret
	.cfi_endproc
");

            Out.WriteLine();
        }
    }

    static class VCpuCodeGenTest
    {
        public static void CodeGen()
        {
            Console.WriteLine($"IPA Box CodeGen Test for {VConsts.CodeGenTarget}");
            Console.WriteLine();

            string srcAsm = @"C:\git\DNT-Jikken\MVPNClientTest\NativeCodeTest\bin\test_exec.asm";
            string dstCs;
            string dstAsm;

            if (VConsts.CodeGenTarget == CodeGenTargetEnum.CSharp)
                dstCs = @"C:\git\DNT-Jikken\MVPNClientTest\VCpuTest\GeneratedCode.cs";
            else
                dstCs = @"C:\git\DNT-Jikken\MVPNClientTest\VCpuTestNative\GeneratedCode.c";

            dstAsm = @"C:\git\DNT-Jikken\MVPNClientTest\VCpuTestNative\dynasm.s";

            string[] includeFunctions = new string[]
                {
                    "test_target1",
                    "test_target2",
                    "test_target3",
                    "test_target4",
                    "test_target5",
                    "test_target6",
                    "test_target7",
                    "test_target8",
                    "test_target9",
                };

            string[] lines = File.ReadAllLines(srcAsm);

            string currentFuncName = null;

            Dictionary<string, List<string>> linesByFunc = new Dictionary<string, List<string>>();
            Dictionary<string, uint> functionTable = new Dictionary<string, uint>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (line.Contains("<") && line.EndsWith(">:"))
                {
                    currentFuncName = includeFunctions.Where(f => line.EndsWith($"<{f}>:")).FirstOrDefault();
                    if (currentFuncName != null)
                    {
                        linesByFunc.Add(currentFuncName, new List<string>());
                        functionTable.Add(currentFuncName, Convert.ToUInt32(line.Split(' ')[0], 16));
                    }
                }

                if (currentFuncName != null)
                    linesByFunc[currentFuncName].Add(line);
            }

            lines = linesByFunc.Values.SelectMany(x => x.ToArray()).ToArray();

            VCodeGen gen = new VCodeGen(lines, functionTable);

            gen.DoMain();

            File.WriteAllText(dstCs, gen.ToString());

            File.WriteAllText(dstAsm, gen.GetAsm());
        }
    }
}


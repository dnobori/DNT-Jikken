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
    public static class VConsts
    {
        public const uint PageSize = 4096;
        public const uint NumPages = (uint)(0x100000000 / PageSize);
        public const uint Magic_Return = 0xdeadbeef;
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
            PageTableEntry = (VPageTableEntry *)Marshal.AllocCoTaskMem((int)(sizeof(VPageTableEntry) * VConsts.NumPages));
            for (int i = 0; i < VConsts.NumPages; i++)
            {
                PageTableEntry[i].RealMemory = null;
                PageTableEntry[i].CanRead = PageTableEntry[i].CanWrite = false;
            }
        }

        public void Write(uint address, uint value)
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

        public uint Read(uint address)
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

        public void AllocateMemory(uint startAddress, uint size, bool canRead = true, bool canWrite = true)
        {
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

        public void AllignMemoryToPage(uint startAddress, uint size, out uint pageNumberStart, out uint pageCount)
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

        public VProcess Process;
        public VMemory Memory;

        public VCpuState(VProcess process)
        {
            this.Process = process;
            this.Memory = this.Process.Memory;
        }
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

    class VCodeOperand
    {
        public bool IsPointer = false;
        public bool IsLabel = false;
        public uint Displacement = 0;
        public bool IsDisplacementNegative = false;
        public string BaseRegister = null;
        public string OffsetRegister = null;
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
                        OffsetRegister = GetRegisterName(tokens[1]);
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

        public string GetCode()
        {
            if (this.IsLabel) throw new ApplicationException("This operand is a label.");
            string str = "";
            if (this.BaseRegister != null)
                str += this.BaseRegister;
            if (this.OffsetRegister != null && this.Scaler != 0)
                str = "(" + str + " + " + this.OffsetRegister + " * " + "0x" + this.Scaler.ToString("x") + ")";
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

        public string GenerateMemoryAccessCode(uint codeAddress, bool writeMode, string srcOrDestinationCode)
        {
            StringWriter w = new StringWriter();

            w.WriteLine($"uint vaddr = {GetCode()};");
            w.WriteLine($"uint vaddr1_index = vaddr / VConsts.PageSize;");
            w.WriteLine($"uint vaddr1_offset = vaddr % VConsts.PageSize;");
            w.WriteLine($"if (vaddr1_index == cache_last_page1)");
            w.WriteLine("{");
            if (writeMode)
            {
                w.WriteLine($"    *((uint *)(((byte *)cache_last_realaddr1) + vaddr1_offset)) = {srcOrDestinationCode};");
            }
            else
            {
                w.WriteLine($"    {srcOrDestinationCode}= *((uint *)(((byte *)cache_last_realaddr1) + vaddr1_offset));");
            }
            w.WriteLine("}");
            w.WriteLine("else if (vaddr1_index == cache_last_page2)");
            w.WriteLine("{");
            if (writeMode)
            {
                w.WriteLine($"    *((uint *)(((byte *)cache_last_realaddr2) + vaddr1_offset)) = {srcOrDestinationCode};");
            }
            else
            {
                w.WriteLine($"    {srcOrDestinationCode}= *((uint *)(((byte *)cache_last_realaddr2) + vaddr1_offset));");
            }
            w.WriteLine("}");
            w.WriteLine("else");
            w.WriteLine("{");
            w.WriteLine($"if (pte[vaddr1_index].{(writeMode ? "CanWrite" : "CanRead")} == false)");
            w.WriteLine("{");
            w.WriteLine("    exception_string = $\"Access violation to 0x{vaddr:x}.\";");
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
            w.WriteLine($"byte *realaddr1 = (byte *)(pte[vaddr1_index].RealMemory + vaddr1_offset);");

            
            // beyond two pages
            w.WriteLine("if ((vaddr1_offset + 4) > VConsts.PageSize)");
            w.WriteLine("{");
            w.WriteLine("    uint size1 = VConsts.PageSize - vaddr1_offset;");
            w.WriteLine("    uint size2 = 4 - size1;");
            w.WriteLine("    uint vaddr2 = vaddr + size1;");

            w.WriteLine($"    uint vaddr2_index = vaddr2 / VConsts.PageSize;");
            w.WriteLine($"    if (pte[vaddr2_index].{(writeMode ? "CanWrite" : "CanRead")} == false)");
            w.WriteLine("    {");
            w.WriteLine("        exception_string = $\"Access violation to 0x{vaddr2:x}.\";");
            w.WriteLine($"        exception_address = 0x{codeAddress:x};");
            w.WriteLine("        goto L_RETURN;");
            w.WriteLine("    }");
            w.WriteLine($"    byte *realaddr2 = (byte *)(pte[vaddr2_index].RealMemory);");
            if (writeMode)
            {
                w.WriteLine($"    uint set_value = {srcOrDestinationCode};");
                w.WriteLine("    byte *set_ptr = (byte *)set_value;");
                w.WriteLine("    if (size1 == 1) { realaddr1[0] = set_ptr[0]; realaddr2[0] = set_ptr[1]; realaddr2[1] = set_ptr[2]; realaddr2[2] = set_ptr[3]; }");
                w.WriteLine("    else if (size1 == 2) { realaddr1[0] = set_ptr[0]; realaddr1[1] = set_ptr[1]; realaddr2[0] = set_ptr[2]; realaddr2[1] = set_ptr[3]; }");
                w.WriteLine("    else if (size1 == 3) { realaddr1[0] = set_ptr[0]; realaddr1[1] = set_ptr[1]; realaddr1[2] = set_ptr[2]; realaddr2[0] = set_ptr[3]; }");
            }
            else
            {
                w.WriteLine($"    uint get_value = 0;");
                w.WriteLine("    byte *get_ptr = (byte *)get_value;");
                w.WriteLine("    if (size1 == 1) { get_ptr[0] = realaddr1[0]; get_ptr[1] = realaddr2[0]; get_ptr[2] = realaddr2[1]; get_ptr[3] = realaddr2[2]; }");
                w.WriteLine("    else if (size1 == 2) { get_ptr[0] = realaddr1[0]; get_ptr[1] = realaddr1[1]; get_ptr[2] = realaddr2[0]; get_ptr[3] = realaddr2[1]; }");
                w.WriteLine("    else if (size1 == 3) { get_ptr[0] = realaddr1[0]; get_ptr[1] = realaddr1[1]; get_ptr[2] = realaddr1[2]; get_ptr[3] = realaddr2[0]; }");
                w.WriteLine($"    {srcOrDestinationCode}= get_value;");
            }

            w.WriteLine("}");

            // single page
            w.WriteLine("else"); 
            w.WriteLine("{");
            if (writeMode)
            {
                w.WriteLine($"    *((uint *)realaddr1) ={srcOrDestinationCode};");
            }
            else
            {
                w.WriteLine($"    {srcOrDestinationCode}= *((uint *)realaddr1);");
            }
            w.WriteLine("}");
            w.WriteLine("}");

            return w.ToString();
        }
    }

    class VCodeOperation
    {
        void WriteJumpCode(StringWriter w, string condition, VCodeOperand operand)
        {
            w.WriteLine($"if ({condition}) {{");

            if (operand.IsLabel == false) throw new ApplicationException("Operand is not a label.");

            if (Gen.CodeLabels.Contains(operand.Displacement))
            {
                //w.WriteLine("compare_result = 0;");
                w.WriteLine($"    goto L_{operand.Displacement:x};");
            }
            else
            {
                w.WriteLine("compare_result = 0;");
                w.WriteLine($"    next_ip = 0x{operand.Displacement:x};");
                w.WriteLine("    goto L_START;");
            }

            w.WriteLine("}");
        }

        public void WriteCode(StringWriter w)
        {
            if (Gen.LabelRefs.Contains(Address))
            {
                w.WriteLine($"L_{Address:x}:");
            }

            w.WriteLine("{");

            switch (Opcode)
            {
                case "push":
                    {
                        w.WriteLine("esp -= 4;");
                        var destMemory = new VCodeOperand("(%esp)");
                        w.WriteLine(destMemory.GenerateMemoryAccessCode(Address, true, Operand1.GetValueAccessCode()));
                        break;
                    }

                case "pop":
                    {
                        var destMemory = new VCodeOperand("(%esp)");
                        w.WriteLine(destMemory.GenerateMemoryAccessCode(Address, false, Operand1.GetValueAccessCode()));
                        w.WriteLine("esp += 4;");
                        break;
                    }

                case "xor":
                    {
                        w.WriteLine($"{Operand2.GetValueAccessCode()} ^= {Operand1.GetValueAccessCode()};");
                        break;
                    }

                case "mov":
                case "movl":
                    {
                        if (Operand1.IsPointer == false && Operand2.IsPointer == false)
                        {
                            w.WriteLine($"{Operand2.GetValueAccessCode()} = {Operand1.GetValueAccessCode()};");
                        }
                        else if (Operand1.IsPointer)
                        {
                            w.WriteLine(Operand1.GenerateMemoryAccessCode(Address, false, Operand2.GetValueAccessCode()));
                        }
                        else if (Operand2.IsPointer)
                        {
                            w.WriteLine(Operand2.GenerateMemoryAccessCode(Address, true, Operand1.GetValueAccessCode()));
                        }
                        else
                        {
                            throw new ApplicationException("Both operand1 and operand2 are pointers.");
                        }
                        break;
                    }

                case "test":
                    {
                        if (Operand2.GetValueAccessCode() == Operand1.GetValueAccessCode())
                        {
                            w.WriteLine($"compare_result = (uint)({Operand2.GetValueAccessCode()});");
                        }
                        else
                        {
                            w.WriteLine($"compare_result = (uint)({Operand2.GetValueAccessCode()} & {Operand1.GetValueAccessCode()});");
                        }
                        break;
                    }

                case "ret":
                    {
                        var destMemory = new VCodeOperand("(%esp)");
                        w.WriteLine(destMemory.GenerateMemoryAccessCode(Address, false, "next_ip"));
                        w.WriteLine("esp += 4;");
                        w.WriteLine("goto L_START;");
                    }
                    break;
                    

                case "je":
                    {
                        WriteJumpCode(w, "compare_result == 0", Operand1);
                        break;
                    }

                case "jbe":
                    {
                        WriteJumpCode(w, "compare_result == 0 || compare_result >= 0x80000000", Operand1);
                        break;
                    }

                case "jae":
                    {
                        WriteJumpCode(w, "compare_result <= 0x80000000", Operand1);
                        break;
                    }

                case "ja":
                    {
                        WriteJumpCode(w, "compare_result != 0 && compare_result <= 0x80000000", Operand1);
                        break;
                    }

                case "jne":
                    {
                        WriteJumpCode(w, "compare_result != 0", Operand1);
                        break;
                    }

                case "jmp":
                    {
                        WriteJumpCode(w, "true", Operand1);
                        break;
                    }

                case "lea":
                    {
                        w.WriteLine($"{Operand2.GetValueAccessCode()} = {Operand1.GetCode()};");
                        break;
                    }

                case "div":
                    {
                        //w.WriteLine("ulong target = ((ulong)edx << 32) + (ulong)eax;");
                        //w.WriteLine($"eax = (uint)(target / {Operand1.GetValueAccessCode()});");
                        //w.WriteLine($"edx = (uint)(target % {Operand1.GetValueAccessCode()});");

                        w.WriteLine("uint tmp1 =  (uint)(((ulong)edx << 32) + (ulong)eax);");
                        w.WriteLine($"uint tmp2 = {Operand1.GetValueAccessCode()};");
                        w.WriteLine("eax = (tmp1 / tmp2);");
                        w.WriteLine("edx = (tmp1 % tmp2);");
                        break;
                    }

                case "add":
                    {
                        //if (Address == 0x8048874) break;
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
                        break;
                    }

                case "sub":
                    {
                        w.WriteLine($"{Operand2.GetValueAccessCode()} -= {Operand1.GetValueAccessCode()};");
                        w.WriteLine($"compare_result = {Operand2.GetValueAccessCode()};");
                        break;
                    }

                case "cmp":
                    {
                        w.WriteLine($"compare_result = (uint)({Operand2.GetValueAccessCode()} - {Operand1.GetValueAccessCode()});");
                        break;
                    }

                case "xchg":
                    {
                        break;
                    }

                case "nop":
                    {
                        break;
                    }

                case "call":
                    {
                        if (ToString().Contains("__stack_chk_fail")) { }
                        else
                        {
                            throw new NotImplementedException();
                        }
                        break;
                    }

                default:
                    throw new ApplicationException("Invalid operation: " + ToString());
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
        string[] Lines;
        public Dictionary<string, uint> FunctionTable;
        SortedDictionary<uint, VCodeOperation> OperationLines = new SortedDictionary<uint, VCodeOperation>();
        public HashSet<uint> CodeLabels = new HashSet<uint>();
        public HashSet<uint> LabelRefs = new HashSet<uint>();

        public override string ToString() => Out.ToString();

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
        }

        void WriteFunctionTable()
        {
            Out.WriteLine("public enum FunctionTable");
            Out.WriteLine("{");
            foreach (string functionName in this.FunctionTable.Keys)
            {
                Out.WriteLine($"    {functionName} = 0x{FunctionTable[functionName]:x},");
            }
            Out.WriteLine("}");
            Out.WriteLine("");
        }

        void WriteMainFunction()
        {
            Out.WriteLine("public static void Iam_The_IntelCPU_HaHaHa(VCpuState state, uint ip)");
            Out.WriteLine("{");

            Out.WriteLine("uint eax = state.Eax; ref ushort al = ref *((ushort*)(&eax) + 0); ref ushort ah = ref *((ushort*)(&eax) + 1);");
            Out.WriteLine("uint ebx = state.Ebx; ref ushort bl = ref *((ushort*)(&ebx) + 0); ref ushort bh = ref *((ushort*)(&ebx) + 1);");
            Out.WriteLine("uint ecx = state.Ecx; ref ushort cl = ref *((ushort*)(&ecx) + 0); ref ushort ch = ref *((ushort*)(&ecx) + 1);");
            Out.WriteLine("uint edx = state.Edx; ref ushort dl = ref *((ushort*)(&edx) + 0); ref ushort dh = ref *((ushort*)(&edx) + 1);");
            Out.WriteLine("uint esi = state.Esi; ");
            Out.WriteLine("uint edi = state.Edi; ");
            Out.WriteLine("uint ebp = state.Ebp; ");
            Out.WriteLine("uint esp = state.Esp; ");
            Out.WriteLine("const uint eiz = 0; ");
            Out.WriteLine("string exception_string = null;");
            Out.WriteLine("uint exception_address = 0;");
            Out.WriteLine("uint compare_result = 0;");
            Out.WriteLine("uint cache_last_page1 = 0xffffffff;");
            Out.WriteLine("uint last_used_cache = 0;");
            Out.WriteLine("byte *cache_last_realaddr1 = null;");
            Out.WriteLine("uint cache_last_page2 = 0xffffffff;");
            Out.WriteLine("byte *cache_last_realaddr2 = null;");

            Out.WriteLine("VMemory Memory = state.Memory;");
            Out.WriteLine("VPageTableEntry* pte = Memory.PageTableEntry;");
            Out.WriteLine("uint next_ip = ip;");

            Out.WriteLine();

            Out.WriteLine("L_START:");
            Out.WriteLine("switch (next_ip)");
            Out.WriteLine("{");

            foreach (VCodeOperation op in OperationLines.Values)
            {
                if (LabelRefs.Contains(op.Address))
                {
                    Out.Write($"case 0x{op.Address:x}: ");
                    Out.WriteLine($"goto L_{op.Address:x};");
                }
            }

            Out.Write($"case 0x{VConsts.Magic_Return:x}: ");
            Out.WriteLine("goto L_RETURN;");

            Out.WriteLine("default:");
            Out.WriteLine($"    exception_string = \"Invalid jump target.\";");
            Out.WriteLine("    exception_address = next_ip;");
            Out.WriteLine("    goto L_RETURN;");

            Out.WriteLine("}");

            Out.WriteLine();

            foreach (VCodeOperation op in OperationLines.Values)
            {
                if (FunctionTable.Values.Contains(op.Address))
                {
                    Out.WriteLine($"// function {FunctionTable.Where(x => x.Value == op.Address).Select(x => x.Key).Single()}();");
                }

                Out.WriteLine($"// {op.ToString()}");
                op.WriteCode(Out);
                Out.WriteLine();
            }

            Out.WriteLine(" // Restore CPU state");
            Out.WriteLine("L_RETURN:");
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

            Out.WriteLine("}");
        }
        
        public void DoMain()
        {
            Out = new StringWriter();

            Out.WriteLine("// Auto generated by IPA Box Test");
            Out.WriteLine("using System;");
            Out.WriteLine("using SoftEther.WebSocket.Helper;");
            Out.WriteLine();
            Out.WriteLine("#pragma warning disable CS0164, CS0219, CS1717, CS0162");
            Out.WriteLine();
            Out.WriteLine("public static unsafe class VCode");
            Out.WriteLine("{");

            WriteMainFunction();

            Out.WriteLine();

            WriteFunctionTable();

            Out.WriteLine("}");
            Out.WriteLine();
        }
    }

    static class VCpuCodeGenTest
    {
        public static void CodeGen()
        {
            Console.WriteLine("IPA Box CodeGen Test");
            Console.WriteLine();

            string srcAsm = @"C:\git\DNT-Jikken\MVPNClientTest\NativeCodeTest\bin\test_exec.asm";
            string dstCs = @"C:\git\DNT-Jikken\MVPNClientTest\VCpuTest\GeneratedCode.cs";

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
        }
    }
}


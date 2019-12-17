using Ionic.Crc;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Ionic.BZip2;
using Ionic.Zlib;

namespace Ionic.BZip2
{
    // Token: 0x02000042 RID: 66
    internal class BitWriter
    {
        // Token: 0x06000338 RID: 824 RVA: 0x00015A24 File Offset: 0x00013C24
        public BitWriter(Stream s)
        {
            this.output = s;
        }

        /// <summary>
        ///   Delivers the remaining bits, left-aligned, in a byte.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     This is valid only if NumRemainingBits is less than 8;
        ///     in other words it is valid only after a call to Flush().
        ///   </para>
        /// </remarks>
        // Token: 0x170000DF RID: 223
        // (get) Token: 0x06000339 RID: 825 RVA: 0x00015A38 File Offset: 0x00013C38
        public byte RemainingBits
        {
            get
            {
                return (byte)(this.accumulator >> 32 - this.nAccumulatedBits & 255u);
            }
        }

        // Token: 0x170000E0 RID: 224
        // (get) Token: 0x0600033A RID: 826 RVA: 0x00015A64 File Offset: 0x00013C64
        public int NumRemainingBits
        {
            get
            {
                return this.nAccumulatedBits;
            }
        }

        // Token: 0x170000E1 RID: 225
        // (get) Token: 0x0600033B RID: 827 RVA: 0x00015A7C File Offset: 0x00013C7C
        public int TotalBytesWrittenOut
        {
            get
            {
                return this.totalBytesWrittenOut;
            }
        }

        /// <summary>
        ///   Reset the BitWriter.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     This is useful when the BitWriter writes into a MemoryStream, and
        ///     is used by a BZip2Compressor, which itself is re-used for multiple
        ///     distinct data blocks.
        ///   </para>
        /// </remarks>
        // Token: 0x0600033C RID: 828 RVA: 0x00015A94 File Offset: 0x00013C94
        public void Reset()
        {
            this.accumulator = 0u;
            this.nAccumulatedBits = 0;
            this.totalBytesWrittenOut = 0;
            this.output.Seek(0L, SeekOrigin.Begin);
            this.output.SetLength(0L);
        }

        /// <summary>
        ///   Write some number of bits from the given value, into the output.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     The nbits value should be a max of 25, for safety. For performance
        ///     reasons, this method does not check!
        ///   </para>
        /// </remarks>
        // Token: 0x0600033D RID: 829 RVA: 0x00015ACC File Offset: 0x00013CCC
        public void WriteBits(int nbits, uint value)
        {
            int nAccumulated = this.nAccumulatedBits;
            uint u = this.accumulator;
            while (nAccumulated >= 8)
            {
                this.output.WriteByte((byte)(u >> 24 & 255u));
                this.totalBytesWrittenOut++;
                u <<= 8;
                nAccumulated -= 8;
            }
            this.accumulator = (u | value << 32 - nAccumulated - nbits);
            this.nAccumulatedBits = nAccumulated + nbits;
        }

        /// <summary>
        ///   Write a full 8-bit byte into the output.
        /// </summary>
        // Token: 0x0600033E RID: 830 RVA: 0x00015B40 File Offset: 0x00013D40
        public void WriteByte(byte b)
        {
            this.WriteBits(8, (uint)b);
        }

        /// <summary>
        ///   Write four 8-bit bytes into the output.
        /// </summary>
        // Token: 0x0600033F RID: 831 RVA: 0x00015B4C File Offset: 0x00013D4C
        public void WriteInt(uint u)
        {
            this.WriteBits(8, u >> 24 & 255u);
            this.WriteBits(8, u >> 16 & 255u);
            this.WriteBits(8, u >> 8 & 255u);
            this.WriteBits(8, u & 255u);
        }

        /// <summary>
        ///   Write all available byte-aligned bytes.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     This method writes no new output, but flushes any accumulated
        ///     bits. At completion, the accumulator may contain up to 7
        ///     bits.
        ///   </para>
        ///   <para>
        ///     This is necessary when re-assembling output from N independent
        ///     compressors, one for each of N blocks. The output of any
        ///     particular compressor will in general have some fragment of a byte
        ///     remaining. This fragment needs to be accumulated into the
        ///     parent BZip2OutputStream.
        ///   </para>
        /// </remarks>
        // Token: 0x06000340 RID: 832 RVA: 0x00015B9E File Offset: 0x00013D9E
        public void Flush()
        {
            this.WriteBits(0, 0u);
        }

        /// <summary>
        ///   Writes all available bytes, and emits padding for the final byte as
        ///   necessary. This must be the last method invoked on an instance of
        ///   BitWriter.
        /// </summary>
        // Token: 0x06000341 RID: 833 RVA: 0x00015BAC File Offset: 0x00013DAC
        public void FinishAndPad()
        {
            this.Flush();
            if (this.NumRemainingBits > 0)
            {
                byte b = (byte)(this.accumulator >> 24 & 255u);
                this.output.WriteByte(b);
                this.totalBytesWrittenOut++;
            }
        }

        // Token: 0x040001A9 RID: 425
        private uint accumulator;

        // Token: 0x040001AA RID: 426
        private int nAccumulatedBits;

        // Token: 0x040001AB RID: 427
        private Stream output;

        // Token: 0x040001AC RID: 428
        private int totalBytesWrittenOut;
    }
}

namespace Ionic.BZip2
{
    // Token: 0x02000048 RID: 72
    internal static class BZip2
    {
        // Token: 0x0600038D RID: 909 RVA: 0x00019FD8 File Offset: 0x000181D8
        internal static T[][] InitRectangularArray<T>(int d1, int d2)
        {
            T[][] x = new T[d1][];
            for (int i = 0; i < d1; i++)
            {
                x[i] = new T[d2];
            }
            return x;
        }

        // Token: 0x04000216 RID: 534
        public static readonly int BlockSizeMultiple = 100000;

        // Token: 0x04000217 RID: 535
        public static readonly int MinBlockSize = 1;

        // Token: 0x04000218 RID: 536
        public static readonly int MaxBlockSize = 9;

        // Token: 0x04000219 RID: 537
        public static readonly int MaxAlphaSize = 258;

        // Token: 0x0400021A RID: 538
        public static readonly int MaxCodeLength = 23;

        // Token: 0x0400021B RID: 539
        public static readonly char RUNA = '\0';

        // Token: 0x0400021C RID: 540
        public static readonly char RUNB = '\u0001';

        // Token: 0x0400021D RID: 541
        public static readonly int NGroups = 6;

        // Token: 0x0400021E RID: 542
        public static readonly int G_SIZE = 50;

        // Token: 0x0400021F RID: 543
        public static readonly int N_ITERS = 4;

        // Token: 0x04000220 RID: 544
        public static readonly int MaxSelectors = 2 + 900000 / BZip2.G_SIZE;

        // Token: 0x04000221 RID: 545
        public static readonly int NUM_OVERSHOOT_BYTES = 20;

        // Token: 0x04000222 RID: 546
        internal static readonly int QSORT_STACK_SIZE = 1000;
    }
}

namespace Ionic.BZip2
{
    // Token: 0x02000043 RID: 67
    internal class BZip2Compressor
    {
        /// <summary>
        ///   BZip2Compressor writes its compressed data out via a BitWriter. This
        ///   is necessary because BZip2 does byte shredding.
        /// </summary>
        // Token: 0x06000342 RID: 834 RVA: 0x00015BFF File Offset: 0x00013DFF
        public BZip2Compressor(BitWriter writer) : this(writer, BZip2.MaxBlockSize)
        {
        }

        // Token: 0x06000343 RID: 835 RVA: 0x00015C10 File Offset: 0x00013E10
        public BZip2Compressor(BitWriter writer, int blockSize)
        {
            this.blockSize100k = blockSize;
            this.bw = writer;
            this.outBlockFillThreshold = blockSize * BZip2.BlockSizeMultiple - 20;
            this.cstate = new BZip2Compressor.CompressionState(blockSize);
            this.Reset();
        }

        // Token: 0x06000344 RID: 836 RVA: 0x00015C74 File Offset: 0x00013E74
        private void Reset()
        {
            this.crc.Reset();
            this.currentByte = -1;
            this.runLength = 0;
            this.last = -1;
            int i = 256;
            while (--i >= 0)
            {
                this.cstate.inUse[i] = false;
            }
        }

        // Token: 0x170000E2 RID: 226
        // (get) Token: 0x06000345 RID: 837 RVA: 0x00015CC8 File Offset: 0x00013EC8
        public int BlockSize
        {
            get
            {
                return this.blockSize100k;
            }
        }

        // Token: 0x170000E3 RID: 227
        // (get) Token: 0x06000346 RID: 838 RVA: 0x00015CE0 File Offset: 0x00013EE0
        // (set) Token: 0x06000347 RID: 839 RVA: 0x00015CF7 File Offset: 0x00013EF7
        public uint Crc32 { get; private set; }

        // Token: 0x170000E4 RID: 228
        // (get) Token: 0x06000348 RID: 840 RVA: 0x00015D00 File Offset: 0x00013F00
        // (set) Token: 0x06000349 RID: 841 RVA: 0x00015D17 File Offset: 0x00013F17
        public int AvailableBytesOut { get; private set; }

        /// <summary>
        ///   The number of uncompressed bytes being held in the buffer.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     I am thinking this may be useful in a Stream that uses this
        ///     compressor class. In the Close() method on the stream it could
        ///     check this value to see if anything has been written at all.  You
        ///     may think the stream could easily track the number of bytes it
        ///     wrote, which would eliminate the need for this. But, there is the
        ///     case where the stream writes a complete block, and it is full, and
        ///     then writes no more. In that case the stream may want to check.
        ///   </para>
        /// </remarks>
        // Token: 0x170000E5 RID: 229
        // (get) Token: 0x0600034A RID: 842 RVA: 0x00015D20 File Offset: 0x00013F20
        public int UncompressedBytes
        {
            get
            {
                return this.last + 1;
            }
        }

        /// <summary>
        ///   Accept new bytes into the compressor data buffer
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     This method does the first-level (cheap) run-length encoding, and
        ///     stores the encoded data into the rle block.
        ///   </para>
        /// </remarks>
        // Token: 0x0600034B RID: 843 RVA: 0x00015D3C File Offset: 0x00013F3C
        public int Fill(byte[] buffer, int offset, int count)
        {
            int result;
            if (this.last >= this.outBlockFillThreshold)
            {
                result = 0;
            }
            else
            {
                int bytesWritten = 0;
                int limit = offset + count;
                int rc;
                do
                {
                    rc = this.write0(buffer[offset++]);
                    if (rc > 0)
                    {
                        bytesWritten++;
                    }
                }
                while (offset < limit && rc == 1);
                result = bytesWritten;
            }
            return result;
        }

        /// <summary>
        ///   Process one input byte into the block.
        /// </summary>
        ///
        /// <remarks>
        ///   <para>
        ///     To "process" the byte means to do the run-length encoding.
        ///     There are 3 possible return values:
        ///
        ///        0 - the byte was not written, in other words, not
        ///            encoded into the block. This happens when the
        ///            byte b would require the start of a new run, and
        ///            the block has no more room for new runs.
        ///
        ///        1 - the byte was written, and the block is not full.
        ///
        ///        2 - the byte was written, and the block is full.
        ///
        ///   </para>
        /// </remarks>
        /// <returns>0 if the byte was not written, non-zero if written.</returns>
        // Token: 0x0600034C RID: 844 RVA: 0x00015DA0 File Offset: 0x00013FA0
        private int write0(byte b)
        {
            int result;
            if (this.currentByte == -1)
            {
                this.currentByte = (int)b;
                this.runLength++;
                result = 1;
            }
            else if (this.currentByte == (int)b)
            {
                if (++this.runLength > 254)
                {
                    bool rc = this.AddRunToOutputBlock(false);
                    this.currentByte = -1;
                    this.runLength = 0;
                    result = (rc ? 2 : 1);
                }
                else
                {
                    result = 1;
                }
            }
            else
            {
                bool rc = this.AddRunToOutputBlock(false);
                if (rc)
                {
                    this.currentByte = -1;
                    this.runLength = 0;
                    result = 0;
                }
                else
                {
                    this.runLength = 1;
                    this.currentByte = (int)b;
                    result = 1;
                }
            }
            return result;
        }

        /// <summary>
        ///   Append one run to the output block.
        /// </summary>
        ///
        /// <remarks>
        ///   <para>
        ///     This compressor does run-length-encoding before BWT and etc. This
        ///     method simply appends a run to the output block. The append always
        ///     succeeds. The return value indicates whether the block is full:
        ///     false (not full) implies that at least one additional run could be
        ///     processed.
        ///   </para>
        /// </remarks>
        /// <returns>true if the block is now full; otherwise false.</returns>
        // Token: 0x0600034D RID: 845 RVA: 0x00015E68 File Offset: 0x00014068
        private bool AddRunToOutputBlock(bool final)
        {
            this.runs++;
            int previousLast = this.last;
            if (previousLast >= this.outBlockFillThreshold && !final)
            {
                string msg = string.Format("block overrun(final={2}): {0} >= threshold ({1})", previousLast, this.outBlockFillThreshold, final);
                throw new Exception(msg);
            }
            byte b = (byte)this.currentByte;
            byte[] block = this.cstate.block;
            this.cstate.inUse[(int)b] = true;
            int rl = this.runLength;
            this.crc.UpdateCRC(b, rl);
            switch (rl)
            {
                case 1:
                    block[previousLast + 2] = b;
                    this.last = previousLast + 1;
                    break;
                case 2:
                    block[previousLast + 2] = b;
                    block[previousLast + 3] = b;
                    this.last = previousLast + 2;
                    break;
                case 3:
                    block[previousLast + 2] = b;
                    block[previousLast + 3] = b;
                    block[previousLast + 4] = b;
                    this.last = previousLast + 3;
                    break;
                default:
                    rl -= 4;
                    this.cstate.inUse[rl] = true;
                    block[previousLast + 2] = b;
                    block[previousLast + 3] = b;
                    block[previousLast + 4] = b;
                    block[previousLast + 5] = b;
                    block[previousLast + 6] = (byte)rl;
                    this.last = previousLast + 5;
                    break;
            }
            return this.last >= this.outBlockFillThreshold;
        }

        /// <summary>
        ///   Compress the data that has been placed (Run-length-encoded) into the
        ///   block. The compressed data goes into the CompressedBytes array.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Side effects: 1.  fills the CompressedBytes array.  2. sets the
        ///     AvailableBytesOut property.
        ///   </para>
        /// </remarks>
        // Token: 0x0600034E RID: 846 RVA: 0x00015FB8 File Offset: 0x000141B8
        public void CompressAndWrite()
        {
            if (this.runLength > 0)
            {
                this.AddRunToOutputBlock(true);
            }
            this.currentByte = -1;
            if (this.last != -1)
            {
                this.blockSort();
                this.bw.WriteByte(49);
                this.bw.WriteByte(65);
                this.bw.WriteByte(89);
                this.bw.WriteByte(38);
                this.bw.WriteByte(83);
                this.bw.WriteByte(89);
                this.Crc32 = (uint)this.crc.Crc32Result;
                this.bw.WriteInt(this.Crc32);
                this.bw.WriteBits(1, this.blockRandomised ? 1u : 0u);
                this.moveToFrontCodeAndSend();
                this.Reset();
            }
        }

        // Token: 0x0600034F RID: 847 RVA: 0x000160A0 File Offset: 0x000142A0
        private void randomiseBlock()
        {
            bool[] inUse = this.cstate.inUse;
            byte[] block = this.cstate.block;
            int lastShadow = this.last;
            int i = 256;
            while (--i >= 0)
            {
                inUse[i] = false;
            }
            int rNToGo = 0;
            int rTPos = 0;
            i = 0;
            int j = 1;
            while (i <= lastShadow)
            {
                if (rNToGo == 0)
                {
                    rNToGo = (int)((ushort)Rand.Rnums(rTPos));
                    if (++rTPos == 512)
                    {
                        rTPos = 0;
                    }
                }
                rNToGo--;
                byte[] array = block;
                int num = j;
                array[num] ^= (byte)(((rNToGo == 1) ? 1 : 0));
                inUse[(int)(block[j] & byte.MaxValue)] = true;
                i = j;
                j++;
            }
            this.blockRandomised = true;
        }

        // Token: 0x06000350 RID: 848 RVA: 0x00016180 File Offset: 0x00014380
        private void mainSort()
        {
            BZip2Compressor.CompressionState dataShadow = this.cstate;
            int[] runningOrder = dataShadow.mainSort_runningOrder;
            int[] copy = dataShadow.mainSort_copy;
            bool[] bigDone = dataShadow.mainSort_bigDone;
            int[] ftab = dataShadow.ftab;
            byte[] block = dataShadow.block;
            int[] fmap = dataShadow.fmap;
            char[] quadrant = dataShadow.quadrant;
            int lastShadow = this.last;
            int workLimitShadow = this.workLimit;
            bool firstAttemptShadow = this.firstAttempt;
            int i = 65537;
            while (--i >= 0)
            {
                ftab[i] = 0;
            }
            for (i = 0; i < BZip2.NUM_OVERSHOOT_BYTES; i++)
            {
                block[lastShadow + i + 2] = block[i % (lastShadow + 1) + 1];
            }
            i = lastShadow + BZip2.NUM_OVERSHOOT_BYTES + 1;
            while (--i >= 0)
            {
                quadrant[i] = '\0';
            }
            block[0] = block[lastShadow + 1];
            int c = (int)(block[0] & byte.MaxValue);
            for (i = 0; i <= lastShadow; i++)
            {
                int c2 = (int)(block[i + 1] & byte.MaxValue);
                ftab[(c << 8) + c2]++;
                c = c2;
            }
            for (i = 1; i <= 65536; i++)
            {
                ftab[i] += ftab[i - 1];
            }
            c = (int)(block[1] & byte.MaxValue);
            for (i = 0; i < lastShadow; i++)
            {
                int c2 = (int)(block[i + 2] & byte.MaxValue);
                fmap[--ftab[(c << 8) + c2]] = i;
                c = c2;
            }
            fmap[--ftab[((int)(block[lastShadow + 1] & byte.MaxValue) << 8) + (int)(block[1] & byte.MaxValue)]] = lastShadow;
            i = 256;
            while (--i >= 0)
            {
                bigDone[i] = false;
                runningOrder[i] = i;
            }
            int h = 364;
            while (h != 1)
            {
                h /= 3;
                for (i = h; i <= 255; i++)
                {
                    int vv = runningOrder[i];
                    int a = ftab[vv + 1 << 8] - ftab[vv << 8];
                    int b = h - 1;
                    int j = i;
                    int ro = runningOrder[j - h];
                    while (ftab[ro + 1 << 8] - ftab[ro << 8] > a)
                    {
                        runningOrder[j] = ro;
                        j -= h;
                        if (j <= b)
                        {
                            break;
                        }
                        ro = runningOrder[j - h];
                    }
                    runningOrder[j] = vv;
                }
            }
            for (i = 0; i <= 255; i++)
            {
                int ss = runningOrder[i];
                int j;
                for (j = 0; j <= 255; j++)
                {
                    int sb = (ss << 8) + j;
                    int ftab_sb = ftab[sb];
                    if ((ftab_sb & BZip2Compressor.SETMASK) != BZip2Compressor.SETMASK)
                    {
                        int lo = ftab_sb & BZip2Compressor.CLEARMASK;
                        int hi = (ftab[sb + 1] & BZip2Compressor.CLEARMASK) - 1;
                        if (hi > lo)
                        {
                            this.mainQSort3(dataShadow, lo, hi, 2);
                            if (firstAttemptShadow && this.workDone > workLimitShadow)
                            {
                                return;
                            }
                        }
                        ftab[sb] = (ftab_sb | BZip2Compressor.SETMASK);
                    }
                }
                for (j = 0; j <= 255; j++)
                {
                    copy[j] = (ftab[(j << 8) + ss] & BZip2Compressor.CLEARMASK);
                }
                j = (ftab[ss << 8] & BZip2Compressor.CLEARMASK);
                int hj = ftab[ss + 1 << 8] & BZip2Compressor.CLEARMASK;
                while (j < hj)
                {
                    int fmap_j = fmap[j];
                    c = (int)(block[fmap_j] & byte.MaxValue);
                    if (!bigDone[c])
                    {
                        fmap[copy[c]] = ((fmap_j == 0) ? lastShadow : (fmap_j - 1));
                        copy[c]++;
                    }
                    j++;
                }
                j = 256;
                while (--j >= 0)
                {
                    ftab[(j << 8) + ss] |= BZip2Compressor.SETMASK;
                }
                bigDone[ss] = true;
                if (i < 255)
                {
                    int bbStart = ftab[ss << 8] & BZip2Compressor.CLEARMASK;
                    int bbSize = (ftab[ss + 1 << 8] & BZip2Compressor.CLEARMASK) - bbStart;
                    int shifts = 0;
                    while (bbSize >> shifts > 65534)
                    {
                        shifts++;
                    }
                    for (j = 0; j < bbSize; j++)
                    {
                        int a2update = fmap[bbStart + j];
                        char qVal = (char)(j >> shifts);
                        quadrant[a2update] = qVal;
                        if (a2update < BZip2.NUM_OVERSHOOT_BYTES)
                        {
                            quadrant[a2update + lastShadow + 1] = qVal;
                        }
                    }
                }
            }
        }

        // Token: 0x06000351 RID: 849 RVA: 0x000166F8 File Offset: 0x000148F8
        private void blockSort()
        {
            this.workLimit = BZip2Compressor.WORK_FACTOR * this.last;
            this.workDone = 0;
            this.blockRandomised = false;
            this.firstAttempt = true;
            this.mainSort();
            if (this.firstAttempt && this.workDone > this.workLimit)
            {
                this.randomiseBlock();
                this.workLimit = (this.workDone = 0);
                this.firstAttempt = false;
                this.mainSort();
            }
            int[] fmap = this.cstate.fmap;
            this.origPtr = -1;
            int i = 0;
            int lastShadow = this.last;
            while (i <= lastShadow)
            {
                if (fmap[i] == 0)
                {
                    this.origPtr = i;
                    break;
                }
                i++;
            }
        }

        /// This is the most hammered method of this class.
        ///
        /// <p>
        /// This is the version using unrolled loops.
        /// </p>
        // Token: 0x06000352 RID: 850 RVA: 0x000167C4 File Offset: 0x000149C4
        private bool mainSimpleSort(BZip2Compressor.CompressionState dataShadow, int lo, int hi, int d)
        {
            int bigN = hi - lo + 1;
            bool result;
            if (bigN < 2)
            {
                result = (this.firstAttempt && this.workDone > this.workLimit);
            }
            else
            {
                int hp = 0;
                while (BZip2Compressor.increments[hp] < bigN)
                {
                    hp++;
                }
                int[] fmap = dataShadow.fmap;
                char[] quadrant = dataShadow.quadrant;
                byte[] block = dataShadow.block;
                int lastShadow = this.last;
                int lastPlus = lastShadow + 1;
                bool firstAttemptShadow = this.firstAttempt;
                int workLimitShadow = this.workLimit;
                int workDoneShadow = this.workDone;
                while (--hp >= 0)
                {
                    int h = BZip2Compressor.increments[hp];
                    int mj = lo + h - 1;
                    int i = lo + h;
                    while (i <= hi)
                    {
                        int j = 3;
                        while (i <= hi && --j >= 0)
                        {
                            int v = fmap[i];
                            int vd = v + d;
                            int k = i;
                            bool onceRunned = false;
                            int a = 0;
                            for (; ; )
                            {
                                IL_CC:
                                if (onceRunned)
                                {
                                    fmap[k] = a;
                                    if ((k -= h) <= mj)
                                    {
                                        break;
                                    }
                                }
                                else
                                {
                                    onceRunned = true;
                                }
                                a = fmap[k - h];
                                int i2 = a + d;
                                int i3 = vd;
                                if (block[i2 + 1] == block[i3 + 1])
                                {
                                    if (block[i2 + 2] == block[i3 + 2])
                                    {
                                        if (block[i2 + 3] == block[i3 + 3])
                                        {
                                            if (block[i2 + 4] == block[i3 + 4])
                                            {
                                                if (block[i2 + 5] == block[i3 + 5])
                                                {
                                                    if (block[i2 += 6] == block[i3 += 6])
                                                    {
                                                        int x = lastShadow;
                                                        while (x > 0)
                                                        {
                                                            x -= 4;
                                                            if (block[i2 + 1] == block[i3 + 1])
                                                            {
                                                                if (quadrant[i2] == quadrant[i3])
                                                                {
                                                                    if (block[i2 + 2] == block[i3 + 2])
                                                                    {
                                                                        if (quadrant[i2 + 1] == quadrant[i3 + 1])
                                                                        {
                                                                            if (block[i2 + 3] == block[i3 + 3])
                                                                            {
                                                                                if (quadrant[i2 + 2] == quadrant[i3 + 2])
                                                                                {
                                                                                    if (block[i2 + 4] == block[i3 + 4])
                                                                                    {
                                                                                        if (quadrant[i2 + 3] == quadrant[i3 + 3])
                                                                                        {
                                                                                            if ((i2 += 4) >= lastPlus)
                                                                                            {
                                                                                                i2 -= lastPlus;
                                                                                            }
                                                                                            if ((i3 += 4) >= lastPlus)
                                                                                            {
                                                                                                i3 -= lastPlus;
                                                                                            }
                                                                                            workDoneShadow++;
                                                                                        }
                                                                                        else
                                                                                        {
                                                                                            if (quadrant[i2 + 3] > quadrant[i3 + 3])
                                                                                            {
                                                                                                goto IL_CC;
                                                                                            }
                                                                                            break;
                                                                                        }
                                                                                    }
                                                                                    else
                                                                                    {
                                                                                        if ((block[i2 + 4] & 255) > (block[i3 + 4] & 255))
                                                                                        {
                                                                                            goto IL_CC;
                                                                                        }
                                                                                        break;
                                                                                    }
                                                                                }
                                                                                else
                                                                                {
                                                                                    if (quadrant[i2 + 2] > quadrant[i3 + 2])
                                                                                    {
                                                                                        goto IL_CC;
                                                                                    }
                                                                                    break;
                                                                                }
                                                                            }
                                                                            else
                                                                            {
                                                                                if ((block[i2 + 3] & 255) > (block[i3 + 3] & 255))
                                                                                {
                                                                                    goto IL_CC;
                                                                                }
                                                                                break;
                                                                            }
                                                                        }
                                                                        else
                                                                        {
                                                                            if (quadrant[i2 + 1] > quadrant[i3 + 1])
                                                                            {
                                                                                goto IL_CC;
                                                                            }
                                                                            break;
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        if ((block[i2 + 2] & 255) > (block[i3 + 2] & 255))
                                                                        {
                                                                            goto IL_CC;
                                                                        }
                                                                        break;
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    if (quadrant[i2] > quadrant[i3])
                                                                    {
                                                                        goto IL_CC;
                                                                    }
                                                                    break;
                                                                }
                                                            }
                                                            else
                                                            {
                                                                if ((block[i2 + 1] & 255) > (block[i3 + 1] & 255))
                                                                {
                                                                    goto IL_CC;
                                                                }
                                                                break;
                                                            }
                                                        }
                                                        break;
                                                    }
                                                    if ((block[i2] & 255) <= (block[i3] & 255))
                                                    {
                                                        break;
                                                    }
                                                }
                                                else if ((block[i2 + 5] & 255) <= (block[i3 + 5] & 255))
                                                {
                                                    break;
                                                }
                                            }
                                            else if ((block[i2 + 4] & 255) <= (block[i3 + 4] & 255))
                                            {
                                                break;
                                            }
                                        }
                                        else if ((block[i2 + 3] & 255) <= (block[i3 + 3] & 255))
                                        {
                                            break;
                                        }
                                    }
                                    else if ((block[i2 + 2] & 255) <= (block[i3 + 2] & 255))
                                    {
                                        break;
                                    }
                                }
                                else if ((block[i2 + 1] & 255) <= (block[i3 + 1] & 255))
                                {
                                    break;
                                }
                            }
                            IL_572:
                            fmap[k] = v;
                            i++;
                            continue;
                            goto IL_572;
                        }
                        if (firstAttemptShadow && i <= hi && workDoneShadow > workLimitShadow)
                        {
                            goto IL_5E2;
                        }
                    }
                }
                IL_5E2:
                this.workDone = workDoneShadow;
                result = (firstAttemptShadow && workDoneShadow > workLimitShadow);
            }
            return result;
        }

        // Token: 0x06000353 RID: 851 RVA: 0x00016DD0 File Offset: 0x00014FD0
        private static void vswap(int[] fmap, int p1, int p2, int n)
        {
            n += p1;
            while (p1 < n)
            {
                int t = fmap[p1];
                fmap[p1++] = fmap[p2];
                fmap[p2++] = t;
            }
        }

        // Token: 0x06000354 RID: 852 RVA: 0x00016E08 File Offset: 0x00015008
        private static byte med3(byte a, byte b, byte c)
        {
            return (a < b) ? ((b < c) ? b : ((a < c) ? c : a)) : ((b > c) ? b : ((a > c) ? c : a));
        }

        /// Method "mainQSort3", file "blocksort.c", BZip2 1.0.2
        // Token: 0x06000355 RID: 853 RVA: 0x00016E40 File Offset: 0x00015040
        private void mainQSort3(BZip2Compressor.CompressionState dataShadow, int loSt, int hiSt, int dSt)
        {
            int[] stack_ll = dataShadow.stack_ll;
            int[] stack_hh = dataShadow.stack_hh;
            int[] stack_dd = dataShadow.stack_dd;
            int[] fmap = dataShadow.fmap;
            byte[] block = dataShadow.block;
            stack_ll[0] = loSt;
            stack_hh[0] = hiSt;
            stack_dd[0] = dSt;
            int sp = 1;
            while (--sp >= 0)
            {
                int lo = stack_ll[sp];
                int hi = stack_hh[sp];
                int d = stack_dd[sp];
                if (hi - lo < BZip2Compressor.SMALL_THRESH || d > BZip2Compressor.DEPTH_THRESH)
                {
                    if (this.mainSimpleSort(dataShadow, lo, hi, d))
                    {
                        break;
                    }
                }
                else
                {
                    int d2 = d + 1;
                    int med = (int)(BZip2Compressor.med3(block[fmap[lo] + d2], block[fmap[hi] + d2], block[fmap[lo + hi >> 1] + d2]) & byte.MaxValue);
                    int unLo = lo;
                    int unHi = hi;
                    int ltLo = lo;
                    int gtHi = hi;
                    for (; ; )
                    {
                        int temp;
                        while (unLo <= unHi)
                        {
                            int i = (int)(block[fmap[unLo] + d2] & byte.MaxValue) - med;
                            if (i == 0)
                            {
                                temp = fmap[unLo];
                                fmap[unLo++] = fmap[ltLo];
                                fmap[ltLo++] = temp;
                            }
                            else
                            {
                                if (i >= 0)
                                {
                                    break;
                                }
                                unLo++;
                            }
                        }
                        while (unLo <= unHi)
                        {
                            int i = (int)(block[fmap[unHi] + d2] & byte.MaxValue) - med;
                            if (i == 0)
                            {
                                temp = fmap[unHi];
                                fmap[unHi--] = fmap[gtHi];
                                fmap[gtHi--] = temp;
                            }
                            else
                            {
                                if (i <= 0)
                                {
                                    break;
                                }
                                unHi--;
                            }
                        }
                        if (unLo > unHi)
                        {
                            break;
                        }
                        temp = fmap[unLo];
                        fmap[unLo++] = fmap[unHi];
                        fmap[unHi--] = temp;
                    }
                    if (gtHi < ltLo)
                    {
                        stack_ll[sp] = lo;
                        stack_hh[sp] = hi;
                        stack_dd[sp] = d2;
                        sp++;
                    }
                    else
                    {
                        int i = (ltLo - lo < unLo - ltLo) ? (ltLo - lo) : (unLo - ltLo);
                        BZip2Compressor.vswap(fmap, lo, unLo - i, i);
                        int j = (hi - gtHi < gtHi - unHi) ? (hi - gtHi) : (gtHi - unHi);
                        BZip2Compressor.vswap(fmap, unLo, hi - j + 1, j);
                        i = lo + unLo - ltLo - 1;
                        j = hi - (gtHi - unHi) + 1;
                        stack_ll[sp] = lo;
                        stack_hh[sp] = i;
                        stack_dd[sp] = d;
                        sp++;
                        stack_ll[sp] = i + 1;
                        stack_hh[sp] = j - 1;
                        stack_dd[sp] = d2;
                        sp++;
                        stack_ll[sp] = j;
                        stack_hh[sp] = hi;
                        stack_dd[sp] = d;
                        sp++;
                    }
                }
            }
        }

        // Token: 0x06000356 RID: 854 RVA: 0x00017150 File Offset: 0x00015350
        private void generateMTFValues()
        {
            int lastShadow = this.last;
            BZip2Compressor.CompressionState dataShadow = this.cstate;
            bool[] inUse = dataShadow.inUse;
            byte[] block = dataShadow.block;
            int[] fmap = dataShadow.fmap;
            char[] sfmap = dataShadow.sfmap;
            int[] mtfFreq = dataShadow.mtfFreq;
            byte[] unseqToSeq = dataShadow.unseqToSeq;
            byte[] yy = dataShadow.generateMTFValues_yy;
            int nInUseShadow = 0;
            int i;
            for (i = 0; i < 256; i++)
            {
                if (inUse[i])
                {
                    unseqToSeq[i] = (byte)nInUseShadow;
                    nInUseShadow++;
                }
            }
            this.nInUse = nInUseShadow;
            int eob = nInUseShadow + 1;
            for (i = eob; i >= 0; i--)
            {
                mtfFreq[i] = 0;
            }
            i = nInUseShadow;
            while (--i >= 0)
            {
                yy[i] = (byte)i;
            }
            int wr = 0;
            int zPend = 0;
            for (i = 0; i <= lastShadow; i++)
            {
                byte ll_i = unseqToSeq[(int)(block[fmap[i]] & byte.MaxValue)];
                byte tmp = yy[0];
                int j = 0;
                while (ll_i != tmp)
                {
                    j++;
                    byte tmp2 = tmp;
                    tmp = yy[j];
                    yy[j] = tmp2;
                }
                yy[0] = tmp;
                if (j == 0)
                {
                    zPend++;
                }
                else
                {
                    if (zPend > 0)
                    {
                        zPend--;
                        for (; ; )
                        {
                            if ((zPend & 1) == 0)
                            {
                                sfmap[wr] = BZip2.RUNA;
                                wr++;
                                mtfFreq[(int)BZip2.RUNA]++;
                            }
                            else
                            {
                                sfmap[wr] = BZip2.RUNB;
                                wr++;
                                mtfFreq[(int)BZip2.RUNB]++;
                            }
                            if (zPend < 2)
                            {
                                break;
                            }
                            zPend = zPend - 2 >> 1;
                        }
                        zPend = 0;
                    }
                    sfmap[wr] = (char)(j + 1);
                    wr++;
                    mtfFreq[j + 1]++;
                }
            }
            if (zPend > 0)
            {
                zPend--;
                for (; ; )
                {
                    if ((zPend & 1) == 0)
                    {
                        sfmap[wr] = BZip2.RUNA;
                        wr++;
                        mtfFreq[(int)BZip2.RUNA]++;
                    }
                    else
                    {
                        sfmap[wr] = BZip2.RUNB;
                        wr++;
                        mtfFreq[(int)BZip2.RUNB]++;
                    }
                    if (zPend < 2)
                    {
                        break;
                    }
                    zPend = zPend - 2 >> 1;
                }
            }
            sfmap[wr] = (char)eob;
            mtfFreq[eob]++;
            this.nMTF = wr + 1;
        }

        // Token: 0x06000357 RID: 855 RVA: 0x00017468 File Offset: 0x00015668
        private static void hbAssignCodes(int[] code, byte[] length, int minLen, int maxLen, int alphaSize)
        {
            int vec = 0;
            for (int i = minLen; i <= maxLen; i++)
            {
                for (int j = 0; j < alphaSize; j++)
                {
                    if ((int)(length[j] & 255) == i)
                    {
                        code[j] = vec;
                        vec++;
                    }
                }
                vec <<= 1;
            }
        }

        // Token: 0x06000358 RID: 856 RVA: 0x000174C4 File Offset: 0x000156C4
        private void sendMTFValues()
        {
            byte[][] len = this.cstate.sendMTFValues_len;
            int alphaSize = this.nInUse + 2;
            int t = BZip2.NGroups;
            while (--t >= 0)
            {
                byte[] len_t = len[t];
                int v = alphaSize;
                while (--v >= 0)
                {
                    len_t[v] = BZip2Compressor.GREATER_ICOST;
                }
            }
            int nGroups = (this.nMTF < 200) ? 2 : ((this.nMTF < 600) ? 3 : ((this.nMTF < 1200) ? 4 : ((this.nMTF < 2400) ? 5 : 6)));
            this.sendMTFValues0(nGroups, alphaSize);
            int nSelectors = this.sendMTFValues1(nGroups, alphaSize);
            this.sendMTFValues2(nGroups, nSelectors);
            this.sendMTFValues3(nGroups, alphaSize);
            this.sendMTFValues4();
            this.sendMTFValues5(nGroups, nSelectors);
            this.sendMTFValues6(nGroups, alphaSize);
            this.sendMTFValues7(nSelectors);
        }

        // Token: 0x06000359 RID: 857 RVA: 0x000175BC File Offset: 0x000157BC
        private void sendMTFValues0(int nGroups, int alphaSize)
        {
            byte[][] len = this.cstate.sendMTFValues_len;
            int[] mtfFreq = this.cstate.mtfFreq;
            int remF = this.nMTF;
            int gs = 0;
            for (int nPart = nGroups; nPart > 0; nPart--)
            {
                int tFreq = remF / nPart;
                int ge = gs - 1;
                int aFreq = 0;
                int a = alphaSize - 1;
                while (aFreq < tFreq && ge < a)
                {
                    aFreq += mtfFreq[++ge];
                }
                if (ge > gs && nPart != nGroups && nPart != 1 && (nGroups - nPart & 1) != 0)
                {
                    aFreq -= mtfFreq[ge--];
                }
                byte[] len_np = len[nPart - 1];
                int v = alphaSize;
                while (--v >= 0)
                {
                    if (v >= gs && v <= ge)
                    {
                        len_np[v] = BZip2Compressor.LESSER_ICOST;
                    }
                    else
                    {
                        len_np[v] = BZip2Compressor.GREATER_ICOST;
                    }
                }
                gs = ge + 1;
                remF -= aFreq;
            }
        }

        // Token: 0x0600035A RID: 858 RVA: 0x000176D0 File Offset: 0x000158D0
        private static void hbMakeCodeLengths(byte[] len, int[] freq, BZip2Compressor.CompressionState state1, int alphaSize, int maxLen)
        {
            int[] heap = state1.heap;
            int[] weight = state1.weight;
            int[] parent = state1.parent;
            int i = alphaSize;
            while (--i >= 0)
            {
                weight[i + 1] = ((freq[i] == 0) ? 1 : freq[i]) << 8;
            }
            bool tooLong = true;
            while (tooLong)
            {
                tooLong = false;
                int nNodes = alphaSize;
                int nHeap = 0;
                heap[0] = 0;
                weight[0] = 0;
                parent[0] = -2;
                for (i = 1; i <= alphaSize; i++)
                {
                    parent[i] = -1;
                    nHeap++;
                    heap[nHeap] = i;
                    int zz = nHeap;
                    int tmp = heap[zz];
                    while (weight[tmp] < weight[heap[zz >> 1]])
                    {
                        heap[zz] = heap[zz >> 1];
                        zz >>= 1;
                    }
                    heap[zz] = tmp;
                }
                while (nHeap > 1)
                {
                    int n = heap[1];
                    heap[1] = heap[nHeap];
                    nHeap--;
                    int zz = 1;
                    int tmp = heap[1];
                    for (; ; )
                    {
                        int yy = zz << 1;
                        if (yy > nHeap)
                        {
                            break;
                        }
                        if (yy < nHeap && weight[heap[yy + 1]] < weight[heap[yy]])
                        {
                            yy++;
                        }
                        if (weight[tmp] < weight[heap[yy]])
                        {
                            break;
                        }
                        heap[zz] = heap[yy];
                        zz = yy;
                    }
                    IL_14E:
                    heap[zz] = tmp;
                    int n2 = heap[1];
                    heap[1] = heap[nHeap];
                    nHeap--;
                    zz = 1;
                    tmp = heap[1];
                    for (; ; )
                    {
                        int yy = zz << 1;
                        if (yy > nHeap)
                        {
                            break;
                        }
                        if (yy < nHeap && weight[heap[yy + 1]] < weight[heap[yy]])
                        {
                            yy++;
                        }
                        if (weight[tmp] < weight[heap[yy]])
                        {
                            break;
                        }
                        heap[zz] = heap[yy];
                        zz = yy;
                    }
                    IL_1E0:
                    heap[zz] = tmp;
                    nNodes++;
                    parent[n] = (parent[n2] = nNodes);
                    int weight_n = weight[n];
                    int weight_n2 = weight[n2];
                    weight[nNodes] = ((weight_n & -256) + (weight_n2 & -256) | 1 + (((weight_n & 255) > (weight_n2 & 255)) ? (weight_n & 255) : (weight_n2 & 255)));
                    parent[nNodes] = -1;
                    nHeap++;
                    heap[nHeap] = nNodes;
                    zz = nHeap;
                    tmp = heap[zz];
                    int weight_tmp = weight[tmp];
                    while (weight_tmp < weight[heap[zz >> 1]])
                    {
                        heap[zz] = heap[zz >> 1];
                        zz >>= 1;
                    }
                    heap[zz] = tmp;
                    continue;
                    goto IL_1E0;
                    goto IL_14E;
                }
                for (i = 1; i <= alphaSize; i++)
                {
                    int j = 0;
                    int k = i;
                    int parent_k;
                    while ((parent_k = parent[k]) >= 0)
                    {
                        k = parent_k;
                        j++;
                    }
                    len[i - 1] = (byte)j;
                    if (j > maxLen)
                    {
                        tooLong = true;
                    }
                }
                if (tooLong)
                {
                    for (i = 1; i < alphaSize; i++)
                    {
                        int j = weight[i] >> 8;
                        j = 1 + (j >> 1);
                        weight[i] = j << 8;
                    }
                }
            }
        }

        // Token: 0x0600035B RID: 859 RVA: 0x00017A1C File Offset: 0x00015C1C
        private int sendMTFValues1(int nGroups, int alphaSize)
        {
            BZip2Compressor.CompressionState dataShadow = this.cstate;
            int[][] rfreq = dataShadow.sendMTFValues_rfreq;
            int[] fave = dataShadow.sendMTFValues_fave;
            short[] cost = dataShadow.sendMTFValues_cost;
            char[] sfmap = dataShadow.sfmap;
            byte[] selector = dataShadow.selector;
            byte[][] len = dataShadow.sendMTFValues_len;
            byte[] len_0 = len[0];
            byte[] len_ = len[1];
            byte[] len_2 = len[2];
            byte[] len_3 = len[3];
            byte[] len_4 = len[4];
            byte[] len_5 = len[5];
            int nMTFShadow = this.nMTF;
            int nSelectors = 0;
            for (int iter = 0; iter < BZip2.N_ITERS; iter++)
            {
                int t = nGroups;
                while (--t >= 0)
                {
                    fave[t] = 0;
                    int[] rfreqt = rfreq[t];
                    int i = alphaSize;
                    while (--i >= 0)
                    {
                        rfreqt[i] = 0;
                    }
                }
                nSelectors = 0;
                int ge;
                for (int gs = 0; gs < this.nMTF; gs = ge + 1)
                {
                    ge = Math.Min(gs + BZip2.G_SIZE - 1, nMTFShadow - 1);
                    if (nGroups == BZip2.NGroups)
                    {
                        int[] c = new int[6];
                        for (int i = gs; i <= ge; i++)
                        {
                            int icv = (int)sfmap[i];
                            c[0] += (int)(len_0[icv] & byte.MaxValue);
                            c[1] += (int)(len_[icv] & byte.MaxValue);
                            c[2] += (int)(len_2[icv] & byte.MaxValue);
                            c[3] += (int)(len_3[icv] & byte.MaxValue);
                            c[4] += (int)(len_4[icv] & byte.MaxValue);
                            c[5] += (int)(len_5[icv] & byte.MaxValue);
                        }
                        cost[0] = (short)c[0];
                        cost[1] = (short)c[1];
                        cost[2] = (short)c[2];
                        cost[3] = (short)c[3];
                        cost[4] = (short)c[4];
                        cost[5] = (short)c[5];
                    }
                    else
                    {
                        t = nGroups;
                        while (--t >= 0)
                        {
                            cost[t] = 0;
                        }
                        for (int i = gs; i <= ge; i++)
                        {
                            int icv = (int)sfmap[i];
                            t = nGroups;
                            while (--t >= 0)
                            {
                                short[] array = cost;
                                int num = t;
                                array[num] += (short)(len[t][icv] & byte.MaxValue);
                            }
                        }
                    }
                    int bt = -1;
                    t = nGroups;
                    int bc = 999999999;
                    while (--t >= 0)
                    {
                        int cost_t = (int)cost[t];
                        if (cost_t < bc)
                        {
                            bc = cost_t;
                            bt = t;
                        }
                    }
                    fave[bt]++;
                    selector[nSelectors] = (byte)bt;
                    nSelectors++;
                    int[] rfreq_bt = rfreq[bt];
                    for (int i = gs; i <= ge; i++)
                    {
                        rfreq_bt[(int)sfmap[i]]++;
                    }
                }
                for (t = 0; t < nGroups; t++)
                {
                    BZip2Compressor.hbMakeCodeLengths(len[t], rfreq[t], this.cstate, alphaSize, 20);
                }
            }
            return nSelectors;
        }

        // Token: 0x0600035C RID: 860 RVA: 0x00017DC4 File Offset: 0x00015FC4
        private void sendMTFValues2(int nGroups, int nSelectors)
        {
            BZip2Compressor.CompressionState dataShadow = this.cstate;
            byte[] pos = dataShadow.sendMTFValues2_pos;
            int i = nGroups;
            while (--i >= 0)
            {
                pos[i] = (byte)i;
            }
            for (i = 0; i < nSelectors; i++)
            {
                byte ll_i = dataShadow.selector[i];
                byte tmp = pos[0];
                int j = 0;
                while (ll_i != tmp)
                {
                    j++;
                    byte tmp2 = tmp;
                    tmp = pos[j];
                    pos[j] = tmp2;
                }
                pos[0] = tmp;
                dataShadow.selectorMtf[i] = (byte)j;
            }
        }

        // Token: 0x0600035D RID: 861 RVA: 0x00017E5C File Offset: 0x0001605C
        private void sendMTFValues3(int nGroups, int alphaSize)
        {
            int[][] code = this.cstate.sendMTFValues_code;
            byte[][] len = this.cstate.sendMTFValues_len;
            for (int t = 0; t < nGroups; t++)
            {
                int minLen = 32;
                int maxLen = 0;
                byte[] len_t = len[t];
                int i = alphaSize;
                while (--i >= 0)
                {
                    int j = (int)(len_t[i] & byte.MaxValue);
                    if (j > maxLen)
                    {
                        maxLen = j;
                    }
                    if (j < minLen)
                    {
                        minLen = j;
                    }
                }
                BZip2Compressor.hbAssignCodes(code[t], len[t], minLen, maxLen, alphaSize);
            }
        }

        // Token: 0x0600035E RID: 862 RVA: 0x00017F00 File Offset: 0x00016100
        private void sendMTFValues4()
        {
            bool[] inUse = this.cstate.inUse;
            bool[] inUse2 = this.cstate.sentMTFValues4_inUse16;
            int i = 16;
            while (--i >= 0)
            {
                inUse2[i] = false;
                int i2 = i * 16;
                int j = 16;
                while (--j >= 0)
                {
                    if (inUse[i2 + j])
                    {
                        inUse2[i] = true;
                    }
                }
            }
            uint u = 0u;
            for (i = 0; i < 16; i++)
            {
                if (inUse2[i])
                {
                    u |= 1u << 16 - i - 1;
                }
            }
            this.bw.WriteBits(16, u);
            for (i = 0; i < 16; i++)
            {
                if (inUse2[i])
                {
                    int i2 = i * 16;
                    u = 0u;
                    for (int j = 0; j < 16; j++)
                    {
                        if (inUse[i2 + j])
                        {
                            u |= 1u << 16 - j - 1;
                        }
                    }
                    this.bw.WriteBits(16, u);
                }
            }
        }

        // Token: 0x0600035F RID: 863 RVA: 0x00018034 File Offset: 0x00016234
        private void sendMTFValues5(int nGroups, int nSelectors)
        {
            this.bw.WriteBits(3, (uint)nGroups);
            this.bw.WriteBits(15, (uint)nSelectors);
            byte[] selectorMtf = this.cstate.selectorMtf;
            for (int i = 0; i < nSelectors; i++)
            {
                int j = 0;
                int hj = (int)(selectorMtf[i] & byte.MaxValue);
                while (j < hj)
                {
                    this.bw.WriteBits(1, 1u);
                    j++;
                }
                this.bw.WriteBits(1, 0u);
            }
        }

        // Token: 0x06000360 RID: 864 RVA: 0x000180BC File Offset: 0x000162BC
        private void sendMTFValues6(int nGroups, int alphaSize)
        {
            byte[][] len = this.cstate.sendMTFValues_len;
            for (int t = 0; t < nGroups; t++)
            {
                byte[] len_t = len[t];
                uint curr = (uint)(len_t[0] & byte.MaxValue);
                this.bw.WriteBits(5, curr);
                for (int i = 0; i < alphaSize; i++)
                {
                    int lti = (int)(len_t[i] & byte.MaxValue);
                    while ((ulong)curr < (ulong)((long)lti))
                    {
                        this.bw.WriteBits(2, 2u);
                        curr += 1u;
                    }
                    while ((ulong)curr > (ulong)((long)lti))
                    {
                        this.bw.WriteBits(2, 3u);
                        curr -= 1u;
                    }
                    this.bw.WriteBits(1, 0u);
                }
            }
        }

        // Token: 0x06000361 RID: 865 RVA: 0x00018184 File Offset: 0x00016384
        private void sendMTFValues7(int nSelectors)
        {
            byte[][] len = this.cstate.sendMTFValues_len;
            int[][] code = this.cstate.sendMTFValues_code;
            byte[] selector = this.cstate.selector;
            char[] sfmap = this.cstate.sfmap;
            int nMTFShadow = this.nMTF;
            int selCtr = 0;
            int gs = 0;
            while (gs < nMTFShadow)
            {
                int ge = Math.Min(gs + BZip2.G_SIZE - 1, nMTFShadow - 1);
                int ix = (int)(selector[selCtr] & byte.MaxValue);
                int[] code_selCtr = code[ix];
                byte[] len_selCtr = len[ix];
                while (gs <= ge)
                {
                    int sfmap_i = (int)sfmap[gs];
                    int i = (int)(len_selCtr[sfmap_i] & byte.MaxValue);
                    this.bw.WriteBits(i, (uint)code_selCtr[sfmap_i]);
                    gs++;
                }
                gs = ge + 1;
                selCtr++;
            }
        }

        // Token: 0x06000362 RID: 866 RVA: 0x0001825B File Offset: 0x0001645B
        private void moveToFrontCodeAndSend()
        {
            this.bw.WriteBits(24, (uint)this.origPtr);
            this.generateMTFValues();
            this.sendMTFValues();
        }

        // Token: 0x040001AD RID: 429
        private int blockSize100k;

        // Token: 0x040001AE RID: 430
        private int currentByte = -1;

        // Token: 0x040001AF RID: 431
        private int runLength = 0;

        // Token: 0x040001B0 RID: 432
        private int last;

        // Token: 0x040001B1 RID: 433
        private int outBlockFillThreshold;

        // Token: 0x040001B2 RID: 434
        private BZip2Compressor.CompressionState cstate;

        // Token: 0x040001B3 RID: 435
        private readonly CRC32 crc = new CRC32(true);

        // Token: 0x040001B4 RID: 436
        private BitWriter bw;

        // Token: 0x040001B5 RID: 437
        private int runs;

        // Token: 0x040001B6 RID: 438
        private int workDone;

        // Token: 0x040001B7 RID: 439
        private int workLimit;

        // Token: 0x040001B8 RID: 440
        private bool firstAttempt;

        // Token: 0x040001B9 RID: 441
        private bool blockRandomised;

        // Token: 0x040001BA RID: 442
        private int origPtr;

        // Token: 0x040001BB RID: 443
        private int nInUse;

        // Token: 0x040001BC RID: 444
        private int nMTF;

        // Token: 0x040001BD RID: 445
        private static readonly int SETMASK = 2097152;

        // Token: 0x040001BE RID: 446
        private static readonly int CLEARMASK = ~BZip2Compressor.SETMASK;

        // Token: 0x040001BF RID: 447
        private static readonly byte GREATER_ICOST = 15;

        // Token: 0x040001C0 RID: 448
        private static readonly byte LESSER_ICOST = 0;

        // Token: 0x040001C1 RID: 449
        private static readonly int SMALL_THRESH = 20;

        // Token: 0x040001C2 RID: 450
        private static readonly int DEPTH_THRESH = 10;

        // Token: 0x040001C3 RID: 451
        private static readonly int WORK_FACTOR = 30;

        /// Knuth's increments seem to work better than Incerpi-Sedgewick here.
        /// Possibly because the number of elems to sort is usually small, typically
        /// &lt;= 20.
        // Token: 0x040001C4 RID: 452
        private static readonly int[] increments = new int[]
        {
            1,
            4,
            13,
            40,
            121,
            364,
            1093,
            3280,
            9841,
            29524,
            88573,
            265720,
            797161,
            2391484
        };

        // Token: 0x02000044 RID: 68
        private class CompressionState
        {
            // Token: 0x06000364 RID: 868 RVA: 0x00018314 File Offset: 0x00016514
            public CompressionState(int blockSize100k)
            {
                int i = blockSize100k * BZip2.BlockSizeMultiple;
                this.block = new byte[i + 1 + BZip2.NUM_OVERSHOOT_BYTES];
                this.fmap = new int[i];
                this.sfmap = new char[2 * i];
                this.quadrant = this.sfmap;
                this.sendMTFValues_len = BZip2.InitRectangularArray<byte>(BZip2.NGroups, BZip2.MaxAlphaSize);
                this.sendMTFValues_rfreq = BZip2.InitRectangularArray<int>(BZip2.NGroups, BZip2.MaxAlphaSize);
                this.sendMTFValues_code = BZip2.InitRectangularArray<int>(BZip2.NGroups, BZip2.MaxAlphaSize);
            }

            // Token: 0x040001C7 RID: 455
            public readonly bool[] inUse = new bool[256];

            // Token: 0x040001C8 RID: 456
            public readonly byte[] unseqToSeq = new byte[256];

            // Token: 0x040001C9 RID: 457
            public readonly int[] mtfFreq = new int[BZip2.MaxAlphaSize];

            // Token: 0x040001CA RID: 458
            public readonly byte[] selector = new byte[BZip2.MaxSelectors];

            // Token: 0x040001CB RID: 459
            public readonly byte[] selectorMtf = new byte[BZip2.MaxSelectors];

            // Token: 0x040001CC RID: 460
            public readonly byte[] generateMTFValues_yy = new byte[256];

            // Token: 0x040001CD RID: 461
            public byte[][] sendMTFValues_len;

            // Token: 0x040001CE RID: 462
            public int[][] sendMTFValues_rfreq;

            // Token: 0x040001CF RID: 463
            public readonly int[] sendMTFValues_fave = new int[BZip2.NGroups];

            // Token: 0x040001D0 RID: 464
            public readonly short[] sendMTFValues_cost = new short[BZip2.NGroups];

            // Token: 0x040001D1 RID: 465
            public int[][] sendMTFValues_code;

            // Token: 0x040001D2 RID: 466
            public readonly byte[] sendMTFValues2_pos = new byte[BZip2.NGroups];

            // Token: 0x040001D3 RID: 467
            public readonly bool[] sentMTFValues4_inUse16 = new bool[16];

            // Token: 0x040001D4 RID: 468
            public readonly int[] stack_ll = new int[BZip2.QSORT_STACK_SIZE];

            // Token: 0x040001D5 RID: 469
            public readonly int[] stack_hh = new int[BZip2.QSORT_STACK_SIZE];

            // Token: 0x040001D6 RID: 470
            public readonly int[] stack_dd = new int[BZip2.QSORT_STACK_SIZE];

            // Token: 0x040001D7 RID: 471
            public readonly int[] mainSort_runningOrder = new int[256];

            // Token: 0x040001D8 RID: 472
            public readonly int[] mainSort_copy = new int[256];

            // Token: 0x040001D9 RID: 473
            public readonly bool[] mainSort_bigDone = new bool[256];

            // Token: 0x040001DA RID: 474
            public int[] heap = new int[BZip2.MaxAlphaSize + 2];

            // Token: 0x040001DB RID: 475
            public int[] weight = new int[BZip2.MaxAlphaSize * 2];

            // Token: 0x040001DC RID: 476
            public int[] parent = new int[BZip2.MaxAlphaSize * 2];

            // Token: 0x040001DD RID: 477
            public readonly int[] ftab = new int[65537];

            // Token: 0x040001DE RID: 478
            public byte[] block;

            // Token: 0x040001DF RID: 479
            public int[] fmap;

            // Token: 0x040001E0 RID: 480
            public char[] sfmap;

            /// Array instance identical to sfmap, both are used only
            /// temporarily and independently, so we do not need to allocate
            /// additional memory.
            // Token: 0x040001E1 RID: 481
            public char[] quadrant;
        }
    }
}

namespace Ionic.BZip2
{
    /// <summary>
    ///   A read-only decorator stream that performs BZip2 decompression on Read.
    /// </summary>
    // Token: 0x02000045 RID: 69
    public class BZip2InputStream : Stream
    {
        /// <summary>
        ///   Create a BZip2InputStream, wrapping it around the given input Stream.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     The input stream will be closed when the BZip2InputStream is closed.
        ///   </para>
        /// </remarks>
        /// <param name="input">The stream from which to read compressed data</param>
        // Token: 0x06000365 RID: 869 RVA: 0x000184EE File Offset: 0x000166EE
        public BZip2InputStream(Stream input) : this(input, false)
        {
        }

        /// <summary>
        ///   Create a BZip2InputStream with the given stream, and
        ///   specifying whether to leave the wrapped stream open when
        ///   the BZip2InputStream is closed.
        /// </summary>
        /// <param name="input">The stream from which to read compressed data</param>
        /// <param name="leaveOpen">
        ///   Whether to leave the input stream open, when the BZip2InputStream closes.
        /// </param>
        ///
        /// <example>
        ///
        ///   This example reads a bzip2-compressed file, decompresses it,
        ///   and writes the decompressed data into a newly created file.
        ///
        ///   <code>
        ///   var fname = "logfile.log.bz2";
        ///   using (var fs = File.OpenRead(fname))
        ///   {
        ///       using (var decompressor = new Ionic.BZip2.BZip2InputStream(fs))
        ///       {
        ///           var outFname = fname + ".decompressed";
        ///           using (var output = File.Create(outFname))
        ///           {
        ///               byte[] buffer = new byte[2048];
        ///               int n;
        ///               while ((n = decompressor.Read(buffer, 0, buffer.Length)) &gt; 0)
        ///               {
        ///                   output.Write(buffer, 0, n);
        ///               }
        ///           }
        ///       }
        ///   }
        ///   </code>
        /// </example>
        // Token: 0x06000366 RID: 870 RVA: 0x000184FB File Offset: 0x000166FB
        public BZip2InputStream(Stream input, bool leaveOpen)
        {
            this.input = input;
            this._leaveOpen = leaveOpen;
            this.init();
        }

        /// <summary>
        ///   Read data from the stream.
        /// </summary>
        ///
        /// <remarks>
        ///   <para>
        ///     To decompress a BZip2 data stream, create a <c>BZip2InputStream</c>,
        ///     providing a stream that reads compressed data.  Then call Read() on
        ///     that <c>BZip2InputStream</c>, and the data read will be decompressed
        ///     as you read.
        ///   </para>
        ///
        ///   <para>
        ///     A <c>BZip2InputStream</c> can be used only for <c>Read()</c>, not for <c>Write()</c>.
        ///   </para>
        /// </remarks>
        ///
        /// <param name="buffer">The buffer into which the read data should be placed.</param>
        /// <param name="offset">the offset within that data array to put the first byte read.</param>
        /// <param name="count">the number of bytes to read.</param>
        /// <returns>the number of bytes actually read</returns>
        // Token: 0x06000367 RID: 871 RVA: 0x00018538 File Offset: 0x00016738
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (offset < 0)
            {
                throw new IndexOutOfRangeException(string.Format("offset ({0}) must be > 0", offset));
            }
            if (count < 0)
            {
                throw new IndexOutOfRangeException(string.Format("count ({0}) must be > 0", count));
            }
            if (offset + count > buffer.Length)
            {
                throw new IndexOutOfRangeException(string.Format("offset({0}) count({1}) bLength({2})", offset, count, buffer.Length));
            }
            if (this.input == null)
            {
                throw new IOException("the stream is not open");
            }
            int hi = offset + count;
            int destOffset = offset;
            int b;
            while (destOffset < hi && (b = this.ReadByte()) >= 0)
            {
                buffer[destOffset++] = (byte)b;
            }
            return (destOffset == offset) ? -1 : (destOffset - offset);
        }

        // Token: 0x06000368 RID: 872 RVA: 0x0001861C File Offset: 0x0001681C
        private void MakeMaps()
        {
            bool[] inUse = this.data.inUse;
            byte[] seqToUnseq = this.data.seqToUnseq;
            int i = 0;
            for (int j = 0; j < 256; j++)
            {
                if (inUse[j])
                {
                    seqToUnseq[i++] = (byte)j;
                }
            }
            this.nInUse = i;
        }

        /// <summary>
        ///   Read a single byte from the stream.
        /// </summary>
        /// <returns>the byte read from the stream, or -1 if EOF</returns>
        // Token: 0x06000369 RID: 873 RVA: 0x00018678 File Offset: 0x00016878
        public override int ReadByte()
        {
            int retChar = this.currentChar;
            this.totalBytesRead += 1L;
            switch (this.currentState)
            {
                case BZip2InputStream.CState.EOF:
                    return -1;
                case BZip2InputStream.CState.START_BLOCK:
                    throw new IOException("bad state");
                case BZip2InputStream.CState.RAND_PART_A:
                    throw new IOException("bad state");
                case BZip2InputStream.CState.RAND_PART_B:
                    this.SetupRandPartB();
                    break;
                case BZip2InputStream.CState.RAND_PART_C:
                    this.SetupRandPartC();
                    break;
                case BZip2InputStream.CState.NO_RAND_PART_A:
                    throw new IOException("bad state");
                case BZip2InputStream.CState.NO_RAND_PART_B:
                    this.SetupNoRandPartB();
                    break;
                case BZip2InputStream.CState.NO_RAND_PART_C:
                    this.SetupNoRandPartC();
                    break;
                default:
                    throw new IOException("bad state");
            }
            return retChar;
        }

        /// <summary>
        /// Indicates whether the stream can be read.
        /// </summary>
        /// <remarks>
        /// The return value depends on whether the captive stream supports reading.
        /// </remarks>
        // Token: 0x170000E6 RID: 230
        // (get) Token: 0x0600036A RID: 874 RVA: 0x00018724 File Offset: 0x00016924
        public override bool CanRead
        {
            get
            {
                if (this._disposed)
                {
                    throw new ObjectDisposedException("BZip2Stream");
                }
                return this.input.CanRead;
            }
        }

        /// <summary>
        /// Indicates whether the stream supports Seek operations.
        /// </summary>
        /// <remarks>
        /// Always returns false.
        /// </remarks>
        // Token: 0x170000E7 RID: 231
        // (get) Token: 0x0600036B RID: 875 RVA: 0x0001875C File Offset: 0x0001695C
        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Indicates whether the stream can be written.
        /// </summary>
        /// <remarks>
        /// The return value depends on whether the captive stream supports writing.
        /// </remarks>
        // Token: 0x170000E8 RID: 232
        // (get) Token: 0x0600036C RID: 876 RVA: 0x00018770 File Offset: 0x00016970
        public override bool CanWrite
        {
            get
            {
                if (this._disposed)
                {
                    throw new ObjectDisposedException("BZip2Stream");
                }
                return this.input.CanWrite;
            }
        }

        /// <summary>
        /// Flush the stream.
        /// </summary>
        // Token: 0x0600036D RID: 877 RVA: 0x000187A8 File Offset: 0x000169A8
        public override void Flush()
        {
            if (this._disposed)
            {
                throw new ObjectDisposedException("BZip2Stream");
            }
            this.input.Flush();
        }

        /// <summary>
        /// Reading this property always throws a <see cref="T:System.NotImplementedException" />.
        /// </summary>
        // Token: 0x170000E9 RID: 233
        // (get) Token: 0x0600036E RID: 878 RVA: 0x000187DA File Offset: 0x000169DA
        public override long Length
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// The position of the stream pointer.
        /// </summary>
        ///
        /// <remarks>
        ///   Setting this property always throws a <see cref="T:System.NotImplementedException" />. Reading will return the
        ///   total number of uncompressed bytes read in.
        /// </remarks>
        // Token: 0x170000EA RID: 234
        // (get) Token: 0x0600036F RID: 879 RVA: 0x000187E4 File Offset: 0x000169E4
        // (set) Token: 0x06000370 RID: 880 RVA: 0x000187FC File Offset: 0x000169FC
        public override long Position
        {
            get
            {
                return this.totalBytesRead;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Calling this method always throws a <see cref="T:System.NotImplementedException" />.
        /// </summary>
        /// <param name="offset">this is irrelevant, since it will always throw!</param>
        /// <param name="origin">this is irrelevant, since it will always throw!</param>
        /// <returns>irrelevant!</returns>
        // Token: 0x06000371 RID: 881 RVA: 0x00018804 File Offset: 0x00016A04
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Calling this method always throws a <see cref="T:System.NotImplementedException" />.
        /// </summary>
        /// <param name="value">this is irrelevant, since it will always throw!</param>
        // Token: 0x06000372 RID: 882 RVA: 0x0001880C File Offset: 0x00016A0C
        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///   Calling this method always throws a <see cref="T:System.NotImplementedException" />.
        /// </summary>
        /// <param name="buffer">this parameter is never used</param>
        /// <param name="offset">this parameter is never used</param>
        /// <param name="count">this parameter is never used</param>
        // Token: 0x06000373 RID: 883 RVA: 0x00018814 File Offset: 0x00016A14
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///   Dispose the stream.
        /// </summary>
        /// <param name="disposing">
        ///   indicates whether the Dispose method was invoked by user code.
        /// </param>
        // Token: 0x06000374 RID: 884 RVA: 0x0001881C File Offset: 0x00016A1C
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!this._disposed)
                {
                    if (disposing && this.input != null)
                    {
                        this.input.Close();
                    }
                    this._disposed = true;
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        // Token: 0x06000375 RID: 885 RVA: 0x0001887C File Offset: 0x00016A7C
        private void init()
        {
            if (null == this.input)
            {
                throw new IOException("No input Stream");
            }
            if (!this.input.CanRead)
            {
                throw new IOException("Unreadable input Stream");
            }
            this.CheckMagicChar('B', 0);
            this.CheckMagicChar('Z', 1);
            this.CheckMagicChar('h', 2);
            int blockSize = this.input.ReadByte();
            if (blockSize < 49 || blockSize > 57)
            {
                throw new IOException("Stream is not BZip2 formatted: illegal blocksize " + (char)blockSize);
            }
            this.blockSize100k = blockSize - 48;
            this.InitBlock();
            this.SetupBlock();
        }

        // Token: 0x06000376 RID: 886 RVA: 0x0001892C File Offset: 0x00016B2C
        private void CheckMagicChar(char expected, int position)
        {
            int magic = this.input.ReadByte();
            if (magic != (int)expected)
            {
                string msg = string.Format("Not a valid BZip2 stream. byte {0}, expected '{1}', got '{2}'", position, (int)expected, magic);
                throw new IOException(msg);
            }
        }

        // Token: 0x06000377 RID: 887 RVA: 0x00018974 File Offset: 0x00016B74
        private void InitBlock()
        {
            char magic0 = this.bsGetUByte();
            char magic = this.bsGetUByte();
            char magic2 = this.bsGetUByte();
            char magic3 = this.bsGetUByte();
            char magic4 = this.bsGetUByte();
            char magic5 = this.bsGetUByte();
            if (magic0 == '\u0017' && magic == 'r' && magic2 == 'E' && magic3 == '8' && magic4 == 'P' && magic5 == '\u0090')
            {
                this.complete();
            }
            else
            {
                if (magic0 != '1' || magic != 'A' || magic2 != 'Y' || magic3 != '&' || magic4 != 'S' || magic5 != 'Y')
                {
                    this.currentState = BZip2InputStream.CState.EOF;
                    string msg = string.Format("bad block header at offset 0x{0:X}", this.input.Position);
                    throw new IOException(msg);
                }
                this.storedBlockCRC = this.bsGetInt();
                this.blockRandomised = (this.GetBits(1) == 1);
                if (this.data == null)
                {
                    this.data = new BZip2InputStream.DecompressionState(this.blockSize100k);
                }
                this.getAndMoveToFrontDecode();
                this.crc.Reset();
                this.currentState = BZip2InputStream.CState.START_BLOCK;
            }
        }

        // Token: 0x06000378 RID: 888 RVA: 0x00018A9C File Offset: 0x00016C9C
        private void EndBlock()
        {
            this.computedBlockCRC = (uint)this.crc.Crc32Result;
            if (this.storedBlockCRC != this.computedBlockCRC)
            {
                string msg = string.Format("BZip2 CRC error (expected {0:X8}, computed {1:X8})", this.storedBlockCRC, this.computedBlockCRC);
                throw new IOException(msg);
            }
            this.computedCombinedCRC = (this.computedCombinedCRC << 1 | this.computedCombinedCRC >> 31);
            this.computedCombinedCRC ^= this.computedBlockCRC;
        }

        // Token: 0x06000379 RID: 889 RVA: 0x00018B24 File Offset: 0x00016D24
        private void complete()
        {
            this.storedCombinedCRC = this.bsGetInt();
            this.currentState = BZip2InputStream.CState.EOF;
            this.data = null;
            if (this.storedCombinedCRC != this.computedCombinedCRC)
            {
                string msg = string.Format("BZip2 CRC error (expected {0:X8}, computed {1:X8})", this.storedCombinedCRC, this.computedCombinedCRC);
                throw new IOException(msg);
            }
        }

        /// <summary>
        ///   Close the stream.
        /// </summary>
        // Token: 0x0600037A RID: 890 RVA: 0x00018B88 File Offset: 0x00016D88
        public override void Close()
        {
            Stream inShadow = this.input;
            if (inShadow != null)
            {
                try
                {
                    if (!this._leaveOpen)
                    {
                        inShadow.Close();
                    }
                }
                finally
                {
                    this.data = null;
                    this.input = null;
                }
            }
        }

        /// <summary>
        ///   Read n bits from input, right justifying the result.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     For example, if you read 1 bit, the result is either 0
        ///     or 1.
        ///   </para>
        /// </remarks>
        /// <param name="n">
        ///   The number of bits to read, always between 1 and 32.
        /// </param>
        // Token: 0x0600037B RID: 891 RVA: 0x00018BE0 File Offset: 0x00016DE0
        private int GetBits(int n)
        {
            int bsLiveShadow = this.bsLive;
            int bsBuffShadow = this.bsBuff;
            if (bsLiveShadow < n)
            {
                for (; ; )
                {
                    int thech = this.input.ReadByte();
                    if (thech < 0)
                    {
                        break;
                    }
                    bsBuffShadow = (bsBuffShadow << 8 | thech);
                    bsLiveShadow += 8;
                    if (bsLiveShadow >= n)
                    {
                        goto Block_2;
                    }
                }
                throw new IOException("unexpected end of stream");
                Block_2:
                this.bsBuff = bsBuffShadow;
            }
            this.bsLive = bsLiveShadow - n;
            return bsBuffShadow >> bsLiveShadow - n & (1 << n) - 1;
        }

        // Token: 0x0600037C RID: 892 RVA: 0x00018C6C File Offset: 0x00016E6C
        private bool bsGetBit()
        {
            int bit = this.GetBits(1);
            return bit != 0;
        }

        // Token: 0x0600037D RID: 893 RVA: 0x00018C90 File Offset: 0x00016E90
        private char bsGetUByte()
        {
            return (char)this.GetBits(8);
        }

        // Token: 0x0600037E RID: 894 RVA: 0x00018CAC File Offset: 0x00016EAC
        private uint bsGetInt()
        {
            return (uint)(((this.GetBits(8) << 8 | this.GetBits(8)) << 8 | this.GetBits(8)) << 8 | this.GetBits(8));
        }

        /// Called by createHuffmanDecodingTables() exclusively.
        // Token: 0x0600037F RID: 895 RVA: 0x00018CE4 File Offset: 0x00016EE4
        private static void hbCreateDecodeTables(int[] limit, int[] bbase, int[] perm, char[] length, int minLen, int maxLen, int alphaSize)
        {
            int i = minLen;
            int pp = 0;
            while (i <= maxLen)
            {
                for (int j = 0; j < alphaSize; j++)
                {
                    if ((int)length[j] == i)
                    {
                        perm[pp++] = j;
                    }
                }
                i++;
            }
            i = BZip2.MaxCodeLength;
            while (--i > 0)
            {
                bbase[i] = 0;
                limit[i] = 0;
            }
            for (i = 0; i < alphaSize; i++)
            {
                bbase[(int)(length[i] + '\u0001')]++;
            }
            i = 1;
            int b = bbase[0];
            while (i < BZip2.MaxCodeLength)
            {
                b += bbase[i];
                bbase[i] = b;
                i++;
            }
            i = minLen;
            int vec = 0;
            b = bbase[i];
            while (i <= maxLen)
            {
                int nb = bbase[i + 1];
                vec += nb - b;
                b = nb;
                limit[i] = vec - 1;
                vec <<= 1;
                i++;
            }
            for (i = minLen + 1; i <= maxLen; i++)
            {
                bbase[i] = (limit[i - 1] + 1 << 1) - bbase[i];
            }
        }

        // Token: 0x06000380 RID: 896 RVA: 0x00018E1C File Offset: 0x0001701C
        private void recvDecodingTables()
        {
            BZip2InputStream.DecompressionState s = this.data;
            bool[] inUse = s.inUse;
            byte[] pos = s.recvDecodingTables_pos;
            int inUse2 = 0;
            int i;
            for (i = 0; i < 16; i++)
            {
                if (this.bsGetBit())
                {
                    inUse2 |= 1 << i;
                }
            }
            i = 256;
            while (--i >= 0)
            {
                inUse[i] = false;
            }
            for (i = 0; i < 16; i++)
            {
                if ((inUse2 & 1 << i) != 0)
                {
                    int i2 = i << 4;
                    for (int j = 0; j < 16; j++)
                    {
                        if (this.bsGetBit())
                        {
                            inUse[i2 + j] = true;
                        }
                    }
                }
            }
            this.MakeMaps();
            int alphaSize = this.nInUse + 2;
            int nGroups = this.GetBits(3);
            int nSelectors = this.GetBits(15);
            for (i = 0; i < nSelectors; i++)
            {
                int j = 0;
                while (this.bsGetBit())
                {
                    j++;
                }
                s.selectorMtf[i] = (byte)j;
            }
            int v = nGroups;
            while (--v >= 0)
            {
                pos[v] = (byte)v;
            }
            for (i = 0; i < nSelectors; i++)
            {
                v = (int)s.selectorMtf[i];
                byte tmp = pos[v];
                while (v > 0)
                {
                    pos[v] = pos[v - 1];
                    v--;
                }
                pos[0] = tmp;
                s.selector[i] = tmp;
            }
            char[][] len = s.temp_charArray2d;
            for (int t = 0; t < nGroups; t++)
            {
                int curr = this.GetBits(5);
                char[] len_t = len[t];
                for (i = 0; i < alphaSize; i++)
                {
                    while (this.bsGetBit())
                    {
                        curr += (this.bsGetBit() ? -1 : 1);
                    }
                    len_t[i] = (char)curr;
                }
            }
            this.createHuffmanDecodingTables(alphaSize, nGroups);
        }

        /// Called by recvDecodingTables() exclusively.
        // Token: 0x06000381 RID: 897 RVA: 0x00019058 File Offset: 0x00017258
        private void createHuffmanDecodingTables(int alphaSize, int nGroups)
        {
            BZip2InputStream.DecompressionState s = this.data;
            char[][] len = s.temp_charArray2d;
            for (int t = 0; t < nGroups; t++)
            {
                int minLen = 32;
                int maxLen = 0;
                char[] len_t = len[t];
                int i = alphaSize;
                while (--i >= 0)
                {
                    char lent = len_t[i];
                    if ((int)lent > maxLen)
                    {
                        maxLen = (int)lent;
                    }
                    if ((int)lent < minLen)
                    {
                        minLen = (int)lent;
                    }
                }
                BZip2InputStream.hbCreateDecodeTables(s.gLimit[t], s.gBase[t], s.gPerm[t], len[t], minLen, maxLen, alphaSize);
                s.gMinlen[t] = minLen;
            }
        }

        // Token: 0x06000382 RID: 898 RVA: 0x0001910C File Offset: 0x0001730C
        private void getAndMoveToFrontDecode()
        {
            BZip2InputStream.DecompressionState s = this.data;
            this.origPtr = this.GetBits(24);
            if (this.origPtr < 0)
            {
                throw new IOException("BZ_DATA_ERROR");
            }
            if (this.origPtr > 10 + BZip2.BlockSizeMultiple * this.blockSize100k)
            {
                throw new IOException("BZ_DATA_ERROR");
            }
            this.recvDecodingTables();
            byte[] yy = s.getAndMoveToFrontDecode_yy;
            int limitLast = this.blockSize100k * BZip2.BlockSizeMultiple;
            int i = 256;
            while (--i >= 0)
            {
                yy[i] = (byte)i;
                s.unzftab[i] = 0;
            }
            int groupNo = 0;
            int groupPos = BZip2.G_SIZE - 1;
            int eob = this.nInUse + 1;
            int nextSym = this.getAndMoveToFrontDecode0(0);
            int bsBuffShadow = this.bsBuff;
            int bsLiveShadow = this.bsLive;
            int lastShadow = -1;
            int zt = (int)(s.selector[groupNo] & byte.MaxValue);
            int[] base_zt = s.gBase[zt];
            int[] limit_zt = s.gLimit[zt];
            int[] perm_zt = s.gPerm[zt];
            int minLens_zt = s.gMinlen[zt];
            while (nextSym != eob)
            {
                int zn;
                int zvec;
                if (nextSym == (int)BZip2.RUNA || nextSym == (int)BZip2.RUNB)
                {
                    int es = -1;
                    int j = 1;
                    for (; ; )
                    {
                        if (nextSym == (int)BZip2.RUNA)
                        {
                            es += j;
                        }
                        else
                        {
                            if (nextSym != (int)BZip2.RUNB)
                            {
                                break;
                            }
                            es += j << 1;
                        }
                        if (groupPos == 0)
                        {
                            groupPos = BZip2.G_SIZE - 1;
                            zt = (int)(s.selector[++groupNo] & byte.MaxValue);
                            base_zt = s.gBase[zt];
                            limit_zt = s.gLimit[zt];
                            perm_zt = s.gPerm[zt];
                            minLens_zt = s.gMinlen[zt];
                        }
                        else
                        {
                            groupPos--;
                        }
                        zn = minLens_zt;
                        while (bsLiveShadow < zn)
                        {
                            int thech = this.input.ReadByte();
                            if (thech < 0)
                            {
                                goto IL_21D;
                            }
                            bsBuffShadow = (bsBuffShadow << 8 | thech);
                            bsLiveShadow += 8;
                        }
                        zvec = (bsBuffShadow >> bsLiveShadow - zn & (1 << zn) - 1);
                        bsLiveShadow -= zn;
                        while (zvec > limit_zt[zn])
                        {
                            zn++;
                            while (bsLiveShadow < 1)
                            {
                                int thech = this.input.ReadByte();
                                if (thech < 0)
                                {
                                    goto IL_289;
                                }
                                bsBuffShadow = (bsBuffShadow << 8 | thech);
                                bsLiveShadow += 8;
                            }
                            bsLiveShadow--;
                            zvec = (zvec << 1 | (bsBuffShadow >> bsLiveShadow & 1));
                        }
                        nextSym = perm_zt[zvec - base_zt[zn]];
                        j <<= 1;
                    }
                    byte ch = s.seqToUnseq[(int)yy[0]];
                    s.unzftab[(int)(ch & byte.MaxValue)] += es + 1;
                    while (es-- >= 0)
                    {
                        s.ll8[++lastShadow] = ch;
                    }
                    if (lastShadow >= limitLast)
                    {
                        throw new IOException("block overrun");
                    }
                    continue;
                    IL_21D:
                    throw new IOException("unexpected end of stream");
                    IL_289:
                    throw new IOException("unexpected end of stream");
                }
                if (++lastShadow >= limitLast)
                {
                    throw new IOException("block overrun");
                }
                byte tmp = yy[nextSym - 1];
                s.unzftab[(int)(s.seqToUnseq[(int)tmp] & byte.MaxValue)]++;
                s.ll8[lastShadow] = s.seqToUnseq[(int)tmp];
                if (nextSym <= 16)
                {
                    int k = nextSym - 1;
                    while (k > 0)
                    {
                        yy[k] = yy[--k];
                    }
                }
                else
                {
                    Buffer.BlockCopy(yy, 0, yy, 1, nextSym - 1);
                }
                yy[0] = tmp;
                if (groupPos == 0)
                {
                    groupPos = BZip2.G_SIZE - 1;
                    zt = (int)(s.selector[++groupNo] & byte.MaxValue);
                    base_zt = s.gBase[zt];
                    limit_zt = s.gLimit[zt];
                    perm_zt = s.gPerm[zt];
                    minLens_zt = s.gMinlen[zt];
                }
                else
                {
                    groupPos--;
                }
                zn = minLens_zt;
                while (bsLiveShadow < zn)
                {
                    int thech = this.input.ReadByte();
                    if (thech < 0)
                    {
                        throw new IOException("unexpected end of stream");
                    }
                    bsBuffShadow = (bsBuffShadow << 8 | thech);
                    bsLiveShadow += 8;
                }
                zvec = (bsBuffShadow >> bsLiveShadow - zn & (1 << zn) - 1);
                bsLiveShadow -= zn;
                while (zvec > limit_zt[zn])
                {
                    zn++;
                    while (bsLiveShadow < 1)
                    {
                        int thech = this.input.ReadByte();
                        if (thech < 0)
                        {
                            throw new IOException("unexpected end of stream");
                        }
                        bsBuffShadow = (bsBuffShadow << 8 | thech);
                        bsLiveShadow += 8;
                    }
                    bsLiveShadow--;
                    zvec = (zvec << 1 | (bsBuffShadow >> bsLiveShadow & 1));
                }
                nextSym = perm_zt[zvec - base_zt[zn]];
            }
            this.last = lastShadow;
            this.bsLive = bsLiveShadow;
            this.bsBuff = bsBuffShadow;
        }

        // Token: 0x06000383 RID: 899 RVA: 0x0001968C File Offset: 0x0001788C
        private int getAndMoveToFrontDecode0(int groupNo)
        {
            BZip2InputStream.DecompressionState s = this.data;
            int zt = (int)(s.selector[groupNo] & byte.MaxValue);
            int[] limit_zt = s.gLimit[zt];
            int zn = s.gMinlen[zt];
            int zvec = this.GetBits(zn);
            int bsLiveShadow = this.bsLive;
            int bsBuffShadow = this.bsBuff;
            while (zvec > limit_zt[zn])
            {
                zn++;
                while (bsLiveShadow < 1)
                {
                    int thech = this.input.ReadByte();
                    if (thech < 0)
                    {
                        throw new IOException("unexpected end of stream");
                    }
                    bsBuffShadow = (bsBuffShadow << 8 | thech);
                    bsLiveShadow += 8;
                }
                bsLiveShadow--;
                zvec = (zvec << 1 | (bsBuffShadow >> bsLiveShadow & 1));
            }
            this.bsLive = bsLiveShadow;
            this.bsBuff = bsBuffShadow;
            return s.gPerm[zt][zvec - s.gBase[zt][zn]];
        }

        // Token: 0x06000384 RID: 900 RVA: 0x00019778 File Offset: 0x00017978
        private void SetupBlock()
        {
            if (this.data != null)
            {
                BZip2InputStream.DecompressionState s = this.data;
                int[] tt = s.initTT(this.last + 1);
                int i;
                for (i = 0; i <= 255; i++)
                {
                    if (s.unzftab[i] < 0 || s.unzftab[i] > this.last)
                    {
                        throw new Exception("BZ_DATA_ERROR");
                    }
                }
                s.cftab[0] = 0;
                for (i = 1; i <= 256; i++)
                {
                    s.cftab[i] = s.unzftab[i - 1];
                }
                for (i = 1; i <= 256; i++)
                {
                    s.cftab[i] += s.cftab[i - 1];
                }
                for (i = 0; i <= 256; i++)
                {
                    if (s.cftab[i] < 0 || s.cftab[i] > this.last + 1)
                    {
                        string msg = string.Format("BZ_DATA_ERROR: cftab[{0}]={1} last={2}", i, s.cftab[i], this.last);
                        throw new Exception(msg);
                    }
                }
                for (i = 1; i <= 256; i++)
                {
                    if (s.cftab[i - 1] > s.cftab[i])
                    {
                        throw new Exception("BZ_DATA_ERROR");
                    }
                }
                i = 0;
                int lastShadow = this.last;
                while (i <= lastShadow)
                {
                    tt[s.cftab[(int)(s.ll8[i] & byte.MaxValue)]++] = i;
                    i++;
                }
                if (this.origPtr < 0 || this.origPtr >= tt.Length)
                {
                    throw new IOException("stream corrupted");
                }
                this.su_tPos = tt[this.origPtr];
                this.su_count = 0;
                this.su_i2 = 0;
                this.su_ch2 = 256;
                if (this.blockRandomised)
                {
                    this.su_rNToGo = 0;
                    this.su_rTPos = 0;
                    this.SetupRandPartA();
                }
                else
                {
                    this.SetupNoRandPartA();
                }
            }
        }

        // Token: 0x06000385 RID: 901 RVA: 0x000199F8 File Offset: 0x00017BF8
        private void SetupRandPartA()
        {
            if (this.su_i2 <= this.last)
            {
                this.su_chPrev = this.su_ch2;
                int su_ch2Shadow = (int)(this.data.ll8[this.su_tPos] & byte.MaxValue);
                this.su_tPos = this.data.tt[this.su_tPos];
                if (this.su_rNToGo == 0)
                {
                    this.su_rNToGo = Rand.Rnums(this.su_rTPos) - 1;
                    if (++this.su_rTPos == 512)
                    {
                        this.su_rTPos = 0;
                    }
                }
                else
                {
                    this.su_rNToGo--;
                }
                su_ch2Shadow = (this.su_ch2 = (su_ch2Shadow ^ ((this.su_rNToGo == 1) ? 1 : 0)));
                this.su_i2++;
                this.currentChar = su_ch2Shadow;
                this.currentState = BZip2InputStream.CState.RAND_PART_B;
                this.crc.UpdateCRC((byte)su_ch2Shadow);
            }
            else
            {
                this.EndBlock();
                this.InitBlock();
                this.SetupBlock();
            }
        }

        // Token: 0x06000386 RID: 902 RVA: 0x00019B14 File Offset: 0x00017D14
        private void SetupNoRandPartA()
        {
            if (this.su_i2 <= this.last)
            {
                this.su_chPrev = this.su_ch2;
                int su_ch2Shadow = (int)(this.data.ll8[this.su_tPos] & byte.MaxValue);
                this.su_ch2 = su_ch2Shadow;
                this.su_tPos = this.data.tt[this.su_tPos];
                this.su_i2++;
                this.currentChar = su_ch2Shadow;
                this.currentState = BZip2InputStream.CState.NO_RAND_PART_B;
                this.crc.UpdateCRC((byte)su_ch2Shadow);
            }
            else
            {
                this.currentState = BZip2InputStream.CState.NO_RAND_PART_A;
                this.EndBlock();
                this.InitBlock();
                this.SetupBlock();
            }
        }

        // Token: 0x06000387 RID: 903 RVA: 0x00019BC4 File Offset: 0x00017DC4
        private void SetupRandPartB()
        {
            if (this.su_ch2 != this.su_chPrev)
            {
                this.currentState = BZip2InputStream.CState.RAND_PART_A;
                this.su_count = 1;
                this.SetupRandPartA();
            }
            else if (++this.su_count >= 4)
            {
                this.su_z = (char)(this.data.ll8[this.su_tPos] & byte.MaxValue);
                this.su_tPos = this.data.tt[this.su_tPos];
                if (this.su_rNToGo == 0)
                {
                    this.su_rNToGo = Rand.Rnums(this.su_rTPos) - 1;
                    if (++this.su_rTPos == 512)
                    {
                        this.su_rTPos = 0;
                    }
                }
                else
                {
                    this.su_rNToGo--;
                }
                this.su_j2 = 0;
                this.currentState = BZip2InputStream.CState.RAND_PART_C;
                if (this.su_rNToGo == 1)
                {
                    this.su_z ^= '\u0001';
                }
                this.SetupRandPartC();
            }
            else
            {
                this.currentState = BZip2InputStream.CState.RAND_PART_A;
                this.SetupRandPartA();
            }
        }

        // Token: 0x06000388 RID: 904 RVA: 0x00019CFC File Offset: 0x00017EFC
        private void SetupRandPartC()
        {
            if (this.su_j2 < (int)this.su_z)
            {
                this.currentChar = this.su_ch2;
                this.crc.UpdateCRC((byte)this.su_ch2);
                this.su_j2++;
            }
            else
            {
                this.currentState = BZip2InputStream.CState.RAND_PART_A;
                this.su_i2++;
                this.su_count = 0;
                this.SetupRandPartA();
            }
        }

        // Token: 0x06000389 RID: 905 RVA: 0x00019D78 File Offset: 0x00017F78
        private void SetupNoRandPartB()
        {
            if (this.su_ch2 != this.su_chPrev)
            {
                this.su_count = 1;
                this.SetupNoRandPartA();
            }
            else if (++this.su_count >= 4)
            {
                this.su_z = (char)(this.data.ll8[this.su_tPos] & byte.MaxValue);
                this.su_tPos = this.data.tt[this.su_tPos];
                this.su_j2 = 0;
                this.SetupNoRandPartC();
            }
            else
            {
                this.SetupNoRandPartA();
            }
        }

        // Token: 0x0600038A RID: 906 RVA: 0x00019E14 File Offset: 0x00018014
        private void SetupNoRandPartC()
        {
            if (this.su_j2 < (int)this.su_z)
            {
                int su_ch2Shadow = this.su_ch2;
                this.currentChar = su_ch2Shadow;
                this.crc.UpdateCRC((byte)su_ch2Shadow);
                this.su_j2++;
                this.currentState = BZip2InputStream.CState.NO_RAND_PART_C;
            }
            else
            {
                this.su_i2++;
                this.su_count = 0;
                this.SetupNoRandPartA();
            }
        }

        // Token: 0x040001E2 RID: 482
        private bool _disposed;

        // Token: 0x040001E3 RID: 483
        private bool _leaveOpen;

        // Token: 0x040001E4 RID: 484
        private long totalBytesRead;

        // Token: 0x040001E5 RID: 485
        private int last;

        // Token: 0x040001E6 RID: 486
        private int origPtr;

        // Token: 0x040001E7 RID: 487
        private int blockSize100k;

        // Token: 0x040001E8 RID: 488
        private bool blockRandomised;

        // Token: 0x040001E9 RID: 489
        private int bsBuff;

        // Token: 0x040001EA RID: 490
        private int bsLive;

        // Token: 0x040001EB RID: 491
        private readonly CRC32 crc = new CRC32(true);

        // Token: 0x040001EC RID: 492
        private int nInUse;

        // Token: 0x040001ED RID: 493
        private Stream input;

        // Token: 0x040001EE RID: 494
        private int currentChar = -1;

        // Token: 0x040001EF RID: 495
        private BZip2InputStream.CState currentState = BZip2InputStream.CState.START_BLOCK;

        // Token: 0x040001F0 RID: 496
        private uint storedBlockCRC;

        // Token: 0x040001F1 RID: 497
        private uint storedCombinedCRC;

        // Token: 0x040001F2 RID: 498
        private uint computedBlockCRC;

        // Token: 0x040001F3 RID: 499
        private uint computedCombinedCRC;

        // Token: 0x040001F4 RID: 500
        private int su_count;

        // Token: 0x040001F5 RID: 501
        private int su_ch2;

        // Token: 0x040001F6 RID: 502
        private int su_chPrev;

        // Token: 0x040001F7 RID: 503
        private int su_i2;

        // Token: 0x040001F8 RID: 504
        private int su_j2;

        // Token: 0x040001F9 RID: 505
        private int su_rNToGo;

        // Token: 0x040001FA RID: 506
        private int su_rTPos;

        // Token: 0x040001FB RID: 507
        private int su_tPos;

        // Token: 0x040001FC RID: 508
        private char su_z;

        // Token: 0x040001FD RID: 509
        private BZip2InputStream.DecompressionState data;

        /// <summary>
        ///   Compressor State
        /// </summary>
        // Token: 0x02000046 RID: 70
        private enum CState
        {
            // Token: 0x040001FF RID: 511
            EOF,
            // Token: 0x04000200 RID: 512
            START_BLOCK,
            // Token: 0x04000201 RID: 513
            RAND_PART_A,
            // Token: 0x04000202 RID: 514
            RAND_PART_B,
            // Token: 0x04000203 RID: 515
            RAND_PART_C,
            // Token: 0x04000204 RID: 516
            NO_RAND_PART_A,
            // Token: 0x04000205 RID: 517
            NO_RAND_PART_B,
            // Token: 0x04000206 RID: 518
            NO_RAND_PART_C
        }

        // Token: 0x02000047 RID: 71
        private sealed class DecompressionState
        {
            // Token: 0x0600038B RID: 907 RVA: 0x00019E8C File Offset: 0x0001808C
            public DecompressionState(int blockSize100k)
            {
                this.unzftab = new int[256];
                this.gLimit = BZip2.InitRectangularArray<int>(BZip2.NGroups, BZip2.MaxAlphaSize);
                this.gBase = BZip2.InitRectangularArray<int>(BZip2.NGroups, BZip2.MaxAlphaSize);
                this.gPerm = BZip2.InitRectangularArray<int>(BZip2.NGroups, BZip2.MaxAlphaSize);
                this.gMinlen = new int[BZip2.NGroups];
                this.cftab = new int[257];
                this.getAndMoveToFrontDecode_yy = new byte[256];
                this.temp_charArray2d = BZip2.InitRectangularArray<char>(BZip2.NGroups, BZip2.MaxAlphaSize);
                this.recvDecodingTables_pos = new byte[BZip2.NGroups];
                this.ll8 = new byte[blockSize100k * BZip2.BlockSizeMultiple];
            }

            /// Initializes the tt array.
            ///
            /// This method is called when the required length of the array is known.
            /// I don't initialize it at construction time to avoid unneccessary
            /// memory allocation when compressing small files.
            // Token: 0x0600038C RID: 908 RVA: 0x00019F98 File Offset: 0x00018198
            public int[] initTT(int length)
            {
                int[] ttShadow = this.tt;
                if (ttShadow == null || ttShadow.Length < length)
                {
                    ttShadow = (this.tt = new int[length]);
                }
                return ttShadow;
            }

            // Token: 0x04000207 RID: 519
            public readonly bool[] inUse = new bool[256];

            // Token: 0x04000208 RID: 520
            public readonly byte[] seqToUnseq = new byte[256];

            // Token: 0x04000209 RID: 521
            public readonly byte[] selector = new byte[BZip2.MaxSelectors];

            // Token: 0x0400020A RID: 522
            public readonly byte[] selectorMtf = new byte[BZip2.MaxSelectors];

            /// Freq table collected to save a pass over the data during
            /// decompression.
            // Token: 0x0400020B RID: 523
            public readonly int[] unzftab;

            // Token: 0x0400020C RID: 524
            public readonly int[][] gLimit;

            // Token: 0x0400020D RID: 525
            public readonly int[][] gBase;

            // Token: 0x0400020E RID: 526
            public readonly int[][] gPerm;

            // Token: 0x0400020F RID: 527
            public readonly int[] gMinlen;

            // Token: 0x04000210 RID: 528
            public readonly int[] cftab;

            // Token: 0x04000211 RID: 529
            public readonly byte[] getAndMoveToFrontDecode_yy;

            // Token: 0x04000212 RID: 530
            public readonly char[][] temp_charArray2d;

            // Token: 0x04000213 RID: 531
            public readonly byte[] recvDecodingTables_pos;

            // Token: 0x04000214 RID: 532
            public int[] tt;

            // Token: 0x04000215 RID: 533
            public byte[] ll8;
        }
    }
}

namespace Ionic.BZip2
{
    /// <summary>
    ///   A write-only decorator stream that compresses data as it is
    ///   written using the BZip2 algorithm.
    /// </summary>
    // Token: 0x02000049 RID: 73
    public class BZip2OutputStream : Stream
    {
        /// <summary>
        ///   Constructs a new <c>BZip2OutputStream</c>, that sends its
        ///   compressed output to the given output stream.
        /// </summary>
        ///
        /// <param name="output">
        ///   The destination stream, to which compressed output will be sent.
        /// </param>
        ///
        /// <example>
        ///
        ///   This example reads a file, then compresses it with bzip2 file,
        ///   and writes the compressed data into a newly created file.
        ///
        ///   <code>
        ///   var fname = "logfile.log";
        ///   using (var fs = File.OpenRead(fname))
        ///   {
        ///       var outFname = fname + ".bz2";
        ///       using (var output = File.Create(outFname))
        ///       {
        ///           using (var compressor = new Ionic.BZip2.BZip2OutputStream(output))
        ///           {
        ///               byte[] buffer = new byte[2048];
        ///               int n;
        ///               while ((n = fs.Read(buffer, 0, buffer.Length)) &gt; 0)
        ///               {
        ///                   compressor.Write(buffer, 0, n);
        ///               }
        ///           }
        ///       }
        ///   }
        ///   </code>
        /// </example>
        // Token: 0x0600038F RID: 911 RVA: 0x0001A087 File Offset: 0x00018287
        public BZip2OutputStream(Stream output) : this(output, BZip2.MaxBlockSize, false)
        {
        }

        /// <summary>
        ///   Constructs a new <c>BZip2OutputStream</c> with specified blocksize.
        /// </summary>
        /// <param name="output">the destination stream.</param>
        /// <param name="blockSize">
        ///   The blockSize in units of 100000 bytes.
        ///   The valid range is 1..9.
        /// </param>
        // Token: 0x06000390 RID: 912 RVA: 0x0001A099 File Offset: 0x00018299
        public BZip2OutputStream(Stream output, int blockSize) : this(output, blockSize, false)
        {
        }

        /// <summary>
        ///   Constructs a new <c>BZip2OutputStream</c>.
        /// </summary>
        ///   <param name="output">the destination stream.</param>
        /// <param name="leaveOpen">
        ///   whether to leave the captive stream open upon closing this stream.
        /// </param>
        // Token: 0x06000391 RID: 913 RVA: 0x0001A0A7 File Offset: 0x000182A7
        public BZip2OutputStream(Stream output, bool leaveOpen) : this(output, BZip2.MaxBlockSize, leaveOpen)
        {
        }

        /// <summary>
        ///   Constructs a new <c>BZip2OutputStream</c> with specified blocksize,
        ///   and explicitly specifies whether to leave the wrapped stream open.
        /// </summary>
        ///
        /// <param name="output">the destination stream.</param>
        /// <param name="blockSize">
        ///   The blockSize in units of 100000 bytes.
        ///   The valid range is 1..9.
        /// </param>
        /// <param name="leaveOpen">
        ///   whether to leave the captive stream open upon closing this stream.
        /// </param>
        // Token: 0x06000392 RID: 914 RVA: 0x0001A0BC File Offset: 0x000182BC
        public BZip2OutputStream(Stream output, int blockSize, bool leaveOpen)
        {
            if (blockSize < BZip2.MinBlockSize || blockSize > BZip2.MaxBlockSize)
            {
                string msg = string.Format("blockSize={0} is out of range; must be between {1} and {2}", blockSize, BZip2.MinBlockSize, BZip2.MaxBlockSize);
                throw new ArgumentException(msg, "blockSize");
            }
            this.output = output;
            if (!this.output.CanWrite)
            {
                throw new ArgumentException("The stream is not writable.", "output");
            }
            this.bw = new BitWriter(this.output);
            this.blockSize100k = blockSize;
            this.compressor = new BZip2Compressor(this.bw, blockSize);
            this.leaveOpen = leaveOpen;
            this.combinedCRC = 0u;
            this.EmitHeader();
        }

        /// <summary>
        ///   Close the stream.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     This may or may not close the underlying stream.  Check the
        ///     constructors that accept a bool value.
        ///   </para>
        /// </remarks>
        // Token: 0x06000393 RID: 915 RVA: 0x0001A18C File Offset: 0x0001838C
        public override void Close()
        {
            if (this.output != null)
            {
                Stream o = this.output;
                this.Finish();
                if (!this.leaveOpen)
                {
                    o.Close();
                }
            }
        }

        /// <summary>
        ///   Flush the stream.
        /// </summary>
        // Token: 0x06000394 RID: 916 RVA: 0x0001A1C8 File Offset: 0x000183C8
        public override void Flush()
        {
            if (this.output != null)
            {
                this.bw.Flush();
                this.output.Flush();
            }
        }

        // Token: 0x06000395 RID: 917 RVA: 0x0001A204 File Offset: 0x00018404
        private void EmitHeader()
        {
            byte[] array = new byte[]
            {
                66,
                90,
                104,
                0
            };
            array[3] = (byte)(48 + this.blockSize100k);
            byte[] magic = array;
            this.output.Write(magic, 0, magic.Length);
        }

        // Token: 0x06000396 RID: 918 RVA: 0x0001A244 File Offset: 0x00018444
        private void EmitTrailer()
        {
            this.bw.WriteByte(23);
            this.bw.WriteByte(114);
            this.bw.WriteByte(69);
            this.bw.WriteByte(56);
            this.bw.WriteByte(80);
            this.bw.WriteByte(144);
            this.bw.WriteInt(this.combinedCRC);
            this.bw.FinishAndPad();
        }

        // Token: 0x06000397 RID: 919 RVA: 0x0001A2C8 File Offset: 0x000184C8
        private void Finish()
        {
            try
            {
                int totalBefore = this.bw.TotalBytesWrittenOut;
                this.compressor.CompressAndWrite();
                this.combinedCRC = (this.combinedCRC << 1 | this.combinedCRC >> 31);
                this.combinedCRC ^= this.compressor.Crc32;
                this.EmitTrailer();
            }
            finally
            {
                this.output = null;
                this.compressor = null;
                this.bw = null;
            }
        }

        /// <summary>
        ///   The blocksize parameter specified at construction time.
        /// </summary>
        // Token: 0x170000EB RID: 235
        // (get) Token: 0x06000398 RID: 920 RVA: 0x0001A354 File Offset: 0x00018554
        public int BlockSize
        {
            get
            {
                return this.blockSize100k;
            }
        }

        /// <summary>
        ///   Write data to the stream.
        /// </summary>
        /// <remarks>
        ///
        /// <para>
        ///   Use the <c>BZip2OutputStream</c> to compress data while writing:
        ///   create a <c>BZip2OutputStream</c> with a writable output stream.
        ///   Then call <c>Write()</c> on that <c>BZip2OutputStream</c>, providing
        ///   uncompressed data as input.  The data sent to the output stream will
        ///   be the compressed form of the input data.
        /// </para>
        ///
        /// <para>
        ///   A <c>BZip2OutputStream</c> can be used only for <c>Write()</c> not for <c>Read()</c>.
        /// </para>
        ///
        /// </remarks>
        ///
        /// <param name="buffer">The buffer holding data to write to the stream.</param>
        /// <param name="offset">the offset within that data array to find the first byte to write.</param>
        /// <param name="count">the number of bytes to write.</param>
        // Token: 0x06000399 RID: 921 RVA: 0x0001A36C File Offset: 0x0001856C
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (offset < 0)
            {
                throw new IndexOutOfRangeException(string.Format("offset ({0}) must be > 0", offset));
            }
            if (count < 0)
            {
                throw new IndexOutOfRangeException(string.Format("count ({0}) must be > 0", count));
            }
            if (offset + count > buffer.Length)
            {
                throw new IndexOutOfRangeException(string.Format("offset({0}) count({1}) bLength({2})", offset, count, buffer.Length));
            }
            if (this.output == null)
            {
                throw new IOException("the stream is not open");
            }
            if (count != 0)
            {
                int bytesWritten = 0;
                int bytesRemaining = count;
                do
                {
                    int i = this.compressor.Fill(buffer, offset, bytesRemaining);
                    if (i != bytesRemaining)
                    {
                        int totalBefore = this.bw.TotalBytesWrittenOut;
                        this.compressor.CompressAndWrite();
                        this.combinedCRC = (this.combinedCRC << 1 | this.combinedCRC >> 31);
                        this.combinedCRC ^= this.compressor.Crc32;
                        offset += i;
                    }
                    bytesRemaining -= i;
                    bytesWritten += i;
                }
                while (bytesRemaining > 0);
                this.totalBytesWrittenIn += bytesWritten;
            }
        }

        /// <summary>
        /// Indicates whether the stream can be read.
        /// </summary>
        /// <remarks>
        /// The return value is always false.
        /// </remarks>
        // Token: 0x170000EC RID: 236
        // (get) Token: 0x0600039A RID: 922 RVA: 0x0001A4B4 File Offset: 0x000186B4
        public override bool CanRead
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Indicates whether the stream supports Seek operations.
        /// </summary>
        /// <remarks>
        /// Always returns false.
        /// </remarks>
        // Token: 0x170000ED RID: 237
        // (get) Token: 0x0600039B RID: 923 RVA: 0x0001A4C8 File Offset: 0x000186C8
        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Indicates whether the stream can be written.
        /// </summary>
        /// <remarks>
        /// The return value should always be true, unless and until the
        /// object is disposed and closed.
        /// </remarks>
        // Token: 0x170000EE RID: 238
        // (get) Token: 0x0600039C RID: 924 RVA: 0x0001A4DC File Offset: 0x000186DC
        public override bool CanWrite
        {
            get
            {
                if (this.output == null)
                {
                    throw new ObjectDisposedException("BZip2Stream");
                }
                return this.output.CanWrite;
            }
        }

        /// <summary>
        /// Reading this property always throws a <see cref="T:System.NotImplementedException" />.
        /// </summary>
        // Token: 0x170000EF RID: 239
        // (get) Token: 0x0600039D RID: 925 RVA: 0x0001A514 File Offset: 0x00018714
        public override long Length
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// The position of the stream pointer.
        /// </summary>
        ///
        /// <remarks>
        ///   Setting this property always throws a <see cref="T:System.NotImplementedException" />. Reading will return the
        ///   total number of uncompressed bytes written through.
        /// </remarks>
        // Token: 0x170000F0 RID: 240
        // (get) Token: 0x0600039E RID: 926 RVA: 0x0001A51C File Offset: 0x0001871C
        // (set) Token: 0x0600039F RID: 927 RVA: 0x0001A535 File Offset: 0x00018735
        public override long Position
        {
            get
            {
                return (long)this.totalBytesWrittenIn;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Calling this method always throws a <see cref="T:System.NotImplementedException" />.
        /// </summary>
        /// <param name="offset">this is irrelevant, since it will always throw!</param>
        /// <param name="origin">this is irrelevant, since it will always throw!</param>
        /// <returns>irrelevant!</returns>
        // Token: 0x060003A0 RID: 928 RVA: 0x0001A53D File Offset: 0x0001873D
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Calling this method always throws a <see cref="T:System.NotImplementedException" />.
        /// </summary>
        /// <param name="value">this is irrelevant, since it will always throw!</param>
        // Token: 0x060003A1 RID: 929 RVA: 0x0001A545 File Offset: 0x00018745
        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///   Calling this method always throws a <see cref="T:System.NotImplementedException" />.
        /// </summary>
        /// <param name="buffer">this parameter is never used</param>
        /// <param name="offset">this parameter is never used</param>
        /// <param name="count">this parameter is never used</param>
        /// <returns>never returns anything; always throws</returns>
        // Token: 0x060003A2 RID: 930 RVA: 0x0001A54D File Offset: 0x0001874D
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        // Token: 0x060003A3 RID: 931 RVA: 0x0001A558 File Offset: 0x00018758
        [Conditional("Trace")]
        private void TraceOutput(BZip2OutputStream.TraceBits bits, string format, params object[] varParams)
        {
            if ((bits & this.desiredTrace) != BZip2OutputStream.TraceBits.None)
            {
                int tid = Thread.CurrentThread.GetHashCode();
                Console.ForegroundColor = tid % 8 + ConsoleColor.Green;
                Console.Write("{0:000} PBOS ", tid);
                Console.WriteLine(format, varParams);
                Console.ResetColor();
            }
        }

        // Token: 0x04000223 RID: 547
        private int totalBytesWrittenIn;

        // Token: 0x04000224 RID: 548
        private bool leaveOpen;

        // Token: 0x04000225 RID: 549
        private BZip2Compressor compressor;

        // Token: 0x04000226 RID: 550
        private uint combinedCRC;

        // Token: 0x04000227 RID: 551
        private Stream output;

        // Token: 0x04000228 RID: 552
        private BitWriter bw;

        // Token: 0x04000229 RID: 553
        private int blockSize100k;

        // Token: 0x0400022A RID: 554
        private BZip2OutputStream.TraceBits desiredTrace = BZip2OutputStream.TraceBits.Crc | BZip2OutputStream.TraceBits.Write;

        // Token: 0x0200004A RID: 74
        [Flags]
        private enum TraceBits : uint
        {
            // Token: 0x0400022C RID: 556
            None = 0u,
            // Token: 0x0400022D RID: 557
            Crc = 1u,
            // Token: 0x0400022E RID: 558
            Write = 2u,
            // Token: 0x0400022F RID: 559
            All = 4294967295u
        }
    }
}

namespace Ionic.BZip2
{
    /// <summary>
    ///   A write-only decorator stream that compresses data as it is
    ///   written using the BZip2 algorithm. This stream compresses by
    ///   block using multiple threads.
    /// </summary>
    /// <para>
    ///   This class performs BZIP2 compression through writing.  For
    ///   more information on the BZIP2 algorithm, see
    ///   <see href="http://en.wikipedia.org/wiki/BZIP2" />.
    /// </para>
    ///
    /// <para>
    ///   This class is similar to <see cref="T:Ionic.BZip2.BZip2OutputStream" />,
    ///   except that this implementation uses an approach that employs multiple
    ///   worker threads to perform the compression.  On a multi-cpu or multi-core
    ///   computer, the performance of this class can be significantly higher than
    ///   the single-threaded BZip2OutputStream, particularly for larger streams.
    ///   How large?  Anything over 10mb is a good candidate for parallel
    ///   compression.
    /// </para>
    ///
    /// <para>
    ///   The tradeoff is that this class uses more memory and more CPU than the
    ///   vanilla <c>BZip2OutputStream</c>. Also, for small files, the
    ///   <c>ParallelBZip2OutputStream</c> can be much slower than the vanilla
    ///   <c>BZip2OutputStream</c>, because of the overhead associated to using the
    ///   thread pool.
    /// </para>
    ///
    /// <seealso cref="T:Ionic.BZip2.BZip2OutputStream" />
    // Token: 0x0200004C RID: 76
    public class ParallelBZip2OutputStream : Stream
    {
        /// <summary>
        ///   Constructs a new <c>ParallelBZip2OutputStream</c>, that sends its
        ///   compressed output to the given output stream.
        /// </summary>
        ///
        /// <param name="output">
        ///   The destination stream, to which compressed output will be sent.
        /// </param>
        ///
        /// <example>
        ///
        ///   This example reads a file, then compresses it with bzip2 file,
        ///   and writes the compressed data into a newly created file.
        ///
        ///   <code>
        ///   var fname = "logfile.log";
        ///   using (var fs = File.OpenRead(fname))
        ///   {
        ///       var outFname = fname + ".bz2";
        ///       using (var output = File.Create(outFname))
        ///       {
        ///           using (var compressor = new Ionic.BZip2.ParallelBZip2OutputStream(output))
        ///           {
        ///               byte[] buffer = new byte[2048];
        ///               int n;
        ///               while ((n = fs.Read(buffer, 0, buffer.Length)) &gt; 0)
        ///               {
        ///                   compressor.Write(buffer, 0, n);
        ///               }
        ///           }
        ///       }
        ///   }
        ///   </code>
        /// </example>
        // Token: 0x060003A7 RID: 935 RVA: 0x0001A61C File Offset: 0x0001881C
        public ParallelBZip2OutputStream(Stream output) : this(output, BZip2.MaxBlockSize, false)
        {
        }

        /// <summary>
        ///   Constructs a new <c>ParallelBZip2OutputStream</c> with specified blocksize.
        /// </summary>
        /// <param name="output">the destination stream.</param>
        /// <param name="blockSize">
        ///   The blockSize in units of 100000 bytes.
        ///   The valid range is 1..9.
        /// </param>
        // Token: 0x060003A8 RID: 936 RVA: 0x0001A62E File Offset: 0x0001882E
        public ParallelBZip2OutputStream(Stream output, int blockSize) : this(output, blockSize, false)
        {
        }

        /// <summary>
        ///   Constructs a new <c>ParallelBZip2OutputStream</c>.
        /// </summary>
        ///   <param name="output">the destination stream.</param>
        /// <param name="leaveOpen">
        ///   whether to leave the captive stream open upon closing this stream.
        /// </param>
        // Token: 0x060003A9 RID: 937 RVA: 0x0001A63C File Offset: 0x0001883C
        public ParallelBZip2OutputStream(Stream output, bool leaveOpen) : this(output, BZip2.MaxBlockSize, leaveOpen)
        {
        }

        /// <summary>
        ///   Constructs a new <c>ParallelBZip2OutputStream</c> with specified blocksize,
        ///   and explicitly specifies whether to leave the wrapped stream open.
        /// </summary>
        ///
        /// <param name="output">the destination stream.</param>
        /// <param name="blockSize">
        ///   The blockSize in units of 100000 bytes.
        ///   The valid range is 1..9.
        /// </param>
        /// <param name="leaveOpen">
        ///   whether to leave the captive stream open upon closing this stream.
        /// </param>
        // Token: 0x060003AA RID: 938 RVA: 0x0001A650 File Offset: 0x00018850
        public ParallelBZip2OutputStream(Stream output, int blockSize, bool leaveOpen)
        {
            if (blockSize < BZip2.MinBlockSize || blockSize > BZip2.MaxBlockSize)
            {
                string msg = string.Format("blockSize={0} is out of range; must be between {1} and {2}", blockSize, BZip2.MinBlockSize, BZip2.MaxBlockSize);
                throw new ArgumentException(msg, "blockSize");
            }
            this.output = output;
            if (!this.output.CanWrite)
            {
                throw new ArgumentException("The stream is not writable.", "output");
            }
            this.bw = new BitWriter(this.output);
            this.blockSize100k = blockSize;
            this.leaveOpen = leaveOpen;
            this.combinedCRC = 0u;
            this.MaxWorkers = 16;
            this.EmitHeader();
        }

        // Token: 0x060003AB RID: 939 RVA: 0x0001A738 File Offset: 0x00018938
        private void InitializePoolOfWorkItems()
        {
            this.toWrite = new Queue<int>();
            this.toFill = new Queue<int>();
            this.pool = new List<WorkItem>();
            int nWorkers = ParallelBZip2OutputStream.BufferPairsPerCore * Environment.ProcessorCount;
            nWorkers = Math.Min(nWorkers, this.MaxWorkers);
            for (int i = 0; i < nWorkers; i++)
            {
                this.pool.Add(new WorkItem(i, this.blockSize100k));
                this.toFill.Enqueue(i);
            }
            this.newlyCompressedBlob = new AutoResetEvent(false);
            this.currentlyFilling = -1;
            this.lastFilled = -1;
            this.lastWritten = -1;
            this.latestCompressed = -1;
        }

        /// <summary>
        ///   The maximum number of concurrent compression worker threads to use.
        /// </summary>
        ///
        /// <remarks>
        /// <para>
        ///   This property sets an upper limit on the number of concurrent worker
        ///   threads to employ for compression. The implementation of this stream
        ///   employs multiple threads from the .NET thread pool, via <see cref="M:System.Threading.ThreadPool.QueueUserWorkItem(System.Threading.WaitCallback)">
        ///   ThreadPool.QueueUserWorkItem()</see>, to compress the incoming data by
        ///   block.  As each block of data is compressed, this stream re-orders the
        ///   compressed blocks and writes them to the output stream.
        /// </para>
        ///
        /// <para>
        ///   A higher number of workers enables a higher degree of
        ///   parallelism, which tends to increase the speed of compression on
        ///   multi-cpu computers.  On the other hand, a higher number of buffer
        ///   pairs also implies a larger memory consumption, more active worker
        ///   threads, and a higher cpu utilization for any compression. This
        ///   property enables the application to limit its memory consumption and
        ///   CPU utilization behavior depending on requirements.
        /// </para>
        ///
        /// <para>
        ///   By default, DotNetZip allocates 4 workers per CPU core, subject to the
        ///   upper limit specified in this property. For example, suppose the
        ///   application sets this property to 16.  Then, on a machine with 2
        ///   cores, DotNetZip will use 8 workers; that number does not exceed the
        ///   upper limit specified by this property, so the actual number of
        ///   workers used will be 4 * 2 = 8.  On a machine with 4 cores, DotNetZip
        ///   will use 16 workers; again, the limit does not apply. On a machine
        ///   with 8 cores, DotNetZip will use 16 workers, because of the limit.
        /// </para>
        ///
        /// <para>
        ///   For each compression "worker thread" that occurs in parallel, there is
        ///   up to 2mb of memory allocated, for buffering and processing. The
        ///   actual number depends on the <see cref="P:Ionic.BZip2.ParallelBZip2OutputStream.BlockSize" /> property.
        /// </para>
        ///
        /// <para>
        ///   CPU utilization will also go up with additional workers, because a
        ///   larger number of buffer pairs allows a larger number of background
        ///   threads to compress in parallel. If you find that parallel
        ///   compression is consuming too much memory or CPU, you can adjust this
        ///   value downward.
        /// </para>
        ///
        /// <para>
        ///   The default value is 16. Different values may deliver better or
        ///   worse results, depending on your priorities and the dynamic
        ///   performance characteristics of your storage and compute resources.
        /// </para>
        ///
        /// <para>
        ///   The application can set this value at any time, but it is effective
        ///   only before the first call to Write(), which is when the buffers are
        ///   allocated.
        /// </para>
        /// </remarks>
        // Token: 0x170000F2 RID: 242
        // (get) Token: 0x060003AC RID: 940 RVA: 0x0001A7E0 File Offset: 0x000189E0
        // (set) Token: 0x060003AD RID: 941 RVA: 0x0001A7F8 File Offset: 0x000189F8
        public int MaxWorkers
        {
            get
            {
                return this._maxWorkers;
            }
            set
            {
                if (value < 4)
                {
                    throw new ArgumentException("MaxWorkers", "Value must be 4 or greater.");
                }
                this._maxWorkers = value;
            }
        }

        /// <summary>
        ///   Close the stream.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     This may or may not close the underlying stream.  Check the
        ///     constructors that accept a bool value.
        ///   </para>
        /// </remarks>
        // Token: 0x060003AE RID: 942 RVA: 0x0001A828 File Offset: 0x00018A28
        public override void Close()
        {
            if (this.pendingException != null)
            {
                this.handlingException = true;
                Exception pe = this.pendingException;
                this.pendingException = null;
                throw pe;
            }
            if (!this.handlingException)
            {
                if (this.output != null)
                {
                    Stream o = this.output;
                    try
                    {
                        this.FlushOutput(true);
                    }
                    finally
                    {
                        this.output = null;
                        this.bw = null;
                    }
                    if (!this.leaveOpen)
                    {
                        o.Close();
                    }
                }
            }
        }

        // Token: 0x060003AF RID: 943 RVA: 0x0001A8C8 File Offset: 0x00018AC8
        private void FlushOutput(bool lastInput)
        {
            if (!this.emitting)
            {
                if (this.currentlyFilling >= 0)
                {
                    WorkItem workitem = this.pool[this.currentlyFilling];
                    this.CompressOne(workitem);
                    this.currentlyFilling = -1;
                }
                if (lastInput)
                {
                    this.EmitPendingBuffers(true, false);
                    this.EmitTrailer();
                }
                else
                {
                    this.EmitPendingBuffers(false, false);
                }
            }
        }

        /// <summary>
        ///   Flush the stream.
        /// </summary>
        // Token: 0x060003B0 RID: 944 RVA: 0x0001A93C File Offset: 0x00018B3C
        public override void Flush()
        {
            if (this.output != null)
            {
                this.FlushOutput(false);
                this.bw.Flush();
                this.output.Flush();
            }
        }

        // Token: 0x060003B1 RID: 945 RVA: 0x0001A984 File Offset: 0x00018B84
        private void EmitHeader()
        {
            byte[] array = new byte[]
            {
                66,
                90,
                104,
                0
            };
            array[3] = (byte)(48 + this.blockSize100k);
            byte[] magic = array;
            this.output.Write(magic, 0, magic.Length);
        }

        // Token: 0x060003B2 RID: 946 RVA: 0x0001A9C4 File Offset: 0x00018BC4
        private void EmitTrailer()
        {
            this.bw.WriteByte(23);
            this.bw.WriteByte(114);
            this.bw.WriteByte(69);
            this.bw.WriteByte(56);
            this.bw.WriteByte(80);
            this.bw.WriteByte(144);
            this.bw.WriteInt(this.combinedCRC);
            this.bw.FinishAndPad();
        }

        /// <summary>
        ///   The blocksize parameter specified at construction time.
        /// </summary>
        // Token: 0x170000F3 RID: 243
        // (get) Token: 0x060003B3 RID: 947 RVA: 0x0001AA48 File Offset: 0x00018C48
        public int BlockSize
        {
            get
            {
                return this.blockSize100k;
            }
        }

        /// <summary>
        ///   Write data to the stream.
        /// </summary>
        /// <remarks>
        ///
        /// <para>
        ///   Use the <c>ParallelBZip2OutputStream</c> to compress data while
        ///   writing: create a <c>ParallelBZip2OutputStream</c> with a writable
        ///   output stream.  Then call <c>Write()</c> on that
        ///   <c>ParallelBZip2OutputStream</c>, providing uncompressed data as
        ///   input.  The data sent to the output stream will be the compressed
        ///   form of the input data.
        /// </para>
        ///
        /// <para>
        ///   A <c>ParallelBZip2OutputStream</c> can be used only for
        ///   <c>Write()</c> not for <c>Read()</c>.
        /// </para>
        ///
        /// </remarks>
        ///
        /// <param name="buffer">The buffer holding data to write to the stream.</param>
        /// <param name="offset">the offset within that data array to find the first byte to write.</param>
        /// <param name="count">the number of bytes to write.</param>
        // Token: 0x060003B4 RID: 948 RVA: 0x0001AA60 File Offset: 0x00018C60
        public override void Write(byte[] buffer, int offset, int count)
        {
            bool mustWait = false;
            if (this.output == null)
            {
                throw new IOException("the stream is not open");
            }
            if (this.pendingException != null)
            {
                this.handlingException = true;
                Exception pe = this.pendingException;
                this.pendingException = null;
                throw pe;
            }
            if (offset < 0)
            {
                throw new IndexOutOfRangeException(string.Format("offset ({0}) must be > 0", offset));
            }
            if (count < 0)
            {
                throw new IndexOutOfRangeException(string.Format("count ({0}) must be > 0", count));
            }
            if (offset + count > buffer.Length)
            {
                throw new IndexOutOfRangeException(string.Format("offset({0}) count({1}) bLength({2})", offset, count, buffer.Length));
            }
            if (count != 0)
            {
                if (!this.firstWriteDone)
                {
                    this.InitializePoolOfWorkItems();
                    this.firstWriteDone = true;
                }
                int bytesWritten = 0;
                int bytesRemaining = count;
                for (; ; )
                {
                    this.EmitPendingBuffers(false, mustWait);
                    mustWait = false;
                    int ix;
                    if (this.currentlyFilling >= 0)
                    {
                        ix = this.currentlyFilling;
                        goto IL_160;
                    }
                    if (this.toFill.Count != 0)
                    {
                        ix = this.toFill.Dequeue();
                        this.lastFilled++;
                        goto IL_160;
                    }
                    mustWait = true;
                    IL_1E0:
                    if (bytesRemaining <= 0)
                    {
                        goto Block_12;
                    }
                    continue;
                    IL_160:
                    WorkItem workitem = this.pool[ix];
                    workitem.ordinal = this.lastFilled;
                    int i = workitem.Compressor.Fill(buffer, offset, bytesRemaining);
                    if (i != bytesRemaining)
                    {
                        if (!ThreadPool.QueueUserWorkItem(new WaitCallback(this.CompressOne), workitem))
                        {
                            break;
                        }
                        this.currentlyFilling = -1;
                        offset += i;
                    }
                    else
                    {
                        this.currentlyFilling = ix;
                    }
                    bytesRemaining -= i;
                    bytesWritten += i;
                    goto IL_1E0;
                }
                throw new Exception("Cannot enqueue workitem");
                Block_12:
                this.totalBytesWrittenIn += (long)bytesWritten;
            }
        }

        // Token: 0x060003B5 RID: 949 RVA: 0x0001AC6C File Offset: 0x00018E6C
        private void EmitPendingBuffers(bool doAll, bool mustWait)
        {
            if (!this.emitting)
            {
                this.emitting = true;
                if (doAll || mustWait)
                {
                    this.newlyCompressedBlob.WaitOne();
                }
                do
                {
                    int firstSkip = -1;
                    int millisecondsToWait = doAll ? 200 : (mustWait ? -1 : 0);
                    int nextToWrite = -1;
                    for (; ; )
                    {
                        if (Monitor.TryEnter(this.toWrite, millisecondsToWait))
                        {
                            nextToWrite = -1;
                            try
                            {
                                if (this.toWrite.Count > 0)
                                {
                                    nextToWrite = this.toWrite.Dequeue();
                                }
                            }
                            finally
                            {
                                Monitor.Exit(this.toWrite);
                            }
                            if (nextToWrite >= 0)
                            {
                                WorkItem workitem = this.pool[nextToWrite];
                                if (workitem.ordinal != this.lastWritten + 1)
                                {
                                    lock (this.toWrite)
                                    {
                                        this.toWrite.Enqueue(nextToWrite);
                                    }
                                    if (firstSkip == nextToWrite)
                                    {
                                        this.newlyCompressedBlob.WaitOne();
                                        firstSkip = -1;
                                    }
                                    else if (firstSkip == -1)
                                    {
                                        firstSkip = nextToWrite;
                                    }
                                }
                                else
                                {
                                    firstSkip = -1;
                                    BitWriter bw2 = workitem.bw;
                                    bw2.Flush();
                                    MemoryStream ms = workitem.ms;
                                    ms.Seek(0L, SeekOrigin.Begin);
                                    long totOut = 0L;
                                    byte[] buffer = new byte[1024];
                                    int i;
                                    while ((i = ms.Read(buffer, 0, buffer.Length)) > 0)
                                    {
                                        for (int j = 0; j < i; j++)
                                        {
                                            this.bw.WriteByte(buffer[j]);
                                        }
                                        totOut += (long)i;
                                    }
                                    if (bw2.NumRemainingBits > 0)
                                    {
                                        this.bw.WriteBits(bw2.NumRemainingBits, (uint)bw2.RemainingBits);
                                    }
                                    this.combinedCRC = (this.combinedCRC << 1 | this.combinedCRC >> 31);
                                    this.combinedCRC ^= workitem.Compressor.Crc32;
                                    this.totalBytesWrittenOut += totOut;
                                    bw2.Reset();
                                    this.lastWritten = workitem.ordinal;
                                    workitem.ordinal = -1;
                                    this.toFill.Enqueue(workitem.index);
                                    if (millisecondsToWait == -1)
                                    {
                                        millisecondsToWait = 0;
                                    }
                                }
                            }
                        }
                        else
                        {
                            nextToWrite = -1;
                        }
                        IL_26E:
                        if (nextToWrite < 0)
                        {
                            break;
                        }
                        continue;
                        goto IL_26E;
                    }
                }
                while (doAll && this.lastWritten != this.latestCompressed);
                if (doAll)
                {
                }
                this.emitting = false;
            }
        }

        // Token: 0x060003B6 RID: 950 RVA: 0x0001AF48 File Offset: 0x00019148
        private void CompressOne(object wi)
        {
            WorkItem workitem = (WorkItem)wi;
            try
            {
                workitem.Compressor.CompressAndWrite();
                lock (this.latestLock)
                {
                    if (workitem.ordinal > this.latestCompressed)
                    {
                        this.latestCompressed = workitem.ordinal;
                    }
                }
                lock (this.toWrite)
                {
                    this.toWrite.Enqueue(workitem.index);
                }
                this.newlyCompressedBlob.Set();
            }
            catch (Exception exc)
            {
                lock (this.eLock)
                {
                    if (this.pendingException != null)
                    {
                        this.pendingException = exc;
                    }
                }
            }
        }

        /// <summary>
        /// Indicates whether the stream can be read.
        /// </summary>
        /// <remarks>
        /// The return value is always false.
        /// </remarks>
        // Token: 0x170000F4 RID: 244
        // (get) Token: 0x060003B7 RID: 951 RVA: 0x0001B054 File Offset: 0x00019254
        public override bool CanRead
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Indicates whether the stream supports Seek operations.
        /// </summary>
        /// <remarks>
        /// Always returns false.
        /// </remarks>
        // Token: 0x170000F5 RID: 245
        // (get) Token: 0x060003B8 RID: 952 RVA: 0x0001B068 File Offset: 0x00019268
        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Indicates whether the stream can be written.
        /// </summary>
        /// <remarks>
        /// The return value depends on whether the captive stream supports writing.
        /// </remarks>
        // Token: 0x170000F6 RID: 246
        // (get) Token: 0x060003B9 RID: 953 RVA: 0x0001B07C File Offset: 0x0001927C
        public override bool CanWrite
        {
            get
            {
                if (this.output == null)
                {
                    throw new ObjectDisposedException("BZip2Stream");
                }
                return this.output.CanWrite;
            }
        }

        /// <summary>
        /// Reading this property always throws a <see cref="T:System.NotImplementedException" />.
        /// </summary>
        // Token: 0x170000F7 RID: 247
        // (get) Token: 0x060003BA RID: 954 RVA: 0x0001B0B4 File Offset: 0x000192B4
        public override long Length
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// The position of the stream pointer.
        /// </summary>
        ///
        /// <remarks>
        ///   Setting this property always throws a <see cref="T:System.NotImplementedException" />. Reading will return the
        ///   total number of uncompressed bytes written through.
        /// </remarks>
        // Token: 0x170000F8 RID: 248
        // (get) Token: 0x060003BB RID: 955 RVA: 0x0001B0BC File Offset: 0x000192BC
        // (set) Token: 0x060003BC RID: 956 RVA: 0x0001B0D4 File Offset: 0x000192D4
        public override long Position
        {
            get
            {
                return this.totalBytesWrittenIn;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// The total number of bytes written out by the stream.
        /// </summary>
        /// <remarks>
        /// This value is meaningful only after a call to Close().
        /// </remarks>
        // Token: 0x170000F9 RID: 249
        // (get) Token: 0x060003BD RID: 957 RVA: 0x0001B0DC File Offset: 0x000192DC
        public long BytesWrittenOut
        {
            get
            {
                return this.totalBytesWrittenOut;
            }
        }

        /// <summary>
        /// Calling this method always throws a <see cref="T:System.NotImplementedException" />.
        /// </summary>
        /// <param name="offset">this is irrelevant, since it will always throw!</param>
        /// <param name="origin">this is irrelevant, since it will always throw!</param>
        /// <returns>irrelevant!</returns>
        // Token: 0x060003BE RID: 958 RVA: 0x0001B0F4 File Offset: 0x000192F4
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Calling this method always throws a <see cref="T:System.NotImplementedException" />.
        /// </summary>
        /// <param name="value">this is irrelevant, since it will always throw!</param>
        // Token: 0x060003BF RID: 959 RVA: 0x0001B0FC File Offset: 0x000192FC
        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Calling this method always throws a <see cref="T:System.NotImplementedException" />.
        /// </summary>
        /// <param name="buffer">this parameter is never used</param>
        /// <param name="offset">this parameter is never used</param>
        /// <param name="count">this parameter is never used</param>
        /// <returns>never returns anything; always throws</returns>
        // Token: 0x060003C0 RID: 960 RVA: 0x0001B104 File Offset: 0x00019304
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        // Token: 0x060003C1 RID: 961 RVA: 0x0001B10C File Offset: 0x0001930C
        [Conditional("Trace")]
        private void TraceOutput(ParallelBZip2OutputStream.TraceBits bits, string format, params object[] varParams)
        {
            if ((bits & this.desiredTrace) != ParallelBZip2OutputStream.TraceBits.None)
            {
                lock (this.outputLock)
                {
                    int tid = Thread.CurrentThread.GetHashCode();
                    Console.ForegroundColor = tid % 8 + ConsoleColor.Green;
                    Console.Write("{0:000} PBOS ", tid);
                    Console.WriteLine(format, varParams);
                    Console.ResetColor();
                }
            }
        }

        // Token: 0x04000235 RID: 565
        private static readonly int BufferPairsPerCore = 4;

        // Token: 0x04000236 RID: 566
        private int _maxWorkers;

        // Token: 0x04000237 RID: 567
        private bool firstWriteDone;

        // Token: 0x04000238 RID: 568
        private int lastFilled;

        // Token: 0x04000239 RID: 569
        private int lastWritten;

        // Token: 0x0400023A RID: 570
        private int latestCompressed;

        // Token: 0x0400023B RID: 571
        private int currentlyFilling;

        // Token: 0x0400023C RID: 572
        private volatile Exception pendingException;

        // Token: 0x0400023D RID: 573
        private bool handlingException;

        // Token: 0x0400023E RID: 574
        private bool emitting;

        // Token: 0x0400023F RID: 575
        private Queue<int> toWrite;

        // Token: 0x04000240 RID: 576
        private Queue<int> toFill;

        // Token: 0x04000241 RID: 577
        private List<WorkItem> pool;

        // Token: 0x04000242 RID: 578
        private object latestLock = new object();

        // Token: 0x04000243 RID: 579
        private object eLock = new object();

        // Token: 0x04000244 RID: 580
        private object outputLock = new object();

        // Token: 0x04000245 RID: 581
        private AutoResetEvent newlyCompressedBlob;

        // Token: 0x04000246 RID: 582
        private long totalBytesWrittenIn;

        // Token: 0x04000247 RID: 583
        private long totalBytesWrittenOut;

        // Token: 0x04000248 RID: 584
        private bool leaveOpen;

        // Token: 0x04000249 RID: 585
        private uint combinedCRC;

        // Token: 0x0400024A RID: 586
        private Stream output;

        // Token: 0x0400024B RID: 587
        private BitWriter bw;

        // Token: 0x0400024C RID: 588
        private int blockSize100k;

        // Token: 0x0400024D RID: 589
        private ParallelBZip2OutputStream.TraceBits desiredTrace = ParallelBZip2OutputStream.TraceBits.Crc | ParallelBZip2OutputStream.TraceBits.Write;

        // Token: 0x0200004D RID: 77
        [Flags]
        private enum TraceBits : uint
        {
            // Token: 0x0400024F RID: 591
            None = 0u,
            // Token: 0x04000250 RID: 592
            Crc = 1u,
            // Token: 0x04000251 RID: 593
            Write = 2u,
            // Token: 0x04000252 RID: 594
            All = 4294967295u
        }
    }
}

namespace Ionic.BZip2
{
    // Token: 0x0200004E RID: 78
    internal static class Rand
    {
        /// <summary>
        ///   Returns the "random" number at a specific index.
        /// </summary>
        /// <param name="i">the index</param>
        /// <returns>the random number</returns>
        // Token: 0x060003C3 RID: 963 RVA: 0x0001B194 File Offset: 0x00019394
        internal static int Rnums(int i)
        {
            return Rand.RNUMS[i];
        }

        // Token: 0x04000253 RID: 595
        private static int[] RNUMS = new int[]
        {
            619,
            720,
            127,
            481,
            931,
            816,
            813,
            233,
            566,
            247,
            985,
            724,
            205,
            454,
            863,
            491,
            741,
            242,
            949,
            214,
            733,
            859,
            335,
            708,
            621,
            574,
            73,
            654,
            730,
            472,
            419,
            436,
            278,
            496,
            867,
            210,
            399,
            680,
            480,
            51,
            878,
            465,
            811,
            169,
            869,
            675,
            611,
            697,
            867,
            561,
            862,
            687,
            507,
            283,
            482,
            129,
            807,
            591,
            733,
            623,
            150,
            238,
            59,
            379,
            684,
            877,
            625,
            169,
            643,
            105,
            170,
            607,
            520,
            932,
            727,
            476,
            693,
            425,
            174,
            647,
            73,
            122,
            335,
            530,
            442,
            853,
            695,
            249,
            445,
            515,
            909,
            545,
            703,
            919,
            874,
            474,
            882,
            500,
            594,
            612,
            641,
            801,
            220,
            162,
            819,
            984,
            589,
            513,
            495,
            799,
            161,
            604,
            958,
            533,
            221,
            400,
            386,
            867,
            600,
            782,
            382,
            596,
            414,
            171,
            516,
            375,
            682,
            485,
            911,
            276,
            98,
            553,
            163,
            354,
            666,
            933,
            424,
            341,
            533,
            870,
            227,
            730,
            475,
            186,
            263,
            647,
            537,
            686,
            600,
            224,
            469,
            68,
            770,
            919,
            190,
            373,
            294,
            822,
            808,
            206,
            184,
            943,
            795,
            384,
            383,
            461,
            404,
            758,
            839,
            887,
            715,
            67,
            618,
            276,
            204,
            918,
            873,
            777,
            604,
            560,
            951,
            160,
            578,
            722,
            79,
            804,
            96,
            409,
            713,
            940,
            652,
            934,
            970,
            447,
            318,
            353,
            859,
            672,
            112,
            785,
            645,
            863,
            803,
            350,
            139,
            93,
            354,
            99,
            820,
            908,
            609,
            772,
            154,
            274,
            580,
            184,
            79,
            626,
            630,
            742,
            653,
            282,
            762,
            623,
            680,
            81,
            927,
            626,
            789,
            125,
            411,
            521,
            938,
            300,
            821,
            78,
            343,
            175,
            128,
            250,
            170,
            774,
            972,
            275,
            999,
            639,
            495,
            78,
            352,
            126,
            857,
            956,
            358,
            619,
            580,
            124,
            737,
            594,
            701,
            612,
            669,
            112,
            134,
            694,
            363,
            992,
            809,
            743,
            168,
            974,
            944,
            375,
            748,
            52,
            600,
            747,
            642,
            182,
            862,
            81,
            344,
            805,
            988,
            739,
            511,
            655,
            814,
            334,
            249,
            515,
            897,
            955,
            664,
            981,
            649,
            113,
            974,
            459,
            893,
            228,
            433,
            837,
            553,
            268,
            926,
            240,
            102,
            654,
            459,
            51,
            686,
            754,
            806,
            760,
            493,
            403,
            415,
            394,
            687,
            700,
            946,
            670,
            656,
            610,
            738,
            392,
            760,
            799,
            887,
            653,
            978,
            321,
            576,
            617,
            626,
            502,
            894,
            679,
            243,
            440,
            680,
            879,
            194,
            572,
            640,
            724,
            926,
            56,
            204,
            700,
            707,
            151,
            457,
            449,
            797,
            195,
            791,
            558,
            945,
            679,
            297,
            59,
            87,
            824,
            713,
            663,
            412,
            693,
            342,
            606,
            134,
            108,
            571,
            364,
            631,
            212,
            174,
            643,
            304,
            329,
            343,
            97,
            430,
            751,
            497,
            314,
            983,
            374,
            822,
            928,
            140,
            206,
            73,
            263,
            980,
            736,
            876,
            478,
            430,
            305,
            170,
            514,
            364,
            692,
            829,
            82,
            855,
            953,
            676,
            246,
            369,
            970,
            294,
            750,
            807,
            827,
            150,
            790,
            288,
            923,
            804,
            378,
            215,
            828,
            592,
            281,
            565,
            555,
            710,
            82,
            896,
            831,
            547,
            261,
            524,
            462,
            293,
            465,
            502,
            56,
            661,
            821,
            976,
            991,
            658,
            869,
            905,
            758,
            745,
            193,
            768,
            550,
            608,
            933,
            378,
            286,
            215,
            979,
            792,
            961,
            61,
            688,
            793,
            644,
            986,
            403,
            106,
            366,
            905,
            644,
            372,
            567,
            466,
            434,
            645,
            210,
            389,
            550,
            919,
            135,
            780,
            773,
            635,
            389,
            707,
            100,
            626,
            958,
            165,
            504,
            920,
            176,
            193,
            713,
            857,
            265,
            203,
            50,
            668,
            108,
            645,
            990,
            626,
            197,
            510,
            357,
            358,
            850,
            858,
            364,
            936,
            638
        };
    }
}

namespace Ionic.BZip2
{
    // Token: 0x0200004B RID: 75
    internal class WorkItem
    {
        // Token: 0x170000F1 RID: 241
        // (get) Token: 0x060003A4 RID: 932 RVA: 0x0001A5B0 File Offset: 0x000187B0
        // (set) Token: 0x060003A5 RID: 933 RVA: 0x0001A5C7 File Offset: 0x000187C7
        public BZip2Compressor Compressor { get; private set; }

        // Token: 0x060003A6 RID: 934 RVA: 0x0001A5D0 File Offset: 0x000187D0
        public WorkItem(int ix, int blockSize)
        {
            this.ms = new MemoryStream();
            this.bw = new BitWriter(this.ms);
            this.Compressor = new BZip2Compressor(this.bw, blockSize);
            this.index = ix;
        }

        // Token: 0x04000230 RID: 560
        public int index;

        // Token: 0x04000231 RID: 561
        public MemoryStream ms;

        // Token: 0x04000232 RID: 562
        public int ordinal;

        // Token: 0x04000233 RID: 563
        public BitWriter bw;
    }
}

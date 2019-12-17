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

namespace Ionic.Zlib
{
    /// <summary>
    /// Computes an Adler-32 checksum.
    /// </summary>
    /// <remarks>
    /// The Adler checksum is similar to a CRC checksum, but faster to compute, though less
    /// reliable.  It is used in producing RFC1950 compressed streams.  The Adler checksum
    /// is a required part of the "ZLIB" standard.  Applications will almost never need to
    /// use this class directly.
    /// </remarks>
    ///
    /// <exclude />
    // Token: 0x02000069 RID: 105
    public sealed class Adler
    {
        /// <summary>
        ///   Calculates the Adler32 checksum.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     This is used within ZLIB.  You probably don't need to use this directly.
        ///   </para>
        /// </remarks>
        /// <example>
        ///    To compute an Adler32 checksum on a byte array:
        ///  <code>
        ///    var adler = Adler.Adler32(0, null, 0, 0);
        ///    adler = Adler.Adler32(adler, buffer, index, length);
        ///  </code>
        /// </example>
        // Token: 0x06000488 RID: 1160 RVA: 0x00026E50 File Offset: 0x00025050
        public static uint Adler32(uint adler, byte[] buf, int index, int len)
        {
            uint result;
            if (buf == null)
            {
                result = 1u;
            }
            else
            {
                uint s = adler & 65535u;
                uint s2 = adler >> 16 & 65535u;
                while (len > 0)
                {
                    int i = (len < Adler.NMAX) ? len : Adler.NMAX;
                    len -= i;
                    while (i >= 16)
                    {
                        s += (uint)buf[index++];
                        s2 += s;
                        s += (uint)buf[index++];
                        s2 += s;
                        s += (uint)buf[index++];
                        s2 += s;
                        s += (uint)buf[index++];
                        s2 += s;
                        s += (uint)buf[index++];
                        s2 += s;
                        s += (uint)buf[index++];
                        s2 += s;
                        s += (uint)buf[index++];
                        s2 += s;
                        s += (uint)buf[index++];
                        s2 += s;
                        s += (uint)buf[index++];
                        s2 += s;
                        s += (uint)buf[index++];
                        s2 += s;
                        s += (uint)buf[index++];
                        s2 += s;
                        s += (uint)buf[index++];
                        s2 += s;
                        s += (uint)buf[index++];
                        s2 += s;
                        s += (uint)buf[index++];
                        s2 += s;
                        s += (uint)buf[index++];
                        s2 += s;
                        s += (uint)buf[index++];
                        s2 += s;
                        i -= 16;
                    }
                    if (i != 0)
                    {
                        do
                        {
                            s += (uint)buf[index++];
                            s2 += s;
                        }
                        while (--i != 0);
                    }
                    s %= Adler.BASE;
                    s2 %= Adler.BASE;
                }
                result = (s2 << 16 | s);
            }
            return result;
        }

        // Token: 0x0400039C RID: 924
        private static readonly uint BASE = 65521u;

        // Token: 0x0400039D RID: 925
        private static readonly int NMAX = 5552;
    }
}




namespace Ionic.Zlib
{
    // Token: 0x0200004F RID: 79
    internal enum BlockState
    {
        // Token: 0x04000255 RID: 597
        NeedMore,
        // Token: 0x04000256 RID: 598
        BlockDone,
        // Token: 0x04000257 RID: 599
        FinishStarted,
        // Token: 0x04000258 RID: 600
        FinishDone
    }
}


namespace Ionic.Zlib
{
    /// <summary>
    /// The compression level to be used when using a DeflateStream or ZlibStream with CompressionMode.Compress.
    /// </summary>
    // Token: 0x02000062 RID: 98
    public enum CompressionLevel
    {
        /// <summary>
        /// None means that the data will be simply stored, with no change at all.
        /// If you are producing ZIPs for use on Mac OSX, be aware that archives produced with CompressionLevel.None
        /// cannot be opened with the default zip reader. Use a different CompressionLevel.
        /// </summary>
        // Token: 0x04000373 RID: 883
        None,
        /// <summary>
        /// Same as None.
        /// </summary>
        // Token: 0x04000374 RID: 884
        Level0 = 0,
        /// <summary>
        /// The fastest but least effective compression.
        /// </summary>
        // Token: 0x04000375 RID: 885
        BestSpeed,
        /// <summary>
        /// A synonym for BestSpeed.
        /// </summary>
        // Token: 0x04000376 RID: 886
        Level1 = 1,
        /// <summary>
        /// A little slower, but better, than level 1.
        /// </summary>
        // Token: 0x04000377 RID: 887
        Level2,
        /// <summary>
        /// A little slower, but better, than level 2.
        /// </summary>
        // Token: 0x04000378 RID: 888
        Level3,
        /// <summary>
        /// A little slower, but better, than level 3.
        /// </summary>
        // Token: 0x04000379 RID: 889
        Level4,
        /// <summary>
        /// A little slower than level 4, but with better compression.
        /// </summary>
        // Token: 0x0400037A RID: 890
        Level5,
        /// <summary>
        /// The default compression level, with a good balance of speed and compression efficiency.
        /// </summary>
        // Token: 0x0400037B RID: 891
        Default,
        /// <summary>
        /// A synonym for Default.
        /// </summary>
        // Token: 0x0400037C RID: 892
        Level6 = 6,
        /// <summary>
        /// Pretty good compression!
        /// </summary>
        // Token: 0x0400037D RID: 893
        Level7,
        /// <summary>
        ///  Better compression than Level7!
        /// </summary>
        // Token: 0x0400037E RID: 894
        Level8,
        /// <summary>
        /// The "best" compression, where best means greatest reduction in size of the input data stream.
        /// This is also the slowest compression.
        /// </summary>
        // Token: 0x0400037F RID: 895
        BestCompression,
        /// <summary>
        /// A synonym for BestCompression.
        /// </summary>
        // Token: 0x04000380 RID: 896
        Level9 = 9
    }
}


namespace Ionic.Zlib
{
    /// <summary>
    /// An enum to specify the direction of transcoding - whether to compress or decompress.
    /// </summary>
    // Token: 0x02000064 RID: 100
    public enum CompressionMode
    {
        /// <summary>
        /// Used to specify that the stream should compress the data.
        /// </summary>
        // Token: 0x04000386 RID: 902
        Compress,
        /// <summary>
        /// Used to specify that the stream should decompress the data.
        /// </summary>
        // Token: 0x04000387 RID: 903
        Decompress
    }
}


namespace Ionic.Zlib
{
    /// <summary>
    /// Describes options for how the compression algorithm is executed.  Different strategies
    /// work better on different sorts of data.  The strategy parameter can affect the compression
    /// ratio and the speed of compression but not the correctness of the compresssion.
    /// </summary>
    // Token: 0x02000063 RID: 99
    public enum CompressionStrategy
    {
        /// <summary>
        /// The default strategy is probably the best for normal data.
        /// </summary>
        // Token: 0x04000382 RID: 898
        Default,
        /// <summary>
        /// The <c>Filtered</c> strategy is intended to be used most effectively with data produced by a
        /// filter or predictor.  By this definition, filtered data consists mostly of small
        /// values with a somewhat random distribution.  In this case, the compression algorithm
        /// is tuned to compress them better.  The effect of <c>Filtered</c> is to force more Huffman
        /// coding and less string matching; it is a half-step between <c>Default</c> and <c>HuffmanOnly</c>.
        /// </summary>
        // Token: 0x04000383 RID: 899
        Filtered,
        /// <summary>
        /// Using <c>HuffmanOnly</c> will force the compressor to do Huffman encoding only, with no
        /// string matching.
        /// </summary>
        // Token: 0x04000384 RID: 900
        HuffmanOnly
    }
}


namespace Ionic.Zlib
{
    // Token: 0x02000050 RID: 80
    internal enum DeflateFlavor
    {
        // Token: 0x0400025A RID: 602
        Store,
        // Token: 0x0400025B RID: 603
        Fast,
        // Token: 0x0400025C RID: 604
        Slow
    }
}


namespace Ionic.Zlib
{
    // Token: 0x02000051 RID: 81
    internal sealed class DeflateManager
    {
        // Token: 0x060003C5 RID: 965 RVA: 0x0001B9CC File Offset: 0x00019BCC
        internal DeflateManager()
        {
            this.dyn_ltree = new short[DeflateManager.HEAP_SIZE * 2];
            this.dyn_dtree = new short[(2 * InternalConstants.D_CODES + 1) * 2];
            this.bl_tree = new short[(2 * InternalConstants.BL_CODES + 1) * 2];
        }

        // Token: 0x060003C6 RID: 966 RVA: 0x0001BA8C File Offset: 0x00019C8C
        private void _InitializeLazyMatch()
        {
            this.window_size = 2 * this.w_size;
            Array.Clear(this.head, 0, this.hash_size);
            this.config = DeflateManager.Config.Lookup(this.compressionLevel);
            this.SetDeflater();
            this.strstart = 0;
            this.block_start = 0;
            this.lookahead = 0;
            this.match_length = (this.prev_length = DeflateManager.MIN_MATCH - 1);
            this.match_available = 0;
            this.ins_h = 0;
        }

        // Token: 0x060003C7 RID: 967 RVA: 0x0001BB0C File Offset: 0x00019D0C
        private void _InitializeTreeData()
        {
            this.treeLiterals.dyn_tree = this.dyn_ltree;
            this.treeLiterals.staticTree = StaticTree.Literals;
            this.treeDistances.dyn_tree = this.dyn_dtree;
            this.treeDistances.staticTree = StaticTree.Distances;
            this.treeBitLengths.dyn_tree = this.bl_tree;
            this.treeBitLengths.staticTree = StaticTree.BitLengths;
            this.bi_buf = 0;
            this.bi_valid = 0;
            this.last_eob_len = 8;
            this._InitializeBlocks();
        }

        // Token: 0x060003C8 RID: 968 RVA: 0x0001BB9C File Offset: 0x00019D9C
        internal void _InitializeBlocks()
        {
            for (int i = 0; i < InternalConstants.L_CODES; i++)
            {
                this.dyn_ltree[i * 2] = 0;
            }
            for (int i = 0; i < InternalConstants.D_CODES; i++)
            {
                this.dyn_dtree[i * 2] = 0;
            }
            for (int i = 0; i < InternalConstants.BL_CODES; i++)
            {
                this.bl_tree[i * 2] = 0;
            }
            this.dyn_ltree[DeflateManager.END_BLOCK * 2] = 1;
            this.opt_len = (this.static_len = 0);
            this.last_lit = (this.matches = 0);
        }

        // Token: 0x060003C9 RID: 969 RVA: 0x0001BC38 File Offset: 0x00019E38
        internal void pqdownheap(short[] tree, int k)
        {
            int v = this.heap[k];
            for (int i = k << 1; i <= this.heap_len; i <<= 1)
            {
                if (i < this.heap_len && DeflateManager._IsSmaller(tree, this.heap[i + 1], this.heap[i], this.depth))
                {
                    i++;
                }
                if (DeflateManager._IsSmaller(tree, v, this.heap[i], this.depth))
                {
                    break;
                }
                this.heap[k] = this.heap[i];
                k = i;
            }
            this.heap[k] = v;
        }

        // Token: 0x060003CA RID: 970 RVA: 0x0001BCDC File Offset: 0x00019EDC
        internal static bool _IsSmaller(short[] tree, int n, int m, sbyte[] depth)
        {
            short tn2 = tree[n * 2];
            short tm2 = tree[m * 2];
            return tn2 < tm2 || (tn2 == tm2 && depth[n] <= depth[m]);
        }

        // Token: 0x060003CB RID: 971 RVA: 0x0001BD14 File Offset: 0x00019F14
        internal void scan_tree(short[] tree, int max_code)
        {
            int prevlen = -1;
            int nextlen = (int)tree[1];
            int count = 0;
            int max_count = 7;
            int min_count = 4;
            if (nextlen == 0)
            {
                max_count = 138;
                min_count = 3;
            }
            tree[(max_code + 1) * 2 + 1] = short.MaxValue;
            for (int i = 0; i <= max_code; i++)
            {
                int curlen = nextlen;
                nextlen = (int)tree[(i + 1) * 2 + 1];
                if (++count >= max_count || curlen != nextlen)
                {
                    if (count < min_count)
                    {
                        this.bl_tree[curlen * 2] = (short)((int)this.bl_tree[curlen * 2] + count);
                    }
                    else if (curlen != 0)
                    {
                        if (curlen != prevlen)
                        {
                            short[] array = this.bl_tree;
                            int num = curlen * 2;
                            array[num] += 1;
                        }
                        short[] array2 = this.bl_tree;
                        int num2 = InternalConstants.REP_3_6 * 2;
                        array2[num2] += 1;
                    }
                    else if (count <= 10)
                    {
                        short[] array3 = this.bl_tree;
                        int num3 = InternalConstants.REPZ_3_10 * 2;
                        array3[num3] += 1;
                    }
                    else
                    {
                        short[] array4 = this.bl_tree;
                        int num4 = InternalConstants.REPZ_11_138 * 2;
                        array4[num4] += 1;
                    }
                    count = 0;
                    prevlen = curlen;
                    if (nextlen == 0)
                    {
                        max_count = 138;
                        min_count = 3;
                    }
                    else if (curlen == nextlen)
                    {
                        max_count = 6;
                        min_count = 3;
                    }
                    else
                    {
                        max_count = 7;
                        min_count = 4;
                    }
                }
            }
        }

        // Token: 0x060003CC RID: 972 RVA: 0x0001BEB8 File Offset: 0x0001A0B8
        internal int build_bl_tree()
        {
            this.scan_tree(this.dyn_ltree, this.treeLiterals.max_code);
            this.scan_tree(this.dyn_dtree, this.treeDistances.max_code);
            this.treeBitLengths.build_tree(this);
            int max_blindex;
            for (max_blindex = InternalConstants.BL_CODES - 1; max_blindex >= 3; max_blindex--)
            {
                if (this.bl_tree[(int)(Tree.bl_order[max_blindex] * 2 + 1)] != 0)
                {
                    break;
                }
            }
            this.opt_len += 3 * (max_blindex + 1) + 5 + 5 + 4;
            return max_blindex;
        }

        // Token: 0x060003CD RID: 973 RVA: 0x0001BF58 File Offset: 0x0001A158
        internal void send_all_trees(int lcodes, int dcodes, int blcodes)
        {
            this.send_bits(lcodes - 257, 5);
            this.send_bits(dcodes - 1, 5);
            this.send_bits(blcodes - 4, 4);
            for (int rank = 0; rank < blcodes; rank++)
            {
                this.send_bits((int)this.bl_tree[(int)(Tree.bl_order[rank] * 2 + 1)], 3);
            }
            this.send_tree(this.dyn_ltree, lcodes - 1);
            this.send_tree(this.dyn_dtree, dcodes - 1);
        }

        // Token: 0x060003CE RID: 974 RVA: 0x0001BFD8 File Offset: 0x0001A1D8
        internal void send_tree(short[] tree, int max_code)
        {
            int prevlen = -1;
            int nextlen = (int)tree[1];
            int count = 0;
            int max_count = 7;
            int min_count = 4;
            if (nextlen == 0)
            {
                max_count = 138;
                min_count = 3;
            }
            for (int i = 0; i <= max_code; i++)
            {
                int curlen = nextlen;
                nextlen = (int)tree[(i + 1) * 2 + 1];
                if (++count >= max_count || curlen != nextlen)
                {
                    if (count < min_count)
                    {
                        do
                        {
                            this.send_code(curlen, this.bl_tree);
                        }
                        while (--count != 0);
                    }
                    else if (curlen != 0)
                    {
                        if (curlen != prevlen)
                        {
                            this.send_code(curlen, this.bl_tree);
                            count--;
                        }
                        this.send_code(InternalConstants.REP_3_6, this.bl_tree);
                        this.send_bits(count - 3, 2);
                    }
                    else if (count <= 10)
                    {
                        this.send_code(InternalConstants.REPZ_3_10, this.bl_tree);
                        this.send_bits(count - 3, 3);
                    }
                    else
                    {
                        this.send_code(InternalConstants.REPZ_11_138, this.bl_tree);
                        this.send_bits(count - 11, 7);
                    }
                    count = 0;
                    prevlen = curlen;
                    if (nextlen == 0)
                    {
                        max_count = 138;
                        min_count = 3;
                    }
                    else if (curlen == nextlen)
                    {
                        max_count = 6;
                        min_count = 3;
                    }
                    else
                    {
                        max_count = 7;
                        min_count = 4;
                    }
                }
            }
        }

        // Token: 0x060003CF RID: 975 RVA: 0x0001C16B File Offset: 0x0001A36B
        private void put_bytes(byte[] p, int start, int len)
        {
            Array.Copy(p, start, this.pending, this.pendingCount, len);
            this.pendingCount += len;
        }

        // Token: 0x060003D0 RID: 976 RVA: 0x0001C194 File Offset: 0x0001A394
        internal void send_code(int c, short[] tree)
        {
            int c2 = c * 2;
            this.send_bits((int)tree[c2] & 65535, (int)tree[c2 + 1] & 65535);
        }

        // Token: 0x060003D1 RID: 977 RVA: 0x0001C1C4 File Offset: 0x0001A3C4
        internal void send_bits(int value, int length)
        {
            if (this.bi_valid > DeflateManager.Buf_size - length)
            {
                this.bi_buf |= (short)(value << this.bi_valid & 65535);
                this.pending[this.pendingCount++] = (byte)this.bi_buf;
                this.pending[this.pendingCount++] = (byte)(this.bi_buf >> 8);
                this.bi_buf = (short)((uint)value >> DeflateManager.Buf_size - this.bi_valid);
                this.bi_valid += length - DeflateManager.Buf_size;
            }
            else
            {
                this.bi_buf |= (short)(value << this.bi_valid & 65535);
                this.bi_valid += length;
            }
        }

        // Token: 0x060003D2 RID: 978 RVA: 0x0001C2B0 File Offset: 0x0001A4B0
        internal void _tr_align()
        {
            this.send_bits(DeflateManager.STATIC_TREES << 1, 3);
            this.send_code(DeflateManager.END_BLOCK, StaticTree.lengthAndLiteralsTreeCodes);
            this.bi_flush();
            if (1 + this.last_eob_len + 10 - this.bi_valid < 9)
            {
                this.send_bits(DeflateManager.STATIC_TREES << 1, 3);
                this.send_code(DeflateManager.END_BLOCK, StaticTree.lengthAndLiteralsTreeCodes);
                this.bi_flush();
            }
            this.last_eob_len = 7;
        }

        // Token: 0x060003D3 RID: 979 RVA: 0x0001C334 File Offset: 0x0001A534
        internal bool _tr_tally(int dist, int lc)
        {
            this.pending[this._distanceOffset + this.last_lit * 2] = (byte)((uint)dist >> 8);
            this.pending[this._distanceOffset + this.last_lit * 2 + 1] = (byte)dist;
            this.pending[this._lengthOffset + this.last_lit] = (byte)lc;
            this.last_lit++;
            if (dist == 0)
            {
                short[] array = this.dyn_ltree;
                int num = lc * 2;
                array[num] += 1;
            }
            else
            {
                this.matches++;
                dist--;
                short[] array2 = this.dyn_ltree;
                int num2 = ((int)Tree.LengthCode[lc] + InternalConstants.LITERALS + 1) * 2;
                array2[num2] += 1;
                short[] array3 = this.dyn_dtree;
                int num3 = Tree.DistanceCode(dist) * 2;
                array3[num3] += 1;
            }
            if ((this.last_lit & 8191) == 0 && this.compressionLevel > CompressionLevel.Level2)
            {
                int out_length = this.last_lit << 3;
                int in_length = this.strstart - this.block_start;
                for (int dcode = 0; dcode < InternalConstants.D_CODES; dcode++)
                {
                    out_length = (int)((long)out_length + (long)this.dyn_dtree[dcode * 2] * (5L + (long)Tree.ExtraDistanceBits[dcode]));
                }
                out_length >>= 3;
                if (this.matches < this.last_lit / 2 && out_length < in_length / 2)
                {
                    return true;
                }
            }
            return this.last_lit == this.lit_bufsize - 1 || this.last_lit == this.lit_bufsize;
        }

        // Token: 0x060003D4 RID: 980 RVA: 0x0001C4E8 File Offset: 0x0001A6E8
        internal void send_compressed_block(short[] ltree, short[] dtree)
        {
            int lx = 0;
            if (this.last_lit != 0)
            {
                do
                {
                    int ix = this._distanceOffset + lx * 2;
                    int distance = ((int)this.pending[ix] << 8 & 65280) | (int)(this.pending[ix + 1] & byte.MaxValue);
                    int lc = (int)(this.pending[this._lengthOffset + lx] & byte.MaxValue);
                    lx++;
                    if (distance == 0)
                    {
                        this.send_code(lc, ltree);
                    }
                    else
                    {
                        int code = (int)Tree.LengthCode[lc];
                        this.send_code(code + InternalConstants.LITERALS + 1, ltree);
                        int extra = Tree.ExtraLengthBits[code];
                        if (extra != 0)
                        {
                            lc -= Tree.LengthBase[code];
                            this.send_bits(lc, extra);
                        }
                        distance--;
                        code = Tree.DistanceCode(distance);
                        this.send_code(code, dtree);
                        extra = Tree.ExtraDistanceBits[code];
                        if (extra != 0)
                        {
                            distance -= Tree.DistanceBase[code];
                            this.send_bits(distance, extra);
                        }
                    }
                }
                while (lx < this.last_lit);
            }
            this.send_code(DeflateManager.END_BLOCK, ltree);
            this.last_eob_len = (int)ltree[DeflateManager.END_BLOCK * 2 + 1];
        }

        // Token: 0x060003D5 RID: 981 RVA: 0x0001C628 File Offset: 0x0001A828
        internal void set_data_type()
        {
            int i = 0;
            int ascii_freq = 0;
            int bin_freq = 0;
            while (i < 7)
            {
                bin_freq += (int)this.dyn_ltree[i * 2];
                i++;
            }
            while (i < 128)
            {
                ascii_freq += (int)this.dyn_ltree[i * 2];
                i++;
            }
            while (i < InternalConstants.LITERALS)
            {
                bin_freq += (int)this.dyn_ltree[i * 2];
                i++;
            }
            this.data_type = (sbyte)((bin_freq > ascii_freq >> 2) ? DeflateManager.Z_BINARY : DeflateManager.Z_ASCII);
        }

        // Token: 0x060003D6 RID: 982 RVA: 0x0001C6B4 File Offset: 0x0001A8B4
        internal void bi_flush()
        {
            if (this.bi_valid == 16)
            {
                this.pending[this.pendingCount++] = (byte)this.bi_buf;
                this.pending[this.pendingCount++] = (byte)(this.bi_buf >> 8);
                this.bi_buf = 0;
                this.bi_valid = 0;
            }
            else if (this.bi_valid >= 8)
            {
                this.pending[this.pendingCount++] = (byte)this.bi_buf;
                this.bi_buf = (short)(this.bi_buf >> 8);
                this.bi_valid -= 8;
            }
        }

        // Token: 0x060003D7 RID: 983 RVA: 0x0001C770 File Offset: 0x0001A970
        internal void bi_windup()
        {
            if (this.bi_valid > 8)
            {
                this.pending[this.pendingCount++] = (byte)this.bi_buf;
                this.pending[this.pendingCount++] = (byte)(this.bi_buf >> 8);
            }
            else if (this.bi_valid > 0)
            {
                this.pending[this.pendingCount++] = (byte)this.bi_buf;
            }
            this.bi_buf = 0;
            this.bi_valid = 0;
        }

        // Token: 0x060003D8 RID: 984 RVA: 0x0001C814 File Offset: 0x0001AA14
        internal void copy_block(int buf, int len, bool header)
        {
            this.bi_windup();
            this.last_eob_len = 8;
            if (header)
            {
                this.pending[this.pendingCount++] = (byte)len;
                this.pending[this.pendingCount++] = (byte)(len >> 8);
                this.pending[this.pendingCount++] = (byte)(~(byte)len);
                this.pending[this.pendingCount++] = (byte)(~len >> 8);
            }
            this.put_bytes(this.window, buf, len);
        }

        // Token: 0x060003D9 RID: 985 RVA: 0x0001C8B8 File Offset: 0x0001AAB8
        internal void flush_block_only(bool eof)
        {
            this._tr_flush_block((this.block_start >= 0) ? this.block_start : -1, this.strstart - this.block_start, eof);
            this.block_start = this.strstart;
            this._codec.flush_pending();
        }

        // Token: 0x060003DA RID: 986 RVA: 0x0001C908 File Offset: 0x0001AB08
        internal BlockState DeflateNone(FlushType flush)
        {
            int max_block_size = 65535;
            if (max_block_size > this.pending.Length - 5)
            {
                max_block_size = this.pending.Length - 5;
            }
            for (; ; )
            {
                if (this.lookahead <= 1)
                {
                    this._fillWindow();
                    if (this.lookahead == 0 && flush == FlushType.None)
                    {
                        break;
                    }
                    if (this.lookahead == 0)
                    {
                        goto Block_5;
                    }
                }
                this.strstart += this.lookahead;
                this.lookahead = 0;
                int max_start = this.block_start + max_block_size;
                if (this.strstart == 0 || this.strstart >= max_start)
                {
                    this.lookahead = this.strstart - max_start;
                    this.strstart = max_start;
                    this.flush_block_only(false);
                    if (this._codec.AvailableBytesOut == 0)
                    {
                        goto Block_8;
                    }
                }
                if (this.strstart - this.block_start >= this.w_size - DeflateManager.MIN_LOOKAHEAD)
                {
                    this.flush_block_only(false);
                    if (this._codec.AvailableBytesOut == 0)
                    {
                        goto Block_10;
                    }
                }
            }
            return BlockState.NeedMore;
            Block_5:
            this.flush_block_only(flush == FlushType.Finish);
            if (this._codec.AvailableBytesOut == 0)
            {
                return (flush == FlushType.Finish) ? BlockState.FinishStarted : BlockState.NeedMore;
            }
            return (flush == FlushType.Finish) ? BlockState.FinishDone : BlockState.BlockDone;
            Block_8:
            return BlockState.NeedMore;
            Block_10:
            return BlockState.NeedMore;
        }

        // Token: 0x060003DB RID: 987 RVA: 0x0001CA82 File Offset: 0x0001AC82
        internal void _tr_stored_block(int buf, int stored_len, bool eof)
        {
            this.send_bits((DeflateManager.STORED_BLOCK << 1) + (eof ? 1 : 0), 3);
            this.copy_block(buf, stored_len, true);
        }

        // Token: 0x060003DC RID: 988 RVA: 0x0001CAA8 File Offset: 0x0001ACA8
        internal void _tr_flush_block(int buf, int stored_len, bool eof)
        {
            int max_blindex = 0;
            int opt_lenb;
            int static_lenb;
            if (this.compressionLevel > CompressionLevel.None)
            {
                if ((int)this.data_type == DeflateManager.Z_UNKNOWN)
                {
                    this.set_data_type();
                }
                this.treeLiterals.build_tree(this);
                this.treeDistances.build_tree(this);
                max_blindex = this.build_bl_tree();
                opt_lenb = this.opt_len + 3 + 7 >> 3;
                static_lenb = this.static_len + 3 + 7 >> 3;
                if (static_lenb <= opt_lenb)
                {
                    opt_lenb = static_lenb;
                }
            }
            else
            {
                static_lenb = (opt_lenb = stored_len + 5);
            }
            if (stored_len + 4 <= opt_lenb && buf != -1)
            {
                this._tr_stored_block(buf, stored_len, eof);
            }
            else if (static_lenb == opt_lenb)
            {
                this.send_bits((DeflateManager.STATIC_TREES << 1) + (eof ? 1 : 0), 3);
                this.send_compressed_block(StaticTree.lengthAndLiteralsTreeCodes, StaticTree.distTreeCodes);
            }
            else
            {
                this.send_bits((DeflateManager.DYN_TREES << 1) + (eof ? 1 : 0), 3);
                this.send_all_trees(this.treeLiterals.max_code + 1, this.treeDistances.max_code + 1, max_blindex + 1);
                this.send_compressed_block(this.dyn_ltree, this.dyn_dtree);
            }
            this._InitializeBlocks();
            if (eof)
            {
                this.bi_windup();
            }
        }

        // Token: 0x060003DD RID: 989 RVA: 0x0001CBF8 File Offset: 0x0001ADF8
        private void _fillWindow()
        {
            do
            {
                int more = this.window_size - this.lookahead - this.strstart;
                int i;
                if (more == 0 && this.strstart == 0 && this.lookahead == 0)
                {
                    more = this.w_size;
                }
                else if (more == -1)
                {
                    more--;
                }
                else if (this.strstart >= this.w_size + this.w_size - DeflateManager.MIN_LOOKAHEAD)
                {
                    Array.Copy(this.window, this.w_size, this.window, 0, this.w_size);
                    this.match_start -= this.w_size;
                    this.strstart -= this.w_size;
                    this.block_start -= this.w_size;
                    i = this.hash_size;
                    int p = i;
                    do
                    {
                        int j = (int)this.head[--p] & 65535;
                        this.head[p] = (short)((j >= this.w_size) ? (j - this.w_size) : 0);
                    }
                    while (--i != 0);
                    i = this.w_size;
                    p = i;
                    do
                    {
                        int j = (int)this.prev[--p] & 65535;
                        this.prev[p] = (short)((j >= this.w_size) ? (j - this.w_size) : 0);
                    }
                    while (--i != 0);
                    more += this.w_size;
                }
                if (this._codec.AvailableBytesIn == 0)
                {
                    break;
                }
                i = this._codec.read_buf(this.window, this.strstart + this.lookahead, more);
                this.lookahead += i;
                if (this.lookahead >= DeflateManager.MIN_MATCH)
                {
                    this.ins_h = (int)(this.window[this.strstart] & byte.MaxValue);
                    this.ins_h = ((this.ins_h << this.hash_shift ^ (int)(this.window[this.strstart + 1] & byte.MaxValue)) & this.hash_mask);
                }
            }
            while (this.lookahead < DeflateManager.MIN_LOOKAHEAD && this._codec.AvailableBytesIn != 0);
        }

        // Token: 0x060003DE RID: 990 RVA: 0x0001CE58 File Offset: 0x0001B058
        internal BlockState DeflateFast(FlushType flush)
        {
            int hash_head = 0;
            for (; ; )
            {
                if (this.lookahead < DeflateManager.MIN_LOOKAHEAD)
                {
                    this._fillWindow();
                    if (this.lookahead < DeflateManager.MIN_LOOKAHEAD && flush == FlushType.None)
                    {
                        break;
                    }
                    if (this.lookahead == 0)
                    {
                        goto Block_4;
                    }
                }
                if (this.lookahead >= DeflateManager.MIN_MATCH)
                {
                    this.ins_h = ((this.ins_h << this.hash_shift ^ (int)(this.window[this.strstart + (DeflateManager.MIN_MATCH - 1)] & byte.MaxValue)) & this.hash_mask);
                    hash_head = ((int)this.head[this.ins_h] & 65535);
                    this.prev[this.strstart & this.w_mask] = this.head[this.ins_h];
                    this.head[this.ins_h] = (short)this.strstart;
                }
                if ((long)hash_head != 0L && (this.strstart - hash_head & 65535) <= this.w_size - DeflateManager.MIN_LOOKAHEAD)
                {
                    if (this.compressionStrategy != CompressionStrategy.HuffmanOnly)
                    {
                        this.match_length = this.longest_match(hash_head);
                    }
                }
                bool bflush;
                if (this.match_length >= DeflateManager.MIN_MATCH)
                {
                    bflush = this._tr_tally(this.strstart - this.match_start, this.match_length - DeflateManager.MIN_MATCH);
                    this.lookahead -= this.match_length;
                    if (this.match_length <= this.config.MaxLazy && this.lookahead >= DeflateManager.MIN_MATCH)
                    {
                        this.match_length--;
                        do
                        {
                            this.strstart++;
                            this.ins_h = ((this.ins_h << this.hash_shift ^ (int)(this.window[this.strstart + (DeflateManager.MIN_MATCH - 1)] & byte.MaxValue)) & this.hash_mask);
                            hash_head = ((int)this.head[this.ins_h] & 65535);
                            this.prev[this.strstart & this.w_mask] = this.head[this.ins_h];
                            this.head[this.ins_h] = (short)this.strstart;
                        }
                        while (--this.match_length != 0);
                        this.strstart++;
                    }
                    else
                    {
                        this.strstart += this.match_length;
                        this.match_length = 0;
                        this.ins_h = (int)(this.window[this.strstart] & byte.MaxValue);
                        this.ins_h = ((this.ins_h << this.hash_shift ^ (int)(this.window[this.strstart + 1] & byte.MaxValue)) & this.hash_mask);
                    }
                }
                else
                {
                    bflush = this._tr_tally(0, (int)(this.window[this.strstart] & byte.MaxValue));
                    this.lookahead--;
                    this.strstart++;
                }
                if (bflush)
                {
                    this.flush_block_only(false);
                    if (this._codec.AvailableBytesOut == 0)
                    {
                        goto Block_14;
                    }
                }
            }
            return BlockState.NeedMore;
            Block_4:
            this.flush_block_only(flush == FlushType.Finish);
            if (this._codec.AvailableBytesOut != 0)
            {
                return (flush == FlushType.Finish) ? BlockState.FinishDone : BlockState.BlockDone;
            }
            if (flush == FlushType.Finish)
            {
                return BlockState.FinishStarted;
            }
            return BlockState.NeedMore;
            Block_14:
            return BlockState.NeedMore;
        }

        // Token: 0x060003DF RID: 991 RVA: 0x0001D200 File Offset: 0x0001B400
        internal BlockState DeflateSlow(FlushType flush)
        {
            int hash_head = 0;
            for (; ; )
            {
                if (this.lookahead < DeflateManager.MIN_LOOKAHEAD)
                {
                    this._fillWindow();
                    if (this.lookahead < DeflateManager.MIN_LOOKAHEAD && flush == FlushType.None)
                    {
                        break;
                    }
                    if (this.lookahead == 0)
                    {
                        goto Block_4;
                    }
                }
                if (this.lookahead >= DeflateManager.MIN_MATCH)
                {
                    this.ins_h = ((this.ins_h << this.hash_shift ^ (int)(this.window[this.strstart + (DeflateManager.MIN_MATCH - 1)] & byte.MaxValue)) & this.hash_mask);
                    hash_head = ((int)this.head[this.ins_h] & 65535);
                    this.prev[this.strstart & this.w_mask] = this.head[this.ins_h];
                    this.head[this.ins_h] = (short)this.strstart;
                }
                this.prev_length = this.match_length;
                this.prev_match = this.match_start;
                this.match_length = DeflateManager.MIN_MATCH - 1;
                if (hash_head != 0 && this.prev_length < this.config.MaxLazy && (this.strstart - hash_head & 65535) <= this.w_size - DeflateManager.MIN_LOOKAHEAD)
                {
                    if (this.compressionStrategy != CompressionStrategy.HuffmanOnly)
                    {
                        this.match_length = this.longest_match(hash_head);
                    }
                    if (this.match_length <= 5 && (this.compressionStrategy == CompressionStrategy.Filtered || (this.match_length == DeflateManager.MIN_MATCH && this.strstart - this.match_start > 4096)))
                    {
                        this.match_length = DeflateManager.MIN_MATCH - 1;
                    }
                }
                if (this.prev_length >= DeflateManager.MIN_MATCH && this.match_length <= this.prev_length)
                {
                    int max_insert = this.strstart + this.lookahead - DeflateManager.MIN_MATCH;
                    bool bflush = this._tr_tally(this.strstart - 1 - this.prev_match, this.prev_length - DeflateManager.MIN_MATCH);
                    this.lookahead -= this.prev_length - 1;
                    this.prev_length -= 2;
                    do
                    {
                        if (++this.strstart <= max_insert)
                        {
                            this.ins_h = ((this.ins_h << this.hash_shift ^ (int)(this.window[this.strstart + (DeflateManager.MIN_MATCH - 1)] & byte.MaxValue)) & this.hash_mask);
                            hash_head = ((int)this.head[this.ins_h] & 65535);
                            this.prev[this.strstart & this.w_mask] = this.head[this.ins_h];
                            this.head[this.ins_h] = (short)this.strstart;
                        }
                    }
                    while (--this.prev_length != 0);
                    this.match_available = 0;
                    this.match_length = DeflateManager.MIN_MATCH - 1;
                    this.strstart++;
                    if (bflush)
                    {
                        this.flush_block_only(false);
                        if (this._codec.AvailableBytesOut == 0)
                        {
                            goto Block_19;
                        }
                    }
                }
                else if (this.match_available != 0)
                {
                    bool bflush = this._tr_tally(0, (int)(this.window[this.strstart - 1] & byte.MaxValue));
                    if (bflush)
                    {
                        this.flush_block_only(false);
                    }
                    this.strstart++;
                    this.lookahead--;
                    if (this._codec.AvailableBytesOut == 0)
                    {
                        goto Block_22;
                    }
                }
                else
                {
                    this.match_available = 1;
                    this.strstart++;
                    this.lookahead--;
                }
            }
            return BlockState.NeedMore;
            Block_4:
            if (this.match_available != 0)
            {
                bool bflush = this._tr_tally(0, (int)(this.window[this.strstart - 1] & byte.MaxValue));
                this.match_available = 0;
            }
            this.flush_block_only(flush == FlushType.Finish);
            if (this._codec.AvailableBytesOut != 0)
            {
                return (flush == FlushType.Finish) ? BlockState.FinishDone : BlockState.BlockDone;
            }
            if (flush == FlushType.Finish)
            {
                return BlockState.FinishStarted;
            }
            return BlockState.NeedMore;
            Block_19:
            return BlockState.NeedMore;
            Block_22:
            return BlockState.NeedMore;
        }

        // Token: 0x060003E0 RID: 992 RVA: 0x0001D6A8 File Offset: 0x0001B8A8
        internal int longest_match(int cur_match)
        {
            int chain_length = this.config.MaxChainLength;
            int scan = this.strstart;
            int best_len = this.prev_length;
            int limit = (this.strstart > this.w_size - DeflateManager.MIN_LOOKAHEAD) ? (this.strstart - (this.w_size - DeflateManager.MIN_LOOKAHEAD)) : 0;
            int niceLength = this.config.NiceLength;
            int wmask = this.w_mask;
            int strend = this.strstart + DeflateManager.MAX_MATCH;
            byte scan_end = this.window[scan + best_len - 1];
            byte scan_end2 = this.window[scan + best_len];
            if (this.prev_length >= this.config.GoodLength)
            {
                chain_length >>= 2;
            }
            if (niceLength > this.lookahead)
            {
                niceLength = this.lookahead;
            }
            do
            {
                int match = cur_match;
                if (this.window[match + best_len] == scan_end2 && this.window[match + best_len - 1] == scan_end && this.window[match] == this.window[scan] && this.window[++match] == this.window[scan + 1])
                {
                    scan += 2;
                    match++;
                    while (this.window[++scan] == this.window[++match] && this.window[++scan] == this.window[++match] && this.window[++scan] == this.window[++match] && this.window[++scan] == this.window[++match] && this.window[++scan] == this.window[++match] && this.window[++scan] == this.window[++match] && this.window[++scan] == this.window[++match] && this.window[++scan] == this.window[++match] && scan < strend)
                    {
                    }
                    int len = DeflateManager.MAX_MATCH - (strend - scan);
                    scan = strend - DeflateManager.MAX_MATCH;
                    if (len > best_len)
                    {
                        this.match_start = cur_match;
                        best_len = len;
                        if (len >= niceLength)
                        {
                            break;
                        }
                        scan_end = this.window[scan + best_len - 1];
                        scan_end2 = this.window[scan + best_len];
                    }
                }
            }
            while ((cur_match = ((int)this.prev[cur_match & wmask] & 65535)) > limit && --chain_length != 0);
            int result;
            if (best_len <= this.lookahead)
            {
                result = best_len;
            }
            else
            {
                result = this.lookahead;
            }
            return result;
        }

        // Token: 0x170000FA RID: 250
        // (get) Token: 0x060003E1 RID: 993 RVA: 0x0001D970 File Offset: 0x0001BB70
        // (set) Token: 0x060003E2 RID: 994 RVA: 0x0001D988 File Offset: 0x0001BB88
        internal bool WantRfc1950HeaderBytes
        {
            get
            {
                return this._WantRfc1950HeaderBytes;
            }
            set
            {
                this._WantRfc1950HeaderBytes = value;
            }
        }

        // Token: 0x060003E3 RID: 995 RVA: 0x0001D994 File Offset: 0x0001BB94
        internal int Initialize(ZlibCodec codec, CompressionLevel level)
        {
            return this.Initialize(codec, level, 15);
        }

        // Token: 0x060003E4 RID: 996 RVA: 0x0001D9B0 File Offset: 0x0001BBB0
        internal int Initialize(ZlibCodec codec, CompressionLevel level, int bits)
        {
            return this.Initialize(codec, level, bits, DeflateManager.MEM_LEVEL_DEFAULT, CompressionStrategy.Default);
        }

        // Token: 0x060003E5 RID: 997 RVA: 0x0001D9D4 File Offset: 0x0001BBD4
        internal int Initialize(ZlibCodec codec, CompressionLevel level, int bits, CompressionStrategy compressionStrategy)
        {
            return this.Initialize(codec, level, bits, DeflateManager.MEM_LEVEL_DEFAULT, compressionStrategy);
        }

        // Token: 0x060003E6 RID: 998 RVA: 0x0001D9F8 File Offset: 0x0001BBF8
        internal int Initialize(ZlibCodec codec, CompressionLevel level, int windowBits, int memLevel, CompressionStrategy strategy)
        {
            this._codec = codec;
            this._codec.Message = null;
            if (windowBits < 9 || windowBits > 15)
            {
                throw new ZlibException("windowBits must be in the range 9..15.");
            }
            if (memLevel < 1 || memLevel > DeflateManager.MEM_LEVEL_MAX)
            {
                throw new ZlibException(string.Format("memLevel must be in the range 1.. {0}", DeflateManager.MEM_LEVEL_MAX));
            }
            this._codec.dstate = this;
            this.w_bits = windowBits;
            this.w_size = 1 << this.w_bits;
            this.w_mask = this.w_size - 1;
            this.hash_bits = memLevel + 7;
            this.hash_size = 1 << this.hash_bits;
            this.hash_mask = this.hash_size - 1;
            this.hash_shift = (this.hash_bits + DeflateManager.MIN_MATCH - 1) / DeflateManager.MIN_MATCH;
            this.window = new byte[this.w_size * 2];
            this.prev = new short[this.w_size];
            this.head = new short[this.hash_size];
            this.lit_bufsize = 1 << memLevel + 6;
            this.pending = new byte[this.lit_bufsize * 4];
            this._distanceOffset = this.lit_bufsize;
            this._lengthOffset = 3 * this.lit_bufsize;
            this.compressionLevel = level;
            this.compressionStrategy = strategy;
            this.Reset();
            return 0;
        }

        // Token: 0x060003E7 RID: 999 RVA: 0x0001DB6C File Offset: 0x0001BD6C
        internal void Reset()
        {
            this._codec.TotalBytesIn = (this._codec.TotalBytesOut = 0L);
            this._codec.Message = null;
            this.pendingCount = 0;
            this.nextPending = 0;
            this.Rfc1950BytesEmitted = false;
            this.status = (this.WantRfc1950HeaderBytes ? DeflateManager.INIT_STATE : DeflateManager.BUSY_STATE);
            this._codec._Adler32 = Adler.Adler32(0u, null, 0, 0);
            this.last_flush = 0;
            this._InitializeTreeData();
            this._InitializeLazyMatch();
        }

        // Token: 0x060003E8 RID: 1000 RVA: 0x0001DBFC File Offset: 0x0001BDFC
        internal int End()
        {
            int result;
            if (this.status != DeflateManager.INIT_STATE && this.status != DeflateManager.BUSY_STATE && this.status != DeflateManager.FINISH_STATE)
            {
                result = -2;
            }
            else
            {
                this.pending = null;
                this.head = null;
                this.prev = null;
                this.window = null;
                result = ((this.status == DeflateManager.BUSY_STATE) ? -3 : 0);
            }
            return result;
        }

        // Token: 0x060003E9 RID: 1001 RVA: 0x0001DC70 File Offset: 0x0001BE70
        private void SetDeflater()
        {
            switch (this.config.Flavor)
            {
                case DeflateFlavor.Store:
                    this.DeflateFunction = new DeflateManager.CompressFunc(this.DeflateNone);
                    break;
                case DeflateFlavor.Fast:
                    this.DeflateFunction = new DeflateManager.CompressFunc(this.DeflateFast);
                    break;
                case DeflateFlavor.Slow:
                    this.DeflateFunction = new DeflateManager.CompressFunc(this.DeflateSlow);
                    break;
            }
        }

        // Token: 0x060003EA RID: 1002 RVA: 0x0001DCDC File Offset: 0x0001BEDC
        internal int SetParams(CompressionLevel level, CompressionStrategy strategy)
        {
            int result = 0;
            if (this.compressionLevel != level)
            {
                DeflateManager.Config newConfig = DeflateManager.Config.Lookup(level);
                if (newConfig.Flavor != this.config.Flavor && this._codec.TotalBytesIn != 0L)
                {
                    result = this._codec.Deflate(FlushType.Partial);
                }
                this.compressionLevel = level;
                this.config = newConfig;
                this.SetDeflater();
            }
            this.compressionStrategy = strategy;
            return result;
        }

        // Token: 0x060003EB RID: 1003 RVA: 0x0001DD5C File Offset: 0x0001BF5C
        internal int SetDictionary(byte[] dictionary)
        {
            int length = dictionary.Length;
            int index = 0;
            if (dictionary == null || this.status != DeflateManager.INIT_STATE)
            {
                throw new ZlibException("Stream error.");
            }
            this._codec._Adler32 = Adler.Adler32(this._codec._Adler32, dictionary, 0, dictionary.Length);
            int result;
            if (length < DeflateManager.MIN_MATCH)
            {
                result = 0;
            }
            else
            {
                if (length > this.w_size - DeflateManager.MIN_LOOKAHEAD)
                {
                    length = this.w_size - DeflateManager.MIN_LOOKAHEAD;
                    index = dictionary.Length - length;
                }
                Array.Copy(dictionary, index, this.window, 0, length);
                this.strstart = length;
                this.block_start = length;
                this.ins_h = (int)(this.window[0] & byte.MaxValue);
                this.ins_h = ((this.ins_h << this.hash_shift ^ (int)(this.window[1] & byte.MaxValue)) & this.hash_mask);
                for (int i = 0; i <= length - DeflateManager.MIN_MATCH; i++)
                {
                    this.ins_h = ((this.ins_h << this.hash_shift ^ (int)(this.window[i + (DeflateManager.MIN_MATCH - 1)] & byte.MaxValue)) & this.hash_mask);
                    this.prev[i & this.w_mask] = this.head[this.ins_h];
                    this.head[this.ins_h] = (short)i;
                }
                result = 0;
            }
            return result;
        }

        // Token: 0x060003EC RID: 1004 RVA: 0x0001DED8 File Offset: 0x0001C0D8
        internal int Deflate(FlushType flush)
        {
            if (this._codec.OutputBuffer == null || (this._codec.InputBuffer == null && this._codec.AvailableBytesIn != 0) || (this.status == DeflateManager.FINISH_STATE && flush != FlushType.Finish))
            {
                this._codec.Message = DeflateManager._ErrorMessage[4];
                throw new ZlibException(string.Format("Something is fishy. [{0}]", this._codec.Message));
            }
            if (this._codec.AvailableBytesOut == 0)
            {
                this._codec.Message = DeflateManager._ErrorMessage[7];
                throw new ZlibException("OutputBuffer is full (AvailableBytesOut == 0)");
            }
            int old_flush = this.last_flush;
            this.last_flush = (int)flush;
            if (this.status == DeflateManager.INIT_STATE)
            {
                int header = DeflateManager.Z_DEFLATED + (this.w_bits - 8 << 4) << 8;
                int level_flags = (this.compressionLevel - CompressionLevel.BestSpeed & 255) >> 1;
                if (level_flags > 3)
                {
                    level_flags = 3;
                }
                header |= level_flags << 6;
                if (this.strstart != 0)
                {
                    header |= DeflateManager.PRESET_DICT;
                }
                header += 31 - header % 31;
                this.status = DeflateManager.BUSY_STATE;
                this.pending[this.pendingCount++] = (byte)(header >> 8);
                this.pending[this.pendingCount++] = (byte)header;
                if (this.strstart != 0)
                {
                    this.pending[this.pendingCount++] = (byte)((this._codec._Adler32 & 4278190080u) >> 24);
                    this.pending[this.pendingCount++] = (byte)((this._codec._Adler32 & 16711680u) >> 16);
                    this.pending[this.pendingCount++] = (byte)((this._codec._Adler32 & 65280u) >> 8);
                    this.pending[this.pendingCount++] = (byte)(this._codec._Adler32 & 255u);
                }
                this._codec._Adler32 = Adler.Adler32(0u, null, 0, 0);
            }
            if (this.pendingCount != 0)
            {
                this._codec.flush_pending();
                if (this._codec.AvailableBytesOut == 0)
                {
                    this.last_flush = -1;
                    return 0;
                }
            }
            else if (this._codec.AvailableBytesIn == 0 && flush <= (FlushType)old_flush && flush != FlushType.Finish)
            {
                return 0;
            }
            if (this.status == DeflateManager.FINISH_STATE && this._codec.AvailableBytesIn != 0)
            {
                this._codec.Message = DeflateManager._ErrorMessage[7];
                throw new ZlibException("status == FINISH_STATE && _codec.AvailableBytesIn != 0");
            }
            if (this._codec.AvailableBytesIn != 0 || this.lookahead != 0 || (flush != FlushType.None && this.status != DeflateManager.FINISH_STATE))
            {
                BlockState bstate = this.DeflateFunction(flush);
                if (bstate == BlockState.FinishStarted || bstate == BlockState.FinishDone)
                {
                    this.status = DeflateManager.FINISH_STATE;
                }
                if (bstate == BlockState.NeedMore || bstate == BlockState.FinishStarted)
                {
                    if (this._codec.AvailableBytesOut == 0)
                    {
                        this.last_flush = -1;
                    }
                    return 0;
                }
                if (bstate == BlockState.BlockDone)
                {
                    if (flush == FlushType.Partial)
                    {
                        this._tr_align();
                    }
                    else
                    {
                        this._tr_stored_block(0, 0, false);
                        if (flush == FlushType.Full)
                        {
                            for (int i = 0; i < this.hash_size; i++)
                            {
                                this.head[i] = 0;
                            }
                        }
                    }
                    this._codec.flush_pending();
                    if (this._codec.AvailableBytesOut == 0)
                    {
                        this.last_flush = -1;
                        return 0;
                    }
                }
            }
            int result;
            if (flush != FlushType.Finish)
            {
                result = 0;
            }
            else if (!this.WantRfc1950HeaderBytes || this.Rfc1950BytesEmitted)
            {
                result = 1;
            }
            else
            {
                this.pending[this.pendingCount++] = (byte)((this._codec._Adler32 & 4278190080u) >> 24);
                this.pending[this.pendingCount++] = (byte)((this._codec._Adler32 & 16711680u) >> 16);
                this.pending[this.pendingCount++] = (byte)((this._codec._Adler32 & 65280u) >> 8);
                this.pending[this.pendingCount++] = (byte)(this._codec._Adler32 & 255u);
                this._codec.flush_pending();
                this.Rfc1950BytesEmitted = true;
                result = ((this.pendingCount != 0) ? 0 : 1);
            }
            return result;
        }

        // Token: 0x0400025D RID: 605
        private static readonly int MEM_LEVEL_MAX = 9;

        // Token: 0x0400025E RID: 606
        private static readonly int MEM_LEVEL_DEFAULT = 8;

        // Token: 0x0400025F RID: 607
        private DeflateManager.CompressFunc DeflateFunction;

        // Token: 0x04000260 RID: 608
        private static readonly string[] _ErrorMessage = new string[]
        {
            "need dictionary",
            "stream end",
            "",
            "file error",
            "stream error",
            "data error",
            "insufficient memory",
            "buffer error",
            "incompatible version",
            ""
        };

        // Token: 0x04000261 RID: 609
        private static readonly int PRESET_DICT = 32;

        // Token: 0x04000262 RID: 610
        private static readonly int INIT_STATE = 42;

        // Token: 0x04000263 RID: 611
        private static readonly int BUSY_STATE = 113;

        // Token: 0x04000264 RID: 612
        private static readonly int FINISH_STATE = 666;

        // Token: 0x04000265 RID: 613
        private static readonly int Z_DEFLATED = 8;

        // Token: 0x04000266 RID: 614
        private static readonly int STORED_BLOCK = 0;

        // Token: 0x04000267 RID: 615
        private static readonly int STATIC_TREES = 1;

        // Token: 0x04000268 RID: 616
        private static readonly int DYN_TREES = 2;

        // Token: 0x04000269 RID: 617
        private static readonly int Z_BINARY = 0;

        // Token: 0x0400026A RID: 618
        private static readonly int Z_ASCII = 1;

        // Token: 0x0400026B RID: 619
        private static readonly int Z_UNKNOWN = 2;

        // Token: 0x0400026C RID: 620
        private static readonly int Buf_size = 16;

        // Token: 0x0400026D RID: 621
        private static readonly int MIN_MATCH = 3;

        // Token: 0x0400026E RID: 622
        private static readonly int MAX_MATCH = 258;

        // Token: 0x0400026F RID: 623
        private static readonly int MIN_LOOKAHEAD = DeflateManager.MAX_MATCH + DeflateManager.MIN_MATCH + 1;

        // Token: 0x04000270 RID: 624
        private static readonly int HEAP_SIZE = 2 * InternalConstants.L_CODES + 1;

        // Token: 0x04000271 RID: 625
        private static readonly int END_BLOCK = 256;

        // Token: 0x04000272 RID: 626
        internal ZlibCodec _codec;

        // Token: 0x04000273 RID: 627
        internal int status;

        // Token: 0x04000274 RID: 628
        internal byte[] pending;

        // Token: 0x04000275 RID: 629
        internal int nextPending;

        // Token: 0x04000276 RID: 630
        internal int pendingCount;

        // Token: 0x04000277 RID: 631
        internal sbyte data_type;

        // Token: 0x04000278 RID: 632
        internal int last_flush;

        // Token: 0x04000279 RID: 633
        internal int w_size;

        // Token: 0x0400027A RID: 634
        internal int w_bits;

        // Token: 0x0400027B RID: 635
        internal int w_mask;

        // Token: 0x0400027C RID: 636
        internal byte[] window;

        // Token: 0x0400027D RID: 637
        internal int window_size;

        // Token: 0x0400027E RID: 638
        internal short[] prev;

        // Token: 0x0400027F RID: 639
        internal short[] head;

        // Token: 0x04000280 RID: 640
        internal int ins_h;

        // Token: 0x04000281 RID: 641
        internal int hash_size;

        // Token: 0x04000282 RID: 642
        internal int hash_bits;

        // Token: 0x04000283 RID: 643
        internal int hash_mask;

        // Token: 0x04000284 RID: 644
        internal int hash_shift;

        // Token: 0x04000285 RID: 645
        internal int block_start;

        // Token: 0x04000286 RID: 646
        private DeflateManager.Config config;

        // Token: 0x04000287 RID: 647
        internal int match_length;

        // Token: 0x04000288 RID: 648
        internal int prev_match;

        // Token: 0x04000289 RID: 649
        internal int match_available;

        // Token: 0x0400028A RID: 650
        internal int strstart;

        // Token: 0x0400028B RID: 651
        internal int match_start;

        // Token: 0x0400028C RID: 652
        internal int lookahead;

        // Token: 0x0400028D RID: 653
        internal int prev_length;

        // Token: 0x0400028E RID: 654
        internal CompressionLevel compressionLevel;

        // Token: 0x0400028F RID: 655
        internal CompressionStrategy compressionStrategy;

        // Token: 0x04000290 RID: 656
        internal short[] dyn_ltree;

        // Token: 0x04000291 RID: 657
        internal short[] dyn_dtree;

        // Token: 0x04000292 RID: 658
        internal short[] bl_tree;

        // Token: 0x04000293 RID: 659
        internal Tree treeLiterals = new Tree();

        // Token: 0x04000294 RID: 660
        internal Tree treeDistances = new Tree();

        // Token: 0x04000295 RID: 661
        internal Tree treeBitLengths = new Tree();

        // Token: 0x04000296 RID: 662
        internal short[] bl_count = new short[InternalConstants.MAX_BITS + 1];

        // Token: 0x04000297 RID: 663
        internal int[] heap = new int[2 * InternalConstants.L_CODES + 1];

        // Token: 0x04000298 RID: 664
        internal int heap_len;

        // Token: 0x04000299 RID: 665
        internal int heap_max;

        // Token: 0x0400029A RID: 666
        internal sbyte[] depth = new sbyte[2 * InternalConstants.L_CODES + 1];

        // Token: 0x0400029B RID: 667
        internal int _lengthOffset;

        // Token: 0x0400029C RID: 668
        internal int lit_bufsize;

        // Token: 0x0400029D RID: 669
        internal int last_lit;

        // Token: 0x0400029E RID: 670
        internal int _distanceOffset;

        // Token: 0x0400029F RID: 671
        internal int opt_len;

        // Token: 0x040002A0 RID: 672
        internal int static_len;

        // Token: 0x040002A1 RID: 673
        internal int matches;

        // Token: 0x040002A2 RID: 674
        internal int last_eob_len;

        // Token: 0x040002A3 RID: 675
        internal short bi_buf;

        // Token: 0x040002A4 RID: 676
        internal int bi_valid;

        // Token: 0x040002A5 RID: 677
        private bool Rfc1950BytesEmitted = false;

        // Token: 0x040002A6 RID: 678
        private bool _WantRfc1950HeaderBytes = true;

        // Token: 0x02000052 RID: 82
        // (Invoke) Token: 0x060003EF RID: 1007
        internal delegate BlockState CompressFunc(FlushType flush);

        // Token: 0x02000053 RID: 83
        internal class Config
        {
            // Token: 0x060003F2 RID: 1010 RVA: 0x0001E537 File Offset: 0x0001C737
            private Config(int goodLength, int maxLazy, int niceLength, int maxChainLength, DeflateFlavor flavor)
            {
                this.GoodLength = goodLength;
                this.MaxLazy = maxLazy;
                this.NiceLength = niceLength;
                this.MaxChainLength = maxChainLength;
                this.Flavor = flavor;
            }

            // Token: 0x060003F3 RID: 1011 RVA: 0x0001E568 File Offset: 0x0001C768
            public static DeflateManager.Config Lookup(CompressionLevel level)
            {
                return DeflateManager.Config.Table[(int)level];
            }

            // Token: 0x040002A7 RID: 679
            internal int GoodLength;

            // Token: 0x040002A8 RID: 680
            internal int MaxLazy;

            // Token: 0x040002A9 RID: 681
            internal int NiceLength;

            // Token: 0x040002AA RID: 682
            internal int MaxChainLength;

            // Token: 0x040002AB RID: 683
            internal DeflateFlavor Flavor;

            // Token: 0x040002AC RID: 684
            private static readonly DeflateManager.Config[] Table = new DeflateManager.Config[]
            {
                new DeflateManager.Config(0, 0, 0, 0, DeflateFlavor.Store),
                new DeflateManager.Config(4, 4, 8, 4, DeflateFlavor.Fast),
                new DeflateManager.Config(4, 5, 16, 8, DeflateFlavor.Fast),
                new DeflateManager.Config(4, 6, 32, 32, DeflateFlavor.Fast),
                new DeflateManager.Config(4, 4, 16, 16, DeflateFlavor.Slow),
                new DeflateManager.Config(8, 16, 32, 32, DeflateFlavor.Slow),
                new DeflateManager.Config(8, 16, 128, 128, DeflateFlavor.Slow),
                new DeflateManager.Config(8, 32, 128, 256, DeflateFlavor.Slow),
                new DeflateManager.Config(32, 128, 258, 1024, DeflateFlavor.Slow),
                new DeflateManager.Config(32, 258, 258, 4096, DeflateFlavor.Slow)
            };
        }
    }
}


namespace Ionic.Zlib
{
    /// <summary>
    /// A class for compressing and decompressing streams using the Deflate algorithm.
    /// </summary>
    ///
    /// <remarks>
    ///
    /// <para>
    ///   The DeflateStream is a <see href="http://en.wikipedia.org/wiki/Decorator_pattern">Decorator</see> on a <see cref="T:System.IO.Stream" />.  It adds DEFLATE compression or decompression to any
    ///   stream.
    /// </para>
    ///
    /// <para>
    ///   Using this stream, applications can compress or decompress data via stream
    ///   <c>Read</c> and <c>Write</c> operations.  Either compresssion or decompression
    ///   can occur through either reading or writing. The compression format used is
    ///   DEFLATE, which is documented in <see href="http://www.ietf.org/rfc/rfc1951.txt">IETF RFC 1951</see>, "DEFLATE
    ///   Compressed Data Format Specification version 1.3.".
    /// </para>
    ///
    /// <para>
    ///   This class is similar to <see cref="T:Ionic.Zlib.ZlibStream" />, except that
    ///   <c>ZlibStream</c> adds the <see href="http://www.ietf.org/rfc/rfc1950.txt">RFC
    ///   1950 - ZLIB</see> framing bytes to a compressed stream when compressing, or
    ///   expects the RFC1950 framing bytes when decompressing. The <c>DeflateStream</c>
    ///   does not.
    /// </para>
    ///
    /// </remarks>
    ///
    /// <seealso cref="T:Ionic.Zlib.ZlibStream" />
    /// <seealso cref="T:Ionic.Zlib.GZipStream" />
    // Token: 0x02000054 RID: 84
    public class DeflateStream : Stream
    {
        /// <summary>
        ///   Create a DeflateStream using the specified CompressionMode.
        /// </summary>
        ///
        /// <remarks>
        ///   When mode is <c>CompressionMode.Compress</c>, the DeflateStream will use
        ///   the default compression level. The "captive" stream will be closed when
        ///   the DeflateStream is closed.
        /// </remarks>
        ///
        /// <example>
        /// This example uses a DeflateStream to compress data from a file, and writes
        /// the compressed data to another file.
        /// <code>
        /// using (System.IO.Stream input = System.IO.File.OpenRead(fileToCompress))
        /// {
        ///     using (var raw = System.IO.File.Create(fileToCompress + ".deflated"))
        ///     {
        ///         using (Stream compressor = new DeflateStream(raw, CompressionMode.Compress))
        ///         {
        ///             byte[] buffer = new byte[WORKING_BUFFER_SIZE];
        ///             int n;
        ///             while ((n= input.Read(buffer, 0, buffer.Length)) != 0)
        ///             {
        ///                 compressor.Write(buffer, 0, n);
        ///             }
        ///         }
        ///     }
        /// }
        /// </code>
        ///
        /// <code lang="VB">
        /// Using input As Stream = File.OpenRead(fileToCompress)
        ///     Using raw As FileStream = File.Create(fileToCompress &amp; ".deflated")
        ///         Using compressor As Stream = New DeflateStream(raw, CompressionMode.Compress)
        ///             Dim buffer As Byte() = New Byte(4096) {}
        ///             Dim n As Integer = -1
        ///             Do While (n &lt;&gt; 0)
        ///                 If (n &gt; 0) Then
        ///                     compressor.Write(buffer, 0, n)
        ///                 End If
        ///                 n = input.Read(buffer, 0, buffer.Length)
        ///             Loop
        ///         End Using
        ///     End Using
        /// End Using
        /// </code>
        /// </example>
        /// <param name="stream">The stream which will be read or written.</param>
        /// <param name="mode">Indicates whether the DeflateStream will compress or decompress.</param>
        // Token: 0x060003F5 RID: 1013 RVA: 0x0001E657 File Offset: 0x0001C857
        public DeflateStream(Stream stream, CompressionMode mode) : this(stream, mode, CompressionLevel.Default, false)
        {
        }

        /// <summary>
        /// Create a DeflateStream using the specified CompressionMode and the specified CompressionLevel.
        /// </summary>
        ///
        /// <remarks>
        ///
        /// <para>
        ///   When mode is <c>CompressionMode.Decompress</c>, the level parameter is
        ///   ignored.  The "captive" stream will be closed when the DeflateStream is
        ///   closed.
        /// </para>
        ///
        /// </remarks>
        ///
        /// <example>
        ///
        ///   This example uses a DeflateStream to compress data from a file, and writes
        ///   the compressed data to another file.
        ///
        /// <code>
        /// using (System.IO.Stream input = System.IO.File.OpenRead(fileToCompress))
        /// {
        ///     using (var raw = System.IO.File.Create(fileToCompress + ".deflated"))
        ///     {
        ///         using (Stream compressor = new DeflateStream(raw,
        ///                                                      CompressionMode.Compress,
        ///                                                      CompressionLevel.BestCompression))
        ///         {
        ///             byte[] buffer = new byte[WORKING_BUFFER_SIZE];
        ///             int n= -1;
        ///             while (n != 0)
        ///             {
        ///                 if (n &gt; 0)
        ///                     compressor.Write(buffer, 0, n);
        ///                 n= input.Read(buffer, 0, buffer.Length);
        ///             }
        ///         }
        ///     }
        /// }
        /// </code>
        ///
        /// <code lang="VB">
        /// Using input As Stream = File.OpenRead(fileToCompress)
        ///     Using raw As FileStream = File.Create(fileToCompress &amp; ".deflated")
        ///         Using compressor As Stream = New DeflateStream(raw, CompressionMode.Compress, CompressionLevel.BestCompression)
        ///             Dim buffer As Byte() = New Byte(4096) {}
        ///             Dim n As Integer = -1
        ///             Do While (n &lt;&gt; 0)
        ///                 If (n &gt; 0) Then
        ///                     compressor.Write(buffer, 0, n)
        ///                 End If
        ///                 n = input.Read(buffer, 0, buffer.Length)
        ///             Loop
        ///         End Using
        ///     End Using
        /// End Using
        /// </code>
        /// </example>
        /// <param name="stream">The stream to be read or written while deflating or inflating.</param>
        /// <param name="mode">Indicates whether the <c>DeflateStream</c> will compress or decompress.</param>
        /// <param name="level">A tuning knob to trade speed for effectiveness.</param>
        // Token: 0x060003F6 RID: 1014 RVA: 0x0001E666 File Offset: 0x0001C866
        public DeflateStream(Stream stream, CompressionMode mode, CompressionLevel level) : this(stream, mode, level, false)
        {
        }

        /// <summary>
        ///   Create a <c>DeflateStream</c> using the specified
        ///   <c>CompressionMode</c>, and explicitly specify whether the
        ///   stream should be left open after Deflation or Inflation.
        /// </summary>
        ///
        /// <remarks>
        ///
        /// <para>
        ///   This constructor allows the application to request that the captive stream
        ///   remain open after the deflation or inflation occurs.  By default, after
        ///   <c>Close()</c> is called on the stream, the captive stream is also
        ///   closed. In some cases this is not desired, for example if the stream is a
        ///   memory stream that will be re-read after compression.  Specify true for
        ///   the <paramref name="leaveOpen" /> parameter to leave the stream open.
        /// </para>
        ///
        /// <para>
        ///   The <c>DeflateStream</c> will use the default compression level.
        /// </para>
        ///
        /// <para>
        ///   See the other overloads of this constructor for example code.
        /// </para>
        /// </remarks>
        ///
        /// <param name="stream">
        ///   The stream which will be read or written. This is called the
        ///   "captive" stream in other places in this documentation.
        /// </param>
        ///
        /// <param name="mode">
        ///   Indicates whether the <c>DeflateStream</c> will compress or decompress.
        /// </param>
        ///
        /// <param name="leaveOpen">true if the application would like the stream to
        /// remain open after inflation/deflation.</param>
        // Token: 0x060003F7 RID: 1015 RVA: 0x0001E675 File Offset: 0x0001C875
        public DeflateStream(Stream stream, CompressionMode mode, bool leaveOpen) : this(stream, mode, CompressionLevel.Default, leaveOpen)
        {
        }

        /// <summary>
        ///   Create a <c>DeflateStream</c> using the specified <c>CompressionMode</c>
        ///   and the specified <c>CompressionLevel</c>, and explicitly specify whether
        ///   the stream should be left open after Deflation or Inflation.
        /// </summary>
        ///
        /// <remarks>
        ///
        /// <para>
        ///   When mode is <c>CompressionMode.Decompress</c>, the level parameter is ignored.
        /// </para>
        ///
        /// <para>
        ///   This constructor allows the application to request that the captive stream
        ///   remain open after the deflation or inflation occurs.  By default, after
        ///   <c>Close()</c> is called on the stream, the captive stream is also
        ///   closed. In some cases this is not desired, for example if the stream is a
        ///   <see cref="T:System.IO.MemoryStream" /> that will be re-read after
        ///   compression.  Specify true for the <paramref name="leaveOpen" /> parameter
        ///   to leave the stream open.
        /// </para>
        ///
        /// </remarks>
        ///
        /// <example>
        ///
        ///   This example shows how to use a <c>DeflateStream</c> to compress data from
        ///   a file, and store the compressed data into another file.
        ///
        /// <code>
        /// using (var output = System.IO.File.Create(fileToCompress + ".deflated"))
        /// {
        ///     using (System.IO.Stream input = System.IO.File.OpenRead(fileToCompress))
        ///     {
        ///         using (Stream compressor = new DeflateStream(output, CompressionMode.Compress, CompressionLevel.BestCompression, true))
        ///         {
        ///             byte[] buffer = new byte[WORKING_BUFFER_SIZE];
        ///             int n= -1;
        ///             while (n != 0)
        ///             {
        ///                 if (n &gt; 0)
        ///                     compressor.Write(buffer, 0, n);
        ///                 n= input.Read(buffer, 0, buffer.Length);
        ///             }
        ///         }
        ///     }
        ///     // can write additional data to the output stream here
        /// }
        /// </code>
        ///
        /// <code lang="VB">
        /// Using output As FileStream = File.Create(fileToCompress &amp; ".deflated")
        ///     Using input As Stream = File.OpenRead(fileToCompress)
        ///         Using compressor As Stream = New DeflateStream(output, CompressionMode.Compress, CompressionLevel.BestCompression, True)
        ///             Dim buffer As Byte() = New Byte(4096) {}
        ///             Dim n As Integer = -1
        ///             Do While (n &lt;&gt; 0)
        ///                 If (n &gt; 0) Then
        ///                     compressor.Write(buffer, 0, n)
        ///                 End If
        ///                 n = input.Read(buffer, 0, buffer.Length)
        ///             Loop
        ///         End Using
        ///     End Using
        ///     ' can write additional data to the output stream here.
        /// End Using
        /// </code>
        /// </example>
        /// <param name="stream">The stream which will be read or written.</param>
        /// <param name="mode">Indicates whether the DeflateStream will compress or decompress.</param>
        /// <param name="leaveOpen">true if the application would like the stream to remain open after inflation/deflation.</param>
        /// <param name="level">A tuning knob to trade speed for effectiveness.</param>
        // Token: 0x060003F8 RID: 1016 RVA: 0x0001E684 File Offset: 0x0001C884
        public DeflateStream(Stream stream, CompressionMode mode, CompressionLevel level, bool leaveOpen)
        {
            this._innerStream = stream;
            this._baseStream = new ZlibBaseStream(stream, mode, level, ZlibStreamFlavor.DEFLATE, leaveOpen);
        }

        /// <summary>
        /// This property sets the flush behavior on the stream.
        /// </summary>
        /// <remarks> See the ZLIB documentation for the meaning of the flush behavior.
        /// </remarks>
        // Token: 0x170000FB RID: 251
        // (get) Token: 0x060003F9 RID: 1017 RVA: 0x0001E6AC File Offset: 0x0001C8AC
        // (set) Token: 0x060003FA RID: 1018 RVA: 0x0001E6CC File Offset: 0x0001C8CC
        public virtual FlushType FlushMode
        {
            get
            {
                return this._baseStream._flushMode;
            }
            set
            {
                if (this._disposed)
                {
                    throw new ObjectDisposedException("DeflateStream");
                }
                this._baseStream._flushMode = value;
            }
        }

        /// <summary>
        ///   The size of the working buffer for the compression codec.
        /// </summary>
        ///
        /// <remarks>
        /// <para>
        ///   The working buffer is used for all stream operations.  The default size is
        ///   1024 bytes.  The minimum size is 128 bytes. You may get better performance
        ///   with a larger buffer.  Then again, you might not.  You would have to test
        ///   it.
        /// </para>
        ///
        /// <para>
        ///   Set this before the first call to <c>Read()</c> or <c>Write()</c> on the
        ///   stream. If you try to set it afterwards, it will throw.
        /// </para>
        /// </remarks>
        // Token: 0x170000FC RID: 252
        // (get) Token: 0x060003FB RID: 1019 RVA: 0x0001E700 File Offset: 0x0001C900
        // (set) Token: 0x060003FC RID: 1020 RVA: 0x0001E720 File Offset: 0x0001C920
        public int BufferSize
        {
            get
            {
                return this._baseStream._bufferSize;
            }
            set
            {
                if (this._disposed)
                {
                    throw new ObjectDisposedException("DeflateStream");
                }
                if (this._baseStream._workingBuffer != null)
                {
                    throw new ZlibException("The working buffer is already set.");
                }
                if (value < 1024)
                {
                    throw new ZlibException(string.Format("Don't be silly. {0} bytes?? Use a bigger buffer, at least {1}.", value, 1024));
                }
                this._baseStream._bufferSize = value;
            }
        }

        /// <summary>
        ///   The ZLIB strategy to be used during compression.
        /// </summary>
        ///
        /// <remarks>
        ///   By tweaking this parameter, you may be able to optimize the compression for
        ///   data with particular characteristics.
        /// </remarks>
        // Token: 0x170000FD RID: 253
        // (get) Token: 0x060003FD RID: 1021 RVA: 0x0001E7A0 File Offset: 0x0001C9A0
        // (set) Token: 0x060003FE RID: 1022 RVA: 0x0001E7C0 File Offset: 0x0001C9C0
        public CompressionStrategy Strategy
        {
            get
            {
                return this._baseStream.Strategy;
            }
            set
            {
                if (this._disposed)
                {
                    throw new ObjectDisposedException("DeflateStream");
                }
                this._baseStream.Strategy = value;
            }
        }

        /// <summary> Returns the total number of bytes input so far.</summary>
        // Token: 0x170000FE RID: 254
        // (get) Token: 0x060003FF RID: 1023 RVA: 0x0001E7F4 File Offset: 0x0001C9F4
        public virtual long TotalIn
        {
            get
            {
                return this._baseStream._z.TotalBytesIn;
            }
        }

        /// <summary> Returns the total number of bytes output so far.</summary>
        // Token: 0x170000FF RID: 255
        // (get) Token: 0x06000400 RID: 1024 RVA: 0x0001E818 File Offset: 0x0001CA18
        public virtual long TotalOut
        {
            get
            {
                return this._baseStream._z.TotalBytesOut;
            }
        }

        /// <summary>
        ///   Dispose the stream.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     This may or may not result in a <c>Close()</c> call on the captive
        ///     stream.  See the constructors that have a <c>leaveOpen</c> parameter
        ///     for more information.
        ///   </para>
        ///   <para>
        ///     Application code won't call this code directly.  This method may be
        ///     invoked in two distinct scenarios.  If disposing == true, the method
        ///     has been called directly or indirectly by a user's code, for example
        ///     via the public Dispose() method. In this case, both managed and
        ///     unmanaged resources can be referenced and disposed.  If disposing ==
        ///     false, the method has been called by the runtime from inside the
        ///     object finalizer and this method should not reference other objects;
        ///     in that case only unmanaged resources must be referenced or
        ///     disposed.
        ///   </para>
        /// </remarks>
        /// <param name="disposing">
        ///   true if the Dispose method was invoked by user code.
        /// </param>
        // Token: 0x06000401 RID: 1025 RVA: 0x0001E83C File Offset: 0x0001CA3C
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!this._disposed)
                {
                    if (disposing && this._baseStream != null)
                    {
                        this._baseStream.Close();
                    }
                    this._disposed = true;
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Indicates whether the stream can be read.
        /// </summary>
        /// <remarks>
        /// The return value depends on whether the captive stream supports reading.
        /// </remarks>
        // Token: 0x17000100 RID: 256
        // (get) Token: 0x06000402 RID: 1026 RVA: 0x0001E89C File Offset: 0x0001CA9C
        public override bool CanRead
        {
            get
            {
                if (this._disposed)
                {
                    throw new ObjectDisposedException("DeflateStream");
                }
                return this._baseStream._stream.CanRead;
            }
        }

        /// <summary>
        /// Indicates whether the stream supports Seek operations.
        /// </summary>
        /// <remarks>
        /// Always returns false.
        /// </remarks>
        // Token: 0x17000101 RID: 257
        // (get) Token: 0x06000403 RID: 1027 RVA: 0x0001E8D8 File Offset: 0x0001CAD8
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
        // Token: 0x17000102 RID: 258
        // (get) Token: 0x06000404 RID: 1028 RVA: 0x0001E8EC File Offset: 0x0001CAEC
        public override bool CanWrite
        {
            get
            {
                if (this._disposed)
                {
                    throw new ObjectDisposedException("DeflateStream");
                }
                return this._baseStream._stream.CanWrite;
            }
        }

        /// <summary>
        /// Flush the stream.
        /// </summary>
        // Token: 0x06000405 RID: 1029 RVA: 0x0001E928 File Offset: 0x0001CB28
        public override void Flush()
        {
            if (this._disposed)
            {
                throw new ObjectDisposedException("DeflateStream");
            }
            this._baseStream.Flush();
        }

        /// <summary>
        /// Reading this property always throws a <see cref="T:System.NotImplementedException" />.
        /// </summary>
        // Token: 0x17000103 RID: 259
        // (get) Token: 0x06000406 RID: 1030 RVA: 0x0001E95A File Offset: 0x0001CB5A
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
        ///   Setting this property always throws a <see cref="T:System.NotImplementedException" />. Reading will return the total bytes
        ///   written out, if used in writing, or the total bytes read in, if used in
        ///   reading.  The count may refer to compressed bytes or uncompressed bytes,
        ///   depending on how you've used the stream.
        /// </remarks>
        // Token: 0x17000104 RID: 260
        // (get) Token: 0x06000407 RID: 1031 RVA: 0x0001E964 File Offset: 0x0001CB64
        // (set) Token: 0x06000408 RID: 1032 RVA: 0x0001E9C8 File Offset: 0x0001CBC8
        public override long Position
        {
            get
            {
                long result;
                if (this._baseStream._streamMode == ZlibBaseStream.StreamMode.Writer)
                {
                    result = this._baseStream._z.TotalBytesOut;
                }
                else if (this._baseStream._streamMode == ZlibBaseStream.StreamMode.Reader)
                {
                    result = this._baseStream._z.TotalBytesIn;
                }
                else
                {
                    result = 0L;
                }
                return result;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Read data from the stream.
        /// </summary>
        /// <remarks>
        ///
        /// <para>
        ///   If you wish to use the <c>DeflateStream</c> to compress data while
        ///   reading, you can create a <c>DeflateStream</c> with
        ///   <c>CompressionMode.Compress</c>, providing an uncompressed data stream.
        ///   Then call Read() on that <c>DeflateStream</c>, and the data read will be
        ///   compressed as you read.  If you wish to use the <c>DeflateStream</c> to
        ///   decompress data while reading, you can create a <c>DeflateStream</c> with
        ///   <c>CompressionMode.Decompress</c>, providing a readable compressed data
        ///   stream.  Then call Read() on that <c>DeflateStream</c>, and the data read
        ///   will be decompressed as you read.
        /// </para>
        ///
        /// <para>
        ///   A <c>DeflateStream</c> can be used for <c>Read()</c> or <c>Write()</c>, but not both.
        /// </para>
        ///
        /// </remarks>
        /// <param name="buffer">The buffer into which the read data should be placed.</param>
        /// <param name="offset">the offset within that data array to put the first byte read.</param>
        /// <param name="count">the number of bytes to read.</param>
        /// <returns>the number of bytes actually read</returns>
        // Token: 0x06000409 RID: 1033 RVA: 0x0001E9D0 File Offset: 0x0001CBD0
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (this._disposed)
            {
                throw new ObjectDisposedException("DeflateStream");
            }
            return this._baseStream.Read(buffer, offset, count);
        }

        /// <summary>
        /// Calling this method always throws a <see cref="T:System.NotImplementedException" />.
        /// </summary>
        /// <param name="offset">this is irrelevant, since it will always throw!</param>
        /// <param name="origin">this is irrelevant, since it will always throw!</param>
        /// <returns>irrelevant!</returns>
        // Token: 0x0600040A RID: 1034 RVA: 0x0001EA08 File Offset: 0x0001CC08
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Calling this method always throws a <see cref="T:System.NotImplementedException" />.
        /// </summary>
        /// <param name="value">this is irrelevant, since it will always throw!</param>
        // Token: 0x0600040B RID: 1035 RVA: 0x0001EA10 File Offset: 0x0001CC10
        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///   Write data to the stream.
        /// </summary>
        /// <remarks>
        ///
        /// <para>
        ///   If you wish to use the <c>DeflateStream</c> to compress data while
        ///   writing, you can create a <c>DeflateStream</c> with
        ///   <c>CompressionMode.Compress</c>, and a writable output stream.  Then call
        ///   <c>Write()</c> on that <c>DeflateStream</c>, providing uncompressed data
        ///   as input.  The data sent to the output stream will be the compressed form
        ///   of the data written.  If you wish to use the <c>DeflateStream</c> to
        ///   decompress data while writing, you can create a <c>DeflateStream</c> with
        ///   <c>CompressionMode.Decompress</c>, and a writable output stream.  Then
        ///   call <c>Write()</c> on that stream, providing previously compressed
        ///   data. The data sent to the output stream will be the decompressed form of
        ///   the data written.
        /// </para>
        ///
        /// <para>
        ///   A <c>DeflateStream</c> can be used for <c>Read()</c> or <c>Write()</c>,
        ///   but not both.
        /// </para>
        ///
        /// </remarks>
        ///
        /// <param name="buffer">The buffer holding data to write to the stream.</param>
        /// <param name="offset">the offset within that data array to find the first byte to write.</param>
        /// <param name="count">the number of bytes to write.</param>
        // Token: 0x0600040C RID: 1036 RVA: 0x0001EA18 File Offset: 0x0001CC18
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (this._disposed)
            {
                throw new ObjectDisposedException("DeflateStream");
            }
            this._baseStream.Write(buffer, offset, count);
        }

        /// <summary>
        ///   Compress a string into a byte array using DEFLATE (RFC 1951).
        /// </summary>
        ///
        /// <remarks>
        ///   Uncompress it with <see cref="M:Ionic.Zlib.DeflateStream.UncompressString(System.Byte[])" />.
        /// </remarks>
        ///
        /// <seealso cref="M:Ionic.Zlib.DeflateStream.UncompressString(System.Byte[])">DeflateStream.UncompressString(byte[])</seealso>
        /// <seealso cref="M:Ionic.Zlib.DeflateStream.CompressBuffer(System.Byte[])">DeflateStream.CompressBuffer(byte[])</seealso>
        /// <seealso cref="M:Ionic.Zlib.GZipStream.CompressString(System.String)">GZipStream.CompressString(string)</seealso>
        /// <seealso cref="M:Ionic.Zlib.ZlibStream.CompressString(System.String)">ZlibStream.CompressString(string)</seealso>
        ///
        /// <param name="s">
        ///   A string to compress. The string will first be encoded
        ///   using UTF8, then compressed.
        /// </param>
        ///
        /// <returns>The string in compressed form</returns>
        // Token: 0x0600040D RID: 1037 RVA: 0x0001EA50 File Offset: 0x0001CC50
        public static byte[] CompressString(string s)
        {
            byte[] result;
            using (MemoryStream ms = new MemoryStream())
            {
                Stream compressor = new DeflateStream(ms, CompressionMode.Compress, CompressionLevel.BestCompression);
                ZlibBaseStream.CompressString(s, compressor);
                result = ms.ToArray();
            }
            return result;
        }

        /// <summary>
        ///   Compress a byte array into a new byte array using DEFLATE.
        /// </summary>
        ///
        /// <remarks>
        ///   Uncompress it with <see cref="M:Ionic.Zlib.DeflateStream.UncompressBuffer(System.Byte[])" />.
        /// </remarks>
        ///
        /// <seealso cref="M:Ionic.Zlib.DeflateStream.CompressString(System.String)">DeflateStream.CompressString(string)</seealso>
        /// <seealso cref="M:Ionic.Zlib.DeflateStream.UncompressBuffer(System.Byte[])">DeflateStream.UncompressBuffer(byte[])</seealso>
        /// <seealso cref="M:Ionic.Zlib.GZipStream.CompressBuffer(System.Byte[])">GZipStream.CompressBuffer(byte[])</seealso>
        /// <seealso cref="M:Ionic.Zlib.ZlibStream.CompressBuffer(System.Byte[])">ZlibStream.CompressBuffer(byte[])</seealso>
        ///
        /// <param name="b">
        ///   A buffer to compress.
        /// </param>
        ///
        /// <returns>The data in compressed form</returns>
        // Token: 0x0600040E RID: 1038 RVA: 0x0001EAA4 File Offset: 0x0001CCA4
        public static byte[] CompressBuffer(byte[] b)
        {
            byte[] result;
            using (MemoryStream ms = new MemoryStream())
            {
                Stream compressor = new DeflateStream(ms, CompressionMode.Compress, CompressionLevel.BestCompression);
                ZlibBaseStream.CompressBuffer(b, compressor);
                result = ms.ToArray();
            }
            return result;
        }

        /// <summary>
        ///   Uncompress a DEFLATE'd byte array into a single string.
        /// </summary>
        ///
        /// <seealso cref="M:Ionic.Zlib.DeflateStream.CompressString(System.String)">DeflateStream.CompressString(String)</seealso>
        /// <seealso cref="M:Ionic.Zlib.DeflateStream.UncompressBuffer(System.Byte[])">DeflateStream.UncompressBuffer(byte[])</seealso>
        /// <seealso cref="M:Ionic.Zlib.GZipStream.UncompressString(System.Byte[])">GZipStream.UncompressString(byte[])</seealso>
        /// <seealso cref="M:Ionic.Zlib.ZlibStream.UncompressString(System.Byte[])">ZlibStream.UncompressString(byte[])</seealso>
        ///
        /// <param name="compressed">
        ///   A buffer containing DEFLATE-compressed data.
        /// </param>
        ///
        /// <returns>The uncompressed string</returns>
        // Token: 0x0600040F RID: 1039 RVA: 0x0001EAF8 File Offset: 0x0001CCF8
        public static string UncompressString(byte[] compressed)
        {
            string result;
            using (MemoryStream input = new MemoryStream(compressed))
            {
                Stream decompressor = new DeflateStream(input, CompressionMode.Decompress);
                result = ZlibBaseStream.UncompressString(compressed, decompressor);
            }
            return result;
        }

        /// <summary>
        ///   Uncompress a DEFLATE'd byte array into a byte array.
        /// </summary>
        ///
        /// <seealso cref="M:Ionic.Zlib.DeflateStream.CompressBuffer(System.Byte[])">DeflateStream.CompressBuffer(byte[])</seealso>
        /// <seealso cref="M:Ionic.Zlib.DeflateStream.UncompressString(System.Byte[])">DeflateStream.UncompressString(byte[])</seealso>
        /// <seealso cref="M:Ionic.Zlib.GZipStream.UncompressBuffer(System.Byte[])">GZipStream.UncompressBuffer(byte[])</seealso>
        /// <seealso cref="M:Ionic.Zlib.ZlibStream.UncompressBuffer(System.Byte[])">ZlibStream.UncompressBuffer(byte[])</seealso>
        ///
        /// <param name="compressed">
        ///   A buffer containing data that has been compressed with DEFLATE.
        /// </param>
        ///
        /// <returns>The data in uncompressed form</returns>
        // Token: 0x06000410 RID: 1040 RVA: 0x0001EB44 File Offset: 0x0001CD44
        public static byte[] UncompressBuffer(byte[] compressed)
        {
            byte[] result;
            using (MemoryStream input = new MemoryStream(compressed))
            {
                Stream decompressor = new DeflateStream(input, CompressionMode.Decompress);
                result = ZlibBaseStream.UncompressBuffer(compressed, decompressor);
            }
            return result;
        }

        // Token: 0x040002AD RID: 685
        internal ZlibBaseStream _baseStream;

        // Token: 0x040002AE RID: 686
        internal Stream _innerStream;

        // Token: 0x040002AF RID: 687
        private bool _disposed;
    }
}


namespace Ionic.Zlib
{
    /// <summary>
    /// Describes how to flush the current deflate operation.
    /// </summary>
    /// <remarks>
    /// The different FlushType values are useful when using a Deflate in a streaming application.
    /// </remarks>
    // Token: 0x02000061 RID: 97
    public enum FlushType
    {
        /// <summary>No flush at all.</summary>
        // Token: 0x0400036D RID: 877
        None,
        /// <summary>Closes the current block, but doesn't flush it to
        /// the output. Used internally only in hypothetical
        /// scenarios.  This was supposed to be removed by Zlib, but it is
        /// still in use in some edge cases.
        /// </summary>
        // Token: 0x0400036E RID: 878
        Partial,
        /// <summary>
        /// Use this during compression to specify that all pending output should be
        /// flushed to the output buffer and the output should be aligned on a byte
        /// boundary.  You might use this in a streaming communication scenario, so that
        /// the decompressor can get all input data available so far.  When using this
        /// with a ZlibCodec, <c>AvailableBytesIn</c> will be zero after the call if
        /// enough output space has been provided before the call.  Flushing will
        /// degrade compression and so it should be used only when necessary.
        /// </summary>
        // Token: 0x0400036F RID: 879
        Sync,
        /// <summary>
        /// Use this during compression to specify that all output should be flushed, as
        /// with <c>FlushType.Sync</c>, but also, the compression state should be reset
        /// so that decompression can restart from this point if previous compressed
        /// data has been damaged or if random access is desired.  Using
        /// <c>FlushType.Full</c> too often can significantly degrade the compression.
        /// </summary>
        // Token: 0x04000370 RID: 880
        Full,
        /// <summary>Signals the end of the compression/decompression stream.</summary>
        // Token: 0x04000371 RID: 881
        Finish
    }
}


namespace Ionic.Zlib
{
    /// <summary>
    ///   A class for compressing and decompressing GZIP streams.
    /// </summary>
    /// <remarks>
    ///
    /// <para>
    ///   The <c>GZipStream</c> is a <see href="http://en.wikipedia.org/wiki/Decorator_pattern">Decorator</see> on a
    ///   <see cref="T:System.IO.Stream" />. It adds GZIP compression or decompression to any
    ///   stream.
    /// </para>
    ///
    /// <para>
    ///   Like the <c>System.IO.Compression.GZipStream</c> in the .NET Base Class Library, the
    ///   <c>Ionic.Zlib.GZipStream</c> can compress while writing, or decompress while
    ///   reading, but not vice versa.  The compression method used is GZIP, which is
    ///   documented in <see href="http://www.ietf.org/rfc/rfc1952.txt">IETF RFC
    ///   1952</see>, "GZIP file format specification version 4.3".</para>
    ///
    /// <para>
    ///   A <c>GZipStream</c> can be used to decompress data (through <c>Read()</c>) or
    ///   to compress data (through <c>Write()</c>), but not both.
    /// </para>
    ///
    /// <para>
    ///   If you wish to use the <c>GZipStream</c> to compress data, you must wrap it
    ///   around a write-able stream. As you call <c>Write()</c> on the <c>GZipStream</c>, the
    ///   data will be compressed into the GZIP format.  If you want to decompress data,
    ///   you must wrap the <c>GZipStream</c> around a readable stream that contains an
    ///   IETF RFC 1952-compliant stream.  The data will be decompressed as you call
    ///   <c>Read()</c> on the <c>GZipStream</c>.
    /// </para>
    ///
    /// <para>
    ///   Though the GZIP format allows data from multiple files to be concatenated
    ///   together, this stream handles only a single segment of GZIP format, typically
    ///   representing a single file.
    /// </para>
    ///
    /// <para>
    ///   This class is similar to <see cref="T:Ionic.Zlib.ZlibStream" /> and <see cref="T:Ionic.Zlib.DeflateStream" />.
    ///   <c>ZlibStream</c> handles RFC1950-compliant streams.  <see cref="T:Ionic.Zlib.DeflateStream" />
    ///   handles RFC1951-compliant streams. This class handles RFC1952-compliant streams.
    /// </para>
    ///
    /// </remarks>
    ///
    /// <seealso cref="T:Ionic.Zlib.DeflateStream" />
    /// <seealso cref="T:Ionic.Zlib.ZlibStream" />
    // Token: 0x02000055 RID: 85
    public class GZipStream : Stream
    {
        /// <summary>
        ///   The comment on the GZIP stream.
        /// </summary>
        ///
        /// <remarks>
        /// <para>
        ///   The GZIP format allows for each file to optionally have an associated
        ///   comment stored with the file.  The comment is encoded with the ISO-8859-1
        ///   code page.  To include a comment in a GZIP stream you create, set this
        ///   property before calling <c>Write()</c> for the first time on the
        ///   <c>GZipStream</c>.
        /// </para>
        ///
        /// <para>
        ///   When using <c>GZipStream</c> to decompress, you can retrieve this property
        ///   after the first call to <c>Read()</c>.  If no comment has been set in the
        ///   GZIP bytestream, the Comment property will return <c>null</c>
        ///   (<c>Nothing</c> in VB).
        /// </para>
        /// </remarks>
        // Token: 0x17000105 RID: 261
        // (get) Token: 0x06000411 RID: 1041 RVA: 0x0001EB90 File Offset: 0x0001CD90
        // (set) Token: 0x06000412 RID: 1042 RVA: 0x0001EBA8 File Offset: 0x0001CDA8
        public string Comment
        {
            get
            {
                return this._Comment;
            }
            set
            {
                if (this._disposed)
                {
                    throw new ObjectDisposedException("GZipStream");
                }
                this._Comment = value;
            }
        }

        /// <summary>
        ///   The FileName for the GZIP stream.
        /// </summary>
        ///
        /// <remarks>
        ///
        /// <para>
        ///   The GZIP format optionally allows each file to have an associated
        ///   filename.  When compressing data (through <c>Write()</c>), set this
        ///   FileName before calling <c>Write()</c> the first time on the <c>GZipStream</c>.
        ///   The actual filename is encoded into the GZIP bytestream with the
        ///   ISO-8859-1 code page, according to RFC 1952. It is the application's
        ///   responsibility to insure that the FileName can be encoded and decoded
        ///   correctly with this code page.
        /// </para>
        ///
        /// <para>
        ///   When decompressing (through <c>Read()</c>), you can retrieve this value
        ///   any time after the first <c>Read()</c>.  In the case where there was no filename
        ///   encoded into the GZIP bytestream, the property will return <c>null</c> (<c>Nothing</c>
        ///   in VB).
        /// </para>
        /// </remarks>
        // Token: 0x17000106 RID: 262
        // (get) Token: 0x06000413 RID: 1043 RVA: 0x0001EBD8 File Offset: 0x0001CDD8
        // (set) Token: 0x06000414 RID: 1044 RVA: 0x0001EBF0 File Offset: 0x0001CDF0
        public string FileName
        {
            get
            {
                return this._FileName;
            }
            set
            {
                if (this._disposed)
                {
                    throw new ObjectDisposedException("GZipStream");
                }
                this._FileName = value;
                if (this._FileName != null)
                {
                    if (this._FileName.IndexOf("/") != -1)
                    {
                        this._FileName = this._FileName.Replace("/", "\\");
                    }
                    if (this._FileName.EndsWith("\\"))
                    {
                        throw new Exception("Illegal filename");
                    }
                    if (this._FileName.IndexOf("\\") != -1)
                    {
                        this._FileName = Path.GetFileName(this._FileName);
                    }
                }
            }
        }

        /// <summary>
        /// The CRC on the GZIP stream.
        /// </summary>
        /// <remarks>
        /// This is used for internal error checking. You probably don't need to look at this property.
        /// </remarks>
        // Token: 0x17000107 RID: 263
        // (get) Token: 0x06000415 RID: 1045 RVA: 0x0001ECB4 File Offset: 0x0001CEB4
        public int Crc32
        {
            get
            {
                return this._Crc32;
            }
        }

        /// <summary>
        ///   Create a <c>GZipStream</c> using the specified <c>CompressionMode</c>.
        /// </summary>
        /// <remarks>
        ///
        /// <para>
        ///   When mode is <c>CompressionMode.Compress</c>, the <c>GZipStream</c> will use the
        ///   default compression level.
        /// </para>
        ///
        /// <para>
        ///   As noted in the class documentation, the <c>CompressionMode</c> (Compress
        ///   or Decompress) also establishes the "direction" of the stream.  A
        ///   <c>GZipStream</c> with <c>CompressionMode.Compress</c> works only through
        ///   <c>Write()</c>.  A <c>GZipStream</c> with
        ///   <c>CompressionMode.Decompress</c> works only through <c>Read()</c>.
        /// </para>
        ///
        /// </remarks>
        ///
        /// <example>
        ///   This example shows how to use a GZipStream to compress data.
        /// <code>
        /// using (System.IO.Stream input = System.IO.File.OpenRead(fileToCompress))
        /// {
        ///     using (var raw = System.IO.File.Create(outputFile))
        ///     {
        ///         using (Stream compressor = new GZipStream(raw, CompressionMode.Compress))
        ///         {
        ///             byte[] buffer = new byte[WORKING_BUFFER_SIZE];
        ///             int n;
        ///             while ((n= input.Read(buffer, 0, buffer.Length)) != 0)
        ///             {
        ///                 compressor.Write(buffer, 0, n);
        ///             }
        ///         }
        ///     }
        /// }
        /// </code>
        /// <code lang="VB">
        /// Dim outputFile As String = (fileToCompress &amp; ".compressed")
        /// Using input As Stream = File.OpenRead(fileToCompress)
        ///     Using raw As FileStream = File.Create(outputFile)
        ///     Using compressor As Stream = New GZipStream(raw, CompressionMode.Compress)
        ///         Dim buffer As Byte() = New Byte(4096) {}
        ///         Dim n As Integer = -1
        ///         Do While (n &lt;&gt; 0)
        ///             If (n &gt; 0) Then
        ///                 compressor.Write(buffer, 0, n)
        ///             End If
        ///             n = input.Read(buffer, 0, buffer.Length)
        ///         Loop
        ///     End Using
        ///     End Using
        /// End Using
        /// </code>
        /// </example>
        ///
        /// <example>
        /// This example shows how to use a GZipStream to uncompress a file.
        /// <code>
        /// private void GunZipFile(string filename)
        /// {
        ///     if (!filename.EndsWith(".gz))
        ///         throw new ArgumentException("filename");
        ///     var DecompressedFile = filename.Substring(0,filename.Length-3);
        ///     byte[] working = new byte[WORKING_BUFFER_SIZE];
        ///     int n= 1;
        ///     using (System.IO.Stream input = System.IO.File.OpenRead(filename))
        ///     {
        ///         using (Stream decompressor= new Ionic.Zlib.GZipStream(input, CompressionMode.Decompress, true))
        ///         {
        ///             using (var output = System.IO.File.Create(DecompressedFile))
        ///             {
        ///                 while (n !=0)
        ///                 {
        ///                     n= decompressor.Read(working, 0, working.Length);
        ///                     if (n &gt; 0)
        ///                     {
        ///                         output.Write(working, 0, n);
        ///                     }
        ///                 }
        ///             }
        ///         }
        ///     }
        /// }
        /// </code>
        ///
        /// <code lang="VB">
        /// Private Sub GunZipFile(ByVal filename as String)
        ///     If Not (filename.EndsWith(".gz)) Then
        ///         Throw New ArgumentException("filename")
        ///     End If
        ///     Dim DecompressedFile as String = filename.Substring(0,filename.Length-3)
        ///     Dim working(WORKING_BUFFER_SIZE) as Byte
        ///     Dim n As Integer = 1
        ///     Using input As Stream = File.OpenRead(filename)
        ///         Using decompressor As Stream = new Ionic.Zlib.GZipStream(input, CompressionMode.Decompress, True)
        ///             Using output As Stream = File.Create(UncompressedFile)
        ///                 Do
        ///                     n= decompressor.Read(working, 0, working.Length)
        ///                     If n &gt; 0 Then
        ///                         output.Write(working, 0, n)
        ///                     End IF
        ///                 Loop While (n  &gt; 0)
        ///             End Using
        ///         End Using
        ///     End Using
        /// End Sub
        /// </code>
        /// </example>
        ///
        /// <param name="stream">The stream which will be read or written.</param>
        /// <param name="mode">Indicates whether the GZipStream will compress or decompress.</param>
        // Token: 0x06000416 RID: 1046 RVA: 0x0001ECCC File Offset: 0x0001CECC
        public GZipStream(Stream stream, CompressionMode mode) : this(stream, mode, CompressionLevel.Default, false)
        {
        }

        /// <summary>
        ///   Create a <c>GZipStream</c> using the specified <c>CompressionMode</c> and
        ///   the specified <c>CompressionLevel</c>.
        /// </summary>
        /// <remarks>
        ///
        /// <para>
        ///   The <c>CompressionMode</c> (Compress or Decompress) also establishes the
        ///   "direction" of the stream.  A <c>GZipStream</c> with
        ///   <c>CompressionMode.Compress</c> works only through <c>Write()</c>.  A
        ///   <c>GZipStream</c> with <c>CompressionMode.Decompress</c> works only
        ///   through <c>Read()</c>.
        /// </para>
        ///
        /// </remarks>
        ///
        /// <example>
        ///
        /// This example shows how to use a <c>GZipStream</c> to compress a file into a .gz file.
        ///
        /// <code>
        /// using (System.IO.Stream input = System.IO.File.OpenRead(fileToCompress))
        /// {
        ///     using (var raw = System.IO.File.Create(fileToCompress + ".gz"))
        ///     {
        ///         using (Stream compressor = new GZipStream(raw,
        ///                                                   CompressionMode.Compress,
        ///                                                   CompressionLevel.BestCompression))
        ///         {
        ///             byte[] buffer = new byte[WORKING_BUFFER_SIZE];
        ///             int n;
        ///             while ((n= input.Read(buffer, 0, buffer.Length)) != 0)
        ///             {
        ///                 compressor.Write(buffer, 0, n);
        ///             }
        ///         }
        ///     }
        /// }
        /// </code>
        ///
        /// <code lang="VB">
        /// Using input As Stream = File.OpenRead(fileToCompress)
        ///     Using raw As FileStream = File.Create(fileToCompress &amp; ".gz")
        ///         Using compressor As Stream = New GZipStream(raw, CompressionMode.Compress, CompressionLevel.BestCompression)
        ///             Dim buffer As Byte() = New Byte(4096) {}
        ///             Dim n As Integer = -1
        ///             Do While (n &lt;&gt; 0)
        ///                 If (n &gt; 0) Then
        ///                     compressor.Write(buffer, 0, n)
        ///                 End If
        ///                 n = input.Read(buffer, 0, buffer.Length)
        ///             Loop
        ///         End Using
        ///     End Using
        /// End Using
        /// </code>
        /// </example>
        /// <param name="stream">The stream to be read or written while deflating or inflating.</param>
        /// <param name="mode">Indicates whether the <c>GZipStream</c> will compress or decompress.</param>
        /// <param name="level">A tuning knob to trade speed for effectiveness.</param>
        // Token: 0x06000417 RID: 1047 RVA: 0x0001ECDB File Offset: 0x0001CEDB
        public GZipStream(Stream stream, CompressionMode mode, CompressionLevel level) : this(stream, mode, level, false)
        {
        }

        /// <summary>
        ///   Create a <c>GZipStream</c> using the specified <c>CompressionMode</c>, and
        ///   explicitly specify whether the stream should be left open after Deflation
        ///   or Inflation.
        /// </summary>
        ///
        /// <remarks>
        /// <para>
        ///   This constructor allows the application to request that the captive stream
        ///   remain open after the deflation or inflation occurs.  By default, after
        ///   <c>Close()</c> is called on the stream, the captive stream is also
        ///   closed. In some cases this is not desired, for example if the stream is a
        ///   memory stream that will be re-read after compressed data has been written
        ///   to it.  Specify true for the <paramref name="leaveOpen" /> parameter to leave
        ///   the stream open.
        /// </para>
        ///
        /// <para>
        ///   The <see cref="T:Ionic.Zlib.CompressionMode" /> (Compress or Decompress) also
        ///   establishes the "direction" of the stream.  A <c>GZipStream</c> with
        ///   <c>CompressionMode.Compress</c> works only through <c>Write()</c>.  A <c>GZipStream</c>
        ///   with <c>CompressionMode.Decompress</c> works only through <c>Read()</c>.
        /// </para>
        ///
        /// <para>
        ///   The <c>GZipStream</c> will use the default compression level. If you want
        ///   to specify the compression level, see <see cref="M:Ionic.Zlib.GZipStream.#ctor(System.IO.Stream,Ionic.Zlib.CompressionMode,Ionic.Zlib.CompressionLevel,System.Boolean)" />.
        /// </para>
        ///
        /// <para>
        ///   See the other overloads of this constructor for example code.
        /// </para>
        ///
        /// </remarks>
        ///
        /// <param name="stream">
        ///   The stream which will be read or written. This is called the "captive"
        ///   stream in other places in this documentation.
        /// </param>
        ///
        /// <param name="mode">Indicates whether the GZipStream will compress or decompress.
        /// </param>
        ///
        /// <param name="leaveOpen">
        ///   true if the application would like the base stream to remain open after
        ///   inflation/deflation.
        /// </param>
        // Token: 0x06000418 RID: 1048 RVA: 0x0001ECEA File Offset: 0x0001CEEA
        public GZipStream(Stream stream, CompressionMode mode, bool leaveOpen) : this(stream, mode, CompressionLevel.Default, leaveOpen)
        {
        }

        /// <summary>
        ///   Create a <c>GZipStream</c> using the specified <c>CompressionMode</c> and the
        ///   specified <c>CompressionLevel</c>, and explicitly specify whether the
        ///   stream should be left open after Deflation or Inflation.
        /// </summary>
        ///
        /// <remarks>
        ///
        /// <para>
        ///   This constructor allows the application to request that the captive stream
        ///   remain open after the deflation or inflation occurs.  By default, after
        ///   <c>Close()</c> is called on the stream, the captive stream is also
        ///   closed. In some cases this is not desired, for example if the stream is a
        ///   memory stream that will be re-read after compressed data has been written
        ///   to it.  Specify true for the <paramref name="leaveOpen" /> parameter to
        ///   leave the stream open.
        /// </para>
        ///
        /// <para>
        ///   As noted in the class documentation, the <c>CompressionMode</c> (Compress
        ///   or Decompress) also establishes the "direction" of the stream.  A
        ///   <c>GZipStream</c> with <c>CompressionMode.Compress</c> works only through
        ///   <c>Write()</c>.  A <c>GZipStream</c> with <c>CompressionMode.Decompress</c> works only
        ///   through <c>Read()</c>.
        /// </para>
        ///
        /// </remarks>
        ///
        /// <example>
        ///   This example shows how to use a <c>GZipStream</c> to compress data.
        /// <code>
        /// using (System.IO.Stream input = System.IO.File.OpenRead(fileToCompress))
        /// {
        ///     using (var raw = System.IO.File.Create(outputFile))
        ///     {
        ///         using (Stream compressor = new GZipStream(raw, CompressionMode.Compress, CompressionLevel.BestCompression, true))
        ///         {
        ///             byte[] buffer = new byte[WORKING_BUFFER_SIZE];
        ///             int n;
        ///             while ((n= input.Read(buffer, 0, buffer.Length)) != 0)
        ///             {
        ///                 compressor.Write(buffer, 0, n);
        ///             }
        ///         }
        ///     }
        /// }
        /// </code>
        /// <code lang="VB">
        /// Dim outputFile As String = (fileToCompress &amp; ".compressed")
        /// Using input As Stream = File.OpenRead(fileToCompress)
        ///     Using raw As FileStream = File.Create(outputFile)
        ///     Using compressor As Stream = New GZipStream(raw, CompressionMode.Compress, CompressionLevel.BestCompression, True)
        ///         Dim buffer As Byte() = New Byte(4096) {}
        ///         Dim n As Integer = -1
        ///         Do While (n &lt;&gt; 0)
        ///             If (n &gt; 0) Then
        ///                 compressor.Write(buffer, 0, n)
        ///             End If
        ///             n = input.Read(buffer, 0, buffer.Length)
        ///         Loop
        ///     End Using
        ///     End Using
        /// End Using
        /// </code>
        /// </example>
        /// <param name="stream">The stream which will be read or written.</param>
        /// <param name="mode">Indicates whether the GZipStream will compress or decompress.</param>
        /// <param name="leaveOpen">true if the application would like the stream to remain open after inflation/deflation.</param>
        /// <param name="level">A tuning knob to trade speed for effectiveness.</param>
        // Token: 0x06000419 RID: 1049 RVA: 0x0001ECF9 File Offset: 0x0001CEF9
        public GZipStream(Stream stream, CompressionMode mode, CompressionLevel level, bool leaveOpen)
        {
            this._baseStream = new ZlibBaseStream(stream, mode, level, ZlibStreamFlavor.GZIP, leaveOpen);
        }

        /// <summary>
        /// This property sets the flush behavior on the stream.
        /// </summary>
        // Token: 0x17000108 RID: 264
        // (get) Token: 0x0600041A RID: 1050 RVA: 0x0001ED1C File Offset: 0x0001CF1C
        // (set) Token: 0x0600041B RID: 1051 RVA: 0x0001ED3C File Offset: 0x0001CF3C
        public virtual FlushType FlushMode
        {
            get
            {
                return this._baseStream._flushMode;
            }
            set
            {
                if (this._disposed)
                {
                    throw new ObjectDisposedException("GZipStream");
                }
                this._baseStream._flushMode = value;
            }
        }

        /// <summary>
        ///   The size of the working buffer for the compression codec.
        /// </summary>
        ///
        /// <remarks>
        /// <para>
        ///   The working buffer is used for all stream operations.  The default size is
        ///   1024 bytes.  The minimum size is 128 bytes. You may get better performance
        ///   with a larger buffer.  Then again, you might not.  You would have to test
        ///   it.
        /// </para>
        ///
        /// <para>
        ///   Set this before the first call to <c>Read()</c> or <c>Write()</c> on the
        ///   stream. If you try to set it afterwards, it will throw.
        /// </para>
        /// </remarks>
        // Token: 0x17000109 RID: 265
        // (get) Token: 0x0600041C RID: 1052 RVA: 0x0001ED70 File Offset: 0x0001CF70
        // (set) Token: 0x0600041D RID: 1053 RVA: 0x0001ED90 File Offset: 0x0001CF90
        public int BufferSize
        {
            get
            {
                return this._baseStream._bufferSize;
            }
            set
            {
                if (this._disposed)
                {
                    throw new ObjectDisposedException("GZipStream");
                }
                if (this._baseStream._workingBuffer != null)
                {
                    throw new ZlibException("The working buffer is already set.");
                }
                if (value < 1024)
                {
                    throw new ZlibException(string.Format("Don't be silly. {0} bytes?? Use a bigger buffer, at least {1}.", value, 1024));
                }
                this._baseStream._bufferSize = value;
            }
        }

        /// <summary> Returns the total number of bytes input so far.</summary>
        // Token: 0x1700010A RID: 266
        // (get) Token: 0x0600041E RID: 1054 RVA: 0x0001EE10 File Offset: 0x0001D010
        public virtual long TotalIn
        {
            get
            {
                return this._baseStream._z.TotalBytesIn;
            }
        }

        /// <summary> Returns the total number of bytes output so far.</summary>
        // Token: 0x1700010B RID: 267
        // (get) Token: 0x0600041F RID: 1055 RVA: 0x0001EE34 File Offset: 0x0001D034
        public virtual long TotalOut
        {
            get
            {
                return this._baseStream._z.TotalBytesOut;
            }
        }

        /// <summary>
        ///   Dispose the stream.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     This may or may not result in a <c>Close()</c> call on the captive
        ///     stream.  See the constructors that have a <c>leaveOpen</c> parameter
        ///     for more information.
        ///   </para>
        ///   <para>
        ///     This method may be invoked in two distinct scenarios.  If disposing
        ///     == true, the method has been called directly or indirectly by a
        ///     user's code, for example via the public Dispose() method. In this
        ///     case, both managed and unmanaged resources can be referenced and
        ///     disposed.  If disposing == false, the method has been called by the
        ///     runtime from inside the object finalizer and this method should not
        ///     reference other objects; in that case only unmanaged resources must
        ///     be referenced or disposed.
        ///   </para>
        /// </remarks>
        /// <param name="disposing">
        ///   indicates whether the Dispose method was invoked by user code.
        /// </param>
        // Token: 0x06000420 RID: 1056 RVA: 0x0001EE58 File Offset: 0x0001D058
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!this._disposed)
                {
                    if (disposing && this._baseStream != null)
                    {
                        this._baseStream.Close();
                        this._Crc32 = this._baseStream.Crc32;
                    }
                    this._disposed = true;
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Indicates whether the stream can be read.
        /// </summary>
        /// <remarks>
        /// The return value depends on whether the captive stream supports reading.
        /// </remarks>
        // Token: 0x1700010C RID: 268
        // (get) Token: 0x06000421 RID: 1057 RVA: 0x0001EECC File Offset: 0x0001D0CC
        public override bool CanRead
        {
            get
            {
                if (this._disposed)
                {
                    throw new ObjectDisposedException("GZipStream");
                }
                return this._baseStream._stream.CanRead;
            }
        }

        /// <summary>
        /// Indicates whether the stream supports Seek operations.
        /// </summary>
        /// <remarks>
        /// Always returns false.
        /// </remarks>
        // Token: 0x1700010D RID: 269
        // (get) Token: 0x06000422 RID: 1058 RVA: 0x0001EF08 File Offset: 0x0001D108
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
        // Token: 0x1700010E RID: 270
        // (get) Token: 0x06000423 RID: 1059 RVA: 0x0001EF1C File Offset: 0x0001D11C
        public override bool CanWrite
        {
            get
            {
                if (this._disposed)
                {
                    throw new ObjectDisposedException("GZipStream");
                }
                return this._baseStream._stream.CanWrite;
            }
        }

        /// <summary>
        /// Flush the stream.
        /// </summary>
        // Token: 0x06000424 RID: 1060 RVA: 0x0001EF58 File Offset: 0x0001D158
        public override void Flush()
        {
            if (this._disposed)
            {
                throw new ObjectDisposedException("GZipStream");
            }
            this._baseStream.Flush();
        }

        /// <summary>
        /// Reading this property always throws a <see cref="T:System.NotImplementedException" />.
        /// </summary>
        // Token: 0x1700010F RID: 271
        // (get) Token: 0x06000425 RID: 1061 RVA: 0x0001EF8A File Offset: 0x0001D18A
        public override long Length
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        ///   The position of the stream pointer.
        /// </summary>
        ///
        /// <remarks>
        ///   Setting this property always throws a <see cref="T:System.NotImplementedException" />. Reading will return the total bytes
        ///   written out, if used in writing, or the total bytes read in, if used in
        ///   reading.  The count may refer to compressed bytes or uncompressed bytes,
        ///   depending on how you've used the stream.
        /// </remarks>
        // Token: 0x17000110 RID: 272
        // (get) Token: 0x06000426 RID: 1062 RVA: 0x0001EF94 File Offset: 0x0001D194
        // (set) Token: 0x06000427 RID: 1063 RVA: 0x0001F00D File Offset: 0x0001D20D
        public override long Position
        {
            get
            {
                long result;
                if (this._baseStream._streamMode == ZlibBaseStream.StreamMode.Writer)
                {
                    result = this._baseStream._z.TotalBytesOut + (long)this._headerByteCount;
                }
                else if (this._baseStream._streamMode == ZlibBaseStream.StreamMode.Reader)
                {
                    result = this._baseStream._z.TotalBytesIn + (long)this._baseStream._gzipHeaderByteCount;
                }
                else
                {
                    result = 0L;
                }
                return result;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        ///   Read and decompress data from the source stream.
        /// </summary>
        ///
        /// <remarks>
        ///   With a <c>GZipStream</c>, decompression is done through reading.
        /// </remarks>
        ///
        /// <example>
        /// <code>
        /// byte[] working = new byte[WORKING_BUFFER_SIZE];
        /// using (System.IO.Stream input = System.IO.File.OpenRead(_CompressedFile))
        /// {
        ///     using (Stream decompressor= new Ionic.Zlib.GZipStream(input, CompressionMode.Decompress, true))
        ///     {
        ///         using (var output = System.IO.File.Create(_DecompressedFile))
        ///         {
        ///             int n;
        ///             while ((n= decompressor.Read(working, 0, working.Length)) !=0)
        ///             {
        ///                 output.Write(working, 0, n);
        ///             }
        ///         }
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <param name="buffer">The buffer into which the decompressed data should be placed.</param>
        /// <param name="offset">the offset within that data array to put the first byte read.</param>
        /// <param name="count">the number of bytes to read.</param>
        /// <returns>the number of bytes actually read</returns>
        // Token: 0x06000428 RID: 1064 RVA: 0x0001F018 File Offset: 0x0001D218
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (this._disposed)
            {
                throw new ObjectDisposedException("GZipStream");
            }
            int i = this._baseStream.Read(buffer, offset, count);
            if (!this._firstReadDone)
            {
                this._firstReadDone = true;
                this.FileName = this._baseStream._GzipFileName;
                this.Comment = this._baseStream._GzipComment;
            }
            return i;
        }

        /// <summary>
        ///   Calling this method always throws a <see cref="T:System.NotImplementedException" />.
        /// </summary>
        /// <param name="offset">irrelevant; it will always throw!</param>
        /// <param name="origin">irrelevant; it will always throw!</param>
        /// <returns>irrelevant!</returns>
        // Token: 0x06000429 RID: 1065 RVA: 0x0001F089 File Offset: 0x0001D289
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///   Calling this method always throws a <see cref="T:System.NotImplementedException" />.
        /// </summary>
        /// <param name="value">irrelevant; this method will always throw!</param>
        // Token: 0x0600042A RID: 1066 RVA: 0x0001F091 File Offset: 0x0001D291
        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///   Write data to the stream.
        /// </summary>
        ///
        /// <remarks>
        /// <para>
        ///   If you wish to use the <c>GZipStream</c> to compress data while writing,
        ///   you can create a <c>GZipStream</c> with <c>CompressionMode.Compress</c>, and a
        ///   writable output stream.  Then call <c>Write()</c> on that <c>GZipStream</c>,
        ///   providing uncompressed data as input.  The data sent to the output stream
        ///   will be the compressed form of the data written.
        /// </para>
        ///
        /// <para>
        ///   A <c>GZipStream</c> can be used for <c>Read()</c> or <c>Write()</c>, but not
        ///   both. Writing implies compression.  Reading implies decompression.
        /// </para>
        ///
        /// </remarks>
        /// <param name="buffer">The buffer holding data to write to the stream.</param>
        /// <param name="offset">the offset within that data array to find the first byte to write.</param>
        /// <param name="count">the number of bytes to write.</param>
        // Token: 0x0600042B RID: 1067 RVA: 0x0001F09C File Offset: 0x0001D29C
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (this._disposed)
            {
                throw new ObjectDisposedException("GZipStream");
            }
            if (this._baseStream._streamMode == ZlibBaseStream.StreamMode.Undefined)
            {
                if (!this._baseStream._wantCompress)
                {
                    throw new InvalidOperationException();
                }
                this._headerByteCount = this.EmitHeader();
            }
            this._baseStream.Write(buffer, offset, count);
        }

        // Token: 0x0600042C RID: 1068 RVA: 0x0001F114 File Offset: 0x0001D314
        private int EmitHeader()
        {
            byte[] commentBytes = (this.Comment == null) ? null : GZipStream.iso8859dash1.GetBytes(this.Comment);
            byte[] filenameBytes = (this.FileName == null) ? null : GZipStream.iso8859dash1.GetBytes(this.FileName);
            int cbLength = (this.Comment == null) ? 0 : (commentBytes.Length + 1);
            int fnLength = (this.FileName == null) ? 0 : (filenameBytes.Length + 1);
            int bufferLength = 10 + cbLength + fnLength;
            byte[] header = new byte[bufferLength];
            int i = 0;
            header[i++] = 31;
            header[i++] = 139;
            header[i++] = 8;
            byte flag = 0;
            if (this.Comment != null)
            {
                flag ^= 16;
            }
            if (this.FileName != null)
            {
                flag ^= 8;
            }
            header[i++] = flag;
            if (this.LastModified == null)
            {
                this.LastModified = new DateTime?(DateTime.Now);
            }
            int timet = (int)(this.LastModified.Value - GZipStream._unixEpoch).TotalSeconds;
            Array.Copy(BitConverter.GetBytes(timet), 0, header, i, 4);
            i += 4;
            header[i++] = 0;
            header[i++] = byte.MaxValue;
            if (fnLength != 0)
            {
                Array.Copy(filenameBytes, 0, header, i, fnLength - 1);
                i += fnLength - 1;
                header[i++] = 0;
            }
            if (cbLength != 0)
            {
                Array.Copy(commentBytes, 0, header, i, cbLength - 1);
                i += cbLength - 1;
                header[i++] = 0;
            }
            this._baseStream._stream.Write(header, 0, header.Length);
            return header.Length;
        }

        /// <summary>
        ///   Compress a string into a byte array using GZip.
        /// </summary>
        ///
        /// <remarks>
        ///   Uncompress it with <see cref="M:Ionic.Zlib.GZipStream.UncompressString(System.Byte[])" />.
        /// </remarks>
        ///
        /// <seealso cref="M:Ionic.Zlib.GZipStream.UncompressString(System.Byte[])" />
        /// <seealso cref="M:Ionic.Zlib.GZipStream.CompressBuffer(System.Byte[])" />
        ///
        /// <param name="s">
        ///   A string to compress. The string will first be encoded
        ///   using UTF8, then compressed.
        /// </param>
        ///
        /// <returns>The string in compressed form</returns>
        // Token: 0x0600042D RID: 1069 RVA: 0x0001F2E8 File Offset: 0x0001D4E8
        public static byte[] CompressString(string s)
        {
            byte[] result;
            using (MemoryStream ms = new MemoryStream())
            {
                Stream compressor = new GZipStream(ms, CompressionMode.Compress, CompressionLevel.BestCompression);
                ZlibBaseStream.CompressString(s, compressor);
                result = ms.ToArray();
            }
            return result;
        }

        /// <summary>
        ///   Compress a byte array into a new byte array using GZip.
        /// </summary>
        ///
        /// <remarks>
        ///   Uncompress it with <see cref="M:Ionic.Zlib.GZipStream.UncompressBuffer(System.Byte[])" />.
        /// </remarks>
        ///
        /// <seealso cref="M:Ionic.Zlib.GZipStream.CompressString(System.String)" />
        /// <seealso cref="M:Ionic.Zlib.GZipStream.UncompressBuffer(System.Byte[])" />
        ///
        /// <param name="b">
        ///   A buffer to compress.
        /// </param>
        ///
        /// <returns>The data in compressed form</returns>
        // Token: 0x0600042E RID: 1070 RVA: 0x0001F33C File Offset: 0x0001D53C
        public static byte[] CompressBuffer(byte[] b)
        {
            byte[] result;
            using (MemoryStream ms = new MemoryStream())
            {
                Stream compressor = new GZipStream(ms, CompressionMode.Compress, CompressionLevel.BestCompression);
                ZlibBaseStream.CompressBuffer(b, compressor);
                result = ms.ToArray();
            }
            return result;
        }

        /// <summary>
        ///   Uncompress a GZip'ed byte array into a single string.
        /// </summary>
        ///
        /// <seealso cref="M:Ionic.Zlib.GZipStream.CompressString(System.String)" />
        /// <seealso cref="M:Ionic.Zlib.GZipStream.UncompressBuffer(System.Byte[])" />
        ///
        /// <param name="compressed">
        ///   A buffer containing GZIP-compressed data.
        /// </param>
        ///
        /// <returns>The uncompressed string</returns>
        // Token: 0x0600042F RID: 1071 RVA: 0x0001F390 File Offset: 0x0001D590
        public static string UncompressString(byte[] compressed)
        {
            string result;
            using (MemoryStream input = new MemoryStream(compressed))
            {
                Stream decompressor = new GZipStream(input, CompressionMode.Decompress);
                result = ZlibBaseStream.UncompressString(compressed, decompressor);
            }
            return result;
        }

        /// <summary>
        ///   Uncompress a GZip'ed byte array into a byte array.
        /// </summary>
        ///
        /// <seealso cref="M:Ionic.Zlib.GZipStream.CompressBuffer(System.Byte[])" />
        /// <seealso cref="M:Ionic.Zlib.GZipStream.UncompressString(System.Byte[])" />
        ///
        /// <param name="compressed">
        ///   A buffer containing data that has been compressed with GZip.
        /// </param>
        ///
        /// <returns>The data in uncompressed form</returns>
        // Token: 0x06000430 RID: 1072 RVA: 0x0001F3DC File Offset: 0x0001D5DC
        public static byte[] UncompressBuffer(byte[] compressed)
        {
            byte[] result;
            using (MemoryStream input = new MemoryStream(compressed))
            {
                Stream decompressor = new GZipStream(input, CompressionMode.Decompress);
                result = ZlibBaseStream.UncompressBuffer(compressed, decompressor);
            }
            return result;
        }

        /// <summary>
        ///   The last modified time for the GZIP stream.
        /// </summary>
        ///
        /// <remarks>
        ///   GZIP allows the storage of a last modified time with each GZIP entry.
        ///   When compressing data, you can set this before the first call to
        ///   <c>Write()</c>.  When decompressing, you can retrieve this value any time
        ///   after the first call to <c>Read()</c>.
        /// </remarks>
        // Token: 0x040002B0 RID: 688
        public DateTime? LastModified;

        // Token: 0x040002B1 RID: 689
        private int _headerByteCount;

        // Token: 0x040002B2 RID: 690
        internal ZlibBaseStream _baseStream;

        // Token: 0x040002B3 RID: 691
        private bool _disposed;

        // Token: 0x040002B4 RID: 692
        private bool _firstReadDone;

        // Token: 0x040002B5 RID: 693
        private string _FileName;

        // Token: 0x040002B6 RID: 694
        private string _Comment;

        // Token: 0x040002B7 RID: 695
        private int _Crc32;

        // Token: 0x040002B8 RID: 696
        internal static readonly DateTime _unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Token: 0x040002B9 RID: 697
        internal static readonly Encoding iso8859dash1 = Encoding.GetEncoding("iso-8859-1");
    }
}


namespace Ionic.Zlib
{
    // Token: 0x02000056 RID: 86
    internal sealed class InflateBlocks
    {
        // Token: 0x06000432 RID: 1074 RVA: 0x0001F450 File Offset: 0x0001D650
        internal InflateBlocks(ZlibCodec codec, object checkfn, int w)
        {
            this._codec = codec;
            this.hufts = new int[4320];
            this.window = new byte[w];
            this.end = w;
            this.checkfn = checkfn;
            this.mode = InflateBlocks.InflateBlockMode.TYPE;
            this.Reset();
        }

        // Token: 0x06000433 RID: 1075 RVA: 0x0001F4D4 File Offset: 0x0001D6D4
        internal uint Reset()
        {
            uint oldCheck = this.check;
            this.mode = InflateBlocks.InflateBlockMode.TYPE;
            this.bitk = 0;
            this.bitb = 0;
            this.readAt = (this.writeAt = 0);
            if (this.checkfn != null)
            {
                this._codec._Adler32 = (this.check = Adler.Adler32(0u, null, 0, 0));
            }
            return oldCheck;
        }

        // Token: 0x06000434 RID: 1076 RVA: 0x0001F540 File Offset: 0x0001D740
        internal int Process(int r)
        {
            int p = this._codec.NextIn;
            int i = this._codec.AvailableBytesIn;
            int b = this.bitb;
            int j = this.bitk;
            int q = this.writeAt;
            int k = (q < this.readAt) ? (this.readAt - q - 1) : (this.end - q);
            int t;
            for (; ; )
            {
                int[] bl;
                int[] bd;
                switch (this.mode)
                {
                    case InflateBlocks.InflateBlockMode.TYPE:
                        while (j < 3)
                        {
                            if (i == 0)
                            {
                                goto IL_AC;
                            }
                            r = 0;
                            i--;
                            b |= (int)(this._codec.InputBuffer[p++] & byte.MaxValue) << j;
                            j += 8;
                        }
                        t = (b & 7);
                        this.last = (t & 1);
                        switch ((uint)t >> 1)
                        {
                            case 0u:
                                b >>= 3;
                                j -= 3;
                                t = (j & 7);
                                b >>= t;
                                j -= t;
                                this.mode = InflateBlocks.InflateBlockMode.LENS;
                                break;
                            case 1u:
                                {
                                    bl = new int[1];
                                    bd = new int[1];
                                    int[][] tl = new int[1][];
                                    int[][] td = new int[1][];
                                    InfTree.inflate_trees_fixed(bl, bd, tl, td, this._codec);
                                    this.codes.Init(bl[0], bd[0], tl[0], 0, td[0], 0);
                                    b >>= 3;
                                    j -= 3;
                                    this.mode = InflateBlocks.InflateBlockMode.CODES;
                                    break;
                                }
                            case 2u:
                                b >>= 3;
                                j -= 3;
                                this.mode = InflateBlocks.InflateBlockMode.TABLE;
                                break;
                            case 3u:
                                goto IL_20C;
                        }
                        continue;
                    case InflateBlocks.InflateBlockMode.LENS:
                        while (j < 32)
                        {
                            if (i == 0)
                            {
                                goto IL_2AA;
                            }
                            r = 0;
                            i--;
                            b |= (int)(this._codec.InputBuffer[p++] & byte.MaxValue) << j;
                            j += 8;
                        }
                        if ((~b >> 16 & 65535) != (b & 65535))
                        {
                            goto Block_8;
                        }
                        this.left = (b & 65535);
                        j = (b = 0);
                        this.mode = ((this.left != 0) ? InflateBlocks.InflateBlockMode.STORED : ((this.last != 0) ? InflateBlocks.InflateBlockMode.DRY : InflateBlocks.InflateBlockMode.TYPE));
                        continue;
                    case InflateBlocks.InflateBlockMode.STORED:
                        if (i == 0)
                        {
                            goto Block_11;
                        }
                        if (k == 0)
                        {
                            if (q == this.end && this.readAt != 0)
                            {
                                q = 0;
                                k = ((q < this.readAt) ? (this.readAt - q - 1) : (this.end - q));
                            }
                            if (k == 0)
                            {
                                this.writeAt = q;
                                r = this.Flush(r);
                                q = this.writeAt;
                                k = ((q < this.readAt) ? (this.readAt - q - 1) : (this.end - q));
                                if (q == this.end && this.readAt != 0)
                                {
                                    q = 0;
                                    k = ((q < this.readAt) ? (this.readAt - q - 1) : (this.end - q));
                                }
                                if (k == 0)
                                {
                                    goto Block_21;
                                }
                            }
                        }
                        r = 0;
                        t = this.left;
                        if (t > i)
                        {
                            t = i;
                        }
                        if (t > k)
                        {
                            t = k;
                        }
                        Array.Copy(this._codec.InputBuffer, p, this.window, q, t);
                        p += t;
                        i -= t;
                        q += t;
                        k -= t;
                        if ((this.left -= t) != 0)
                        {
                            continue;
                        }
                        this.mode = ((this.last != 0) ? InflateBlocks.InflateBlockMode.DRY : InflateBlocks.InflateBlockMode.TYPE);
                        continue;
                    case InflateBlocks.InflateBlockMode.TABLE:
                        while (j < 14)
                        {
                            if (i == 0)
                            {
                                goto IL_673;
                            }
                            r = 0;
                            i--;
                            b |= (int)(this._codec.InputBuffer[p++] & byte.MaxValue) << j;
                            j += 8;
                        }
                        t = (this.table = (b & 16383));
                        if ((t & 31) > 29 || (t >> 5 & 31) > 29)
                        {
                            goto Block_29;
                        }
                        t = 258 + (t & 31) + (t >> 5 & 31);
                        if (this.blens == null || this.blens.Length < t)
                        {
                            this.blens = new int[t];
                        }
                        else
                        {
                            Array.Clear(this.blens, 0, t);
                        }
                        b >>= 14;
                        j -= 14;
                        this.index = 0;
                        this.mode = InflateBlocks.InflateBlockMode.BTREE;
                        goto IL_81B;
                    case InflateBlocks.InflateBlockMode.BTREE:
                        goto IL_81B;
                    case InflateBlocks.InflateBlockMode.DTREE:
                        goto IL_A1B;
                    case InflateBlocks.InflateBlockMode.CODES:
                        goto IL_EBF;
                    case InflateBlocks.InflateBlockMode.DRY:
                        goto IL_FB2;
                    case InflateBlocks.InflateBlockMode.DONE:
                        goto IL_1068;
                    case InflateBlocks.InflateBlockMode.BAD:
                        goto IL_10C8;
                }
                break;
                continue;
                IL_EBF:
                this.bitb = b;
                this.bitk = j;
                this._codec.AvailableBytesIn = i;
                this._codec.TotalBytesIn += (long)(p - this._codec.NextIn);
                this._codec.NextIn = p;
                this.writeAt = q;
                r = this.codes.Process(this, r);
                if (r != 1)
                {
                    goto Block_53;
                }
                r = 0;
                p = this._codec.NextIn;
                i = this._codec.AvailableBytesIn;
                b = this.bitb;
                j = this.bitk;
                q = this.writeAt;
                k = ((q < this.readAt) ? (this.readAt - q - 1) : (this.end - q));
                if (this.last == 0)
                {
                    this.mode = InflateBlocks.InflateBlockMode.TYPE;
                    continue;
                }
                goto IL_FA9;
                IL_A1B:
                for (; ; )
                {
                    t = this.table;
                    if (this.index >= 258 + (t & 31) + (t >> 5 & 31))
                    {
                        break;
                    }
                    t = this.bb[0];
                    while (j < t)
                    {
                        if (i == 0)
                        {
                            goto IL_A6E;
                        }
                        r = 0;
                        i--;
                        b |= (int)(this._codec.InputBuffer[p++] & byte.MaxValue) << j;
                        j += 8;
                    }
                    t = this.hufts[(this.tb[0] + (b & InternalInflateConstants.InflateMask[t])) * 3 + 1];
                    int c = this.hufts[(this.tb[0] + (b & InternalInflateConstants.InflateMask[t])) * 3 + 2];
                    if (c < 16)
                    {
                        b >>= t;
                        j -= t;
                        this.blens[this.index++] = c;
                    }
                    else
                    {
                        int l = (c == 18) ? 7 : (c - 14);
                        int m = (c == 18) ? 11 : 3;
                        while (j < t + l)
                        {
                            if (i == 0)
                            {
                                goto IL_BB3;
                            }
                            r = 0;
                            i--;
                            b |= (int)(this._codec.InputBuffer[p++] & byte.MaxValue) << j;
                            j += 8;
                        }
                        b >>= t;
                        j -= t;
                        m += (b & InternalInflateConstants.InflateMask[l]);
                        b >>= l;
                        j -= l;
                        l = this.index;
                        t = this.table;
                        if (l + m > 258 + (t & 31) + (t >> 5 & 31) || (c == 16 && l < 1))
                        {
                            goto Block_48;
                        }
                        c = ((c == 16) ? this.blens[l - 1] : 0);
                        do
                        {
                            this.blens[l++] = c;
                        }
                        while (--m != 0);
                        this.index = l;
                    }
                }
                this.tb[0] = -1;
                bl = new int[]
                {
                    9
                };
                bd = new int[]
                {
                    6
                };
                int[] tl2 = new int[1];
                int[] td2 = new int[1];
                t = this.table;
                t = this.inftree.inflate_trees_dynamic(257 + (t & 31), 1 + (t >> 5 & 31), this.blens, bl, bd, tl2, td2, this.hufts, this._codec);
                if (t != 0)
                {
                    goto Block_51;
                }
                this.codes.Init(bl[0], bd[0], this.hufts, tl2[0], this.hufts, td2[0]);
                this.mode = InflateBlocks.InflateBlockMode.CODES;
                goto IL_EBF;
                IL_81B:
                while (this.index < 4 + (this.table >> 10))
                {
                    while (j < 3)
                    {
                        if (i == 0)
                        {
                            goto IL_839;
                        }
                        r = 0;
                        i--;
                        b |= (int)(this._codec.InputBuffer[p++] & byte.MaxValue) << j;
                        j += 8;
                    }
                    this.blens[InflateBlocks.border[this.index++]] = (b & 7);
                    b >>= 3;
                    j -= 3;
                }
                while (this.index < 19)
                {
                    this.blens[InflateBlocks.border[this.index++]] = 0;
                }
                this.bb[0] = 7;
                t = this.inftree.inflate_trees_bits(this.blens, this.bb, this.tb, this.hufts, this._codec);
                if (t != 0)
                {
                    goto Block_36;
                }
                this.index = 0;
                this.mode = InflateBlocks.InflateBlockMode.DTREE;
                goto IL_A1B;
            }
            r = -2;
            this.bitb = b;
            this.bitk = j;
            this._codec.AvailableBytesIn = i;
            this._codec.TotalBytesIn += (long)(p - this._codec.NextIn);
            this._codec.NextIn = p;
            this.writeAt = q;
            return this.Flush(r);
            IL_AC:
            this.bitb = b;
            this.bitk = j;
            this._codec.AvailableBytesIn = i;
            this._codec.TotalBytesIn += (long)(p - this._codec.NextIn);
            this._codec.NextIn = p;
            this.writeAt = q;
            return this.Flush(r);
            IL_20C:
            b >>= 3;
            j -= 3;
            this.mode = InflateBlocks.InflateBlockMode.BAD;
            this._codec.Message = "invalid block type";
            r = -3;
            this.bitb = b;
            this.bitk = j;
            this._codec.AvailableBytesIn = i;
            this._codec.TotalBytesIn += (long)(p - this._codec.NextIn);
            this._codec.NextIn = p;
            this.writeAt = q;
            return this.Flush(r);
            IL_2AA:
            this.bitb = b;
            this.bitk = j;
            this._codec.AvailableBytesIn = i;
            this._codec.TotalBytesIn += (long)(p - this._codec.NextIn);
            this._codec.NextIn = p;
            this.writeAt = q;
            return this.Flush(r);
            Block_8:
            this.mode = InflateBlocks.InflateBlockMode.BAD;
            this._codec.Message = "invalid stored block lengths";
            r = -3;
            this.bitb = b;
            this.bitk = j;
            this._codec.AvailableBytesIn = i;
            this._codec.TotalBytesIn += (long)(p - this._codec.NextIn);
            this._codec.NextIn = p;
            this.writeAt = q;
            return this.Flush(r);
            Block_11:
            this.bitb = b;
            this.bitk = j;
            this._codec.AvailableBytesIn = i;
            this._codec.TotalBytesIn += (long)(p - this._codec.NextIn);
            this._codec.NextIn = p;
            this.writeAt = q;
            return this.Flush(r);
            Block_21:
            this.bitb = b;
            this.bitk = j;
            this._codec.AvailableBytesIn = i;
            this._codec.TotalBytesIn += (long)(p - this._codec.NextIn);
            this._codec.NextIn = p;
            this.writeAt = q;
            return this.Flush(r);
            IL_673:
            this.bitb = b;
            this.bitk = j;
            this._codec.AvailableBytesIn = i;
            this._codec.TotalBytesIn += (long)(p - this._codec.NextIn);
            this._codec.NextIn = p;
            this.writeAt = q;
            return this.Flush(r);
            Block_29:
            this.mode = InflateBlocks.InflateBlockMode.BAD;
            this._codec.Message = "too many length or distance symbols";
            r = -3;
            this.bitb = b;
            this.bitk = j;
            this._codec.AvailableBytesIn = i;
            this._codec.TotalBytesIn += (long)(p - this._codec.NextIn);
            this._codec.NextIn = p;
            this.writeAt = q;
            return this.Flush(r);
            IL_839:
            this.bitb = b;
            this.bitk = j;
            this._codec.AvailableBytesIn = i;
            this._codec.TotalBytesIn += (long)(p - this._codec.NextIn);
            this._codec.NextIn = p;
            this.writeAt = q;
            return this.Flush(r);
            Block_36:
            r = t;
            if (r == -3)
            {
                this.blens = null;
                this.mode = InflateBlocks.InflateBlockMode.BAD;
            }
            this.bitb = b;
            this.bitk = j;
            this._codec.AvailableBytesIn = i;
            this._codec.TotalBytesIn += (long)(p - this._codec.NextIn);
            this._codec.NextIn = p;
            this.writeAt = q;
            return this.Flush(r);
            IL_A6E:
            this.bitb = b;
            this.bitk = j;
            this._codec.AvailableBytesIn = i;
            this._codec.TotalBytesIn += (long)(p - this._codec.NextIn);
            this._codec.NextIn = p;
            this.writeAt = q;
            return this.Flush(r);
            IL_BB3:
            this.bitb = b;
            this.bitk = j;
            this._codec.AvailableBytesIn = i;
            this._codec.TotalBytesIn += (long)(p - this._codec.NextIn);
            this._codec.NextIn = p;
            this.writeAt = q;
            return this.Flush(r);
            Block_48:
            this.blens = null;
            this.mode = InflateBlocks.InflateBlockMode.BAD;
            this._codec.Message = "invalid bit length repeat";
            r = -3;
            this.bitb = b;
            this.bitk = j;
            this._codec.AvailableBytesIn = i;
            this._codec.TotalBytesIn += (long)(p - this._codec.NextIn);
            this._codec.NextIn = p;
            this.writeAt = q;
            return this.Flush(r);
            Block_51:
            if (t == -3)
            {
                this.blens = null;
                this.mode = InflateBlocks.InflateBlockMode.BAD;
            }
            r = t;
            this.bitb = b;
            this.bitk = j;
            this._codec.AvailableBytesIn = i;
            this._codec.TotalBytesIn += (long)(p - this._codec.NextIn);
            this._codec.NextIn = p;
            this.writeAt = q;
            return this.Flush(r);
            Block_53:
            return this.Flush(r);
            IL_FA9:
            this.mode = InflateBlocks.InflateBlockMode.DRY;
            IL_FB2:
            this.writeAt = q;
            r = this.Flush(r);
            q = this.writeAt;
            int num = (q < this.readAt) ? (this.readAt - q - 1) : (this.end - q);
            if (this.readAt != this.writeAt)
            {
                this.bitb = b;
                this.bitk = j;
                this._codec.AvailableBytesIn = i;
                this._codec.TotalBytesIn += (long)(p - this._codec.NextIn);
                this._codec.NextIn = p;
                this.writeAt = q;
                return this.Flush(r);
            }
            this.mode = InflateBlocks.InflateBlockMode.DONE;
            IL_1068:
            r = 1;
            this.bitb = b;
            this.bitk = j;
            this._codec.AvailableBytesIn = i;
            this._codec.TotalBytesIn += (long)(p - this._codec.NextIn);
            this._codec.NextIn = p;
            this.writeAt = q;
            return this.Flush(r);
            IL_10C8:
            r = -3;
            this.bitb = b;
            this.bitk = j;
            this._codec.AvailableBytesIn = i;
            this._codec.TotalBytesIn += (long)(p - this._codec.NextIn);
            this._codec.NextIn = p;
            this.writeAt = q;
            return this.Flush(r);
        }

        // Token: 0x06000435 RID: 1077 RVA: 0x000206DC File Offset: 0x0001E8DC
        internal void Free()
        {
            this.Reset();
            this.window = null;
            this.hufts = null;
        }

        // Token: 0x06000436 RID: 1078 RVA: 0x000206F4 File Offset: 0x0001E8F4
        internal void SetDictionary(byte[] d, int start, int n)
        {
            Array.Copy(d, start, this.window, 0, n);
            this.writeAt = n;
            this.readAt = n;
        }

        // Token: 0x06000437 RID: 1079 RVA: 0x00020724 File Offset: 0x0001E924
        internal int SyncPoint()
        {
            return (this.mode == InflateBlocks.InflateBlockMode.LENS) ? 1 : 0;
        }

        // Token: 0x06000438 RID: 1080 RVA: 0x00020744 File Offset: 0x0001E944
        internal int Flush(int r)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                int nBytes;
                if (pass == 0)
                {
                    nBytes = ((this.readAt <= this.writeAt) ? this.writeAt : this.end) - this.readAt;
                }
                else
                {
                    nBytes = this.writeAt - this.readAt;
                }
                if (nBytes == 0)
                {
                    if (r == -5)
                    {
                        r = 0;
                    }
                    return r;
                }
                if (nBytes > this._codec.AvailableBytesOut)
                {
                    nBytes = this._codec.AvailableBytesOut;
                }
                if (nBytes != 0 && r == -5)
                {
                    r = 0;
                }
                this._codec.AvailableBytesOut -= nBytes;
                this._codec.TotalBytesOut += (long)nBytes;
                if (this.checkfn != null)
                {
                    this._codec._Adler32 = (this.check = Adler.Adler32(this.check, this.window, this.readAt, nBytes));
                }
                Array.Copy(this.window, this.readAt, this._codec.OutputBuffer, this._codec.NextOut, nBytes);
                this._codec.NextOut += nBytes;
                this.readAt += nBytes;
                if (this.readAt == this.end && pass == 0)
                {
                    this.readAt = 0;
                    if (this.writeAt == this.end)
                    {
                        this.writeAt = 0;
                    }
                }
                else
                {
                    pass++;
                }
            }
            return r;
        }

        // Token: 0x040002BA RID: 698
        private const int MANY = 1440;

        // Token: 0x040002BB RID: 699
        internal static readonly int[] border = new int[]
        {
            16,
            17,
            18,
            0,
            8,
            7,
            9,
            6,
            10,
            5,
            11,
            4,
            12,
            3,
            13,
            2,
            14,
            1,
            15
        };

        // Token: 0x040002BC RID: 700
        private InflateBlocks.InflateBlockMode mode;

        // Token: 0x040002BD RID: 701
        internal int left;

        // Token: 0x040002BE RID: 702
        internal int table;

        // Token: 0x040002BF RID: 703
        internal int index;

        // Token: 0x040002C0 RID: 704
        internal int[] blens;

        // Token: 0x040002C1 RID: 705
        internal int[] bb = new int[1];

        // Token: 0x040002C2 RID: 706
        internal int[] tb = new int[1];

        // Token: 0x040002C3 RID: 707
        internal InflateCodes codes = new InflateCodes();

        // Token: 0x040002C4 RID: 708
        internal int last;

        // Token: 0x040002C5 RID: 709
        internal ZlibCodec _codec;

        // Token: 0x040002C6 RID: 710
        internal int bitk;

        // Token: 0x040002C7 RID: 711
        internal int bitb;

        // Token: 0x040002C8 RID: 712
        internal int[] hufts;

        // Token: 0x040002C9 RID: 713
        internal byte[] window;

        // Token: 0x040002CA RID: 714
        internal int end;

        // Token: 0x040002CB RID: 715
        internal int readAt;

        // Token: 0x040002CC RID: 716
        internal int writeAt;

        // Token: 0x040002CD RID: 717
        internal object checkfn;

        // Token: 0x040002CE RID: 718
        internal uint check;

        // Token: 0x040002CF RID: 719
        internal InfTree inftree = new InfTree();

        // Token: 0x02000057 RID: 87
        private enum InflateBlockMode
        {
            // Token: 0x040002D1 RID: 721
            TYPE,
            // Token: 0x040002D2 RID: 722
            LENS,
            // Token: 0x040002D3 RID: 723
            STORED,
            // Token: 0x040002D4 RID: 724
            TABLE,
            // Token: 0x040002D5 RID: 725
            BTREE,
            // Token: 0x040002D6 RID: 726
            DTREE,
            // Token: 0x040002D7 RID: 727
            CODES,
            // Token: 0x040002D8 RID: 728
            DRY,
            // Token: 0x040002D9 RID: 729
            DONE,
            // Token: 0x040002DA RID: 730
            BAD
        }
    }
}


namespace Ionic.Zlib
{
    // Token: 0x02000059 RID: 89
    internal sealed class InflateCodes
    {
        // Token: 0x0600043B RID: 1083 RVA: 0x000209C5 File Offset: 0x0001EBC5
        internal InflateCodes()
        {
        }

        // Token: 0x0600043C RID: 1084 RVA: 0x000209D7 File Offset: 0x0001EBD7
        internal void Init(int bl, int bd, int[] tl, int tl_index, int[] td, int td_index)
        {
            this.mode = 0;
            this.lbits = (byte)bl;
            this.dbits = (byte)bd;
            this.ltree = tl;
            this.ltree_index = tl_index;
            this.dtree = td;
            this.dtree_index = td_index;
            this.tree = null;
        }

        // Token: 0x0600043D RID: 1085 RVA: 0x00020A18 File Offset: 0x0001EC18
        internal int Process(InflateBlocks blocks, int r)
        {
            ZlibCodec z = blocks._codec;
            int p = z.NextIn;
            int i = z.AvailableBytesIn;
            int b = blocks.bitb;
            int j = blocks.bitk;
            int q = blocks.writeAt;
            int k = (q < blocks.readAt) ? (blocks.readAt - q - 1) : (blocks.end - q);
            for (; ; )
            {
                int l;
                switch (this.mode)
                {
                    case 0:
                        if (k >= 258 && i >= 10)
                        {
                            blocks.bitb = b;
                            blocks.bitk = j;
                            z.AvailableBytesIn = i;
                            z.TotalBytesIn += (long)(p - z.NextIn);
                            z.NextIn = p;
                            blocks.writeAt = q;
                            r = this.InflateFast((int)this.lbits, (int)this.dbits, this.ltree, this.ltree_index, this.dtree, this.dtree_index, blocks, z);
                            p = z.NextIn;
                            i = z.AvailableBytesIn;
                            b = blocks.bitb;
                            j = blocks.bitk;
                            q = blocks.writeAt;
                            k = ((q < blocks.readAt) ? (blocks.readAt - q - 1) : (blocks.end - q));
                            if (r != 0)
                            {
                                this.mode = ((r == 1) ? 7 : 9);
                                continue;
                            }
                        }
                        this.need = (int)this.lbits;
                        this.tree = this.ltree;
                        this.tree_index = this.ltree_index;
                        this.mode = 1;
                        goto IL_1C3;
                    case 1:
                        goto IL_1C3;
                    case 2:
                        l = this.bitsToGet;
                        while (j < l)
                        {
                            if (i == 0)
                            {
                                goto IL_3D3;
                            }
                            r = 0;
                            i--;
                            b |= (int)(z.InputBuffer[p++] & byte.MaxValue) << j;
                            j += 8;
                        }
                        this.len += (b & InternalInflateConstants.InflateMask[l]);
                        b >>= l;
                        j -= l;
                        this.need = (int)this.dbits;
                        this.tree = this.dtree;
                        this.tree_index = this.dtree_index;
                        this.mode = 3;
                        goto IL_4AD;
                    case 3:
                        goto IL_4AD;
                    case 4:
                        l = this.bitsToGet;
                        while (j < l)
                        {
                            if (i == 0)
                            {
                                goto IL_679;
                            }
                            r = 0;
                            i--;
                            b |= (int)(z.InputBuffer[p++] & byte.MaxValue) << j;
                            j += 8;
                        }
                        this.dist += (b & InternalInflateConstants.InflateMask[l]);
                        b >>= l;
                        j -= l;
                        this.mode = 5;
                        goto IL_72F;
                    case 5:
                        goto IL_72F;
                    case 6:
                        if (k == 0)
                        {
                            if (q == blocks.end && blocks.readAt != 0)
                            {
                                q = 0;
                                k = ((q < blocks.readAt) ? (blocks.readAt - q - 1) : (blocks.end - q));
                            }
                            if (k == 0)
                            {
                                blocks.writeAt = q;
                                r = blocks.Flush(r);
                                q = blocks.writeAt;
                                k = ((q < blocks.readAt) ? (blocks.readAt - q - 1) : (blocks.end - q));
                                if (q == blocks.end && blocks.readAt != 0)
                                {
                                    q = 0;
                                    k = ((q < blocks.readAt) ? (blocks.readAt - q - 1) : (blocks.end - q));
                                }
                                if (k == 0)
                                {
                                    goto Block_44;
                                }
                            }
                        }
                        r = 0;
                        blocks.window[q++] = (byte)this.lit;
                        k--;
                        this.mode = 0;
                        continue;
                    case 7:
                        goto IL_A78;
                    case 8:
                        goto IL_B43;
                    case 9:
                        goto IL_B96;
                }
                break;
                continue;
                IL_1C3:
                l = this.need;
                while (j < l)
                {
                    if (i == 0)
                    {
                        goto IL_1E0;
                    }
                    r = 0;
                    i--;
                    b |= (int)(z.InputBuffer[p++] & byte.MaxValue) << j;
                    j += 8;
                }
                int tindex = (this.tree_index + (b & InternalInflateConstants.InflateMask[l])) * 3;
                b >>= this.tree[tindex + 1];
                j -= this.tree[tindex + 1];
                int e = this.tree[tindex];
                if (e == 0)
                {
                    this.lit = this.tree[tindex + 2];
                    this.mode = 6;
                    continue;
                }
                if ((e & 16) != 0)
                {
                    this.bitsToGet = (e & 15);
                    this.len = this.tree[tindex + 2];
                    this.mode = 2;
                    continue;
                }
                if ((e & 64) == 0)
                {
                    this.need = e;
                    this.tree_index = tindex / 3 + this.tree[tindex + 2];
                    continue;
                }
                if ((e & 32) != 0)
                {
                    this.mode = 7;
                    continue;
                }
                goto IL_34E;
                IL_4AD:
                l = this.need;
                while (j < l)
                {
                    if (i == 0)
                    {
                        goto IL_4CA;
                    }
                    r = 0;
                    i--;
                    b |= (int)(z.InputBuffer[p++] & byte.MaxValue) << j;
                    j += 8;
                }
                tindex = (this.tree_index + (b & InternalInflateConstants.InflateMask[l])) * 3;
                b >>= this.tree[tindex + 1];
                j -= this.tree[tindex + 1];
                e = this.tree[tindex];
                if ((e & 16) != 0)
                {
                    this.bitsToGet = (e & 15);
                    this.dist = this.tree[tindex + 2];
                    this.mode = 4;
                    continue;
                }
                if ((e & 64) == 0)
                {
                    this.need = e;
                    this.tree_index = tindex / 3 + this.tree[tindex + 2];
                    continue;
                }
                goto IL_5F4;
                IL_72F:
                int f;
                for (f = q - this.dist; f < 0; f += blocks.end)
                {
                }
                while (this.len != 0)
                {
                    if (k == 0)
                    {
                        if (q == blocks.end && blocks.readAt != 0)
                        {
                            q = 0;
                            k = ((q < blocks.readAt) ? (blocks.readAt - q - 1) : (blocks.end - q));
                        }
                        if (k == 0)
                        {
                            blocks.writeAt = q;
                            r = blocks.Flush(r);
                            q = blocks.writeAt;
                            k = ((q < blocks.readAt) ? (blocks.readAt - q - 1) : (blocks.end - q));
                            if (q == blocks.end && blocks.readAt != 0)
                            {
                                q = 0;
                                k = ((q < blocks.readAt) ? (blocks.readAt - q - 1) : (blocks.end - q));
                            }
                            if (k == 0)
                            {
                                goto Block_32;
                            }
                        }
                    }
                    blocks.window[q++] = blocks.window[f++];
                    k--;
                    if (f == blocks.end)
                    {
                        f = 0;
                    }
                    this.len--;
                }
                this.mode = 0;
            }
            r = -2;
            blocks.bitb = b;
            blocks.bitk = j;
            z.AvailableBytesIn = i;
            z.TotalBytesIn += (long)(p - z.NextIn);
            z.NextIn = p;
            blocks.writeAt = q;
            return blocks.Flush(r);
            IL_1E0:
            blocks.bitb = b;
            blocks.bitk = j;
            z.AvailableBytesIn = i;
            z.TotalBytesIn += (long)(p - z.NextIn);
            z.NextIn = p;
            blocks.writeAt = q;
            return blocks.Flush(r);
            IL_34E:
            this.mode = 9;
            z.Message = "invalid literal/length code";
            r = -3;
            blocks.bitb = b;
            blocks.bitk = j;
            z.AvailableBytesIn = i;
            z.TotalBytesIn += (long)(p - z.NextIn);
            z.NextIn = p;
            blocks.writeAt = q;
            return blocks.Flush(r);
            IL_3D3:
            blocks.bitb = b;
            blocks.bitk = j;
            z.AvailableBytesIn = i;
            z.TotalBytesIn += (long)(p - z.NextIn);
            z.NextIn = p;
            blocks.writeAt = q;
            return blocks.Flush(r);
            IL_4CA:
            blocks.bitb = b;
            blocks.bitk = j;
            z.AvailableBytesIn = i;
            z.TotalBytesIn += (long)(p - z.NextIn);
            z.NextIn = p;
            blocks.writeAt = q;
            return blocks.Flush(r);
            IL_5F4:
            this.mode = 9;
            z.Message = "invalid distance code";
            r = -3;
            blocks.bitb = b;
            blocks.bitk = j;
            z.AvailableBytesIn = i;
            z.TotalBytesIn += (long)(p - z.NextIn);
            z.NextIn = p;
            blocks.writeAt = q;
            return blocks.Flush(r);
            IL_679:
            blocks.bitb = b;
            blocks.bitk = j;
            z.AvailableBytesIn = i;
            z.TotalBytesIn += (long)(p - z.NextIn);
            z.NextIn = p;
            blocks.writeAt = q;
            return blocks.Flush(r);
            Block_32:
            blocks.bitb = b;
            blocks.bitk = j;
            z.AvailableBytesIn = i;
            z.TotalBytesIn += (long)(p - z.NextIn);
            z.NextIn = p;
            blocks.writeAt = q;
            return blocks.Flush(r);
            Block_44:
            blocks.bitb = b;
            blocks.bitk = j;
            z.AvailableBytesIn = i;
            z.TotalBytesIn += (long)(p - z.NextIn);
            z.NextIn = p;
            blocks.writeAt = q;
            return blocks.Flush(r);
            IL_A78:
            if (j > 7)
            {
                j -= 8;
                i++;
                p--;
            }
            blocks.writeAt = q;
            r = blocks.Flush(r);
            q = blocks.writeAt;
            int num = (q < blocks.readAt) ? (blocks.readAt - q - 1) : (blocks.end - q);
            if (blocks.readAt != blocks.writeAt)
            {
                blocks.bitb = b;
                blocks.bitk = j;
                z.AvailableBytesIn = i;
                z.TotalBytesIn += (long)(p - z.NextIn);
                z.NextIn = p;
                blocks.writeAt = q;
                return blocks.Flush(r);
            }
            this.mode = 8;
            IL_B43:
            r = 1;
            blocks.bitb = b;
            blocks.bitk = j;
            z.AvailableBytesIn = i;
            z.TotalBytesIn += (long)(p - z.NextIn);
            z.NextIn = p;
            blocks.writeAt = q;
            return blocks.Flush(r);
            IL_B96:
            r = -3;
            blocks.bitb = b;
            blocks.bitk = j;
            z.AvailableBytesIn = i;
            z.TotalBytesIn += (long)(p - z.NextIn);
            z.NextIn = p;
            blocks.writeAt = q;
            return blocks.Flush(r);
        }

        // Token: 0x0600043E RID: 1086 RVA: 0x00021668 File Offset: 0x0001F868
        internal int InflateFast(int bl, int bd, int[] tl, int tl_index, int[] td, int td_index, InflateBlocks s, ZlibCodec z)
        {
            int p = z.NextIn;
            int i = z.AvailableBytesIn;
            int b = s.bitb;
            int j = s.bitk;
            int q = s.writeAt;
            int k = (q < s.readAt) ? (s.readAt - q - 1) : (s.end - q);
            int ml = InternalInflateConstants.InflateMask[bl];
            int md = InternalInflateConstants.InflateMask[bd];
            int e;
            int c;
            for (; ; )
            {
                while (j < 20)
                {
                    i--;
                    b |= (int)(z.InputBuffer[p++] & byte.MaxValue) << j;
                    j += 8;
                }
                int t = b & ml;
                int tp_index_t_3 = (tl_index + t) * 3;
                if ((e = tl[tp_index_t_3]) == 0)
                {
                    b >>= tl[tp_index_t_3 + 1];
                    j -= tl[tp_index_t_3 + 1];
                    s.window[q++] = (byte)tl[tp_index_t_3 + 2];
                    k--;
                }
                else
                {
                    for (; ; )
                    {
                        b >>= tl[tp_index_t_3 + 1];
                        j -= tl[tp_index_t_3 + 1];
                        if ((e & 16) != 0)
                        {
                            goto Block_4;
                        }
                        if ((e & 64) != 0)
                        {
                            goto IL_589;
                        }
                        t += tl[tp_index_t_3 + 2];
                        t += (b & InternalInflateConstants.InflateMask[e]);
                        tp_index_t_3 = (tl_index + t) * 3;
                        if ((e = tl[tp_index_t_3]) == 0)
                        {
                            goto Block_20;
                        }
                    }
                    IL_6B7:
                    goto IL_6B8;
                    Block_20:
                    b >>= tl[tp_index_t_3 + 1];
                    j -= tl[tp_index_t_3 + 1];
                    s.window[q++] = (byte)tl[tp_index_t_3 + 2];
                    k--;
                    goto IL_6B7;
                    Block_4:
                    e &= 15;
                    c = tl[tp_index_t_3 + 2] + (b & InternalInflateConstants.InflateMask[e]);
                    b >>= e;
                    for (j -= e; j < 15; j += 8)
                    {
                        i--;
                        b |= (int)(z.InputBuffer[p++] & byte.MaxValue) << j;
                    }
                    t = (b & md);
                    tp_index_t_3 = (td_index + t) * 3;
                    e = td[tp_index_t_3];
                    for (; ; )
                    {
                        b >>= td[tp_index_t_3 + 1];
                        j -= td[tp_index_t_3 + 1];
                        if ((e & 16) != 0)
                        {
                            break;
                        }
                        if ((e & 64) != 0)
                        {
                            goto IL_469;
                        }
                        t += td[tp_index_t_3 + 2];
                        t += (b & InternalInflateConstants.InflateMask[e]);
                        tp_index_t_3 = (td_index + t) * 3;
                        e = td[tp_index_t_3];
                    }
                    e &= 15;
                    while (j < e)
                    {
                        i--;
                        b |= (int)(z.InputBuffer[p++] & byte.MaxValue) << j;
                        j += 8;
                    }
                    int d = td[tp_index_t_3 + 2] + (b & InternalInflateConstants.InflateMask[e]);
                    b >>= e;
                    j -= e;
                    k -= c;
                    int r;
                    if (q >= d)
                    {
                        r = q - d;
                        if (q - r > 0 && 2 > q - r)
                        {
                            s.window[q++] = s.window[r++];
                            s.window[q++] = s.window[r++];
                            c -= 2;
                        }
                        else
                        {
                            Array.Copy(s.window, r, s.window, q, 2);
                            q += 2;
                            r += 2;
                            c -= 2;
                        }
                    }
                    else
                    {
                        r = q - d;
                        do
                        {
                            r += s.end;
                        }
                        while (r < 0);
                        e = s.end - r;
                        if (c > e)
                        {
                            c -= e;
                            if (q - r > 0 && e > q - r)
                            {
                                do
                                {
                                    s.window[q++] = s.window[r++];
                                }
                                while (--e != 0);
                            }
                            else
                            {
                                Array.Copy(s.window, r, s.window, q, e);
                                q += e;
                                r += e;
                            }
                            r = 0;
                        }
                    }
                    if (q - r > 0 && c > q - r)
                    {
                        do
                        {
                            s.window[q++] = s.window[r++];
                        }
                        while (--c != 0);
                    }
                    else
                    {
                        Array.Copy(s.window, r, s.window, q, c);
                        q += c;
                        r += c;
                    }
                }
                IL_6B8:
                if (k < 258 || i < 10)
                {
                    goto Block_25;
                }
            }
            IL_469:
            z.Message = "invalid distance code";
            c = z.AvailableBytesIn - i;
            c = ((j >> 3 < c) ? (j >> 3) : c);
            i += c;
            p -= c;
            j -= c << 3;
            s.bitb = b;
            s.bitk = j;
            z.AvailableBytesIn = i;
            z.TotalBytesIn += (long)(p - z.NextIn);
            z.NextIn = p;
            s.writeAt = q;
            return -3;
            IL_589:
            if ((e & 32) != 0)
            {
                c = z.AvailableBytesIn - i;
                c = ((j >> 3 < c) ? (j >> 3) : c);
                i += c;
                p -= c;
                j -= c << 3;
                s.bitb = b;
                s.bitk = j;
                z.AvailableBytesIn = i;
                z.TotalBytesIn += (long)(p - z.NextIn);
                z.NextIn = p;
                s.writeAt = q;
                return 1;
            }
            z.Message = "invalid literal/length code";
            c = z.AvailableBytesIn - i;
            c = ((j >> 3 < c) ? (j >> 3) : c);
            i += c;
            p -= c;
            j -= c << 3;
            s.bitb = b;
            s.bitk = j;
            z.AvailableBytesIn = i;
            z.TotalBytesIn += (long)(p - z.NextIn);
            z.NextIn = p;
            s.writeAt = q;
            return -3;
            Block_25:
            c = z.AvailableBytesIn - i;
            c = ((j >> 3 < c) ? (j >> 3) : c);
            i += c;
            p -= c;
            j -= c << 3;
            s.bitb = b;
            s.bitk = j;
            z.AvailableBytesIn = i;
            z.TotalBytesIn += (long)(p - z.NextIn);
            z.NextIn = p;
            s.writeAt = q;
            return 0;
        }

        // Token: 0x040002DC RID: 732
        private const int START = 0;

        // Token: 0x040002DD RID: 733
        private const int LEN = 1;

        // Token: 0x040002DE RID: 734
        private const int LENEXT = 2;

        // Token: 0x040002DF RID: 735
        private const int DIST = 3;

        // Token: 0x040002E0 RID: 736
        private const int DISTEXT = 4;

        // Token: 0x040002E1 RID: 737
        private const int COPY = 5;

        // Token: 0x040002E2 RID: 738
        private const int LIT = 6;

        // Token: 0x040002E3 RID: 739
        private const int WASH = 7;

        // Token: 0x040002E4 RID: 740
        private const int END = 8;

        // Token: 0x040002E5 RID: 741
        private const int BADCODE = 9;

        // Token: 0x040002E6 RID: 742
        internal int mode;

        // Token: 0x040002E7 RID: 743
        internal int len;

        // Token: 0x040002E8 RID: 744
        internal int[] tree;

        // Token: 0x040002E9 RID: 745
        internal int tree_index = 0;

        // Token: 0x040002EA RID: 746
        internal int need;

        // Token: 0x040002EB RID: 747
        internal int lit;

        // Token: 0x040002EC RID: 748
        internal int bitsToGet;

        // Token: 0x040002ED RID: 749
        internal int dist;

        // Token: 0x040002EE RID: 750
        internal byte lbits;

        // Token: 0x040002EF RID: 751
        internal byte dbits;

        // Token: 0x040002F0 RID: 752
        internal int[] ltree;

        // Token: 0x040002F1 RID: 753
        internal int ltree_index;

        // Token: 0x040002F2 RID: 754
        internal int[] dtree;

        // Token: 0x040002F3 RID: 755
        internal int dtree_index;
    }
}


namespace Ionic.Zlib
{
    // Token: 0x0200005A RID: 90
    internal sealed class InflateManager
    {
        // Token: 0x17000111 RID: 273
        // (get) Token: 0x0600043F RID: 1087 RVA: 0x00021DD0 File Offset: 0x0001FFD0
        // (set) Token: 0x06000440 RID: 1088 RVA: 0x00021DE8 File Offset: 0x0001FFE8
        internal bool HandleRfc1950HeaderBytes
        {
            get
            {
                return this._handleRfc1950HeaderBytes;
            }
            set
            {
                this._handleRfc1950HeaderBytes = value;
            }
        }

        // Token: 0x06000441 RID: 1089 RVA: 0x00021DF2 File Offset: 0x0001FFF2
        public InflateManager()
        {
        }

        // Token: 0x06000442 RID: 1090 RVA: 0x00021E04 File Offset: 0x00020004
        public InflateManager(bool expectRfc1950HeaderBytes)
        {
            this._handleRfc1950HeaderBytes = expectRfc1950HeaderBytes;
        }

        // Token: 0x06000443 RID: 1091 RVA: 0x00021E20 File Offset: 0x00020020
        internal int Reset()
        {
            this._codec.TotalBytesIn = (this._codec.TotalBytesOut = 0L);
            this._codec.Message = null;
            this.mode = (this.HandleRfc1950HeaderBytes ? InflateManager.InflateManagerMode.METHOD : InflateManager.InflateManagerMode.BLOCKS);
            this.blocks.Reset();
            return 0;
        }

        // Token: 0x06000444 RID: 1092 RVA: 0x00021E78 File Offset: 0x00020078
        internal int End()
        {
            if (this.blocks != null)
            {
                this.blocks.Free();
            }
            this.blocks = null;
            return 0;
        }

        // Token: 0x06000445 RID: 1093 RVA: 0x00021EAC File Offset: 0x000200AC
        internal int Initialize(ZlibCodec codec, int w)
        {
            this._codec = codec;
            this._codec.Message = null;
            this.blocks = null;
            if (w < 8 || w > 15)
            {
                this.End();
                throw new ZlibException("Bad window size.");
            }
            this.wbits = w;
            this.blocks = new InflateBlocks(codec, this.HandleRfc1950HeaderBytes ? this : null, 1 << w);
            this.Reset();
            return 0;
        }

        // Token: 0x06000446 RID: 1094 RVA: 0x00021F2C File Offset: 0x0002012C
        internal int Inflate(FlushType flush)
        {
            unchecked
            {
                if (this._codec.InputBuffer == null)
                {
                    throw new ZlibException("InputBuffer is null. ");
                }
                int f = 0;
                int r = -5;
                for (; ; )
                {
                    switch (this.mode)
                    {
                        case InflateManager.InflateManagerMode.METHOD:
                            if (this._codec.AvailableBytesIn == 0)
                            {
                                goto Block_3;
                            }
                            r = f;
                            this._codec.AvailableBytesIn--;
                            this._codec.TotalBytesIn += 1L;
                            if (((this.method = (int)this._codec.InputBuffer[this._codec.NextIn++]) & 15) != 8)
                            {
                                this.mode = InflateManager.InflateManagerMode.BAD;
                                this._codec.Message = string.Format("unknown compression method (0x{0:X2})", this.method);
                                this.marker = 5;
                                continue;
                            }
                            if ((this.method >> 4) + 8 > this.wbits)
                            {
                                this.mode = InflateManager.InflateManagerMode.BAD;
                                this._codec.Message = string.Format("invalid window size ({0})", (this.method >> 4) + 8);
                                this.marker = 5;
                                continue;
                            }
                            this.mode = InflateManager.InflateManagerMode.FLAG;
                            continue;
                        case InflateManager.InflateManagerMode.FLAG:
                            {
                                if (this._codec.AvailableBytesIn == 0)
                                {
                                    goto Block_6;
                                }
                                r = f;
                                this._codec.AvailableBytesIn--;
                                this._codec.TotalBytesIn += 1L;
                                int b = (int)(this._codec.InputBuffer[this._codec.NextIn++] & byte.MaxValue);
                                if (((this.method << 8) + b) % 31 != 0)
                                {
                                    this.mode = InflateManager.InflateManagerMode.BAD;
                                    this._codec.Message = "incorrect header check";
                                    this.marker = 5;
                                    continue;
                                }
                                this.mode = (((b & 32) == 0) ? InflateManager.InflateManagerMode.BLOCKS : InflateManager.InflateManagerMode.DICT4);
                                continue;
                            }
                        case InflateManager.InflateManagerMode.DICT4:
                            if (this._codec.AvailableBytesIn == 0)
                            {
                                goto Block_9;
                            }
                            r = f;
                            this._codec.AvailableBytesIn--;
                            this._codec.TotalBytesIn += 1L;
                            this.expectedCheck = (uint)((long)((long)this._codec.InputBuffer[this._codec.NextIn++] << 24) & (long)((ulong)-16777216));
                            this.mode = InflateManager.InflateManagerMode.DICT3;
                            continue;
                        case InflateManager.InflateManagerMode.DICT3:
                            if (this._codec.AvailableBytesIn == 0)
                            {
                                goto Block_10;
                            }
                            r = f;
                            this._codec.AvailableBytesIn--;
                            this._codec.TotalBytesIn += 1L;
                            this.expectedCheck += (uint)((int)this._codec.InputBuffer[this._codec.NextIn++] << 16 & 16711680);
                            this.mode = InflateManager.InflateManagerMode.DICT2;
                            continue;
                        case InflateManager.InflateManagerMode.DICT2:
                            if (this._codec.AvailableBytesIn == 0)
                            {
                                goto Block_11;
                            }
                            r = f;
                            this._codec.AvailableBytesIn--;
                            this._codec.TotalBytesIn += 1L;
                            this.expectedCheck += (uint)((int)this._codec.InputBuffer[this._codec.NextIn++] << 8 & 65280);
                            this.mode = InflateManager.InflateManagerMode.DICT1;
                            continue;
                        case InflateManager.InflateManagerMode.DICT1:
                            goto IL_3F5;
                        case InflateManager.InflateManagerMode.DICT0:
                            goto IL_492;
                        case InflateManager.InflateManagerMode.BLOCKS:
                            r = this.blocks.Process(r);
                            if (r == -3)
                            {
                                this.mode = InflateManager.InflateManagerMode.BAD;
                                this.marker = 0;
                                continue;
                            }
                            if (r == 0)
                            {
                                r = f;
                            }
                            if (r != 1)
                            {
                                goto Block_15;
                            }
                            r = f;
                            this.computedCheck = this.blocks.Reset();
                            if (!this.HandleRfc1950HeaderBytes)
                            {
                                goto Block_16;
                            }
                            this.mode = InflateManager.InflateManagerMode.CHECK4;
                            continue;
                        case InflateManager.InflateManagerMode.CHECK4:
                            if (this._codec.AvailableBytesIn == 0)
                            {
                                goto Block_17;
                            }
                            r = f;
                            this._codec.AvailableBytesIn--;
                            this._codec.TotalBytesIn += 1L;
                            this.expectedCheck = (uint)((long)((long)this._codec.InputBuffer[this._codec.NextIn++] << 24) & (long)((ulong)-16777216));
                            this.mode = InflateManager.InflateManagerMode.CHECK3;
                            continue;
                        case InflateManager.InflateManagerMode.CHECK3:
                            if (this._codec.AvailableBytesIn == 0)
                            {
                                goto Block_18;
                            }
                            r = f;
                            this._codec.AvailableBytesIn--;
                            this._codec.TotalBytesIn += 1L;
                            this.expectedCheck += (uint)((int)this._codec.InputBuffer[this._codec.NextIn++] << 16 & 16711680);
                            this.mode = InflateManager.InflateManagerMode.CHECK2;
                            continue;
                        case InflateManager.InflateManagerMode.CHECK2:
                            if (this._codec.AvailableBytesIn == 0)
                            {
                                goto Block_19;
                            }
                            r = f;
                            this._codec.AvailableBytesIn--;
                            this._codec.TotalBytesIn += 1L;
                            this.expectedCheck += (uint)((int)this._codec.InputBuffer[this._codec.NextIn++] << 8 & 65280);
                            this.mode = InflateManager.InflateManagerMode.CHECK1;
                            continue;
                        case InflateManager.InflateManagerMode.CHECK1:
                            if (this._codec.AvailableBytesIn == 0)
                            {
                                goto Block_20;
                            }
                            r = f;
                            this._codec.AvailableBytesIn--;
                            this._codec.TotalBytesIn += 1L;
                            this.expectedCheck += (uint)(this._codec.InputBuffer[this._codec.NextIn++] & byte.MaxValue);
                            if (this.computedCheck != this.expectedCheck)
                            {
                                this.mode = InflateManager.InflateManagerMode.BAD;
                                this._codec.Message = "incorrect data check";
                                this.marker = 5;
                                continue;
                            }
                            goto IL_79D;
                        case InflateManager.InflateManagerMode.DONE:
                            goto IL_7A9;
                        case InflateManager.InflateManagerMode.BAD:
                            goto IL_7AD;
                    }
                    break;
                }
                throw new ZlibException("Stream error.");
                Block_3:
                return r;
                Block_6:
                return r;
                Block_9:
                return r;
                Block_10:
                return r;
                Block_11:
                return r;
                IL_3F5:
                if (this._codec.AvailableBytesIn == 0)
                {
                    return r;
                }
                this._codec.AvailableBytesIn--;
                this._codec.TotalBytesIn += 1L;
                this.expectedCheck += (uint)(this._codec.InputBuffer[this._codec.NextIn++] & byte.MaxValue);
                this._codec._Adler32 = this.expectedCheck;
                this.mode = InflateManager.InflateManagerMode.DICT0;
                return 2;
                IL_492:
                this.mode = InflateManager.InflateManagerMode.BAD;
                this._codec.Message = "need dictionary";
                this.marker = 0;
                return -2;
                Block_15:
                return r;
                Block_16:
                this.mode = InflateManager.InflateManagerMode.DONE;
                return 1;
                Block_17:
                return r;
                Block_18:
                return r;
                Block_19:
                return r;
                Block_20:
                return r;
                IL_79D:
                this.mode = InflateManager.InflateManagerMode.DONE;
                return 1;
                IL_7A9:
                return 1;
                IL_7AD:
                throw new ZlibException(string.Format("Bad state ({0})", this._codec.Message));
            }
        }

        // Token: 0x06000447 RID: 1095 RVA: 0x00022718 File Offset: 0x00020918
        internal int SetDictionary(byte[] dictionary)
        {
            int index = 0;
            int length = dictionary.Length;
            if (this.mode != InflateManager.InflateManagerMode.DICT0)
            {
                throw new ZlibException("Stream error.");
            }
            int result;
            if (Adler.Adler32(1u, dictionary, 0, dictionary.Length) != this._codec._Adler32)
            {
                result = -3;
            }
            else
            {
                this._codec._Adler32 = Adler.Adler32(0u, null, 0, 0);
                if (length >= 1 << this.wbits)
                {
                    length = (1 << this.wbits) - 1;
                    index = dictionary.Length - length;
                }
                this.blocks.SetDictionary(dictionary, index, length);
                this.mode = InflateManager.InflateManagerMode.BLOCKS;
                result = 0;
            }
            return result;
        }

        // Token: 0x06000448 RID: 1096 RVA: 0x000227C0 File Offset: 0x000209C0
        internal int Sync()
        {
            if (this.mode != InflateManager.InflateManagerMode.BAD)
            {
                this.mode = InflateManager.InflateManagerMode.BAD;
                this.marker = 0;
            }
            int i;
            int result;
            if ((i = this._codec.AvailableBytesIn) == 0)
            {
                result = -5;
            }
            else
            {
                int p = this._codec.NextIn;
                int j = this.marker;
                while (i != 0 && j < 4)
                {
                    if (this._codec.InputBuffer[p] == InflateManager.mark[j])
                    {
                        j++;
                    }
                    else if (this._codec.InputBuffer[p] != 0)
                    {
                        j = 0;
                    }
                    else
                    {
                        j = 4 - j;
                    }
                    p++;
                    i--;
                }
                this._codec.TotalBytesIn += (long)(p - this._codec.NextIn);
                this._codec.NextIn = p;
                this._codec.AvailableBytesIn = i;
                this.marker = j;
                if (j != 4)
                {
                    result = -3;
                }
                else
                {
                    long r = this._codec.TotalBytesIn;
                    long w = this._codec.TotalBytesOut;
                    this.Reset();
                    this._codec.TotalBytesIn = r;
                    this._codec.TotalBytesOut = w;
                    this.mode = InflateManager.InflateManagerMode.BLOCKS;
                    result = 0;
                }
            }
            return result;
        }

        // Token: 0x06000449 RID: 1097 RVA: 0x00022920 File Offset: 0x00020B20
        internal int SyncPoint(ZlibCodec z)
        {
            return this.blocks.SyncPoint();
        }

        // Token: 0x040002F4 RID: 756
        private const int PRESET_DICT = 32;

        // Token: 0x040002F5 RID: 757
        private const int Z_DEFLATED = 8;

        // Token: 0x040002F6 RID: 758
        private InflateManager.InflateManagerMode mode;

        // Token: 0x040002F7 RID: 759
        internal ZlibCodec _codec;

        // Token: 0x040002F8 RID: 760
        internal int method;

        // Token: 0x040002F9 RID: 761
        internal uint computedCheck;

        // Token: 0x040002FA RID: 762
        internal uint expectedCheck;

        // Token: 0x040002FB RID: 763
        internal int marker;

        // Token: 0x040002FC RID: 764
        private bool _handleRfc1950HeaderBytes = true;

        // Token: 0x040002FD RID: 765
        internal int wbits;

        // Token: 0x040002FE RID: 766
        internal InflateBlocks blocks;

        // Token: 0x040002FF RID: 767
        private static readonly byte[] mark = new byte[]
        {
            0,
            0,
            byte.MaxValue,
            byte.MaxValue
        };

        // Token: 0x0200005B RID: 91
        private enum InflateManagerMode
        {
            // Token: 0x04000301 RID: 769
            METHOD,
            // Token: 0x04000302 RID: 770
            FLAG,
            // Token: 0x04000303 RID: 771
            DICT4,
            // Token: 0x04000304 RID: 772
            DICT3,
            // Token: 0x04000305 RID: 773
            DICT2,
            // Token: 0x04000306 RID: 774
            DICT1,
            // Token: 0x04000307 RID: 775
            DICT0,
            // Token: 0x04000308 RID: 776
            BLOCKS,
            // Token: 0x04000309 RID: 777
            CHECK4,
            // Token: 0x0400030A RID: 778
            CHECK3,
            // Token: 0x0400030B RID: 779
            CHECK2,
            // Token: 0x0400030C RID: 780
            CHECK1,
            // Token: 0x0400030D RID: 781
            DONE,
            // Token: 0x0400030E RID: 782
            BAD
        }
    }
}


namespace Ionic.Zlib
{
    // Token: 0x0200005C RID: 92
    internal sealed class InfTree
    {
        // Token: 0x0600044B RID: 1099 RVA: 0x0002296C File Offset: 0x00020B6C
        private int huft_build(int[] b, int bindex, int n, int s, int[] d, int[] e, int[] t, int[] m, int[] hp, int[] hn, int[] v)
        {
            int p = 0;
            int i = n;
            do
            {
                this.c[b[bindex + p]]++;
                p++;
                i--;
            }
            while (i != 0);
            int result;
            if (this.c[0] == n)
            {
                t[0] = -1;
                m[0] = 0;
                result = 0;
            }
            else
            {
                int j = m[0];
                int k;
                for (k = 1; k <= 15; k++)
                {
                    if (this.c[k] != 0)
                    {
                        break;
                    }
                }
                int l = k;
                if (j < k)
                {
                    j = k;
                }
                for (i = 15; i != 0; i--)
                {
                    if (this.c[i] != 0)
                    {
                        break;
                    }
                }
                int g = i;
                if (j > i)
                {
                    j = i;
                }
                m[0] = j;
                int y = 1 << k;
                while (k < i)
                {
                    if ((y -= this.c[k]) < 0)
                    {
                        return -3;
                    }
                    k++;
                    y <<= 1;
                }
                if ((y -= this.c[i]) < 0)
                {
                    result = -3;
                }
                else
                {
                    this.c[i] += y;
                    k = (this.x[1] = 0);
                    p = 1;
                    int xp = 2;
                    while (--i != 0)
                    {
                        k = (this.x[xp] = k + this.c[p]);
                        xp++;
                        p++;
                    }
                    i = 0;
                    p = 0;
                    do
                    {
                        if ((k = b[bindex + p]) != 0)
                        {
                            v[this.x[k]++] = i;
                        }
                        p++;
                    }
                    while (++i < n);
                    n = this.x[g];
                    i = (this.x[0] = 0);
                    p = 0;
                    int h = -1;
                    int w = -j;
                    this.u[0] = 0;
                    int q = 0;
                    int z = 0;
                    while (l <= g)
                    {
                        int a = this.c[l];
                        while (a-- != 0)
                        {
                            int f;
                            while (l > w + j)
                            {
                                h++;
                                w += j;
                                z = g - w;
                                z = ((z > j) ? j : z);
                                if ((f = 1 << ((k = l - w) & 31)) > a + 1)
                                {
                                    f -= a + 1;
                                    xp = l;
                                    if (k < z)
                                    {
                                        while (++k < z)
                                        {
                                            if ((f <<= 1) <= this.c[++xp])
                                            {
                                                break;
                                            }
                                            f -= this.c[xp];
                                        }
                                    }
                                }
                                z = 1 << k;
                                if (hn[0] + z > 1440)
                                {
                                    return -3;
                                }
                                q = (this.u[h] = hn[0]);
                                hn[0] += z;
                                if (h != 0)
                                {
                                    this.x[h] = i;
                                    this.r[0] = (int)((sbyte)k);
                                    this.r[1] = (int)((sbyte)j);
                                    k = SharedUtils.URShift(i, w - j);
                                    this.r[2] = q - this.u[h - 1] - k;
                                    Array.Copy(this.r, 0, hp, (this.u[h - 1] + k) * 3, 3);
                                }
                                else
                                {
                                    t[0] = q;
                                }
                            }
                            this.r[1] = (int)((sbyte)(l - w));
                            if (p >= n)
                            {
                                this.r[0] = 192;
                            }
                            else if (v[p] < s)
                            {
                                this.r[0] = (int)((v[p] < 256) ? 0 : 96);
                                this.r[2] = v[p++];
                            }
                            else
                            {
                                this.r[0] = (int)((sbyte)(e[v[p] - s] + 16 + 64));
                                this.r[2] = d[v[p++] - s];
                            }
                            f = 1 << l - w;
                            for (k = SharedUtils.URShift(i, w); k < z; k += f)
                            {
                                Array.Copy(this.r, 0, hp, (q + k) * 3, 3);
                            }
                            k = 1 << l - 1;
                            while ((i & k) != 0)
                            {
                                i ^= k;
                                k = SharedUtils.URShift(k, 1);
                            }
                            i ^= k;
                            int mask = (1 << w) - 1;
                            while ((i & mask) != this.x[h])
                            {
                                h--;
                                w -= j;
                                mask = (1 << w) - 1;
                            }
                        }
                        l++;
                    }
                    result = ((y != 0 && g != 1) ? -5 : 0);
                }
            }
            return result;
        }

        // Token: 0x0600044C RID: 1100 RVA: 0x00022F00 File Offset: 0x00021100
        internal int inflate_trees_bits(int[] c, int[] bb, int[] tb, int[] hp, ZlibCodec z)
        {
            this.initWorkArea(19);
            this.hn[0] = 0;
            int result = this.huft_build(c, 0, 19, 19, null, null, tb, bb, hp, this.hn, this.v);
            if (result == -3)
            {
                z.Message = "oversubscribed dynamic bit lengths tree";
            }
            else if (result == -5 || bb[0] == 0)
            {
                z.Message = "incomplete dynamic bit lengths tree";
                result = -3;
            }
            return result;
        }

        // Token: 0x0600044D RID: 1101 RVA: 0x00022F88 File Offset: 0x00021188
        internal int inflate_trees_dynamic(int nl, int nd, int[] c, int[] bl, int[] bd, int[] tl, int[] td, int[] hp, ZlibCodec z)
        {
            this.initWorkArea(288);
            this.hn[0] = 0;
            int result = this.huft_build(c, 0, nl, 257, InfTree.cplens, InfTree.cplext, tl, bl, hp, this.hn, this.v);
            int result2;
            if (result != 0 || bl[0] == 0)
            {
                if (result == -3)
                {
                    z.Message = "oversubscribed literal/length tree";
                }
                else if (result != -4)
                {
                    z.Message = "incomplete literal/length tree";
                    result = -3;
                }
                result2 = result;
            }
            else
            {
                this.initWorkArea(288);
                result = this.huft_build(c, nl, nd, 0, InfTree.cpdist, InfTree.cpdext, td, bd, hp, this.hn, this.v);
                if (result != 0 || (bd[0] == 0 && nl > 257))
                {
                    if (result == -3)
                    {
                        z.Message = "oversubscribed distance tree";
                    }
                    else if (result == -5)
                    {
                        z.Message = "incomplete distance tree";
                        result = -3;
                    }
                    else if (result != -4)
                    {
                        z.Message = "empty distance tree with lengths";
                        result = -3;
                    }
                    result2 = result;
                }
                else
                {
                    result2 = 0;
                }
            }
            return result2;
        }

        // Token: 0x0600044E RID: 1102 RVA: 0x000230D8 File Offset: 0x000212D8
        internal static int inflate_trees_fixed(int[] bl, int[] bd, int[][] tl, int[][] td, ZlibCodec z)
        {
            bl[0] = 9;
            bd[0] = 5;
            tl[0] = InfTree.fixed_tl;
            td[0] = InfTree.fixed_td;
            return 0;
        }

        // Token: 0x0600044F RID: 1103 RVA: 0x00023104 File Offset: 0x00021304
        private void initWorkArea(int vsize)
        {
            if (this.hn == null)
            {
                this.hn = new int[1];
                this.v = new int[vsize];
                this.c = new int[16];
                this.r = new int[3];
                this.u = new int[15];
                this.x = new int[16];
            }
            else
            {
                if (this.v.Length < vsize)
                {
                    this.v = new int[vsize];
                }
                Array.Clear(this.v, 0, vsize);
                Array.Clear(this.c, 0, 16);
                this.r[0] = 0;
                this.r[1] = 0;
                this.r[2] = 0;
                Array.Clear(this.u, 0, 15);
                Array.Clear(this.x, 0, 16);
            }
        }

        // Token: 0x0400030F RID: 783
        private const int MANY = 1440;

        // Token: 0x04000310 RID: 784
        private const int Z_OK = 0;

        // Token: 0x04000311 RID: 785
        private const int Z_STREAM_END = 1;

        // Token: 0x04000312 RID: 786
        private const int Z_NEED_DICT = 2;

        // Token: 0x04000313 RID: 787
        private const int Z_ERRNO = -1;

        // Token: 0x04000314 RID: 788
        private const int Z_STREAM_ERROR = -2;

        // Token: 0x04000315 RID: 789
        private const int Z_DATA_ERROR = -3;

        // Token: 0x04000316 RID: 790
        private const int Z_MEM_ERROR = -4;

        // Token: 0x04000317 RID: 791
        private const int Z_BUF_ERROR = -5;

        // Token: 0x04000318 RID: 792
        private const int Z_VERSION_ERROR = -6;

        // Token: 0x04000319 RID: 793
        internal const int fixed_bl = 9;

        // Token: 0x0400031A RID: 794
        internal const int fixed_bd = 5;

        // Token: 0x0400031B RID: 795
        internal const int BMAX = 15;

        // Token: 0x0400031C RID: 796
        internal static readonly int[] fixed_tl = new int[]
        {
            96,
            7,
            256,
            0,
            8,
            80,
            0,
            8,
            16,
            84,
            8,
            115,
            82,
            7,
            31,
            0,
            8,
            112,
            0,
            8,
            48,
            0,
            9,
            192,
            80,
            7,
            10,
            0,
            8,
            96,
            0,
            8,
            32,
            0,
            9,
            160,
            0,
            8,
            0,
            0,
            8,
            128,
            0,
            8,
            64,
            0,
            9,
            224,
            80,
            7,
            6,
            0,
            8,
            88,
            0,
            8,
            24,
            0,
            9,
            144,
            83,
            7,
            59,
            0,
            8,
            120,
            0,
            8,
            56,
            0,
            9,
            208,
            81,
            7,
            17,
            0,
            8,
            104,
            0,
            8,
            40,
            0,
            9,
            176,
            0,
            8,
            8,
            0,
            8,
            136,
            0,
            8,
            72,
            0,
            9,
            240,
            80,
            7,
            4,
            0,
            8,
            84,
            0,
            8,
            20,
            85,
            8,
            227,
            83,
            7,
            43,
            0,
            8,
            116,
            0,
            8,
            52,
            0,
            9,
            200,
            81,
            7,
            13,
            0,
            8,
            100,
            0,
            8,
            36,
            0,
            9,
            168,
            0,
            8,
            4,
            0,
            8,
            132,
            0,
            8,
            68,
            0,
            9,
            232,
            80,
            7,
            8,
            0,
            8,
            92,
            0,
            8,
            28,
            0,
            9,
            152,
            84,
            7,
            83,
            0,
            8,
            124,
            0,
            8,
            60,
            0,
            9,
            216,
            82,
            7,
            23,
            0,
            8,
            108,
            0,
            8,
            44,
            0,
            9,
            184,
            0,
            8,
            12,
            0,
            8,
            140,
            0,
            8,
            76,
            0,
            9,
            248,
            80,
            7,
            3,
            0,
            8,
            82,
            0,
            8,
            18,
            85,
            8,
            163,
            83,
            7,
            35,
            0,
            8,
            114,
            0,
            8,
            50,
            0,
            9,
            196,
            81,
            7,
            11,
            0,
            8,
            98,
            0,
            8,
            34,
            0,
            9,
            164,
            0,
            8,
            2,
            0,
            8,
            130,
            0,
            8,
            66,
            0,
            9,
            228,
            80,
            7,
            7,
            0,
            8,
            90,
            0,
            8,
            26,
            0,
            9,
            148,
            84,
            7,
            67,
            0,
            8,
            122,
            0,
            8,
            58,
            0,
            9,
            212,
            82,
            7,
            19,
            0,
            8,
            106,
            0,
            8,
            42,
            0,
            9,
            180,
            0,
            8,
            10,
            0,
            8,
            138,
            0,
            8,
            74,
            0,
            9,
            244,
            80,
            7,
            5,
            0,
            8,
            86,
            0,
            8,
            22,
            192,
            8,
            0,
            83,
            7,
            51,
            0,
            8,
            118,
            0,
            8,
            54,
            0,
            9,
            204,
            81,
            7,
            15,
            0,
            8,
            102,
            0,
            8,
            38,
            0,
            9,
            172,
            0,
            8,
            6,
            0,
            8,
            134,
            0,
            8,
            70,
            0,
            9,
            236,
            80,
            7,
            9,
            0,
            8,
            94,
            0,
            8,
            30,
            0,
            9,
            156,
            84,
            7,
            99,
            0,
            8,
            126,
            0,
            8,
            62,
            0,
            9,
            220,
            82,
            7,
            27,
            0,
            8,
            110,
            0,
            8,
            46,
            0,
            9,
            188,
            0,
            8,
            14,
            0,
            8,
            142,
            0,
            8,
            78,
            0,
            9,
            252,
            96,
            7,
            256,
            0,
            8,
            81,
            0,
            8,
            17,
            85,
            8,
            131,
            82,
            7,
            31,
            0,
            8,
            113,
            0,
            8,
            49,
            0,
            9,
            194,
            80,
            7,
            10,
            0,
            8,
            97,
            0,
            8,
            33,
            0,
            9,
            162,
            0,
            8,
            1,
            0,
            8,
            129,
            0,
            8,
            65,
            0,
            9,
            226,
            80,
            7,
            6,
            0,
            8,
            89,
            0,
            8,
            25,
            0,
            9,
            146,
            83,
            7,
            59,
            0,
            8,
            121,
            0,
            8,
            57,
            0,
            9,
            210,
            81,
            7,
            17,
            0,
            8,
            105,
            0,
            8,
            41,
            0,
            9,
            178,
            0,
            8,
            9,
            0,
            8,
            137,
            0,
            8,
            73,
            0,
            9,
            242,
            80,
            7,
            4,
            0,
            8,
            85,
            0,
            8,
            21,
            80,
            8,
            258,
            83,
            7,
            43,
            0,
            8,
            117,
            0,
            8,
            53,
            0,
            9,
            202,
            81,
            7,
            13,
            0,
            8,
            101,
            0,
            8,
            37,
            0,
            9,
            170,
            0,
            8,
            5,
            0,
            8,
            133,
            0,
            8,
            69,
            0,
            9,
            234,
            80,
            7,
            8,
            0,
            8,
            93,
            0,
            8,
            29,
            0,
            9,
            154,
            84,
            7,
            83,
            0,
            8,
            125,
            0,
            8,
            61,
            0,
            9,
            218,
            82,
            7,
            23,
            0,
            8,
            109,
            0,
            8,
            45,
            0,
            9,
            186,
            0,
            8,
            13,
            0,
            8,
            141,
            0,
            8,
            77,
            0,
            9,
            250,
            80,
            7,
            3,
            0,
            8,
            83,
            0,
            8,
            19,
            85,
            8,
            195,
            83,
            7,
            35,
            0,
            8,
            115,
            0,
            8,
            51,
            0,
            9,
            198,
            81,
            7,
            11,
            0,
            8,
            99,
            0,
            8,
            35,
            0,
            9,
            166,
            0,
            8,
            3,
            0,
            8,
            131,
            0,
            8,
            67,
            0,
            9,
            230,
            80,
            7,
            7,
            0,
            8,
            91,
            0,
            8,
            27,
            0,
            9,
            150,
            84,
            7,
            67,
            0,
            8,
            123,
            0,
            8,
            59,
            0,
            9,
            214,
            82,
            7,
            19,
            0,
            8,
            107,
            0,
            8,
            43,
            0,
            9,
            182,
            0,
            8,
            11,
            0,
            8,
            139,
            0,
            8,
            75,
            0,
            9,
            246,
            80,
            7,
            5,
            0,
            8,
            87,
            0,
            8,
            23,
            192,
            8,
            0,
            83,
            7,
            51,
            0,
            8,
            119,
            0,
            8,
            55,
            0,
            9,
            206,
            81,
            7,
            15,
            0,
            8,
            103,
            0,
            8,
            39,
            0,
            9,
            174,
            0,
            8,
            7,
            0,
            8,
            135,
            0,
            8,
            71,
            0,
            9,
            238,
            80,
            7,
            9,
            0,
            8,
            95,
            0,
            8,
            31,
            0,
            9,
            158,
            84,
            7,
            99,
            0,
            8,
            127,
            0,
            8,
            63,
            0,
            9,
            222,
            82,
            7,
            27,
            0,
            8,
            111,
            0,
            8,
            47,
            0,
            9,
            190,
            0,
            8,
            15,
            0,
            8,
            143,
            0,
            8,
            79,
            0,
            9,
            254,
            96,
            7,
            256,
            0,
            8,
            80,
            0,
            8,
            16,
            84,
            8,
            115,
            82,
            7,
            31,
            0,
            8,
            112,
            0,
            8,
            48,
            0,
            9,
            193,
            80,
            7,
            10,
            0,
            8,
            96,
            0,
            8,
            32,
            0,
            9,
            161,
            0,
            8,
            0,
            0,
            8,
            128,
            0,
            8,
            64,
            0,
            9,
            225,
            80,
            7,
            6,
            0,
            8,
            88,
            0,
            8,
            24,
            0,
            9,
            145,
            83,
            7,
            59,
            0,
            8,
            120,
            0,
            8,
            56,
            0,
            9,
            209,
            81,
            7,
            17,
            0,
            8,
            104,
            0,
            8,
            40,
            0,
            9,
            177,
            0,
            8,
            8,
            0,
            8,
            136,
            0,
            8,
            72,
            0,
            9,
            241,
            80,
            7,
            4,
            0,
            8,
            84,
            0,
            8,
            20,
            85,
            8,
            227,
            83,
            7,
            43,
            0,
            8,
            116,
            0,
            8,
            52,
            0,
            9,
            201,
            81,
            7,
            13,
            0,
            8,
            100,
            0,
            8,
            36,
            0,
            9,
            169,
            0,
            8,
            4,
            0,
            8,
            132,
            0,
            8,
            68,
            0,
            9,
            233,
            80,
            7,
            8,
            0,
            8,
            92,
            0,
            8,
            28,
            0,
            9,
            153,
            84,
            7,
            83,
            0,
            8,
            124,
            0,
            8,
            60,
            0,
            9,
            217,
            82,
            7,
            23,
            0,
            8,
            108,
            0,
            8,
            44,
            0,
            9,
            185,
            0,
            8,
            12,
            0,
            8,
            140,
            0,
            8,
            76,
            0,
            9,
            249,
            80,
            7,
            3,
            0,
            8,
            82,
            0,
            8,
            18,
            85,
            8,
            163,
            83,
            7,
            35,
            0,
            8,
            114,
            0,
            8,
            50,
            0,
            9,
            197,
            81,
            7,
            11,
            0,
            8,
            98,
            0,
            8,
            34,
            0,
            9,
            165,
            0,
            8,
            2,
            0,
            8,
            130,
            0,
            8,
            66,
            0,
            9,
            229,
            80,
            7,
            7,
            0,
            8,
            90,
            0,
            8,
            26,
            0,
            9,
            149,
            84,
            7,
            67,
            0,
            8,
            122,
            0,
            8,
            58,
            0,
            9,
            213,
            82,
            7,
            19,
            0,
            8,
            106,
            0,
            8,
            42,
            0,
            9,
            181,
            0,
            8,
            10,
            0,
            8,
            138,
            0,
            8,
            74,
            0,
            9,
            245,
            80,
            7,
            5,
            0,
            8,
            86,
            0,
            8,
            22,
            192,
            8,
            0,
            83,
            7,
            51,
            0,
            8,
            118,
            0,
            8,
            54,
            0,
            9,
            205,
            81,
            7,
            15,
            0,
            8,
            102,
            0,
            8,
            38,
            0,
            9,
            173,
            0,
            8,
            6,
            0,
            8,
            134,
            0,
            8,
            70,
            0,
            9,
            237,
            80,
            7,
            9,
            0,
            8,
            94,
            0,
            8,
            30,
            0,
            9,
            157,
            84,
            7,
            99,
            0,
            8,
            126,
            0,
            8,
            62,
            0,
            9,
            221,
            82,
            7,
            27,
            0,
            8,
            110,
            0,
            8,
            46,
            0,
            9,
            189,
            0,
            8,
            14,
            0,
            8,
            142,
            0,
            8,
            78,
            0,
            9,
            253,
            96,
            7,
            256,
            0,
            8,
            81,
            0,
            8,
            17,
            85,
            8,
            131,
            82,
            7,
            31,
            0,
            8,
            113,
            0,
            8,
            49,
            0,
            9,
            195,
            80,
            7,
            10,
            0,
            8,
            97,
            0,
            8,
            33,
            0,
            9,
            163,
            0,
            8,
            1,
            0,
            8,
            129,
            0,
            8,
            65,
            0,
            9,
            227,
            80,
            7,
            6,
            0,
            8,
            89,
            0,
            8,
            25,
            0,
            9,
            147,
            83,
            7,
            59,
            0,
            8,
            121,
            0,
            8,
            57,
            0,
            9,
            211,
            81,
            7,
            17,
            0,
            8,
            105,
            0,
            8,
            41,
            0,
            9,
            179,
            0,
            8,
            9,
            0,
            8,
            137,
            0,
            8,
            73,
            0,
            9,
            243,
            80,
            7,
            4,
            0,
            8,
            85,
            0,
            8,
            21,
            80,
            8,
            258,
            83,
            7,
            43,
            0,
            8,
            117,
            0,
            8,
            53,
            0,
            9,
            203,
            81,
            7,
            13,
            0,
            8,
            101,
            0,
            8,
            37,
            0,
            9,
            171,
            0,
            8,
            5,
            0,
            8,
            133,
            0,
            8,
            69,
            0,
            9,
            235,
            80,
            7,
            8,
            0,
            8,
            93,
            0,
            8,
            29,
            0,
            9,
            155,
            84,
            7,
            83,
            0,
            8,
            125,
            0,
            8,
            61,
            0,
            9,
            219,
            82,
            7,
            23,
            0,
            8,
            109,
            0,
            8,
            45,
            0,
            9,
            187,
            0,
            8,
            13,
            0,
            8,
            141,
            0,
            8,
            77,
            0,
            9,
            251,
            80,
            7,
            3,
            0,
            8,
            83,
            0,
            8,
            19,
            85,
            8,
            195,
            83,
            7,
            35,
            0,
            8,
            115,
            0,
            8,
            51,
            0,
            9,
            199,
            81,
            7,
            11,
            0,
            8,
            99,
            0,
            8,
            35,
            0,
            9,
            167,
            0,
            8,
            3,
            0,
            8,
            131,
            0,
            8,
            67,
            0,
            9,
            231,
            80,
            7,
            7,
            0,
            8,
            91,
            0,
            8,
            27,
            0,
            9,
            151,
            84,
            7,
            67,
            0,
            8,
            123,
            0,
            8,
            59,
            0,
            9,
            215,
            82,
            7,
            19,
            0,
            8,
            107,
            0,
            8,
            43,
            0,
            9,
            183,
            0,
            8,
            11,
            0,
            8,
            139,
            0,
            8,
            75,
            0,
            9,
            247,
            80,
            7,
            5,
            0,
            8,
            87,
            0,
            8,
            23,
            192,
            8,
            0,
            83,
            7,
            51,
            0,
            8,
            119,
            0,
            8,
            55,
            0,
            9,
            207,
            81,
            7,
            15,
            0,
            8,
            103,
            0,
            8,
            39,
            0,
            9,
            175,
            0,
            8,
            7,
            0,
            8,
            135,
            0,
            8,
            71,
            0,
            9,
            239,
            80,
            7,
            9,
            0,
            8,
            95,
            0,
            8,
            31,
            0,
            9,
            159,
            84,
            7,
            99,
            0,
            8,
            127,
            0,
            8,
            63,
            0,
            9,
            223,
            82,
            7,
            27,
            0,
            8,
            111,
            0,
            8,
            47,
            0,
            9,
            191,
            0,
            8,
            15,
            0,
            8,
            143,
            0,
            8,
            79,
            0,
            9,
            255
        };

        // Token: 0x0400031D RID: 797
        internal static readonly int[] fixed_td = new int[]
        {
            80,
            5,
            1,
            87,
            5,
            257,
            83,
            5,
            17,
            91,
            5,
            4097,
            81,
            5,
            5,
            89,
            5,
            1025,
            85,
            5,
            65,
            93,
            5,
            16385,
            80,
            5,
            3,
            88,
            5,
            513,
            84,
            5,
            33,
            92,
            5,
            8193,
            82,
            5,
            9,
            90,
            5,
            2049,
            86,
            5,
            129,
            192,
            5,
            24577,
            80,
            5,
            2,
            87,
            5,
            385,
            83,
            5,
            25,
            91,
            5,
            6145,
            81,
            5,
            7,
            89,
            5,
            1537,
            85,
            5,
            97,
            93,
            5,
            24577,
            80,
            5,
            4,
            88,
            5,
            769,
            84,
            5,
            49,
            92,
            5,
            12289,
            82,
            5,
            13,
            90,
            5,
            3073,
            86,
            5,
            193,
            192,
            5,
            24577
        };

        // Token: 0x0400031E RID: 798
        internal static readonly int[] cplens = new int[]
        {
            3,
            4,
            5,
            6,
            7,
            8,
            9,
            10,
            11,
            13,
            15,
            17,
            19,
            23,
            27,
            31,
            35,
            43,
            51,
            59,
            67,
            83,
            99,
            115,
            131,
            163,
            195,
            227,
            258,
            0,
            0
        };

        // Token: 0x0400031F RID: 799
        internal static readonly int[] cplext = new int[]
        {
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            1,
            1,
            2,
            2,
            2,
            2,
            3,
            3,
            3,
            3,
            4,
            4,
            4,
            4,
            5,
            5,
            5,
            5,
            0,
            112,
            112
        };

        // Token: 0x04000320 RID: 800
        internal static readonly int[] cpdist = new int[]
        {
            1,
            2,
            3,
            4,
            5,
            7,
            9,
            13,
            17,
            25,
            33,
            49,
            65,
            97,
            129,
            193,
            257,
            385,
            513,
            769,
            1025,
            1537,
            2049,
            3073,
            4097,
            6145,
            8193,
            12289,
            16385,
            24577
        };

        // Token: 0x04000321 RID: 801
        internal static readonly int[] cpdext = new int[]
        {
            0,
            0,
            0,
            0,
            1,
            1,
            2,
            2,
            3,
            3,
            4,
            4,
            5,
            5,
            6,
            6,
            7,
            7,
            8,
            8,
            9,
            9,
            10,
            10,
            11,
            11,
            12,
            12,
            13,
            13
        };

        // Token: 0x04000322 RID: 802
        internal int[] hn = null;

        // Token: 0x04000323 RID: 803
        internal int[] v = null;

        // Token: 0x04000324 RID: 804
        internal int[] c = null;

        // Token: 0x04000325 RID: 805
        internal int[] r = null;

        // Token: 0x04000326 RID: 806
        internal int[] u = null;

        // Token: 0x04000327 RID: 807
        internal int[] x = null;
    }
}


namespace Ionic.Zlib
{
    // Token: 0x02000067 RID: 103
    internal static class InternalConstants
    {
        // Token: 0x04000388 RID: 904
        internal static readonly int MAX_BITS = 15;

        // Token: 0x04000389 RID: 905
        internal static readonly int BL_CODES = 19;

        // Token: 0x0400038A RID: 906
        internal static readonly int D_CODES = 30;

        // Token: 0x0400038B RID: 907
        internal static readonly int LITERALS = 256;

        // Token: 0x0400038C RID: 908
        internal static readonly int LENGTH_CODES = 29;

        // Token: 0x0400038D RID: 909
        internal static readonly int L_CODES = InternalConstants.LITERALS + 1 + InternalConstants.LENGTH_CODES;

        // Token: 0x0400038E RID: 910
        internal static readonly int MAX_BL_BITS = 7;

        // Token: 0x0400038F RID: 911
        internal static readonly int REP_3_6 = 16;

        // Token: 0x04000390 RID: 912
        internal static readonly int REPZ_3_10 = 17;

        // Token: 0x04000391 RID: 913
        internal static readonly int REPZ_11_138 = 18;
    }
}


namespace Ionic.Zlib
{
    // Token: 0x02000058 RID: 88
    internal static class InternalInflateConstants
    {
        // Token: 0x040002DB RID: 731
        internal static readonly int[] InflateMask = new int[]
        {
            0,
            1,
            3,
            7,
            15,
            31,
            63,
            127,
            255,
            511,
            1023,
            2047,
            4095,
            8191,
            16383,
            32767,
            65535
        };
    }
}


namespace Ionic.Zlib
{
    /// <summary>
    ///   A class for compressing streams using the
    ///   Deflate algorithm with multiple threads.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    ///   This class performs DEFLATE compression through writing.  For
    ///   more information on the Deflate algorithm, see IETF RFC 1951,
    ///   "DEFLATE Compressed Data Format Specification version 1.3."
    /// </para>
    ///
    /// <para>
    ///   This class is similar to <see cref="T:Ionic.Zlib.DeflateStream" />, except
    ///   that this class is for compression only, and this implementation uses an
    ///   approach that employs multiple worker threads to perform the DEFLATE.  On
    ///   a multi-cpu or multi-core computer, the performance of this class can be
    ///   significantly higher than the single-threaded DeflateStream, particularly
    ///   for larger streams.  How large?  Anything over 10mb is a good candidate
    ///   for parallel compression.
    /// </para>
    ///
    /// <para>
    ///   The tradeoff is that this class uses more memory and more CPU than the
    ///   vanilla DeflateStream, and also is less efficient as a compressor. For
    ///   large files the size of the compressed data stream can be less than 1%
    ///   larger than the size of a compressed data stream from the vanialla
    ///   DeflateStream.  For smaller files the difference can be larger.  The
    ///   difference will also be larger if you set the BufferSize to be lower than
    ///   the default value.  Your mileage may vary. Finally, for small files, the
    ///   ParallelDeflateOutputStream can be much slower than the vanilla
    ///   DeflateStream, because of the overhead associated to using the thread
    ///   pool.
    /// </para>
    ///
    /// </remarks>
    /// <seealso cref="T:Ionic.Zlib.DeflateStream" />
    // Token: 0x0200005E RID: 94
    public class ParallelDeflateOutputStream : Stream
    {
        /// <summary>
        /// Create a ParallelDeflateOutputStream.
        /// </summary>
        /// <remarks>
        ///
        /// <para>
        ///   This stream compresses data written into it via the DEFLATE
        ///   algorithm (see RFC 1951), and writes out the compressed byte stream.
        /// </para>
        ///
        /// <para>
        ///   The instance will use the default compression level, the default
        ///   buffer sizes and the default number of threads and buffers per
        ///   thread.
        /// </para>
        ///
        /// <para>
        ///   This class is similar to <see cref="T:Ionic.Zlib.DeflateStream" />,
        ///   except that this implementation uses an approach that employs
        ///   multiple worker threads to perform the DEFLATE.  On a multi-cpu or
        ///   multi-core computer, the performance of this class can be
        ///   significantly higher than the single-threaded DeflateStream,
        ///   particularly for larger streams.  How large?  Anything over 10mb is
        ///   a good candidate for parallel compression.
        /// </para>
        ///
        /// </remarks>
        ///
        /// <example>
        ///
        /// This example shows how to use a ParallelDeflateOutputStream to compress
        /// data.  It reads a file, compresses it, and writes the compressed data to
        /// a second, output file.
        ///
        /// <code>
        /// byte[] buffer = new byte[WORKING_BUFFER_SIZE];
        /// int n= -1;
        /// String outputFile = fileToCompress + ".compressed";
        /// using (System.IO.Stream input = System.IO.File.OpenRead(fileToCompress))
        /// {
        ///     using (var raw = System.IO.File.Create(outputFile))
        ///     {
        ///         using (Stream compressor = new ParallelDeflateOutputStream(raw))
        ///         {
        ///             while ((n= input.Read(buffer, 0, buffer.Length)) != 0)
        ///             {
        ///                 compressor.Write(buffer, 0, n);
        ///             }
        ///         }
        ///     }
        /// }
        /// </code>
        /// <code lang="VB">
        /// Dim buffer As Byte() = New Byte(4096) {}
        /// Dim n As Integer = -1
        /// Dim outputFile As String = (fileToCompress &amp; ".compressed")
        /// Using input As Stream = File.OpenRead(fileToCompress)
        ///     Using raw As FileStream = File.Create(outputFile)
        ///         Using compressor As Stream = New ParallelDeflateOutputStream(raw)
        ///             Do While (n &lt;&gt; 0)
        ///                 If (n &gt; 0) Then
        ///                     compressor.Write(buffer, 0, n)
        ///                 End If
        ///                 n = input.Read(buffer, 0, buffer.Length)
        ///             Loop
        ///         End Using
        ///     End Using
        /// End Using
        /// </code>
        /// </example>
        /// <param name="stream">The stream to which compressed data will be written.</param>
        // Token: 0x06000453 RID: 1107 RVA: 0x00024EB1 File Offset: 0x000230B1
        public ParallelDeflateOutputStream(Stream stream) : this(stream, CompressionLevel.Default, CompressionStrategy.Default, false)
        {
        }

        /// <summary>
        ///   Create a ParallelDeflateOutputStream using the specified CompressionLevel.
        /// </summary>
        /// <remarks>
        ///   See the <see cref="M:Ionic.Zlib.ParallelDeflateOutputStream.#ctor(System.IO.Stream)" />
        ///   constructor for example code.
        /// </remarks>
        /// <param name="stream">The stream to which compressed data will be written.</param>
        /// <param name="level">A tuning knob to trade speed for effectiveness.</param>
        // Token: 0x06000454 RID: 1108 RVA: 0x00024EC0 File Offset: 0x000230C0
        public ParallelDeflateOutputStream(Stream stream, CompressionLevel level) : this(stream, level, CompressionStrategy.Default, false)
        {
        }

        /// <summary>
        /// Create a ParallelDeflateOutputStream and specify whether to leave the captive stream open
        /// when the ParallelDeflateOutputStream is closed.
        /// </summary>
        /// <remarks>
        ///   See the <see cref="M:Ionic.Zlib.ParallelDeflateOutputStream.#ctor(System.IO.Stream)" />
        ///   constructor for example code.
        /// </remarks>
        /// <param name="stream">The stream to which compressed data will be written.</param>
        /// <param name="leaveOpen">
        ///    true if the application would like the stream to remain open after inflation/deflation.
        /// </param>
        // Token: 0x06000455 RID: 1109 RVA: 0x00024ECF File Offset: 0x000230CF
        public ParallelDeflateOutputStream(Stream stream, bool leaveOpen) : this(stream, CompressionLevel.Default, CompressionStrategy.Default, leaveOpen)
        {
        }

        /// <summary>
        /// Create a ParallelDeflateOutputStream and specify whether to leave the captive stream open
        /// when the ParallelDeflateOutputStream is closed.
        /// </summary>
        /// <remarks>
        ///   See the <see cref="M:Ionic.Zlib.ParallelDeflateOutputStream.#ctor(System.IO.Stream)" />
        ///   constructor for example code.
        /// </remarks>
        /// <param name="stream">The stream to which compressed data will be written.</param>
        /// <param name="level">A tuning knob to trade speed for effectiveness.</param>
        /// <param name="leaveOpen">
        ///    true if the application would like the stream to remain open after inflation/deflation.
        /// </param>
        // Token: 0x06000456 RID: 1110 RVA: 0x00024EDE File Offset: 0x000230DE
        public ParallelDeflateOutputStream(Stream stream, CompressionLevel level, bool leaveOpen) : this(stream, CompressionLevel.Default, CompressionStrategy.Default, leaveOpen)
        {
        }

        /// <summary>
        /// Create a ParallelDeflateOutputStream using the specified
        /// CompressionLevel and CompressionStrategy, and specifying whether to
        /// leave the captive stream open when the ParallelDeflateOutputStream is
        /// closed.
        /// </summary>
        /// <remarks>
        ///   See the <see cref="M:Ionic.Zlib.ParallelDeflateOutputStream.#ctor(System.IO.Stream)" />
        ///   constructor for example code.
        /// </remarks>
        /// <param name="stream">The stream to which compressed data will be written.</param>
        /// <param name="level">A tuning knob to trade speed for effectiveness.</param>
        /// <param name="strategy">
        ///   By tweaking this parameter, you may be able to optimize the compression for
        ///   data with particular characteristics.
        /// </param>
        /// <param name="leaveOpen">
        ///    true if the application would like the stream to remain open after inflation/deflation.
        /// </param>
        // Token: 0x06000457 RID: 1111 RVA: 0x00024EF0 File Offset: 0x000230F0
        public ParallelDeflateOutputStream(Stream stream, CompressionLevel level, CompressionStrategy strategy, bool leaveOpen)
        {
            this._outStream = stream;
            this._compressLevel = level;
            this.Strategy = strategy;
            this._leaveOpen = leaveOpen;
            this.MaxBufferPairs = 16;
        }

        /// <summary>
        ///   The ZLIB strategy to be used during compression.
        /// </summary>
        // Token: 0x17000112 RID: 274
        // (get) Token: 0x06000458 RID: 1112 RVA: 0x00024F64 File Offset: 0x00023164
        // (set) Token: 0x06000459 RID: 1113 RVA: 0x00024F7B File Offset: 0x0002317B
        public CompressionStrategy Strategy { get; private set; }

        /// <summary>
        ///   The maximum number of buffer pairs to use.
        /// </summary>
        ///
        /// <remarks>
        /// <para>
        ///   This property sets an upper limit on the number of memory buffer
        ///   pairs to create.  The implementation of this stream allocates
        ///   multiple buffers to facilitate parallel compression.  As each buffer
        ///   fills up, this stream uses <see cref="M:System.Threading.ThreadPool.QueueUserWorkItem(System.Threading.WaitCallback)">
        ///   ThreadPool.QueueUserWorkItem()</see>
        ///   to compress those buffers in a background threadpool thread. After a
        ///   buffer is compressed, it is re-ordered and written to the output
        ///   stream.
        /// </para>
        ///
        /// <para>
        ///   A higher number of buffer pairs enables a higher degree of
        ///   parallelism, which tends to increase the speed of compression on
        ///   multi-cpu computers.  On the other hand, a higher number of buffer
        ///   pairs also implies a larger memory consumption, more active worker
        ///   threads, and a higher cpu utilization for any compression. This
        ///   property enables the application to limit its memory consumption and
        ///   CPU utilization behavior depending on requirements.
        /// </para>
        ///
        /// <para>
        ///   For each compression "task" that occurs in parallel, there are 2
        ///   buffers allocated: one for input and one for output.  This property
        ///   sets a limit for the number of pairs.  The total amount of storage
        ///   space allocated for buffering will then be (N*S*2), where N is the
        ///   number of buffer pairs, S is the size of each buffer (<see cref="P:Ionic.Zlib.ParallelDeflateOutputStream.BufferSize" />).  By default, DotNetZip allocates 4 buffer
        ///   pairs per CPU core, so if your machine has 4 cores, and you retain
        ///   the default buffer size of 128k, then the
        ///   ParallelDeflateOutputStream will use 4 * 4 * 2 * 128kb of buffer
        ///   memory in total, or 4mb, in blocks of 128kb.  If you then set this
        ///   property to 8, then the number will be 8 * 2 * 128kb of buffer
        ///   memory, or 2mb.
        /// </para>
        ///
        /// <para>
        ///   CPU utilization will also go up with additional buffers, because a
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
        ///   This property is not the number of buffer pairs to use; it is an
        ///   upper limit. An illustration: Suppose you have an application that
        ///   uses the default value of this property (which is 16), and it runs
        ///   on a machine with 2 CPU cores. In that case, DotNetZip will allocate
        ///   4 buffer pairs per CPU core, for a total of 8 pairs.  The upper
        ///   limit specified by this property has no effect.
        /// </para>
        ///
        /// <para>
        ///   The application can set this value at any time, but it is effective
        ///   only before the first call to Write(), which is when the buffers are
        ///   allocated.
        /// </para>
        /// </remarks>
        // Token: 0x17000113 RID: 275
        // (get) Token: 0x0600045A RID: 1114 RVA: 0x00024F84 File Offset: 0x00023184
        // (set) Token: 0x0600045B RID: 1115 RVA: 0x00024F9C File Offset: 0x0002319C
        public int MaxBufferPairs
        {
            get
            {
                return this._maxBufferPairs;
            }
            set
            {
                if (value < 4)
                {
                    throw new ArgumentException("MaxBufferPairs", "Value must be 4 or greater.");
                }
                this._maxBufferPairs = value;
            }
        }

        /// <summary>
        ///   The size of the buffers used by the compressor threads.
        /// </summary>
        /// <remarks>
        ///
        /// <para>
        ///   The default buffer size is 128k. The application can set this value
        ///   at any time, but it is effective only before the first Write().
        /// </para>
        ///
        /// <para>
        ///   Larger buffer sizes implies larger memory consumption but allows
        ///   more efficient compression. Using smaller buffer sizes consumes less
        ///   memory but may result in less effective compression.  For example,
        ///   using the default buffer size of 128k, the compression delivered is
        ///   within 1% of the compression delivered by the single-threaded <see cref="T:Ionic.Zlib.DeflateStream" />.  On the other hand, using a
        ///   BufferSize of 8k can result in a compressed data stream that is 5%
        ///   larger than that delivered by the single-threaded
        ///   <c>DeflateStream</c>.  Excessively small buffer sizes can also cause
        ///   the speed of the ParallelDeflateOutputStream to drop, because of
        ///   larger thread scheduling overhead dealing with many many small
        ///   buffers.
        /// </para>
        ///
        /// <para>
        ///   The total amount of storage space allocated for buffering will be
        ///   (N*S*2), where N is the number of buffer pairs, and S is the size of
        ///   each buffer (this property). There are 2 buffers used by the
        ///   compressor, one for input and one for output.  By default, DotNetZip
        ///   allocates 4 buffer pairs per CPU core, so if your machine has 4
        ///   cores, then the number of buffer pairs used will be 16. If you
        ///   accept the default value of this property, 128k, then the
        ///   ParallelDeflateOutputStream will use 16 * 2 * 128kb of buffer memory
        ///   in total, or 4mb, in blocks of 128kb.  If you set this property to
        ///   64kb, then the number will be 16 * 2 * 64kb of buffer memory, or
        ///   2mb.
        /// </para>
        ///
        /// </remarks>
        // Token: 0x17000114 RID: 276
        // (get) Token: 0x0600045C RID: 1116 RVA: 0x00024FCC File Offset: 0x000231CC
        // (set) Token: 0x0600045D RID: 1117 RVA: 0x00024FE4 File Offset: 0x000231E4
        public int BufferSize
        {
            get
            {
                return this._bufferSize;
            }
            set
            {
                if (value < 1024)
                {
                    throw new ArgumentOutOfRangeException("BufferSize", "BufferSize must be greater than 1024 bytes");
                }
                this._bufferSize = value;
            }
        }

        /// <summary>
        /// The CRC32 for the data that was written out, prior to compression.
        /// </summary>
        /// <remarks>
        /// This value is meaningful only after a call to Close().
        /// </remarks>
        // Token: 0x17000115 RID: 277
        // (get) Token: 0x0600045E RID: 1118 RVA: 0x00025018 File Offset: 0x00023218
        public int Crc32
        {
            get
            {
                return this._Crc32;
            }
        }

        /// <summary>
        /// The total number of uncompressed bytes processed by the ParallelDeflateOutputStream.
        /// </summary>
        /// <remarks>
        /// This value is meaningful only after a call to Close().
        /// </remarks>
        // Token: 0x17000116 RID: 278
        // (get) Token: 0x0600045F RID: 1119 RVA: 0x00025030 File Offset: 0x00023230
        public long BytesProcessed
        {
            get
            {
                return this._totalBytesProcessed;
            }
        }

        // Token: 0x06000460 RID: 1120 RVA: 0x00025048 File Offset: 0x00023248
        private void _InitializePoolOfWorkItems()
        {
            this._toWrite = new Queue<int>();
            this._toFill = new Queue<int>();
            this._pool = new List<WorkItem>();
            int nTasks = ParallelDeflateOutputStream.BufferPairsPerCore * Environment.ProcessorCount;
            nTasks = Math.Min(nTasks, this._maxBufferPairs);
            for (int i = 0; i < nTasks; i++)
            {
                this._pool.Add(new WorkItem(this._bufferSize, this._compressLevel, this.Strategy, i));
                this._toFill.Enqueue(i);
            }
            this._newlyCompressedBlob = new AutoResetEvent(false);
            this._runningCrc = new CRC32();
            this._currentlyFilling = -1;
            this._lastFilled = -1;
            this._lastWritten = -1;
            this._latestCompressed = -1;
        }

        /// <summary>
        ///   Write data to the stream.
        /// </summary>
        ///
        /// <remarks>
        ///
        /// <para>
        ///   To use the ParallelDeflateOutputStream to compress data, create a
        ///   ParallelDeflateOutputStream with CompressionMode.Compress, passing a
        ///   writable output stream.  Then call Write() on that
        ///   ParallelDeflateOutputStream, providing uncompressed data as input.  The
        ///   data sent to the output stream will be the compressed form of the data
        ///   written.
        /// </para>
        ///
        /// <para>
        ///   To decompress data, use the <see cref="T:Ionic.Zlib.DeflateStream" /> class.
        /// </para>
        ///
        /// </remarks>
        /// <param name="buffer">The buffer holding data to write to the stream.</param>
        /// <param name="offset">the offset within that data array to find the first byte to write.</param>
        /// <param name="count">the number of bytes to write.</param>
        // Token: 0x06000461 RID: 1121 RVA: 0x00025108 File Offset: 0x00023308
        public override void Write(byte[] buffer, int offset, int count)
        {
            bool mustWait = false;
            if (this._isClosed)
            {
                throw new InvalidOperationException();
            }
            if (this._pendingException != null)
            {
                this._handlingException = true;
                Exception pe = this._pendingException;
                this._pendingException = null;
                throw pe;
            }
            if (count != 0)
            {
                if (!this._firstWriteDone)
                {
                    this._InitializePoolOfWorkItems();
                    this._firstWriteDone = true;
                }
                for (; ; )
                {
                    this.EmitPendingBuffers(false, mustWait);
                    mustWait = false;
                    int ix;
                    if (this._currentlyFilling >= 0)
                    {
                        ix = this._currentlyFilling;
                        goto IL_D6;
                    }
                    if (this._toFill.Count != 0)
                    {
                        ix = this._toFill.Dequeue();
                        this._lastFilled++;
                        goto IL_D6;
                    }
                    mustWait = true;
                    IL_1A2:
                    if (count <= 0)
                    {
                        return;
                    }
                    continue;
                    IL_D6:
                    WorkItem workitem = this._pool[ix];
                    int limit = (workitem.buffer.Length - workitem.inputBytesAvailable > count) ? count : (workitem.buffer.Length - workitem.inputBytesAvailable);
                    workitem.ordinal = this._lastFilled;
                    Buffer.BlockCopy(buffer, offset, workitem.buffer, workitem.inputBytesAvailable, limit);
                    count -= limit;
                    offset += limit;
                    workitem.inputBytesAvailable += limit;
                    if (workitem.inputBytesAvailable == workitem.buffer.Length)
                    {
                        if (!ThreadPool.QueueUserWorkItem(new WaitCallback(this._DeflateOne), workitem))
                        {
                            break;
                        }
                        this._currentlyFilling = -1;
                    }
                    else
                    {
                        this._currentlyFilling = ix;
                    }
                    if (count > 0)
                    {
                    }
                    goto IL_1A2;
                }
                throw new Exception("Cannot enqueue workitem");
            }
        }

        // Token: 0x06000462 RID: 1122 RVA: 0x000252C8 File Offset: 0x000234C8
        private void _FlushFinish()
        {
            byte[] buffer = new byte[128];
            ZlibCodec compressor = new ZlibCodec();
            int rc = compressor.InitializeDeflate(this._compressLevel, false);
            compressor.InputBuffer = null;
            compressor.NextIn = 0;
            compressor.AvailableBytesIn = 0;
            compressor.OutputBuffer = buffer;
            compressor.NextOut = 0;
            compressor.AvailableBytesOut = buffer.Length;
            rc = compressor.Deflate(FlushType.Finish);
            if (rc != 1 && rc != 0)
            {
                throw new Exception("deflating: " + compressor.Message);
            }
            if (buffer.Length - compressor.AvailableBytesOut > 0)
            {
                this._outStream.Write(buffer, 0, buffer.Length - compressor.AvailableBytesOut);
            }
            compressor.EndDeflate();
            this._Crc32 = this._runningCrc.Crc32Result;
        }

        // Token: 0x06000463 RID: 1123 RVA: 0x00025394 File Offset: 0x00023594
        private void _Flush(bool lastInput)
        {
            if (this._isClosed)
            {
                throw new InvalidOperationException();
            }
            if (!this.emitting)
            {
                if (this._currentlyFilling >= 0)
                {
                    WorkItem workitem = this._pool[this._currentlyFilling];
                    this._DeflateOne(workitem);
                    this._currentlyFilling = -1;
                }
                if (lastInput)
                {
                    this.EmitPendingBuffers(true, false);
                    this._FlushFinish();
                }
                else
                {
                    this.EmitPendingBuffers(false, false);
                }
            }
        }

        /// <summary>
        /// Flush the stream.
        /// </summary>
        // Token: 0x06000464 RID: 1124 RVA: 0x0002541C File Offset: 0x0002361C
        public override void Flush()
        {
            if (this._pendingException != null)
            {
                this._handlingException = true;
                Exception pe = this._pendingException;
                this._pendingException = null;
                throw pe;
            }
            if (!this._handlingException)
            {
                this._Flush(false);
            }
        }

        /// <summary>
        /// Close the stream.
        /// </summary>
        /// <remarks>
        /// You must call Close on the stream to guarantee that all of the data written in has
        /// been compressed, and the compressed data has been written out.
        /// </remarks>
        // Token: 0x06000465 RID: 1125 RVA: 0x0002546C File Offset: 0x0002366C
        public override void Close()
        {
            if (this._pendingException != null)
            {
                this._handlingException = true;
                Exception pe = this._pendingException;
                this._pendingException = null;
                throw pe;
            }
            if (!this._handlingException)
            {
                if (!this._isClosed)
                {
                    this._Flush(true);
                    if (!this._leaveOpen)
                    {
                        this._outStream.Close();
                    }
                    this._isClosed = true;
                }
            }
        }

        /// <summary>Dispose the object</summary>
        /// <remarks>
        ///   <para>
        ///     Because ParallelDeflateOutputStream is IDisposable, the
        ///     application must call this method when finished using the instance.
        ///   </para>
        ///   <para>
        ///     This method is generally called implicitly upon exit from
        ///     a <c>using</c> scope in C# (<c>Using</c> in VB).
        ///   </para>
        /// </remarks>
        // Token: 0x06000466 RID: 1126 RVA: 0x000254E8 File Offset: 0x000236E8
        public new void Dispose()
        {
            this.Close();
            this._pool = null;
            this.Dispose(true);
        }

        /// <summary>The Dispose method</summary>
        /// <param name="disposing">
        ///   indicates whether the Dispose method was invoked by user code.
        /// </param>
        // Token: 0x06000467 RID: 1127 RVA: 0x00025501 File Offset: 0x00023701
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        /// <summary>
        ///   Resets the stream for use with another stream.
        /// </summary>
        /// <remarks>
        ///   Because the ParallelDeflateOutputStream is expensive to create, it
        ///   has been designed so that it can be recycled and re-used.  You have
        ///   to call Close() on the stream first, then you can call Reset() on
        ///   it, to use it again on another stream.
        /// </remarks>
        ///
        /// <param name="stream">
        ///   The new output stream for this era.
        /// </param>
        ///
        /// <example>
        /// <code>
        /// ParallelDeflateOutputStream deflater = null;
        /// foreach (var inputFile in listOfFiles)
        /// {
        ///     string outputFile = inputFile + ".compressed";
        ///     using (System.IO.Stream input = System.IO.File.OpenRead(inputFile))
        ///     {
        ///         using (var outStream = System.IO.File.Create(outputFile))
        ///         {
        ///             if (deflater == null)
        ///                 deflater = new ParallelDeflateOutputStream(outStream,
        ///                                                            CompressionLevel.Best,
        ///                                                            CompressionStrategy.Default,
        ///                                                            true);
        ///             deflater.Reset(outStream);
        ///
        ///             while ((n= input.Read(buffer, 0, buffer.Length)) != 0)
        ///             {
        ///                 deflater.Write(buffer, 0, n);
        ///             }
        ///         }
        ///     }
        /// }
        /// </code>
        /// </example>
        // Token: 0x06000468 RID: 1128 RVA: 0x0002550C File Offset: 0x0002370C
        public void Reset(Stream stream)
        {
            if (this._firstWriteDone)
            {
                this._toWrite.Clear();
                this._toFill.Clear();
                foreach (WorkItem workitem in this._pool)
                {
                    this._toFill.Enqueue(workitem.index);
                    workitem.ordinal = -1;
                }
                this._firstWriteDone = false;
                this._totalBytesProcessed = 0L;
                this._runningCrc = new CRC32();
                this._isClosed = false;
                this._currentlyFilling = -1;
                this._lastFilled = -1;
                this._lastWritten = -1;
                this._latestCompressed = -1;
                this._outStream = stream;
            }
        }

        // Token: 0x06000469 RID: 1129 RVA: 0x000255E4 File Offset: 0x000237E4
        private void EmitPendingBuffers(bool doAll, bool mustWait)
        {
            if (!this.emitting)
            {
                this.emitting = true;
                if (doAll || mustWait)
                {
                    this._newlyCompressedBlob.WaitOne();
                }
                do
                {
                    int firstSkip = -1;
                    int millisecondsToWait = doAll ? 200 : (mustWait ? -1 : 0);
                    int nextToWrite = -1;
                    for (; ; )
                    {
                        if (Monitor.TryEnter(this._toWrite, millisecondsToWait))
                        {
                            nextToWrite = -1;
                            try
                            {
                                if (this._toWrite.Count > 0)
                                {
                                    nextToWrite = this._toWrite.Dequeue();
                                }
                            }
                            finally
                            {
                                Monitor.Exit(this._toWrite);
                            }
                            if (nextToWrite >= 0)
                            {
                                WorkItem workitem = this._pool[nextToWrite];
                                if (workitem.ordinal != this._lastWritten + 1)
                                {
                                    lock (this._toWrite)
                                    {
                                        this._toWrite.Enqueue(nextToWrite);
                                    }
                                    if (firstSkip == nextToWrite)
                                    {
                                        this._newlyCompressedBlob.WaitOne();
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
                                    this._outStream.Write(workitem.compressed, 0, workitem.compressedBytesAvailable);
                                    this._runningCrc.Combine(workitem.crc, workitem.inputBytesAvailable);
                                    this._totalBytesProcessed += (long)workitem.inputBytesAvailable;
                                    workitem.inputBytesAvailable = 0;
                                    this._lastWritten = workitem.ordinal;
                                    this._toFill.Enqueue(workitem.index);
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
                        IL_1B3:
                        if (nextToWrite < 0)
                        {
                            break;
                        }
                        continue;
                        goto IL_1B3;
                    }
                }
                while (doAll && this._lastWritten != this._latestCompressed);
                this.emitting = false;
            }
        }

        // Token: 0x0600046A RID: 1130 RVA: 0x000257F8 File Offset: 0x000239F8
        private void _DeflateOne(object wi)
        {
            WorkItem workitem = (WorkItem)wi;
            try
            {
                int myItem = workitem.index;
                CRC32 crc = new CRC32();
                crc.SlurpBlock(workitem.buffer, 0, workitem.inputBytesAvailable);
                this.DeflateOneSegment(workitem);
                workitem.crc = crc.Crc32Result;
                lock (this._latestLock)
                {
                    if (workitem.ordinal > this._latestCompressed)
                    {
                        this._latestCompressed = workitem.ordinal;
                    }
                }
                lock (this._toWrite)
                {
                    this._toWrite.Enqueue(workitem.index);
                }
                this._newlyCompressedBlob.Set();
            }
            catch (Exception exc)
            {
                lock (this._eLock)
                {
                    if (this._pendingException != null)
                    {
                        this._pendingException = exc;
                    }
                }
            }
        }

        // Token: 0x0600046B RID: 1131 RVA: 0x00025934 File Offset: 0x00023B34
        private bool DeflateOneSegment(WorkItem workitem)
        {
            ZlibCodec compressor = workitem.compressor;
            compressor.ResetDeflate();
            compressor.NextIn = 0;
            compressor.AvailableBytesIn = workitem.inputBytesAvailable;
            compressor.NextOut = 0;
            compressor.AvailableBytesOut = workitem.compressed.Length;
            do
            {
                compressor.Deflate(FlushType.None);
            }
            while (compressor.AvailableBytesIn > 0 || compressor.AvailableBytesOut == 0);
            int rc = compressor.Deflate(FlushType.Sync);
            workitem.compressedBytesAvailable = (int)compressor.TotalBytesOut;
            return true;
        }

        // Token: 0x0600046C RID: 1132 RVA: 0x000259B8 File Offset: 0x00023BB8
        [Conditional("Trace")]
        private void TraceOutput(ParallelDeflateOutputStream.TraceBits bits, string format, params object[] varParams)
        {
            if ((bits & this._DesiredTrace) != ParallelDeflateOutputStream.TraceBits.None)
            {
                lock (this._outputLock)
                {
                    int tid = Thread.CurrentThread.GetHashCode();
                    Console.ForegroundColor = tid % 8 + ConsoleColor.DarkGray;
                    Console.Write("{0:000} PDOS ", tid);
                    Console.WriteLine(format, varParams);
                    Console.ResetColor();
                }
            }
        }

        /// <summary>
        /// Indicates whether the stream supports Seek operations.
        /// </summary>
        /// <remarks>
        /// Always returns false.
        /// </remarks>
        // Token: 0x17000117 RID: 279
        // (get) Token: 0x0600046D RID: 1133 RVA: 0x00025A38 File Offset: 0x00023C38
        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Indicates whether the stream supports Read operations.
        /// </summary>
        /// <remarks>
        /// Always returns false.
        /// </remarks>
        // Token: 0x17000118 RID: 280
        // (get) Token: 0x0600046E RID: 1134 RVA: 0x00025A4C File Offset: 0x00023C4C
        public override bool CanRead
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Indicates whether the stream supports Write operations.
        /// </summary>
        /// <remarks>
        /// Returns true if the provided stream is writable.
        /// </remarks>
        // Token: 0x17000119 RID: 281
        // (get) Token: 0x0600046F RID: 1135 RVA: 0x00025A60 File Offset: 0x00023C60
        public override bool CanWrite
        {
            get
            {
                return this._outStream.CanWrite;
            }
        }

        /// <summary>
        /// Reading this property always throws a NotSupportedException.
        /// </summary>
        // Token: 0x1700011A RID: 282
        // (get) Token: 0x06000470 RID: 1136 RVA: 0x00025A7D File Offset: 0x00023C7D
        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Returns the current position of the output stream.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Because the output gets written by a background thread,
        ///     the value may change asynchronously.  Setting this
        ///     property always throws a NotSupportedException.
        ///   </para>
        /// </remarks>
        // Token: 0x1700011B RID: 283
        // (get) Token: 0x06000471 RID: 1137 RVA: 0x00025A88 File Offset: 0x00023C88
        // (set) Token: 0x06000472 RID: 1138 RVA: 0x00025AA5 File Offset: 0x00023CA5
        public override long Position
        {
            get
            {
                return this._outStream.Position;
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// This method always throws a NotSupportedException.
        /// </summary>
        /// <param name="buffer">
        ///   The buffer into which data would be read, IF THIS METHOD
        ///   ACTUALLY DID ANYTHING.
        /// </param>
        /// <param name="offset">
        ///   The offset within that data array at which to insert the
        ///   data that is read, IF THIS METHOD ACTUALLY DID
        ///   ANYTHING.
        /// </param>
        /// <param name="count">
        ///   The number of bytes to write, IF THIS METHOD ACTUALLY DID
        ///   ANYTHING.
        /// </param>
        /// <returns>nothing.</returns>
        // Token: 0x06000473 RID: 1139 RVA: 0x00025AAD File Offset: 0x00023CAD
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// This method always throws a NotSupportedException.
        /// </summary>
        /// <param name="offset">
        ///   The offset to seek to....
        ///   IF THIS METHOD ACTUALLY DID ANYTHING.
        /// </param>
        /// <param name="origin">
        ///   The reference specifying how to apply the offset....  IF
        ///   THIS METHOD ACTUALLY DID ANYTHING.
        /// </param>
        /// <returns>nothing. It always throws.</returns>
        // Token: 0x06000474 RID: 1140 RVA: 0x00025AB5 File Offset: 0x00023CB5
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// This method always throws a NotSupportedException.
        /// </summary>
        /// <param name="value">
        ///   The new value for the stream length....  IF
        ///   THIS METHOD ACTUALLY DID ANYTHING.
        /// </param>
        // Token: 0x06000475 RID: 1141 RVA: 0x00025ABD File Offset: 0x00023CBD
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        // Token: 0x04000330 RID: 816
        private static readonly int IO_BUFFER_SIZE_DEFAULT = 65536;

        // Token: 0x04000331 RID: 817
        private static readonly int BufferPairsPerCore = 4;

        // Token: 0x04000332 RID: 818
        private List<WorkItem> _pool;

        // Token: 0x04000333 RID: 819
        private bool _leaveOpen;

        // Token: 0x04000334 RID: 820
        private bool emitting;

        // Token: 0x04000335 RID: 821
        private Stream _outStream;

        // Token: 0x04000336 RID: 822
        private int _maxBufferPairs;

        // Token: 0x04000337 RID: 823
        private int _bufferSize = ParallelDeflateOutputStream.IO_BUFFER_SIZE_DEFAULT;

        // Token: 0x04000338 RID: 824
        private AutoResetEvent _newlyCompressedBlob;

        // Token: 0x04000339 RID: 825
        private object _outputLock = new object();

        // Token: 0x0400033A RID: 826
        private bool _isClosed;

        // Token: 0x0400033B RID: 827
        private bool _firstWriteDone;

        // Token: 0x0400033C RID: 828
        private int _currentlyFilling;

        // Token: 0x0400033D RID: 829
        private int _lastFilled;

        // Token: 0x0400033E RID: 830
        private int _lastWritten;

        // Token: 0x0400033F RID: 831
        private int _latestCompressed;

        // Token: 0x04000340 RID: 832
        private int _Crc32;

        // Token: 0x04000341 RID: 833
        private CRC32 _runningCrc;

        // Token: 0x04000342 RID: 834
        private object _latestLock = new object();

        // Token: 0x04000343 RID: 835
        private Queue<int> _toWrite;

        // Token: 0x04000344 RID: 836
        private Queue<int> _toFill;

        // Token: 0x04000345 RID: 837
        private long _totalBytesProcessed;

        // Token: 0x04000346 RID: 838
        private CompressionLevel _compressLevel;

        // Token: 0x04000347 RID: 839
        private volatile Exception _pendingException;

        // Token: 0x04000348 RID: 840
        private bool _handlingException;

        // Token: 0x04000349 RID: 841
        private object _eLock = new object();

        // Token: 0x0400034A RID: 842
        private ParallelDeflateOutputStream.TraceBits _DesiredTrace = ParallelDeflateOutputStream.TraceBits.EmitLock | ParallelDeflateOutputStream.TraceBits.EmitEnter | ParallelDeflateOutputStream.TraceBits.EmitBegin | ParallelDeflateOutputStream.TraceBits.EmitDone | ParallelDeflateOutputStream.TraceBits.EmitSkip | ParallelDeflateOutputStream.TraceBits.Session | ParallelDeflateOutputStream.TraceBits.Compress | ParallelDeflateOutputStream.TraceBits.WriteEnter | ParallelDeflateOutputStream.TraceBits.WriteTake;

        // Token: 0x0200005F RID: 95
        [Flags]
        private enum TraceBits : uint
        {
            // Token: 0x0400034D RID: 845
            None = 0u,
            // Token: 0x0400034E RID: 846
            NotUsed1 = 1u,
            // Token: 0x0400034F RID: 847
            EmitLock = 2u,
            // Token: 0x04000350 RID: 848
            EmitEnter = 4u,
            // Token: 0x04000351 RID: 849
            EmitBegin = 8u,
            // Token: 0x04000352 RID: 850
            EmitDone = 16u,
            // Token: 0x04000353 RID: 851
            EmitSkip = 32u,
            // Token: 0x04000354 RID: 852
            EmitAll = 58u,
            // Token: 0x04000355 RID: 853
            Flush = 64u,
            // Token: 0x04000356 RID: 854
            Lifecycle = 128u,
            // Token: 0x04000357 RID: 855
            Session = 256u,
            // Token: 0x04000358 RID: 856
            Synch = 512u,
            // Token: 0x04000359 RID: 857
            Instance = 1024u,
            // Token: 0x0400035A RID: 858
            Compress = 2048u,
            // Token: 0x0400035B RID: 859
            Write = 4096u,
            // Token: 0x0400035C RID: 860
            WriteEnter = 8192u,
            // Token: 0x0400035D RID: 861
            WriteTake = 16384u,
            // Token: 0x0400035E RID: 862
            All = 4294967295u
        }
    }
}


namespace Ionic.Zlib
{
    // Token: 0x02000066 RID: 102
    internal class SharedUtils
    {
        /// <summary>
        /// Performs an unsigned bitwise right shift with the specified number
        /// </summary>
        /// <param name="number">Number to operate on</param>
        /// <param name="bits">Ammount of bits to shift</param>
        /// <returns>The resulting number from the shift operation</returns>
        // Token: 0x06000480 RID: 1152 RVA: 0x00026760 File Offset: 0x00024960
        public static int URShift(int number, int bits)
        {
            return (int)((uint)number >> bits);
        }

        /// <summary>
        ///   Reads a number of characters from the current source TextReader and writes
        ///   the data to the target array at the specified index.
        /// </summary>
        ///
        /// <param name="sourceTextReader">The source TextReader to read from</param>
        /// <param name="target">Contains the array of characteres read from the source TextReader.</param>
        /// <param name="start">The starting index of the target array.</param>
        /// <param name="count">The maximum number of characters to read from the source TextReader.</param>
        ///
        /// <returns>
        ///   The number of characters read. The number will be less than or equal to
        ///   count depending on the data available in the source TextReader. Returns -1
        ///   if the end of the stream is reached.
        /// </returns>
        // Token: 0x06000481 RID: 1153 RVA: 0x00026778 File Offset: 0x00024978
        public static int ReadInput(TextReader sourceTextReader, byte[] target, int start, int count)
        {
            int result;
            if (target.Length == 0)
            {
                result = 0;
            }
            else
            {
                char[] charArray = new char[target.Length];
                int bytesRead = sourceTextReader.Read(charArray, start, count);
                if (bytesRead == 0)
                {
                    result = -1;
                }
                else
                {
                    for (int index = start; index < start + bytesRead; index++)
                    {
                        target[index] = (byte)charArray[index];
                    }
                    result = bytesRead;
                }
            }
            return result;
        }

        // Token: 0x06000482 RID: 1154 RVA: 0x000267E0 File Offset: 0x000249E0
        internal static byte[] ToByteArray(string sourceString)
        {
            return Encoding.UTF8.GetBytes(sourceString);
        }

        // Token: 0x06000483 RID: 1155 RVA: 0x00026800 File Offset: 0x00024A00
        internal static char[] ToCharArray(byte[] byteArray)
        {
            return Encoding.UTF8.GetChars(byteArray);
        }
    }
}


namespace Ionic.Zlib
{
    // Token: 0x02000068 RID: 104
    internal sealed class StaticTree
    {
        // Token: 0x06000486 RID: 1158 RVA: 0x00026888 File Offset: 0x00024A88
        private StaticTree(short[] treeCodes, int[] extraBits, int extraBase, int elems, int maxLength)
        {
            this.treeCodes = treeCodes;
            this.extraBits = extraBits;
            this.extraBase = extraBase;
            this.elems = elems;
            this.maxLength = maxLength;
        }

        // Token: 0x04000392 RID: 914
        internal static readonly short[] lengthAndLiteralsTreeCodes = new short[]
        {
            12,
            8,
            140,
            8,
            76,
            8,
            204,
            8,
            44,
            8,
            172,
            8,
            108,
            8,
            236,
            8,
            28,
            8,
            156,
            8,
            92,
            8,
            220,
            8,
            60,
            8,
            188,
            8,
            124,
            8,
            252,
            8,
            2,
            8,
            130,
            8,
            66,
            8,
            194,
            8,
            34,
            8,
            162,
            8,
            98,
            8,
            226,
            8,
            18,
            8,
            146,
            8,
            82,
            8,
            210,
            8,
            50,
            8,
            178,
            8,
            114,
            8,
            242,
            8,
            10,
            8,
            138,
            8,
            74,
            8,
            202,
            8,
            42,
            8,
            170,
            8,
            106,
            8,
            234,
            8,
            26,
            8,
            154,
            8,
            90,
            8,
            218,
            8,
            58,
            8,
            186,
            8,
            122,
            8,
            250,
            8,
            6,
            8,
            134,
            8,
            70,
            8,
            198,
            8,
            38,
            8,
            166,
            8,
            102,
            8,
            230,
            8,
            22,
            8,
            150,
            8,
            86,
            8,
            214,
            8,
            54,
            8,
            182,
            8,
            118,
            8,
            246,
            8,
            14,
            8,
            142,
            8,
            78,
            8,
            206,
            8,
            46,
            8,
            174,
            8,
            110,
            8,
            238,
            8,
            30,
            8,
            158,
            8,
            94,
            8,
            222,
            8,
            62,
            8,
            190,
            8,
            126,
            8,
            254,
            8,
            1,
            8,
            129,
            8,
            65,
            8,
            193,
            8,
            33,
            8,
            161,
            8,
            97,
            8,
            225,
            8,
            17,
            8,
            145,
            8,
            81,
            8,
            209,
            8,
            49,
            8,
            177,
            8,
            113,
            8,
            241,
            8,
            9,
            8,
            137,
            8,
            73,
            8,
            201,
            8,
            41,
            8,
            169,
            8,
            105,
            8,
            233,
            8,
            25,
            8,
            153,
            8,
            89,
            8,
            217,
            8,
            57,
            8,
            185,
            8,
            121,
            8,
            249,
            8,
            5,
            8,
            133,
            8,
            69,
            8,
            197,
            8,
            37,
            8,
            165,
            8,
            101,
            8,
            229,
            8,
            21,
            8,
            149,
            8,
            85,
            8,
            213,
            8,
            53,
            8,
            181,
            8,
            117,
            8,
            245,
            8,
            13,
            8,
            141,
            8,
            77,
            8,
            205,
            8,
            45,
            8,
            173,
            8,
            109,
            8,
            237,
            8,
            29,
            8,
            157,
            8,
            93,
            8,
            221,
            8,
            61,
            8,
            189,
            8,
            125,
            8,
            253,
            8,
            19,
            9,
            275,
            9,
            147,
            9,
            403,
            9,
            83,
            9,
            339,
            9,
            211,
            9,
            467,
            9,
            51,
            9,
            307,
            9,
            179,
            9,
            435,
            9,
            115,
            9,
            371,
            9,
            243,
            9,
            499,
            9,
            11,
            9,
            267,
            9,
            139,
            9,
            395,
            9,
            75,
            9,
            331,
            9,
            203,
            9,
            459,
            9,
            43,
            9,
            299,
            9,
            171,
            9,
            427,
            9,
            107,
            9,
            363,
            9,
            235,
            9,
            491,
            9,
            27,
            9,
            283,
            9,
            155,
            9,
            411,
            9,
            91,
            9,
            347,
            9,
            219,
            9,
            475,
            9,
            59,
            9,
            315,
            9,
            187,
            9,
            443,
            9,
            123,
            9,
            379,
            9,
            251,
            9,
            507,
            9,
            7,
            9,
            263,
            9,
            135,
            9,
            391,
            9,
            71,
            9,
            327,
            9,
            199,
            9,
            455,
            9,
            39,
            9,
            295,
            9,
            167,
            9,
            423,
            9,
            103,
            9,
            359,
            9,
            231,
            9,
            487,
            9,
            23,
            9,
            279,
            9,
            151,
            9,
            407,
            9,
            87,
            9,
            343,
            9,
            215,
            9,
            471,
            9,
            55,
            9,
            311,
            9,
            183,
            9,
            439,
            9,
            119,
            9,
            375,
            9,
            247,
            9,
            503,
            9,
            15,
            9,
            271,
            9,
            143,
            9,
            399,
            9,
            79,
            9,
            335,
            9,
            207,
            9,
            463,
            9,
            47,
            9,
            303,
            9,
            175,
            9,
            431,
            9,
            111,
            9,
            367,
            9,
            239,
            9,
            495,
            9,
            31,
            9,
            287,
            9,
            159,
            9,
            415,
            9,
            95,
            9,
            351,
            9,
            223,
            9,
            479,
            9,
            63,
            9,
            319,
            9,
            191,
            9,
            447,
            9,
            127,
            9,
            383,
            9,
            255,
            9,
            511,
            9,
            0,
            7,
            64,
            7,
            32,
            7,
            96,
            7,
            16,
            7,
            80,
            7,
            48,
            7,
            112,
            7,
            8,
            7,
            72,
            7,
            40,
            7,
            104,
            7,
            24,
            7,
            88,
            7,
            56,
            7,
            120,
            7,
            4,
            7,
            68,
            7,
            36,
            7,
            100,
            7,
            20,
            7,
            84,
            7,
            52,
            7,
            116,
            7,
            3,
            8,
            131,
            8,
            67,
            8,
            195,
            8,
            35,
            8,
            163,
            8,
            99,
            8,
            227,
            8
        };

        // Token: 0x04000393 RID: 915
        internal static readonly short[] distTreeCodes = new short[]
        {
            0,
            5,
            16,
            5,
            8,
            5,
            24,
            5,
            4,
            5,
            20,
            5,
            12,
            5,
            28,
            5,
            2,
            5,
            18,
            5,
            10,
            5,
            26,
            5,
            6,
            5,
            22,
            5,
            14,
            5,
            30,
            5,
            1,
            5,
            17,
            5,
            9,
            5,
            25,
            5,
            5,
            5,
            21,
            5,
            13,
            5,
            29,
            5,
            3,
            5,
            19,
            5,
            11,
            5,
            27,
            5,
            7,
            5,
            23,
            5
        };

        // Token: 0x04000394 RID: 916
        internal static readonly StaticTree Literals = new StaticTree(StaticTree.lengthAndLiteralsTreeCodes, Tree.ExtraLengthBits, InternalConstants.LITERALS + 1, InternalConstants.L_CODES, InternalConstants.MAX_BITS);

        // Token: 0x04000395 RID: 917
        internal static readonly StaticTree Distances = new StaticTree(StaticTree.distTreeCodes, Tree.ExtraDistanceBits, 0, InternalConstants.D_CODES, InternalConstants.MAX_BITS);

        // Token: 0x04000396 RID: 918
        internal static readonly StaticTree BitLengths = new StaticTree(null, Tree.extra_blbits, 0, InternalConstants.BL_CODES, InternalConstants.MAX_BL_BITS);

        // Token: 0x04000397 RID: 919
        internal short[] treeCodes;

        // Token: 0x04000398 RID: 920
        internal int[] extraBits;

        // Token: 0x04000399 RID: 921
        internal int extraBase;

        // Token: 0x0400039A RID: 922
        internal int elems;

        // Token: 0x0400039B RID: 923
        internal int maxLength;
    }
}


namespace Ionic.Zlib
{
    // Token: 0x02000060 RID: 96
    internal sealed class Tree
    {
        /// <summary>
        /// Map from a distance to a distance code.
        /// </summary>
        /// <remarks> 
        /// No side effects. _dist_code[256] and _dist_code[257] are never used.
        /// </remarks>
        // Token: 0x06000477 RID: 1143 RVA: 0x00025AD8 File Offset: 0x00023CD8
        internal static int DistanceCode(int dist)
        {
            return (int)((dist < 256) ? Tree._dist_code[dist] : Tree._dist_code[256 + SharedUtils.URShift(dist, 7)]);
        }

        // Token: 0x06000478 RID: 1144 RVA: 0x00025B10 File Offset: 0x00023D10
        internal void gen_bitlen(DeflateManager s)
        {
            short[] tree = this.dyn_tree;
            short[] stree = this.staticTree.treeCodes;
            int[] extra = this.staticTree.extraBits;
            int base_Renamed = this.staticTree.extraBase;
            int max_length = this.staticTree.maxLength;
            int overflow = 0;
            for (int bits = 0; bits <= InternalConstants.MAX_BITS; bits++)
            {
                s.bl_count[bits] = 0;
            }
            tree[s.heap[s.heap_max] * 2 + 1] = 0;
            int h;
            for (h = s.heap_max + 1; h < Tree.HEAP_SIZE; h++)
            {
                int i = s.heap[h];
                int bits = (int)(tree[(int)(tree[i * 2 + 1] * 2 + 1)] + 1);
                if (bits > max_length)
                {
                    bits = max_length;
                    overflow++;
                }
                tree[i * 2 + 1] = (short)bits;
                if (i <= this.max_code)
                {
                    short[] bl_count = s.bl_count;
                    int num = bits;
                    bl_count[num] += 1;
                    int xbits = 0;
                    if (i >= base_Renamed)
                    {
                        xbits = extra[i - base_Renamed];
                    }
                    short f = tree[i * 2];
                    s.opt_len += (int)f * (bits + xbits);
                    if (stree != null)
                    {
                        s.static_len += (int)f * ((int)stree[i * 2 + 1] + xbits);
                    }
                }
            }
            if (overflow != 0)
            {
                do
                {
                    int bits = max_length - 1;
                    while (s.bl_count[bits] == 0)
                    {
                        bits--;
                    }
                    short[] bl_count2 = s.bl_count;
                    int num2 = bits;
                    bl_count2[num2] -= 1;
                    s.bl_count[bits + 1] = (short)(s.bl_count[bits + 1] + 2);
                    short[] bl_count3 = s.bl_count;
                    int num3 = max_length;
                    bl_count3[num3] -= 1;
                    overflow -= 2;
                }
                while (overflow > 0);
                for (int bits = max_length; bits != 0; bits--)
                {
                    int i = (int)s.bl_count[bits];
                    while (i != 0)
                    {
                        int j = s.heap[--h];
                        if (j <= this.max_code)
                        {
                            if ((int)tree[j * 2 + 1] != bits)
                            {
                                s.opt_len = (int)((long)s.opt_len + ((long)bits - (long)tree[j * 2 + 1]) * (long)tree[j * 2]);
                                tree[j * 2 + 1] = (short)bits;
                            }
                            i--;
                        }
                    }
                }
            }
        }

        // Token: 0x06000479 RID: 1145 RVA: 0x00025DD0 File Offset: 0x00023FD0
        internal void build_tree(DeflateManager s)
        {
            short[] tree = this.dyn_tree;
            short[] stree = this.staticTree.treeCodes;
            int elems = this.staticTree.elems;
            int max_code = -1;
            s.heap_len = 0;
            s.heap_max = Tree.HEAP_SIZE;
            for (int i = 0; i < elems; i++)
            {
                if (tree[i * 2] != 0)
                {
                    max_code = (s.heap[++s.heap_len] = i);
                    s.depth[i] = 0;
                }
                else
                {
                    tree[i * 2 + 1] = 0;
                }
            }
            int node;
            while (s.heap_len < 2)
            {
                node = (s.heap[++s.heap_len] = ((max_code < 2) ? (++max_code) : 0));
                tree[node * 2] = 1;
                s.depth[node] = 0;
                s.opt_len--;
                if (stree != null)
                {
                    s.static_len -= (int)stree[node * 2 + 1];
                }
            }
            this.max_code = max_code;
            for (int i = s.heap_len / 2; i >= 1; i--)
            {
                s.pqdownheap(tree, i);
            }
            node = elems;
            do
            {
                int i = s.heap[1];
                s.heap[1] = s.heap[s.heap_len--];
                s.pqdownheap(tree, 1);
                int j = s.heap[1];
                s.heap[--s.heap_max] = i;
                s.heap[--s.heap_max] = j;
                tree[node * 2] = (short)(tree[i * 2] + tree[j * 2]);
                s.depth[node] = (sbyte)(Math.Max((byte)s.depth[i], (byte)s.depth[j]) + 1);
                tree[i * 2 + 1] = (tree[j * 2 + 1] = (short)node);
                s.heap[1] = node++;
                s.pqdownheap(tree, 1);
            }
            while (s.heap_len >= 2);
            s.heap[--s.heap_max] = s.heap[1];
            this.gen_bitlen(s);
            Tree.gen_codes(tree, max_code, s.bl_count);
        }

        // Token: 0x0600047A RID: 1146 RVA: 0x00026048 File Offset: 0x00024248
        internal static void gen_codes(short[] tree, int max_code, short[] bl_count)
        {
            short[] next_code = new short[InternalConstants.MAX_BITS + 1];
            short code = 0;
            for (int bits = 1; bits <= InternalConstants.MAX_BITS; bits++)
            {
                code = (next_code[bits] = (short)(code + bl_count[bits - 1] << 1));
            }
            for (int i = 0; i <= max_code; i++)
            {
                int len = (int)tree[i * 2 + 1];
                if (len != 0)
                {
                    int num = i * 2;
                    short[] array = next_code;
                    int num2 = len;
                    short code2;
                    array[num2] = (short)((code2 = array[num2]) + 1);
                    tree[num] = (short)Tree.bi_reverse((int)code2, len);
                }
            }
        }

        // Token: 0x0600047B RID: 1147 RVA: 0x000260E8 File Offset: 0x000242E8
        internal static int bi_reverse(int code, int len)
        {
            int res = 0;
            do
            {
                res |= (code & 1);
                code >>= 1;
                res <<= 1;
            }
            while (--len > 0);
            return res >> 1;
        }

        // Token: 0x0400035F RID: 863
        internal const int Buf_size = 16;

        // Token: 0x04000360 RID: 864
        private static readonly int HEAP_SIZE = 2 * InternalConstants.L_CODES + 1;

        // Token: 0x04000361 RID: 865
        internal static readonly int[] ExtraLengthBits = new int[]
        {
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            1,
            1,
            2,
            2,
            2,
            2,
            3,
            3,
            3,
            3,
            4,
            4,
            4,
            4,
            5,
            5,
            5,
            5,
            0
        };

        // Token: 0x04000362 RID: 866
        internal static readonly int[] ExtraDistanceBits = new int[]
        {
            0,
            0,
            0,
            0,
            1,
            1,
            2,
            2,
            3,
            3,
            4,
            4,
            5,
            5,
            6,
            6,
            7,
            7,
            8,
            8,
            9,
            9,
            10,
            10,
            11,
            11,
            12,
            12,
            13,
            13
        };

        // Token: 0x04000363 RID: 867
        internal static readonly int[] extra_blbits = new int[]
        {
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            2,
            3,
            7
        };

        // Token: 0x04000364 RID: 868
        internal static readonly sbyte[] bl_order = new sbyte[]
        {
            16,
            17,
            18,
            0,
            8,
            7,
            9,
            6,
            10,
            5,
            11,
            4,
            12,
            3,
            13,
            2,
            14,
            1,
            15
        };

        // Token: 0x04000365 RID: 869
        private static readonly sbyte[] _dist_code = new sbyte[]
        {
            0,
            1,
            2,
            3,
            4,
            4,
            5,
            5,
            6,
            6,
            6,
            6,
            7,
            7,
            7,
            7,
            8,
            8,
            8,
            8,
            8,
            8,
            8,
            8,
            9,
            9,
            9,
            9,
            9,
            9,
            9,
            9,
            10,
            10,
            10,
            10,
            10,
            10,
            10,
            10,
            10,
            10,
            10,
            10,
            10,
            10,
            10,
            10,
            11,
            11,
            11,
            11,
            11,
            11,
            11,
            11,
            11,
            11,
            11,
            11,
            11,
            11,
            11,
            11,
            12,
            12,
            12,
            12,
            12,
            12,
            12,
            12,
            12,
            12,
            12,
            12,
            12,
            12,
            12,
            12,
            12,
            12,
            12,
            12,
            12,
            12,
            12,
            12,
            12,
            12,
            12,
            12,
            12,
            12,
            12,
            12,
            13,
            13,
            13,
            13,
            13,
            13,
            13,
            13,
            13,
            13,
            13,
            13,
            13,
            13,
            13,
            13,
            13,
            13,
            13,
            13,
            13,
            13,
            13,
            13,
            13,
            13,
            13,
            13,
            13,
            13,
            13,
            13,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            14,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            0,
            0,
            16,
            17,
            18,
            18,
            19,
            19,
            20,
            20,
            20,
            20,
            21,
            21,
            21,
            21,
            22,
            22,
            22,
            22,
            22,
            22,
            22,
            22,
            23,
            23,
            23,
            23,
            23,
            23,
            23,
            23,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            28,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29,
            29
        };

        // Token: 0x04000366 RID: 870
        internal static readonly sbyte[] LengthCode = new sbyte[]
        {
            0,
            1,
            2,
            3,
            4,
            5,
            6,
            7,
            8,
            8,
            9,
            9,
            10,
            10,
            11,
            11,
            12,
            12,
            12,
            12,
            13,
            13,
            13,
            13,
            14,
            14,
            14,
            14,
            15,
            15,
            15,
            15,
            16,
            16,
            16,
            16,
            16,
            16,
            16,
            16,
            17,
            17,
            17,
            17,
            17,
            17,
            17,
            17,
            18,
            18,
            18,
            18,
            18,
            18,
            18,
            18,
            19,
            19,
            19,
            19,
            19,
            19,
            19,
            19,
            20,
            20,
            20,
            20,
            20,
            20,
            20,
            20,
            20,
            20,
            20,
            20,
            20,
            20,
            20,
            20,
            21,
            21,
            21,
            21,
            21,
            21,
            21,
            21,
            21,
            21,
            21,
            21,
            21,
            21,
            21,
            21,
            22,
            22,
            22,
            22,
            22,
            22,
            22,
            22,
            22,
            22,
            22,
            22,
            22,
            22,
            22,
            22,
            23,
            23,
            23,
            23,
            23,
            23,
            23,
            23,
            23,
            23,
            23,
            23,
            23,
            23,
            23,
            23,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            24,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            25,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            26,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            27,
            28
        };

        // Token: 0x04000367 RID: 871
        internal static readonly int[] LengthBase = new int[]
        {
            0,
            1,
            2,
            3,
            4,
            5,
            6,
            7,
            8,
            10,
            12,
            14,
            16,
            20,
            24,
            28,
            32,
            40,
            48,
            56,
            64,
            80,
            96,
            112,
            128,
            160,
            192,
            224,
            0
        };

        // Token: 0x04000368 RID: 872
        internal static readonly int[] DistanceBase = new int[]
        {
            0,
            1,
            2,
            3,
            4,
            6,
            8,
            12,
            16,
            24,
            32,
            48,
            64,
            96,
            128,
            192,
            256,
            384,
            512,
            768,
            1024,
            1536,
            2048,
            3072,
            4096,
            6144,
            8192,
            12288,
            16384,
            24576
        };

        // Token: 0x04000369 RID: 873
        internal short[] dyn_tree;

        // Token: 0x0400036A RID: 874
        internal int max_code;

        // Token: 0x0400036B RID: 875
        internal StaticTree staticTree;
    }
}


namespace Ionic.Zlib
{
    // Token: 0x0200005D RID: 93
    internal class WorkItem
    {
        // Token: 0x06000452 RID: 1106 RVA: 0x00024E30 File Offset: 0x00023030
        public WorkItem(int size, CompressionLevel compressLevel, CompressionStrategy strategy, int ix)
        {
            this.buffer = new byte[size];
            int i = size + (size / 32768 + 1) * 5 * 2;
            this.compressed = new byte[i];
            this.compressor = new ZlibCodec();
            this.compressor.InitializeDeflate(compressLevel, false);
            this.compressor.OutputBuffer = this.compressed;
            this.compressor.InputBuffer = this.buffer;
            this.index = ix;
        }

        // Token: 0x04000328 RID: 808
        public byte[] buffer;

        // Token: 0x04000329 RID: 809
        public byte[] compressed;

        // Token: 0x0400032A RID: 810
        public int crc;

        // Token: 0x0400032B RID: 811
        public int index;

        // Token: 0x0400032C RID: 812
        public int ordinal;

        // Token: 0x0400032D RID: 813
        public int inputBytesAvailable;

        // Token: 0x0400032E RID: 814
        public int compressedBytesAvailable;

        // Token: 0x0400032F RID: 815
        public ZlibCodec compressor;
    }
}


namespace Ionic.Zlib
{
    // Token: 0x0200006B RID: 107
    internal class ZlibBaseStream : Stream
    {
        // Token: 0x1700011C RID: 284
        // (get) Token: 0x0600048B RID: 1163 RVA: 0x00027024 File Offset: 0x00025224
        internal int Crc32
        {
            get
            {
                int result;
                if (this.crc == null)
                {
                    result = 0;
                }
                else
                {
                    result = this.crc.Crc32Result;
                }
                return result;
            }
        }

        // Token: 0x0600048C RID: 1164 RVA: 0x00027058 File Offset: 0x00025258
        public ZlibBaseStream(Stream stream, CompressionMode compressionMode, CompressionLevel level, ZlibStreamFlavor flavor, bool leaveOpen)
        {
            this._flushMode = FlushType.None;
            this._stream = stream;
            this._leaveOpen = leaveOpen;
            this._compressionMode = compressionMode;
            this._flavor = flavor;
            this._level = level;
            if (flavor == ZlibStreamFlavor.GZIP)
            {
                this.crc = new CRC32();
            }
        }

        // Token: 0x1700011D RID: 285
        // (get) Token: 0x0600048D RID: 1165 RVA: 0x000270EC File Offset: 0x000252EC
        protected internal bool _wantCompress
        {
            get
            {
                return this._compressionMode == CompressionMode.Compress;
            }
        }

        // Token: 0x1700011E RID: 286
        // (get) Token: 0x0600048E RID: 1166 RVA: 0x00027108 File Offset: 0x00025308
        private ZlibCodec z
        {
            get
            {
                if (this._z == null)
                {
                    bool wantRfc1950Header = this._flavor == ZlibStreamFlavor.ZLIB;
                    this._z = new ZlibCodec();
                    if (this._compressionMode == CompressionMode.Decompress)
                    {
                        this._z.InitializeInflate(wantRfc1950Header);
                    }
                    else
                    {
                        this._z.Strategy = this.Strategy;
                        this._z.InitializeDeflate(this._level, wantRfc1950Header);
                    }
                }
                return this._z;
            }
        }

        // Token: 0x1700011F RID: 287
        // (get) Token: 0x0600048F RID: 1167 RVA: 0x00027194 File Offset: 0x00025394
        private byte[] workingBuffer
        {
            get
            {
                if (this._workingBuffer == null)
                {
                    this._workingBuffer = new byte[this._bufferSize];
                }
                return this._workingBuffer;
            }
        }

        // Token: 0x06000490 RID: 1168 RVA: 0x000271D0 File Offset: 0x000253D0
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (this.crc != null)
            {
                this.crc.SlurpBlock(buffer, offset, count);
            }
            if (this._streamMode == ZlibBaseStream.StreamMode.Undefined)
            {
                this._streamMode = ZlibBaseStream.StreamMode.Writer;
            }
            else if (this._streamMode != ZlibBaseStream.StreamMode.Writer)
            {
                throw new ZlibException("Cannot Write after Reading.");
            }
            if (count != 0)
            {
                this.z.InputBuffer = buffer;
                this._z.NextIn = offset;
                this._z.AvailableBytesIn = count;
                for (; ; )
                {
                    this._z.OutputBuffer = this.workingBuffer;
                    this._z.NextOut = 0;
                    this._z.AvailableBytesOut = this._workingBuffer.Length;
                    int rc = this._wantCompress ? this._z.Deflate(this._flushMode) : this._z.Inflate(this._flushMode);
                    if (rc != 0 && rc != 1)
                    {
                        break;
                    }
                    this._stream.Write(this._workingBuffer, 0, this._workingBuffer.Length - this._z.AvailableBytesOut);
                    bool done = this._z.AvailableBytesIn == 0 && this._z.AvailableBytesOut != 0;
                    if (this._flavor == ZlibStreamFlavor.GZIP && !this._wantCompress)
                    {
                        done = (this._z.AvailableBytesIn == 8 && this._z.AvailableBytesOut != 0);
                    }
                    if (done)
                    {
                        return;
                    }
                }
                throw new ZlibException((this._wantCompress ? "de" : "in") + "flating: " + this._z.Message);
            }
        }

        // Token: 0x06000491 RID: 1169 RVA: 0x00027390 File Offset: 0x00025590
        private void finish()
        {
            unchecked
            {
                if (this._z != null)
                {
                    if (this._streamMode == ZlibBaseStream.StreamMode.Writer)
                    {
                        int rc;
                        for (; ; )
                        {
                            this._z.OutputBuffer = this.workingBuffer;
                            this._z.NextOut = 0;
                            this._z.AvailableBytesOut = this._workingBuffer.Length;
                            rc = (this._wantCompress ? this._z.Deflate(FlushType.Finish) : this._z.Inflate(FlushType.Finish));
                            if (rc != 1 && rc != 0)
                            {
                                break;
                            }
                            if (this._workingBuffer.Length - this._z.AvailableBytesOut > 0)
                            {
                                this._stream.Write(this._workingBuffer, 0, this._workingBuffer.Length - this._z.AvailableBytesOut);
                            }
                            bool done = this._z.AvailableBytesIn == 0 && this._z.AvailableBytesOut != 0;
                            if (this._flavor == ZlibStreamFlavor.GZIP && !this._wantCompress)
                            {
                                done = (this._z.AvailableBytesIn == 8 && this._z.AvailableBytesOut != 0);
                            }
                            if (done)
                            {
                                goto Block_12;
                            }
                        }
                        string verb = (this._wantCompress ? "de" : "in") + "flating";
                        if (this._z.Message == null)
                        {
                            throw new ZlibException(string.Format("{0}: (rc = {1})", verb, rc));
                        }
                        throw new ZlibException(verb + ": " + this._z.Message);
                        Block_12:
                        this.Flush();
                        if (this._flavor == ZlibStreamFlavor.GZIP)
                        {
                            if (!this._wantCompress)
                            {
                                throw new ZlibException("Writing with decompression is not supported.");
                            }
                            int c = this.crc.Crc32Result;
                            this._stream.Write(BitConverter.GetBytes(c), 0, 4);
                            int c2 = (int)(this.crc.TotalBytesRead & (long)((ulong)-1));
                            this._stream.Write(BitConverter.GetBytes(c2), 0, 4);
                        }
                    }
                    else if (this._streamMode == ZlibBaseStream.StreamMode.Reader)
                    {
                        if (this._flavor == ZlibStreamFlavor.GZIP)
                        {
                            if (this._wantCompress)
                            {
                                throw new ZlibException("Reading with compression is not supported.");
                            }
                            if (this._z.TotalBytesOut != 0L)
                            {
                                byte[] trailer = new byte[8];
                                if (this._z.AvailableBytesIn < 8)
                                {
                                    Array.Copy(this._z.InputBuffer, this._z.NextIn, trailer, 0, this._z.AvailableBytesIn);
                                    int bytesNeeded = 8 - this._z.AvailableBytesIn;
                                    int bytesRead = this._stream.Read(trailer, this._z.AvailableBytesIn, bytesNeeded);
                                    if (bytesNeeded != bytesRead)
                                    {
                                        throw new ZlibException(string.Format("Missing or incomplete GZIP trailer. Expected 8 bytes, got {0}.", this._z.AvailableBytesIn + bytesRead));
                                    }
                                }
                                else
                                {
                                    Array.Copy(this._z.InputBuffer, this._z.NextIn, trailer, 0, trailer.Length);
                                }
                                int crc32_expected = BitConverter.ToInt32(trailer, 0);
                                int crc32_actual = this.crc.Crc32Result;
                                int isize_expected = BitConverter.ToInt32(trailer, 4);
                                int isize_actual = (int)(this._z.TotalBytesOut & (long)((ulong)-1));
                                if (crc32_actual != crc32_expected)
                                {
                                    throw new ZlibException(string.Format("Bad CRC32 in GZIP trailer. (actual({0:X8})!=expected({1:X8}))", crc32_actual, crc32_expected));
                                }
                                if (isize_actual != isize_expected)
                                {
                                    throw new ZlibException(string.Format("Bad size in GZIP trailer. (actual({0})!=expected({1}))", isize_actual, isize_expected));
                                }
                            }
                        }
                    }
                }
            }
        }

        // Token: 0x06000492 RID: 1170 RVA: 0x000277A4 File Offset: 0x000259A4
        private void end()
        {
            if (this.z != null)
            {
                if (this._wantCompress)
                {
                    this._z.EndDeflate();
                }
                else
                {
                    this._z.EndInflate();
                }
                this._z = null;
            }
        }

        // Token: 0x06000493 RID: 1171 RVA: 0x000277F8 File Offset: 0x000259F8
        public override void Close()
        {
            if (this._stream != null)
            {
                try
                {
                    this.finish();
                }
                finally
                {
                    this.end();
                    if (!this._leaveOpen)
                    {
                        this._stream.Close();
                    }
                    this._stream = null;
                }
            }
        }

        // Token: 0x06000494 RID: 1172 RVA: 0x0002785C File Offset: 0x00025A5C
        public override void Flush()
        {
            this._stream.Flush();
        }

        // Token: 0x06000495 RID: 1173 RVA: 0x0002786B File Offset: 0x00025A6B
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        // Token: 0x06000496 RID: 1174 RVA: 0x00027873 File Offset: 0x00025A73
        public override void SetLength(long value)
        {
            this._stream.SetLength(value);
        }

        // Token: 0x06000497 RID: 1175 RVA: 0x00027884 File Offset: 0x00025A84
        private string ReadZeroTerminatedString()
        {
            List<byte> list = new List<byte>();
            bool done = false;
            for (; ; )
            {
                int i = this._stream.Read(this._buf1, 0, 1);
                if (i != 1)
                {
                    break;
                }
                if (this._buf1[0] == 0)
                {
                    done = true;
                }
                else
                {
                    list.Add(this._buf1[0]);
                }
                if (done)
                {
                    goto Block_3;
                }
            }
            throw new ZlibException("Unexpected EOF reading GZIP header.");
            Block_3:
            byte[] a = list.ToArray();
            return GZipStream.iso8859dash1.GetString(a, 0, a.Length);
        }

        // Token: 0x06000498 RID: 1176 RVA: 0x00027914 File Offset: 0x00025B14
        private int _ReadAndValidateGzipHeader()
        {
            int totalBytesRead = 0;
            byte[] header = new byte[10];
            int i = this._stream.Read(header, 0, header.Length);
            int result;
            if (i == 0)
            {
                result = 0;
            }
            else
            {
                if (i != 10)
                {
                    throw new ZlibException("Not a valid GZIP stream.");
                }
                if (header[0] != 31 || header[1] != 139 || header[2] != 8)
                {
                    throw new ZlibException("Bad GZIP header.");
                }
                int timet = BitConverter.ToInt32(header, 4);
                this._GzipMtime = GZipStream._unixEpoch.AddSeconds((double)timet);
                totalBytesRead += i;
                if ((header[3] & 4) == 4)
                {
                    i = this._stream.Read(header, 0, 2);
                    totalBytesRead += i;
                    short extraLength = (short)((int)header[0] + (int)header[1] * 256);
                    byte[] extra = new byte[(int)extraLength];
                    i = this._stream.Read(extra, 0, extra.Length);
                    if (i != (int)extraLength)
                    {
                        throw new ZlibException("Unexpected end-of-file reading GZIP header.");
                    }
                    totalBytesRead += i;
                }
                if ((header[3] & 8) == 8)
                {
                    this._GzipFileName = this.ReadZeroTerminatedString();
                }
                if ((header[3] & 16) == 16)
                {
                    this._GzipComment = this.ReadZeroTerminatedString();
                }
                if ((header[3] & 2) == 2)
                {
                    this.Read(this._buf1, 0, 1);
                }
                result = totalBytesRead;
            }
            return result;
        }

        // Token: 0x06000499 RID: 1177 RVA: 0x00027A84 File Offset: 0x00025C84
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (this._streamMode == ZlibBaseStream.StreamMode.Undefined)
            {
                if (!this._stream.CanRead)
                {
                    throw new ZlibException("The stream is not readable.");
                }
                this._streamMode = ZlibBaseStream.StreamMode.Reader;
                this.z.AvailableBytesIn = 0;
                if (this._flavor == ZlibStreamFlavor.GZIP)
                {
                    this._gzipHeaderByteCount = this._ReadAndValidateGzipHeader();
                    if (this._gzipHeaderByteCount == 0)
                    {
                        return 0;
                    }
                }
            }
            if (this._streamMode != ZlibBaseStream.StreamMode.Reader)
            {
                throw new ZlibException("Cannot Read after Writing.");
            }
            int result;
            if (count == 0)
            {
                result = 0;
            }
            else if (this.nomoreinput && this._wantCompress)
            {
                result = 0;
            }
            else
            {
                if (buffer == null)
                {
                    throw new ArgumentNullException("buffer");
                }
                if (count < 0)
                {
                    throw new ArgumentOutOfRangeException("count");
                }
                if (offset < buffer.GetLowerBound(0))
                {
                    throw new ArgumentOutOfRangeException("offset");
                }
                if (offset + count > buffer.GetLength(0))
                {
                    throw new ArgumentOutOfRangeException("count");
                }
                this._z.OutputBuffer = buffer;
                this._z.NextOut = offset;
                this._z.AvailableBytesOut = count;
                this._z.InputBuffer = this.workingBuffer;
                int rc;
                for (; ; )
                {
                    if (this._z.AvailableBytesIn == 0 && !this.nomoreinput)
                    {
                        this._z.NextIn = 0;
                        this._z.AvailableBytesIn = this._stream.Read(this._workingBuffer, 0, this._workingBuffer.Length);
                        if (this._z.AvailableBytesIn == 0)
                        {
                            this.nomoreinput = true;
                        }
                    }
                    rc = (this._wantCompress ? this._z.Deflate(this._flushMode) : this._z.Inflate(this._flushMode));
                    if (this.nomoreinput && rc == -5)
                    {
                        break;
                    }
                    if (rc != 0 && rc != 1)
                    {
                        goto Block_20;
                    }
                    if ((this.nomoreinput || rc == 1) && this._z.AvailableBytesOut == count)
                    {
                        goto Block_23;
                    }
                    if (this._z.AvailableBytesOut <= 0 || this.nomoreinput || rc != 0)
                    {
                        goto IL_2A2;
                    }
                }
                return 0;
                Block_20:
                throw new ZlibException(string.Format("{0}flating:  rc={1}  msg={2}", this._wantCompress ? "de" : "in", rc, this._z.Message));
                Block_23:
                IL_2A2:
                if (this._z.AvailableBytesOut > 0)
                {
                    if (rc == 0 && this._z.AvailableBytesIn == 0)
                    {
                    }
                    if (this.nomoreinput)
                    {
                        if (this._wantCompress)
                        {
                            rc = this._z.Deflate(FlushType.Finish);
                            if (rc != 0 && rc != 1)
                            {
                                throw new ZlibException(string.Format("Deflating:  rc={0}  msg={1}", rc, this._z.Message));
                            }
                        }
                    }
                }
                rc = count - this._z.AvailableBytesOut;
                if (this.crc != null)
                {
                    this.crc.SlurpBlock(buffer, offset, rc);
                }
                result = rc;
            }
            return result;
        }

        // Token: 0x17000120 RID: 288
        // (get) Token: 0x0600049A RID: 1178 RVA: 0x00027DF0 File Offset: 0x00025FF0
        public override bool CanRead
        {
            get
            {
                return this._stream.CanRead;
            }
        }

        // Token: 0x17000121 RID: 289
        // (get) Token: 0x0600049B RID: 1179 RVA: 0x00027E10 File Offset: 0x00026010
        public override bool CanSeek
        {
            get
            {
                return this._stream.CanSeek;
            }
        }

        // Token: 0x17000122 RID: 290
        // (get) Token: 0x0600049C RID: 1180 RVA: 0x00027E30 File Offset: 0x00026030
        public override bool CanWrite
        {
            get
            {
                return this._stream.CanWrite;
            }
        }

        // Token: 0x17000123 RID: 291
        // (get) Token: 0x0600049D RID: 1181 RVA: 0x00027E50 File Offset: 0x00026050
        public override long Length
        {
            get
            {
                return this._stream.Length;
            }
        }

        // Token: 0x17000124 RID: 292
        // (get) Token: 0x0600049E RID: 1182 RVA: 0x00027E6D File Offset: 0x0002606D
        // (set) Token: 0x0600049F RID: 1183 RVA: 0x00027E75 File Offset: 0x00026075
        public override long Position
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        // Token: 0x060004A0 RID: 1184 RVA: 0x00027E80 File Offset: 0x00026080
        public static void CompressString(string s, Stream compressor)
        {
            byte[] uncompressed = Encoding.UTF8.GetBytes(s);
            try
            {
                compressor.Write(uncompressed, 0, uncompressed.Length);
            }
            finally
            {
                if (compressor != null)
                {
                    ((IDisposable)compressor).Dispose();
                }
            }
        }

        // Token: 0x060004A1 RID: 1185 RVA: 0x00027ED0 File Offset: 0x000260D0
        public static void CompressBuffer(byte[] b, Stream compressor)
        {
            try
            {
                compressor.Write(b, 0, b.Length);
            }
            finally
            {
                if (compressor != null)
                {
                    ((IDisposable)compressor).Dispose();
                }
            }
        }

        // Token: 0x060004A2 RID: 1186 RVA: 0x00027F14 File Offset: 0x00026114
        public static string UncompressString(byte[] compressed, Stream decompressor)
        {
            byte[] working = new byte[1024];
            Encoding encoding = Encoding.UTF8;
            string result;
            using (MemoryStream output = new MemoryStream())
            {
                try
                {
                    int i;
                    while ((i = decompressor.Read(working, 0, working.Length)) != 0)
                    {
                        output.Write(working, 0, i);
                    }
                }
                finally
                {
                    if (decompressor != null)
                    {
                        ((IDisposable)decompressor).Dispose();
                    }
                }
                output.Seek(0L, SeekOrigin.Begin);
                StreamReader sr = new StreamReader(output, encoding);
                result = sr.ReadToEnd();
            }
            return result;
        }

        // Token: 0x060004A3 RID: 1187 RVA: 0x00027FCC File Offset: 0x000261CC
        public static byte[] UncompressBuffer(byte[] compressed, Stream decompressor)
        {
            byte[] working = new byte[1024];
            byte[] result;
            using (MemoryStream output = new MemoryStream())
            {
                try
                {
                    int i;
                    while ((i = decompressor.Read(working, 0, working.Length)) != 0)
                    {
                        output.Write(working, 0, i);
                    }
                }
                finally
                {
                    if (decompressor != null)
                    {
                        ((IDisposable)decompressor).Dispose();
                    }
                }
                result = output.ToArray();
            }
            return result;
        }

        // Token: 0x040003A2 RID: 930
        protected internal ZlibCodec _z = null;

        // Token: 0x040003A3 RID: 931
        protected internal ZlibBaseStream.StreamMode _streamMode = ZlibBaseStream.StreamMode.Undefined;

        // Token: 0x040003A4 RID: 932
        protected internal FlushType _flushMode;

        // Token: 0x040003A5 RID: 933
        protected internal ZlibStreamFlavor _flavor;

        // Token: 0x040003A6 RID: 934
        protected internal CompressionMode _compressionMode;

        // Token: 0x040003A7 RID: 935
        protected internal CompressionLevel _level;

        // Token: 0x040003A8 RID: 936
        protected internal bool _leaveOpen;

        // Token: 0x040003A9 RID: 937
        protected internal byte[] _workingBuffer;

        // Token: 0x040003AA RID: 938
        protected internal int _bufferSize = 16384;

        // Token: 0x040003AB RID: 939
        protected internal byte[] _buf1 = new byte[1];

        // Token: 0x040003AC RID: 940
        protected internal Stream _stream;

        // Token: 0x040003AD RID: 941
        protected internal CompressionStrategy Strategy = CompressionStrategy.Default;

        // Token: 0x040003AE RID: 942
        private CRC32 crc;

        // Token: 0x040003AF RID: 943
        protected internal string _GzipFileName;

        // Token: 0x040003B0 RID: 944
        protected internal string _GzipComment;

        // Token: 0x040003B1 RID: 945
        protected internal DateTime _GzipMtime;

        // Token: 0x040003B2 RID: 946
        protected internal int _gzipHeaderByteCount;

        // Token: 0x040003B3 RID: 947
        private bool nomoreinput = false;

        // Token: 0x0200006C RID: 108
        internal enum StreamMode
        {
            // Token: 0x040003B5 RID: 949
            Writer,
            // Token: 0x040003B6 RID: 950
            Reader,
            // Token: 0x040003B7 RID: 951
            Undefined
        }
    }
}


namespace Ionic.Zlib
{
    /// <summary>
    /// Encoder and Decoder for ZLIB and DEFLATE (IETF RFC1950 and RFC1951).
    /// </summary>
    ///
    /// <remarks>
    /// This class compresses and decompresses data according to the Deflate algorithm
    /// and optionally, the ZLIB format, as documented in <see href="http://www.ietf.org/rfc/rfc1950.txt">RFC 1950 - ZLIB</see> and <see href="http://www.ietf.org/rfc/rfc1951.txt">RFC 1951 - DEFLATE</see>.
    /// </remarks>
    // Token: 0x0200006D RID: 109
    [ClassInterface(ClassInterfaceType.AutoDispatch)]
    [Guid("ebc25cf6-9120-4283-b972-0e5520d0000D")]
    [ComVisible(true)]
    public sealed class ZlibCodec
    {
        /// <summary>
        /// The Adler32 checksum on the data transferred through the codec so far. You probably don't need to look at this.
        /// </summary>
        // Token: 0x17000125 RID: 293
        // (get) Token: 0x060004A4 RID: 1188 RVA: 0x00028068 File Offset: 0x00026268
        public int Adler32
        {
            get
            {
                return (int)this._Adler32;
            }
        }

        /// <summary>
        /// Create a ZlibCodec.
        /// </summary>
        /// <remarks>
        /// If you use this default constructor, you will later have to explicitly call 
        /// InitializeInflate() or InitializeDeflate() before using the ZlibCodec to compress 
        /// or decompress. 
        /// </remarks>
        // Token: 0x060004A5 RID: 1189 RVA: 0x00028080 File Offset: 0x00026280
        public ZlibCodec()
        {
        }

        /// <summary>
        /// Create a ZlibCodec that either compresses or decompresses.
        /// </summary>
        /// <param name="mode">
        /// Indicates whether the codec should compress (deflate) or decompress (inflate).
        /// </param>
        // Token: 0x060004A6 RID: 1190 RVA: 0x000280A4 File Offset: 0x000262A4
        public ZlibCodec(CompressionMode mode)
        {
            if (mode == CompressionMode.Compress)
            {
                int rc = this.InitializeDeflate();
                if (rc != 0)
                {
                    throw new ZlibException("Cannot initialize for deflate.");
                }
            }
            else
            {
                if (mode != CompressionMode.Decompress)
                {
                    throw new ZlibException("Invalid ZlibStreamFlavor.");
                }
                int rc = this.InitializeInflate();
                if (rc != 0)
                {
                    throw new ZlibException("Cannot initialize for inflate.");
                }
            }
        }

        /// <summary>
        /// Initialize the inflation state. 
        /// </summary>
        /// <remarks>
        /// It is not necessary to call this before using the ZlibCodec to inflate data; 
        /// It is implicitly called when you call the constructor.
        /// </remarks>
        /// <returns>Z_OK if everything goes well.</returns>
        // Token: 0x060004A7 RID: 1191 RVA: 0x00028130 File Offset: 0x00026330
        public int InitializeInflate()
        {
            return this.InitializeInflate(this.WindowBits);
        }

        /// <summary>
        /// Initialize the inflation state with an explicit flag to
        /// govern the handling of RFC1950 header bytes.
        /// </summary>
        ///
        /// <remarks>
        /// By default, the ZLIB header defined in <see href="http://www.ietf.org/rfc/rfc1950.txt">RFC 1950</see> is expected.  If
        /// you want to read a zlib stream you should specify true for
        /// expectRfc1950Header.  If you have a deflate stream, you will want to specify
        /// false. It is only necessary to invoke this initializer explicitly if you
        /// want to specify false.
        /// </remarks>
        ///
        /// <param name="expectRfc1950Header">whether to expect an RFC1950 header byte
        /// pair when reading the stream of data to be inflated.</param>
        ///
        /// <returns>Z_OK if everything goes well.</returns>
        // Token: 0x060004A8 RID: 1192 RVA: 0x00028150 File Offset: 0x00026350
        public int InitializeInflate(bool expectRfc1950Header)
        {
            return this.InitializeInflate(this.WindowBits, expectRfc1950Header);
        }

        /// <summary>
        /// Initialize the ZlibCodec for inflation, with the specified number of window bits. 
        /// </summary>
        /// <param name="windowBits">The number of window bits to use. If you need to ask what that is, 
        /// then you shouldn't be calling this initializer.</param>
        /// <returns>Z_OK if all goes well.</returns>
        // Token: 0x060004A9 RID: 1193 RVA: 0x00028170 File Offset: 0x00026370
        public int InitializeInflate(int windowBits)
        {
            this.WindowBits = windowBits;
            return this.InitializeInflate(windowBits, true);
        }

        /// <summary>
        /// Initialize the inflation state with an explicit flag to govern the handling of
        /// RFC1950 header bytes. 
        /// </summary>
        ///
        /// <remarks>
        /// If you want to read a zlib stream you should specify true for
        /// expectRfc1950Header. In this case, the library will expect to find a ZLIB
        /// header, as defined in <see href="http://www.ietf.org/rfc/rfc1950.txt">RFC
        /// 1950</see>, in the compressed stream.  If you will be reading a DEFLATE or
        /// GZIP stream, which does not have such a header, you will want to specify
        /// false.
        /// </remarks>
        ///
        /// <param name="expectRfc1950Header">whether to expect an RFC1950 header byte pair when reading 
        /// the stream of data to be inflated.</param>
        /// <param name="windowBits">The number of window bits to use. If you need to ask what that is, 
        /// then you shouldn't be calling this initializer.</param>
        /// <returns>Z_OK if everything goes well.</returns>
        // Token: 0x060004AA RID: 1194 RVA: 0x00028194 File Offset: 0x00026394
        public int InitializeInflate(int windowBits, bool expectRfc1950Header)
        {
            this.WindowBits = windowBits;
            if (this.dstate != null)
            {
                throw new ZlibException("You may not call InitializeInflate() after calling InitializeDeflate().");
            }
            this.istate = new InflateManager(expectRfc1950Header);
            return this.istate.Initialize(this, windowBits);
        }

        /// <summary>
        /// Inflate the data in the InputBuffer, placing the result in the OutputBuffer.
        /// </summary>
        /// <remarks>
        /// You must have set InputBuffer and OutputBuffer, NextIn and NextOut, and AvailableBytesIn and 
        /// AvailableBytesOut  before calling this method.
        /// </remarks>
        /// <example>
        /// <code>
        /// private void InflateBuffer()
        /// {
        ///     int bufferSize = 1024;
        ///     byte[] buffer = new byte[bufferSize];
        ///     ZlibCodec decompressor = new ZlibCodec();
        ///
        ///     Console.WriteLine("\n============================================");
        ///     Console.WriteLine("Size of Buffer to Inflate: {0} bytes.", CompressedBytes.Length);
        ///     MemoryStream ms = new MemoryStream(DecompressedBytes);
        ///
        ///     int rc = decompressor.InitializeInflate();
        ///
        ///     decompressor.InputBuffer = CompressedBytes;
        ///     decompressor.NextIn = 0;
        ///     decompressor.AvailableBytesIn = CompressedBytes.Length;
        ///
        ///     decompressor.OutputBuffer = buffer;
        ///
        ///     // pass 1: inflate 
        ///     do
        ///     {
        ///         decompressor.NextOut = 0;
        ///         decompressor.AvailableBytesOut = buffer.Length;
        ///         rc = decompressor.Inflate(FlushType.None);
        ///
        ///         if (rc != ZlibConstants.Z_OK &amp;&amp; rc != ZlibConstants.Z_STREAM_END)
        ///             throw new Exception("inflating: " + decompressor.Message);
        ///
        ///         ms.Write(decompressor.OutputBuffer, 0, buffer.Length - decompressor.AvailableBytesOut);
        ///     }
        ///     while (decompressor.AvailableBytesIn &gt; 0 || decompressor.AvailableBytesOut == 0);
        ///
        ///     // pass 2: finish and flush
        ///     do
        ///     {
        ///         decompressor.NextOut = 0;
        ///         decompressor.AvailableBytesOut = buffer.Length;
        ///         rc = decompressor.Inflate(FlushType.Finish);
        ///
        ///         if (rc != ZlibConstants.Z_STREAM_END &amp;&amp; rc != ZlibConstants.Z_OK)
        ///             throw new Exception("inflating: " + decompressor.Message);
        ///
        ///         if (buffer.Length - decompressor.AvailableBytesOut &gt; 0)
        ///             ms.Write(buffer, 0, buffer.Length - decompressor.AvailableBytesOut);
        ///     }
        ///     while (decompressor.AvailableBytesIn &gt; 0 || decompressor.AvailableBytesOut == 0);
        ///
        ///     decompressor.EndInflate();
        /// }
        ///
        /// </code>
        /// </example>
        /// <param name="flush">The flush to use when inflating.</param>
        /// <returns>Z_OK if everything goes well.</returns>
        // Token: 0x060004AB RID: 1195 RVA: 0x000281E0 File Offset: 0x000263E0
        public int Inflate(FlushType flush)
        {
            if (this.istate == null)
            {
                throw new ZlibException("No Inflate State!");
            }
            return this.istate.Inflate(flush);
        }

        /// <summary>
        /// Ends an inflation session. 
        /// </summary>
        /// <remarks>
        /// Call this after successively calling Inflate().  This will cause all buffers to be flushed. 
        /// After calling this you cannot call Inflate() without a intervening call to one of the
        /// InitializeInflate() overloads.
        /// </remarks>
        /// <returns>Z_OK if everything goes well.</returns>
        // Token: 0x060004AC RID: 1196 RVA: 0x0002821C File Offset: 0x0002641C
        public int EndInflate()
        {
            if (this.istate == null)
            {
                throw new ZlibException("No Inflate State!");
            }
            int ret = this.istate.End();
            this.istate = null;
            return ret;
        }

        /// <summary>
        /// I don't know what this does!
        /// </summary>
        /// <returns>Z_OK if everything goes well.</returns>
        // Token: 0x060004AD RID: 1197 RVA: 0x00028260 File Offset: 0x00026460
        public int SyncInflate()
        {
            if (this.istate == null)
            {
                throw new ZlibException("No Inflate State!");
            }
            return this.istate.Sync();
        }

        /// <summary>
        /// Initialize the ZlibCodec for deflation operation.
        /// </summary>
        /// <remarks>
        /// The codec will use the MAX window bits and the default level of compression.
        /// </remarks>
        /// <example>
        /// <code>
        ///  int bufferSize = 40000;
        ///  byte[] CompressedBytes = new byte[bufferSize];
        ///  byte[] DecompressedBytes = new byte[bufferSize];
        ///
        ///  ZlibCodec compressor = new ZlibCodec();
        ///
        ///  compressor.InitializeDeflate(CompressionLevel.Default);
        ///
        ///  compressor.InputBuffer = System.Text.ASCIIEncoding.ASCII.GetBytes(TextToCompress);
        ///  compressor.NextIn = 0;
        ///  compressor.AvailableBytesIn = compressor.InputBuffer.Length;
        ///
        ///  compressor.OutputBuffer = CompressedBytes;
        ///  compressor.NextOut = 0;
        ///  compressor.AvailableBytesOut = CompressedBytes.Length;
        ///
        ///  while (compressor.TotalBytesIn != TextToCompress.Length &amp;&amp; compressor.TotalBytesOut &lt; bufferSize)
        ///  {
        ///    compressor.Deflate(FlushType.None);
        ///  }
        ///
        ///  while (true)
        ///  {
        ///    int rc= compressor.Deflate(FlushType.Finish);
        ///    if (rc == ZlibConstants.Z_STREAM_END) break;
        ///  }
        ///
        ///  compressor.EndDeflate();
        ///
        /// </code>
        /// </example>
        /// <returns>Z_OK if all goes well. You generally don't need to check the return code.</returns>
        // Token: 0x060004AE RID: 1198 RVA: 0x00028298 File Offset: 0x00026498
        public int InitializeDeflate()
        {
            return this._InternalInitializeDeflate(true);
        }

        /// <summary>
        /// Initialize the ZlibCodec for deflation operation, using the specified CompressionLevel.
        /// </summary>
        /// <remarks>
        /// The codec will use the maximum window bits (15) and the specified
        /// CompressionLevel.  It will emit a ZLIB stream as it compresses.
        /// </remarks>
        /// <param name="level">The compression level for the codec.</param>
        /// <returns>Z_OK if all goes well.</returns>
        // Token: 0x060004AF RID: 1199 RVA: 0x000282B4 File Offset: 0x000264B4
        public int InitializeDeflate(CompressionLevel level)
        {
            this.CompressLevel = level;
            return this._InternalInitializeDeflate(true);
        }

        /// <summary>
        /// Initialize the ZlibCodec for deflation operation, using the specified CompressionLevel, 
        /// and the explicit flag governing whether to emit an RFC1950 header byte pair.
        /// </summary>
        /// <remarks>
        /// The codec will use the maximum window bits (15) and the specified CompressionLevel.
        /// If you want to generate a zlib stream, you should specify true for
        /// wantRfc1950Header. In this case, the library will emit a ZLIB
        /// header, as defined in <see href="http://www.ietf.org/rfc/rfc1950.txt">RFC
        /// 1950</see>, in the compressed stream.  
        /// </remarks>
        /// <param name="level">The compression level for the codec.</param>
        /// <param name="wantRfc1950Header">whether to emit an initial RFC1950 byte pair in the compressed stream.</param>
        /// <returns>Z_OK if all goes well.</returns>
        // Token: 0x060004B0 RID: 1200 RVA: 0x000282D4 File Offset: 0x000264D4
        public int InitializeDeflate(CompressionLevel level, bool wantRfc1950Header)
        {
            this.CompressLevel = level;
            return this._InternalInitializeDeflate(wantRfc1950Header);
        }

        /// <summary>
        /// Initialize the ZlibCodec for deflation operation, using the specified CompressionLevel, 
        /// and the specified number of window bits. 
        /// </summary>
        /// <remarks>
        /// The codec will use the specified number of window bits and the specified CompressionLevel.
        /// </remarks>
        /// <param name="level">The compression level for the codec.</param>
        /// <param name="bits">the number of window bits to use.  If you don't know what this means, don't use this method.</param>
        /// <returns>Z_OK if all goes well.</returns>
        // Token: 0x060004B1 RID: 1201 RVA: 0x000282F4 File Offset: 0x000264F4
        public int InitializeDeflate(CompressionLevel level, int bits)
        {
            this.CompressLevel = level;
            this.WindowBits = bits;
            return this._InternalInitializeDeflate(true);
        }

        /// <summary>
        /// Initialize the ZlibCodec for deflation operation, using the specified
        /// CompressionLevel, the specified number of window bits, and the explicit flag
        /// governing whether to emit an RFC1950 header byte pair.
        /// </summary>
        ///
        /// <param name="level">The compression level for the codec.</param>
        /// <param name="wantRfc1950Header">whether to emit an initial RFC1950 byte pair in the compressed stream.</param>
        /// <param name="bits">the number of window bits to use.  If you don't know what this means, don't use this method.</param>
        /// <returns>Z_OK if all goes well.</returns>
        // Token: 0x060004B2 RID: 1202 RVA: 0x0002831C File Offset: 0x0002651C
        public int InitializeDeflate(CompressionLevel level, int bits, bool wantRfc1950Header)
        {
            this.CompressLevel = level;
            this.WindowBits = bits;
            return this._InternalInitializeDeflate(wantRfc1950Header);
        }

        // Token: 0x060004B3 RID: 1203 RVA: 0x00028344 File Offset: 0x00026544
        private int _InternalInitializeDeflate(bool wantRfc1950Header)
        {
            if (this.istate != null)
            {
                throw new ZlibException("You may not call InitializeDeflate() after calling InitializeInflate().");
            }
            this.dstate = new DeflateManager();
            this.dstate.WantRfc1950HeaderBytes = wantRfc1950Header;
            return this.dstate.Initialize(this, this.CompressLevel, this.WindowBits, this.Strategy);
        }

        /// <summary>
        /// Deflate one batch of data.
        /// </summary>
        /// <remarks>
        /// You must have set InputBuffer and OutputBuffer before calling this method.
        /// </remarks>
        /// <example>
        /// <code>
        /// private void DeflateBuffer(CompressionLevel level)
        /// {
        ///     int bufferSize = 1024;
        ///     byte[] buffer = new byte[bufferSize];
        ///     ZlibCodec compressor = new ZlibCodec();
        ///
        ///     Console.WriteLine("\n============================================");
        ///     Console.WriteLine("Size of Buffer to Deflate: {0} bytes.", UncompressedBytes.Length);
        ///     MemoryStream ms = new MemoryStream();
        ///
        ///     int rc = compressor.InitializeDeflate(level);
        ///
        ///     compressor.InputBuffer = UncompressedBytes;
        ///     compressor.NextIn = 0;
        ///     compressor.AvailableBytesIn = UncompressedBytes.Length;
        ///
        ///     compressor.OutputBuffer = buffer;
        ///
        ///     // pass 1: deflate 
        ///     do
        ///     {
        ///         compressor.NextOut = 0;
        ///         compressor.AvailableBytesOut = buffer.Length;
        ///         rc = compressor.Deflate(FlushType.None);
        ///
        ///         if (rc != ZlibConstants.Z_OK &amp;&amp; rc != ZlibConstants.Z_STREAM_END)
        ///             throw new Exception("deflating: " + compressor.Message);
        ///
        ///         ms.Write(compressor.OutputBuffer, 0, buffer.Length - compressor.AvailableBytesOut);
        ///     }
        ///     while (compressor.AvailableBytesIn &gt; 0 || compressor.AvailableBytesOut == 0);
        ///
        ///     // pass 2: finish and flush
        ///     do
        ///     {
        ///         compressor.NextOut = 0;
        ///         compressor.AvailableBytesOut = buffer.Length;
        ///         rc = compressor.Deflate(FlushType.Finish);
        ///
        ///         if (rc != ZlibConstants.Z_STREAM_END &amp;&amp; rc != ZlibConstants.Z_OK)
        ///             throw new Exception("deflating: " + compressor.Message);
        ///
        ///         if (buffer.Length - compressor.AvailableBytesOut &gt; 0)
        ///             ms.Write(buffer, 0, buffer.Length - compressor.AvailableBytesOut);
        ///     }
        ///     while (compressor.AvailableBytesIn &gt; 0 || compressor.AvailableBytesOut == 0);
        ///
        ///     compressor.EndDeflate();
        ///
        ///     ms.Seek(0, SeekOrigin.Begin);
        ///     CompressedBytes = new byte[compressor.TotalBytesOut];
        ///     ms.Read(CompressedBytes, 0, CompressedBytes.Length);
        /// }
        /// </code>
        /// </example>
        /// <param name="flush">whether to flush all data as you deflate. Generally you will want to 
        /// use Z_NO_FLUSH here, in a series of calls to Deflate(), and then call EndDeflate() to 
        /// flush everything. 
        /// </param>
        /// <returns>Z_OK if all goes well.</returns>
        // Token: 0x060004B4 RID: 1204 RVA: 0x000283A4 File Offset: 0x000265A4
        public int Deflate(FlushType flush)
        {
            if (this.dstate == null)
            {
                throw new ZlibException("No Deflate State!");
            }
            return this.dstate.Deflate(flush);
        }

        /// <summary>
        /// End a deflation session.
        /// </summary>
        /// <remarks>
        /// Call this after making a series of one or more calls to Deflate(). All buffers are flushed.
        /// </remarks>
        /// <returns>Z_OK if all goes well.</returns>
        // Token: 0x060004B5 RID: 1205 RVA: 0x000283E0 File Offset: 0x000265E0
        public int EndDeflate()
        {
            if (this.dstate == null)
            {
                throw new ZlibException("No Deflate State!");
            }
            this.dstate = null;
            return 0;
        }

        /// <summary>
        /// Reset a codec for another deflation session.
        /// </summary>
        /// <remarks>
        /// Call this to reset the deflation state.  For example if a thread is deflating
        /// non-consecutive blocks, you can call Reset() after the Deflate(Sync) of the first
        /// block and before the next Deflate(None) of the second block.
        /// </remarks>
        /// <returns>Z_OK if all goes well.</returns>
        // Token: 0x060004B6 RID: 1206 RVA: 0x00028418 File Offset: 0x00026618
        public void ResetDeflate()
        {
            if (this.dstate == null)
            {
                throw new ZlibException("No Deflate State!");
            }
            this.dstate.Reset();
        }

        /// <summary>
        /// Set the CompressionStrategy and CompressionLevel for a deflation session.
        /// </summary>
        /// <param name="level">the level of compression to use.</param>
        /// <param name="strategy">the strategy to use for compression.</param>
        /// <returns>Z_OK if all goes well.</returns>
        // Token: 0x060004B7 RID: 1207 RVA: 0x00028450 File Offset: 0x00026650
        public int SetDeflateParams(CompressionLevel level, CompressionStrategy strategy)
        {
            if (this.dstate == null)
            {
                throw new ZlibException("No Deflate State!");
            }
            return this.dstate.SetParams(level, strategy);
        }

        /// <summary>
        /// Set the dictionary to be used for either Inflation or Deflation.
        /// </summary>
        /// <param name="dictionary">The dictionary bytes to use.</param>
        /// <returns>Z_OK if all goes well.</returns>
        // Token: 0x060004B8 RID: 1208 RVA: 0x0002848C File Offset: 0x0002668C
        public int SetDictionary(byte[] dictionary)
        {
            int result;
            if (this.istate != null)
            {
                result = this.istate.SetDictionary(dictionary);
            }
            else
            {
                if (this.dstate == null)
                {
                    throw new ZlibException("No Inflate or Deflate state!");
                }
                result = this.dstate.SetDictionary(dictionary);
            }
            return result;
        }

        // Token: 0x060004B9 RID: 1209 RVA: 0x000284E0 File Offset: 0x000266E0
        internal void flush_pending()
        {
            int len = this.dstate.pendingCount;
            if (len > this.AvailableBytesOut)
            {
                len = this.AvailableBytesOut;
            }
            if (len != 0)
            {
                if (this.dstate.pending.Length <= this.dstate.nextPending || this.OutputBuffer.Length <= this.NextOut || this.dstate.pending.Length < this.dstate.nextPending + len || this.OutputBuffer.Length < this.NextOut + len)
                {
                    throw new ZlibException(string.Format("Invalid State. (pending.Length={0}, pendingCount={1})", this.dstate.pending.Length, this.dstate.pendingCount));
                }
                Array.Copy(this.dstate.pending, this.dstate.nextPending, this.OutputBuffer, this.NextOut, len);
                this.NextOut += len;
                this.dstate.nextPending += len;
                this.TotalBytesOut += (long)len;
                this.AvailableBytesOut -= len;
                this.dstate.pendingCount -= len;
                if (this.dstate.pendingCount == 0)
                {
                    this.dstate.nextPending = 0;
                }
            }
        }

        // Token: 0x060004BA RID: 1210 RVA: 0x00028654 File Offset: 0x00026854
        internal int read_buf(byte[] buf, int start, int size)
        {
            int len = this.AvailableBytesIn;
            if (len > size)
            {
                len = size;
            }
            int result;
            if (len == 0)
            {
                result = 0;
            }
            else
            {
                this.AvailableBytesIn -= len;
                if (this.dstate.WantRfc1950HeaderBytes)
                {
                    this._Adler32 = Adler.Adler32(this._Adler32, this.InputBuffer, this.NextIn, len);
                }
                Array.Copy(this.InputBuffer, this.NextIn, buf, start, len);
                this.NextIn += len;
                this.TotalBytesIn += (long)len;
                result = len;
            }
            return result;
        }

        /// <summary>
        /// The buffer from which data is taken.
        /// </summary>
        // Token: 0x040003B8 RID: 952
        public byte[] InputBuffer;

        /// <summary>
        /// An index into the InputBuffer array, indicating where to start reading. 
        /// </summary>
        // Token: 0x040003B9 RID: 953
        public int NextIn;

        /// <summary>
        /// The number of bytes available in the InputBuffer, starting at NextIn. 
        /// </summary>
        /// <remarks>
        /// Generally you should set this to InputBuffer.Length before the first Inflate() or Deflate() call. 
        /// The class will update this number as calls to Inflate/Deflate are made.
        /// </remarks>
        // Token: 0x040003BA RID: 954
        public int AvailableBytesIn;

        /// <summary>
        /// Total number of bytes read so far, through all calls to Inflate()/Deflate().
        /// </summary>
        // Token: 0x040003BB RID: 955
        public long TotalBytesIn;

        /// <summary>
        /// Buffer to store output data.
        /// </summary>
        // Token: 0x040003BC RID: 956
        public byte[] OutputBuffer;

        /// <summary>
        /// An index into the OutputBuffer array, indicating where to start writing. 
        /// </summary>
        // Token: 0x040003BD RID: 957
        public int NextOut;

        /// <summary>
        /// The number of bytes available in the OutputBuffer, starting at NextOut. 
        /// </summary>
        /// <remarks>
        /// Generally you should set this to OutputBuffer.Length before the first Inflate() or Deflate() call. 
        /// The class will update this number as calls to Inflate/Deflate are made.
        /// </remarks>
        // Token: 0x040003BE RID: 958
        public int AvailableBytesOut;

        /// <summary>
        /// Total number of bytes written to the output so far, through all calls to Inflate()/Deflate().
        /// </summary>
        // Token: 0x040003BF RID: 959
        public long TotalBytesOut;

        /// <summary>
        /// used for diagnostics, when something goes wrong!
        /// </summary>
        // Token: 0x040003C0 RID: 960
        public string Message;

        // Token: 0x040003C1 RID: 961
        internal DeflateManager dstate;

        // Token: 0x040003C2 RID: 962
        internal InflateManager istate;

        // Token: 0x040003C3 RID: 963
        internal uint _Adler32;

        /// <summary>
        /// The compression level to use in this codec.  Useful only in compression mode.
        /// </summary>
        // Token: 0x040003C4 RID: 964
        public CompressionLevel CompressLevel = CompressionLevel.Default;

        /// <summary>
        /// The number of Window Bits to use.  
        /// </summary>
        /// <remarks>
        /// This gauges the size of the sliding window, and hence the 
        /// compression effectiveness as well as memory consumption. It's best to just leave this 
        /// setting alone if you don't know what it is.  The maximum value is 15 bits, which implies
        /// a 32k window.  
        /// </remarks>
        // Token: 0x040003C5 RID: 965
        public int WindowBits = 15;

        /// <summary>
        /// The compression strategy to use.
        /// </summary>
        /// <remarks>
        /// This is only effective in compression.  The theory offered by ZLIB is that different
        /// strategies could potentially produce significant differences in compression behavior
        /// for different data sets.  Unfortunately I don't have any good recommendations for how
        /// to set it differently.  When I tested changing the strategy I got minimally different
        /// compression performance. It's best to leave this property alone if you don't have a
        /// good feel for it.  Or, you may want to produce a test harness that runs through the
        /// different strategy options and evaluates them on different file types. If you do that,
        /// let me know your results.
        /// </remarks>
        // Token: 0x040003C6 RID: 966
        public CompressionStrategy Strategy = CompressionStrategy.Default;
    }
}


namespace Ionic.Zlib
{
    /// <summary>
    /// A bunch of constants used in the Zlib interface.
    /// </summary>
    // Token: 0x0200006E RID: 110
    public static class ZlibConstants
    {
        /// <summary>
        /// The maximum number of window bits for the Deflate algorithm.
        /// </summary>
        // Token: 0x040003C7 RID: 967
        public const int WindowBitsMax = 15;

        /// <summary>
        /// The default number of window bits for the Deflate algorithm.
        /// </summary>
        // Token: 0x040003C8 RID: 968
        public const int WindowBitsDefault = 15;

        /// <summary>
        /// indicates everything is A-OK
        /// </summary>
        // Token: 0x040003C9 RID: 969
        public const int Z_OK = 0;

        /// <summary>
        /// Indicates that the last operation reached the end of the stream.
        /// </summary>
        // Token: 0x040003CA RID: 970
        public const int Z_STREAM_END = 1;

        /// <summary>
        /// The operation ended in need of a dictionary. 
        /// </summary>
        // Token: 0x040003CB RID: 971
        public const int Z_NEED_DICT = 2;

        /// <summary>
        /// There was an error with the stream - not enough data, not open and readable, etc.
        /// </summary>
        // Token: 0x040003CC RID: 972
        public const int Z_STREAM_ERROR = -2;

        /// <summary>
        /// There was an error with the data - not enough data, bad data, etc.
        /// </summary>
        // Token: 0x040003CD RID: 973
        public const int Z_DATA_ERROR = -3;

        /// <summary>
        /// There was an error with the working buffer.
        /// </summary>
        // Token: 0x040003CE RID: 974
        public const int Z_BUF_ERROR = -5;

        /// <summary>
        /// The size of the working buffer used in the ZlibCodec class. Defaults to 8192 bytes.
        /// </summary>
        // Token: 0x040003CF RID: 975
        public const int WorkingBufferSizeDefault = 16384;

        /// <summary>
        /// The minimum size of the working buffer used in the ZlibCodec class.  Currently it is 128 bytes.
        /// </summary>
        // Token: 0x040003D0 RID: 976
        public const int WorkingBufferSizeMin = 1024;
    }
}


namespace Ionic.Zlib
{
    /// <summary>
    /// A general purpose exception class for exceptions in the Zlib library.
    /// </summary>
    // Token: 0x02000065 RID: 101
    [Guid("ebc25cf6-9120-4283-b972-0e5520d0000E")]
    public class ZlibException : Exception
    {
        /// <summary>
        /// The ZlibException class captures exception information generated
        /// by the Zlib library.
        /// </summary>
        // Token: 0x0600047E RID: 1150 RVA: 0x00026749 File Offset: 0x00024949
        public ZlibException()
        {
        }

        /// <summary>
        /// This ctor collects a message attached to the exception.
        /// </summary>
        /// <param name="s">the message for the exception.</param>
        // Token: 0x0600047F RID: 1151 RVA: 0x00026754 File Offset: 0x00024954
        public ZlibException(string s) : base(s)
        {
        }
    }
}


namespace Ionic.Zlib
{
    /// <summary>
    /// Represents a Zlib stream for compression or decompression.
    /// </summary>
    /// <remarks>
    ///
    /// <para>
    /// The ZlibStream is a <see href="http://en.wikipedia.org/wiki/Decorator_pattern">Decorator</see> on a <see cref="T:System.IO.Stream" />.  It adds ZLIB compression or decompression to any
    /// stream.
    /// </para>
    ///
    /// <para> Using this stream, applications can compress or decompress data via
    /// stream <c>Read()</c> and <c>Write()</c> operations.  Either compresssion or
    /// decompression can occur through either reading or writing. The compression
    /// format used is ZLIB, which is documented in <see href="http://www.ietf.org/rfc/rfc1950.txt">IETF RFC 1950</see>, "ZLIB Compressed
    /// Data Format Specification version 3.3". This implementation of ZLIB always uses
    /// DEFLATE as the compression method.  (see <see href="http://www.ietf.org/rfc/rfc1951.txt">IETF RFC 1951</see>, "DEFLATE
    /// Compressed Data Format Specification version 1.3.") </para>
    ///
    /// <para>
    /// The ZLIB format allows for varying compression methods, window sizes, and dictionaries.
    /// This implementation always uses the DEFLATE compression method, a preset dictionary,
    /// and 15 window bits by default.
    /// </para>
    ///
    /// <para>
    /// This class is similar to <see cref="T:Ionic.Zlib.DeflateStream" />, except that it adds the
    /// RFC1950 header and trailer bytes to a compressed stream when compressing, or expects
    /// the RFC1950 header and trailer bytes when decompressing.  It is also similar to the
    /// <see cref="T:Ionic.Zlib.GZipStream" />.
    /// </para>
    /// </remarks>
    /// <seealso cref="T:Ionic.Zlib.DeflateStream" />
    /// <seealso cref="T:Ionic.Zlib.GZipStream" />
    // Token: 0x0200006F RID: 111
    public class ZlibStream : Stream
    {
        /// <summary>
        /// Create a <c>ZlibStream</c> using the specified <c>CompressionMode</c>.
        /// </summary>
        /// <remarks>
        ///
        /// <para>
        ///   When mode is <c>CompressionMode.Compress</c>, the <c>ZlibStream</c>
        ///   will use the default compression level. The "captive" stream will be
        ///   closed when the <c>ZlibStream</c> is closed.
        /// </para>
        ///
        /// </remarks>
        ///
        /// <example>
        /// This example uses a <c>ZlibStream</c> to compress a file, and writes the
        /// compressed data to another file.
        /// <code>
        /// using (System.IO.Stream input = System.IO.File.OpenRead(fileToCompress))
        /// {
        ///     using (var raw = System.IO.File.Create(fileToCompress + ".zlib"))
        ///     {
        ///         using (Stream compressor = new ZlibStream(raw, CompressionMode.Compress))
        ///         {
        ///             byte[] buffer = new byte[WORKING_BUFFER_SIZE];
        ///             int n;
        ///             while ((n= input.Read(buffer, 0, buffer.Length)) != 0)
        ///             {
        ///                 compressor.Write(buffer, 0, n);
        ///             }
        ///         }
        ///     }
        /// }
        /// </code>
        /// <code lang="VB">
        /// Using input As Stream = File.OpenRead(fileToCompress)
        ///     Using raw As FileStream = File.Create(fileToCompress &amp; ".zlib")
        ///     Using compressor As Stream = New ZlibStream(raw, CompressionMode.Compress)
        ///         Dim buffer As Byte() = New Byte(4096) {}
        ///         Dim n As Integer = -1
        ///         Do While (n &lt;&gt; 0)
        ///             If (n &gt; 0) Then
        ///                 compressor.Write(buffer, 0, n)
        ///             End If
        ///             n = input.Read(buffer, 0, buffer.Length)
        ///         Loop
        ///     End Using
        ///     End Using
        /// End Using
        /// </code>
        /// </example>
        ///
        /// <param name="stream">The stream which will be read or written.</param>
        /// <param name="mode">Indicates whether the ZlibStream will compress or decompress.</param>
        // Token: 0x060004BB RID: 1211 RVA: 0x000286FC File Offset: 0x000268FC
        public ZlibStream(Stream stream, CompressionMode mode) : this(stream, mode, CompressionLevel.Default, false)
        {
        }

        /// <summary>
        ///   Create a <c>ZlibStream</c> using the specified <c>CompressionMode</c> and
        ///   the specified <c>CompressionLevel</c>.
        /// </summary>
        ///
        /// <remarks>
        ///
        /// <para>
        ///   When mode is <c>CompressionMode.Decompress</c>, the level parameter is ignored.
        ///   The "captive" stream will be closed when the <c>ZlibStream</c> is closed.
        /// </para>
        ///
        /// </remarks>
        ///
        /// <example>
        ///   This example uses a <c>ZlibStream</c> to compress data from a file, and writes the
        ///   compressed data to another file.
        ///
        /// <code>
        /// using (System.IO.Stream input = System.IO.File.OpenRead(fileToCompress))
        /// {
        ///     using (var raw = System.IO.File.Create(fileToCompress + ".zlib"))
        ///     {
        ///         using (Stream compressor = new ZlibStream(raw,
        ///                                                   CompressionMode.Compress,
        ///                                                   CompressionLevel.BestCompression))
        ///         {
        ///             byte[] buffer = new byte[WORKING_BUFFER_SIZE];
        ///             int n;
        ///             while ((n= input.Read(buffer, 0, buffer.Length)) != 0)
        ///             {
        ///                 compressor.Write(buffer, 0, n);
        ///             }
        ///         }
        ///     }
        /// }
        /// </code>
        ///
        /// <code lang="VB">
        /// Using input As Stream = File.OpenRead(fileToCompress)
        ///     Using raw As FileStream = File.Create(fileToCompress &amp; ".zlib")
        ///         Using compressor As Stream = New ZlibStream(raw, CompressionMode.Compress, CompressionLevel.BestCompression)
        ///             Dim buffer As Byte() = New Byte(4096) {}
        ///             Dim n As Integer = -1
        ///             Do While (n &lt;&gt; 0)
        ///                 If (n &gt; 0) Then
        ///                     compressor.Write(buffer, 0, n)
        ///                 End If
        ///                 n = input.Read(buffer, 0, buffer.Length)
        ///             Loop
        ///         End Using
        ///     End Using
        /// End Using
        /// </code>
        /// </example>
        ///
        /// <param name="stream">The stream to be read or written while deflating or inflating.</param>
        /// <param name="mode">Indicates whether the ZlibStream will compress or decompress.</param>
        /// <param name="level">A tuning knob to trade speed for effectiveness.</param>
        // Token: 0x060004BC RID: 1212 RVA: 0x0002870B File Offset: 0x0002690B
        public ZlibStream(Stream stream, CompressionMode mode, CompressionLevel level) : this(stream, mode, level, false)
        {
        }

        /// <summary>
        ///   Create a <c>ZlibStream</c> using the specified <c>CompressionMode</c>, and
        ///   explicitly specify whether the captive stream should be left open after
        ///   Deflation or Inflation.
        /// </summary>
        ///
        /// <remarks>
        ///
        /// <para>
        ///   When mode is <c>CompressionMode.Compress</c>, the <c>ZlibStream</c> will use
        ///   the default compression level.
        /// </para>
        ///
        /// <para>
        ///   This constructor allows the application to request that the captive stream
        ///   remain open after the deflation or inflation occurs.  By default, after
        ///   <c>Close()</c> is called on the stream, the captive stream is also
        ///   closed. In some cases this is not desired, for example if the stream is a
        ///   <see cref="T:System.IO.MemoryStream" /> that will be re-read after
        ///   compression.  Specify true for the <paramref name="leaveOpen" /> parameter to leave the stream
        ///   open.
        /// </para>
        ///
        /// <para>
        /// See the other overloads of this constructor for example code.
        /// </para>
        ///
        /// </remarks>
        ///
        /// <param name="stream">The stream which will be read or written. This is called the
        /// "captive" stream in other places in this documentation.</param>
        /// <param name="mode">Indicates whether the ZlibStream will compress or decompress.</param>
        /// <param name="leaveOpen">true if the application would like the stream to remain
        /// open after inflation/deflation.</param>
        // Token: 0x060004BD RID: 1213 RVA: 0x0002871A File Offset: 0x0002691A
        public ZlibStream(Stream stream, CompressionMode mode, bool leaveOpen) : this(stream, mode, CompressionLevel.Default, leaveOpen)
        {
        }

        /// <summary>
        ///   Create a <c>ZlibStream</c> using the specified <c>CompressionMode</c>
        ///   and the specified <c>CompressionLevel</c>, and explicitly specify
        ///   whether the stream should be left open after Deflation or Inflation.
        /// </summary>
        ///
        /// <remarks>
        ///
        /// <para>
        ///   This constructor allows the application to request that the captive
        ///   stream remain open after the deflation or inflation occurs.  By
        ///   default, after <c>Close()</c> is called on the stream, the captive
        ///   stream is also closed. In some cases this is not desired, for example
        ///   if the stream is a <see cref="T:System.IO.MemoryStream" /> that will be
        ///   re-read after compression.  Specify true for the <paramref name="leaveOpen" /> parameter to leave the stream open.
        /// </para>
        ///
        /// <para>
        ///   When mode is <c>CompressionMode.Decompress</c>, the level parameter is
        ///   ignored.
        /// </para>
        ///
        /// </remarks>
        ///
        /// <example>
        ///
        /// This example shows how to use a ZlibStream to compress the data from a file,
        /// and store the result into another file. The filestream remains open to allow
        /// additional data to be written to it.
        ///
        /// <code>
        /// using (var output = System.IO.File.Create(fileToCompress + ".zlib"))
        /// {
        ///     using (System.IO.Stream input = System.IO.File.OpenRead(fileToCompress))
        ///     {
        ///         using (Stream compressor = new ZlibStream(output, CompressionMode.Compress, CompressionLevel.BestCompression, true))
        ///         {
        ///             byte[] buffer = new byte[WORKING_BUFFER_SIZE];
        ///             int n;
        ///             while ((n= input.Read(buffer, 0, buffer.Length)) != 0)
        ///             {
        ///                 compressor.Write(buffer, 0, n);
        ///             }
        ///         }
        ///     }
        ///     // can write additional data to the output stream here
        /// }
        /// </code>
        /// <code lang="VB">
        /// Using output As FileStream = File.Create(fileToCompress &amp; ".zlib")
        ///     Using input As Stream = File.OpenRead(fileToCompress)
        ///         Using compressor As Stream = New ZlibStream(output, CompressionMode.Compress, CompressionLevel.BestCompression, True)
        ///             Dim buffer As Byte() = New Byte(4096) {}
        ///             Dim n As Integer = -1
        ///             Do While (n &lt;&gt; 0)
        ///                 If (n &gt; 0) Then
        ///                     compressor.Write(buffer, 0, n)
        ///                 End If
        ///                 n = input.Read(buffer, 0, buffer.Length)
        ///             Loop
        ///         End Using
        ///     End Using
        ///     ' can write additional data to the output stream here.
        /// End Using
        /// </code>
        /// </example>
        ///
        /// <param name="stream">The stream which will be read or written.</param>
        ///
        /// <param name="mode">Indicates whether the ZlibStream will compress or decompress.</param>
        ///
        /// <param name="leaveOpen">
        /// true if the application would like the stream to remain open after
        /// inflation/deflation.
        /// </param>
        ///
        /// <param name="level">
        /// A tuning knob to trade speed for effectiveness. This parameter is
        /// effective only when mode is <c>CompressionMode.Compress</c>.
        /// </param>
        // Token: 0x060004BE RID: 1214 RVA: 0x00028729 File Offset: 0x00026929
        public ZlibStream(Stream stream, CompressionMode mode, CompressionLevel level, bool leaveOpen)
        {
            this._baseStream = new ZlibBaseStream(stream, mode, level, ZlibStreamFlavor.ZLIB, leaveOpen);
        }

        /// <summary>
        /// This property sets the flush behavior on the stream.
        /// Sorry, though, not sure exactly how to describe all the various settings.
        /// </summary>
        // Token: 0x17000126 RID: 294
        // (get) Token: 0x060004BF RID: 1215 RVA: 0x0002874C File Offset: 0x0002694C
        // (set) Token: 0x060004C0 RID: 1216 RVA: 0x0002876C File Offset: 0x0002696C
        public virtual FlushType FlushMode
        {
            get
            {
                return this._baseStream._flushMode;
            }
            set
            {
                if (this._disposed)
                {
                    throw new ObjectDisposedException("ZlibStream");
                }
                this._baseStream._flushMode = value;
            }
        }

        /// <summary>
        ///   The size of the working buffer for the compression codec.
        /// </summary>
        ///
        /// <remarks>
        /// <para>
        ///   The working buffer is used for all stream operations.  The default size is
        ///   1024 bytes. The minimum size is 128 bytes. You may get better performance
        ///   with a larger buffer.  Then again, you might not.  You would have to test
        ///   it.
        /// </para>
        ///
        /// <para>
        ///   Set this before the first call to <c>Read()</c> or <c>Write()</c> on the
        ///   stream. If you try to set it afterwards, it will throw.
        /// </para>
        /// </remarks>
        // Token: 0x17000127 RID: 295
        // (get) Token: 0x060004C1 RID: 1217 RVA: 0x000287A0 File Offset: 0x000269A0
        // (set) Token: 0x060004C2 RID: 1218 RVA: 0x000287C0 File Offset: 0x000269C0
        public int BufferSize
        {
            get
            {
                return this._baseStream._bufferSize;
            }
            set
            {
                if (this._disposed)
                {
                    throw new ObjectDisposedException("ZlibStream");
                }
                if (this._baseStream._workingBuffer != null)
                {
                    throw new ZlibException("The working buffer is already set.");
                }
                if (value < 1024)
                {
                    throw new ZlibException(string.Format("Don't be silly. {0} bytes?? Use a bigger buffer, at least {1}.", value, 1024));
                }
                this._baseStream._bufferSize = value;
            }
        }

        /// <summary> Returns the total number of bytes input so far.</summary>
        // Token: 0x17000128 RID: 296
        // (get) Token: 0x060004C3 RID: 1219 RVA: 0x00028840 File Offset: 0x00026A40
        public virtual long TotalIn
        {
            get
            {
                return this._baseStream._z.TotalBytesIn;
            }
        }

        /// <summary> Returns the total number of bytes output so far.</summary>
        // Token: 0x17000129 RID: 297
        // (get) Token: 0x060004C4 RID: 1220 RVA: 0x00028864 File Offset: 0x00026A64
        public virtual long TotalOut
        {
            get
            {
                return this._baseStream._z.TotalBytesOut;
            }
        }

        /// <summary>
        ///   Dispose the stream.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     This may or may not result in a <c>Close()</c> call on the captive
        ///     stream.  See the constructors that have a <c>leaveOpen</c> parameter
        ///     for more information.
        ///   </para>
        ///   <para>
        ///     This method may be invoked in two distinct scenarios.  If disposing
        ///     == true, the method has been called directly or indirectly by a
        ///     user's code, for example via the public Dispose() method. In this
        ///     case, both managed and unmanaged resources can be referenced and
        ///     disposed.  If disposing == false, the method has been called by the
        ///     runtime from inside the object finalizer and this method should not
        ///     reference other objects; in that case only unmanaged resources must
        ///     be referenced or disposed.
        ///   </para>
        /// </remarks>
        /// <param name="disposing">
        ///   indicates whether the Dispose method was invoked by user code.
        /// </param>
        // Token: 0x060004C5 RID: 1221 RVA: 0x00028888 File Offset: 0x00026A88
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!this._disposed)
                {
                    if (disposing && this._baseStream != null)
                    {
                        this._baseStream.Close();
                    }
                    this._disposed = true;
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Indicates whether the stream can be read.
        /// </summary>
        /// <remarks>
        /// The return value depends on whether the captive stream supports reading.
        /// </remarks>
        // Token: 0x1700012A RID: 298
        // (get) Token: 0x060004C6 RID: 1222 RVA: 0x000288E8 File Offset: 0x00026AE8
        public override bool CanRead
        {
            get
            {
                if (this._disposed)
                {
                    throw new ObjectDisposedException("ZlibStream");
                }
                return this._baseStream._stream.CanRead;
            }
        }

        /// <summary>
        /// Indicates whether the stream supports Seek operations.
        /// </summary>
        /// <remarks>
        /// Always returns false.
        /// </remarks>
        // Token: 0x1700012B RID: 299
        // (get) Token: 0x060004C7 RID: 1223 RVA: 0x00028924 File Offset: 0x00026B24
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
        // Token: 0x1700012C RID: 300
        // (get) Token: 0x060004C8 RID: 1224 RVA: 0x00028938 File Offset: 0x00026B38
        public override bool CanWrite
        {
            get
            {
                if (this._disposed)
                {
                    throw new ObjectDisposedException("ZlibStream");
                }
                return this._baseStream._stream.CanWrite;
            }
        }

        /// <summary>
        /// Flush the stream.
        /// </summary>
        // Token: 0x060004C9 RID: 1225 RVA: 0x00028974 File Offset: 0x00026B74
        public override void Flush()
        {
            if (this._disposed)
            {
                throw new ObjectDisposedException("ZlibStream");
            }
            this._baseStream.Flush();
        }

        /// <summary>
        /// Reading this property always throws a <see cref="T:System.NotSupportedException" />.
        /// </summary>
        // Token: 0x1700012D RID: 301
        // (get) Token: 0x060004CA RID: 1226 RVA: 0x000289A6 File Offset: 0x00026BA6
        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        /// <summary>
        ///   The position of the stream pointer.
        /// </summary>
        ///
        /// <remarks>
        ///   Setting this property always throws a <see cref="T:System.NotSupportedException" />. Reading will return the total bytes
        ///   written out, if used in writing, or the total bytes read in, if used in
        ///   reading.  The count may refer to compressed bytes or uncompressed bytes,
        ///   depending on how you've used the stream.
        /// </remarks>
        // Token: 0x1700012E RID: 302
        // (get) Token: 0x060004CB RID: 1227 RVA: 0x000289B0 File Offset: 0x00026BB0
        // (set) Token: 0x060004CC RID: 1228 RVA: 0x00028A14 File Offset: 0x00026C14
        public override long Position
        {
            get
            {
                long result;
                if (this._baseStream._streamMode == ZlibBaseStream.StreamMode.Writer)
                {
                    result = this._baseStream._z.TotalBytesOut;
                }
                else if (this._baseStream._streamMode == ZlibBaseStream.StreamMode.Reader)
                {
                    result = this._baseStream._z.TotalBytesIn;
                }
                else
                {
                    result = 0L;
                }
                return result;
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Read data from the stream.
        /// </summary>
        ///
        /// <remarks>
        ///
        /// <para>
        ///   If you wish to use the <c>ZlibStream</c> to compress data while reading,
        ///   you can create a <c>ZlibStream</c> with <c>CompressionMode.Compress</c>,
        ///   providing an uncompressed data stream.  Then call <c>Read()</c> on that
        ///   <c>ZlibStream</c>, and the data read will be compressed.  If you wish to
        ///   use the <c>ZlibStream</c> to decompress data while reading, you can create
        ///   a <c>ZlibStream</c> with <c>CompressionMode.Decompress</c>, providing a
        ///   readable compressed data stream.  Then call <c>Read()</c> on that
        ///   <c>ZlibStream</c>, and the data will be decompressed as it is read.
        /// </para>
        ///
        /// <para>
        ///   A <c>ZlibStream</c> can be used for <c>Read()</c> or <c>Write()</c>, but
        ///   not both.
        /// </para>
        ///
        /// </remarks>
        ///
        /// <param name="buffer">
        /// The buffer into which the read data should be placed.</param>
        ///
        /// <param name="offset">
        /// the offset within that data array to put the first byte read.</param>
        ///
        /// <param name="count">the number of bytes to read.</param>
        ///
        /// <returns>the number of bytes read</returns>
        // Token: 0x060004CD RID: 1229 RVA: 0x00028A1C File Offset: 0x00026C1C
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (this._disposed)
            {
                throw new ObjectDisposedException("ZlibStream");
            }
            return this._baseStream.Read(buffer, offset, count);
        }

        /// <summary>
        /// Calling this method always throws a <see cref="T:System.NotSupportedException" />.
        /// </summary>
        /// <param name="offset">
        ///   The offset to seek to....
        ///   IF THIS METHOD ACTUALLY DID ANYTHING.
        /// </param>
        /// <param name="origin">
        ///   The reference specifying how to apply the offset....  IF
        ///   THIS METHOD ACTUALLY DID ANYTHING.
        /// </param>
        ///
        /// <returns>nothing. This method always throws.</returns>
        // Token: 0x060004CE RID: 1230 RVA: 0x00028A54 File Offset: 0x00026C54
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Calling this method always throws a <see cref="T:System.NotSupportedException" />.
        /// </summary>
        /// <param name="value">
        ///   The new value for the stream length....  IF
        ///   THIS METHOD ACTUALLY DID ANYTHING.
        /// </param>
        // Token: 0x060004CF RID: 1231 RVA: 0x00028A5C File Offset: 0x00026C5C
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Write data to the stream.
        /// </summary>
        ///
        /// <remarks>
        ///
        /// <para>
        ///   If you wish to use the <c>ZlibStream</c> to compress data while writing,
        ///   you can create a <c>ZlibStream</c> with <c>CompressionMode.Compress</c>,
        ///   and a writable output stream.  Then call <c>Write()</c> on that
        ///   <c>ZlibStream</c>, providing uncompressed data as input.  The data sent to
        ///   the output stream will be the compressed form of the data written.  If you
        ///   wish to use the <c>ZlibStream</c> to decompress data while writing, you
        ///   can create a <c>ZlibStream</c> with <c>CompressionMode.Decompress</c>, and a
        ///   writable output stream.  Then call <c>Write()</c> on that stream,
        ///   providing previously compressed data. The data sent to the output stream
        ///   will be the decompressed form of the data written.
        /// </para>
        ///
        /// <para>
        ///   A <c>ZlibStream</c> can be used for <c>Read()</c> or <c>Write()</c>, but not both.
        /// </para>
        /// </remarks>
        /// <param name="buffer">The buffer holding data to write to the stream.</param>
        /// <param name="offset">the offset within that data array to find the first byte to write.</param>
        /// <param name="count">the number of bytes to write.</param>
        // Token: 0x060004D0 RID: 1232 RVA: 0x00028A64 File Offset: 0x00026C64
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (this._disposed)
            {
                throw new ObjectDisposedException("ZlibStream");
            }
            this._baseStream.Write(buffer, offset, count);
        }

        /// <summary>
        ///   Compress a string into a byte array using ZLIB.
        /// </summary>
        ///
        /// <remarks>
        ///   Uncompress it with <see cref="M:Ionic.Zlib.ZlibStream.UncompressString(System.Byte[])" />.
        /// </remarks>
        ///
        /// <seealso cref="M:Ionic.Zlib.ZlibStream.UncompressString(System.Byte[])" />
        /// <seealso cref="M:Ionic.Zlib.ZlibStream.CompressBuffer(System.Byte[])" />
        /// <seealso cref="M:Ionic.Zlib.GZipStream.CompressString(System.String)" />
        ///
        /// <param name="s">
        ///   A string to compress.  The string will first be encoded
        ///   using UTF8, then compressed.
        /// </param>
        ///
        /// <returns>The string in compressed form</returns>
        // Token: 0x060004D1 RID: 1233 RVA: 0x00028A9C File Offset: 0x00026C9C
        public static byte[] CompressString(string s)
        {
            byte[] result;
            using (MemoryStream ms = new MemoryStream())
            {
                Stream compressor = new ZlibStream(ms, CompressionMode.Compress, CompressionLevel.BestCompression);
                ZlibBaseStream.CompressString(s, compressor);
                result = ms.ToArray();
            }
            return result;
        }

        /// <summary>
        ///   Compress a byte array into a new byte array using ZLIB.
        /// </summary>
        ///
        /// <remarks>
        ///   Uncompress it with <see cref="M:Ionic.Zlib.ZlibStream.UncompressBuffer(System.Byte[])" />.
        /// </remarks>
        ///
        /// <seealso cref="M:Ionic.Zlib.ZlibStream.CompressString(System.String)" />
        /// <seealso cref="M:Ionic.Zlib.ZlibStream.UncompressBuffer(System.Byte[])" />
        ///
        /// <param name="b">
        /// A buffer to compress.
        /// </param>
        ///
        /// <returns>The data in compressed form</returns>
        // Token: 0x060004D2 RID: 1234 RVA: 0x00028AF0 File Offset: 0x00026CF0
        public static byte[] CompressBuffer(byte[] b)
        {
            byte[] result;
            using (MemoryStream ms = new MemoryStream())
            {
                Stream compressor = new ZlibStream(ms, CompressionMode.Compress, CompressionLevel.BestCompression);
                ZlibBaseStream.CompressBuffer(b, compressor);
                result = ms.ToArray();
            }
            return result;
        }

        /// <summary>
        ///   Uncompress a ZLIB-compressed byte array into a single string.
        /// </summary>
        ///
        /// <seealso cref="M:Ionic.Zlib.ZlibStream.CompressString(System.String)" />
        /// <seealso cref="M:Ionic.Zlib.ZlibStream.UncompressBuffer(System.Byte[])" />
        ///
        /// <param name="compressed">
        ///   A buffer containing ZLIB-compressed data.
        /// </param>
        ///
        /// <returns>The uncompressed string</returns>
        // Token: 0x060004D3 RID: 1235 RVA: 0x00028B44 File Offset: 0x00026D44
        public static string UncompressString(byte[] compressed)
        {
            string result;
            using (MemoryStream input = new MemoryStream(compressed))
            {
                Stream decompressor = new ZlibStream(input, CompressionMode.Decompress);
                result = ZlibBaseStream.UncompressString(compressed, decompressor);
            }
            return result;
        }

        /// <summary>
        ///   Uncompress a ZLIB-compressed byte array into a byte array.
        /// </summary>
        ///
        /// <seealso cref="M:Ionic.Zlib.ZlibStream.CompressBuffer(System.Byte[])" />
        /// <seealso cref="M:Ionic.Zlib.ZlibStream.UncompressString(System.Byte[])" />
        ///
        /// <param name="compressed">
        ///   A buffer containing ZLIB-compressed data.
        /// </param>
        ///
        /// <returns>The data in uncompressed form</returns>
        // Token: 0x060004D4 RID: 1236 RVA: 0x00028B90 File Offset: 0x00026D90
        public static byte[] UncompressBuffer(byte[] compressed)
        {
            byte[] result;
            using (MemoryStream input = new MemoryStream(compressed))
            {
                Stream decompressor = new ZlibStream(input, CompressionMode.Decompress);
                result = ZlibBaseStream.UncompressBuffer(compressed, decompressor);
            }
            return result;
        }

        // Token: 0x040003D1 RID: 977
        internal ZlibBaseStream _baseStream;

        // Token: 0x040003D2 RID: 978
        private bool _disposed;
    }
}


namespace Ionic.Zlib
{
    // Token: 0x0200006A RID: 106
    internal enum ZlibStreamFlavor
    {
        // Token: 0x0400039F RID: 927
        ZLIB = 1950,
        // Token: 0x040003A0 RID: 928
        DEFLATE,
        // Token: 0x040003A1 RID: 929
        GZIP
    }
}

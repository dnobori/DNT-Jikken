using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Ionic.Crc
{
    /// <summary>
    ///   Computes a CRC-32. The CRC-32 algorithm is parameterized - you
    ///   can set the polynomial and enable or disable bit
    ///   reversal. This can be used for GZIP, BZip2, or ZIP.
    /// </summary>
    /// <remarks>
    ///   This type is used internally by DotNetZip; it is generally not used
    ///   directly by applications wishing to create, read, or manipulate zip
    ///   archive files.
    /// </remarks>
    // Token: 0x02000070 RID: 112
    [ComVisible(true)]
    [Guid("ebc25cf6-9120-4283-b972-0e5520d0000C")]
    [ClassInterface(ClassInterfaceType.AutoDispatch)]
    public class CRC32
    {
        /// <summary>
        ///   Indicates the total number of bytes applied to the CRC.
        /// </summary>
        // Token: 0x1700012F RID: 303
        // (get) Token: 0x060004D5 RID: 1237 RVA: 0x00028BDC File Offset: 0x00026DDC
        public long TotalBytesRead
        {
            get
            {
                return this._TotalBytesRead;
            }
        }

        /// <summary>
        /// Indicates the current CRC for all blocks slurped in.
        /// </summary>
        // Token: 0x17000130 RID: 304
        // (get) Token: 0x060004D6 RID: 1238 RVA: 0x00028BF4 File Offset: 0x00026DF4
        public int Crc32Result
        {
            get
            {
                return (int)(~(int)this._register);
            }
        }

        /// <summary>
        /// Returns the CRC32 for the specified stream.
        /// </summary>
        /// <param name="input">The stream over which to calculate the CRC32</param>
        /// <returns>the CRC32 calculation</returns>
        // Token: 0x060004D7 RID: 1239 RVA: 0x00028C10 File Offset: 0x00026E10
        public int GetCrc32(Stream input)
        {
            return this.GetCrc32AndCopy(input, null);
        }

        /// <summary>
        /// Returns the CRC32 for the specified stream, and writes the input into the
        /// output stream.
        /// </summary>
        /// <param name="input">The stream over which to calculate the CRC32</param>
        /// <param name="output">The stream into which to deflate the input</param>
        /// <returns>the CRC32 calculation</returns>
        // Token: 0x060004D8 RID: 1240 RVA: 0x00028C2C File Offset: 0x00026E2C
        public int GetCrc32AndCopy(Stream input, Stream output)
        {
            if (input == null)
            {
                throw new Exception("The input stream must not be null.");
            }
            byte[] buffer = new byte[8192];
            int readSize = 8192;
            this._TotalBytesRead = 0L;
            int count = input.Read(buffer, 0, readSize);
            if (output != null)
            {
                output.Write(buffer, 0, count);
            }
            this._TotalBytesRead += (long)count;
            while (count > 0)
            {
                this.SlurpBlock(buffer, 0, count);
                count = input.Read(buffer, 0, readSize);
                if (output != null)
                {
                    output.Write(buffer, 0, count);
                }
                this._TotalBytesRead += (long)count;
            }
            return (int)(~(int)this._register);
        }

        /// <summary>
        ///   Get the CRC32 for the given (word,byte) combo.  This is a
        ///   computation defined by PKzip for PKZIP 2.0 (weak) encryption.
        /// </summary>
        /// <param name="W">The word to start with.</param>
        /// <param name="B">The byte to combine it with.</param>
        /// <returns>The CRC-ized result.</returns>
        // Token: 0x060004D9 RID: 1241 RVA: 0x00028CEC File Offset: 0x00026EEC
        public int ComputeCrc32(int W, byte B)
        {
            return this._InternalComputeCrc32((uint)W, B);
        }

        // Token: 0x060004DA RID: 1242 RVA: 0x00028D08 File Offset: 0x00026F08
        internal int _InternalComputeCrc32(uint W, byte B)
        {
            return (int)(this.crc32Table[(int)((UIntPtr)((W ^ (uint)B) & 255u))] ^ W >> 8);
        }

        /// <summary>
        /// Update the value for the running CRC32 using the given block of bytes.
        /// This is useful when using the CRC32() class in a Stream.
        /// </summary>
        /// <param name="block">block of bytes to slurp</param>
        /// <param name="offset">starting point in the block</param>
        /// <param name="count">how many bytes within the block to slurp</param>
        // Token: 0x060004DB RID: 1243 RVA: 0x00028D30 File Offset: 0x00026F30
        public void SlurpBlock(byte[] block, int offset, int count)
        {
            if (block == null)
            {
                throw new Exception("The data buffer must not be null.");
            }
            for (int i = 0; i < count; i++)
            {
                int x = offset + i;
                byte b = block[x];
                if (this.reverseBits)
                {
                    uint temp = this._register >> 24 ^ (uint)b;
                    this._register = (this._register << 8 ^ this.crc32Table[(int)((UIntPtr)temp)]);
                }
                else
                {
                    uint temp = (this._register & 255u) ^ (uint)b;
                    this._register = (this._register >> 8 ^ this.crc32Table[(int)((UIntPtr)temp)]);
                }
            }
            this._TotalBytesRead += (long)count;
        }

        /// <summary>
        ///   Process one byte in the CRC.
        /// </summary>
        /// <param name="b">the byte to include into the CRC .  </param>
        // Token: 0x060004DC RID: 1244 RVA: 0x00028DE4 File Offset: 0x00026FE4
        public void UpdateCRC(byte b)
        {
            if (this.reverseBits)
            {
                uint temp = this._register >> 24 ^ (uint)b;
                this._register = (this._register << 8 ^ this.crc32Table[(int)((UIntPtr)temp)]);
            }
            else
            {
                uint temp = (this._register & 255u) ^ (uint)b;
                this._register = (this._register >> 8 ^ this.crc32Table[(int)((UIntPtr)temp)]);
            }
        }

        /// <summary>
        ///   Process a run of N identical bytes into the CRC.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     This method serves as an optimization for updating the CRC when a
        ///     run of identical bytes is found. Rather than passing in a buffer of
        ///     length n, containing all identical bytes b, this method accepts the
        ///     byte value and the length of the (virtual) buffer - the length of
        ///     the run.
        ///   </para>
        /// </remarks>
        /// <param name="b">the byte to include into the CRC.  </param>
        /// <param name="n">the number of times that byte should be repeated. </param>
        // Token: 0x060004DD RID: 1245 RVA: 0x00028E50 File Offset: 0x00027050
        public void UpdateCRC(byte b, int n)
        {
            while (n-- > 0)
            {
                if (this.reverseBits)
                {
                    uint temp = this._register >> 24 ^ (uint)b;
                    this._register = (this._register << 8 ^ this.crc32Table[(int)((UIntPtr)((temp >= 0u) ? temp : (temp + 256u)))]);
                }
                else
                {
                    uint temp = (this._register & 255u) ^ (uint)b;
                    this._register = (this._register >> 8 ^ this.crc32Table[(int)((UIntPtr)((temp >= 0u) ? temp : (temp + 256u)))]);
                }
            }
        }

        // Token: 0x060004DE RID: 1246 RVA: 0x00028EEC File Offset: 0x000270EC
        private static uint ReverseBits(uint data)
        {
            uint ret = (data & 1431655765u) << 1 | (data >> 1 & 1431655765u);
            ret = ((ret & 858993459u) << 2 | (ret >> 2 & 858993459u));
            ret = ((ret & 252645135u) << 4 | (ret >> 4 & 252645135u));
            return ret << 24 | (ret & 65280u) << 8 | (ret >> 8 & 65280u) | ret >> 24;
        }

        // Token: 0x060004DF RID: 1247 RVA: 0x00028F5C File Offset: 0x0002715C
        private static byte ReverseBits(byte data)
        {
            uint u = (uint)data * 131586u;
            uint i = 17055760u;
            uint s = u & i;
            uint t = u << 2 & i << 1;
            return (byte)(16781313u * (s + t) >> 24);
        }

        // Token: 0x060004E0 RID: 1248 RVA: 0x00028F98 File Offset: 0x00027198
        private void GenerateLookupTable()
        {
            this.crc32Table = new uint[256];
            byte i = 0;
            do
            {
                uint dwCrc = (uint)i;
                for (byte j = 8; j > 0; j -= 1)
                {
                    if ((dwCrc & 1u) == 1u)
                    {
                        dwCrc = (dwCrc >> 1 ^ this.dwPolynomial);
                    }
                    else
                    {
                        dwCrc >>= 1;
                    }
                }
                if (this.reverseBits)
                {
                    this.crc32Table[(int)CRC32.ReverseBits(i)] = CRC32.ReverseBits(dwCrc);
                }
                else
                {
                    this.crc32Table[(int)i] = dwCrc;
                }
                i += 1;
            }
            while (i != 0);
        }

        // Token: 0x060004E1 RID: 1249 RVA: 0x00029034 File Offset: 0x00027234
        private uint gf2_matrix_times(uint[] matrix, uint vec)
        {
            uint sum = 0u;
            int i = 0;
            while (vec != 0u)
            {
                if ((vec & 1u) == 1u)
                {
                    sum ^= matrix[i];
                }
                vec >>= 1;
                i++;
            }
            return sum;
        }

        // Token: 0x060004E2 RID: 1250 RVA: 0x00029078 File Offset: 0x00027278
        private void gf2_matrix_square(uint[] square, uint[] mat)
        {
            for (int i = 0; i < 32; i++)
            {
                square[i] = this.gf2_matrix_times(mat, mat[i]);
            }
        }

        /// <summary>
        ///   Combines the given CRC32 value with the current running total.
        /// </summary>
        /// <remarks>
        ///   This is useful when using a divide-and-conquer approach to
        ///   calculating a CRC.  Multiple threads can each calculate a
        ///   CRC32 on a segment of the data, and then combine the
        ///   individual CRC32 values at the end.
        /// </remarks>
        /// <param name="crc">the crc value to be combined with this one</param>
        /// <param name="length">the length of data the CRC value was calculated on</param>
        // Token: 0x060004E3 RID: 1251 RVA: 0x000290A4 File Offset: 0x000272A4
        public void Combine(int crc, int length)
        {
            uint[] even = new uint[32];
            uint[] odd = new uint[32];
            if (length != 0)
            {
                uint crc2 = ~this._register;
                odd[0] = this.dwPolynomial;
                uint row = 1u;
                for (int i = 1; i < 32; i++)
                {
                    odd[i] = row;
                    row <<= 1;
                }
                this.gf2_matrix_square(even, odd);
                this.gf2_matrix_square(odd, even);
                uint len2 = (uint)length;
                do
                {
                    this.gf2_matrix_square(even, odd);
                    if ((len2 & 1u) == 1u)
                    {
                        crc2 = this.gf2_matrix_times(even, crc2);
                    }
                    len2 >>= 1;
                    if (len2 == 0u)
                    {
                        break;
                    }
                    this.gf2_matrix_square(odd, even);
                    if ((len2 & 1u) == 1u)
                    {
                        crc2 = this.gf2_matrix_times(odd, crc2);
                    }
                    len2 >>= 1;
                }
                while (len2 != 0u);
                crc2 ^= (uint)crc;
                this._register = ~crc2;
            }
        }

        /// <summary>
        ///   Create an instance of the CRC32 class using the default settings: no
        ///   bit reversal, and a polynomial of 0xEDB88320.
        /// </summary>
        // Token: 0x060004E4 RID: 1252 RVA: 0x000291A2 File Offset: 0x000273A2
        public CRC32() : this(false)
        {
        }

        /// <summary>
        ///   Create an instance of the CRC32 class, specifying whether to reverse
        ///   data bits or not.
        /// </summary>
        /// <param name="reverseBits">
        ///   specify true if the instance should reverse data bits.
        /// </param>
        /// <remarks>
        ///   <para>
        ///     In the CRC-32 used by BZip2, the bits are reversed. Therefore if you
        ///     want a CRC32 with compatibility with BZip2, you should pass true
        ///     here. In the CRC-32 used by GZIP and PKZIP, the bits are not
        ///     reversed; Therefore if you want a CRC32 with compatibility with
        ///     those, you should pass false.
        ///   </para>
        /// </remarks>
        // Token: 0x060004E5 RID: 1253 RVA: 0x000291AE File Offset: 0x000273AE
        public CRC32(bool reverseBits) : this(-306674912, reverseBits)
        {
        }

        /// <summary>
        ///   Create an instance of the CRC32 class, specifying the polynomial and
        ///   whether to reverse data bits or not.
        /// </summary>
        /// <param name="polynomial">
        ///   The polynomial to use for the CRC, expressed in the reversed (LSB)
        ///   format: the highest ordered bit in the polynomial value is the
        ///   coefficient of the 0th power; the second-highest order bit is the
        ///   coefficient of the 1 power, and so on. Expressed this way, the
        ///   polynomial for the CRC-32C used in IEEE 802.3, is 0xEDB88320.
        /// </param>
        /// <param name="reverseBits">
        ///   specify true if the instance should reverse data bits.
        /// </param>
        ///
        /// <remarks>
        ///   <para>
        ///     In the CRC-32 used by BZip2, the bits are reversed. Therefore if you
        ///     want a CRC32 with compatibility with BZip2, you should pass true
        ///     here for the <c>reverseBits</c> parameter. In the CRC-32 used by
        ///     GZIP and PKZIP, the bits are not reversed; Therefore if you want a
        ///     CRC32 with compatibility with those, you should pass false for the
        ///     <c>reverseBits</c> parameter.
        ///   </para>
        /// </remarks>
        // Token: 0x060004E6 RID: 1254 RVA: 0x000291BF File Offset: 0x000273BF
        public CRC32(int polynomial, bool reverseBits)
        {
            this.reverseBits = reverseBits;
            this.dwPolynomial = (uint)polynomial;
            this.GenerateLookupTable();
        }

        /// <summary>
        ///   Reset the CRC-32 class - clear the CRC "remainder register."
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Use this when employing a single instance of this class to compute
        ///     multiple, distinct CRCs on multiple, distinct data blocks.
        ///   </para>
        /// </remarks>
        // Token: 0x060004E7 RID: 1255 RVA: 0x000291E6 File Offset: 0x000273E6
        public void Reset()
        {
            this._register = uint.MaxValue;
        }

        // Token: 0x040003D3 RID: 979
        private const int BUFFER_SIZE = 8192;

        // Token: 0x040003D4 RID: 980
        private uint dwPolynomial;

        // Token: 0x040003D5 RID: 981
        private long _TotalBytesRead;

        // Token: 0x040003D6 RID: 982
        private bool reverseBits;

        // Token: 0x040003D7 RID: 983
        private uint[] crc32Table;

        // Token: 0x040003D8 RID: 984
        private uint _register = uint.MaxValue;
    }
}

namespace Ionic.Crc
{
    /// <summary>
    /// A Stream that calculates a CRC32 (a checksum) on all bytes read,
    /// or on all bytes written.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// This class can be used to verify the CRC of a ZipEntry when
    /// reading from a stream, or to calculate a CRC when writing to a
    /// stream.  The stream should be used to either read, or write, but
    /// not both.  If you intermix reads and writes, the results are not
    /// defined.
    /// </para>
    ///
    /// <para>
    /// This class is intended primarily for use internally by the
    /// DotNetZip library.
    /// </para>
    /// </remarks>
    // Token: 0x02000071 RID: 113
    public class CrcCalculatorStream : Stream, IDisposable
    {
        /// <summary>
        /// The default constructor.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Instances returned from this constructor will leave the underlying
        ///     stream open upon Close().  The stream uses the default CRC32
        ///     algorithm, which implies a polynomial of 0xEDB88320.
        ///   </para>
        /// </remarks>
        /// <param name="stream">The underlying stream</param>
        // Token: 0x060004E8 RID: 1256 RVA: 0x000291F0 File Offset: 0x000273F0
        public CrcCalculatorStream(Stream stream) : this(true, CrcCalculatorStream.UnsetLengthLimit, stream, null)
        {
        }

        /// <summary>
        ///   The constructor allows the caller to specify how to handle the
        ///   underlying stream at close.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     The stream uses the default CRC32 algorithm, which implies a
        ///     polynomial of 0xEDB88320.
        ///   </para>
        /// </remarks>
        /// <param name="stream">The underlying stream</param>
        /// <param name="leaveOpen">true to leave the underlying stream
        /// open upon close of the <c>CrcCalculatorStream</c>; false otherwise.</param>
        // Token: 0x060004E9 RID: 1257 RVA: 0x00029203 File Offset: 0x00027403
        public CrcCalculatorStream(Stream stream, bool leaveOpen) : this(leaveOpen, CrcCalculatorStream.UnsetLengthLimit, stream, null)
        {
        }

        /// <summary>
        ///   A constructor allowing the specification of the length of the stream
        ///   to read.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     The stream uses the default CRC32 algorithm, which implies a
        ///     polynomial of 0xEDB88320.
        ///   </para>
        ///   <para>
        ///     Instances returned from this constructor will leave the underlying
        ///     stream open upon Close().
        ///   </para>
        /// </remarks>
        /// <param name="stream">The underlying stream</param>
        /// <param name="length">The length of the stream to slurp</param>
        // Token: 0x060004EA RID: 1258 RVA: 0x00029218 File Offset: 0x00027418
        public CrcCalculatorStream(Stream stream, long length) : this(true, length, stream, null)
        {
            if (length < 0L)
            {
                throw new ArgumentException("length");
            }
        }

        /// <summary>
        ///   A constructor allowing the specification of the length of the stream
        ///   to read, as well as whether to keep the underlying stream open upon
        ///   Close().
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     The stream uses the default CRC32 algorithm, which implies a
        ///     polynomial of 0xEDB88320.
        ///   </para>
        /// </remarks>
        /// <param name="stream">The underlying stream</param>
        /// <param name="length">The length of the stream to slurp</param>
        /// <param name="leaveOpen">true to leave the underlying stream
        /// open upon close of the <c>CrcCalculatorStream</c>; false otherwise.</param>
        // Token: 0x060004EB RID: 1259 RVA: 0x0002924C File Offset: 0x0002744C
        public CrcCalculatorStream(Stream stream, long length, bool leaveOpen) : this(leaveOpen, length, stream, null)
        {
            if (length < 0L)
            {
                throw new ArgumentException("length");
            }
        }

        /// <summary>
        ///   A constructor allowing the specification of the length of the stream
        ///   to read, as well as whether to keep the underlying stream open upon
        ///   Close(), and the CRC32 instance to use.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     The stream uses the specified CRC32 instance, which allows the
        ///     application to specify how the CRC gets calculated.
        ///   </para>
        /// </remarks>
        /// <param name="stream">The underlying stream</param>
        /// <param name="length">The length of the stream to slurp</param>
        /// <param name="leaveOpen">true to leave the underlying stream
        /// open upon close of the <c>CrcCalculatorStream</c>; false otherwise.</param>
        /// <param name="crc32">the CRC32 instance to use to calculate the CRC32</param>
        // Token: 0x060004EC RID: 1260 RVA: 0x00029280 File Offset: 0x00027480
        public CrcCalculatorStream(Stream stream, long length, bool leaveOpen, CRC32 crc32) : this(leaveOpen, length, stream, crc32)
        {
            if (length < 0L)
            {
                throw new ArgumentException("length");
            }
        }

        // Token: 0x060004ED RID: 1261 RVA: 0x000292B2 File Offset: 0x000274B2
        private CrcCalculatorStream(bool leaveOpen, long length, Stream stream, CRC32 crc32)
        {
            this._innerStream = stream;
            this._Crc32 = (crc32 ?? new CRC32());
            this._lengthLimit = length;
            this._leaveOpen = leaveOpen;
        }

        /// <summary>
        ///   Gets the total number of bytes run through the CRC32 calculator.
        /// </summary>
        ///
        /// <remarks>
        ///   This is either the total number of bytes read, or the total number of
        ///   bytes written, depending on the direction of this stream.
        /// </remarks>
        // Token: 0x17000131 RID: 305
        // (get) Token: 0x060004EE RID: 1262 RVA: 0x000292EC File Offset: 0x000274EC
        public long TotalBytesSlurped
        {
            get
            {
                return this._Crc32.TotalBytesRead;
            }
        }

        /// <summary>
        ///   Provides the current CRC for all blocks slurped in.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     The running total of the CRC is kept as data is written or read
        ///     through the stream.  read this property after all reads or writes to
        ///     get an accurate CRC for the entire stream.
        ///   </para>
        /// </remarks>
        // Token: 0x17000132 RID: 306
        // (get) Token: 0x060004EF RID: 1263 RVA: 0x0002930C File Offset: 0x0002750C
        public int Crc
        {
            get
            {
                return this._Crc32.Crc32Result;
            }
        }

        /// <summary>
        ///   Indicates whether the underlying stream will be left open when the
        ///   <c>CrcCalculatorStream</c> is Closed.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Set this at any point before calling <see cref="M:Ionic.Crc.CrcCalculatorStream.Close" />.
        ///   </para>
        /// </remarks>
        // Token: 0x17000133 RID: 307
        // (get) Token: 0x060004F0 RID: 1264 RVA: 0x0002932C File Offset: 0x0002752C
        // (set) Token: 0x060004F1 RID: 1265 RVA: 0x00029344 File Offset: 0x00027544
        public bool LeaveOpen
        {
            get
            {
                return this._leaveOpen;
            }
            set
            {
                this._leaveOpen = value;
            }
        }

        /// <summary>
        /// Read from the stream
        /// </summary>
        /// <param name="buffer">the buffer to read</param>
        /// <param name="offset">the offset at which to start</param>
        /// <param name="count">the number of bytes to read</param>
        /// <returns>the number of bytes actually read</returns>
        // Token: 0x060004F2 RID: 1266 RVA: 0x00029350 File Offset: 0x00027550
        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesToRead = count;
            if (this._lengthLimit != CrcCalculatorStream.UnsetLengthLimit)
            {
                if (this._Crc32.TotalBytesRead >= this._lengthLimit)
                {
                    return 0;
                }
                long bytesRemaining = this._lengthLimit - this._Crc32.TotalBytesRead;
                if (bytesRemaining < (long)count)
                {
                    bytesToRead = (int)bytesRemaining;
                }
            }
            int i = this._innerStream.Read(buffer, offset, bytesToRead);
            if (i > 0)
            {
                this._Crc32.SlurpBlock(buffer, offset, i);
            }
            return i;
        }

        /// <summary>
        /// Write to the stream.
        /// </summary>
        /// <param name="buffer">the buffer from which to write</param>
        /// <param name="offset">the offset at which to start writing</param>
        /// <param name="count">the number of bytes to write</param>
        // Token: 0x060004F3 RID: 1267 RVA: 0x000293E8 File Offset: 0x000275E8
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count > 0)
            {
                this._Crc32.SlurpBlock(buffer, offset, count);
            }
            this._innerStream.Write(buffer, offset, count);
        }

        /// <summary>
        /// Indicates whether the stream supports reading.
        /// </summary>
        // Token: 0x17000134 RID: 308
        // (get) Token: 0x060004F4 RID: 1268 RVA: 0x00029420 File Offset: 0x00027620
        public override bool CanRead
        {
            get
            {
                return this._innerStream.CanRead;
            }
        }

        /// <summary>
        ///   Indicates whether the stream supports seeking.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Always returns false.
        ///   </para>
        /// </remarks>
        // Token: 0x17000135 RID: 309
        // (get) Token: 0x060004F5 RID: 1269 RVA: 0x00029440 File Offset: 0x00027640
        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Indicates whether the stream supports writing.
        /// </summary>
        // Token: 0x17000136 RID: 310
        // (get) Token: 0x060004F6 RID: 1270 RVA: 0x00029454 File Offset: 0x00027654
        public override bool CanWrite
        {
            get
            {
                return this._innerStream.CanWrite;
            }
        }

        /// <summary>
        /// Flush the stream.
        /// </summary>
        // Token: 0x060004F7 RID: 1271 RVA: 0x00029471 File Offset: 0x00027671
        public override void Flush()
        {
            this._innerStream.Flush();
        }

        /// <summary>
        ///   Returns the length of the underlying stream.
        /// </summary>
        // Token: 0x17000137 RID: 311
        // (get) Token: 0x060004F8 RID: 1272 RVA: 0x00029480 File Offset: 0x00027680
        public override long Length
        {
            get
            {
                long result;
                if (this._lengthLimit == CrcCalculatorStream.UnsetLengthLimit)
                {
                    result = this._innerStream.Length;
                }
                else
                {
                    result = this._lengthLimit;
                }
                return result;
            }
        }

        /// <summary>
        ///   The getter for this property returns the total bytes read.
        ///   If you use the setter, it will throw
        /// <see cref="T:System.NotSupportedException" />.
        /// </summary>
        // Token: 0x17000138 RID: 312
        // (get) Token: 0x060004F9 RID: 1273 RVA: 0x000294BC File Offset: 0x000276BC
        // (set) Token: 0x060004FA RID: 1274 RVA: 0x000294D9 File Offset: 0x000276D9
        public override long Position
        {
            get
            {
                return this._Crc32.TotalBytesRead;
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Seeking is not supported on this stream. This method always throws
        /// <see cref="T:System.NotSupportedException" />
        /// </summary>
        /// <param name="offset">N/A</param>
        /// <param name="origin">N/A</param>
        /// <returns>N/A</returns>
        // Token: 0x060004FB RID: 1275 RVA: 0x000294E1 File Offset: 0x000276E1
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// This method always throws
        /// <see cref="T:System.NotSupportedException" />
        /// </summary>
        /// <param name="value">N/A</param>
        // Token: 0x060004FC RID: 1276 RVA: 0x000294E9 File Offset: 0x000276E9
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        // Token: 0x060004FD RID: 1277 RVA: 0x000294F1 File Offset: 0x000276F1
        void IDisposable.Dispose()
        {
            this.Close();
        }

        /// <summary>
        /// Closes the stream.
        /// </summary>
        // Token: 0x060004FE RID: 1278 RVA: 0x000294FC File Offset: 0x000276FC
        public override void Close()
        {
            base.Close();
            if (!this._leaveOpen)
            {
                this._innerStream.Close();
            }
        }

        // Token: 0x040003D9 RID: 985
        private static readonly long UnsetLengthLimit = -99L;

        // Token: 0x040003DA RID: 986
        internal Stream _innerStream;

        // Token: 0x040003DB RID: 987
        private CRC32 _Crc32;

        // Token: 0x040003DC RID: 988
        private long _lengthLimit = -99L;

        // Token: 0x040003DD RID: 989
        private bool _leaveOpen;
    }
}

using System;
using System.IO;
using System.Text;
using System.ComponentModel;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Ionic.Zip;

namespace Ionic
{
    // Token: 0x02000015 RID: 21
    internal class AttributesCriterion : SelectionCriterion
    {
        // Token: 0x17000044 RID: 68
        // (get) Token: 0x060000B2 RID: 178 RVA: 0x000045EC File Offset: 0x000027EC
        // (set) Token: 0x060000B3 RID: 179 RVA: 0x000046B0 File Offset: 0x000028B0
        internal string AttributeString
        {
            get
            {
                string result = "";
                if (((int)this._Attributes & 2) != 0)
                {
                    result += "H";
                }
                if (((int)this._Attributes & 4) != 0)
                {
                    result += "S";
                }
                if (((int)this._Attributes & 1) != 0)
                {
                    result += "R";
                }
                if (((int)this._Attributes & 32) != 0)
                {
                    result += "A";
                }
                if (((int)this._Attributes & 1024) != 0)
                {
                    result += "L";
                }
                if (((int)this._Attributes & 8192) != 0)
                {
                    result += "I";
                }
                return result;
            }
            set
            {
                this._Attributes = (FileAttributes) 128;
                foreach (char c in value.ToUpper())
                {
                    char c2 = c;
                    if (c2 != 'A')
                    {
                        switch (c2)
                        {
                            case 'H':
                                if (((int)this._Attributes & 2) != 0)
                                {
                                    throw new ArgumentException(string.Format("Repeated flag. ({0})", c), "value");
                                }
                                this._Attributes |= (FileAttributes)2;
                                goto IL_1EE;
                            case 'I':
                                if ((this._Attributes & (FileAttributes)8192) != 0)
                                {
                                    throw new ArgumentException(string.Format("Repeated flag. ({0})", c), "value");
                                }
                                this._Attributes |= (FileAttributes)8192;
                                goto IL_1EE;
                            case 'J':
                            case 'K':
                                break;
                            case 'L':
                                if ((this._Attributes & (FileAttributes)1024) != 0)
                                {
                                    throw new ArgumentException(string.Format("Repeated flag. ({0})", c), "value");
                                }
                                this._Attributes |= (FileAttributes)1024;
                                goto IL_1EE;
                            default:
                                switch (c2)
                                {
                                    case 'R':
                                        if ((this._Attributes & (FileAttributes)1) != 0)
                                        {
                                            throw new ArgumentException(string.Format("Repeated flag. ({0})", c), "value");
                                        }
                                        this._Attributes |= (FileAttributes)1;
                                        goto IL_1EE;
                                    case 'S':
                                        if ((this._Attributes & (FileAttributes)4) != 0)
                                        {
                                            throw new ArgumentException(string.Format("Repeated flag. ({0})", c), "value");
                                        }
                                        this._Attributes |= (FileAttributes)4;
                                        goto IL_1EE;
                                }
                                break;
                        }
                        throw new ArgumentException(value);
                    }
                    if ((this._Attributes & (FileAttributes)32) != 0)
                    {
                        throw new ArgumentException(string.Format("Repeated flag. ({0})", c), "value");
                    }
                    this._Attributes |= (FileAttributes)32;
                    IL_1EE:;
                }
            }
        }

        // Token: 0x060000B4 RID: 180 RVA: 0x000048C4 File Offset: 0x00002AC4
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("attributes ").Append(EnumUtil.GetDescription(this.Operator)).Append(" ").Append(this.AttributeString);
            return sb.ToString();
        }

        // Token: 0x060000B5 RID: 181 RVA: 0x00004918 File Offset: 0x00002B18
        private bool _EvaluateOne(FileAttributes fileAttrs, FileAttributes criterionAttrs)
        {
            return (this._Attributes & criterionAttrs) != criterionAttrs || (fileAttrs & criterionAttrs) == criterionAttrs;
        }

        // Token: 0x060000B6 RID: 182 RVA: 0x0000494C File Offset: 0x00002B4C
        internal override bool Evaluate(string filename)
        {
            bool result;
            if (Directory.Exists(filename))
            {
                result = (this.Operator != ComparisonOperator.EqualTo);
            }
            else
            {
                FileAttributes fileAttrs = File.GetAttributes(filename);
                result = this._Evaluate(fileAttrs);
            }
            return result;
        }

        // Token: 0x060000B7 RID: 183 RVA: 0x0000498C File Offset: 0x00002B8C
        private bool _Evaluate(FileAttributes fileAttrs)
        {
            bool result = this._EvaluateOne(fileAttrs, (FileAttributes)2);
            if (result)
            {
                result = this._EvaluateOne(fileAttrs, (FileAttributes)4);
            }
            if (result)
            {
                result = this._EvaluateOne(fileAttrs, (FileAttributes)1);
            }
            if (result)
            {
                result = this._EvaluateOne(fileAttrs, (FileAttributes)32);
            }
            if (result)
            {
                result = this._EvaluateOne(fileAttrs, (FileAttributes)8192);
            }
            if (result)
            {
                result = this._EvaluateOne(fileAttrs, (FileAttributes)1024);
            }
            if (this.Operator != ComparisonOperator.EqualTo)
            {
                result = !result;
            }
            return result;
        }

        // Token: 0x060000B8 RID: 184 RVA: 0x00004A18 File Offset: 0x00002C18
        internal override bool Evaluate(ZipEntry entry)
        {
            FileAttributes fileAttrs = entry.Attributes;
            return this._Evaluate(fileAttrs);
        }

        // Token: 0x04000077 RID: 119
        private FileAttributes _Attributes;

        // Token: 0x04000078 RID: 120
        internal ComparisonOperator Operator;
    }
}

namespace Ionic
{
    // Token: 0x0200000F RID: 15
    internal enum ComparisonOperator
    {
        // Token: 0x04000065 RID: 101
        [Description(">")]
        GreaterThan,
        // Token: 0x04000066 RID: 102
        [Description(">=")]
        GreaterThanOrEqualTo,
        // Token: 0x04000067 RID: 103
        [Description("<")]
        LesserThan,
        // Token: 0x04000068 RID: 104
        [Description("<=")]
        LesserThanOrEqualTo,
        // Token: 0x04000069 RID: 105
        [Description("=")]
        EqualTo,
        // Token: 0x0400006A RID: 106
        [Description("!=")]
        NotEqualTo
    }
}


namespace Ionic
{
    // Token: 0x02000016 RID: 22
    internal class CompoundCriterion : SelectionCriterion
    {
        // Token: 0x17000045 RID: 69
        // (get) Token: 0x060000BA RID: 186 RVA: 0x00004A40 File Offset: 0x00002C40
        // (set) Token: 0x060000BB RID: 187 RVA: 0x00004A58 File Offset: 0x00002C58
        internal SelectionCriterion Right
        {
            get
            {
                return this._Right;
            }
            set
            {
                this._Right = value;
                if (value == null)
                {
                    this.Conjunction = LogicalConjunction.NONE;
                }
                else if (this.Conjunction == LogicalConjunction.NONE)
                {
                    this.Conjunction = LogicalConjunction.AND;
                }
            }
        }

        // Token: 0x060000BC RID: 188 RVA: 0x00004A98 File Offset: 0x00002C98
        internal override bool Evaluate(string filename)
        {
            bool result = this.Left.Evaluate(filename);
            switch (this.Conjunction)
            {
                case LogicalConjunction.AND:
                    if (result)
                    {
                        result = this.Right.Evaluate(filename);
                    }
                    break;
                case LogicalConjunction.OR:
                    if (!result)
                    {
                        result = this.Right.Evaluate(filename);
                    }
                    break;
                case LogicalConjunction.XOR:
                    result ^= this.Right.Evaluate(filename);
                    break;
                default:
                    throw new ArgumentException("Conjunction");
            }
            return result;
        }

        // Token: 0x060000BD RID: 189 RVA: 0x00004B1C File Offset: 0x00002D1C
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("(").Append((this.Left != null) ? this.Left.ToString() : "null").Append(" ").Append(this.Conjunction.ToString()).Append(" ").Append((this.Right != null) ? this.Right.ToString() : "null").Append(")");
            return sb.ToString();
        }

        // Token: 0x060000BE RID: 190 RVA: 0x00004BB8 File Offset: 0x00002DB8
        internal override bool Evaluate(ZipEntry entry)
        {
            bool result = this.Left.Evaluate(entry);
            switch (this.Conjunction)
            {
                case LogicalConjunction.AND:
                    if (result)
                    {
                        result = this.Right.Evaluate(entry);
                    }
                    break;
                case LogicalConjunction.OR:
                    if (!result)
                    {
                        result = this.Right.Evaluate(entry);
                    }
                    break;
                case LogicalConjunction.XOR:
                    result ^= this.Right.Evaluate(entry);
                    break;
            }
            return result;
        }

        // Token: 0x04000079 RID: 121
        internal LogicalConjunction Conjunction;

        // Token: 0x0400007A RID: 122
        internal SelectionCriterion Left;

        // Token: 0x0400007B RID: 123
        private SelectionCriterion _Right;
    }
}


namespace Ionic
{
    /// <summary>
    /// Summary description for EnumUtil.
    /// </summary>
    // Token: 0x0200001A RID: 26
    internal sealed class EnumUtil
    {
        // Token: 0x060000D1 RID: 209 RVA: 0x00005C82 File Offset: 0x00003E82
        private EnumUtil()
        {
        }

        /// <summary>
        ///   Returns the value of the DescriptionAttribute if the specified Enum
        ///   value has one.  If not, returns the ToString() representation of the
        ///   Enum value.
        /// </summary>
        /// <param name="value">The Enum to get the description for</param>
        /// <returns></returns>
        // Token: 0x060000D2 RID: 210 RVA: 0x00005C90 File Offset: 0x00003E90
        internal static string GetDescription(Enum value)
        {
            FieldInfo fi = value.GetType().GetField(value.ToString());
            DescriptionAttribute[] attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
            string result;
            if (attributes.Length > 0)
            {
                result = attributes[0].Description;
            }
            else
            {
                result = value.ToString();
            }
            return result;
        }

        /// <summary>
        ///   Converts the string representation of the name or numeric value of one
        ///   or more enumerated constants to an equivalent enumerated object.
        ///   Note: use the DescriptionAttribute on enum values to enable this.
        /// </summary>
        /// <param name="enumType">The System.Type of the enumeration.</param>
        /// <param name="stringRepresentation">
        ///   A string containing the name or value to convert.
        /// </param>
        /// <returns></returns>
        // Token: 0x060000D3 RID: 211 RVA: 0x00005CEC File Offset: 0x00003EEC
        internal static object Parse(Type enumType, string stringRepresentation)
        {
            return EnumUtil.Parse(enumType, stringRepresentation, false);
        }

        /// <summary>
        ///   Converts the string representation of the name or numeric value of one
        ///   or more enumerated constants to an equivalent enumerated object.  A
        ///   parameter specified whether the operation is case-sensitive.  Note:
        ///   use the DescriptionAttribute on enum values to enable this.
        /// </summary>
        /// <param name="enumType">The System.Type of the enumeration.</param>
        /// <param name="stringRepresentation">
        ///   A string containing the name or value to convert.
        /// </param>
        /// <param name="ignoreCase">
        ///   Whether the operation is case-sensitive or not.</param>
        /// <returns></returns>
        // Token: 0x060000D4 RID: 212 RVA: 0x00005D08 File Offset: 0x00003F08
        internal static object Parse(Type enumType, string stringRepresentation, bool ignoreCase)
        {
            if (ignoreCase)
            {
                stringRepresentation = stringRepresentation.ToLower();
            }
            foreach (object obj in Enum.GetValues(enumType))
            {
                Enum enumVal = (Enum)obj;
                string description = EnumUtil.GetDescription(enumVal);
                if (ignoreCase)
                {
                    description = description.ToLower();
                }
                if (description == stringRepresentation)
                {
                    return enumVal;
                }
            }
            return Enum.Parse(enumType, stringRepresentation, ignoreCase);
        }
    }
}


namespace Ionic
{
    /// <summary>
    ///   FileSelector encapsulates logic that selects files from a source - a zip file
    ///   or the filesystem - based on a set of criteria.  This class is used internally
    ///   by the DotNetZip library, in particular for the AddSelectedFiles() methods.
    ///   This class can also be used independently of the zip capability in DotNetZip.
    /// </summary>
    ///
    /// <remarks>
    ///
    /// <para>
    ///   The FileSelector class is used internally by the ZipFile class for selecting
    ///   files for inclusion into the ZipFile, when the <see cref="M:Ionic.Zip.ZipFile.AddSelectedFiles(System.String,System.String)" /> method, or one of
    ///   its overloads, is called.  It's also used for the <see cref="M:Ionic.Zip.ZipFile.ExtractSelectedEntries(System.String)" /> methods.  Typically, an
    ///   application that creates or manipulates Zip archives will not directly
    ///   interact with the FileSelector class.
    /// </para>
    ///
    /// <para>
    ///   Some applications may wish to use the FileSelector class directly, to
    ///   select files from disk volumes based on a set of criteria, without creating or
    ///   querying Zip archives.  The file selection criteria include: a pattern to
    ///   match the filename; the last modified, created, or last accessed time of the
    ///   file; the size of the file; and the attributes of the file.
    /// </para>
    ///
    /// <para>
    ///   Consult the documentation for <see cref="P:Ionic.FileSelector.SelectionCriteria" />
    ///   for more information on specifying the selection criteria.
    /// </para>
    ///
    /// </remarks>
    // Token: 0x02000017 RID: 23
    public class FileSelector
    {
        /// <summary>
        ///   Constructor that allows the caller to specify file selection criteria.
        /// </summary>
        ///
        /// <remarks>
        /// <para>
        ///   This constructor allows the caller to specify a set of criteria for
        ///   selection of files.
        /// </para>
        ///
        /// <para>
        ///   See <see cref="P:Ionic.FileSelector.SelectionCriteria" /> for a description of
        ///   the syntax of the selectionCriteria string.
        /// </para>
        ///
        /// <para>
        ///   By default the FileSelector will traverse NTFS Reparse Points.  To
        ///   change this, use <see cref="M:Ionic.FileSelector.#ctor(System.String,System.Boolean)">FileSelector(String, bool)</see>.
        /// </para>
        /// </remarks>
        ///
        /// <param name="selectionCriteria">The criteria for file selection.</param>
        // Token: 0x060000C0 RID: 192 RVA: 0x00004C39 File Offset: 0x00002E39
        public FileSelector(string selectionCriteria) : this(selectionCriteria, true)
        {
        }

        /// <summary>
        ///   Constructor that allows the caller to specify file selection criteria.
        /// </summary>
        ///
        /// <remarks>
        /// <para>
        ///   This constructor allows the caller to specify a set of criteria for
        ///   selection of files.
        /// </para>
        ///
        /// <para>
        ///   See <see cref="P:Ionic.FileSelector.SelectionCriteria" /> for a description of
        ///   the syntax of the selectionCriteria string.
        /// </para>
        /// </remarks>
        ///
        /// <param name="selectionCriteria">The criteria for file selection.</param>
        /// <param name="traverseDirectoryReparsePoints">
        /// whether to traverse NTFS reparse points (junctions).
        /// </param>
        // Token: 0x060000C1 RID: 193 RVA: 0x00004C48 File Offset: 0x00002E48
        public FileSelector(string selectionCriteria, bool traverseDirectoryReparsePoints)
        {
            if (!string.IsNullOrEmpty(selectionCriteria))
            {
                this._Criterion = FileSelector._ParseCriterion(selectionCriteria);
            }
            this.TraverseReparsePoints = traverseDirectoryReparsePoints;
        }

        /// <summary>
        ///   The string specifying which files to include when retrieving.
        /// </summary>
        /// <remarks>
        ///
        /// <para>
        ///   Specify the criteria in statements of 3 elements: a noun, an operator,
        ///   and a value.  Consider the string "name != *.doc" .  The noun is
        ///   "name".  The operator is "!=", implying "Not Equal".  The value is
        ///   "*.doc".  That criterion, in English, says "all files with a name that
        ///   does not end in the .doc extension."
        /// </para>
        ///
        /// <para>
        ///   Supported nouns include "name" (or "filename") for the filename;
        ///   "atime", "mtime", and "ctime" for last access time, last modfied time,
        ///   and created time of the file, respectively; "attributes" (or "attrs")
        ///   for the file attributes; "size" (or "length") for the file length
        ///   (uncompressed); and "type" for the type of object, either a file or a
        ///   directory.  The "attributes", "type", and "name" nouns all support =
        ///   and != as operators.  The "size", "atime", "mtime", and "ctime" nouns
        ///   support = and !=, and &gt;, &gt;=, &lt;, &lt;= as well.  The times are
        ///   taken to be expressed in local time.
        /// </para>
        ///
        /// <para>
        ///   Specify values for the file attributes as a string with one or more of
        ///   the characters H,R,S,A,I,L in any order, implying file attributes of
        ///   Hidden, ReadOnly, System, Archive, NotContextIndexed, and ReparsePoint
        ///   (symbolic link) respectively.
        /// </para>
        ///
        /// <para>
        ///   To specify a time, use YYYY-MM-DD-HH:mm:ss or YYYY/MM/DD-HH:mm:ss as
        ///   the format.  If you omit the HH:mm:ss portion, it is assumed to be
        ///   00:00:00 (midnight).
        /// </para>
        ///
        /// <para>
        ///   The value for a size criterion is expressed in integer quantities of
        ///   bytes, kilobytes (use k or kb after the number), megabytes (m or mb),
        ///   or gigabytes (g or gb).
        /// </para>
        ///
        /// <para>
        ///   The value for a name is a pattern to match against the filename,
        ///   potentially including wildcards.  The pattern follows CMD.exe glob
        ///   rules: * implies one or more of any character, while ?  implies one
        ///   character.  If the name pattern contains any slashes, it is matched to
        ///   the entire filename, including the path; otherwise, it is matched
        ///   against only the filename without the path.  This means a pattern of
        ///   "*\*.*" matches all files one directory level deep, while a pattern of
        ///   "*.*" matches all files in all directories.
        /// </para>
        ///
        /// <para>
        ///   To specify a name pattern that includes spaces, use single quotes
        ///   around the pattern.  A pattern of "'* *.*'" will match all files that
        ///   have spaces in the filename.  The full criteria string for that would
        ///   be "name = '* *.*'" .
        /// </para>
        ///
        /// <para>
        ///   The value for a type criterion is either F (implying a file) or D
        ///   (implying a directory).
        /// </para>
        ///
        /// <para>
        ///   Some examples:
        /// </para>
        ///
        /// <list type="table">
        ///   <listheader>
        ///     <term>criteria</term>
        ///     <description>Files retrieved</description>
        ///   </listheader>
        ///
        ///   <item>
        ///     <term>name != *.xls </term>
        ///     <description>any file with an extension that is not .xls
        ///     </description>
        ///   </item>
        ///
        ///   <item>
        ///     <term>name = *.mp3 </term>
        ///     <description>any file with a .mp3 extension.
        ///     </description>
        ///   </item>
        ///
        ///   <item>
        ///     <term>*.mp3</term>
        ///     <description>(same as above) any file with a .mp3 extension.
        ///     </description>
        ///   </item>
        ///
        ///   <item>
        ///     <term>attributes = A </term>
        ///     <description>all files whose attributes include the Archive bit.
        ///     </description>
        ///   </item>
        ///
        ///   <item>
        ///     <term>attributes != H </term>
        ///     <description>all files whose attributes do not include the Hidden bit.
        ///     </description>
        ///   </item>
        ///
        ///   <item>
        ///     <term>mtime &gt; 2009-01-01</term>
        ///     <description>all files with a last modified time after January 1st, 2009.
        ///     </description>
        ///   </item>
        ///
        ///   <item>
        ///     <term>ctime &gt; 2009/01/01-03:00:00</term>
        ///     <description>all files with a created time after 3am (local time),
        ///     on January 1st, 2009.
        ///     </description>
        ///   </item>
        ///
        ///   <item>
        ///     <term>size &gt; 2gb</term>
        ///     <description>all files whose uncompressed size is greater than 2gb.
        ///     </description>
        ///   </item>
        ///
        ///   <item>
        ///     <term>type = D</term>
        ///     <description>all directories in the filesystem. </description>
        ///   </item>
        ///
        /// </list>
        ///
        /// <para>
        ///   You can combine criteria with the conjunctions AND, OR, and XOR. Using
        ///   a string like "name = *.txt AND size &gt;= 100k" for the
        ///   selectionCriteria retrieves entries whose names end in .txt, and whose
        ///   uncompressed size is greater than or equal to 100 kilobytes.
        /// </para>
        ///
        /// <para>
        ///   For more complex combinations of criteria, you can use parenthesis to
        ///   group clauses in the boolean logic.  Absent parenthesis, the
        ///   precedence of the criterion atoms is determined by order of
        ///   appearance.  Unlike the C# language, the AND conjunction does not take
        ///   precendence over the logical OR.  This is important only in strings
        ///   that contain 3 or more criterion atoms.  In other words, "name = *.txt
        ///   and size &gt; 1000 or attributes = H" implies "((name = *.txt AND size
        ///   &gt; 1000) OR attributes = H)" while "attributes = H OR name = *.txt
        ///   and size &gt; 1000" evaluates to "((attributes = H OR name = *.txt)
        ///   AND size &gt; 1000)".  When in doubt, use parenthesis.
        /// </para>
        ///
        /// <para>
        ///   Using time properties requires some extra care. If you want to
        ///   retrieve all entries that were last updated on 2009 February 14,
        ///   specify "mtime &gt;= 2009-02-14 AND mtime &lt; 2009-02-15".  Read this
        ///   to say: all files updated after 12:00am on February 14th, until
        ///   12:00am on February 15th.  You can use the same bracketing approach to
        ///   specify any time period - a year, a month, a week, and so on.
        /// </para>
        ///
        /// <para>
        ///   The syntax allows one special case: if you provide a string with no
        ///   spaces, it is treated as a pattern to match for the filename.
        ///   Therefore a string like "*.xls" will be equivalent to specifying "name
        ///   = *.xls".  This "shorthand" notation does not work with compound
        ///   criteria.
        /// </para>
        ///
        /// <para>
        ///   There is no logic in this class that insures that the inclusion
        ///   criteria are internally consistent.  For example, it's possible to
        ///   specify criteria that says the file must have a size of less than 100
        ///   bytes, as well as a size that is greater than 1000 bytes.  Obviously
        ///   no file will ever satisfy such criteria, but this class does not check
        ///   for or detect such inconsistencies.
        /// </para>
        ///
        /// </remarks>
        ///
        /// <exception cref="T:System.Exception">
        ///   Thrown in the setter if the value has an invalid syntax.
        /// </exception>
        // Token: 0x17000046 RID: 70
        // (get) Token: 0x060000C2 RID: 194 RVA: 0x00004C7C File Offset: 0x00002E7C
        // (set) Token: 0x060000C3 RID: 195 RVA: 0x00004CB0 File Offset: 0x00002EB0
        public string SelectionCriteria
        {
            get
            {
                string result;
                if (this._Criterion == null)
                {
                    result = null;
                }
                else
                {
                    result = this._Criterion.ToString();
                }
                return result;
            }
            set
            {
                if (value == null)
                {
                    this._Criterion = null;
                }
                else if (value.Trim() == "")
                {
                    this._Criterion = null;
                }
                else
                {
                    this._Criterion = FileSelector._ParseCriterion(value);
                }
            }
        }

        /// <summary>
        ///  Indicates whether searches will traverse NTFS reparse points, like Junctions.
        /// </summary>
        // Token: 0x17000047 RID: 71
        // (get) Token: 0x060000C4 RID: 196 RVA: 0x00004D00 File Offset: 0x00002F00
        // (set) Token: 0x060000C5 RID: 197 RVA: 0x00004D17 File Offset: 0x00002F17
        public bool TraverseReparsePoints { get; set; }

        // Token: 0x060000C6 RID: 198 RVA: 0x00004D20 File Offset: 0x00002F20
        private static string NormalizeCriteriaExpression(string source)
        {
            string[][] prPairs = new string[][]
            {
                new string[]
                {
                    "([^']*)\\(\\(([^']+)",
                    "$1( ($2"
                },
                new string[]
                {
                    "(.)\\)\\)",
                    "$1) )"
                },
                new string[]
                {
                    "\\((\\S)",
                    "( $1"
                },
                new string[]
                {
                    "(\\S)\\)",
                    "$1 )"
                },
                new string[]
                {
                    "^\\)",
                    " )"
                },
                new string[]
                {
                    "(\\S)\\(",
                    "$1 ("
                },
                new string[]
                {
                    "\\)(\\S)",
                    ") $1"
                },
                new string[]
                {
                    "(=)('[^']*')",
                    "$1 $2"
                },
                new string[]
                {
                    "([^ !><])(>|<|!=|=)",
                    "$1 $2"
                },
                new string[]
                {
                    "(>|<|!=|=)([^ =])",
                    "$1 $2"
                },
                new string[]
                {
                    "/",
                    "\\"
                }
            };
            string interim = source;
            for (int i = 0; i < prPairs.Length; i++)
            {
                string pattern = FileSelector.RegexAssertions.PrecededByEvenNumberOfSingleQuotes + prPairs[i][0] + FileSelector.RegexAssertions.FollowedByEvenNumberOfSingleQuotesAndLineEnd;
                interim = Regex.Replace(interim, pattern, prPairs[i][1]);
            }
            string regexPattern = "/" + FileSelector.RegexAssertions.FollowedByOddNumberOfSingleQuotesAndLineEnd;
            interim = Regex.Replace(interim, regexPattern, "\\");
            regexPattern = " " + FileSelector.RegexAssertions.FollowedByOddNumberOfSingleQuotesAndLineEnd;
            return Regex.Replace(interim, regexPattern, "\u0006");
        }

        // Token: 0x060000C7 RID: 199 RVA: 0x00004F1C File Offset: 0x0000311C
        private static SelectionCriterion _ParseCriterion(string s)
        {
            SelectionCriterion result;
            if (s == null)
            {
                result = null;
            }
            else
            {
                s = FileSelector.NormalizeCriteriaExpression(s);
                if (s.IndexOf(" ") == -1)
                {
                    s = "name = " + s;
                }
                string[] tokens = s.Trim().Split(new char[]
                {
                    ' ',
                    '\t'
                });
                if (tokens.Length < 3)
                {
                    throw new ArgumentException(s);
                }
                SelectionCriterion current = null;
                Stack<FileSelector.ParseState> stateStack = new Stack<FileSelector.ParseState>();
                Stack<SelectionCriterion> critStack = new Stack<SelectionCriterion>();
                stateStack.Push(FileSelector.ParseState.Start);
                int i = 0;
                while (i < tokens.Length)
                {
                    string tok = tokens[i].ToLower();
                    string text = tok;/*
                    if (text != null)
                    {
                        if (< PrivateImplementationDetails >{ 117900FC - 6796 - 45E1 - 84B2 - B7C6A19AFDAF}.$$method0x60000c7 - 1 == null)
						{

                            < PrivateImplementationDetails >{ 117900FC - 6796 - 45E1 - 84B2 - B7C6A19AFDAF}.$$method0x60000c7 - 1 = new Dictionary<string, int>(16)
                            {
                                {
                                    "and",
                                    0
                                },
                                {
                                    "xor",
                                    1
                                },
                                {
                                    "or",
                                    2
                                },
                                {
                                    "(",
                                    3
                                },
                                {
                                    ")",
                                    4
                                },
                                {
                                    "atime",
                                    5
                                },
                                {
                                    "ctime",
                                    6
                                },
                                {
                                    "mtime",
                                    7
                                },
                                {
                                    "length",
                                    8
                                },
                                {
                                    "size",
                                    9
                                },
                                {
                                    "filename",
                                    10
                                },
                                {
                                    "name",
                                    11
                                },
                                {
                                    "attrs",
                                    12
                                },
                                {
                                    "attributes",
                                    13
                                },
                                {
                                    "type",
                                    14
                                },
                                {
                                    "",
                                    15
                                }
                            };
                        }
                        int num;
                        if (< PrivateImplementationDetails >{ 117900FC - 6796 - 45E1 - 84B2 - B7C6A19AFDAF}.$$method0x60000c7 - 1.TryGetValue(text, out num))
						{
                            FileSelector.ParseState state;
                            switch (num)
                            {
                                case 0:
                                case 1:
                                case 2:
                                    {
                                        state = stateStack.Peek();
                                        if (state != FileSelector.ParseState.CriterionDone)
                                        {
                                            throw new ArgumentException(string.Join(" ", tokens, i, tokens.Length - i));
                                        }
                                        if (tokens.Length <= i + 3)
                                        {
                                            throw new ArgumentException(string.Join(" ", tokens, i, tokens.Length - i));
                                        }
                                        LogicalConjunction pendingConjunction = (LogicalConjunction)Enum.Parse(typeof(LogicalConjunction), tokens[i].ToUpper(), true);
                                        current = new CompoundCriterion
                                        {
                                            Left = current,
                                            Right = null,
                                            Conjunction = pendingConjunction
                                        };
                                        stateStack.Push(state);
                                        stateStack.Push(FileSelector.ParseState.ConjunctionPending);
                                        critStack.Push(current);
                                        break;
                                    }
                                case 3:
                                    state = stateStack.Peek();
                                    if (state != FileSelector.ParseState.Start && state != FileSelector.ParseState.ConjunctionPending && state != FileSelector.ParseState.OpenParen)
                                    {
                                        throw new ArgumentException(string.Join(" ", tokens, i, tokens.Length - i));
                                    }
                                    if (tokens.Length <= i + 4)
                                    {
                                        throw new ArgumentException(string.Join(" ", tokens, i, tokens.Length - i));
                                    }
                                    stateStack.Push(FileSelector.ParseState.OpenParen);
                                    break;
                                case 4:
                                    state = stateStack.Pop();
                                    if (stateStack.Peek() != FileSelector.ParseState.OpenParen)
                                    {
                                        throw new ArgumentException(string.Join(" ", tokens, i, tokens.Length - i));
                                    }
                                    stateStack.Pop();
                                    stateStack.Push(FileSelector.ParseState.CriterionDone);
                                    break;
                                case 5:
                                case 6:
                                case 7:
                                    {
                                        if (tokens.Length <= i + 2)
                                        {
                                            throw new ArgumentException(string.Join(" ", tokens, i, tokens.Length - i));
                                        }
                                        DateTime t;
                                        try
                                        {
                                            t = DateTime.ParseExact(tokens[i + 2], "yyyy-MM-dd-HH:mm:ss", null);
                                        }
                                        catch (FormatException)
                                        {
                                            try
                                            {
                                                t = DateTime.ParseExact(tokens[i + 2], "yyyy/MM/dd-HH:mm:ss", null);
                                            }
                                            catch (FormatException)
                                            {
                                                try
                                                {
                                                    t = DateTime.ParseExact(tokens[i + 2], "yyyy/MM/dd", null);
                                                }
                                                catch (FormatException)
                                                {
                                                    try
                                                    {
                                                        t = DateTime.ParseExact(tokens[i + 2], "MM/dd/yyyy", null);
                                                    }
                                                    catch (FormatException)
                                                    {
                                                        t = DateTime.ParseExact(tokens[i + 2], "yyyy-MM-dd", null);
                                                    }
                                                }
                                            }
                                        }
                                        t = DateTime.SpecifyKind(t, DateTimeKind.Local).ToUniversalTime();
                                        current = new TimeCriterion
                                        {
                                            Which = (WhichTime)Enum.Parse(typeof(WhichTime), tokens[i], true),
                                            Operator = (ComparisonOperator)EnumUtil.Parse(typeof(ComparisonOperator), tokens[i + 1]),
                                            Time = t
                                        };
                                        i += 2;
                                        stateStack.Push(FileSelector.ParseState.CriterionDone);
                                        break;
                                    }
                                case 8:
                                case 9:
                                    {
                                        if (tokens.Length <= i + 2)
                                        {
                                            throw new ArgumentException(string.Join(" ", tokens, i, tokens.Length - i));
                                        }
                                        string v = tokens[i + 2];
                                        long sz;
                                        if (v.ToUpper().EndsWith("K"))
                                        {
                                            sz = long.Parse(v.Substring(0, v.Length - 1)) * 1024L;
                                        }
                                        else if (v.ToUpper().EndsWith("KB"))
                                        {
                                            sz = long.Parse(v.Substring(0, v.Length - 2)) * 1024L;
                                        }
                                        else if (v.ToUpper().EndsWith("M"))
                                        {
                                            sz = long.Parse(v.Substring(0, v.Length - 1)) * 1024L * 1024L;
                                        }
                                        else if (v.ToUpper().EndsWith("MB"))
                                        {
                                            sz = long.Parse(v.Substring(0, v.Length - 2)) * 1024L * 1024L;
                                        }
                                        else if (v.ToUpper().EndsWith("G"))
                                        {
                                            sz = long.Parse(v.Substring(0, v.Length - 1)) * 1024L * 1024L * 1024L;
                                        }
                                        else if (v.ToUpper().EndsWith("GB"))
                                        {
                                            sz = long.Parse(v.Substring(0, v.Length - 2)) * 1024L * 1024L * 1024L;
                                        }
                                        else
                                        {
                                            sz = long.Parse(tokens[i + 2]);
                                        }
                                        current = new SizeCriterion
                                        {
                                            Size = sz,
                                            Operator = (ComparisonOperator)EnumUtil.Parse(typeof(ComparisonOperator), tokens[i + 1])
                                        };
                                        i += 2;
                                        stateStack.Push(FileSelector.ParseState.CriterionDone);
                                        break;
                                    }
                                case 10:
                                case 11:
                                    {
                                        if (tokens.Length <= i + 2)
                                        {
                                            throw new ArgumentException(string.Join(" ", tokens, i, tokens.Length - i));
                                        }
                                        ComparisonOperator c = (ComparisonOperator)EnumUtil.Parse(typeof(ComparisonOperator), tokens[i + 1]);
                                        if (c != ComparisonOperator.NotEqualTo && c != ComparisonOperator.EqualTo)
                                        {
                                            throw new ArgumentException(string.Join(" ", tokens, i, tokens.Length - i));
                                        }
                                        string j = tokens[i + 2];
                                        if (j.StartsWith("'") && j.EndsWith("'"))
                                        {
                                            j = j.Substring(1, j.Length - 2).Replace("\u0006", " ");
                                        }
                                        current = new NameCriterion
                                        {
                                            MatchingFileSpec = j,
                                            Operator = c
                                        };
                                        i += 2;
                                        stateStack.Push(FileSelector.ParseState.CriterionDone);
                                        break;
                                    }
                                case 12:
                                case 13:
                                case 14:
                                    {
                                        if (tokens.Length <= i + 2)
                                        {
                                            throw new ArgumentException(string.Join(" ", tokens, i, tokens.Length - i));
                                        }
                                        ComparisonOperator c = (ComparisonOperator)EnumUtil.Parse(typeof(ComparisonOperator), tokens[i + 1]);
                                        if (c != ComparisonOperator.NotEqualTo && c != ComparisonOperator.EqualTo)
                                        {
                                            throw new ArgumentException(string.Join(" ", tokens, i, tokens.Length - i));
                                        }
                                        current = ((tok == "type") ? new TypeCriterion
                                        {
                                            AttributeString = tokens[i + 2],
                                            Operator = c
                                        } : new AttributesCriterion
                                        {
                                            AttributeString = tokens[i + 2],
                                            Operator = c
                                        });
                                        i += 2;
                                        stateStack.Push(FileSelector.ParseState.CriterionDone);
                                        break;
                                    }
                                case 15:
                                    stateStack.Push(FileSelector.ParseState.Whitespace);
                                    break;
                                default:
                                    goto IL_87C;
                            }
                            state = stateStack.Peek();
                            if (state == FileSelector.ParseState.CriterionDone)
                            {
                                stateStack.Pop();
                                if (stateStack.Peek() == FileSelector.ParseState.ConjunctionPending)
                                {
                                    while (stateStack.Peek() == FileSelector.ParseState.ConjunctionPending)
                                    {
                                        CompoundCriterion cc = critStack.Pop() as CompoundCriterion;
                                        cc.Right = current;
                                        current = cc;
                                        stateStack.Pop();
                                        state = stateStack.Pop();
                                        if (state != FileSelector.ParseState.CriterionDone)
                                        {
                                            throw new ArgumentException("??");
                                        }
                                    }
                                }
                                else
                                {
                                    stateStack.Push(FileSelector.ParseState.CriterionDone);
                                }
                            }
                            if (state == FileSelector.ParseState.Whitespace)
                            {
                                stateStack.Pop();
                            }
                            i++;
                            continue;
                        }
                    }*/
                    IL_87C:
                    throw new ArgumentException("'" + tokens[i] + "'");
                }
                result = current;
            }
            return result;
        }

        /// <summary>
        /// Returns a string representation of the FileSelector object.
        /// </summary>
        /// <returns>The string representation of the boolean logic statement of the file
        /// selection criteria for this instance. </returns>
        // Token: 0x060000C8 RID: 200 RVA: 0x000058B8 File Offset: 0x00003AB8
        public override string ToString()
        {
            return "FileSelector(" + this._Criterion.ToString() + ")";
        }

        // Token: 0x060000C9 RID: 201 RVA: 0x000058E4 File Offset: 0x00003AE4
        private bool Evaluate(string filename)
        {
            return this._Criterion.Evaluate(filename);
        }

        // Token: 0x060000CA RID: 202 RVA: 0x00005904 File Offset: 0x00003B04
        [Conditional("SelectorTrace")]
        private void SelectorTrace(string format, params object[] args)
        {
            if (this._Criterion != null && this._Criterion.Verbose)
            {
                Console.WriteLine(format, args);
            }
        }

        /// <summary>
        ///   Returns the names of the files in the specified directory
        ///   that fit the selection criteria specified in the FileSelector.
        /// </summary>
        ///
        /// <remarks>
        ///   This is equivalent to calling <see cref="M:Ionic.FileSelector.SelectFiles(System.String,System.Boolean)" />
        ///   with recurseDirectories = false.
        /// </remarks>
        ///
        /// <param name="directory">
        ///   The name of the directory over which to apply the FileSelector
        ///   criteria.
        /// </param>
        ///
        /// <returns>
        ///   A collection of strings containing fully-qualified pathnames of files
        ///   that match the criteria specified in the FileSelector instance.
        /// </returns>
        // Token: 0x060000CB RID: 203 RVA: 0x00005938 File Offset: 0x00003B38
        public ICollection<string> SelectFiles(string directory)
        {
            return this.SelectFiles(directory, false);
        }

        /// <summary>
        ///   Returns the names of the files in the specified directory that fit the
        ///   selection criteria specified in the FileSelector, optionally recursing
        ///   through subdirectories.
        /// </summary>
        ///
        /// <remarks>
        ///   This method applies the file selection criteria contained in the
        ///   FileSelector to the files contained in the given directory, and
        ///   returns the names of files that conform to the criteria.
        /// </remarks>
        ///
        /// <param name="directory">
        ///   The name of the directory over which to apply the FileSelector
        ///   criteria.
        /// </param>
        ///
        /// <param name="recurseDirectories">
        ///   Whether to recurse through subdirectories when applying the file
        ///   selection criteria.
        /// </param>
        ///
        /// <returns>
        ///   A collection of strings containing fully-qualified pathnames of files
        ///   that match the criteria specified in the FileSelector instance.
        /// </returns>
        // Token: 0x060000CC RID: 204 RVA: 0x00005954 File Offset: 0x00003B54
        public ReadOnlyCollection<string> SelectFiles(string directory, bool recurseDirectories)
        {
            if (this._Criterion == null)
            {
                throw new ArgumentException("SelectionCriteria has not been set");
            }
            List<string> list = new List<string>();
            try
            {
                if (Directory.Exists(directory))
                {
                    string[] filenames = Directory.GetFiles(directory);
                    foreach (string filename in filenames)
                    {
                        if (this.Evaluate(filename))
                        {
                            list.Add(filename);
                        }
                    }
                    if (recurseDirectories)
                    {
                        string[] dirnames = Directory.GetDirectories(directory);
                        foreach (string dir in dirnames)
                        {
                            if (this.TraverseReparsePoints || (File.GetAttributes(dir) & (FileAttributes)1024) == 0)
                            {
                                if (this.Evaluate(dir))
                                {
                                    list.Add(dir);
                                }
                                list.AddRange(this.SelectFiles(dir, recurseDirectories));
                            }
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }
            return list.AsReadOnly();
        }

        // Token: 0x060000CD RID: 205 RVA: 0x00005AA0 File Offset: 0x00003CA0
        private bool Evaluate(ZipEntry entry)
        {
            return this._Criterion.Evaluate(entry);
        }

        /// <summary>
        /// Retrieve the ZipEntry items in the ZipFile that conform to the specified criteria.
        /// </summary>
        /// <remarks>
        ///
        /// <para>
        /// This method applies the criteria set in the FileSelector instance (as described in
        /// the <see cref="P:Ionic.FileSelector.SelectionCriteria" />) to the specified ZipFile.  Using this
        /// method, for example, you can retrieve all entries from the given ZipFile that
        /// have filenames ending in .txt.
        /// </para>
        ///
        /// <para>
        /// Normally, applications would not call this method directly.  This method is used
        /// by the ZipFile class.
        /// </para>
        ///
        /// <para>
        /// Using the appropriate SelectionCriteria, you can retrieve entries based on size,
        /// time, and attributes. See <see cref="P:Ionic.FileSelector.SelectionCriteria" /> for a
        /// description of the syntax of the SelectionCriteria string.
        /// </para>
        ///
        /// </remarks>
        ///
        /// <param name="zip">The ZipFile from which to retrieve entries.</param>
        ///
        /// <returns>a collection of ZipEntry objects that conform to the criteria.</returns>
        // Token: 0x060000CE RID: 206 RVA: 0x00005AC0 File Offset: 0x00003CC0
        public ICollection<ZipEntry> SelectEntries(ZipFile zip)
        {
            if (zip == null)
            {
                throw new ArgumentNullException("zip");
            }
            List<ZipEntry> list = new List<ZipEntry>();
            foreach (ZipEntry e in zip)
            {
                if (this.Evaluate(e))
                {
                    list.Add(e);
                }
            }
            return list;
        }

        /// <summary>
        /// Retrieve the ZipEntry items in the ZipFile that conform to the specified criteria.
        /// </summary>
        /// <remarks>
        ///
        /// <para>
        /// This method applies the criteria set in the FileSelector instance (as described in
        /// the <see cref="P:Ionic.FileSelector.SelectionCriteria" />) to the specified ZipFile.  Using this
        /// method, for example, you can retrieve all entries from the given ZipFile that
        /// have filenames ending in .txt.
        /// </para>
        ///
        /// <para>
        /// Normally, applications would not call this method directly.  This method is used
        /// by the ZipFile class.
        /// </para>
        ///
        /// <para>
        /// This overload allows the selection of ZipEntry instances from the ZipFile to be restricted
        /// to entries contained within a particular directory in the ZipFile.
        /// </para>
        ///
        /// <para>
        /// Using the appropriate SelectionCriteria, you can retrieve entries based on size,
        /// time, and attributes. See <see cref="P:Ionic.FileSelector.SelectionCriteria" /> for a
        /// description of the syntax of the SelectionCriteria string.
        /// </para>
        ///
        /// </remarks>
        ///
        /// <param name="zip">The ZipFile from which to retrieve entries.</param>
        ///
        /// <param name="directoryPathInArchive">
        /// the directory in the archive from which to select entries. If null, then
        /// all directories in the archive are used.
        /// </param>
        ///
        /// <returns>a collection of ZipEntry objects that conform to the criteria.</returns>
        // Token: 0x060000CF RID: 207 RVA: 0x00005B4C File Offset: 0x00003D4C
        public ICollection<ZipEntry> SelectEntries(ZipFile zip, string directoryPathInArchive)
        {
            if (zip == null)
            {
                throw new ArgumentNullException("zip");
            }
            List<ZipEntry> list = new List<ZipEntry>();
            string slashSwapped = (directoryPathInArchive == null) ? null : directoryPathInArchive.Replace("/", "\\");
            if (slashSwapped != null)
            {
                while (slashSwapped.EndsWith("\\"))
                {
                    slashSwapped = slashSwapped.Substring(0, slashSwapped.Length - 1);
                }
            }
            foreach (ZipEntry e in zip)
            {
                if ((directoryPathInArchive == null || Path.GetDirectoryName(e.FileName) == directoryPathInArchive || Path.GetDirectoryName(e.FileName) == slashSwapped) && this.Evaluate(e))
                {
                    list.Add(e);
                }
            }
            return list;
        }

        // Token: 0x0400007C RID: 124
        internal SelectionCriterion _Criterion;

        // Token: 0x02000018 RID: 24
        private enum ParseState
        {
            // Token: 0x0400007F RID: 127
            Start,
            // Token: 0x04000080 RID: 128
            OpenParen,
            // Token: 0x04000081 RID: 129
            CriterionDone,
            // Token: 0x04000082 RID: 130
            ConjunctionPending,
            // Token: 0x04000083 RID: 131
            Whitespace
        }

        // Token: 0x02000019 RID: 25
        private static class RegexAssertions
        {
            // Token: 0x04000084 RID: 132
            public static readonly string PrecededByOddNumberOfSingleQuotes = "(?<=(?:[^']*'[^']*')*'[^']*)";

            // Token: 0x04000085 RID: 133
            public static readonly string FollowedByOddNumberOfSingleQuotesAndLineEnd = "(?=[^']*'(?:[^']*'[^']*')*[^']*$)";

            // Token: 0x04000086 RID: 134
            public static readonly string PrecededByEvenNumberOfSingleQuotes = "(?<=(?:[^']*'[^']*')*[^']*)";

            // Token: 0x04000087 RID: 135
            public static readonly string FollowedByEvenNumberOfSingleQuotesAndLineEnd = "(?=(?:[^']*'[^']*')*[^']*$)";
        }
    }
}

namespace Ionic
{
    /// <summary>
    /// Enumerates the options for a logical conjunction. This enum is intended for use
    /// internally by the FileSelector class.
    /// </summary>
    // Token: 0x0200000D RID: 13
    internal enum LogicalConjunction
    {
        // Token: 0x0400005C RID: 92
        NONE,
        // Token: 0x0400005D RID: 93
        AND,
        // Token: 0x0400005E RID: 94
        OR,
        // Token: 0x0400005F RID: 95
        XOR
    }
}

namespace Ionic
{
    // Token: 0x02000013 RID: 19
    internal class NameCriterion : SelectionCriterion
    {
        // Token: 0x17000042 RID: 66
        // (set) Token: 0x060000A6 RID: 166 RVA: 0x000042F0 File Offset: 0x000024F0
        internal virtual string MatchingFileSpec
        {
            set
            {
                if (Directory.Exists(value))
                {
                    this._MatchingFileSpec = ".\\" + value + "\\*.*";
                }
                else
                {
                    this._MatchingFileSpec = value;
                }
                this._regexString = "^" + Regex.Escape(this._MatchingFileSpec).Replace("\\\\\\*\\.\\*", "\\\\([^\\.]+|.*\\.[^\\\\\\.]*)").Replace("\\.\\*", "\\.[^\\\\\\.]*").Replace("\\*", ".*").Replace("\\?", "[^\\\\\\.]") + "$";
                this._re = new Regex(this._regexString, RegexOptions.IgnoreCase);
            }
        }

        // Token: 0x060000A7 RID: 167 RVA: 0x0000439C File Offset: 0x0000259C
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("name ").Append(EnumUtil.GetDescription(this.Operator)).Append(" '").Append(this._MatchingFileSpec).Append("'");
            return sb.ToString();
        }

        // Token: 0x060000A8 RID: 168 RVA: 0x000043FC File Offset: 0x000025FC
        internal override bool Evaluate(string filename)
        {
            return this._Evaluate(filename);
        }

        // Token: 0x060000A9 RID: 169 RVA: 0x00004418 File Offset: 0x00002618
        private bool _Evaluate(string fullpath)
        {
            string f = (this._MatchingFileSpec.IndexOf('\\') == -1) ? Path.GetFileName(fullpath) : fullpath;
            bool result = this._re.IsMatch(f);
            if (this.Operator != ComparisonOperator.EqualTo)
            {
                result = !result;
            }
            return result;
        }

        // Token: 0x060000AA RID: 170 RVA: 0x00004464 File Offset: 0x00002664
        internal override bool Evaluate(ZipEntry entry)
        {
            string transformedFileName = entry.FileName.Replace("/", "\\");
            return this._Evaluate(transformedFileName);
        }

        // Token: 0x04000071 RID: 113
        private Regex _re;

        // Token: 0x04000072 RID: 114
        private string _regexString;

        // Token: 0x04000073 RID: 115
        internal ComparisonOperator Operator;

        // Token: 0x04000074 RID: 116
        private string _MatchingFileSpec;
    }
}


namespace Ionic
{
    // Token: 0x02000010 RID: 16
    internal abstract class SelectionCriterion
    {
        // Token: 0x17000041 RID: 65
        // (get) Token: 0x06000096 RID: 150 RVA: 0x00003F90 File Offset: 0x00002190
        // (set) Token: 0x06000097 RID: 151 RVA: 0x00003FA7 File Offset: 0x000021A7
        internal virtual bool Verbose { get; set; }

        // Token: 0x06000098 RID: 152
        internal abstract bool Evaluate(string filename);

        // Token: 0x06000099 RID: 153 RVA: 0x00003FB0 File Offset: 0x000021B0
        [Conditional("SelectorTrace")]
        protected static void CriterionTrace(string format, params object[] args)
        {
        }

        // Token: 0x0600009A RID: 154
        internal abstract bool Evaluate(ZipEntry entry);
    }
}

namespace Ionic
{
    // Token: 0x02000011 RID: 17
    internal class SizeCriterion : SelectionCriterion
    {
        // Token: 0x0600009C RID: 156 RVA: 0x00003FBC File Offset: 0x000021BC
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("size ").Append(EnumUtil.GetDescription(this.Operator)).Append(" ").Append(this.Size.ToString());
            return sb.ToString();
        }

        // Token: 0x0600009D RID: 157 RVA: 0x00004018 File Offset: 0x00002218
        internal override bool Evaluate(string filename)
        {
            FileInfo fi = new FileInfo(filename);
            return this._Evaluate(fi.Length);
        }

        // Token: 0x0600009E RID: 158 RVA: 0x00004040 File Offset: 0x00002240
        private bool _Evaluate(long Length)
        {
            bool result;
            switch (this.Operator)
            {
                case ComparisonOperator.GreaterThan:
                    result = (Length > this.Size);
                    break;
                case ComparisonOperator.GreaterThanOrEqualTo:
                    result = (Length >= this.Size);
                    break;
                case ComparisonOperator.LesserThan:
                    result = (Length < this.Size);
                    break;
                case ComparisonOperator.LesserThanOrEqualTo:
                    result = (Length <= this.Size);
                    break;
                case ComparisonOperator.EqualTo:
                    result = (Length == this.Size);
                    break;
                case ComparisonOperator.NotEqualTo:
                    result = (Length != this.Size);
                    break;
                default:
                    throw new ArgumentException("Operator");
            }
            return result;
        }

        // Token: 0x0600009F RID: 159 RVA: 0x000040D8 File Offset: 0x000022D8
        internal override bool Evaluate(ZipEntry entry)
        {
            return this._Evaluate(entry.UncompressedSize);
        }

        // Token: 0x0400006C RID: 108
        internal ComparisonOperator Operator;

        // Token: 0x0400006D RID: 109
        internal long Size;
    }
}

namespace Ionic
{
    // Token: 0x02000012 RID: 18
    internal class TimeCriterion : SelectionCriterion
    {
        // Token: 0x060000A1 RID: 161 RVA: 0x00004100 File Offset: 0x00002300
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(this.Which.ToString()).Append(" ").Append(EnumUtil.GetDescription(this.Operator)).Append(" ").Append(this.Time.ToString("yyyy-MM-dd-HH:mm:ss"));
            return sb.ToString();
        }

        // Token: 0x060000A2 RID: 162 RVA: 0x00004174 File Offset: 0x00002374
        internal override bool Evaluate(string filename)
        {
            DateTime x;
            switch (this.Which)
            {
                case WhichTime.atime:
                    x = File.GetLastAccessTime(filename).ToUniversalTime();
                    break;
                case WhichTime.mtime:
                    x = File.GetLastWriteTime(filename).ToUniversalTime();
                    break;
                case WhichTime.ctime:
                    x = File.GetCreationTime(filename).ToUniversalTime();
                    break;
                default:
                    throw new ArgumentException("Operator");
            }
            return this._Evaluate(x);
        }

        // Token: 0x060000A3 RID: 163 RVA: 0x000041E8 File Offset: 0x000023E8
        private bool _Evaluate(DateTime x)
        {
            bool result;
            switch (this.Operator)
            {
                case ComparisonOperator.GreaterThan:
                    result = (x > this.Time);
                    break;
                case ComparisonOperator.GreaterThanOrEqualTo:
                    result = (x >= this.Time);
                    break;
                case ComparisonOperator.LesserThan:
                    result = (x < this.Time);
                    break;
                case ComparisonOperator.LesserThanOrEqualTo:
                    result = (x <= this.Time);
                    break;
                case ComparisonOperator.EqualTo:
                    result = (x == this.Time);
                    break;
                case ComparisonOperator.NotEqualTo:
                    result = (x != this.Time);
                    break;
                default:
                    throw new ArgumentException("Operator");
            }
            return result;
        }

        // Token: 0x060000A4 RID: 164 RVA: 0x0000428C File Offset: 0x0000248C
        internal override bool Evaluate(ZipEntry entry)
        {
            DateTime x;
            switch (this.Which)
            {
                case WhichTime.atime:
                    x = entry.AccessedTime;
                    break;
                case WhichTime.mtime:
                    x = entry.ModifiedTime;
                    break;
                case WhichTime.ctime:
                    x = entry.CreationTime;
                    break;
                default:
                    throw new ArgumentException("??time");
            }
            return this._Evaluate(x);
        }

        // Token: 0x0400006E RID: 110
        internal ComparisonOperator Operator;

        // Token: 0x0400006F RID: 111
        internal WhichTime Which;

        // Token: 0x04000070 RID: 112
        internal DateTime Time;
    }
}

namespace Ionic
{
    // Token: 0x02000014 RID: 20
    internal class TypeCriterion : SelectionCriterion
    {
        // Token: 0x17000043 RID: 67
        // (get) Token: 0x060000AC RID: 172 RVA: 0x0000449C File Offset: 0x0000269C
        // (set) Token: 0x060000AD RID: 173 RVA: 0x000044BC File Offset: 0x000026BC
        internal string AttributeString
        {
            get
            {
                return this.ObjectType.ToString();
            }
            set
            {
                if (value.Length != 1 || (value[0] != 'D' && value[0] != 'F'))
                {
                    throw new ArgumentException("Specify a single character: either D or F");
                }
                this.ObjectType = value[0];
            }
        }

        // Token: 0x060000AE RID: 174 RVA: 0x0000450C File Offset: 0x0000270C
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("type ").Append(EnumUtil.GetDescription(this.Operator)).Append(" ").Append(this.AttributeString);
            return sb.ToString();
        }

        // Token: 0x060000AF RID: 175 RVA: 0x00004560 File Offset: 0x00002760
        internal override bool Evaluate(string filename)
        {
            bool result = (this.ObjectType == 'D') ? Directory.Exists(filename) : File.Exists(filename);
            if (this.Operator != ComparisonOperator.EqualTo)
            {
                result = !result;
            }
            return result;
        }

        // Token: 0x060000B0 RID: 176 RVA: 0x000045A0 File Offset: 0x000027A0
        internal override bool Evaluate(ZipEntry entry)
        {
            bool result = (this.ObjectType == 'D') ? entry.IsDirectory : (!entry.IsDirectory);
            if (this.Operator != ComparisonOperator.EqualTo)
            {
                result = !result;
            }
            return result;
        }

        // Token: 0x04000075 RID: 117
        private char ObjectType;

        // Token: 0x04000076 RID: 118
        internal ComparisonOperator Operator;
    }
}
namespace Ionic
{
    // Token: 0x0200000E RID: 14
    internal enum WhichTime
    {
        // Token: 0x04000061 RID: 97
        atime,
        // Token: 0x04000062 RID: 98
        mtime,
        // Token: 0x04000063 RID: 99
        ctime
    }
}

using System;
using System.Text;
using System.Collections.Generic;
using System.IO;

using Ionic.Zip;

namespace IonicZipSrc
{
    class Program
    {
        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // ZIP 圧縮・暗号化
            ZipFile z = new ZipFile();
            z.AlternateEncoding = Encoding.UTF8;
            z.AlternateEncodingUsage = ZipOption.Always;

            ZipEntry ze = z.AddEntry("1.txt", Encoding.ASCII.GetBytes("Hello"));
            ze.CompressionLevel = Ionic.Zlib.CompressionLevel.None;
            ze.Password = "a";
            MemoryStream ms = new MemoryStream();
            z.Save(ms);

            var data = ms.ToArray();

            File.WriteAllBytes(@"C:\tmp2\190829\zip2.zip", data);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Multizip.Core.Formats
{
    /// <summary>
    /// Detects an archive's format from its content (magic bytes) rather than the
    /// file extension. The extension is only used to disambiguate formats that share
    /// the same container signature (the ZIP family: jar/apk/war/ear/nupkg/...).
    /// </summary>
    public static class FormatDetector
    {
        // How many bytes we need at the head of the file to recognise everything.
        // Most signatures sit in the first few bytes, but ISO 9660's "CD001" marker
        // lives at offset 0x8001 (after the 32 KB system area), so we read past it.
        // This is a single sequential read and is cheap even for tiny files.
        private const int HeaderSize = 0x9010; // 36 KB

        private static readonly FormatSignature[] Signatures = BuildSignatures();

        /// <summary>Detect from a file path (opens the file read-only, shared).</summary>
        public static ArchiveFormat Detect(string path)
        {
            if (string.IsNullOrEmpty(path)) return ArchiveFormat.Unknown;

            byte[] header;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                header = ReadHeader(fs);
            }
            return Detect(header, header.Length, Path.GetExtension(path));
        }

        /// <summary>Detect from an already-open, seekable stream. Position is restored.</summary>
        public static ArchiveFormat Detect(Stream stream, string extension = null)
        {
            if (stream == null) return ArchiveFormat.Unknown;

            long original = stream.CanSeek ? stream.Position : 0;
            try
            {
                byte[] header = ReadHeader(stream);
                return Detect(header, header.Length, extension);
            }
            finally
            {
                if (stream.CanSeek) stream.Position = original;
            }
        }

        /// <summary>Detect from a header buffer plus an optional extension hint.</summary>
        public static ArchiveFormat Detect(byte[] header, int length, string extension)
        {
            if (header == null || length <= 0) return ArchiveFormat.Unknown;

            string ext = NormalizeExtension(extension);

            // Walk signatures by descending priority. The first match wins, except
            // for the ZIP family where we refine using the extension.
            foreach (var sig in Signatures)
            {
                if (!sig.Matches(header, length)) continue;

                if (sig.Format == ArchiveFormat.Zip)
                    return RefineZipFamily(ext);

                return sig.Format;
            }

            // Nothing matched on content; fall back to a pure extension guess so the
            // UI can still present *something* sensible to the user.
            return GuessFromExtension(ext);
        }

        /// <summary>True when the format is one Multizip can read/list/extract.</summary>
        public static bool IsKnown(ArchiveFormat format) => format != ArchiveFormat.Unknown;

        private static byte[] ReadHeader(Stream stream)
        {
            var buffer = new byte[HeaderSize];
            int total = 0;
            int read;
            while (total < buffer.Length &&
                   (read = stream.Read(buffer, total, buffer.Length - total)) > 0)
            {
                total += read;
            }
            if (total == buffer.Length) return buffer;
            Array.Resize(ref buffer, total);
            return buffer;
        }

        private static ArchiveFormat RefineZipFamily(string ext)
        {
            switch (ext)
            {
                case "jar": return ArchiveFormat.Jar;
                case "war": return ArchiveFormat.War;
                case "ear": return ArchiveFormat.Ear;
                case "apk": return ArchiveFormat.Apk;
                case "nupkg": return ArchiveFormat.Nupkg;
                default: return ArchiveFormat.Zip;
            }
        }

        private static string NormalizeExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return string.Empty;
            return extension.TrimStart('.').ToLowerInvariant();
        }

        private static ArchiveFormat GuessFromExtension(string ext)
        {
            switch (ext)
            {
                case "zip": return ArchiveFormat.Zip;
                case "7z": return ArchiveFormat.SevenZip;
                case "rar": return ArchiveFormat.Rar;
                case "tar": return ArchiveFormat.Tar;
                case "gz":
                case "tgz": return ArchiveFormat.Gzip;
                case "bz2":
                case "tbz":
                case "tbz2": return ArchiveFormat.Bzip2;
                case "xz":
                case "txz": return ArchiveFormat.Xz;
                case "lzma": return ArchiveFormat.Lzma;
                case "zst":
                case "zstd": return ArchiveFormat.Zstandard;
                case "lz4": return ArchiveFormat.Lz4;
                case "lz": return ArchiveFormat.Lzip;
                case "z": return ArchiveFormat.Z;
                case "cab": return ArchiveFormat.Cab;
                case "arj": return ArchiveFormat.Arj;
                case "lzh":
                case "lha": return ArchiveFormat.Lzh;
                case "cpio": return ArchiveFormat.Cpio;
                case "deb": return ArchiveFormat.Deb;
                case "rpm": return ArchiveFormat.Rpm;
                case "msi": return ArchiveFormat.Msi;
                case "jar": return ArchiveFormat.Jar;
                case "war": return ArchiveFormat.War;
                case "ear": return ArchiveFormat.Ear;
                case "apk": return ArchiveFormat.Apk;
                case "nupkg": return ArchiveFormat.Nupkg;
                case "iso": return ArchiveFormat.Iso;
                case "udf": return ArchiveFormat.Udf;
                case "wim":
                case "swm":
                case "esd": return ArchiveFormat.Wim;
                case "vhd": return ArchiveFormat.Vhd;
                case "vhdx": return ArchiveFormat.Vhdx;
                case "dmg": return ArchiveFormat.Dmg;
                case "qcow2": return ArchiveFormat.Qcow2;
                case "squashfs":
                case "sfs": return ArchiveFormat.Squashfs;
                case "cramfs": return ArchiveFormat.Cramfs;
                case "chm": return ArchiveFormat.Chm;
                default: return ArchiveFormat.Unknown;
            }
        }

        private static FormatSignature[] BuildSignatures()
        {
            const short W = -1; // wildcard
            var list = new List<FormatSignature>
            {
                // --- High priority, very specific containers -----------------
                // 7-Zip: '7z' BC AF 27 1C
                FormatSignature.Bytes(ArchiveFormat.SevenZip, 0, new byte[]{0x37,0x7A,0xBC,0xAF,0x27,0x1C}, 100, "7z"),
                // RAR 5:  52 61 72 21 1A 07 01 00 ; RAR 4: ...07 00
                FormatSignature.Bytes(ArchiveFormat.Rar, 0, new byte[]{0x52,0x61,0x72,0x21,0x1A,0x07,0x01,0x00}, 100, "rar"),
                FormatSignature.Bytes(ArchiveFormat.Rar, 0, new byte[]{0x52,0x61,0x72,0x21,0x1A,0x07,0x00}, 99, "rar"),

                // Debian package: ASCII "!<arch>\n" then "debian-binary"
                new FormatSignature(ArchiveFormat.Deb, 0,
                    Concat(Ascii("!<arch>\n"), AsciiShort("debian")), 95, "deb"),
                // RPM: ED AB EE DB
                FormatSignature.Bytes(ArchiveFormat.Rpm, 0, new byte[]{0xED,0xAB,0xEE,0xDB}, 95, "rpm"),

                // --- Disk / file-system images -------------------------------
                FormatSignature.Ascii(ArchiveFormat.Wim, 0, "MSWIM\0\0\0", 90, "wim","swm","esd"),
                FormatSignature.Ascii(ArchiveFormat.Vhdx, 0, "vhdxfile", 90, "vhdx"),
                FormatSignature.Ascii(ArchiveFormat.Vhd, 0, "conectix", 90, "vhd"),
                FormatSignature.Ascii(ArchiveFormat.Qcow2, 0, "QFI", 90, "qcow2"),
                // SquashFS little- and big-endian magic
                FormatSignature.Bytes(ArchiveFormat.Squashfs, 0, new byte[]{0x68,0x73,0x71,0x73}, 90, "squashfs","sfs"),
                FormatSignature.Bytes(ArchiveFormat.Squashfs, 0, new byte[]{0x73,0x71,0x73,0x68}, 90, "squashfs","sfs"),
                FormatSignature.Bytes(ArchiveFormat.Cramfs, 0, new byte[]{0x45,0x3D,0xCD,0x28}, 90, "cramfs"),
                // ISO 9660: "CD001" at 0x8001, 0x8801 or 0x9001 (after the system area).
                FormatSignature.Ascii(ArchiveFormat.Iso, 0x8001, "CD001", 90, "iso"),
                // DMG koly trailer is at EOF, so DMG is mostly detected by extension.

                // --- Single-stream compressors -------------------------------
                FormatSignature.Bytes(ArchiveFormat.Gzip, 0, new byte[]{0x1F,0x8B}, 80, "gz","tgz"),
                FormatSignature.Ascii(ArchiveFormat.Bzip2, 0, "BZh", 80, "bz2","tbz","tbz2"),
                FormatSignature.Bytes(ArchiveFormat.Xz, 0, new byte[]{0xFD,0x37,0x7A,0x58,0x5A,0x00}, 80, "xz","txz"),
                FormatSignature.Bytes(ArchiveFormat.Zstandard, 0, new byte[]{0x28,0xB5,0x2F,0xFD}, 80, "zst","zstd"),
                FormatSignature.Bytes(ArchiveFormat.Lz4, 0, new byte[]{0x04,0x22,0x4D,0x18}, 80, "lz4"),
                FormatSignature.Ascii(ArchiveFormat.Lzip, 0, "LZIP", 80, "lz"),
                // classic compress (.Z): 1F 9D
                FormatSignature.Bytes(ArchiveFormat.Z, 0, new byte[]{0x1F,0x9D}, 80, "z"),

                // --- Other containers ----------------------------------------
                FormatSignature.Bytes(ArchiveFormat.Cab, 0, new byte[]{0x4D,0x53,0x43,0x46}, 80, "cab"),   // MSCF
                FormatSignature.Bytes(ArchiveFormat.Arj, 0, new byte[]{0x60,0xEA}, 80, "arj"),             // ARJ magic
                // LZH/LHA: '-lh?-' or '-lz?-' at offset 2
                new FormatSignature(ArchiveFormat.Lzh, 2, new short[]{(short)'-',(short)'l', W, W,(short)'-'}, 70, "lzh","lha"),
                // CPIO (several variants): "070707" / "070701" / "070702" or binary C7 71
                FormatSignature.Ascii(ArchiveFormat.Cpio, 0, "070707", 70, "cpio"),
                FormatSignature.Ascii(ArchiveFormat.Cpio, 0, "070701", 70, "cpio"),
                FormatSignature.Ascii(ArchiveFormat.Cpio, 0, "070702", 70, "cpio"),
                // OLE/CFBF compound (MSI, some CHM, legacy office): D0 CF 11 E0 A1 B1 1A E1
                FormatSignature.Bytes(ArchiveFormat.Compound, 0, new byte[]{0xD0,0xCF,0x11,0xE0,0xA1,0xB1,0x1A,0xE1}, 60, "msi"),
                // CHM: "ITSF"
                FormatSignature.Ascii(ArchiveFormat.Chm, 0, "ITSF", 70, "chm"),

                // --- TAR (ustar magic lives deep in the header) --------------
                FormatSignature.Ascii(ArchiveFormat.Tar, 257, "ustar", 60, "tar"),

                // --- ZIP family (lowest priority of the "specific" set) ------
                // Local file header 'PK\x03\x04', plus empty-archive 'PK\x05\x06'
                // and spanned 'PK\x07\x08'.
                FormatSignature.Bytes(ArchiveFormat.Zip, 0, new byte[]{0x50,0x4B,0x03,0x04}, 50, "zip","jar","war","ear","apk","nupkg"),
                FormatSignature.Bytes(ArchiveFormat.Zip, 0, new byte[]{0x50,0x4B,0x05,0x06}, 49, "zip"),
                FormatSignature.Bytes(ArchiveFormat.Zip, 0, new byte[]{0x50,0x4B,0x07,0x08}, 49, "zip"),

                // LZMA alone has only a weak header (properties byte + 8-byte size),
                // so we rely on the extension for raw .lzma streams.
            };

            return list.OrderByDescending(s => s.Priority).ToArray();
        }

        // --- small helpers for building byte patterns ------------------------
        private static short[] Ascii(string s)
        {
            var b = System.Text.Encoding.ASCII.GetBytes(s);
            var r = new short[b.Length];
            for (int i = 0; i < b.Length; i++) r[i] = b[i];
            return r;
        }

        private static short[] AsciiShort(string s) => Ascii(s);

        private static short[] Concat(short[] a, short[] b)
        {
            var r = new short[a.Length + b.Length];
            Array.Copy(a, 0, r, 0, a.Length);
            Array.Copy(b, 0, r, a.Length, b.Length);
            return r;
        }
    }
}

using System;

namespace Multizip.Core.Abstractions
{
    /// <summary>One file or directory listed inside an archive.</summary>
    public sealed class ArchiveEntry
    {
        /// <summary>Full path inside the archive, using '/' as the separator.</summary>
        public string Path { get; set; }

        /// <summary>Uncompressed size in bytes (0 for directories / unknown).</summary>
        public long Size { get; set; }

        /// <summary>Compressed size in bytes when the provider can report it.</summary>
        public long CompressedSize { get; set; }

        public bool IsDirectory { get; set; }

        public bool IsEncrypted { get; set; }

        public DateTime? LastModified { get; set; }

        /// <summary>CRC-32 if exposed by the format, otherwise null.</summary>
        public uint? Crc32 { get; set; }

        /// <summary>Optional comment / method string for the entry.</summary>
        public string Method { get; set; }

        /// <summary>The leaf name (last path segment).</summary>
        public string Name
        {
            get
            {
                if (string.IsNullOrEmpty(Path)) return string.Empty;
                string trimmed = Path.TrimEnd('/');
                int i = trimmed.LastIndexOf('/');
                return i < 0 ? trimmed : trimmed.Substring(i + 1);
            }
        }

        /// <summary>Compression ratio as a percentage saved (0..100), or 0 if unknown.</summary>
        public double RatioPercent
        {
            get
            {
                if (Size <= 0 || CompressedSize <= 0) return 0;
                double r = 100.0 * (1.0 - ((double)CompressedSize / Size));
                if (r < 0) r = 0;
                return r;
            }
        }
    }
}

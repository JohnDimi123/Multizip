using System.Collections.Generic;
using Multizip.Core.Formats;

namespace Multizip.Core.Abstractions
{
    /// <summary>Adjustable compression level, mapped per provider/format.</summary>
    public enum CompressionLevel
    {
        Store = 0,     // no compression, fastest
        Fastest = 1,
        Fast = 3,
        Normal = 5,
        Maximum = 7,
        Ultra = 9
    }

    /// <summary>Everything needed to create an archive.</summary>
    public sealed class CompressionOptions
    {
        /// <summary>Target archive format.</summary>
        public ArchiveFormat Format { get; set; } = ArchiveFormat.Zip;

        public CompressionLevel Level { get; set; } = CompressionLevel.Normal;

        /// <summary>Optional password. When set, AES-256 is used where supported.</summary>
        public string Password { get; set; }

        /// <summary>Encrypt the file list as well as the data (7z header encryption).</summary>
        public bool EncryptFileNames { get; set; } = true;

        /// <summary>Number of worker threads. 0 = let the provider decide (all cores).</summary>
        public int ThreadCount { get; set; } = 0;

        /// <summary>Split into volumes of this many bytes. 0 = single file.</summary>
        public long VolumeSize { get; set; } = 0;

        /// <summary>Produce a self-extracting .exe (7z/zip SFX) where supported.</summary>
        public bool SelfExtracting { get; set; } = false;

        /// <summary>Preserve the directory structure of added items.</summary>
        public bool PreservePaths { get; set; } = true;

        /// <summary>Optional explicit method override (e.g. "LZMA2", "PPMd", "BZip2").</summary>
        public string Method { get; set; }

        /// <summary>The full set of source files / folders being added.</summary>
        public List<string> SourcePaths { get; } = new List<string>();

        /// <summary>Root used to compute relative paths when <see cref="PreservePaths"/> is on.</summary>
        public string BaseDirectory { get; set; }
    }
}

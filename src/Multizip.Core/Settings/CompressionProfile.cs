using Multizip.Core.Abstractions;
using Multizip.Core.Formats;

namespace Multizip.Core.Settings
{
    /// <summary>
    /// A reusable, named bundle of compression settings the user can pick from the
    /// Compress dialog ("Maximum 7z", "Fast Zip", "Encrypted backup", ...).
    /// </summary>
    public sealed class CompressionProfile
    {
        public string Name { get; set; } = "New profile";
        public ArchiveFormat Format { get; set; } = ArchiveFormat.Zip;
        public CompressionLevel Level { get; set; } = CompressionLevel.Normal;
        public int ThreadCount { get; set; } = 0;
        public long VolumeSize { get; set; } = 0;
        public bool EncryptFileNames { get; set; } = true;
        public bool SelfExtracting { get; set; } = false;
        public string Method { get; set; }

        public CompressionOptions ToOptions()
        {
            return new CompressionOptions
            {
                Format = Format,
                Level = Level,
                ThreadCount = ThreadCount,
                VolumeSize = VolumeSize,
                EncryptFileNames = EncryptFileNames,
                SelfExtracting = SelfExtracting,
                Method = Method
            };
        }

        public static CompressionProfile[] Defaults()
        {
            return new[]
            {
                new CompressionProfile { Name = "Zip - Normal", Format = ArchiveFormat.Zip, Level = CompressionLevel.Normal },
                new CompressionProfile { Name = "Zip - Fastest", Format = ArchiveFormat.Zip, Level = CompressionLevel.Fastest },
                new CompressionProfile { Name = "7z - Ultra", Format = ArchiveFormat.SevenZip, Level = CompressionLevel.Ultra },
                new CompressionProfile { Name = "7z - Encrypted backup", Format = ArchiveFormat.SevenZip, Level = CompressionLevel.Maximum, EncryptFileNames = true },
                new CompressionProfile { Name = "Tar + Gzip", Format = ArchiveFormat.Gzip, Level = CompressionLevel.Normal },
            };
        }
    }
}

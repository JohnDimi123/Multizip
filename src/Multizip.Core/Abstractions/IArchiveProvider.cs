using System;
using System.Collections.Generic;
using System.Threading;
using Multizip.Core.Formats;

namespace Multizip.Core.Abstractions
{
    /// <summary>Result of opening/listing an archive.</summary>
    public sealed class ArchiveListing
    {
        public ArchiveFormat Format { get; set; }
        public IReadOnlyList<ArchiveEntry> Entries { get; set; } = Array.Empty<ArchiveEntry>();
        public bool IsEncrypted { get; set; }
        public bool IsMultiVolume { get; set; }
        public string Comment { get; set; }

        public long TotalUncompressedSize { get; set; }
        public long TotalCompressedSize { get; set; }
    }

    /// <summary>
    /// A backend that can read and/or write some set of archive formats. The engine
    /// picks the highest-priority provider that supports a given format for a given
    /// operation. Implemented by the built-in SharpCompress and 7-Zip providers and
    /// by external plugins.
    /// </summary>
    public interface IArchiveProvider
    {
        /// <summary>Human-readable name for logs / the about box.</summary>
        string Name { get; }

        /// <summary>Higher wins when several providers support the same format.</summary>
        int Priority { get; }

        /// <summary>True when this provider can list/extract the format.</summary>
        bool CanRead(ArchiveFormat format);

        /// <summary>True when this provider can create the format.</summary>
        bool CanWrite(ArchiveFormat format);

        /// <summary>Formats this provider can create (for the "new archive" UI).</summary>
        IEnumerable<ArchiveFormat> WritableFormats { get; }

        /// <summary>List entries without extracting. <paramref name="password"/> may be null.</summary>
        ArchiveListing List(string archivePath, string password = null);

        /// <summary>Extract (all or selected entries) to a destination directory.</summary>
        void Extract(string archivePath, ExtractionOptions options,
                     IProgress<ProgressInfo> progress, CancellationToken token);

        /// <summary>Create a new archive from the supplied sources.</summary>
        void Create(string archivePath, CompressionOptions options,
                    IProgress<ProgressInfo> progress, CancellationToken token);

        /// <summary>Verify archive integrity. Returns true when the archive is sound.</summary>
        bool Test(string archivePath, string password,
                  IProgress<ProgressInfo> progress, CancellationToken token);
    }
}

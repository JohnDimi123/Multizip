using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Multizip.Core.Abstractions;
using Multizip.Core.Formats;
using Multizip.Core.Logging;
using Multizip.Core.Util;
using SharpCompress.Archives;
using SharpCompress.Writers;
// NOTE: SharpCompress.Common is intentionally NOT imported wholesale because it
// also defines an "ExtractionOptions" type that would collide with ours. The few
// types we need from it are fully qualified below.
using ReaderOptions = SharpCompress.Readers.ReaderOptions;
using ArchiveType = SharpCompress.Common.ArchiveType;
using CompressionType = SharpCompress.Common.CompressionType;

namespace Multizip.Core.Providers
{
    /// <summary>
    /// Fully-managed provider built on SharpCompress. Acts as the cross-platform
    /// reader for the common formats and as a write fallback when the native 7-Zip
    /// library is unavailable. No external runtime dependency (pure .NET).
    /// </summary>
    public sealed class SharpCompressProvider : IArchiveProvider
    {
        private const int BufferSize = 1024 * 1024;

        public string Name => "SharpCompress (managed)";
        public int Priority => 50;

        private static readonly HashSet<ArchiveFormat> Readable = new HashSet<ArchiveFormat>
        {
            ArchiveFormat.Zip, ArchiveFormat.Jar, ArchiveFormat.War, ArchiveFormat.Ear,
            ArchiveFormat.Apk, ArchiveFormat.Nupkg,
            ArchiveFormat.SevenZip, ArchiveFormat.Rar, ArchiveFormat.Tar,
            ArchiveFormat.Gzip, ArchiveFormat.Bzip2, ArchiveFormat.Xz, ArchiveFormat.Lzma
        };

        private static readonly HashSet<ArchiveFormat> Writable = new HashSet<ArchiveFormat>
        {
            ArchiveFormat.Zip, ArchiveFormat.Tar, ArchiveFormat.Gzip
        };

        public bool CanRead(ArchiveFormat format) => Readable.Contains(format);
        public bool CanWrite(ArchiveFormat format) => Writable.Contains(format);
        public IEnumerable<ArchiveFormat> WritableFormats => Writable;

        public ArchiveListing List(string archivePath, string password = null)
        {
            var options = new ReaderOptions { Password = password, LookForHeader = true };
            using (IArchive archive = ArchiveFactory.Open(archivePath, options))
            {
                var entries = new List<ArchiveEntry>();
                long totalU = 0, totalC = 0;
                bool encrypted = false;

                foreach (IArchiveEntry e in archive.Entries)
                {
                    if (e.IsEncrypted) encrypted = true;
                    var entry = new ArchiveEntry
                    {
                        Path = e.Key,
                        Size = e.Size,
                        CompressedSize = e.CompressedSize,
                        IsDirectory = e.IsDirectory,
                        IsEncrypted = e.IsEncrypted,
                        LastModified = e.LastModifiedTime,
                        Crc32 = e.Crc != 0 ? (uint?)unchecked((uint)e.Crc) : null
                    };
                    entries.Add(entry);
                    if (!e.IsDirectory)
                    {
                        totalU += Math.Max(0, e.Size);
                        totalC += Math.Max(0, e.CompressedSize);
                    }
                }

                return new ArchiveListing
                {
                    Format = FormatDetector.Detect(archivePath),
                    Entries = entries,
                    IsEncrypted = encrypted,
                    TotalUncompressedSize = totalU,
                    TotalCompressedSize = totalC
                };
            }
        }

        public void Extract(string archivePath, ExtractionOptions options,
            IProgress<ProgressInfo> progress, CancellationToken token)
        {
            var selected = options.SelectedEntries.Count > 0
                ? new HashSet<string>(options.SelectedEntries, StringComparer.OrdinalIgnoreCase)
                : null;

            var readerOptions = new ReaderOptions { Password = options.Password, LookForHeader = true };

            using (IArchive archive = ArchiveFactory.Open(archivePath, readerOptions))
            {
                var files = archive.Entries
                    .Where(e => !e.IsDirectory)
                    .Where(e => selected == null || selected.Contains(e.Key))
                    .ToList();

                long total = files.Sum(e => Math.Max(0, e.Size));
                long processed = 0;
                var sw = Stopwatch.StartNew();

                Directory.CreateDirectory(options.Destination);

                foreach (IArchiveEntry entry in files)
                {
                    token.ThrowIfCancellationRequested();

                    string dest = PathUtil.ResolveDestination(
                        options.Destination, entry.Key, options.PreservePaths);

                    if (File.Exists(dest))
                    {
                        switch (options.Overwrite)
                        {
                            case OverwriteMode.Skip:
                                processed += Math.Max(0, entry.Size);
                                continue;
                            case OverwriteMode.RenameNew:
                                dest = PathUtil.MakeUnique(dest);
                                break;
                        }
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(dest));

                    using (Stream src = entry.OpenEntryStream())
                    using (var dst = new FileStream(dest, FileMode.Create, FileAccess.Write,
                               FileShare.None, BufferSize))
                    {
                        CopyWithProgress(src, dst, entry.Key, total, ref processed, progress, sw, token);
                    }

                    if (entry.LastModifiedTime.HasValue)
                    {
                        try { File.SetLastWriteTime(dest, entry.LastModifiedTime.Value); }
                        catch { /* timestamps are best-effort */ }
                    }
                }
            }
        }

        public void Create(string archivePath, CompressionOptions options,
            IProgress<ProgressInfo> progress, CancellationToken token)
        {
            if (!CanWrite(options.Format))
                throw new NotSupportedException(
                    $"The managed provider cannot create {options.Format}. Install 7z.dll for full write support.");

            var files = EnumerateSources(options, out string baseDir).ToList();
            if (options.Format == ArchiveFormat.Gzip && files.Count != 1)
                throw new NotSupportedException("GZip stores a single stream. Use Tar+GZip for multiple files.");

            long total = files.Sum(f => SafeLength(f));
            long processed = 0;
            var sw = Stopwatch.StartNew();

            ArchiveType type;
            CompressionType compression;
            MapWriteType(options.Format, out type, out compression);

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(archivePath)));

            using (var outStream = new FileStream(archivePath, FileMode.Create, FileAccess.Write,
                       FileShare.None, BufferSize))
            using (IWriter writer = WriterFactory.Open(outStream, type, new WriterOptions(compression)
            {
                LeaveStreamOpen = false
            }))
            {
                foreach (string file in files)
                {
                    token.ThrowIfCancellationRequested();
                    string key = MakeKey(file, baseDir, options.PreservePaths);

                    using (var input = new CountingStream(
                        new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, BufferSize)))
                    {
                        input.BytesRead += delta =>
                        {
                            processed += delta;
                            Report(progress, processed, total, key, sw);
                        };
                        writer.Write(key, input, File.GetLastWriteTime(file));
                    }
                }
            }
        }

        public bool Test(string archivePath, string password,
            IProgress<ProgressInfo> progress, CancellationToken token)
        {
            try
            {
                var readerOptions = new ReaderOptions { Password = password, LookForHeader = true };
                using (IArchive archive = ArchiveFactory.Open(archivePath, readerOptions))
                {
                    var files = archive.Entries.Where(e => !e.IsDirectory).ToList();
                    long total = files.Sum(e => Math.Max(0, e.Size));
                    long processed = 0;
                    var sw = Stopwatch.StartNew();
                    var scratch = new byte[BufferSize];

                    foreach (IArchiveEntry entry in files)
                    {
                        token.ThrowIfCancellationRequested();
                        using (Stream s = entry.OpenEntryStream())
                        {
                            int read;
                            while ((read = s.Read(scratch, 0, scratch.Length)) > 0)
                            {
                                token.ThrowIfCancellationRequested();
                                processed += read;
                                Report(progress, processed, total, entry.Key, sw);
                            }
                        }
                    }
                }
                return true;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Logger.Warn("Integrity test failed: " + ex.Message);
                return false;
            }
        }

        // --- helpers ------------------------------------------------------
        private static IEnumerable<string> EnumerateSources(CompressionOptions options, out string baseDir)
        {
            baseDir = options.BaseDirectory;
            var result = new List<string>();
            foreach (string src in options.SourcePaths)
            {
                if (Directory.Exists(src))
                {
                    if (string.IsNullOrEmpty(baseDir)) baseDir = Directory.GetParent(src.TrimEnd('\\', '/'))?.FullName ?? src;
                    result.AddRange(Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories));
                }
                else if (File.Exists(src))
                {
                    if (string.IsNullOrEmpty(baseDir)) baseDir = Path.GetDirectoryName(src);
                    result.Add(src);
                }
            }
            return result;
        }

        private static string MakeKey(string file, string baseDir, bool preservePaths)
        {
            if (!preservePaths || string.IsNullOrEmpty(baseDir))
                return Path.GetFileName(file);

            string full = Path.GetFullPath(file);
            string root = Path.GetFullPath(baseDir);
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                string rel = full.Substring(root.Length).TrimStart('\\', '/');
                return rel.Replace('\\', '/');
            }
            return Path.GetFileName(file);
        }

        private static void MapWriteType(ArchiveFormat format, out ArchiveType type, out CompressionType compression)
        {
            switch (format)
            {
                case ArchiveFormat.Zip:
                    type = ArchiveType.Zip; compression = CompressionType.Deflate; break;
                case ArchiveFormat.Tar:
                    type = ArchiveType.Tar; compression = CompressionType.None; break;
                case ArchiveFormat.Gzip:
                    type = ArchiveType.GZip; compression = CompressionType.GZip; break;
                default:
                    throw new NotSupportedException(format.ToString());
            }
        }

        private static long SafeLength(string file)
        {
            try { return new FileInfo(file).Length; } catch { return 0; }
        }

        private static void CopyWithProgress(Stream src, Stream dst, string name, long total,
            ref long processed, IProgress<ProgressInfo> progress, Stopwatch sw, CancellationToken token)
        {
            var buffer = new byte[BufferSize];
            int read;
            while ((read = src.Read(buffer, 0, buffer.Length)) > 0)
            {
                token.ThrowIfCancellationRequested();
                dst.Write(buffer, 0, read);
                processed += read;
                Report(progress, processed, total, name, sw);
            }
        }

        private static void Report(IProgress<ProgressInfo> progress, long processed, long total,
            string name, Stopwatch sw)
        {
            progress?.Report(new ProgressInfo(processed, total, name, sw.Elapsed));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using Multizip.Core.Abstractions;
using Multizip.Core.Formats;
using Multizip.Core.Plugins;
using Multizip.Core.Util;

namespace Multizip.Plugins.Sample
{
    /// <summary>
    /// Reference plugin. Drop the compiled DLL into Multizip's "Plugins" folder and
    /// the host discovers this class at startup and registers the provider it returns.
    /// This one adds a tiny, fully-managed GZip provider built on System.IO.Compression
    /// - it is intentionally low priority so it never shadows the built-in providers,
    /// and exists purely to demonstrate the extension model.
    /// </summary>
    public sealed class SampleGzipPlugin : IArchivePlugin
    {
        public string Name => "Sample GZip provider";
        public string Version => "1.0.0";
        public IArchiveProvider CreateProvider() => new SampleGzipProvider();
    }

    public sealed class SampleGzipProvider : IArchiveProvider
    {
        private const int BufferSize = 1024 * 1024;

        public string Name => "Sample GZip provider (plugin)";

        // Below the built-in providers (SharpCompress=50, 7-Zip=100) on purpose.
        public int Priority => 10;

        public bool CanRead(ArchiveFormat format) => format == ArchiveFormat.Gzip;
        public bool CanWrite(ArchiveFormat format) => format == ArchiveFormat.Gzip;
        public IEnumerable<ArchiveFormat> WritableFormats => new[] { ArchiveFormat.Gzip };

        public ArchiveListing List(string archivePath, string password = null)
        {
            string inner = InnerName(archivePath);
            var entry = new ArchiveEntry
            {
                Path = inner,
                Size = 0, // a raw gzip stream does not store the original size up front
                CompressedSize = SafeLength(archivePath),
                IsDirectory = false,
                LastModified = File.GetLastWriteTime(archivePath)
            };
            return new ArchiveListing
            {
                Format = ArchiveFormat.Gzip,
                Entries = new[] { entry },
                TotalCompressedSize = entry.CompressedSize
            };
        }

        public void Extract(string archivePath, ExtractionOptions options,
            IProgress<ProgressInfo> progress, CancellationToken token)
        {
            Directory.CreateDirectory(options.Destination);
            string dest = Path.Combine(options.Destination, InnerName(archivePath));
            long total = SafeLength(archivePath);
            var sw = Stopwatch.StartNew();

            using (var input = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize))
            using (var gz = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize))
            {
                Pump(gz, output, total, sw, InnerName(archivePath), progress, token, () => input.Position);
            }
        }

        public void Create(string archivePath, CompressionOptions options,
            IProgress<ProgressInfo> progress, CancellationToken token)
        {
            if (options.SourcePaths.Count != 1 || !File.Exists(options.SourcePaths[0]))
                throw new NotSupportedException("The sample GZip provider compresses exactly one file.");

            string source = options.SourcePaths[0];
            long total = SafeLength(source);
            var sw = Stopwatch.StartNew();

            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize))
            using (var output = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize))
            using (var gz = new GZipStream(output, MapLevel(options.Level)))
            {
                Pump(input, gz, total, sw, Path.GetFileName(source), progress, token, () => input.Position);
            }
        }

        public bool Test(string archivePath, string password,
            IProgress<ProgressInfo> progress, CancellationToken token)
        {
            try
            {
                var scratch = new byte[BufferSize];
                using (var input = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize))
                using (var gz = new GZipStream(input, CompressionMode.Decompress))
                {
                    while (gz.Read(scratch, 0, scratch.Length) > 0)
                        token.ThrowIfCancellationRequested();
                }
                return true;
            }
            catch (OperationCanceledException) { throw; }
            catch { return false; }
        }

        // ---- helpers -----------------------------------------------------
        private static void Pump(Stream from, Stream to, long total, Stopwatch sw, string name,
            IProgress<ProgressInfo> progress, CancellationToken token, Func<long> readPosition)
        {
            var buffer = new byte[BufferSize];
            int read;
            while ((read = from.Read(buffer, 0, buffer.Length)) > 0)
            {
                token.ThrowIfCancellationRequested();
                to.Write(buffer, 0, read);
                progress?.Report(new ProgressInfo(readPosition(), total, name, sw.Elapsed));
            }
        }

        private static System.IO.Compression.CompressionLevel MapLevel(Multizip.Core.Abstractions.CompressionLevel level)
        {
            return level <= Multizip.Core.Abstractions.CompressionLevel.Fast
                ? System.IO.Compression.CompressionLevel.Fastest
                : System.IO.Compression.CompressionLevel.Optimal;
        }

        private static string InnerName(string archivePath)
        {
            string name = Path.GetFileName(archivePath);
            return name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                ? name.Substring(0, name.Length - 3)
                : name + ".out";
        }

        private static long SafeLength(string path)
        {
            try { return new FileInfo(path).Length; } catch { return 0; }
        }
    }
}

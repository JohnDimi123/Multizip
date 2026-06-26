using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Multizip.Core.Abstractions;
using Multizip.Core.Formats;
using Multizip.Core.Logging;
using SevenZip;

namespace Multizip.Core.Providers
{
    /// <summary>
    /// Native provider built on the 7-Zip engine (7z.dll) via Squid-Box.SevenZipSharp.
    /// This is the broad-coverage backend: it reads the long tail of formats (cab,
    /// iso, wim, rpm, deb, vhd/vhdx, dmg, squashfs, ...) and writes 7z/zip/tar/gz/
    /// bz2/xz with AES-256 encryption, multi-volume splitting and SFX output.
    ///
    /// 7z.dll is resolved at startup from the app folder (or a lib\x64 / lib\x86
    /// subfolder matching the process bitness). If it cannot be found this provider
    /// reports itself unavailable and the managed provider takes over.
    /// </summary>
    public sealed class SevenZipProvider : IArchiveProvider
    {
        private const int BufferSize = 1024 * 1024;

        private static bool _initialized;
        private static bool _available;
        private static string _libraryPath;
        private static string _sfxModulePath;
        private static readonly object InitGate = new object();

        public string Name => "7-Zip engine (7z.dll)";
        public int Priority => 100;

        public static bool IsAvailable
        {
            get { EnsureLibrary(); return _available; }
        }

        public static string LibraryPath
        {
            get { EnsureLibrary(); return _libraryPath; }
        }

        private static readonly HashSet<ArchiveFormat> Readable = new HashSet<ArchiveFormat>
        {
            ArchiveFormat.Zip, ArchiveFormat.Jar, ArchiveFormat.War, ArchiveFormat.Ear,
            ArchiveFormat.Apk, ArchiveFormat.Nupkg, ArchiveFormat.SevenZip, ArchiveFormat.Rar,
            ArchiveFormat.Tar, ArchiveFormat.Gzip, ArchiveFormat.Bzip2, ArchiveFormat.Xz,
            ArchiveFormat.Lzma, ArchiveFormat.Cab, ArchiveFormat.Arj, ArchiveFormat.Lzh,
            ArchiveFormat.Cpio, ArchiveFormat.Z, ArchiveFormat.Deb, ArchiveFormat.Rpm,
            ArchiveFormat.Msi, ArchiveFormat.Compound, ArchiveFormat.Iso, ArchiveFormat.Udf,
            ArchiveFormat.Wim, ArchiveFormat.Vhd, ArchiveFormat.Vhdx, ArchiveFormat.Dmg,
            ArchiveFormat.Qcow2, ArchiveFormat.Squashfs, ArchiveFormat.Cramfs,
            ArchiveFormat.Chm, ArchiveFormat.Nsis
        };

        private static readonly HashSet<ArchiveFormat> Writable = new HashSet<ArchiveFormat>
        {
            ArchiveFormat.SevenZip, ArchiveFormat.Zip, ArchiveFormat.Tar,
            ArchiveFormat.Gzip, ArchiveFormat.Bzip2, ArchiveFormat.Xz
        };

        public bool CanRead(ArchiveFormat format) => IsAvailable && Readable.Contains(format);
        public bool CanWrite(ArchiveFormat format) => IsAvailable && Writable.Contains(format);
        public IEnumerable<ArchiveFormat> WritableFormats => IsAvailable ? Writable : Enumerable.Empty<ArchiveFormat>();

        // --- listing ------------------------------------------------------
        public ArchiveListing List(string archivePath, string password = null)
        {
            EnsureAvailable();
            using (var extractor = CreateExtractor(archivePath, password))
            {
                var entries = new List<ArchiveEntry>();
                long totalU = 0;
                bool encrypted = false;

                foreach (ArchiveFileInfo info in extractor.ArchiveFileData)
                {
                    if (info.Encrypted) encrypted = true;
                    entries.Add(new ArchiveEntry
                    {
                        Path = info.FileName,
                        Size = (long)info.Size,
                        CompressedSize = 0, // 7z does not expose per-entry packed size reliably
                        IsDirectory = info.IsDirectory,
                        IsEncrypted = info.Encrypted,
                        LastModified = info.LastWriteTime,
                        Crc32 = info.Crc != 0 ? (uint?)info.Crc : null,
                        Method = info.Method
                    });
                    if (!info.IsDirectory) totalU += (long)info.Size;
                }

                return new ArchiveListing
                {
                    Format = FormatDetector.Detect(archivePath),
                    Entries = entries,
                    IsEncrypted = encrypted,
                    TotalUncompressedSize = totalU,
                    TotalCompressedSize = (long)extractor.PackedSize
                };
            }
        }

        // --- extraction ---------------------------------------------------
        public void Extract(string archivePath, ExtractionOptions options,
            IProgress<ProgressInfo> progress, CancellationToken token)
        {
            EnsureAvailable();
            Directory.CreateDirectory(options.Destination);

            using (var extractor = CreateExtractor(archivePath, options.Password))
            {
                long total = extractor.ArchiveFileData
                    .Where(f => !f.IsDirectory)
                    .Sum(f => (long)f.Size);
                var sw = Stopwatch.StartNew();
                string current = string.Empty;

                extractor.FileExtractionStarted += (s, e) =>
                {
                    current = e.FileInfo.FileName;
                    if (token.IsCancellationRequested) e.Cancel = true;
                };
                extractor.Extracting += (s, e) =>
                {
                    long processed = total > 0 ? (long)(total * (e.PercentDone / 100.0)) : 0;
                    progress?.Report(new ProgressInfo(processed, total, current, sw.Elapsed));
                };

                if (options.SelectedEntries.Count > 0)
                {
                    var wanted = new HashSet<string>(options.SelectedEntries, StringComparer.OrdinalIgnoreCase);
                    int[] indexes = extractor.ArchiveFileData
                        .Where(f => wanted.Contains(f.FileName))
                        .Select(f => f.Index)
                        .ToArray();
                    extractor.ExtractFiles(options.Destination, indexes);
                }
                else
                {
                    extractor.ExtractArchive(options.Destination);
                }
                token.ThrowIfCancellationRequested();
            }
        }

        // --- creation -----------------------------------------------------
        public void Create(string archivePath, CompressionOptions options,
            IProgress<ProgressInfo> progress, CancellationToken token)
        {
            EnsureAvailable();
            if (!Writable.Contains(options.Format))
                throw new NotSupportedException($"7-Zip engine cannot create {options.Format}.");

            var files = ExpandSources(options).ToList();
            if (files.Count == 0) throw new InvalidOperationException("Nothing to compress.");

            OutArchiveFormat outFormat = MapOutFormat(options.Format);
            if ((outFormat == OutArchiveFormat.GZip ||
                 outFormat == OutArchiveFormat.BZip2 ||
                 outFormat == OutArchiveFormat.XZ) && files.Count > 1)
            {
                throw new NotSupportedException(
                    $"{options.Format} stores a single stream. Use 7z, Zip or Tar for multiple files.");
            }

            var compressor = new SevenZipCompressor
            {
                ArchiveFormat = outFormat,
                CompressionLevel = MapLevel(options.Level),
                CompressionMode = CompressionMode.Create,
                DirectoryStructure = options.PreservePaths,
                EncryptHeaders = options.EncryptFileNames && outFormat == OutArchiveFormat.SevenZip
            };

            ConfigureThreads(compressor, options.ThreadCount);
            ConfigureMethod(compressor, options);
            ConfigureVolumes(compressor, options.VolumeSize);

            long total = files.Sum(SafeLength);
            var sw = Stopwatch.StartNew();
            string current = string.Empty;

            compressor.FileCompressionStarted += (s, e) =>
            {
                current = e.FileName;
                if (token.IsCancellationRequested) e.Cancel = true;
            };
            compressor.Compressing += (s, e) =>
            {
                long processed = total > 0 ? (long)(total * (e.PercentDone / 100.0)) : 0;
                progress?.Report(new ProgressInfo(processed, total, current, sw.Elapsed));
            };

            // For SFX we build a normal .7z to a temp file then wrap it.
            bool sfx = options.SelfExtracting && outFormat == OutArchiveFormat.SevenZip;
            string buildPath = sfx ? Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".7z") : archivePath;

            try
            {
                if (!string.IsNullOrEmpty(options.Password))
                {
                    compressor.ZipEncryptionMethod = ZipEncryptionMethod.Aes256; // honoured for zip
                    compressor.CompressFilesEncrypted(buildPath, options.Password, files.ToArray());
                }
                else
                {
                    compressor.CompressFiles(buildPath, files.ToArray());
                }

                token.ThrowIfCancellationRequested();

                if (sfx) WrapSelfExtracting(buildPath, archivePath);
            }
            finally
            {
                if (sfx && File.Exists(buildPath))
                {
                    try { File.Delete(buildPath); } catch { /* temp cleanup */ }
                }
            }
        }

        // --- integrity test ----------------------------------------------
        public bool Test(string archivePath, string password,
            IProgress<ProgressInfo> progress, CancellationToken token)
        {
            EnsureAvailable();
            try
            {
                using (var extractor = CreateExtractor(archivePath, password))
                {
                    long total = extractor.ArchiveFileData.Where(f => !f.IsDirectory).Sum(f => (long)f.Size);
                    var sw = Stopwatch.StartNew();
                    extractor.Extracting += (s, e) =>
                    {
                        long processed = total > 0 ? (long)(total * (e.PercentDone / 100.0)) : 0;
                        progress?.Report(new ProgressInfo(processed, total, "Testing", sw.Elapsed));
                    };
                    // Allow cancellation between files (the only Cancel-capable hook for Check()).
                    extractor.FileExtractionStarted += (s, e) =>
                    {
                        if (token.IsCancellationRequested) e.Cancel = true;
                    };
                    bool ok = extractor.Check();
                    token.ThrowIfCancellationRequested();
                    return ok;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Logger.Warn("7-Zip integrity test failed: " + ex.Message);
                return false;
            }
        }

        // --- configuration helpers ---------------------------------------
        private static SevenZipExtractor CreateExtractor(string archivePath, string password)
        {
            return string.IsNullOrEmpty(password)
                ? new SevenZipExtractor(archivePath)
                : new SevenZipExtractor(archivePath, password);
        }

        private static void ConfigureThreads(SevenZipCompressor compressor, int threadCount)
        {
            try
            {
                if (threadCount > 0) compressor.CustomParameters["mt"] = threadCount.ToString();
                else compressor.CustomParameters["mt"] = "on"; // all cores
            }
            catch { /* CustomParameters unsupported for some formats */ }
        }

        private static void ConfigureMethod(SevenZipCompressor compressor, CompressionOptions options)
        {
            if (!string.IsNullOrEmpty(options.Method) &&
                Enum.TryParse(options.Method, true, out CompressionMethod method))
            {
                compressor.CompressionMethod = method;
            }
        }

        private static void ConfigureVolumes(SevenZipCompressor compressor, long volumeSize)
        {
            if (volumeSize <= 0) return;
            if (volumeSize > int.MaxValue)
            {
                Logger.Warn("Volume size capped at 2 GB (engine limit).");
                volumeSize = int.MaxValue;
            }
            compressor.VolumeSize = (int)volumeSize;
        }

        private static IEnumerable<string> ExpandSources(CompressionOptions options)
        {
            foreach (string src in options.SourcePaths)
            {
                if (Directory.Exists(src))
                    foreach (string f in Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories))
                        yield return f;
                else if (File.Exists(src))
                    yield return src;
            }
        }

        private static OutArchiveFormat MapOutFormat(ArchiveFormat format)
        {
            switch (format)
            {
                case ArchiveFormat.SevenZip: return OutArchiveFormat.SevenZip;
                case ArchiveFormat.Zip: return OutArchiveFormat.Zip;
                case ArchiveFormat.Tar: return OutArchiveFormat.Tar;
                case ArchiveFormat.Gzip: return OutArchiveFormat.GZip;
                case ArchiveFormat.Bzip2: return OutArchiveFormat.BZip2;
                case ArchiveFormat.Xz: return OutArchiveFormat.XZ;
                default: throw new NotSupportedException(format.ToString());
            }
        }

        private static SevenZip.CompressionLevel MapLevel(Abstractions.CompressionLevel level)
        {
            switch (level)
            {
                case Abstractions.CompressionLevel.Store: return SevenZip.CompressionLevel.None;
                case Abstractions.CompressionLevel.Fastest: return SevenZip.CompressionLevel.Fast;
                case Abstractions.CompressionLevel.Fast: return SevenZip.CompressionLevel.Low;
                case Abstractions.CompressionLevel.Normal: return SevenZip.CompressionLevel.Normal;
                case Abstractions.CompressionLevel.Maximum: return SevenZip.CompressionLevel.High;
                case Abstractions.CompressionLevel.Ultra: return SevenZip.CompressionLevel.Ultra;
                default: return SevenZip.CompressionLevel.Normal;
            }
        }

        private static long SafeLength(string file)
        {
            try { return new FileInfo(file).Length; } catch { return 0; }
        }

        /// <summary>
        /// Produce a self-extracting .exe by prepending the 7z SFX module to the
        /// freshly-built .7z payload. The module (7z.sfx) is shipped beside 7z.dll.
        /// </summary>
        private void WrapSelfExtracting(string sevenZipPath, string exePath)
        {
            EnsureLibrary();
            if (string.IsNullOrEmpty(_sfxModulePath) || !File.Exists(_sfxModulePath))
                throw new NotSupportedException(
                    "Self-extracting output needs 7z.sfx next to 7z.dll. It was not found.");

            using (var outStream = new FileStream(exePath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize))
            {
                using (var module = new FileStream(_sfxModulePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize))
                    module.CopyTo(outStream, BufferSize);
                using (var payload = new FileStream(sevenZipPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize))
                    payload.CopyTo(outStream, BufferSize);
            }
        }

        // --- native library discovery ------------------------------------
        private static void EnsureAvailable()
        {
            EnsureLibrary();
            if (!_available)
                throw new InvalidOperationException(
                    "The 7-Zip engine (7z.dll) is not available. Place 7z.dll beside the application.");
        }

        private static void EnsureLibrary()
        {
            if (_initialized) return;
            lock (InitGate)
            {
                if (_initialized) return;
                try
                {
                    string dll = LocateLibrary();
                    if (dll != null)
                    {
                        SevenZipBase.SetLibraryPath(dll);
                        _libraryPath = dll;
                        _sfxModulePath = Path.Combine(Path.GetDirectoryName(dll), "7z.sfx");
                        _available = true;
                        Logger.Info("7-Zip engine loaded from " + dll);
                    }
                    else
                    {
                        Logger.Warn("7z.dll not found; native format support is disabled.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to initialise 7-Zip engine", ex);
                    _available = false;
                }
                finally { _initialized = true; }
            }
        }

        private static string LocateLibrary()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string bitness = IntPtr.Size == 8 ? "x64" : "x86";

            var candidates = new[]
            {
                Path.Combine(baseDir, "7z.dll"),
                Path.Combine(baseDir, "lib", bitness, "7z.dll"),
                Path.Combine(baseDir, bitness, "7z.dll"),
                Path.Combine(baseDir, "7-Zip", "7z.dll")
            };
            return candidates.FirstOrDefault(File.Exists);
        }
    }
}

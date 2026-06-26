using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Multizip.Core.Abstractions;
using Multizip.Core.Formats;
using Multizip.Core.Logging;
using Multizip.Core.Plugins;

namespace Multizip.Core.Providers
{
    /// <summary>
    /// The engine façade the UI talks to. It owns the set of providers (built-in
    /// plus plugins) and routes every operation to the highest-priority provider
    /// that supports the format. Format is always detected from content first.
    /// </summary>
    public sealed class ArchiveEngine
    {
        private readonly List<IArchiveProvider> _providers = new List<IArchiveProvider>();

        public IReadOnlyList<IArchiveProvider> Providers => _providers;

        /// <summary>Builds an engine with the built-in providers and any plugins found.</summary>
        public static ArchiveEngine CreateDefault(string pluginsDirectory = null)
        {
            var engine = new ArchiveEngine();
            engine.Register(new SevenZipProvider());
            engine.Register(new SharpCompressProvider());

            if (!string.IsNullOrEmpty(pluginsDirectory))
            {
                foreach (LoadedPlugin lp in PluginLoader.Load(pluginsDirectory))
                    engine.Register(lp.Provider);
            }
            return engine;
        }

        public void Register(IArchiveProvider provider)
        {
            if (provider == null) return;
            _providers.Add(provider);
            _providers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            Logger.Debug($"Registered provider '{provider.Name}' (priority {provider.Priority}).");
        }

        public ArchiveFormat Detect(string path) => FormatDetector.Detect(path);

        /// <summary>Every format the engine can currently create, de-duplicated.</summary>
        public IReadOnlyList<ArchiveFormat> WritableFormats =>
            _providers.SelectMany(p => p.WritableFormats).Distinct().OrderBy(f => f.ToString()).ToList();

        public bool CanRead(ArchiveFormat format) => _providers.Any(p => p.CanRead(format));
        public bool CanWrite(ArchiveFormat format) => _providers.Any(p => p.CanWrite(format));

        public ArchiveListing List(string archivePath, string password = null)
        {
            ArchiveFormat format = FormatDetector.Detect(archivePath);
            IArchiveProvider provider = ReaderFor(format, archivePath);
            Logger.Info($"Listing '{archivePath}' as {format} via {provider.Name}.");
            ArchiveListing listing = provider.List(archivePath, password);
            listing.Format = format;
            return listing;
        }

        public void Extract(string archivePath, ExtractionOptions options,
            IProgress<ProgressInfo> progress, CancellationToken token)
        {
            ArchiveFormat format = FormatDetector.Detect(archivePath);
            IArchiveProvider provider = ReaderFor(format, archivePath);
            Logger.Info($"Extracting '{archivePath}' ({format}) -> '{options.Destination}' via {provider.Name}.");
            provider.Extract(archivePath, options, progress, token);
        }

        public void Create(string archivePath, CompressionOptions options,
            IProgress<ProgressInfo> progress, CancellationToken token)
        {
            IArchiveProvider provider = WriterFor(options.Format);
            Logger.Info($"Creating '{archivePath}' ({options.Format}) via {provider.Name}.");
            provider.Create(archivePath, options, progress, token);
        }

        public bool Test(string archivePath, string password,
            IProgress<ProgressInfo> progress, CancellationToken token)
        {
            ArchiveFormat format = FormatDetector.Detect(archivePath);
            IArchiveProvider provider = ReaderFor(format, archivePath);
            Logger.Info($"Testing '{archivePath}' ({format}) via {provider.Name}.");
            return provider.Test(archivePath, password, progress, token);
        }

        private IArchiveProvider ReaderFor(ArchiveFormat format, string archivePath)
        {
            IArchiveProvider provider = _providers.FirstOrDefault(p => p.CanRead(format));
            if (provider == null)
            {
                throw new NotSupportedException(
                    $"No installed provider can read '{archivePath}' (detected format: {format}). " +
                    "If this is a Zstandard/LZ4/LZIP file, add a codec plugin.");
            }
            return provider;
        }

        private IArchiveProvider WriterFor(ArchiveFormat format)
        {
            IArchiveProvider provider = _providers.FirstOrDefault(p => p.CanWrite(format));
            if (provider == null)
                throw new NotSupportedException($"No installed provider can create {format}.");
            return provider;
        }
    }
}

using System;
using System.Threading;
using Multizip.Core.Abstractions;
using Multizip.Core.Providers;

namespace Multizip.Core.Jobs
{
    public enum JobKind { Compress, Extract, Test }
    public enum JobStatus { Queued, Running, Completed, Failed, Cancelled }

    /// <summary>A single unit of work in the queue.</summary>
    public abstract class ArchiveJob
    {
        public Guid Id { get; } = Guid.NewGuid();
        public abstract JobKind Kind { get; }
        public string Title { get; protected set; }
        public JobStatus Status { get; internal set; } = JobStatus.Queued;
        public string Error { get; internal set; }
        public ProgressInfo LastProgress { get; internal set; }

        internal abstract void Execute(ArchiveEngine engine, IProgress<ProgressInfo> progress, CancellationToken token);
    }

    public sealed class CompressJob : ArchiveJob
    {
        private readonly string _archivePath;
        private readonly CompressionOptions _options;

        public CompressJob(string archivePath, CompressionOptions options)
        {
            _archivePath = archivePath;
            _options = options;
            Title = "Compress -> " + System.IO.Path.GetFileName(archivePath);
        }

        public override JobKind Kind => JobKind.Compress;

        internal override void Execute(ArchiveEngine engine, IProgress<ProgressInfo> progress, CancellationToken token)
            => engine.Create(_archivePath, _options, progress, token);
    }

    public sealed class ExtractJob : ArchiveJob
    {
        private readonly string _archivePath;
        private readonly ExtractionOptions _options;

        public ExtractJob(string archivePath, ExtractionOptions options)
        {
            _archivePath = archivePath;
            _options = options;
            Title = "Extract -> " + System.IO.Path.GetFileName(archivePath);
        }

        public override JobKind Kind => JobKind.Extract;

        internal override void Execute(ArchiveEngine engine, IProgress<ProgressInfo> progress, CancellationToken token)
            => engine.Extract(_archivePath, _options, progress, token);
    }

    public sealed class TestJob : ArchiveJob
    {
        private readonly string _archivePath;
        private readonly string _password;

        public bool? Result { get; private set; }

        public TestJob(string archivePath, string password)
        {
            _archivePath = archivePath;
            _password = password;
            Title = "Test -> " + System.IO.Path.GetFileName(archivePath);
        }

        public override JobKind Kind => JobKind.Test;

        internal override void Execute(ArchiveEngine engine, IProgress<ProgressInfo> progress, CancellationToken token)
        {
            Result = engine.Test(_archivePath, _password, progress, token);
            if (Result == false) throw new InvalidOperationException("Archive failed the integrity test.");
        }
    }
}

using System;

namespace Multizip.Core.Abstractions
{
    /// <summary>
    /// Immutable snapshot of an in-flight operation. Providers report these through
    /// an <see cref="IProgress{T}"/> so the UI can render rich progress bars.
    /// </summary>
    public sealed class ProgressInfo
    {
        public long BytesProcessed { get; }
        public long BytesTotal { get; }
        public string CurrentFile { get; }
        public TimeSpan Elapsed { get; }

        public ProgressInfo(long bytesProcessed, long bytesTotal, string currentFile, TimeSpan elapsed)
        {
            BytesProcessed = bytesProcessed;
            BytesTotal = bytesTotal;
            CurrentFile = currentFile;
            Elapsed = elapsed;
        }

        /// <summary>0..100, clamped. Returns 0 when the total is unknown.</summary>
        public double Percent
        {
            get
            {
                if (BytesTotal <= 0) return 0;
                double p = 100.0 * BytesProcessed / BytesTotal;
                if (p < 0) p = 0; else if (p > 100) p = 100;
                return p;
            }
        }

        /// <summary>Average throughput in bytes/second over the elapsed window.</summary>
        public double BytesPerSecond
        {
            get
            {
                double secs = Elapsed.TotalSeconds;
                return secs <= 0 ? 0 : BytesProcessed / secs;
            }
        }

        /// <summary>Estimated time remaining based on the current average rate.</summary>
        public TimeSpan EstimatedRemaining
        {
            get
            {
                double rate = BytesPerSecond;
                if (rate <= 0 || BytesTotal <= 0) return TimeSpan.Zero;
                long remaining = BytesTotal - BytesProcessed;
                if (remaining <= 0) return TimeSpan.Zero;
                return TimeSpan.FromSeconds(remaining / rate);
            }
        }
    }
}

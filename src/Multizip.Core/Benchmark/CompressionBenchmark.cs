using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace Multizip.Core.Benchmark
{
    public sealed class BenchmarkResult
    {
        public string Algorithm { get; set; }
        public long OriginalBytes { get; set; }
        public long CompressedBytes { get; set; }
        public double CompressMBPerSecond { get; set; }
        public double DecompressMBPerSecond { get; set; }

        public double RatioPercent =>
            OriginalBytes <= 0 ? 0 : 100.0 * (1.0 - ((double)CompressedBytes / OriginalBytes));
    }

    /// <summary>
    /// Self-contained compression benchmark used by the Benchmark tool. It builds a
    /// representative test buffer (a mix of compressible text and incompressible
    /// random data) and measures throughput and ratio of the built-in codecs. No
    /// external library is required so it always runs.
    /// </summary>
    public static class CompressionBenchmark
    {
        /// <param name="megabytes">Size of the synthetic test buffer, in MB.</param>
        public static IReadOnlyList<BenchmarkResult> Run(int megabytes = 64,
            IProgress<string> status = null, CancellationToken token = default)
        {
            byte[] data = BuildTestData(megabytes);
            var results = new List<BenchmarkResult>();

            status?.Report("Deflate (fastest)...");
            results.Add(Measure("Deflate (fastest)", data,
                () => CompressDeflate(data, CompressionLevel.Fastest), DecompressDeflate, token));

            status?.Report("Deflate (optimal)...");
            results.Add(Measure("Deflate (optimal)", data,
                () => CompressDeflate(data, CompressionLevel.Optimal), DecompressDeflate, token));

            status?.Report("GZip (optimal)...");
            results.Add(Measure("GZip (optimal)", data,
                () => CompressGzip(data, CompressionLevel.Optimal), DecompressGzip, token));

            return results;
        }

        private static BenchmarkResult Measure(string name, byte[] data,
            Func<byte[]> compress, Func<byte[], int> decompressToCount, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            double mb = data.Length / (1024.0 * 1024.0);

            var sw = Stopwatch.StartNew();
            byte[] compressed = compress();
            sw.Stop();
            double compSecs = Math.Max(sw.Elapsed.TotalSeconds, 1e-6);

            sw.Restart();
            decompressToCount(compressed);
            sw.Stop();
            double decompSecs = Math.Max(sw.Elapsed.TotalSeconds, 1e-6);

            return new BenchmarkResult
            {
                Algorithm = name,
                OriginalBytes = data.Length,
                CompressedBytes = compressed.Length,
                CompressMBPerSecond = mb / compSecs,
                DecompressMBPerSecond = mb / decompSecs
            };
        }

        private static byte[] BuildTestData(int megabytes)
        {
            int size = Math.Max(1, megabytes) * 1024 * 1024;
            var data = new byte[size];

            // First half: highly compressible pseudo-text (Lorem-ipsum-like).
            byte[] words = System.Text.Encoding.ASCII.GetBytes(
                "the quick brown fox jumps over the lazy dog multizip archive ");
            int half = size / 2;
            for (int i = 0; i < half; i++) data[i] = words[i % words.Length];

            // Second half: incompressible random bytes (fixed seed for repeatability).
            var rng = new Random(12345);
            var noise = new byte[size - half];
            rng.NextBytes(noise);
            Buffer.BlockCopy(noise, 0, data, half, noise.Length);
            return data;
        }

        private static byte[] CompressDeflate(byte[] data, CompressionLevel level)
        {
            using (var ms = new MemoryStream())
            {
                using (var ds = new DeflateStream(ms, level, leaveOpen: true))
                    ds.Write(data, 0, data.Length);
                return ms.ToArray();
            }
        }

        private static int DecompressDeflate(byte[] compressed)
        {
            int total = 0;
            var buffer = new byte[81920];
            using (var ms = new MemoryStream(compressed))
            using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
            {
                int read;
                while ((read = ds.Read(buffer, 0, buffer.Length)) > 0) total += read;
            }
            return total;
        }

        private static byte[] CompressGzip(byte[] data, CompressionLevel level)
        {
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, level, leaveOpen: true))
                    gz.Write(data, 0, data.Length);
                return ms.ToArray();
            }
        }

        private static int DecompressGzip(byte[] compressed)
        {
            int total = 0;
            var buffer = new byte[81920];
            using (var ms = new MemoryStream(compressed))
            using (var gz = new GZipStream(ms, CompressionMode.Decompress))
            {
                int read;
                while ((read = gz.Read(buffer, 0, buffer.Length)) > 0) total += read;
            }
            return total;
        }
    }
}

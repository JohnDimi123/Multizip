using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace Multizip.Core.Hashing
{
    public enum HashType { Crc32, Md5, Sha1, Sha256, Sha512 }

    /// <summary>
    /// Computes file checksums for the built-in checksum/verify tool. Streams the
    /// file so it works on 100+ GB inputs without loading them into memory, and
    /// reports progress for the progress bar.
    /// </summary>
    public static class ChecksumGenerator
    {
        private const int BufferSize = 1024 * 1024; // 1 MB

        /// <summary>Compute a single hash over a file, returning a lowercase hex string.</summary>
        public static string Compute(string path, HashType type,
            IProgress<double> progress = null, CancellationToken token = default)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                       FileShare.ReadWrite, BufferSize))
            {
                long total = stream.Length;
                long done = 0;

                if (type == HashType.Crc32)
                {
                    var crc = new Crc32();
                    var buffer = new byte[BufferSize];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        token.ThrowIfCancellationRequested();
                        crc.Append(buffer, 0, read);
                        done += read;
                        Report(progress, done, total);
                    }
                    return crc.Value.ToString("x8");
                }

                using (HashAlgorithm algo = Create(type))
                {
                    var buffer = new byte[BufferSize];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        token.ThrowIfCancellationRequested();
                        algo.TransformBlock(buffer, 0, read, null, 0);
                        done += read;
                        Report(progress, done, total);
                    }
                    algo.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    return ToHex(algo.Hash);
                }
            }
        }

        /// <summary>Compute several hashes in a single pass over the file.</summary>
        public static Dictionary<HashType, string> ComputeMany(string path, IEnumerable<HashType> types,
            IProgress<double> progress = null, CancellationToken token = default)
        {
            var requested = new List<HashType>(types);
            var crc = new Crc32();
            bool needCrc = requested.Contains(HashType.Crc32);

            var algos = new Dictionary<HashType, HashAlgorithm>();
            try
            {
                foreach (var t in requested)
                    if (t != HashType.Crc32) algos[t] = Create(t);

                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                           FileShare.ReadWrite, BufferSize))
                {
                    long total = stream.Length;
                    long done = 0;
                    var buffer = new byte[BufferSize];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        token.ThrowIfCancellationRequested();
                        if (needCrc) crc.Append(buffer, 0, read);
                        foreach (var kv in algos) kv.Value.TransformBlock(buffer, 0, read, null, 0);
                        done += read;
                        Report(progress, done, total);
                    }
                }

                var result = new Dictionary<HashType, string>();
                if (needCrc) result[HashType.Crc32] = crc.Value.ToString("x8");
                foreach (var kv in algos)
                {
                    kv.Value.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    result[kv.Key] = ToHex(kv.Value.Hash);
                }
                return result;
            }
            finally
            {
                foreach (var kv in algos) kv.Value.Dispose();
            }
        }

        /// <summary>Case-insensitive comparison of a computed hash against an expected value.</summary>
        public static bool Verify(string path, HashType type, string expected,
            IProgress<double> progress = null, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(expected)) return false;
            string actual = Compute(path, type, progress, token);
            return string.Equals(actual.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static HashAlgorithm Create(HashType type)
        {
            switch (type)
            {
                case HashType.Md5: return MD5.Create();
                case HashType.Sha1: return SHA1.Create();
                case HashType.Sha256: return SHA256.Create();
                case HashType.Sha512: return SHA512.Create();
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        private static void Report(IProgress<double> progress, long done, long total)
        {
            if (progress == null || total <= 0) return;
            progress.Report(100.0 * done / total);
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new System.Text.StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}

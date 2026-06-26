using System;

namespace Multizip.Core.Formats
{
    /// <summary>
    /// Describes a magic-byte signature for a format: a byte pattern that must
    /// appear at a fixed offset from the start of the file. A value of -1 in the
    /// pattern acts as a wildcard byte.
    /// </summary>
    public sealed class FormatSignature
    {
        public ArchiveFormat Format { get; }

        /// <summary>Offset from the beginning of the stream the pattern is matched at.</summary>
        public int Offset { get; }

        /// <summary>
        /// Pattern bytes. Entries equal to -1 match any byte. Using <see cref="short"/>
        /// so we can carry the wildcard sentinel without ambiguity.
        /// </summary>
        public short[] Pattern { get; }

        /// <summary>
        /// Common file extensions for the format (without the leading dot), used
        /// only to disambiguate when several formats share a signature (zip family).
        /// </summary>
        public string[] Extensions { get; }

        /// <summary>
        /// Higher priority signatures are tested first. Container formats that wrap
        /// ZIP (jar/apk/...) win over a bare ZIP match when the extension agrees.
        /// </summary>
        public int Priority { get; }

        public FormatSignature(ArchiveFormat format, int offset, short[] pattern, int priority = 0, params string[] extensions)
        {
            Format = format;
            Offset = offset;
            Pattern = pattern ?? Array.Empty<short>();
            Priority = priority;
            Extensions = extensions ?? Array.Empty<string>();
        }

        /// <summary>Returns true when <paramref name="buffer"/> matches this signature.</summary>
        public bool Matches(byte[] buffer, int length)
        {
            if (Pattern.Length == 0) return false;
            if (Offset + Pattern.Length > length) return false;

            for (int i = 0; i < Pattern.Length; i++)
            {
                short expected = Pattern[i];
                if (expected < 0) continue; // wildcard
                if (buffer[Offset + i] != (byte)expected) return false;
            }
            return true;
        }

        /// <summary>Convenience builder from a normal byte array (no wildcards).</summary>
        public static FormatSignature Bytes(ArchiveFormat format, int offset, byte[] bytes, int priority = 0, params string[] ext)
        {
            var pattern = new short[bytes.Length];
            for (int i = 0; i < bytes.Length; i++) pattern[i] = bytes[i];
            return new FormatSignature(format, offset, pattern, priority, ext);
        }

        /// <summary>Convenience builder from an ASCII string.</summary>
        public static FormatSignature Ascii(ArchiveFormat format, int offset, string ascii, int priority = 0, params string[] ext)
        {
            var bytes = System.Text.Encoding.ASCII.GetBytes(ascii);
            return Bytes(format, offset, bytes, priority, ext);
        }
    }
}

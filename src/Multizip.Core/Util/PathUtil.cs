using System;
using System.IO;
using System.Linq;

namespace Multizip.Core.Util
{
    /// <summary>Path helpers, including hardening against "zip slip" traversal.</summary>
    public static class PathUtil
    {
        private static readonly char[] Separators = { '/', '\\' };

        /// <summary>
        /// Turns a stored archive entry path into a safe relative path: forward and
        /// back slashes are normalised, drive letters / leading roots and ".."
        /// segments are stripped so an extracted file can never escape the
        /// destination directory.
        /// </summary>
        public static string SanitizeEntryPath(string entryPath)
        {
            if (string.IsNullOrEmpty(entryPath)) return string.Empty;

            string normalized = entryPath.Replace('\\', '/');

            // Drop a Windows drive prefix like "C:".
            if (normalized.Length >= 2 && normalized[1] == ':')
                normalized = normalized.Substring(2);

            var segments = normalized
                .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
                .Where(s => s != "." && s != "..")
                .Select(StripInvalid);

            return string.Join(Path.DirectorySeparatorChar.ToString(), segments);
        }

        /// <summary>
        /// Combine a destination directory with a (sanitised) entry path and confirm
        /// the resulting full path is still rooted inside the destination.
        /// </summary>
        public static string ResolveDestination(string destinationDir, string entryPath, bool preservePaths)
        {
            string rel = preservePaths
                ? SanitizeEntryPath(entryPath)
                : StripInvalid(LeafName(entryPath));

            string full = Path.GetFullPath(Path.Combine(destinationDir, rel));
            string root = Path.GetFullPath(destinationDir);

            string rootWithSep = root.EndsWith(Path.DirectorySeparatorChar.ToString())
                ? root : root + Path.DirectorySeparatorChar;

            if (!full.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException($"Refusing to extract outside destination: '{entryPath}'.");
            }
            return full;
        }

        public static string LeafName(string entryPath)
        {
            if (string.IsNullOrEmpty(entryPath)) return string.Empty;
            string trimmed = entryPath.TrimEnd(Separators);
            int i = trimmed.LastIndexOfAny(Separators);
            return i < 0 ? trimmed : trimmed.Substring(i + 1);
        }

        private static string StripInvalid(string segment)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                if (c != Path.DirectorySeparatorChar && c != Path.AltDirectorySeparatorChar)
                    segment = segment.Replace(c, '_');
            return segment;
        }

        /// <summary>A unique "name (1).ext" style path used for the RenameNew policy.</summary>
        public static string MakeUnique(string path)
        {
            if (!File.Exists(path)) return path;
            string dir = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            for (int i = 1; i < int.MaxValue; i++)
            {
                string candidate = Path.Combine(dir, $"{name} ({i}){ext}");
                if (!File.Exists(candidate)) return candidate;
            }
            return path;
        }
    }
}

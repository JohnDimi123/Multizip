using System;
using System.Globalization;

namespace Multizip.Core.Util
{
    /// <summary>Formatting helpers for sizes, rates and durations (UI-friendly).</summary>
    public static class Format
    {
        private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB", "PB" };

        /// <summary>e.g. 1536 -> "1.50 KB". Uses 1024-based units like Windows.</summary>
        public static string Size(long bytes)
        {
            if (bytes < 0) return "0 B";
            double value = bytes;
            int unit = 0;
            while (value >= 1024 && unit < Units.Length - 1)
            {
                value /= 1024;
                unit++;
            }
            return unit == 0
                ? string.Format(CultureInfo.CurrentCulture, "{0} {1}", (long)value, Units[unit])
                : string.Format(CultureInfo.CurrentCulture, "{0:0.00} {1}", value, Units[unit]);
        }

        /// <summary>e.g. "12.4 MB/s".</summary>
        public static string Rate(double bytesPerSecond)
        {
            if (bytesPerSecond <= 0) return "0 B/s";
            return Size((long)bytesPerSecond) + "/s";
        }

        /// <summary>e.g. 01:23 or 1:02:03 for longer spans.</summary>
        public static string Duration(TimeSpan span)
        {
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;
            return span.TotalHours >= 1
                ? string.Format(CultureInfo.CurrentCulture, "{0:0}:{1:00}:{2:00}",
                    Math.Floor(span.TotalHours), span.Minutes, span.Seconds)
                : string.Format(CultureInfo.CurrentCulture, "{0:00}:{1:00}", span.Minutes, span.Seconds);
        }

        /// <summary>Ratio as a saved-percentage string, e.g. "63%".</summary>
        public static string Ratio(long original, long compressed)
        {
            if (original <= 0 || compressed <= 0) return "0%";
            double saved = 100.0 * (1.0 - ((double)compressed / original));
            if (saved < 0) saved = 0;
            return string.Format(CultureInfo.CurrentCulture, "{0:0}%", saved);
        }
    }
}

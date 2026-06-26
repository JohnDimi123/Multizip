using System;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Multizip.Core.Logging;

namespace Multizip.Core.Update
{
    public sealed class UpdateInfo
    {
        public bool UpdateAvailable { get; set; }
        public Version LatestVersion { get; set; }
        public Version CurrentVersion { get; set; }
        public string DownloadUrl { get; set; }
    }

    /// <summary>
    /// Lightweight, best-effort update check. It fetches a tiny manifest of the form
    /// "version|download-url" from a configurable URL and compares to the running
    /// assembly version. Network failures are swallowed - update checks must never
    /// block or crash startup.
    /// </summary>
    public sealed class UpdateChecker
    {
        // Point this at your release manifest. Kept as a field so a build/CI step or
        // the installer can rewrite it without code changes.
        public string ManifestUrl { get; set; } =
            "https://raw.githubusercontent.com/johndimi123/multizip/main/latest.txt";

        public async Task<UpdateInfo> CheckAsync()
        {
            var current = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0, 0);
            var info = new UpdateInfo { CurrentVersion = current };

            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (var client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.UserAgent] = "Multizip-UpdateChecker";
                    string raw = await client.DownloadStringTaskAsync(ManifestUrl).ConfigureAwait(false);
                    ParseManifest(raw, info);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug("Update check skipped: " + ex.Message);
            }
            return info;
        }

        private static void ParseManifest(string raw, UpdateInfo info)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            string[] parts = raw.Trim().Split('|');
            if (Version.TryParse(parts[0].Trim(), out Version latest))
            {
                info.LatestVersion = latest;
                info.UpdateAvailable = latest > info.CurrentVersion;
                if (parts.Length > 1) info.DownloadUrl = parts[1].Trim();
            }
        }
    }
}

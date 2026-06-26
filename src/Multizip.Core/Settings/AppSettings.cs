using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Multizip.Core.Settings
{
    public enum AppTheme { Light, Dark }

    /// <summary>
    /// All persisted user preferences. Serialized to settings.json. Kept small and
    /// flat so it is forward/backward compatible across versions.
    /// </summary>
    public sealed class AppSettings
    {
        public AppTheme Theme { get; set; } = AppTheme.Light;

        /// <summary>Most-recently-opened archives (newest first).</summary>
        public List<string> RecentFiles { get; set; } = new List<string>();

        /// <summary>User-pinned favorite folders / archives.</summary>
        public List<string> Favorites { get; set; } = new List<string>();

        public List<CompressionProfile> Profiles { get; set; } =
            new List<CompressionProfile>(CompressionProfile.Defaults());

        public string LastOutputDirectory { get; set; }
        public string LastBrowseDirectory { get; set; }

        public int MaxRecentFiles { get; set; } = 12;

        /// <summary>0 = auto (all logical cores). Surfaced in Settings.</summary>
        public int DefaultThreadCount { get; set; } = 0;

        // Off by default: no network contact at startup unless the user opts in
        // (Settings -> "Check for updates on startup", or Help -> "Check for Updates").
        public bool CheckForUpdatesOnStartup { get; set; } = false;
        public bool ConfirmBeforeDelete { get; set; } = true;
        public bool VerboseLogging { get; set; } = false;

        // --- recent-files maintenance ------------------------------------
        public void PushRecent(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            RecentFiles.Insert(0, path);
            while (RecentFiles.Count > Math.Max(1, MaxRecentFiles))
                RecentFiles.RemoveAt(RecentFiles.Count - 1);
        }

        public void ToggleFavorite(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            int idx = Favorites.FindIndex(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) Favorites.RemoveAt(idx);
            else Favorites.Add(path);
        }

        // --- persistence --------------------------------------------------
        public static AppSettings Load()
        {
            try
            {
                string file = AppPaths.SettingsFile;
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file);
                    var loaded = JsonConvert.DeserializeObject<AppSettings>(json);
                    if (loaded != null) return loaded.Normalize();
                }
            }
            catch (Exception ex)
            {
                Logging.Logger.Warn("Could not load settings, using defaults: " + ex.Message);
            }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(AppPaths.SettingsFile, json);
            }
            catch (Exception ex)
            {
                Logging.Logger.Warn("Could not save settings: " + ex.Message);
            }
        }

        private AppSettings Normalize()
        {
            RecentFiles ??= new List<string>();
            Favorites ??= new List<string>();
            Profiles ??= new List<CompressionProfile>(CompressionProfile.Defaults());
            if (Profiles.Count == 0) Profiles.AddRange(CompressionProfile.Defaults());
            if (MaxRecentFiles <= 0) MaxRecentFiles = 12;
            return this;
        }
    }
}

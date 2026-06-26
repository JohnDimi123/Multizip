using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using Multizip.Core.Logging;

namespace Multizip.App.Util
{
    /// <summary>
    /// Adds / removes Windows Explorer context-menu entries under HKEY_CURRENT_USER,
    /// so it works without administrator rights and never disturbs other users. The
    /// installer can also create machine-wide entries; this is the portable / per-user
    /// path used from the Settings dialog.
    /// </summary>
    public static class ShellIntegration
    {
        // Archive extensions that get Open / Extract-here / Extract-to verbs.
        private static readonly string[] ArchiveExtensions =
        {
            ".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".xz", ".zst",
            ".cab", ".arj", ".lzh", ".lha", ".cpio", ".iso", ".wim", ".jar", ".apk"
        };

        private const string AddVerbKey = "Multizip.Add";
        private const string OpenVerbKey = "Multizip.Open";
        private const string ExtractHereKey = "Multizip.ExtractHere";
        private const string ExtractToKey = "Multizip.ExtractTo";

        public static bool IsRegistered()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey($@"Software\Classes\*\shell\{AddVerbKey}"))
                return k != null;
        }

        public static void Register()
        {
            string exe = Application.ExecutablePath;
            string icon = exe + ",0";

            // "Add to Multizip archive..." for any file and any folder.
            AddVerb($@"Software\Classes\*\shell\{AddVerbKey}", "Add to Multizip archive...", icon, $"\"{exe}\" /add \"%1\"");
            AddVerb($@"Software\Classes\Directory\shell\{AddVerbKey}", "Add to Multizip archive...", icon, $"\"{exe}\" /add \"%1\"");

            // Open / Extract verbs for recognised archive extensions.
            foreach (string ext in ArchiveExtensions)
            {
                string baseKey = $@"Software\Classes\SystemFileAssociations\{ext}\shell";
                AddVerb($@"{baseKey}\{OpenVerbKey}", "Open with Multizip", icon, $"\"{exe}\" /open \"%1\"");
                AddVerb($@"{baseKey}\{ExtractHereKey}", "Extract here (Multizip)", icon, $"\"{exe}\" /extracthere \"%1\"");
                AddVerb($@"{baseKey}\{ExtractToKey}", "Extract to folder... (Multizip)", icon, $"\"{exe}\" /extractto \"%1\"");
            }

            NotifyShell();
            Logger.Info("Registered Explorer context-menu integration (per-user).");
        }

        public static void Unregister()
        {
            DeleteTree($@"Software\Classes\*\shell\{AddVerbKey}");
            DeleteTree($@"Software\Classes\Directory\shell\{AddVerbKey}");
            foreach (string ext in ArchiveExtensions)
            {
                string baseKey = $@"Software\Classes\SystemFileAssociations\{ext}\shell";
                DeleteTree($@"{baseKey}\{OpenVerbKey}");
                DeleteTree($@"{baseKey}\{ExtractHereKey}");
                DeleteTree($@"{baseKey}\{ExtractToKey}");
            }
            NotifyShell();
            Logger.Info("Removed Explorer context-menu integration.");
        }

        private static void AddVerb(string keyPath, string caption, string icon, string command)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
            {
                key.SetValue(null, caption);
                key.SetValue("Icon", icon);
                using (RegistryKey cmd = key.CreateSubKey("command"))
                    cmd.SetValue(null, command);
            }
        }

        private static void DeleteTree(string keyPath)
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false); }
            catch (Exception ex) { Logger.Debug("Could not remove key " + keyPath + ": " + ex.Message); }
        }

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private static void NotifyShell()
        {
            const int SHCNE_ASSOCCHANGED = 0x08000000;
            const uint SHCNF_IDLIST = 0x0000;
            try { SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero); } catch { }
        }
    }
}

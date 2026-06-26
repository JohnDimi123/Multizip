using System;
using System.IO;

namespace Multizip.Core.Settings
{
    /// <summary>
    /// Resolves where Multizip keeps its data. In <b>portable</b> mode (a marker
    /// file named "multizip.portable" sits next to the executable) everything lives
    /// in a "Data" folder beside the app, so nothing touches the registry or the
    /// user profile. Otherwise we use %APPDATA%\Multizip.
    /// </summary>
    public static class AppPaths
    {
        private static string _root;

        public static bool IsPortable { get; private set; }

        public static void Initialize(string executableDirectory)
        {
            string marker = Path.Combine(executableDirectory, "multizip.portable");
            if (File.Exists(marker))
            {
                IsPortable = true;
                _root = Path.Combine(executableDirectory, "Data");
            }
            else
            {
                IsPortable = false;
                _root = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Multizip");
            }
            Directory.CreateDirectory(_root);
            Directory.CreateDirectory(LogDirectory);
        }

        public static string Root
        {
            get
            {
                if (_root == null)
                    Initialize(AppDomain.CurrentDomain.BaseDirectory);
                return _root;
            }
        }

        public static string SettingsFile => Path.Combine(Root, "settings.json");
        public static string LogDirectory => Path.Combine(Root, "Logs");
        public static string PluginsDirectory =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
    }
}

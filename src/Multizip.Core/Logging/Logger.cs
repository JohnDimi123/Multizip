using System;
using System.IO;
using System.Text;

namespace Multizip.Core.Logging
{
    public enum LogLevel { Debug, Info, Warning, Error }

    /// <summary>
    /// Tiny, dependency-free, thread-safe logger. Writes to a rolling file under the
    /// per-user data directory and also raises an event so the UI log pane can mirror
    /// messages live (just like the ImgBurn log window).
    /// </summary>
    public static class Logger
    {
        private static readonly object Gate = new object();
        private static string _logPath;
        private static LogLevel _minLevel = LogLevel.Info;

        /// <summary>Raised for every accepted message. (level, formatted line).</summary>
        public static event Action<LogLevel, string> Logged;

        public static LogLevel MinimumLevel
        {
            get { return _minLevel; }
            set { _minLevel = value; }
        }

        public static string LogPath => _logPath;

        public static void Initialize(string directory)
        {
            lock (Gate)
            {
                Directory.CreateDirectory(directory);
                _logPath = Path.Combine(directory, "multizip.log");
                RollIfTooLarge();
            }
            Info("==== Multizip session started " + DateTime.Now.ToString("u") + " ====");
        }

        public static void Debug(string message) => Write(LogLevel.Debug, message);
        public static void Info(string message) => Write(LogLevel.Info, message);
        public static void Warn(string message) => Write(LogLevel.Warning, message);
        public static void Error(string message) => Write(LogLevel.Error, message);

        public static void Error(string message, Exception ex)
            => Write(LogLevel.Error, message + " :: " + ex);

        private static void Write(LogLevel level, string message)
        {
            if (level < _minLevel) return;

            string line = string.Format("{0:HH:mm:ss} [{1}] {2}",
                DateTime.Now, LevelTag(level), message);

            try { Logged?.Invoke(level, line); } catch { /* never let UI break logging */ }

            lock (Gate)
            {
                if (_logPath == null) return;
                try
                {
                    File.AppendAllText(_logPath, line + Environment.NewLine, Encoding.UTF8);
                }
                catch { /* logging must never throw into the app */ }
            }
        }

        private static string LevelTag(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug: return "DBG";
                case LogLevel.Warning: return "WRN";
                case LogLevel.Error: return "ERR";
                default: return "INF";
            }
        }

        private static void RollIfTooLarge()
        {
            try
            {
                var fi = new FileInfo(_logPath);
                if (fi.Exists && fi.Length > 2 * 1024 * 1024) // 2 MB
                {
                    string bak = _logPath + ".1";
                    if (File.Exists(bak)) File.Delete(bak);
                    File.Move(_logPath, bak);
                }
            }
            catch { /* best effort */ }
        }
    }
}

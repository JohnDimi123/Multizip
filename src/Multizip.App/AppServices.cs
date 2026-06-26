using System;
using Multizip.App.Theme;
using Multizip.Core.Jobs;
using Multizip.Core.Logging;
using Multizip.Core.Providers;
using Multizip.Core.Settings;

namespace Multizip.App
{
    /// <summary>
    /// Process-wide singletons wired up once at startup. Keeping these in one place
    /// keeps the forms simple and makes the app easy to reason about.
    /// </summary>
    public static class AppServices
    {
        public static AppSettings Settings { get; private set; }
        public static ArchiveEngine Engine { get; private set; }
        public static JobQueue Queue { get; private set; }
        public static ThemeManager Theme { get; private set; }

        public static void Initialize()
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            AppPaths.Initialize(exeDir);
            Logger.Initialize(AppPaths.LogDirectory);

            Settings = AppSettings.Load();
            Logger.MinimumLevel = Settings.VerboseLogging ? LogLevel.Debug : LogLevel.Info;

            Engine = ArchiveEngine.CreateDefault(AppPaths.PluginsDirectory);
            Queue = new JobQueue(Engine);
            Theme = new ThemeManager(Settings.Theme);

            Logger.Info(AppPaths.IsPortable ? "Running in portable mode." : "Running in installed mode.");
        }

        public static void Shutdown()
        {
            try
            {
                Settings?.Save();
                Queue?.Dispose();
                Logger.Info("==== Multizip session ended ====");
            }
            catch (Exception ex)
            {
                Logger.Error("Error during shutdown", ex);
            }
        }
    }
}

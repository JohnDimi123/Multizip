using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Multizip.App.Forms;
using Multizip.Core.Logging;

namespace Multizip.App
{
    internal static class Program
    {
        /// <summary>
        /// Entry point. Besides the normal launch, this handles the verbs the Windows
        /// Explorer context-menu entries invoke:
        ///   Multizip.exe /open       "file"
        ///   Multizip.exe /extracthere "file"
        ///   Multizip.exe /extractto   "file"
        ///   Multizip.exe /test        "file"
        ///   Multizip.exe /hash        "file"
        ///   Multizip.exe /add         "path1" "path2" ...
        /// A bare path argument is treated as /open.
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => ReportCrash(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ReportCrash(e.ExceptionObject as Exception);

            try
            {
                AppServices.Initialize();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Multizip failed to start:\n\n" + ex,
                    "Multizip", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                Application.Run(BuildStartupForm(args));
            }
            finally
            {
                AppServices.Shutdown();
            }
        }

        private static Form BuildStartupForm(string[] args)
        {
            string verb = null;
            string[] paths;

            if (args.Length > 0 && args[0].StartsWith("/", StringComparison.Ordinal))
            {
                verb = args[0].TrimStart('/').ToLowerInvariant();
                paths = args.Skip(1).ToArray();
            }
            else
            {
                paths = args;
                if (paths.Length > 0) verb = "open";
            }

            var main = new MainForm();
            if (!string.IsNullOrEmpty(verb) && paths.Length > 0)
            {
                // Defer until the main window is shown so dialogs have an owner.
                main.PendingStartupAction = () => RunVerb(main, verb, paths);
            }
            return main;
        }

        private static void RunVerb(MainForm main, string verb, string[] paths)
        {
            string first = paths.FirstOrDefault(p => File.Exists(p) || Directory.Exists(p));
            switch (verb)
            {
                case "open":
                    if (first != null) main.OpenArchive(first);
                    break;
                case "extracthere":
                    if (first != null) main.QuickExtract(first, sameFolder: true);
                    break;
                case "extractto":
                    if (first != null) main.QuickExtract(first, sameFolder: false);
                    break;
                case "test":
                    if (first != null) main.TestArchive(first);
                    break;
                case "hash":
                    if (first != null) main.OpenChecksumTool(first);
                    break;
                case "add":
                    main.NewArchiveFrom(paths.Where(p => File.Exists(p) || Directory.Exists(p)).ToArray());
                    break;
                default:
                    if (first != null) main.OpenArchive(first);
                    break;
            }
        }

        private static void ReportCrash(Exception ex)
        {
            try { Logger.Error("Unhandled exception", ex ?? new Exception("unknown")); } catch { }
            MessageBox.Show(
                "An unexpected error occurred. It has been written to the log.\n\n" + ex?.Message,
                "Multizip", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

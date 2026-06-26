using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Multizip.App.Controls;
using Multizip.App.Util;
using Multizip.Core.Abstractions;
using Multizip.Core.Formats;
using Multizip.Core.Logging;
using Multizip.Core.Providers;
using Multizip.Core.Settings;
using Multizip.Core.Update;

namespace Multizip.App.Forms
{
    /// <summary>
    /// The home screen. Deliberately spartan and ImgBurn-like: a classic menu bar, a
    /// small toolbar, six large task buttons and a log pane along the bottom.
    /// </summary>
    public sealed class MainForm : Form
    {
        private MenuStrip _menu;
        private ToolStrip _toolbar;
        private StatusStrip _status;
        private ToolStripStatusLabel _statusText;
        private SplitContainer _split;
        private TextBox _log;
        private ToolStripMenuItem _recentMenu;
        private readonly List<BigButton> _buttons = new List<BigButton>();

        /// <summary>Invoked once after the window is shown (used for command-line verbs).</summary>
        public Action PendingStartupAction { get; set; }

        public MainForm()
        {
            BuildUi();
            HookLog();
        }

        // ----------------------------------------------------------------- UI
        private void BuildUi()
        {
            Text = "Multizip";
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(720, 520);
            MinimumSize = new Size(560, 420);
            StartPosition = FormStartPosition.CenterScreen;
            AllowDrop = true;

            BuildMenu();
            BuildToolbar();
            BuildStatus();
            BuildBody();

            // Docking order: fill first, then edges (menu ends on top).
            Controls.Add(_split);
            Controls.Add(_status);
            Controls.Add(_toolbar);
            Controls.Add(_menu);
            MainMenuStrip = _menu;

            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            Shown += OnShown;
            FormClosing += (s, e) => AppServices.Settings.Save();

            ApplyTheme();
        }

        private void BuildMenu()
        {
            _menu = new MenuStrip();

            var file = new ToolStripMenuItem("&File");
            file.DropDownItems.Add("&New Archive...", null, (s, e) => ShowCompress(null));
            file.DropDownItems.Add("&Open Archive...", null, (s, e) => OpenArchiveDialog());
            _recentMenu = new ToolStripMenuItem("Recent &Files");
            file.DropDownItems.Add(_recentMenu);
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add("E&xit", null, (s, e) => Close());

            var view = new ToolStripMenuItem("&View");
            var dark = new ToolStripMenuItem("&Dark Mode") { CheckOnClick = true, Checked = AppServices.Theme.IsDark };
            dark.Click += (s, e) => ToggleTheme();
            view.DropDownItems.Add(dark);
            view.DropDownItems.Add("Clear &Log", null, (s, e) => _log.Clear());

            var tools = new ToolStripMenuItem("&Tools");
            tools.DropDownItems.Add("&Extract Archive...", null, (s, e) => ExtractDialog());
            tools.DropDownItems.Add("&Test Integrity...", null, (s, e) => TestDialog());
            tools.DropDownItems.Add("&Checksum / Hash...", null, (s, e) => OpenChecksumTool(null));
            tools.DropDownItems.Add("&Benchmark...", null, (s, e) => new BenchmarkForm().ShowDialog(this));
            tools.DropDownItems.Add("Job &Queue...", null, (s, e) => new QueueForm().Show(this));
            tools.DropDownItems.Add(new ToolStripSeparator());
            tools.DropDownItems.Add("&Settings...", null, (s, e) => OpenSettings());

            var help = new ToolStripMenuItem("&Help");
            help.DropDownItems.Add("&View Log File", null, (s, e) => OpenLogFile());
            help.DropDownItems.Add("Check for &Updates", null, (s, e) => CheckForUpdates(interactive: true));
            help.DropDownItems.Add(new ToolStripSeparator());
            help.DropDownItems.Add("&About Multizip", null, (s, e) => new AboutForm().ShowDialog(this));

            _menu.Items.AddRange(new ToolStripItem[] { file, view, tools, help });
            RefreshRecent();
        }

        private void BuildToolbar()
        {
            _toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, ImageScalingSize = new Size(16, 16) };
            _toolbar.Items.Add(ToolButton("New", Glyphs.NewArchive(16), (s, e) => ShowCompress(null)));
            _toolbar.Items.Add(ToolButton("Open", Glyphs.OpenArchive(16), (s, e) => OpenArchiveDialog()));
            _toolbar.Items.Add(ToolButton("Extract", Glyphs.Extract(16), (s, e) => ExtractDialog()));
            _toolbar.Items.Add(new ToolStripSeparator());
            _toolbar.Items.Add(ToolButton("Test", Glyphs.Test(16), (s, e) => TestDialog()));
            _toolbar.Items.Add(ToolButton("Checksum", Glyphs.Checksum(16), (s, e) => OpenChecksumTool(null)));
            _toolbar.Items.Add(ToolButton("Benchmark", Glyphs.Benchmark(16), (s, e) => new BenchmarkForm().ShowDialog(this)));
        }

        private static ToolStripButton ToolButton(string text, Image image, EventHandler onClick)
        {
            var b = new ToolStripButton(text, image, onClick)
            {
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                TextImageRelation = TextImageRelation.ImageBeforeText
            };
            return b;
        }

        private void BuildStatus()
        {
            _status = new StatusStrip();
            _statusText = new ToolStripStatusLabel(BuildStatusText()) { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            _status.Items.Add(_statusText);
        }

        private string BuildStatusText()
        {
            string engine = SevenZipProvider.IsAvailable
                ? "7-Zip engine: ready"
                : "7-Zip engine: not found (managed fallback active)";
            string mode = AppPaths.IsPortable ? "Portable" : "Installed";
            return $"{mode}  |  {engine}";
        }

        private void BuildBody()
        {
            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 6,
                FixedPanel = FixedPanel.Panel2
            };

            // Launcher grid (Panel1).
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                Padding = new Padding(16)
            };
            for (int c = 0; c < 3; c++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            for (int r = 0; r < 2; r++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            grid.Controls.Add(MakeBig("Add to Archive", Glyphs.NewArchive(), (s, e) => ShowCompress(null)), 0, 0);
            grid.Controls.Add(MakeBig("Open / Browse", Glyphs.OpenArchive(), (s, e) => OpenArchiveDialog()), 1, 0);
            grid.Controls.Add(MakeBig("Extract", Glyphs.Extract(), (s, e) => ExtractDialog()), 2, 0);
            grid.Controls.Add(MakeBig("Test Integrity", Glyphs.Test(), (s, e) => TestDialog()), 0, 1);
            grid.Controls.Add(MakeBig("Checksum", Glyphs.Checksum(), (s, e) => OpenChecksumTool(null)), 1, 1);
            grid.Controls.Add(MakeBig("Benchmark", Glyphs.Benchmark(), (s, e) => new BenchmarkForm().ShowDialog(this)), 2, 1);

            _split.Panel1.Controls.Add(grid);

            // Log pane (Panel2), like the ImgBurn log window.
            var logHeader = new Label { Text = "Log", Dock = DockStyle.Top, Height = 18, Padding = new Padding(4, 2, 0, 0) };
            _log = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                BackColor = SystemColors.Window,
                Font = new Font("Consolas", 8.5f)
            };
            _split.Panel2.Controls.Add(_log);
            _split.Panel2.Controls.Add(logHeader);

            Load += (s, e) => { try { _split.SplitterDistance = ClientSize.Height - 180; } catch { } };
        }

        private BigButton MakeBig(string title, Image glyph, EventHandler onClick)
        {
            var b = new BigButton { Title = title, Glyph = glyph, Dock = DockStyle.Fill, Margin = new Padding(8) };
            b.UseTheme(AppServices.Theme);
            b.Click += onClick;
            _buttons.Add(b);
            return b;
        }

        // ------------------------------------------------------------- actions
        public void OpenArchive(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            AppServices.Settings.PushRecent(path);
            RefreshRecent();
            try
            {
                var browser = new BrowseForm(path);
                browser.Show(this);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to open archive", ex);
                MessageBox.Show(this, "Could not open the archive:\n\n" + ex.Message,
                    "Multizip", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void QuickExtract(string archivePath, bool sameFolder)
        {
            if (!File.Exists(archivePath)) return;

            if (!sameFolder)
            {
                ExtractDialogFor(archivePath);
                return;
            }

            string baseName = StripArchiveExtension(Path.GetFileName(archivePath));
            string dest = Path.Combine(Path.GetDirectoryName(archivePath) ?? ".", baseName);
            var options = new ExtractionOptions { Destination = dest, Overwrite = OverwriteMode.Overwrite };

            bool ok = ProgressForm.Run(this, "Extracting " + Path.GetFileName(archivePath),
                (p, t) => AppServices.Engine.Extract(archivePath, options, p, t));

            if (ok) Logger.Info("Extracted to " + dest);
            AppServices.Settings.PushRecent(archivePath);
            RefreshRecent();
        }

        public void TestArchive(string archivePath)
        {
            if (!File.Exists(archivePath)) return;
            bool result = false;
            bool ran = ProgressForm.Run(this, "Testing " + Path.GetFileName(archivePath),
                (p, t) => result = AppServices.Engine.Test(archivePath, null, p, t));

            if (ran)
            {
                MessageBox.Show(this,
                    result ? "The archive passed the integrity test." : "The archive is damaged or incomplete.",
                    "Multizip", MessageBoxButtons.OK,
                    result ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
        }

        public void OpenChecksumTool(string file)
        {
            var form = new ChecksumForm();
            if (!string.IsNullOrEmpty(file) && File.Exists(file)) form.SetInitialFile(file);
            form.Show(this);
        }

        public void NewArchiveFrom(string[] sources) => ShowCompress(sources);

        private void ShowCompress(string[] sources)
        {
            var form = new CompressForm();
            if (sources != null && sources.Length > 0) form.AddSources(sources);
            form.Show(this);
        }

        // ------------------------------------------------------------- dialogs
        private void OpenArchiveDialog()
        {
            using (var dlg = new OpenFileDialog
            {
                Title = "Open Archive",
                Filter = ArchiveFilter(),
                InitialDirectory = AppServices.Settings.LastBrowseDirectory
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    AppServices.Settings.LastBrowseDirectory = Path.GetDirectoryName(dlg.FileName);
                    OpenArchive(dlg.FileName);
                }
            }
        }

        private void ExtractDialog()
        {
            using (var dlg = new OpenFileDialog { Title = "Extract Archive", Filter = ArchiveFilter() })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK) ExtractDialogFor(dlg.FileName);
            }
        }

        private void ExtractDialogFor(string archivePath)
        {
            using (var form = new ExtractForm(archivePath))
            {
                form.ShowDialog(this);
            }
            AppServices.Settings.PushRecent(archivePath);
            RefreshRecent();
        }

        private void TestDialog()
        {
            using (var dlg = new OpenFileDialog { Title = "Test Archive Integrity", Filter = ArchiveFilter() })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK) TestArchive(dlg.FileName);
            }
        }

        private void OpenSettings()
        {
            using (var form = new SettingsForm())
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    ApplyTheme();
                    _statusText.Text = BuildStatusText();
                }
            }
        }

        // -------------------------------------------------------------- helpers
        private static string ArchiveFilter()
        {
            return "All archives|*.zip;*.7z;*.rar;*.tar;*.gz;*.tgz;*.bz2;*.xz;*.zst;*.lz4;*.cab;*.arj;*.lzh;*.lha;" +
                   "*.cpio;*.deb;*.rpm;*.iso;*.wim;*.vhd;*.vhdx;*.dmg;*.jar;*.war;*.apk;*.nupkg;*.z|" +
                   "ZIP archives|*.zip;*.jar;*.war;*.apk;*.nupkg|" +
                   "7-Zip archives|*.7z|" +
                   "RAR archives|*.rar|" +
                   "Tarballs|*.tar;*.gz;*.tgz;*.bz2;*.xz|" +
                   "Disk images|*.iso;*.wim;*.vhd;*.vhdx;*.dmg;*.udf|" +
                   "All files|*.*";
        }

        private static string StripArchiveExtension(string name)
        {
            string lower = name.ToLowerInvariant();
            string[] doubles = { ".tar.gz", ".tar.bz2", ".tar.xz", ".tar.zst" };
            foreach (string d in doubles)
                if (lower.EndsWith(d)) return name.Substring(0, name.Length - d.Length);
            int dot = name.LastIndexOf('.');
            return dot > 0 ? name.Substring(0, dot) : name;
        }

        private void RefreshRecent()
        {
            _recentMenu.DropDownItems.Clear();
            var recent = AppServices.Settings.RecentFiles;
            if (recent.Count == 0)
            {
                _recentMenu.DropDownItems.Add(new ToolStripMenuItem("(empty)") { Enabled = false });
                return;
            }
            foreach (string path in recent.Take(AppServices.Settings.MaxRecentFiles))
            {
                string captured = path;
                var item = new ToolStripMenuItem(Path.GetFileName(path)) { ToolTipText = path };
                item.Click += (s, e) => OpenArchive(captured);
                _recentMenu.DropDownItems.Add(item);
            }
            _recentMenu.DropDownItems.Add(new ToolStripSeparator());
            _recentMenu.DropDownItems.Add("Clear list", null, (s, e) =>
            {
                AppServices.Settings.RecentFiles.Clear();
                RefreshRecent();
            });
        }

        // -------------------------------------------------------------- theme/log
        private void ToggleTheme()
        {
            AppServices.Theme.Toggle();
            AppServices.Settings.Theme = AppServices.Theme.Current;
            AppServices.Settings.Save();
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            AppServices.Theme.Apply(this);
            _log.BackColor = AppServices.Theme.WindowBackground;
            _log.ForeColor = AppServices.Theme.WindowForeground;
            foreach (var b in _buttons) { b.UseTheme(AppServices.Theme); b.Invalidate(); }
        }

        private void HookLog()
        {
            Logger.Logged += (level, line) =>
            {
                if (_log == null || IsDisposed) return;
                try
                {
                    if (_log.InvokeRequired) _log.BeginInvoke(new Action(() => AppendLog(line)));
                    else AppendLog(line);
                }
                catch { /* form tearing down */ }
            };
        }

        private void AppendLog(string line)
        {
            if (_log.TextLength > 200_000) _log.Clear(); // keep memory bounded
            _log.AppendText(line + Environment.NewLine);
        }

        // -------------------------------------------------------------- updates
        private async void OnShown(object sender, EventArgs e)
        {
            try { PendingStartupAction?.Invoke(); }
            catch (Exception ex) { Logger.Error("Startup action failed", ex); }
            finally { PendingStartupAction = null; }

            if (AppServices.Settings.CheckForUpdatesOnStartup)
                await CheckForUpdatesQuiet();
        }

        private async System.Threading.Tasks.Task CheckForUpdatesQuiet()
        {
            try
            {
                var info = await new UpdateChecker().CheckAsync();
                if (info.UpdateAvailable)
                    _statusText.Text = $"Update available: v{info.LatestVersion}  |  " + BuildStatusText();
            }
            catch (Exception ex) { Logger.Debug("Update check failed: " + ex.Message); }
        }

        private async void CheckForUpdates(bool interactive)
        {
            var info = await new UpdateChecker().CheckAsync();
            if (!interactive) return;
            if (info.UpdateAvailable)
            {
                MessageBox.Show(this,
                    $"A newer version is available: v{info.LatestVersion}\nYou have v{info.CurrentVersion}.",
                    "Multizip", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(this, "You are running the latest version.",
                    "Multizip", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OpenLogFile()
        {
            try
            {
                if (File.Exists(Logger.LogPath))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Logger.LogPath) { UseShellExecute = true });
            }
            catch (Exception ex) { Logger.Warn("Could not open log: " + ex.Message); }
        }

        // -------------------------------------------------------------- drag/drop
        private void OnDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (paths == null || paths.Length == 0) return;

            if (paths.Length == 1 && File.Exists(paths[0]) &&
                FormatDetector.Detect(paths[0]) != ArchiveFormat.Unknown)
            {
                OpenArchive(paths[0]);
            }
            else
            {
                NewArchiveFrom(paths);
            }
        }
    }
}

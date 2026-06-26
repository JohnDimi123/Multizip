using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Multizip.Core.Abstractions;
using Multizip.Core.Formats;
using Multizip.Core.Jobs;
using Multizip.Core.Settings;

namespace Multizip.App.Forms
{
    /// <summary>Create a new archive from files / folders. Maps onto CompressionOptions.</summary>
    public sealed class CompressForm : Form
    {
        private ListView _sources;
        private TextBox _output;
        private ComboBox _format, _level, _profile, _volume;
        private TextBox _password;
        private CheckBox _encryptNames, _sfx;
        private NumericUpDown _threads;

        private static readonly (string Label, long Bytes)[] VolumePresets =
        {
            ("Single file (no split)", 0),
            ("10 MB", 10L * 1024 * 1024),
            ("100 MB", 100L * 1024 * 1024),
            ("700 MB (CD)", 700L * 1024 * 1024),
            ("4480 MB (DVD)", 4480L * 1024 * 1024),
            ("1024 MB", 1024L * 1024 * 1024),
        };

        public CompressForm()
        {
            BuildUi();
            AppServices.Theme.Apply(this);
        }

        public void AddSources(IEnumerable<string> paths)
        {
            foreach (string p in paths)
            {
                if (!File.Exists(p) && !Directory.Exists(p)) continue;
                if (_sources.Items.Cast<ListViewItem>().Any(i => string.Equals((string)i.Tag, p, StringComparison.OrdinalIgnoreCase)))
                    continue;
                bool dir = Directory.Exists(p);
                var item = new ListViewItem(Path.GetFileName(p.TrimEnd('\\', '/'))) { Tag = p };
                item.SubItems.Add(dir ? "Folder" : "File");
                item.SubItems.Add(dir ? "" : Core.Util.Format.Size(SafeLen(p)));
                _sources.Items.Add(item);
            }
            SuggestOutputPath();
        }

        private void BuildUi()
        {
            Text = "Create Archive";
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(560, 470);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            // --- sources ---
            var srcBox = new GroupBox { Text = "Files and folders to add", Left = 12, Top = 8, Width = 536, Height = 168 };
            _sources = new ListView
            {
                Left = 10, Top = 20, Width = 410, Height = 138, View = View.Details, FullRowSelect = true, GridLines = false
            };
            _sources.Columns.Add("Name", 230);
            _sources.Columns.Add("Type", 70);
            _sources.Columns.Add("Size", 90);
            srcBox.Controls.Add(_sources);

            var addFiles = new Button { Text = "Add Files...", Left = 428, Top = 20, Width = 96, Height = 26, FlatStyle = FlatStyle.System };
            var addFolder = new Button { Text = "Add Folder...", Left = 428, Top = 50, Width = 96, Height = 26, FlatStyle = FlatStyle.System };
            var remove = new Button { Text = "Remove", Left = 428, Top = 80, Width = 96, Height = 26, FlatStyle = FlatStyle.System };
            addFiles.Click += (s, e) => AddFilesDialog();
            addFolder.Click += (s, e) => AddFolderDialog();
            remove.Click += (s, e) => { foreach (ListViewItem i in _sources.SelectedItems) i.Remove(); };
            srcBox.Controls.AddRange(new Control[] { addFiles, addFolder, remove });
            Controls.Add(srcBox);

            // --- output ---
            Controls.Add(new Label { Text = "Output archive:", Left = 12, Top = 188, Width = 100 });
            _output = new TextBox { Left = 116, Top = 185, Width = 330 };
            var browse = new Button { Text = "Browse...", Left = 452, Top = 183, Width = 96, Height = 26, FlatStyle = FlatStyle.System };
            browse.Click += (s, e) => BrowseOutput();
            Controls.Add(_output);
            Controls.Add(browse);

            // --- options ---
            var opt = new GroupBox { Text = "Options", Left = 12, Top = 220, Width = 536, Height = 168 };

            opt.Controls.Add(new Label { Text = "Format:", Left = 12, Top = 26, Width = 70 });
            _format = new ComboBox { Left = 86, Top = 22, Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (ArchiveFormat f in AppServices.Engine.WritableFormats) _format.Items.Add(f);
            if (_format.Items.Count > 0) _format.SelectedIndex = 0;
            _format.SelectedIndexChanged += (s, e) => OnFormatChanged();
            opt.Controls.Add(_format);

            opt.Controls.Add(new Label { Text = "Level:", Left = 246, Top = 26, Width = 50 });
            _level = new ComboBox { Left = 300, Top = 22, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            _level.Items.AddRange(Enum.GetNames(typeof(CompressionLevel)));
            _level.SelectedItem = nameof(CompressionLevel.Normal);
            opt.Controls.Add(_level);

            opt.Controls.Add(new Label { Text = "Split:", Left = 12, Top = 58, Width = 70 });
            _volume = new ComboBox { Left = 86, Top = 54, Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var v in VolumePresets) _volume.Items.Add(v.Label);
            _volume.SelectedIndex = 0;
            opt.Controls.Add(_volume);

            opt.Controls.Add(new Label { Text = "Threads:", Left = 246, Top = 58, Width = 50 });
            _threads = new NumericUpDown { Left = 300, Top = 54, Width = 120, Minimum = 0, Maximum = 256, Value = Clamp(AppServices.Settings.DefaultThreadCount, 0, 256) };
            opt.Controls.Add(_threads);

            opt.Controls.Add(new Label { Text = "Password:", Left = 12, Top = 90, Width = 70 });
            _password = new TextBox { Left = 86, Top = 86, Width = 140, UseSystemPasswordChar = true };
            opt.Controls.Add(_password);

            _encryptNames = new CheckBox { Text = "Encrypt file names (AES-256)", Left = 246, Top = 88, Width = 220, Checked = true };
            opt.Controls.Add(_encryptNames);

            _sfx = new CheckBox { Text = "Self-extracting (.exe)", Left = 86, Top = 116, Width = 200 };
            opt.Controls.Add(_sfx);

            opt.Controls.Add(new Label { Text = "Profile:", Left = 246, Top = 120, Width = 50 });
            _profile = new ComboBox { Left = 300, Top = 116, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            _profile.Items.Add("(custom)");
            foreach (var p in AppServices.Settings.Profiles) _profile.Items.Add(p.Name);
            _profile.SelectedIndex = 0;
            _profile.SelectedIndexChanged += (s, e) => ApplyProfile();
            opt.Controls.Add(_profile);

            Controls.Add(opt);

            // --- buttons ---
            var compress = new Button { Text = "Compress", Left = 270, Top = 400, Width = 100, Height = 30, FlatStyle = FlatStyle.System };
            var queue = new Button { Text = "Add to Queue", Left = 376, Top = 400, Width = 110, Height = 30, FlatStyle = FlatStyle.System };
            var close = new Button { Text = "Close", Left = 492, Top = 400, Width = 56, Height = 30, FlatStyle = FlatStyle.System };
            compress.Click += (s, e) => DoCompress(enqueueOnly: false);
            queue.Click += (s, e) => DoCompress(enqueueOnly: true);
            close.Click += (s, e) => Close();
            Controls.AddRange(new Control[] { compress, queue, close });
            AcceptButton = compress;

            OnFormatChanged();
        }

        // ----------------------------------------------------------------- helpers
        private void AddFilesDialog()
        {
            using (var dlg = new OpenFileDialog { Multiselect = true, Title = "Add Files" })
                if (dlg.ShowDialog(this) == DialogResult.OK) AddSources(dlg.FileNames);
        }

        private void AddFolderDialog()
        {
            using (var dlg = new FolderBrowserDialog { Description = "Add a folder" })
                if (dlg.ShowDialog(this) == DialogResult.OK) AddSources(new[] { dlg.SelectedPath });
        }

        private void BrowseOutput()
        {
            var fmt = (ArchiveFormat)_format.SelectedItem;
            using (var dlg = new SaveFileDialog
            {
                Title = "Save Archive As",
                FileName = Path.GetFileName(_output.Text),
                Filter = $"{fmt} archive|*.{ExtensionFor(fmt)}|All files|*.*"
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK) _output.Text = dlg.FileName;
            }
        }

        private void OnFormatChanged()
        {
            if (_format.SelectedItem == null) return;
            var fmt = (ArchiveFormat)_format.SelectedItem;
            _sfx.Enabled = fmt == ArchiveFormat.SevenZip;
            if (!_sfx.Enabled) _sfx.Checked = false;
            _encryptNames.Enabled = fmt == ArchiveFormat.SevenZip;
            SuggestOutputPath();
        }

        private void ApplyProfile()
        {
            if (_profile.SelectedIndex <= 0) return;
            string name = _profile.SelectedItem.ToString();
            var profile = AppServices.Settings.Profiles.FirstOrDefault(p => p.Name == name);
            if (profile == null) return;

            SelectFormat(profile.Format);
            _level.SelectedItem = profile.Level.ToString();
            _threads.Value = Clamp(profile.ThreadCount, 0, 256);
            _encryptNames.Checked = profile.EncryptFileNames;
            _sfx.Checked = profile.SelfExtracting && profile.Format == ArchiveFormat.SevenZip;
            SelectVolume(profile.VolumeSize);
        }

        private void SelectFormat(ArchiveFormat fmt)
        {
            for (int i = 0; i < _format.Items.Count; i++)
                if ((ArchiveFormat)_format.Items[i] == fmt) { _format.SelectedIndex = i; return; }
        }

        private void SelectVolume(long bytes)
        {
            for (int i = 0; i < VolumePresets.Length; i++)
                if (VolumePresets[i].Bytes == bytes) { _volume.SelectedIndex = i; return; }
            _volume.SelectedIndex = 0;
        }

        private void SuggestOutputPath()
        {
            if (_sources.Items.Count == 0 || _format.SelectedItem == null) return;
            if (!string.IsNullOrEmpty(_output.Text)) return;

            string first = (string)_sources.Items[0].Tag;
            string dir = Directory.Exists(first) ? Path.GetDirectoryName(first.TrimEnd('\\', '/')) : Path.GetDirectoryName(first);
            string baseName = Path.GetFileNameWithoutExtension(first.TrimEnd('\\', '/'));
            if (string.IsNullOrEmpty(baseName)) baseName = "archive";
            var fmt = (ArchiveFormat)_format.SelectedItem;
            _output.Text = Path.Combine(dir ?? ".", baseName + "." + ExtensionFor(fmt));
        }

        private CompressionOptions BuildOptions(out string outputPath)
        {
            var fmt = (ArchiveFormat)_format.SelectedItem;
            var options = new CompressionOptions
            {
                Format = fmt,
                Level = (CompressionLevel)Enum.Parse(typeof(CompressionLevel), _level.SelectedItem.ToString()),
                Password = string.IsNullOrEmpty(_password.Text) ? null : _password.Text,
                EncryptFileNames = _encryptNames.Checked,
                ThreadCount = (int)_threads.Value,
                VolumeSize = VolumePresets[_volume.SelectedIndex].Bytes,
                SelfExtracting = _sfx.Checked && fmt == ArchiveFormat.SevenZip,
                PreservePaths = true
            };

            foreach (ListViewItem item in _sources.Items) options.SourcePaths.Add((string)item.Tag);

            string first = options.SourcePaths[0];
            options.BaseDirectory = Directory.Exists(first)
                ? Path.GetDirectoryName(first.TrimEnd('\\', '/'))
                : Path.GetDirectoryName(first);

            outputPath = _output.Text;
            if (options.SelfExtracting && !outputPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                outputPath = Path.ChangeExtension(outputPath, ".exe");
            return options;
        }

        private void DoCompress(bool enqueueOnly)
        {
            if (_sources.Items.Count == 0)
            {
                MessageBox.Show(this, "Add at least one file or folder.", "Multizip", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (string.IsNullOrWhiteSpace(_output.Text))
            {
                MessageBox.Show(this, "Choose an output path.", "Multizip", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            CompressionOptions options = BuildOptions(out string outputPath);

            if (enqueueOnly)
            {
                AppServices.Queue.Enqueue(new CompressJob(outputPath, options));
                MessageBox.Show(this, "Added to the job queue.", "Multizip", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool ok = ProgressForm.Run(this, "Creating " + Path.GetFileName(outputPath),
                (p, t) => AppServices.Engine.Create(outputPath, options, p, t));
            if (ok)
            {
                AppServices.Settings.PushRecent(outputPath);
                MessageBox.Show(this, "Archive created:\n" + outputPath, "Multizip", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
        }

        private static string ExtensionFor(ArchiveFormat fmt)
        {
            switch (fmt)
            {
                case ArchiveFormat.SevenZip: return "7z";
                case ArchiveFormat.Zip: return "zip";
                case ArchiveFormat.Tar: return "tar";
                case ArchiveFormat.Gzip: return "gz";
                case ArchiveFormat.Bzip2: return "bz2";
                case ArchiveFormat.Xz: return "xz";
                default: return fmt.ToString().ToLowerInvariant();
            }
        }

        private static long SafeLen(string p)
        {
            try { return new FileInfo(p).Length; } catch { return 0; }
        }

        private static int Clamp(int value, int min, int max)
            => value < min ? min : (value > max ? max : value);
    }
}

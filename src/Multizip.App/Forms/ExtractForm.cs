using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Multizip.Core.Abstractions;
using Multizip.Core.Jobs;

namespace Multizip.App.Forms
{
    /// <summary>Extract a whole archive, or a pre-selected subset of its entries.</summary>
    public sealed class ExtractForm : Form
    {
        private readonly string _archivePath;
        private readonly List<string> _selected;

        private TextBox _destination, _password;
        private ComboBox _overwrite;
        private CheckBox _preservePaths, _openWhenDone;

        public ExtractForm(string archivePath, IEnumerable<string> selectedEntries = null)
        {
            _archivePath = archivePath;
            _selected = selectedEntries?.ToList() ?? new List<string>();
            BuildUi();
            AppServices.Theme.Apply(this);
        }

        private void BuildUi()
        {
            Text = "Extract Archive";
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(520, 250);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            string scope = _selected.Count > 0 ? $"{_selected.Count} selected item(s)" : "all items";
            Controls.Add(new Label { Text = $"Archive:  {Path.GetFileName(_archivePath)}  ({scope})", Left = 12, Top = 12, Width = 496, AutoEllipsis = true });

            Controls.Add(new Label { Text = "Extract to:", Left = 12, Top = 46, Width = 80 });
            _destination = new TextBox { Left = 96, Top = 43, Width = 320, Text = DefaultDestination() };
            var browse = new Button { Text = "Browse...", Left = 422, Top = 41, Width = 86, Height = 26, FlatStyle = FlatStyle.System };
            browse.Click += (s, e) => BrowseDest();
            Controls.Add(_destination);
            Controls.Add(browse);

            Controls.Add(new Label { Text = "Password:", Left = 12, Top = 82, Width = 80 });
            _password = new TextBox { Left = 96, Top = 79, Width = 200, UseSystemPasswordChar = true };
            Controls.Add(_password);

            Controls.Add(new Label { Text = "If exists:", Left = 12, Top = 118, Width = 80 });
            _overwrite = new ComboBox { Left = 96, Top = 114, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            _overwrite.Items.AddRange(new object[] { "Overwrite", "Skip", "Keep both (rename)" });
            _overwrite.SelectedIndex = 0;
            Controls.Add(_overwrite);

            _preservePaths = new CheckBox { Text = "Preserve folder structure", Left = 96, Top = 146, Width = 220, Checked = true };
            _openWhenDone = new CheckBox { Text = "Open destination when finished", Left = 96, Top = 170, Width = 260, Checked = true };
            Controls.Add(_preservePaths);
            Controls.Add(_openWhenDone);

            var extract = new Button { Text = "Extract", Left = 230, Top = 206, Width = 100, Height = 30, FlatStyle = FlatStyle.System };
            var queue = new Button { Text = "Add to Queue", Left = 336, Top = 206, Width = 110, Height = 30, FlatStyle = FlatStyle.System };
            var cancel = new Button { Text = "Cancel", Left = 452, Top = 206, Width = 56, Height = 30, FlatStyle = FlatStyle.System };
            extract.Click += (s, e) => DoExtract(enqueueOnly: false);
            queue.Click += (s, e) => DoExtract(enqueueOnly: true);
            cancel.Click += (s, e) => Close();
            Controls.AddRange(new Control[] { extract, queue, cancel });
            AcceptButton = extract;
            CancelButton = cancel;
        }

        private string DefaultDestination()
        {
            string dir = Path.GetDirectoryName(_archivePath) ?? ".";
            string name = Path.GetFileNameWithoutExtension(_archivePath);
            return Path.Combine(dir, name);
        }

        private void BrowseDest()
        {
            using (var dlg = new FolderBrowserDialog { Description = "Choose destination", SelectedPath = _destination.Text })
                if (dlg.ShowDialog(this) == DialogResult.OK) _destination.Text = dlg.SelectedPath;
        }

        private ExtractionOptions BuildOptions()
        {
            var options = new ExtractionOptions
            {
                Destination = _destination.Text,
                Password = string.IsNullOrEmpty(_password.Text) ? null : _password.Text,
                PreservePaths = _preservePaths.Checked,
                Overwrite = _overwrite.SelectedIndex == 1 ? OverwriteMode.Skip
                          : _overwrite.SelectedIndex == 2 ? OverwriteMode.RenameNew
                          : OverwriteMode.Overwrite
            };
            options.SelectedEntries.AddRange(_selected);
            return options;
        }

        private void DoExtract(bool enqueueOnly)
        {
            if (string.IsNullOrWhiteSpace(_destination.Text))
            {
                MessageBox.Show(this, "Choose a destination folder.", "Multizip", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ExtractionOptions options = BuildOptions();

            if (enqueueOnly)
            {
                AppServices.Queue.Enqueue(new ExtractJob(_archivePath, options));
                MessageBox.Show(this, "Added to the job queue.", "Multizip", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
                return;
            }

            bool ok = ProgressForm.Run(this, "Extracting " + Path.GetFileName(_archivePath),
                (p, t) => AppServices.Engine.Extract(_archivePath, options, p, t));

            if (ok)
            {
                if (_openWhenDone.Checked && Directory.Exists(options.Destination))
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(options.Destination) { UseShellExecute = true }); }
                    catch { /* best effort */ }
                }
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }
}

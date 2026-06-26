using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Multizip.Core.Abstractions;
using Multizip.Core.Logging;
using Multizip.Core.Util;

namespace Multizip.App.Forms
{
    /// <summary>
    /// Browse an archive's contents without extracting. Uses a virtual ListView so it
    /// stays responsive even with hundreds of thousands of entries, and offers fast
    /// in-archive search, extract-selected, integrity test and file preview.
    /// </summary>
    public sealed class BrowseForm : Form
    {
        private readonly string _archivePath;
        private string _password;

        private ToolStrip _toolbar;
        private TextBox _search;
        private ListView _list;
        private StatusStrip _status;
        private ToolStripStatusLabel _statusText;

        private readonly List<ArchiveEntry> _all = new List<ArchiveEntry>();
        private List<ArchiveEntry> _view = new List<ArchiveEntry>();
        private ArchiveListing _listing;

        public BrowseForm(string archivePath)
        {
            _archivePath = archivePath;
            BuildUi();
            AppServices.Theme.Apply(this);
            Load += async (s, e) => await LoadListingAsync();
        }

        private void BuildUi()
        {
            Text = "Multizip - " + Path.GetFileName(_archivePath);
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(780, 520);
            StartPosition = FormStartPosition.CenterParent;

            _toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
            _toolbar.Items.Add(new ToolStripButton("Extract All", null, (s, e) => ExtractAll()));
            _toolbar.Items.Add(new ToolStripButton("Extract Selected", null, (s, e) => ExtractSelected()));
            _toolbar.Items.Add(new ToolStripSeparator());
            _toolbar.Items.Add(new ToolStripButton("Preview", null, (s, e) => PreviewSelected()));
            _toolbar.Items.Add(new ToolStripButton("Test", null, (s, e) => TestArchive()));
            _toolbar.Items.Add(new ToolStripSeparator());
            _toolbar.Items.Add(new ToolStripLabel("Search:"));
            var host = new ToolStripControlHost(_search = new TextBox { Width = 220 });
            _toolbar.Items.Add(host);
            _search.TextChanged += (s, e) => ApplyFilter();

            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                VirtualMode = true,
                MultiSelect = true,
                HideSelection = false
            };
            _list.Columns.Add("Name", 220);
            _list.Columns.Add("Path", 240);
            _list.Columns.Add("Size", 90, HorizontalAlignment.Right);
            _list.Columns.Add("Modified", 120);
            _list.Columns.Add("CRC", 80);
            _list.RetrieveVirtualItem += OnRetrieveVirtualItem;
            _list.DoubleClick += (s, e) => PreviewSelected();

            _status = new StatusStrip();
            _statusText = new ToolStripStatusLabel("Loading...") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            _status.Items.Add(_statusText);

            Controls.Add(_list);
            Controls.Add(_status);
            Controls.Add(_toolbar);
        }

        private async Task LoadListingAsync()
        {
            UseWaitCursor = true;
            _statusText.Text = "Reading archive...";
            try
            {
                string pwd = _password;
                _listing = await Task.Run(() => AppServices.Engine.List(_archivePath, pwd));
            }
            catch (Exception ex) when (LooksEncrypted(ex))
            {
                if (PromptPassword())
                {
                    UseWaitCursor = false;
                    await LoadListingAsync();
                    return;
                }
                Close();
                return;
            }
            catch (Exception ex)
            {
                UseWaitCursor = false;
                Logger.Error("Failed to list archive", ex);
                MessageBox.Show(this, "Could not read the archive:\n\n" + ex.Message,
                    "Multizip", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
                return;
            }

            _all.Clear();
            _all.AddRange(_listing.Entries);
            ApplyFilter();
            UseWaitCursor = false;
        }

        private void OnRetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (e.ItemIndex < 0 || e.ItemIndex >= _view.Count) { e.Item = new ListViewItem(); return; }
            ArchiveEntry entry = _view[e.ItemIndex];
            var item = new ListViewItem(entry.Name);
            item.SubItems.Add(entry.Path);
            item.SubItems.Add(entry.IsDirectory ? "<DIR>" : Format.Size(entry.Size));
            item.SubItems.Add(entry.LastModified?.ToString("yyyy-MM-dd HH:mm") ?? "");
            item.SubItems.Add(entry.Crc32?.ToString("x8") ?? "");
            if (entry.IsEncrypted) item.ForeColor = Color.SteelBlue;
            e.Item = item;
        }

        private void ApplyFilter()
        {
            string q = _search.Text?.Trim();
            _view = string.IsNullOrEmpty(q)
                ? new List<ArchiveEntry>(_all)
                : _all.Where(en => en.Path.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            _list.VirtualListSize = _view.Count;
            _list.Invalidate();

            long shown = _view.Where(en => !en.IsDirectory).Sum(en => en.Size);
            _statusText.Text = $"{_listing?.Format}  |  {_view.Count} of {_all.Count} items  |  {Format.Size(shown)}" +
                               (_listing != null && _listing.IsEncrypted ? "  |  Encrypted" : "");
        }

        private List<string> SelectedPaths()
        {
            var paths = new List<string>();
            foreach (int idx in _list.SelectedIndices)
                if (idx >= 0 && idx < _view.Count && !_view[idx].IsDirectory)
                    paths.Add(_view[idx].Path);
            return paths;
        }

        private void ExtractAll()
        {
            using (var form = new ExtractForm(_archivePath))
                form.ShowDialog(this);
        }

        private void ExtractSelected()
        {
            var paths = SelectedPaths();
            if (paths.Count == 0)
            {
                MessageBox.Show(this, "Select one or more files first.", "Multizip", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var form = new ExtractForm(_archivePath, paths))
                form.ShowDialog(this);
        }

        private void TestArchive()
        {
            bool result = false;
            bool ran = ProgressForm.Run(this, "Testing " + Path.GetFileName(_archivePath),
                (p, t) => result = AppServices.Engine.Test(_archivePath, _password, p, t));
            if (ran)
                MessageBox.Show(this, result ? "Integrity test passed." : "Archive is damaged.",
                    "Multizip", MessageBoxButtons.OK, result ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private void PreviewSelected()
        {
            if (_list.SelectedIndices.Count == 0) return;
            int idx = _list.SelectedIndices[0];
            if (idx < 0 || idx >= _view.Count) return;
            ArchiveEntry entry = _view[idx];
            if (entry.IsDirectory) return;

            if (!PreviewForm.CanPreview(entry.Name))
            {
                MessageBox.Show(this, "No built-in preview for this file type.", "Multizip", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "Multizip_preview", Guid.NewGuid().ToString("N"));
            var options = new ExtractionOptions { Destination = tempDir, Password = _password, PreservePaths = false };
            options.SelectedEntries.Add(entry.Path);

            bool ok = ProgressForm.Run(this, "Preparing preview",
                (p, t) => AppServices.Engine.Extract(_archivePath, options, p, t));
            if (!ok) return;

            string file = Path.Combine(tempDir, PathUtil.LeafName(entry.Path));
            if (File.Exists(file))
                new PreviewForm(file, entry.Name).Show(this);
        }

        private bool PromptPassword()
        {
            using (var dlg = new PasswordForm(Path.GetFileName(_archivePath)))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _password = dlg.Password;
                    return true;
                }
            }
            return false;
        }

        private static bool LooksEncrypted(Exception ex)
        {
            string m = ex.Message?.ToLowerInvariant() ?? "";
            return m.Contains("password") || m.Contains("encrypt") || m.Contains("wrong") || m.Contains("crc");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Multizip.Core.Hashing;

namespace Multizip.App.Forms
{
    /// <summary>
    /// Built-in checksum generator and verifier (CRC-32, MD5, SHA-1/256/512). All
    /// selected hashes are computed in a single streaming pass over the file.
    /// </summary>
    public sealed class ChecksumForm : Form
    {
        private TextBox _file;
        private readonly Dictionary<HashType, CheckBox> _checks = new Dictionary<HashType, CheckBox>();
        private readonly Dictionary<HashType, TextBox> _outputs = new Dictionary<HashType, TextBox>();
        private TextBox _expected;
        private Label _verifyResult;
        private Button _compute;
        private ProgressBar _bar;
        private CancellationTokenSource _cts;

        public ChecksumForm()
        {
            BuildUi();
            AppServices.Theme.Apply(this);
        }

        public void SetInitialFile(string path) => _file.Text = path;

        private void BuildUi()
        {
            Text = "Checksum / Hash";
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(560, 360);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            Controls.Add(new Label { Text = "File:", Left = 12, Top = 16, Width = 40 });
            _file = new TextBox { Left = 56, Top = 13, Width = 392 };
            var browse = new Button { Text = "Browse...", Left = 454, Top = 11, Width = 92, Height = 26, FlatStyle = FlatStyle.System };
            browse.Click += (s, e) => { using (var d = new OpenFileDialog()) if (d.ShowDialog(this) == DialogResult.OK) _file.Text = d.FileName; };
            Controls.Add(_file);
            Controls.Add(browse);

            int top = 50;
            foreach (HashType type in Enum.GetValues(typeof(HashType)))
            {
                var chk = new CheckBox { Text = Label(type), Left = 16, Top = top + 3, Width = 90, Checked = type != HashType.Crc32 && type != HashType.Sha512 };
                var outp = new TextBox { Left = 112, Top = top, Width = 434, ReadOnly = true, Font = new Font("Consolas", 9f) };
                _checks[type] = chk;
                _outputs[type] = outp;
                Controls.Add(chk);
                Controls.Add(outp);
                top += 30;
            }

            _bar = new ProgressBar { Left = 16, Top = top + 4, Width = 530, Height = 16, Maximum = 1000 };
            Controls.Add(_bar);
            top += 28;

            var verifyBox = new GroupBox { Text = "Verify against expected value", Left = 12, Top = top, Width = 536, Height = 70 };
            _expected = new TextBox { Left = 12, Top = 24, Width = 380, Font = new Font("Consolas", 9f) };
            var verify = new Button { Text = "Verify", Left = 400, Top = 22, Width = 80, Height = 26, FlatStyle = FlatStyle.System };
            _verifyResult = new Label { Left = 12, Top = 50, Width = 510, Text = "" };
            verify.Click += (s, e) => DoVerify();
            verifyBox.Controls.AddRange(new Control[] { _expected, verify, _verifyResult });
            Controls.Add(verifyBox);
            top += 80;

            _compute = new Button { Text = "Compute", Left = 360, Top = top, Width = 100, Height = 30, FlatStyle = FlatStyle.System };
            var copy = new Button { Text = "Copy All", Left = 466, Top = top, Width = 80, Height = 30, FlatStyle = FlatStyle.System };
            _compute.Click += async (s, e) => await ComputeAsync();
            copy.Click += (s, e) => CopyAll();
            Controls.Add(_compute);
            Controls.Add(copy);
            AcceptButton = _compute;
        }

        private async Task ComputeAsync()
        {
            if (!File.Exists(_file.Text))
            {
                MessageBox.Show(this, "Choose an existing file.", "Multizip", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var selected = _checks.Where(kv => kv.Value.Checked).Select(kv => kv.Key).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Select at least one hash.", "Multizip", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (var o in _outputs.Values) o.Clear();
            _compute.Enabled = false;
            _cts = new CancellationTokenSource();
            var progress = new Progress<double>(p => _bar.Value = (int)Math.Min(1000, p * 10));
            string path = _file.Text;

            try
            {
                Dictionary<HashType, string> results = await Task.Run(
                    () => ChecksumGenerator.ComputeMany(path, selected, progress, _cts.Token));
                foreach (var kv in results) _outputs[kv.Key].Text = kv.Value;
                _bar.Value = 1000;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Hashing failed:\n" + ex.Message, "Multizip", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { _compute.Enabled = true; }
        }

        private void DoVerify()
        {
            string expected = _expected.Text?.Trim();
            if (string.IsNullOrEmpty(expected)) return;

            var match = _outputs.Values.FirstOrDefault(o =>
                !string.IsNullOrEmpty(o.Text) &&
                string.Equals(o.Text.Trim(), expected, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                _verifyResult.ForeColor = Color.Green;
                _verifyResult.Text = "MATCH - the file is intact.";
            }
            else
            {
                _verifyResult.ForeColor = Color.Firebrick;
                _verifyResult.Text = "NO MATCH against any computed hash above.";
            }
        }

        private void CopyAll()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(Path.GetFileName(_file.Text));
            foreach (var kv in _outputs)
                if (!string.IsNullOrEmpty(kv.Value.Text))
                    sb.AppendLine($"{Label(kv.Key)}: {kv.Value.Text}");
            if (sb.Length > 0) Clipboard.SetText(sb.ToString());
        }

        private static string Label(HashType t)
        {
            switch (t)
            {
                case HashType.Crc32: return "CRC-32";
                case HashType.Md5: return "MD5";
                case HashType.Sha1: return "SHA-1";
                case HashType.Sha256: return "SHA-256";
                case HashType.Sha512: return "SHA-512";
                default: return t.ToString();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _cts?.Cancel();
            base.OnFormClosing(e);
        }
    }
}

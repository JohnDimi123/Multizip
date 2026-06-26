using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Multizip.Core.Benchmark;

namespace Multizip.App.Forms
{
    /// <summary>Runs the built-in compression benchmark and shows throughput + ratio.</summary>
    public sealed class BenchmarkForm : Form
    {
        private ComboBox _size;
        private Button _run;
        private ListView _results;
        private Label _status;
        private CancellationTokenSource _cts;

        public BenchmarkForm()
        {
            BuildUi();
            AppServices.Theme.Apply(this);
        }

        private void BuildUi()
        {
            Text = "Compression Benchmark";
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(560, 340);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            Controls.Add(new Label { Text = "Test data size:", Left = 12, Top = 16, Width = 90 });
            _size = new ComboBox { Left = 106, Top = 12, Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
            _size.Items.AddRange(new object[] { "16 MB", "32 MB", "64 MB", "128 MB", "256 MB" });
            _size.SelectedIndex = 2;
            Controls.Add(_size);

            _run = new Button { Text = "Run", Left = 220, Top = 10, Width = 90, Height = 28, FlatStyle = FlatStyle.System };
            _run.Click += async (s, e) => await RunAsync();
            Controls.Add(_run);

            _results = new ListView { Left = 12, Top = 48, Width = 536, Height = 230, View = View.Details, FullRowSelect = true, GridLines = true };
            _results.Columns.Add("Algorithm", 160);
            _results.Columns.Add("Ratio", 80, HorizontalAlignment.Right);
            _results.Columns.Add("Compressed", 100, HorizontalAlignment.Right);
            _results.Columns.Add("Compress", 90, HorizontalAlignment.Right);
            _results.Columns.Add("Decompress", 90, HorizontalAlignment.Right);
            Controls.Add(_results);

            _status = new Label { Left = 12, Top = 290, Width = 536, Text = "Ready." };
            Controls.Add(_status);
        }

        private async Task RunAsync()
        {
            _run.Enabled = false;
            _results.Items.Clear();
            int mb = int.Parse(_size.SelectedItem.ToString().Replace(" MB", ""));
            _cts = new CancellationTokenSource();
            var status = new Progress<string>(text => _status.Text = "Running: " + text);

            try
            {
                IReadOnlyList<BenchmarkResult> results = await Task.Run(
                    () => CompressionBenchmark.Run(mb, status, _cts.Token), _cts.Token);

                foreach (BenchmarkResult r in results)
                {
                    var item = new ListViewItem(r.Algorithm);
                    item.SubItems.Add(r.RatioPercent.ToString("0.0") + "%");
                    item.SubItems.Add(Core.Util.Format.Size(r.CompressedBytes));
                    item.SubItems.Add(r.CompressMBPerSecond.ToString("0.0") + " MB/s");
                    item.SubItems.Add(r.DecompressMBPerSecond.ToString("0.0") + " MB/s");
                    _results.Items.Add(item);
                }
                _status.Text = "Done.";
            }
            catch (OperationCanceledException) { _status.Text = "Cancelled."; }
            catch (Exception ex) { _status.Text = "Error: " + ex.Message; }
            finally { _run.Enabled = true; }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _cts?.Cancel();
            base.OnFormClosing(e);
        }
    }
}

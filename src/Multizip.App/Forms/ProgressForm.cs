using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Multizip.Core.Abstractions;
using Multizip.Core.Util;

namespace Multizip.App.Forms
{
    /// <summary>
    /// Modal progress dialog with the ImgBurn-style detail readout: a progress bar
    /// plus current file, speed, elapsed/remaining time, processed/total size and
    /// live percentage. Runs the supplied operation on a background thread and can
    /// cancel it cleanly.
    /// </summary>
    public sealed class ProgressForm : Form
    {
        private readonly Action<IProgress<ProgressInfo>, CancellationToken> _work;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Stopwatch _uiThrottle = Stopwatch.StartNew();

        private ProgressBar _bar;
        private Label _file, _percent, _speed, _elapsed, _remaining, _size;
        private Button _cancel;
        private bool _finished;
        private Exception _error;

        private ProgressForm(string title, Action<IProgress<ProgressInfo>, CancellationToken> work)
        {
            _work = work;
            BuildUi(title);
        }

        /// <summary>
        /// Runs <paramref name="work"/> with progress. Returns true on success,
        /// false if it was cancelled or failed (the error is shown to the user).
        /// </summary>
        public static bool Run(IWin32Window owner, string title,
            Action<IProgress<ProgressInfo>, CancellationToken> work)
        {
            using (var form = new ProgressForm(title, work))
            {
                form.ShowDialog(owner);
                if (form._error != null)
                {
                    MessageBox.Show(owner,
                        "The operation failed:\n\n" + form._error.Message,
                        "Multizip", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
                return form._finished && !form._cts.IsCancellationRequested;
            }
        }

        private void BuildUi(string title)
        {
            Text = title;
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(460, 210);
            AppServices.Theme.Apply(this);

            _file = new Label { Left = 12, Top = 14, Width = 436, AutoEllipsis = true, Text = "Preparing..." };
            _bar = new ProgressBar { Left = 12, Top = 40, Width = 436, Height = 22, Style = ProgressBarStyle.Continuous, Maximum = 1000 };
            _percent = new Label { Left = 12, Top = 68, Width = 200, Text = "0%" };

            _size = MakeStat("Processed:", 100);
            _speed = MakeStat("Speed:", 124);
            _elapsed = MakeStat("Elapsed:", 148);
            _remaining = MakeStat("Remaining:", 172);

            _cancel = new Button { Text = "Cancel", Width = 90, Height = 26, Left = 358, Top = 172, FlatStyle = FlatStyle.System };
            _cancel.Click += (s, e) => RequestCancel();

            Controls.Add(_file);
            Controls.Add(_bar);
            Controls.Add(_percent);
            Controls.Add(_cancel);

            Load += OnLoad;
            FormClosing += OnClosing;
        }

        private Label MakeStat(string caption, int top)
        {
            var label = new Label { Left = 12, Top = top, Width = 90, Text = caption, ForeColor = SystemColors.GrayText };
            var value = new Label { Left = 104, Top = top, Width = 240, Text = "-" };
            Controls.Add(label);
            Controls.Add(value);
            return value;
        }

        private void OnLoad(object sender, EventArgs e)
        {
            var progress = new Progress<ProgressInfo>(OnProgress);
            Task.Run(() =>
            {
                try
                {
                    _work(progress, _cts.Token);
                }
                catch (OperationCanceledException) { /* user cancelled */ }
                catch (Exception ex) { _error = ex; }
            }).ContinueWith(_ => OnComplete(), TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void OnProgress(ProgressInfo info)
        {
            // Throttle to ~15 fps so very chatty providers do not flood the UI thread,
            // but always honour the final 100% update.
            if (_uiThrottle.ElapsedMilliseconds < 66 && info.Percent < 100) return;
            _uiThrottle.Restart();

            _bar.Value = (int)Math.Min(1000, Math.Round(info.Percent * 10));
            _percent.Text = info.Percent.ToString("0.0") + "%";
            _file.Text = string.IsNullOrEmpty(info.CurrentFile) ? "Working..." : info.CurrentFile;
            _size.Text = $"{Format.Size(info.BytesProcessed)} / {Format.Size(info.BytesTotal)}";
            _speed.Text = Format.Rate(info.BytesPerSecond);
            _elapsed.Text = Format.Duration(info.Elapsed);
            _remaining.Text = Format.Duration(info.EstimatedRemaining);
        }

        private void OnComplete()
        {
            _finished = true;
            DialogResult = _error == null && !_cts.IsCancellationRequested ? DialogResult.OK : DialogResult.Cancel;
            Close();
        }

        private void RequestCancel()
        {
            _cancel.Enabled = false;
            _cancel.Text = "Cancelling...";
            _cts.Cancel();
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (!_finished)
            {
                // Don't allow closing the window out from under a running job.
                e.Cancel = true;
                RequestCancel();
            }
        }
    }
}

using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Multizip.Core.Abstractions;
using Multizip.Core.Jobs;
using Multizip.Core.Util;

namespace Multizip.App.Forms
{
    /// <summary>
    /// Shows the background job queue and lets the user watch progress and cancel
    /// individual or all jobs. Events from the queue arrive on a worker thread and
    /// are marshalled onto the UI thread here.
    /// </summary>
    public sealed class QueueForm : Form
    {
        private ListView _list;
        private readonly JobQueue _queue;

        public QueueForm()
        {
            _queue = AppServices.Queue;
            BuildUi();
            AppServices.Theme.Apply(this);

            _queue.JobStarted += OnJobChanged;
            _queue.JobProgress += OnJobProgress;
            _queue.JobFinished += OnJobChanged;
            Refresh2();
        }

        private void BuildUi()
        {
            Text = "Job Queue";
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(560, 320);
            StartPosition = FormStartPosition.CenterParent;

            _list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true };
            _list.Columns.Add("Job", 300);
            _list.Columns.Add("Status", 90);
            _list.Columns.Add("Progress", 140, HorizontalAlignment.Right);

            var bar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
            bar.Items.Add(new ToolStripButton("Cancel Selected", null, (s, e) => _queue.CancelCurrent()));
            bar.Items.Add(new ToolStripButton("Cancel All", null, (s, e) => _queue.CancelAll()));

            Controls.Add(_list);
            Controls.Add(bar);
        }

        private void OnJobChanged(ArchiveJob job) => SafeRefresh();
        private void OnJobProgress(ArchiveJob job, ProgressInfo info) => SafeRefresh();

        private void SafeRefresh()
        {
            if (IsDisposed) return;
            try
            {
                if (InvokeRequired) BeginInvoke(new Action(Refresh2));
                else Refresh2();
            }
            catch { /* closing */ }
        }

        private void Refresh2()
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (ArchiveJob job in _queue.Jobs)
            {
                var item = new ListViewItem(job.Title);
                item.SubItems.Add(job.Status.ToString());
                string progress = job.LastProgress != null
                    ? job.LastProgress.Percent.ToString("0") + "%  " + Format.Rate(job.LastProgress.BytesPerSecond)
                    : (job.Status == JobStatus.Completed ? "100%" : "-");
                item.SubItems.Add(progress);
                if (job.Status == JobStatus.Failed) item.ForeColor = Color.Firebrick;
                else if (job.Status == JobStatus.Completed) item.ForeColor = Color.Green;
                _list.Items.Add(item);
            }
            _list.EndUpdate();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _queue.JobStarted -= OnJobChanged;
            _queue.JobProgress -= OnJobProgress;
            _queue.JobFinished -= OnJobChanged;
            base.OnFormClosed(e);
        }
    }
}

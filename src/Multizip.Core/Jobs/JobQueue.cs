using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Multizip.Core.Abstractions;
using Multizip.Core.Logging;
using Multizip.Core.Providers;

namespace Multizip.Core.Jobs
{
    /// <summary>
    /// Sequential background queue for compression / extraction / test jobs. Each
    /// job runs one at a time (the providers are themselves multi-threaded, so this
    /// keeps disk thrash and CPU contention predictable on large workloads). Events
    /// are raised on a background thread - the UI marshals them to the UI thread.
    /// </summary>
    public sealed class JobQueue : IDisposable
    {
        private readonly ArchiveEngine _engine;
        private readonly BlockingCollection<ArchiveJob> _queue = new BlockingCollection<ArchiveJob>();
        private readonly List<ArchiveJob> _all = new List<ArchiveJob>();
        private readonly object _gate = new object();
        private CancellationTokenSource _currentCts;
        private Thread _worker;
        private volatile bool _stopping;

        public JobQueue(ArchiveEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "Multizip.JobQueue" };
            _worker.Start();
        }

        public event Action<ArchiveJob> JobStarted;
        public event Action<ArchiveJob, ProgressInfo> JobProgress;
        public event Action<ArchiveJob> JobFinished; // completed / failed / cancelled
        public event Action QueueIdle;

        public IReadOnlyList<ArchiveJob> Jobs
        {
            get { lock (_gate) return _all.ToArray(); }
        }

        public void Enqueue(ArchiveJob job)
        {
            if (job == null) return;
            lock (_gate) _all.Add(job);
            _queue.Add(job);
            Logger.Info("Queued job: " + job.Title);
        }

        /// <summary>Cancel the job currently executing (the rest of the queue continues).</summary>
        public void CancelCurrent() => _currentCts?.Cancel();

        /// <summary>Cancel everything and stop processing.</summary>
        public void CancelAll()
        {
            _currentCts?.Cancel();
            lock (_gate)
            {
                foreach (var job in _all)
                    if (job.Status == JobStatus.Queued)
                        job.Status = JobStatus.Cancelled;
            }
        }

        private void WorkerLoop()
        {
            foreach (ArchiveJob job in _queue.GetConsumingEnumerable())
            {
                if (_stopping) break;
                if (job.Status == JobStatus.Cancelled)
                {
                    JobFinished?.Invoke(job);
                    continue;
                }

                _currentCts = new CancellationTokenSource();
                job.Status = JobStatus.Running;
                JobStarted?.Invoke(job);

                var progress = new Progress<ProgressInfo>(p =>
                {
                    job.LastProgress = p;
                    JobProgress?.Invoke(job, p);
                });

                try
                {
                    job.Execute(_engine, progress, _currentCts.Token);
                    job.Status = JobStatus.Completed;
                    Logger.Info("Job completed: " + job.Title);
                }
                catch (OperationCanceledException)
                {
                    job.Status = JobStatus.Cancelled;
                    Logger.Warn("Job cancelled: " + job.Title);
                }
                catch (Exception ex)
                {
                    job.Status = JobStatus.Failed;
                    job.Error = ex.Message;
                    Logger.Error("Job failed: " + job.Title, ex);
                }
                finally
                {
                    _currentCts.Dispose();
                    _currentCts = null;
                    JobFinished?.Invoke(job);
                }

                if (_queue.Count == 0) QueueIdle?.Invoke();
            }
        }

        public void Dispose()
        {
            _stopping = true;
            _currentCts?.Cancel();
            _queue.CompleteAdding();
            try { _worker?.Join(2000); } catch { /* shutting down */ }
            _queue.Dispose();
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using BamboozClipStudio.Core;

namespace BamboozClipStudio.Services;

public sealed class BackgroundJobService : ObservableObject
{
    readonly SemaphoreSlim _gate = new(1, 1);

    string _currentJobTitle = string.Empty;
    string _currentJobStatus = string.Empty;
    double _currentJobProgress;
    bool _isIndeterminate;
    bool _isBusy;
    int _queuedJobs;
    Action? _cancelCurrent;

    public string CurrentJobTitle
    {
        get => _currentJobTitle;
        private set => SetProperty(ref _currentJobTitle, value);
    }

    public string CurrentJobStatus
    {
        get => _currentJobStatus;
        private set => SetProperty(ref _currentJobStatus, value);
    }

    public double CurrentJobProgress
    {
        get => _currentJobProgress;
        private set => SetProperty(ref _currentJobProgress, value);
    }

    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        private set => SetProperty(ref _isIndeterminate, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public int QueuedJobs
    {
        get => _queuedJobs;
        private set => SetProperty(ref _queuedJobs, value);
    }

    public bool CanCancel => IsBusy && _cancelCurrent != null;

    public async Task<JobLease> EnqueueAsync(string title, CancellationToken cancellationToken = default)
    {
        await RunOnUiThreadAsync(() =>
        {
            QueuedJobs++;
            if (!IsBusy)
            {
                CurrentJobTitle = title;
                CurrentJobStatus = "Waiting to start…";
                IsIndeterminate = true;
            }
        });

        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await RunOnUiThreadAsync(() => QueuedJobs = Math.Max(0, QueuedJobs - 1));
            throw;
        }

        await RunOnUiThreadAsync(() =>
        {
            QueuedJobs = Math.Max(0, QueuedJobs - 1);
            IsBusy = true;
            CurrentJobTitle = title;
            CurrentJobStatus = "Starting…";
            CurrentJobProgress = 0;
            IsIndeterminate = true;
            _cancelCurrent = null;
            OnPropertyChanged(nameof(CanCancel));
        });

        return new JobLease(this);
    }

    public void Report(string title, string status, double? progress = null, bool? isIndeterminate = null)
    {
        _ = RunOnUiThreadAsync(() =>
        {
            CurrentJobTitle = title;
            CurrentJobStatus = status;
            if (progress.HasValue)
                CurrentJobProgress = Math.Clamp(progress.Value, 0, 100);
            if (isIndeterminate.HasValue)
                IsIndeterminate = isIndeterminate.Value;
        });
    }

    public void RegisterCancellation(Action cancel)
    {
        _ = RunOnUiThreadAsync(() =>
        {
            _cancelCurrent = cancel;
            OnPropertyChanged(nameof(CanCancel));
        });
    }

    public void ClearCancellation()
    {
        _ = RunOnUiThreadAsync(() =>
        {
            _cancelCurrent = null;
            OnPropertyChanged(nameof(CanCancel));
        });
    }

    public void CancelCurrentJob() => _cancelCurrent?.Invoke();

    async Task CompleteAsync()
    {
        await RunOnUiThreadAsync(() =>
        {
            IsBusy = false;
            CurrentJobTitle = string.Empty;
            CurrentJobStatus = string.Empty;
            CurrentJobProgress = 0;
            IsIndeterminate = false;
            _cancelCurrent = null;
            OnPropertyChanged(nameof(CanCancel));
        });

        _gate.Release();
    }

    static Task RunOnUiThreadAsync(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action).Task;
    }

    public sealed class JobLease : IDisposable
    {
        readonly BackgroundJobService _owner;
        bool _disposed;

        public JobLease(BackgroundJobService owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _owner.CompleteAsync().GetAwaiter().GetResult();
        }
    }
}

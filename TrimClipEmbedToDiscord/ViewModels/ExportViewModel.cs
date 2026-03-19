using BamboozClipStudio.Core;
using BamboozClipStudio.Models;
using BamboozClipStudio.Services;
using System.IO;
using System.Windows;

namespace BamboozClipStudio.ViewModels;

public class ExportViewModel : ObservableObject
{
    readonly FFmpegService _ffmpeg;
    readonly UploadService _upload;
    readonly SettingsService _settings;
    readonly BackgroundJobService _jobs;
    AppSettings _appSettings;

    string _estimatedSize = "—";
    bool _isExporting;
    double _exportProgress;
    string _exportStatus = string.Empty;
    CancellationTokenSource? _exportCts;

    public ExportSettings Settings { get; }

    public string EstimatedSize { get => _estimatedSize; private set => SetProperty(ref _estimatedSize, value); }
    public bool IsExporting    { get => _isExporting;    private set => SetProperty(ref _isExporting,    value); }
    public double ExportProgress { get => _exportProgress; private set => SetProperty(ref _exportProgress, value); }
    public string ExportStatus { get => _exportStatus;   private set => SetProperty(ref _exportStatus,   value); }
    public string QueueStatus => _jobs.QueuedJobs > 0 ? $"{_jobs.QueuedJobs} queued" : string.Empty;

    public AsyncRelayCommand ExportCommand { get; }
    public RelayCommand BrowseFolderCommand { get; }
    public RelayCommand CancelExportCommand { get; }

    // Injected before export runs
    public Func<(TimeSpan InPoint, TimeSpan OutPoint, ClipProject? Clip, string VfChain)>? GetExportContext { get; set; }

    /// <summary>Fired after a successful export completes.</summary>
    public event Action? ExportCompleted;

    public string[] ContainerOptions { get; } = ["mp4", "mkv", "webm"];
    public string[] VideoCodecOptions { get; } = ["libx264", "libx265", "libvpx-vp9"];
    public string[] AudioCodecOptions { get; } = ["aac", "libopus", "mp3"];
    public string[] PresetOptions { get; } = ["ultrafast", "superfast", "fast", "medium", "slow", "slower", "veryslow"];
    public string[] ProfileOptions { get; } = ["baseline", "main", "high"];
    public string[] BitrateModeOptions { get; } = ["CRF (Quality)", "CBR (Bitrate)"];
    public bool IsCrf => Settings.BitrateMode == BitrateMode.Crf;

    public ExportViewModel(FFmpegService ffmpeg, UploadService upload, SettingsService settings, BackgroundJobService jobs)
    {
        _ffmpeg = ffmpeg;
        _upload = upload;
        _settings = settings;
        _jobs = jobs;
        _appSettings = settings.Load();

        Settings = new ExportSettings
        {
            OutputFolder = _appSettings.LastOutputFolder,
            MixAudioTracks = _appSettings.MixAudioTracks,
            EmbedUpload = _appSettings.EmbedUpload,
            HardwareAcceleration = _appSettings.HardwareAcceleration,
        };

        Settings.PropertyChanged += (_, _) =>
        {
            UpdateEstimatedSize();
            OnPropertyChanged(nameof(IsCrf));
            SaveSettingsFromExport();
        };

        _jobs.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BackgroundJobService.QueuedJobs))
                OnPropertyChanged(nameof(QueueStatus));
        };

        ExportCommand = new AsyncRelayCommand(RunExportAsync, () => !IsExporting);
        BrowseFolderCommand = new RelayCommand(BrowseFolder);
        CancelExportCommand = new RelayCommand(() => _exportCts?.Cancel(), () => IsExporting);
    }

    public void UpdateEstimatedSize()
    {
        var ctx = GetExportContext?.Invoke();
        if (ctx == null || ctx.Value.Clip == null) { EstimatedSize = "—"; return; }

        var duration = (ctx.Value.OutPoint - ctx.Value.InPoint).TotalSeconds;
        if (duration <= 0) { EstimatedSize = "—"; return; }

        double audioBits = 192_000 * duration;
        double videoBits;

        if (Settings.BitrateMode == BitrateMode.Cbr)
        {
            videoBits = Settings.Bitrate * 1000.0 * duration;
        }
        else
        {
            // CRF heuristic: pixels * duration * quality_factor
            var clip = ctx.Value.Clip;
            double pixels = clip.Width * clip.Height;
            double qualityFactor = Settings.Crf switch
            {
                <= 18 => 0.07,
                <= 23 => 0.05,
                <= 28 => 0.035,
                _ => 0.02
            };
            videoBits = pixels * duration * qualityFactor;
        }

        double totalMb = (videoBits + audioBits) / 8.0 / 1_048_576.0;
        EstimatedSize = $"~{totalMb:F1} MB";
    }

    async Task RunExportAsync()
    {
        var ctx = GetExportContext?.Invoke();
        if (ctx == null || ctx.Value.Clip == null)
        {
            DialogService.ShowWarning("Export", "No media loaded.");
            return;
        }

        var (inPoint, outPoint, clip, vfChain) = ctx.Value;

        string ext = Settings.Container;
        string fileName = $"{Path.GetFileNameWithoutExtension(clip!.FilePath)}_trimmed_{DateTime.Now:HHmmss}.{ext}";
        string outPath = Path.Combine(Settings.OutputFolder, fileName);

        IsExporting = true;
        ExportProgress = 0;
        ExportStatus = "Encoding…";
        _exportCts = new CancellationTokenSource();

        try
        {
            using var job = await _jobs.EnqueueAsync("Exporting clip");
            _jobs.RegisterCancellation(() => _exportCts?.Cancel());
            _jobs.Report("Exporting clip", "Preparing export…", isIndeterminate: true);

            await _ffmpeg.EnsureFfmpegAvailableAsync(null);

            // Probe GPU encoders if HW accel is enabled
            if (Settings.HardwareAcceleration)
                await _ffmpeg.ProbeGpuEncodersAsync();

            int trackCount = clip.AudioTrackCount;
            var progress = new Progress<double>(p =>
            {
                ExportProgress = p;
                ExportStatus = $"Encoding… {p:F0}%";
                _jobs.Report("Exporting clip", ExportStatus, p, isIndeterminate: false);
            });
            await _ffmpeg.EncodeAsync(Settings, string.IsNullOrEmpty(vfChain) ? null : vfChain,
                                      inPoint, outPoint, clip.FilePath, outPath,
                                      trackCount, outPoint - inPoint, progress, _exportCts.Token,
                                      Settings.HardwareAcceleration);

            ExportProgress = 100;
            ExportStatus = "Done!";
            _jobs.Report("Exporting clip", "Encoding complete.", 100, isIndeterminate: false);
            ExportCompleted?.Invoke();

            if (Settings.EmbedUpload)
            {
                ExportStatus = "Uploading…";
                var uploadVm = new UploadProgressViewModel(_upload);
                var dialog = new Views.UploadProgressDialog(uploadVm) { Owner = Application.Current.MainWindow };
                dialog.Show();
                _jobs.Report("Uploading clip", "Uploading to catbox.moe…", 0, isIndeterminate: true);
                await uploadVm.StartUploadAsync(outPath, _exportCts);
                ExportStatus = uploadVm.StatusMessage;
                _jobs.Report("Uploading clip", uploadVm.StatusMessage, uploadVm.ProgressPercent,
                             isIndeterminate: uploadVm.IsIndeterminate);
            }
            else
            {
                DialogService.ShowInfo("Export Complete", $"Exported to:\n{outPath}");
            }
        }
        catch (OperationCanceledException)
        {
            ExportStatus = "Cancelled.";
            _jobs.Report("Background work", "Cancelled.", 0, isIndeterminate: false);
            if (File.Exists(outPath)) try { File.Delete(outPath); } catch { }
        }
        catch (Exception ex)
        {
            ExportStatus = "Failed.";
            _jobs.Report("Background work", $"Failed: {ex.Message}", 0, isIndeterminate: false);
            DialogService.ShowError("Error", $"Export failed:\n{ex.Message}");
        }
        finally
        {
            IsExporting = false;
            _exportCts?.Dispose();
            _exportCts = null;
            _jobs.ClearCancellation();
        }
    }

    void BrowseFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select output folder",
            InitialDirectory = Settings.OutputFolder
        };
        if (dialog.ShowDialog() == true)
        {
            Settings.OutputFolder = dialog.FolderName;
            _appSettings.LastOutputFolder = dialog.FolderName;
            _settings.Save(_appSettings);
        }
    }

    void SaveSettingsFromExport()
    {
        _appSettings.MixAudioTracks = Settings.MixAudioTracks;
        _appSettings.EmbedUpload = Settings.EmbedUpload;
        _settings.Save(_appSettings);
    }

}

using BamboozClipStudio.Core;
using BamboozClipStudio.Models;
using BamboozClipStudio.Services;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace BamboozClipStudio.ViewModels;

public class MainViewModel : ObservableObject
{
    readonly FFmpegService _ffmpeg;
    readonly SettingsService _settings;
    readonly UpdateService _update;

    AppSettings _appSettings;

    ClipProject? _clip;
    bool _hasMedia;
    bool _isLoading;
    string _loadingStatus = string.Empty;
    double _loadingProgress;
    bool _isLoadingIndeterminate = true;
    BitmapImage? _previewFrame;

    public ClipProject? Clip
    {
        get => _clip;
        private set
        {
            SetProperty(ref _clip, value);
            HasMedia = value != null;
            Timeline.Clip = value;
            Export.UpdateEstimatedSize();
        }
    }

    public bool HasMedia            { get => _hasMedia;        private set => SetProperty(ref _hasMedia,        value); }
    public bool IsLoading           { get => _isLoading;       private set => SetProperty(ref _isLoading,       value); }
    public string LoadingStatus     { get => _loadingStatus;   private set => SetProperty(ref _loadingStatus,   value); }
    public double LoadingProgress   { get => _loadingProgress; private set => SetProperty(ref _loadingProgress, value); }
    public bool IsLoadingIndeterminate { get => _isLoadingIndeterminate; private set => SetProperty(ref _isLoadingIndeterminate, value); }
    public BitmapImage? PreviewFrame { get => _previewFrame;   private set => SetProperty(ref _previewFrame,   value); }

    public TimelineViewModel Timeline { get; }
    public FiltersViewModel   Filters { get; }
    public ExportViewModel    Export  { get; }

    public bool AutoCheckUpdates
    {
        get => _appSettings.AutoCheckUpdates;
        set
        {
            _appSettings.AutoCheckUpdates = value;
            _settings.Save(_appSettings);
            OnPropertyChanged();
        }
    }

    public AsyncRelayCommand OpenMediaCommand { get; }
    public RelayCommand       ExitCommand     { get; }
    public AsyncRelayCommand  CheckUpdatesCommand { get; }
    public RelayCommand       ToggleFiltersCommand { get; }
    public RelayCommand       RegisterContextMenuCommand { get; }

    public MainViewModel(FFmpegService ffmpeg, FilterService filterService,
                         UploadService upload, SettingsService settings, UpdateService update)
    {
        _ffmpeg   = ffmpeg;
        _settings = settings;
        _update   = update;
        _appSettings = settings.Load();

        Timeline = new TimelineViewModel();
        Filters  = new FiltersViewModel(filterService);
        Export   = new ExportViewModel(ffmpeg, upload, settings);

        Export.GetExportContext = () => (
            Timeline.InPoint,
            Timeline.OutPoint,
            Clip,
            Filters.BuildVfChain()
        );

        Timeline.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TimelineViewModel.CurrentPosition))
                _ = UpdatePreviewFrameAsync();
        };

        Filters.FiltersChanged += () => _ = UpdatePreviewFrameAsync();

        OpenMediaCommand       = new AsyncRelayCommand(OpenMediaAsync);
        ExitCommand            = new RelayCommand(() => Application.Current.Shutdown());
        CheckUpdatesCommand    = new AsyncRelayCommand(CheckUpdatesAsync);
        ToggleFiltersCommand   = new RelayCommand(Filters.Toggle);
        RegisterContextMenuCommand = new RelayCommand(RegisterContextMenu);
    }

    public async Task InitializeAsync()
    {
        // Download FFmpeg if needed
        IsLoading = true;
        IsLoadingIndeterminate = true;
        LoadingStatus = "Checking FFmpeg…";

        try
        {
            await _ffmpeg.EnsureFfmpegAvailableAsync(new Progress<string>(msg =>
            {
                LoadingStatus = msg;
            }));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not initialize FFmpeg:\n{ex.Message}\n\nThe app may not function correctly.",
                            "FFmpeg Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsLoading = false;
        }

        // Auto update check
        if (_appSettings.AutoCheckUpdates)
            _ = CheckUpdatesAsync();
    }

    public async Task LoadFileAsync(string path)
    {
        if (!File.Exists(path)) return;

        IsLoading = true;
        IsLoadingIndeterminate = true;
        LoadingStatus = "Loading media…";
        Clip = null;
        PreviewFrame = null;

        try
        {
            await _ffmpeg.EnsureFfmpegAvailableAsync(null);
            var clip = await _ffmpeg.GetMediaInfoAsync(path);
            Clip = clip;

            LoadingStatus = "Generating thumbnails…";
            var cts = new CancellationTokenSource();
            await _ffmpeg.GenerateThumbnailsAsync(path, 20,
                (index, img) => Application.Current.Dispatcher.Invoke(() =>
                {
                    double ratio = (double)index / 20;
                    Timeline.Thumbnails.Add(new ThumbnailItem(index, ratio, img));
                }),
                cts.Token);

            await UpdatePreviewFrameAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load media:\n{ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
            Clip = null;
        }
        finally
        {
            IsLoading = false;
        }
    }

    async Task OpenMediaAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Open Media",
            Filter = "Video Files|*.mp4;*.mkv;*.mov;*.avi;*.webm|All Files|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
        };
        if (dialog.ShowDialog() != true) return;
        await LoadFileAsync(dialog.FileName);
    }

    async Task UpdatePreviewFrameAsync()
    {
        if (_clip == null) return;
        try
        {
            var vf = Filters.BuildVfChain();
            var frame = await _ffmpeg.ExtractFrameAsync(_clip.FilePath, Timeline.CurrentPosition,
                                                        string.IsNullOrEmpty(vf) ? null : vf);
            PreviewFrame = frame;
        }
        catch { /* best-effort */ }
    }

    async Task CheckUpdatesAsync()
    {
        var result = await _update.CheckForUpdateAsync();
        if (!result.UpdateAvailable) return;

        var answer = MessageBox.Show(
            $"Bambooz Clip Studio {result.LatestVersion} is available.\n\nUpdate now?",
            "Update Available", MessageBoxButton.YesNo, MessageBoxImage.Information);

        if (answer != MessageBoxResult.Yes) return;

        IsLoading = true;
        IsLoadingIndeterminate = false;
        LoadingStatus = "Downloading update…";

        try
        {
            var installerPath = await _update.DownloadInstallerAsync(
                result.InstallerUrl!,
                new Progress<double>(p => { LoadingProgress = p; LoadingStatus = $"Downloading… {p:F0}%"; }),
                CancellationToken.None);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true
            });
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            IsLoading = false;
            MessageBox.Show($"Update download failed:\n{ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void RegisterContextMenu()
    {
        try
        {
            string exe = System.Reflection.Assembly.GetExecutingAssembly().Location
                             .Replace(".dll", ".exe");
            string[] exts = [".mp4", ".mkv", ".mov", ".avi", ".webm"];

            foreach (var ext in exts)
            {
                string keyPath = $@"Software\Classes\{ext}\shell\BamboozClipStudio";
                using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(keyPath);
                key.SetValue("", "Open with Bambooz Clip Studio");
                key.SetValue("Icon", $"\"{exe}\",0");
                using var cmd = key.CreateSubKey("command");
                cmd.SetValue("", $"\"{exe}\" \"%1\"");
            }

            MessageBox.Show("Context menu registered for .mp4, .mkv, .mov, .avi, .webm\n\nRight-click any video file to open in Bambooz Clip Studio.",
                            "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to register context menu:\n{ex.Message}\n\nTry running as administrator.",
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

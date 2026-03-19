using BamboozClipStudio.Core;
using BamboozClipStudio.Models;
using BamboozClipStudio.Services;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace BamboozClipStudio.ViewModels;

public class MainViewModel : ObservableObject
{
    readonly FFmpegService _ffmpeg;
    readonly SettingsService _settings;
    readonly UpdateService _update;
    readonly BackgroundJobService _jobs;
    readonly DispatcherTimer _previewDebounce;
    readonly DispatcherTimer _sessionSaveDebounce;

    AppSettings _appSettings;

    ClipProject? _clip;
    bool _hasMedia;
    bool _isLoading;
    string _loadingStatus = string.Empty;
    string _loadingDetail = string.Empty;
    double _loadingProgress;
    bool _isLoadingIndeterminate = true;
    BitmapImage? _previewFrame;
    string _dependencyStatus = "FFmpeg status: checking…";
    bool _startupRecoveryPending;

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

    public bool HasMedia            { get => _hasMedia;        private set { SetProperty(ref _hasMedia, value); OnPropertyChanged(nameof(ShowPreviewImage)); OnPropertyChanged(nameof(ShowVideoPlayer)); } }
    public bool IsLoading           { get => _isLoading;       private set => SetProperty(ref _isLoading, value); }
    public string LoadingStatus     { get => _loadingStatus;   private set => SetProperty(ref _loadingStatus, value); }
    public string LoadingDetail     { get => _loadingDetail;   private set => SetProperty(ref _loadingDetail, value); }
    public double LoadingProgress   { get => _loadingProgress; private set => SetProperty(ref _loadingProgress, value); }
    public bool IsLoadingIndeterminate { get => _isLoadingIndeterminate; private set => SetProperty(ref _isLoadingIndeterminate, value); }
    public BitmapImage? PreviewFrame { get => _previewFrame;   private set { SetProperty(ref _previewFrame, value); OnPropertyChanged(nameof(ShowPreviewImage)); OnPropertyChanged(nameof(ShowVideoPlayer)); } }
    public string DependencyStatus  { get => _dependencyStatus; private set => SetProperty(ref _dependencyStatus, value); }

    /// <summary>True once an export completes successfully (reset when new media is loaded).</summary>
    public bool HasExported { get; set; }

    /// <summary>Returns true if the user has a clip loaded but hasn't exported yet.</summary>
    public bool ShouldConfirmClose => HasMedia && !HasExported;

    public bool ShowVideoPlayer => HasMedia && !ShowPreviewImage;
    public bool ShowPreviewImage => HasMedia && !Timeline.IsPlaying && Filters.HasFilters && PreviewFrame != null;

    public TimelineViewModel Timeline { get; }
    public FiltersViewModel Filters { get; }
    public ExportViewModel Export { get; }
    public BackgroundJobService Jobs => _jobs;

    public bool AutoCheckUpdates
    {
        get => _appSettings.AutoCheckUpdates;
        set { _appSettings.AutoCheckUpdates = value; _settings.Save(_appSettings); OnPropertyChanged(); }
    }

    public bool HardwareAcceleration
    {
        get => _appSettings.HardwareAcceleration;
        set { _appSettings.HardwareAcceleration = value; _settings.Save(_appSettings); OnPropertyChanged(); }
    }

    public bool MixAudioTracks
    {
        get => _appSettings.MixAudioTracks;
        set { _appSettings.MixAudioTracks = value; _settings.Save(_appSettings); OnPropertyChanged(); }
    }

    public bool ThemeModeAuto => string.IsNullOrEmpty(_appSettings.ThemeOverride) || _appSettings.ThemeOverride == "Auto";
    public bool ThemeModeDark => _appSettings.ThemeOverride == "Dark";
    public bool ThemeModeLight => _appSettings.ThemeOverride == "Light";
    public bool ThemeModeMintDark => _appSettings.ThemeOverride == "MintDark";
    public bool ThemeModeRedDark => _appSettings.ThemeOverride == "RedDark";
    public bool ThemeModePremierePro => _appSettings.ThemeOverride == "PremierePro";
    public bool ThemeModeOLED => _appSettings.ThemeOverride == "OLED";
    public bool ThemeModeDiscord => _appSettings.ThemeOverride == "Discord";
    public bool ThemeModeTwilightBlurple => _appSettings.ThemeOverride == "TwilightBlurple";
    public bool ThemeModeYouTube => _appSettings.ThemeOverride == "YouTube";

    public string? PendingStartupFilePath { get; private set; }

    public AsyncRelayCommand OpenMediaCommand { get; }
    public RelayCommand ExitCommand { get; }
    public AsyncRelayCommand CheckUpdatesCommand { get; }
    public RelayCommand ToggleFiltersCommand { get; }
    public RelayCommand RegisterContextMenuCommand { get; }

    public MainViewModel(FFmpegService ffmpeg, FilterService filterService,
                         UploadService upload, SettingsService settings, UpdateService update,
                         BackgroundJobService jobs)
    {
        _ffmpeg = ffmpeg;
        _settings = settings;
        _update = update;
        _jobs = jobs;
        _appSettings = settings.Load();

        Timeline = new TimelineViewModel();
        Timeline.Volume = _appSettings.Volume;
        Filters = new FiltersViewModel(filterService);
        Filters.Preset.Saturation = _appSettings.FilterSaturation;
        Filters.Preset.Vibrance = _appSettings.FilterVibrance;
        Filters.Preset.Brightness = _appSettings.FilterBrightness;
        Filters.Preset.Contrast = _appSettings.FilterContrast;
        Filters.Preset.Sharpness = _appSettings.FilterSharpness;
        Filters.Preset.Gamma = _appSettings.FilterGamma;
        Export = new ExportViewModel(ffmpeg, upload, settings, jobs);

        Export.ExportCompleted += () => HasExported = true;
        Export.GetExportContext = () => (
            Timeline.InPoint,
            Timeline.OutPoint,
            Clip,
            Filters.BuildVfChain()
        );

        _previewDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _previewDebounce.Tick += async (_, _) => { _previewDebounce.Stop(); await UpdatePreviewFrameAsync(); };

        _sessionSaveDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _sessionSaveDebounce.Tick += (_, _) =>
        {
            _sessionSaveDebounce.Stop();
            SaveRecoverySnapshot();
        };

        Timeline.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TimelineViewModel.CurrentPosition))
            {
                if (!Timeline.IsPlaying && Filters.HasFilters)
                {
                    _previewDebounce.Stop();
                    _previewDebounce.Start();
                }

                RequestRecoverySnapshotSave();
            }
            else if (e.PropertyName == nameof(TimelineViewModel.InPoint) ||
                     e.PropertyName == nameof(TimelineViewModel.OutPoint))
            {
                RequestRecoverySnapshotSave();
            }
            else if (e.PropertyName == nameof(TimelineViewModel.IsPlaying))
            {
                OnPropertyChanged(nameof(ShowPreviewImage));
                OnPropertyChanged(nameof(ShowVideoPlayer));
            }
            else if (e.PropertyName == nameof(TimelineViewModel.Volume) && !Timeline.IsMuted)
            {
                _appSettings.Volume = Timeline.Volume;
                _settings.Save(_appSettings);
            }
        };

        Filters.FiltersChanged += () =>
        {
            OnPropertyChanged(nameof(ShowPreviewImage));
            OnPropertyChanged(nameof(ShowVideoPlayer));
            if (Filters.HasFilters)
                _ = UpdatePreviewFrameAsync();

            _appSettings.FilterSaturation = Filters.Preset.Saturation;
            _appSettings.FilterVibrance = Filters.Preset.Vibrance;
            _appSettings.FilterBrightness = Filters.Preset.Brightness;
            _appSettings.FilterContrast = Filters.Preset.Contrast;
            _appSettings.FilterSharpness = Filters.Preset.Sharpness;
            _appSettings.FilterGamma = Filters.Preset.Gamma;
            _settings.Save(_appSettings);
        };

        OpenMediaCommand = new AsyncRelayCommand(OpenMediaAsync);
        ExitCommand = new RelayCommand(() => Application.Current.Shutdown());
        CheckUpdatesCommand = new AsyncRelayCommand(CheckUpdatesAsync);
        ToggleFiltersCommand = new RelayCommand(Filters.Toggle);
        RegisterContextMenuCommand = new RelayCommand(RegisterContextMenu);
    }

    public void ConfigureStartup(string? startupFilePath, bool crashedPreviously)
    {
        PendingStartupFilePath = startupFilePath;
        _startupRecoveryPending = crashedPreviously;
    }

    public void SetTheme(string? mode)
    {
        _appSettings.ThemeOverride = mode;
        _settings.Save(_appSettings);
        App.ApplyTheme(mode);
        OnPropertyChanged(nameof(ThemeModeAuto));
        OnPropertyChanged(nameof(ThemeModeDark));
        OnPropertyChanged(nameof(ThemeModeLight));
        OnPropertyChanged(nameof(ThemeModeMintDark));
        OnPropertyChanged(nameof(ThemeModeRedDark));
        OnPropertyChanged(nameof(ThemeModePremierePro));
        OnPropertyChanged(nameof(ThemeModeOLED));
        OnPropertyChanged(nameof(ThemeModeDiscord));
        OnPropertyChanged(nameof(ThemeModeTwilightBlurple));
        OnPropertyChanged(nameof(ThemeModeYouTube));
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        IsLoadingIndeterminate = true;
        LoadingStatus = "Checking FFmpeg…";
        LoadingDetail = "Verifying startup dependencies.";
        _jobs.Report("Startup", LoadingStatus, isIndeterminate: true);

        try
        {
            var result = await _ffmpeg.EnsureFfmpegAvailableAsync(new Progress<string>(msg =>
            {
                LoadingStatus = msg;
                LoadingDetail = "This can take a minute on the first run.";
                _jobs.Report("Startup", msg, isIndeterminate: true);
            }));

            DependencyStatus = result.Source switch
            {
                FFmpegService.FfmpegSource.Path => "FFmpeg: using system installation from PATH.",
                FFmpegService.FfmpegSource.LocalInstall => "FFmpeg: using local AppData installation.",
                FFmpegService.FfmpegSource.DownloadedThisSession => "FFmpeg: downloaded and installed for this machine.",
                _ => "FFmpeg: ready."
            };

            if (!_appSettings.HasCompletedInitialSetup)
            {
                _appSettings.HasCompletedInitialSetup = true;
                _settings.Save(_appSettings);

                DialogService.ShowInfo(
                    "First-Time Setup Complete",
                    $"{DependencyStatus}\n\nStartup dependencies are ready. You can open a file and start trimming.");
            }
        }
        catch (Exception ex)
        {
            DependencyStatus = "FFmpeg: initialization failed.";
            DialogService.ShowWarning("FFmpeg Error",
                $"Could not initialize FFmpeg:\n{ex.Message}\n\nThe app may not function correctly.");
        }
        finally
        {
            IsLoading = false;
            LoadingDetail = string.Empty;
            _jobs.Report("Startup", DependencyStatus, 100, isIndeterminate: false);
        }

        if (_appSettings.AutoCheckUpdates)
            _ = CheckUpdatesAsync();
    }

    public async Task HandleStartupAsync()
    {
        if (!string.IsNullOrWhiteSpace(PendingStartupFilePath) && File.Exists(PendingStartupFilePath))
        {
            var startupPath = PendingStartupFilePath;
            PendingStartupFilePath = null;
            await LoadFileAsync(startupPath);
            return;
        }

        if (!_startupRecoveryPending ||
            string.IsNullOrWhiteSpace(_appSettings.RecoveryMediaPath) ||
            !File.Exists(_appSettings.RecoveryMediaPath))
        {
            return;
        }

        string fileName = Path.GetFileName(_appSettings.RecoveryMediaPath);
        var restore = DialogService.Confirm(
            "Restore Session",
            $"The app did not close cleanly last time.\n\nRestore your last session for {fileName}?");

        if (restore)
        {
            await LoadRecoveredSessionAsync(
                _appSettings.RecoveryMediaPath,
                TimeSpan.FromSeconds(_appSettings.RecoveryInPointSeconds),
                TimeSpan.FromSeconds(_appSettings.RecoveryOutPointSeconds),
                TimeSpan.FromSeconds(_appSettings.RecoveryPositionSeconds));
        }
        else
        {
            ClearRecoverySnapshot();
        }
    }

    public async Task LoadFileAsync(string path)
    {
        if (!File.Exists(path)) return;

        IsLoading = true;
        IsLoadingIndeterminate = true;
        LoadingStatus = "Loading media…";
        LoadingDetail = "Reading media info and generating timeline thumbnails.";
        Clip = null;
        PreviewFrame = null;
        HasExported = false;

        try
        {
            await _ffmpeg.EnsureFfmpegAvailableAsync(null);
            var clip = await _ffmpeg.GetMediaInfoAsync(path);

            await Application.Current.Dispatcher.InvokeAsync(() => Clip = clip);

            LoadingStatus = "Generating thumbnails…";
            var cts = new CancellationTokenSource();
            var thumbResults = new System.Collections.Concurrent.ConcurrentBag<(int Index, double Ratio, BitmapImage Image)>();

            await _ffmpeg.GenerateThumbnailsAsync(path, 10,
                (index, img) =>
                {
                    double ratio = (double)index / 10;
                    thumbResults.Add((index, ratio, img));
                },
                cts.Token);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (var t in thumbResults.OrderBy(t => t.Index))
                    Timeline.Thumbnails.Add(new ThumbnailItem(t.Index, t.Ratio, t.Image));
            });

            if (Filters.HasFilters)
                await UpdatePreviewFrameAsync();

            RequestRecoverySnapshotSave();
        }
        catch (Exception ex)
        {
            DialogService.ShowError("Error", $"Failed to load media:\n{ex.Message}");
            Clip = null;
        }
        finally
        {
            IsLoading = false;
            LoadingDetail = string.Empty;
        }
    }

    public async Task LoadRecoveredSessionAsync(string path, TimeSpan inPoint, TimeSpan outPoint, TimeSpan currentPosition)
    {
        await LoadFileAsync(path);
        if (Clip == null)
            return;

        Timeline.InPoint = inPoint;
        Timeline.OutPoint = outPoint <= inPoint ? Clip.Duration : outPoint;
        Timeline.CurrentPosition = currentPosition;
        RequestRecoverySnapshotSave();
    }

    async Task OpenMediaAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Open Media",
            Filter = "Video Files|*.mp4;*.mkv;*.mov;*.avi;*.webm|All Files|*.*",
            InitialDirectory = Directory.Exists(_appSettings.LastOpenFolder)
                ? _appSettings.LastOpenFolder
                : Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
        };
        if (dialog.ShowDialog() != true) return;
        var folder = Path.GetDirectoryName(dialog.FileName);
        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
        {
            _appSettings.LastOpenFolder = folder;
            _settings.Save(_appSettings);
        }
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
        catch
        {
        }
    }

    async Task CheckUpdatesAsync()
    {
        var result = await _update.CheckForUpdateAsync();
        if (!result.UpdateAvailable) return;

        var answer = DialogService.Confirm(
            "Update Available",
            $"Bambooz Clip Studio {result.LatestVersion} is available.\n\nUpdate now?");

        if (!answer) return;

        IsLoading = true;
        IsLoadingIndeterminate = false;
        LoadingStatus = "Downloading update…";
        LoadingDetail = "Fetching the latest installer from GitHub Releases.";

        try
        {
            var installerPath = await _update.DownloadInstallerAsync(
                result.InstallerUrl!,
                new Progress<double>(p =>
                {
                    LoadingProgress = p;
                    LoadingStatus = $"Downloading… {p:F0}%";
                }),
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
            LoadingDetail = string.Empty;
            DialogService.ShowError("Error", $"Update download failed:\n{ex.Message}");
        }
    }

    public void MarkExitClean()
    {
        _appSettings = _settings.Load();
        _appSettings.LastRunExitedCleanly = true;
        _settings.Save(_appSettings);
    }

    void RequestRecoverySnapshotSave()
    {
        _sessionSaveDebounce.Stop();
        _sessionSaveDebounce.Start();
    }

    void SaveRecoverySnapshot()
    {
        _appSettings = _settings.Load();
        _appSettings.LastRunExitedCleanly = false;

        if (Clip == null)
        {
            _appSettings.RecoveryMediaPath = null;
            _appSettings.RecoveryInPointSeconds = 0;
            _appSettings.RecoveryOutPointSeconds = 0;
            _appSettings.RecoveryPositionSeconds = 0;
        }
        else
        {
            _appSettings.RecoveryMediaPath = Clip.FilePath;
            _appSettings.RecoveryInPointSeconds = Timeline.InPoint.TotalSeconds;
            _appSettings.RecoveryOutPointSeconds = Timeline.OutPoint.TotalSeconds;
            _appSettings.RecoveryPositionSeconds = Timeline.CurrentPosition.TotalSeconds;
        }

        _settings.Save(_appSettings);
    }

    void ClearRecoverySnapshot()
    {
        _appSettings.RecoveryMediaPath = null;
        _appSettings.RecoveryInPointSeconds = 0;
        _appSettings.RecoveryOutPointSeconds = 0;
        _appSettings.RecoveryPositionSeconds = 0;
        _settings.Save(_appSettings);
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

            DialogService.ShowInfo("Success",
                "Context menu registered for .mp4, .mkv, .mov, .avi, .webm\n\nRight-click any video file to open in Bambooz Clip Studio.");
        }
        catch (Exception ex)
        {
            DialogService.ShowError("Error",
                $"Failed to register context menu:\n{ex.Message}\n\nTry running as administrator.");
        }
    }
}

using BamboozClipStudio.Services;
using BamboozClipStudio.ViewModels;
using BamboozClipStudio.Views;
using LibVLCSharp.Shared;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace BamboozClipStudio;

public partial class MainWindow : Window
{
    readonly MainViewModel _vm;
    readonly SettingsService _settingsService;
    readonly DispatcherTimer _positionTimer;

    LibVLC? _libVlc;
    MediaPlayer? _player;

    // Set true while loading new media so the Playing event knows to auto-pause
    bool _openingMedia;
    // Guards against feedback loop when syncing position from VLC → VM
    bool _syncingFromMedia;

    public MainWindow(MainViewModel vm, SettingsService settingsService)
    {
        InitializeComponent();
        _vm = vm;
        _settingsService = settingsService;
        DataContext = vm;
        PreviewKeyDown += OnKeyDown;
        Loaded += async (_, _) =>
        {
            UpdateWindowInset();
            await vm.InitializeAsync();
            await vm.HandleStartupAsync();
        };
        Closing += OnClosing;
        Closed += OnClosed;
        StateChanged += (_, _) => UpdateWindowInset();

        // Restore window position/size
        RestoreWindowBounds();

        // Initialize LibVLC (uses bundled native VLC libs from VideoLAN.LibVLC.Windows NuGet)
        LibVLCSharp.Shared.Core.Initialize();
        _libVlc = new LibVLC("--avcodec-hw=any");
        _player  = new MediaPlayer(_libVlc);
        VideoPlayer.MediaPlayer = _player;

        // VLC events fire on VLC's internal thread — use BeginInvoke (non-blocking) to avoid
        // deadlocks between VLC's thread and the UI dispatcher
        _player.Playing          += (_, _) => Dispatcher.BeginInvoke(OnVlcPlaying);
        _player.EndReached       += (_, _) => Dispatcher.BeginInvoke(() => _vm.Timeline.StopPlayback());
        _player.EncounteredError += (_, _) => Dispatcher.BeginInvoke(OnVlcError);

        // 60fps position sync — only runs during playback
        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _positionTimer.Tick += OnPositionTimerTick;

        vm.PropertyChanged      += VmPropertyChanged;
        vm.Timeline.PropertyChanged += TimelinePropertyChanged;
    }

    // ── VLC player events ────────────────────────────────────────────────────

    void OnVlcPlaying()
    {
        UpdateMediaVolume();

        // Auto-pause after the initial open so the paused frame shows
        if (_openingMedia)
        {
            _openingMedia = false;
            _player!.SetPause(true);
            _player.Time = (long)_vm.Timeline.CurrentPosition.TotalMilliseconds;
        }
    }

    void OnVlcError()
    {
        DialogService.ShowWarning("Playback Error",
            "Media failed to load. The file format may not be supported by VLC.");
        _vm.Timeline.StopPlayback();
    }

    void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_vm.ShouldConfirmClose)
        {
            var result = DialogService.Confirm(
                "Unsaved Work",
                "You have a clip loaded that hasn't been exported yet.\n\nAre you sure you want to close?");
            if (!result)
            {
                e.Cancel = true;
                return;
            }
        }
        SaveWindowBounds();
    }

    void OnClosed(object? sender, EventArgs e)
    {
        _vm.MarkExitClean();
        _positionTimer.Stop();
        _player?.Stop();
        _player?.Dispose();
        _libVlc?.Dispose();
    }

    // ── Clip loading ────────────────────────────────────────────────────────

    void VmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.HasMedia))
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (!_vm.HasMedia)
                {
                    _player?.Stop();
                    Title = "Bambooz Clip Studio";
                    return;
                }
                if (_vm.Clip != null)
                {
                    LoadVlcMedia(_vm.Clip.FilePath);
                    Title = $"{System.IO.Path.GetFileName(_vm.Clip.FilePath)} — Bambooz Clip Studio";
                }
            });
        }
    }

    void LoadVlcMedia(string path)
    {
        if (_player == null || _libVlc == null) return;
        _openingMedia = true;
        var media = new Media(_libVlc, new Uri(path, UriKind.Absolute));
        _player.Media = media;
        media.Dispose(); // MediaPlayer holds its own reference
        _player.Play();  // Must call Play() to enter Playing state; OnVlcPlaying() will pause it
    }

    // ── Timeline → VLC sync ─────────────────────────────────────────────────

    void TimelinePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(TimelineViewModel.IsPlaying):
                if (_vm.Timeline.IsPlaying)
                {
                    // Resume from VLC's currently paused frame. Seeking again here can jump
                    // backwards slightly because the VM position may lag the actual paused frame.
                    _player.SetPause(false);
                    _positionTimer.Start();
                }
                else
                {
                    _positionTimer.Stop();
                    _player!.SetPause(true);
                    // Sync VLC's actual position INTO the VM before pausing —
                    // the 60fps timer may not have fired since VLC's last frame
                    _syncingFromMedia = true;
                    _vm.Timeline.CurrentPosition = TimeSpan.FromMilliseconds(Math.Max(0, _player.Time));
                    _syncingFromMedia = false;
                }
                break;

            case nameof(TimelineViewModel.CurrentPosition):
                // Only seek VLC when scrubbing while paused
                if (!_syncingFromMedia && !_vm.Timeline.IsPlaying && _player?.Media != null)
                    _player.Time = (long)_vm.Timeline.CurrentPosition.TotalMilliseconds;
                break;

            case nameof(TimelineViewModel.Volume):
            case nameof(TimelineViewModel.IsMuted):
                UpdateMediaVolume();
                break;
        }
    }

    // ── Playback position sync (60fps timer) ───────────────────────────────

    void OnPositionTimerTick(object? sender, EventArgs e)
    {
        if (_player == null || !_vm.Timeline.IsPlaying) return;

        _syncingFromMedia = true;
        _vm.Timeline.CurrentPosition = TimeSpan.FromMilliseconds(Math.Max(0, _player.Time));
        _syncingFromMedia = false;

        // Stop at out-point
        if (_player.Time >= (long)_vm.Timeline.OutPoint.TotalMilliseconds)
            _vm.Timeline.StopPlayback();
    }

    void UpdateMediaVolume()
    {
        if (_player == null) return;
        // VLC Volume is int 0-100; drive to 0 when muted rather than using VLC's Mute flag
        _player.Mute   = false;
        _player.Volume = _vm.Timeline.IsMuted ? 0 : (int)(_vm.Timeline.Volume * 100);
    }

    // ── Window bounds persistence ──────────────────────────────────────────

    void RestoreWindowBounds()
    {
        var s = _settingsService.Load();
        if (s.WindowLeft >= 0 && s.WindowTop >= 0)
        {
            // Validate saved position is within virtual screen (all monitors combined)
            double vLeft   = SystemParameters.VirtualScreenLeft;
            double vTop    = SystemParameters.VirtualScreenTop;
            double vRight  = vLeft + SystemParameters.VirtualScreenWidth;
            double vBottom = vTop + SystemParameters.VirtualScreenHeight;

            bool onScreen = s.WindowLeft + s.WindowWidth > vLeft &&
                            s.WindowLeft < vRight &&
                            s.WindowTop + s.WindowHeight > vTop &&
                            s.WindowTop < vBottom;

            if (onScreen)
            {
                Left = s.WindowLeft;
                Top = s.WindowTop;
                Width = s.WindowWidth;
                Height = s.WindowHeight;
                WindowStartupLocation = WindowStartupLocation.Manual;
            }
        }

        if (s.WindowMaximized)
            WindowState = WindowState.Maximized;
    }

    void SaveWindowBounds()
    {
        var s = _settingsService.Load();
        s.WindowMaximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            s.WindowLeft   = Left;
            s.WindowTop    = Top;
            s.WindowWidth  = Width;
            s.WindowHeight = Height;
        }
        _settingsService.Save(s);
    }

    // ── Window chrome ──────────────────────────────────────────────────────

    void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) ToggleMaximize();
        else DragMove();
    }

    void Minimize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    void ToggleMaximize()
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal : WindowState.Maximized;

    void UpdateWindowInset()
    {
        if (WindowState != WindowState.Maximized)
        {
            RootLayout.Margin = new Thickness(0);
            return;
        }

        // Borderless maximized windows need to respect the desktop work area,
        // otherwise bottom content can render under the taskbar or screen edge.
        var workArea = SystemParameters.WorkArea;
        RootLayout.Margin = new Thickness(
            Math.Max(0, workArea.Left),
            Math.Max(0, workArea.Top),
            Math.Max(0, SystemParameters.PrimaryScreenWidth - workArea.Right),
            Math.Max(0, SystemParameters.PrimaryScreenHeight - workArea.Bottom));
    }

    void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ── Keyboard shortcuts ─────────────────────────────────────────────────

    void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Space:
                _vm.Timeline.TogglePlayback();
                e.Handled = true;
                break;
            case Key.O when Keyboard.Modifiers == ModifierKeys.Control:
                _vm.OpenMediaCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Left:
                _vm.Timeline.StepBackCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Right:
                _vm.Timeline.StepForwardCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    // ── Drag and drop ──────────────────────────────────────────────────────

    void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    async void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files.Length > 0)
            await _vm.LoadFileAsync(files[0]);
    }

    // ── Drop zone click ────────────────────────────────────────────────────

    void DropZone_Click(object sender, MouseButtonEventArgs e)
        => _vm.OpenMediaCommand.Execute(null);

    // ── Menus ──────────────────────────────────────────────────────────────

    void FileMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.DataContext = _vm;
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu.IsOpen = true;
        }
    }

    void ToolsMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.DataContext = _vm;
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu.IsOpen = true;
        }
    }

    void SettingsMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.DataContext = _vm;
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu.IsOpen = true;
        }
    }

    void Theme_Auto_Click(object sender, RoutedEventArgs e)            => _vm.SetTheme(null);
    void Theme_Dark_Click(object sender, RoutedEventArgs e)            => _vm.SetTheme("Dark");
    void Theme_Light_Click(object sender, RoutedEventArgs e)           => _vm.SetTheme("Light");
    void Theme_MintDark_Click(object sender, RoutedEventArgs e)        => _vm.SetTheme("MintDark");
    void Theme_RedDark_Click(object sender, RoutedEventArgs e)         => _vm.SetTheme("RedDark");
    void Theme_PremierePro_Click(object sender, RoutedEventArgs e)     => _vm.SetTheme("PremierePro");
    void Theme_OLED_Click(object sender, RoutedEventArgs e)            => _vm.SetTheme("OLED");
    void Theme_Discord_Click(object sender, RoutedEventArgs e)         => _vm.SetTheme("Discord");
    void Theme_TwilightBlurple_Click(object sender, RoutedEventArgs e) => _vm.SetTheme("TwilightBlurple");
    void Theme_YouTube_Click(object sender, RoutedEventArgs e)         => _vm.SetTheme("YouTube");
    void HwAccel_Click(object sender, RoutedEventArgs e)               => _vm.HardwareAcceleration = !_vm.HardwareAcceleration;
    void MixAudio_Click(object sender, RoutedEventArgs e)              => _vm.MixAudioTracks = !_vm.MixAudioTracks;

    void OpenMedia_Click(object sender, RoutedEventArgs e)
        => _vm.OpenMediaCommand.Execute(null);

    void RegisterContextMenu_Click(object sender, RoutedEventArgs e)
        => _vm.RegisterContextMenuCommand.Execute(null);

    void AutoUpdate_Click(object sender, RoutedEventArgs e)
    {
        // IsChecked binding handles the toggle; just ensure save
    }

    // ── Export ─────────────────────────────────────────────────────────────

    void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ExportDialog(_vm.Export) { Owner = this };
        dialog.ShowDialog();
    }

    void CancelBackgroundJob_Click(object sender, RoutedEventArgs e)
        => _vm.Jobs.CancelCurrentJob();
}

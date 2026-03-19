using BamboozClipStudio.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace BamboozClipStudio.Views;

public partial class TimelineView : UserControl
{
    TimelineViewModel? _vm;
    bool _draggingIn, _draggingOut, _draggingPlayhead;
    double _canvasWidth;

    const double RulerHeight = 20;
    const double ThumbHeight = 110;

    public TimelineView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        SizeChanged += (_, _) => RefreshLayout();
        Loaded += (_, _) => RefreshLayout();
    }

    void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm != null) _vm.PropertyChanged -= VmPropertyChanged;
        _vm = DataContext as TimelineViewModel;
        if (_vm != null)
        {
            _vm.PropertyChanged += VmPropertyChanged;
            _vm.Thumbnails.CollectionChanged += (_, _) => Dispatcher.Invoke(DrawThumbnails);
        }
        RefreshLayout();
    }

    void VmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(TimelineViewModel.InPoint):
                case nameof(TimelineViewModel.OutPoint):
                    UpdateHandlesAndHighlight();
                    UpdateTimecodeLabels();
                    break;
                case nameof(TimelineViewModel.CurrentPosition):
                    UpdatePlayhead();
                    UpdateTimecodeLabels();
                    break;
                case nameof(TimelineViewModel.ZoomLevel):
                    UpdateCanvasWidth();
                    RefreshLayout();
                    break;
                case nameof(TimelineViewModel.IsPlaying):
                    UpdatePlayPauseIcon();
                    break;
            }
        });
    }

    void UpdateCanvasWidth()
    {
        if (_vm == null) return;
        _canvasWidth = Math.Max(TimelineScroller.ActualWidth - 20, 200) * _vm.ZoomLevel;
        TimelineCanvas.Width = _canvasWidth;
    }

    new void RefreshLayout()
    {
        if (_vm == null || !IsLoaded) return;
        UpdateCanvasWidth();
        DrawRuler();
        DrawThumbnails();
        UpdateHandlesAndHighlight();
        UpdatePlayhead();
        UpdateTimecodeLabels();
    }

    // ── Ruler ──────────────────────────────────────────────────────────────

    void DrawRuler()
    {
        RulerCanvas.Children.Clear();
        if (_vm?.Clip == null || _canvasWidth <= 0) return;

        double duration = _vm.Clip.Duration.TotalSeconds;
        if (duration <= 0) return;

        // Background
        var bg = new Rectangle { Width = _canvasWidth, Height = RulerHeight, Fill = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x18)) };
        Canvas.SetLeft(bg, 0); Canvas.SetTop(bg, 0);
        RulerCanvas.Children.Add(bg);

        // Determine tick interval
        double pixelsPerSecond = _canvasWidth / duration;
        double tickInterval = PickTickInterval(pixelsPerSecond);

        var brush = new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x70));
        var accentBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xC8, 0xD4));

        for (double t = 0; t <= duration; t += tickInterval)
        {
            double x = t / duration * _canvasWidth;
            bool isMainTick = t % (tickInterval * 5) < 0.001;

            var line = new Line { X1 = x, Y1 = RulerHeight, X2 = x, Y2 = isMainTick ? 6 : 12,
                                  Stroke = isMainTick ? accentBrush : brush, StrokeThickness = 1 };
            RulerCanvas.Children.Add(line);

            if (isMainTick && x > 10)
            {
                var lbl = new TextBlock
                {
                    Text = FormatRulerTime(TimeSpan.FromSeconds(t)),
                    FontSize = 9, Foreground = brush, FontFamily = new FontFamily("Consolas")
                };
                Canvas.SetLeft(lbl, x + 2);
                Canvas.SetTop(lbl, 2);
                RulerCanvas.Children.Add(lbl);
            }
        }
    }

    static double PickTickInterval(double pixelsPerSecond) => pixelsPerSecond switch
    {
        > 200  => 0.1,
        > 50   => 0.5,
        > 20   => 1,
        > 8    => 5,
        > 2    => 10,
        > 0.5  => 30,
        _      => 60
    };

    static string FormatRulerTime(TimeSpan t)
        => t.TotalMinutes >= 1 ? $"{(int)t.TotalMinutes}:{t.Seconds:D2}" : $"{t.TotalSeconds:F1}s";

    // ── Thumbnails ─────────────────────────────────────────────────────────

    void DrawThumbnails()
    {
        // Remove old thumbnails (keep other canvas children)
        var toRemove = TimelineCanvas.Children.OfType<Image>().ToList();
        foreach (var img in toRemove) TimelineCanvas.Children.Remove(img);

        if (_vm?.Clip == null || _canvasWidth <= 0) return;

        double thumbWidth = Math.Max(80, _canvasWidth / Math.Max(_vm.Thumbnails.Count, 1));
        double thumbHeight = 70;
        double topOffset = RulerHeight;

        foreach (var thumb in _vm.Thumbnails)
        {
            var img = new Image
            {
                Source = thumb.Image,
                Width = thumbWidth,
                Height = thumbHeight,
                Stretch = Stretch.UniformToFill,
                StretchDirection = StretchDirection.Both,
                ClipToBounds = true,
                Opacity = 0.75
            };
            Canvas.SetLeft(img, thumb.PositionRatio * _canvasWidth);
            Canvas.SetTop(img, topOffset);
            Panel.SetZIndex(img, 0);
            TimelineCanvas.Children.Add(img);
        }

        UpdateHandlesAndHighlight();
        UpdatePlayhead();
    }

    // ── Handles & Highlight ────────────────────────────────────────────────

    void UpdateHandlesAndHighlight()
    {
        if (_vm?.Clip == null || _canvasWidth <= 0) return;

        double dur = _vm.Clip.Duration.TotalSeconds;
        if (dur <= 0) return;

        double inX  = _vm.InPoint.TotalSeconds  / dur * _canvasWidth;
        double outX = _vm.OutPoint.TotalSeconds / dur * _canvasWidth;

        Canvas.SetLeft(InHandle,  inX - InHandle.Width);
        Canvas.SetLeft(OutHandle, outX);
        Canvas.SetLeft(HighlightRegion, inX);
        HighlightRegion.Width = Math.Max(0, outX - inX);

        Panel.SetZIndex(InHandle,  10);
        Panel.SetZIndex(OutHandle, 10);
        Panel.SetZIndex(HighlightRegion, 1);
    }

    // ── Playhead ───────────────────────────────────────────────────────────

    void UpdatePlayhead()
    {
        if (_vm?.Clip == null || _canvasWidth <= 0) return;
        double dur = _vm.Clip.Duration.TotalSeconds;
        if (dur <= 0) return;

        double x = _vm.CurrentPosition.TotalSeconds / dur * _canvasWidth;
        Canvas.SetLeft(PlayheadCanvas, x - 1);
        Panel.SetZIndex(PlayheadCanvas, 20);
    }

    // ── Play/Pause icon ────────────────────────────────────────────────────

    void UpdatePlayPauseIcon()
    {
        if (_vm == null) return;
        PlayIcon.Visibility  = _vm.IsPlaying ? Visibility.Collapsed : Visibility.Visible;
        PauseIcon.Visibility = _vm.IsPlaying ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Timecode labels ────────────────────────────────────────────────────

    void UpdateTimecodeLabels()
    {
        if (_vm == null) return;
        InTimecode.Text       = FormatTimecode(_vm.InPoint);
        OutTimecode.Text      = FormatTimecode(_vm.OutPoint);
        DurTimecode.Text      = FormatTimecode(_vm.TrimDuration);
        PositionTimecode.Text = _vm.Timecode;
    }

    static string FormatTimecode(TimeSpan t)
        => $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}.{t.Milliseconds / 100}";

    // ── Mouse events ───────────────────────────────────────────────────────

    void InHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _draggingIn = true;
        InHandle.CaptureMouse();
        e.Handled = true;
    }

    void OutHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _draggingOut = true;
        OutHandle.CaptureMouse();
        e.Handled = true;
    }

    void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm?.Clip == null) return;
        _draggingPlayhead = true;
        TimelineCanvas.CaptureMouse();
        SeekToPosition(e.GetPosition(TimelineCanvas).X);
        e.Handled = true;
    }

    void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_vm?.Clip == null) return;
        double x = e.GetPosition(TimelineCanvas).X;
        double dur = _vm.Clip.Duration.TotalSeconds;
        if (dur <= 0) return;

        double ratio = Math.Clamp(x / _canvasWidth, 0, 1);
        var pos = TimeSpan.FromSeconds(ratio * dur);

        if (_draggingIn)
        {
            _vm.InPoint = pos;
            if (_vm.CurrentPosition < _vm.InPoint) _vm.CurrentPosition = _vm.InPoint;
        }
        else if (_draggingOut)
        {
            _vm.OutPoint = pos;
            if (_vm.CurrentPosition > _vm.OutPoint) _vm.CurrentPosition = _vm.OutPoint;
        }
        else if (_draggingPlayhead)
        {
            _vm.StopPlayback();
            SeekToPosition(x);
        }
    }

    void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _draggingIn = _draggingOut = _draggingPlayhead = false;
        InHandle.ReleaseMouseCapture();
        OutHandle.ReleaseMouseCapture();
        TimelineCanvas.ReleaseMouseCapture();
    }

    void SeekToPosition(double x)
    {
        if (_vm?.Clip == null || _canvasWidth <= 0) return;
        double dur = _vm.Clip.Duration.TotalSeconds;
        if (dur <= 0) return;
        double ratio = Math.Clamp(x / _canvasWidth, 0, 1);
        _vm.CurrentPosition = TimeSpan.FromSeconds(ratio * dur);
    }

    // ── Zoom ───────────────────────────────────────────────────────────────

    void TimelineScroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_vm == null) return;
        if (Keyboard.Modifiers == ModifierKeys.Control || true)
        {
            double delta = e.Delta > 0 ? 1.15 : 1.0 / 1.15;
            _vm.ZoomLevel = Math.Clamp(_vm.ZoomLevel * delta, 1.0, 50.0);
            e.Handled = true;
        }
    }

    void ZoomIn_Click(object sender, RoutedEventArgs e) { if (_vm != null) _vm.ZoomLevel = Math.Min(_vm.ZoomLevel * 1.3, 50); }
    void ZoomOut_Click(object sender, RoutedEventArgs e) { if (_vm != null) _vm.ZoomLevel = Math.Max(_vm.ZoomLevel / 1.3, 1); }
    void ZoomReset_Click(object sender, RoutedEventArgs e) { if (_vm != null) _vm.ZoomLevel = 1; }
}

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
    readonly System.Windows.Threading.DispatcherTimer _drawThumbsDebounce;

    // Premiere-style layout: ruler → clip color bar → filename → thumbnails
    const double RulerHeight       = 20;
    const double ClipColorBarH     = 3;
    const double ClipTop           = RulerHeight;          // 20 — clip area starts right below ruler
    const double ClipHeight        = 90;                   // 110 - 20
    const double ThumbTop          = RulerHeight + ClipColorBarH + 14; // 37 — below color bar + filename
    const double ThumbHeight       = 110 - 37;             // 73px for thumbnails
    const double HandleWidth       = 6;

    public TimelineView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        SizeChanged += (_, _) => RefreshLayout();
        Loaded      += (_, _) => RefreshLayout();

        _drawThumbsDebounce = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _drawThumbsDebounce.Tick += (_, _) => { _drawThumbsDebounce.Stop(); DrawThumbnails(); };
    }

    void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm != null) _vm.PropertyChanged -= VmPropertyChanged;
        _vm = DataContext as TimelineViewModel;
        if (_vm != null)
        {
            _vm.PropertyChanged += VmPropertyChanged;
            _vm.Thumbnails.CollectionChanged += (_, _) =>
            {
                _drawThumbsDebounce.Stop();
                _drawThumbsDebounce.Start();
            };
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
                case nameof(TimelineViewModel.IsMuted):
                    UpdateMuteIcon();
                    break;
            }
        });
    }

    void UpdateCanvasWidth()
    {
        if (_vm == null) return;
        _canvasWidth = Math.Max(TimelineScroller.ActualWidth - 20, 200) * _vm.ZoomLevel;
        TimelineCanvas.Width = _canvasWidth;
        RulerCanvas.Width = _canvasWidth;

        // Size clip container elements to full width
        ClipBorder.Width   = _canvasWidth;
        ClipColorBar.Width = _canvasWidth;
    }

    void RefreshLayout()
    {
        if (_vm == null || !IsLoaded) return;
        UpdateCanvasWidth();
        DrawRuler();
        DrawThumbnails();
        UpdateClipFilename();
        UpdateHandlesAndHighlight();
        UpdatePlayhead();
        UpdateTimecodeLabels();
        UpdateMuteIcon();
    }

    // ── Ruler ──────────────────────────────────────────────────────────────

    void DrawRuler()
    {
        RulerCanvas.Children.Clear();
        if (_vm?.Clip == null || _canvasWidth <= 0) return;

        double duration = _vm.Clip.Duration.TotalSeconds;
        if (duration <= 0) return;

        var bg = new Rectangle
        {
            Width = _canvasWidth,
            Height = RulerHeight
        };
        bg.SetResourceReference(Shape.FillProperty, "PanelBg");
        Canvas.SetLeft(bg, 0); Canvas.SetTop(bg, 0);
        RulerCanvas.Children.Add(bg);

        double pixelsPerSecond = _canvasWidth / duration;
        double tickInterval    = PickTickInterval(pixelsPerSecond);

        int tickIndex = 0;
        for (double t = 0; t <= duration + (tickInterval * 0.5); t += tickInterval, tickIndex++)
        {
            double clampedTime = Math.Min(t, duration);
            double x = clampedTime / duration * _canvasWidth;
            bool isMainTick = tickIndex % 5 == 0;

            var line = new Line
            {
                X1 = x, Y1 = RulerHeight,
                X2 = x, Y2 = isMainTick ? 6 : 13,
                StrokeThickness = 1
            };
            line.SetResourceReference(Shape.StrokeProperty, isMainTick ? "Accent" : "TextSecondary");
            RulerCanvas.Children.Add(line);

            if (isMainTick && x > 10)
            {
                var lbl = new TextBlock
                {
                    Text       = FormatRulerTime(TimeSpan.FromSeconds(clampedTime)),
                    FontSize   = 9,
                    FontFamily = new FontFamily("Consolas"),
                    FontWeight = FontWeights.SemiBold,
                    TextTrimming = TextTrimming.None
                };
                lbl.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");
                Canvas.SetLeft(lbl, x + 3);
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
    {
        if (t.TotalMinutes >= 1)
            return $"{(int)t.TotalMinutes}:{t.Seconds:D2}";

        double totalSeconds = t.TotalSeconds;
        double roundedSeconds = Math.Round(totalSeconds);
        return Math.Abs(totalSeconds - roundedSeconds) < 0.0001
            ? $"{roundedSeconds:0}s"
            : $"{totalSeconds:F1}s";
    }

    // ── Clip filename label ───────────────────────────────────────────────

    void UpdateClipFilename()
    {
        if (_vm?.Clip == null)
        {
            ClipFilename.Text = "";
            return;
        }
        ClipFilename.Text = System.IO.Path.GetFileName(_vm.Clip.FilePath);
        Canvas.SetLeft(ClipFilename, 6);
    }

    // ── Thumbnails ─────────────────────────────────────────────────────────

    void DrawThumbnails()
    {
        var toRemove = TimelineCanvas.Children.OfType<Image>().ToList();
        foreach (var img in toRemove) TimelineCanvas.Children.Remove(img);

        if (_vm?.Clip == null || _canvasWidth <= 0) return;

        // Edge-to-edge thumbnails like Premiere — each thumb fills its slot exactly
        int count = Math.Max(_vm.Thumbnails.Count, 1);
        double thumbWidth = _canvasWidth / count;

        foreach (var thumb in _vm.Thumbnails)
        {
            var img = new Image
            {
                Source           = thumb.Image,
                Width            = thumbWidth + 1, // +1px overlap to prevent sub-pixel gaps
                Height           = ThumbHeight,
                Stretch          = Stretch.UniformToFill,
                StretchDirection = StretchDirection.Both,
                ClipToBounds     = true,
                Opacity          = 1.0  // Full brightness (Premiere style)
            };
            Canvas.SetLeft(img, thumb.PositionRatio * _canvasWidth);
            Canvas.SetTop(img, ThumbTop);
            Panel.SetZIndex(img, 1);
            TimelineCanvas.Children.Add(img);
        }

        UpdateHandlesAndHighlight();
        UpdatePlayhead();
    }

    // ── Handles, highlight, and dim regions ────────────────────────────────

    void UpdateHandlesAndHighlight()
    {
        if (_vm?.Clip == null || _canvasWidth <= 0) return;

        double dur = _vm.Clip.Duration.TotalSeconds;
        if (dur <= 0) return;

        double inX  = _vm.InPoint.TotalSeconds  / dur * _canvasWidth;
        double outX = _vm.OutPoint.TotalSeconds / dur * _canvasWidth;

        // Thin trim handles
        Canvas.SetLeft(InHandle,  inX - HandleWidth);
        Canvas.SetLeft(OutHandle, outX);

        // Selection highlight (subtle accent tint over selected range)
        Canvas.SetLeft(HighlightRegion, inX);
        HighlightRegion.Width = Math.Max(0, outX - inX);

        // Dim regions outside in/out (Premiere dims trimmed-out areas)
        Canvas.SetLeft(DimBefore, 0);
        DimBefore.Width = Math.Max(0, inX);

        Canvas.SetLeft(DimAfter, outX);
        DimAfter.Width = Math.Max(0, _canvasWidth - outX);

        // Z-order: dims behind thumbnails, highlight above, handles on top
        Panel.SetZIndex(ClipBorder,       0);
        Panel.SetZIndex(ClipColorBar,     2);
        Panel.SetZIndex(ClipFilename,     3);
        Panel.SetZIndex(DimBefore,        6);
        Panel.SetZIndex(DimAfter,         6);
        Panel.SetZIndex(HighlightRegion,  5);
        Panel.SetZIndex(InHandle,        10);
        Panel.SetZIndex(OutHandle,       10);
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
        PauseIcon.Visibility = _vm.IsPlaying ? Visibility.Visible   : Visibility.Collapsed;
    }

    // ── Mute icon ──────────────────────────────────────────────────────────

    void UpdateMuteIcon()
    {
        if (_vm == null) return;
        SpeakerIcon.Visibility = _vm.IsMuted ? Visibility.Collapsed : Visibility.Visible;
        MuteIcon.Visibility    = _vm.IsMuted ? Visibility.Visible   : Visibility.Collapsed;
    }

    // ── Timecode labels ────────────────────────────────────────────────────

    void UpdateTimecodeLabels()
    {
        if (_vm == null) return;
        PositionTimecode.Text = _vm.Timecode;
    }

    static string FormatTimecode(TimeSpan t)
        => $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}.{t.Milliseconds:D3}";

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
        SeekToPosition(GetCanvasPositionX(e.GetPosition(TimelineCanvas).X));
        e.Handled = true;
    }

    void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_vm?.Clip == null) return;
        double x   = GetCanvasPositionX(e.GetPosition(TimelineCanvas).X);
        double dur = _vm.Clip.Duration.TotalSeconds;
        if (dur <= 0) return;

        double ratio = Math.Clamp(x / _canvasWidth, 0, 1);
        var    pos   = TimeSpan.FromSeconds(ratio * dur);

        if (_draggingIn)
        {
            _vm.InPoint = SnapToSecond(pos);
            if (_vm.CurrentPosition < _vm.InPoint) _vm.CurrentPosition = _vm.InPoint;
        }
        else if (_draggingOut)
        {
            _vm.OutPoint = SnapToSecond(pos);
            if (_vm.CurrentPosition > _vm.OutPoint) _vm.CurrentPosition = _vm.OutPoint;
        }
        else if (_draggingPlayhead)
        {
            _vm.StopPlayback();
            SeekToPosition(x);
        }

        // Hover preview tooltip (only when not dragging)
        if (!_draggingIn && !_draggingOut && !_draggingPlayhead)
            UpdateHoverTooltip(x, pos);
        else
            HoverTooltip.Visibility = Visibility.Collapsed;
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

    double GetCanvasPositionX(double viewportX)
        => Math.Clamp(viewportX + TimelineScroller.HorizontalOffset, 0, _canvasWidth);

    // ── Zoom ───────────────────────────────────────────────────────────────

    void TimelineScroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_vm == null) return;

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            double delta = e.Delta > 0 ? 1.15 : 1.0 / 1.15;
            _vm.ZoomLevel = Math.Clamp(_vm.ZoomLevel * delta, 1.0, 50.0);
            e.Handled = true;
        }
        else
        {
            TimelineScroller.ScrollToHorizontalOffset(
                TimelineScroller.HorizontalOffset - e.Delta * 0.5);
            e.Handled = true;
        }
    }

    // ── Snap to seconds ────────────────────────────────────────────────────

    /// <summary>Snaps a position to the nearest whole second when within a threshold.</summary>
    TimeSpan SnapToSecond(TimeSpan pos)
    {
        if (_vm?.Clip == null || _canvasWidth <= 0) return pos;
        double dur = _vm.Clip.Duration.TotalSeconds;
        double pixelsPerSecond = _canvasWidth / dur;
        // Snap threshold: 8 pixels from a whole second boundary
        double snapThreshold = 8.0 / pixelsPerSecond;

        double seconds = pos.TotalSeconds;
        double nearest = Math.Round(seconds);
        if (Math.Abs(seconds - nearest) < snapThreshold)
            return TimeSpan.FromSeconds(nearest);
        return pos;
    }

    // ── Hover preview tooltip ────────────────────────────────────────────

    void UpdateHoverTooltip(double x, TimeSpan pos)
    {
        if (_vm?.Clip == null || _vm.Thumbnails.Count == 0 || _canvasWidth <= 0)
        {
            HoverTooltip.Visibility = Visibility.Collapsed;
            return;
        }

        // Find nearest thumbnail
        double ratio = Math.Clamp(x / _canvasWidth, 0, 1);
        var nearest = _vm.Thumbnails
            .OrderBy(t => Math.Abs(t.PositionRatio - ratio))
            .FirstOrDefault();

        if (nearest != null)
            HoverThumb.Source = nearest.Image;

        HoverTimecode.Text = FormatTimecode(pos);

        // Position tooltip centered on cursor X, clamped to canvas bounds
        double tooltipWidth = 114; // image width + border
        double left = Math.Clamp(x - tooltipWidth / 2, 0, _canvasWidth - tooltipWidth);
        Canvas.SetLeft(HoverTooltip, left);
        Panel.SetZIndex(HoverTooltip, 30);
        HoverTooltip.Visibility = Visibility.Visible;
    }

    void Canvas_MouseLeave(object sender, MouseEventArgs e)
    {
        HoverTooltip.Visibility = Visibility.Collapsed;
    }

    // ── Zoom / volume ────────────────────────────────────────────────────

    void ZoomIn_Click(object sender, RoutedEventArgs e)    { if (_vm != null) _vm.ZoomLevel = Math.Min(_vm.ZoomLevel * 1.3, 50); }
    void ZoomOut_Click(object sender, RoutedEventArgs e)   { if (_vm != null) _vm.ZoomLevel = Math.Max(_vm.ZoomLevel / 1.3, 1); }
    void ZoomReset_Click(object sender, RoutedEventArgs e) { if (_vm != null) _vm.ZoomLevel = 1; }
    void Mute_Click(object sender, RoutedEventArgs e)      { if (_vm != null) _vm.IsMuted = !_vm.IsMuted; }
}

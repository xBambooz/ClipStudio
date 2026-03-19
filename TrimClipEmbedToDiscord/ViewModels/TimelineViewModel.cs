using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BamboozClipStudio.Core;
using BamboozClipStudio.Models;

namespace BamboozClipStudio.ViewModels;

public class TimelineViewModel : ObservableObject
{
    ClipProject? _clip;
    TimeSpan _inPoint;
    TimeSpan _outPoint;
    TimeSpan _currentPosition;
    double _zoomLevel = 1.0;
    double _scrollOffset;
    bool _isPlaying;
    BitmapImage? _previewFrame;
    string _timecode = "00:00:00.000";

    readonly DispatcherTimer _playTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };

    public ObservableCollection<ThumbnailItem> Thumbnails { get; } = new();

    public ClipProject? Clip
    {
        get => _clip;
        set
        {
            if (SetProperty(ref _clip, value) && value != null)
            {
                InPoint = TimeSpan.Zero;
                OutPoint = value.Duration;
                CurrentPosition = TimeSpan.Zero;
                ZoomLevel = 1.0;
                ScrollOffset = 0;
                Thumbnails.Clear();
            }
        }
    }

    public TimeSpan InPoint
    {
        get => _inPoint;
        set
        {
            if (value < TimeSpan.Zero) value = TimeSpan.Zero;
            if (_clip != null && value >= OutPoint) value = OutPoint - TimeSpan.FromMilliseconds(100);
            SetProperty(ref _inPoint, value);
        }
    }

    public TimeSpan OutPoint
    {
        get => _outPoint;
        set
        {
            if (_clip != null && value > _clip.Duration) value = _clip.Duration;
            if (value <= InPoint) value = InPoint + TimeSpan.FromMilliseconds(100);
            SetProperty(ref _outPoint, value);
        }
    }

    public TimeSpan CurrentPosition
    {
        get => _currentPosition;
        set
        {
            if (_clip != null)
            {
                if (value < TimeSpan.Zero) value = TimeSpan.Zero;
                if (value > _clip.Duration) value = _clip.Duration;
            }
            if (SetProperty(ref _currentPosition, value))
                Timecode = FormatTimecode(value);
        }
    }

    public double ZoomLevel
    {
        get => _zoomLevel;
        set => SetProperty(ref _zoomLevel, Math.Clamp(value, 1.0, 50.0));
    }

    public double ScrollOffset
    {
        get => _scrollOffset;
        set => SetProperty(ref _scrollOffset, Math.Max(0, value));
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set => SetProperty(ref _isPlaying, value);
    }

    public BitmapImage? PreviewFrame
    {
        get => _previewFrame;
        set => SetProperty(ref _previewFrame, value);
    }

    public string Timecode
    {
        get => _timecode;
        private set => SetProperty(ref _timecode, value);
    }

    public TimeSpan TrimDuration => OutPoint - InPoint;

    public RelayCommand PlayPauseCommand { get; }
    public RelayCommand GoToStartCommand { get; }
    public RelayCommand GoToEndCommand { get; }
    public RelayCommand StepBackCommand { get; }
    public RelayCommand StepForwardCommand { get; }

    public TimelineViewModel()
    {
        PlayPauseCommand = new RelayCommand(TogglePlayback, () => Clip != null);
        GoToStartCommand = new RelayCommand(() => { CurrentPosition = InPoint; StopPlayback(); }, () => Clip != null);
        GoToEndCommand = new RelayCommand(() => { CurrentPosition = OutPoint; StopPlayback(); }, () => Clip != null);
        StepBackCommand = new RelayCommand(StepBack, () => Clip != null);
        StepForwardCommand = new RelayCommand(StepForward, () => Clip != null);

        _playTimer.Tick += (_, _) =>
        {
            if (_clip == null) return;
            var next = CurrentPosition + TimeSpan.FromMilliseconds(33);
            if (next >= OutPoint)
            {
                CurrentPosition = OutPoint;
                StopPlayback();
                return;
            }
            CurrentPosition = next;
        };
    }

    public void TogglePlayback()
    {
        if (IsPlaying) StopPlayback();
        else StartPlayback();
    }

    public void StartPlayback()
    {
        if (_clip == null) return;
        if (CurrentPosition >= OutPoint) CurrentPosition = InPoint;
        IsPlaying = true;
        _playTimer.Start();
    }

    public void StopPlayback()
    {
        IsPlaying = false;
        _playTimer.Stop();
    }

    void StepBack()
    {
        if (_clip == null) return;
        StopPlayback();
        var frameTime = TimeSpan.FromSeconds(1.0 / (_clip.FrameRate > 0 ? _clip.FrameRate : 30));
        CurrentPosition = CurrentPosition - frameTime < TimeSpan.Zero
            ? TimeSpan.Zero : CurrentPosition - frameTime;
    }

    void StepForward()
    {
        if (_clip == null) return;
        StopPlayback();
        var frameTime = TimeSpan.FromSeconds(1.0 / (_clip.FrameRate > 0 ? _clip.FrameRate : 30));
        var next = CurrentPosition + frameTime;
        CurrentPosition = next > _clip.Duration ? _clip.Duration : next;
    }

    static string FormatTimecode(TimeSpan t)
        => $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}.{t.Milliseconds:D3}";
}

public record ThumbnailItem(int Index, double PositionRatio, BitmapImage Image);

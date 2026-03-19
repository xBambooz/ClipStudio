using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Media.Imaging;
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
    string _timecode = "00:00:00.000";
    string _fileName = string.Empty;
    double _volume = 1.0;
    double _preMuteVolume = 1.0;
    bool _isMuted;

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
                FileName = Path.GetFileName(value.FilePath);
                Thumbnails.Clear();
                CommandManager.InvalidateRequerySuggested();
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
                UpdateTimecode();
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
        private set
        {
            if (SetProperty(ref _isPlaying, value))
                UpdateTimecode();
        }
    }

    public string Timecode
    {
        get => _timecode;
        private set => SetProperty(ref _timecode, value);
    }

    public string FileName
    {
        get => _fileName;
        private set => SetProperty(ref _fileName, value);
    }

    public double Volume
    {
        get => _volume;
        set
        {
            if (SetProperty(ref _volume, Math.Clamp(value, 0.0, 1.0)))
                OnPropertyChanged(nameof(VolumePercent));
        }
    }

    public string VolumePercent => $"{(int)Math.Round(_volume * 100)}%";

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (SetProperty(ref _isMuted, value))
            {
                if (value)
                {
                    _preMuteVolume = _volume;
                    Volume = 0;
                }
                else
                {
                    Volume = _preMuteVolume;
                }
            }
        }
    }

    public TimeSpan TrimDuration => OutPoint - InPoint;

    public RelayCommand PlayPauseCommand { get; }
    public RelayCommand GoToStartCommand { get; }
    public RelayCommand GoToEndCommand { get; }
    public RelayCommand StepBackCommand { get; }
    public RelayCommand StepForwardCommand { get; }

    public TimelineViewModel()
    {
        PlayPauseCommand = new RelayCommand(TogglePlayback);
        GoToStartCommand = new RelayCommand(() => { CurrentPosition = InPoint; StopPlayback(); });
        GoToEndCommand   = new RelayCommand(() => { CurrentPosition = OutPoint; StopPlayback(); });
        StepBackCommand  = new RelayCommand(StepBack);
        StepForwardCommand = new RelayCommand(StepForward);
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
    }

    public void StopPlayback()
    {
        IsPlaying = false;
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

    void UpdateTimecode()
    {
        Timecode = FormatTimecode(GetDisplayPosition());
    }

    TimeSpan GetDisplayPosition()
    {
        if (!_isPlaying || _clip == null)
            return _currentPosition;

        double frameRate = _clip.FrameRate > 0 ? _clip.FrameRate : 30.0;
        long frameIndex = (long)Math.Floor((_currentPosition.TotalSeconds * frameRate) + 0.0001);
        return TimeSpan.FromSeconds(frameIndex / frameRate);
    }

    static string FormatTimecode(TimeSpan t)
        => $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}.{t.Milliseconds:D3}";
}

public record ThumbnailItem(int Index, double PositionRatio, BitmapImage Image);

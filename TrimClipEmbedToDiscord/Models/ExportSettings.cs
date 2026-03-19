using BamboozClipStudio.Core;

namespace BamboozClipStudio.Models;

public enum BitrateMode { Crf, Cbr }

public class ExportSettings : ObservableObject
{
    string _outputFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    string _container = "mp4";
    string _videoCodec = "libx264";
    string _audioCodec = "aac";
    BitrateMode _bitrateMode = BitrateMode.Crf;
    int _crf = 18;
    int _bitrate = 8000;
    string _preset = "slow";
    string _profile = "high";
    bool _mixAudioTracks = true;
    bool _embedUpload;
    bool _hardwareAcceleration = true;

    public string OutputFolder   { get => _outputFolder;   set => SetProperty(ref _outputFolder,   value); }
    public string Container      { get => _container;      set => SetProperty(ref _container,      value); }
    public string VideoCodec     { get => _videoCodec;     set => SetProperty(ref _videoCodec,     value); }
    public string AudioCodec     { get => _audioCodec;     set => SetProperty(ref _audioCodec,     value); }
    public BitrateMode BitrateMode { get => _bitrateMode; set => SetProperty(ref _bitrateMode,    value); }
    public int Crf               { get => _crf;            set => SetProperty(ref _crf,            value); }
    public int Bitrate           { get => _bitrate;        set => SetProperty(ref _bitrate,        value); }
    public string Preset         { get => _preset;         set => SetProperty(ref _preset,         value); }
    public string Profile        { get => _profile;        set => SetProperty(ref _profile,        value); }
    public bool MixAudioTracks   { get => _mixAudioTracks; set => SetProperty(ref _mixAudioTracks, value); }
    public bool EmbedUpload      { get => _embedUpload;    set => SetProperty(ref _embedUpload,    value); }
    public bool HardwareAcceleration { get => _hardwareAcceleration; set => SetProperty(ref _hardwareAcceleration, value); }
}

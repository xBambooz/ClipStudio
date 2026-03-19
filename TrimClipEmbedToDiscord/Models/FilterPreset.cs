using BamboozClipStudio.Core;

namespace BamboozClipStudio.Models;

public class FilterPreset : ObservableObject
{
    double _saturation = 1.0;
    double _vibrance;
    double _brightness;
    double _contrast = 1.0;
    double _sharpness;
    double _gamma = 1.0;

    public double Saturation  { get => _saturation;  set => SetProperty(ref _saturation,  value); }
    public double Vibrance    { get => _vibrance;    set => SetProperty(ref _vibrance,    value); }
    public double Brightness  { get => _brightness;  set => SetProperty(ref _brightness,  value); }
    public double Contrast    { get => _contrast;    set => SetProperty(ref _contrast,    value); }
    public double Sharpness   { get => _sharpness;   set => SetProperty(ref _sharpness,   value); }
    public double Gamma       { get => _gamma;       set => SetProperty(ref _gamma,       value); }

    public bool IsDefault =>
        Saturation == 1.0 && Vibrance == 0.0 && Brightness == 0.0 &&
        Contrast == 1.0 && Sharpness == 0.0 && Gamma == 1.0;

    public void Reset()
    {
        Saturation = 1.0; Vibrance = 0.0; Brightness = 0.0;
        Contrast = 1.0; Sharpness = 0.0; Gamma = 1.0;
    }
}

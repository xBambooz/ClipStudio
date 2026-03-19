# Filters Rules

## Filter Panel

Side panel (270px wide) toggled from toolbar "Filters" button. Shows 6 sliders with live value display. Active indicator dot on toolbar button when any filter deviates from default.

## FilterPreset Model

```csharp
public class FilterPreset : ObservableObject
{
    double Saturation;   // 0.0–3.0, default 1.0
    double Vibrance;     // -1.0 to 1.0, default 0.0
    double Brightness;   // -1.0 to 1.0, default 0.0
    double Contrast;     // 0.0–3.0, default 1.0
    double Sharpness;    // 0.0–5.0, default 0.0
    double Gamma;        // 0.1–4.0, default 1.0

    bool IsDefault;      // true if all values at defaults
    void Reset();        // resets all to defaults
}
```

## FilterService — Building vf Chains

`FilterService.BuildVfChain(FilterPreset)` returns an FFmpeg `-vf` argument string:

- Returns empty string if all values are at defaults
- Uses `InvariantCulture` formatting (prevents locale comma decimals breaking FFmpeg)
- Components:
  - `eq=saturation={sat}:brightness={br}:contrast={con}:gamma={gam}` — if any color value deviates
  - `vibrance=intensity={vibrance}` — if vibrance != 0
  - `unsharp=luma_amount={sharp}:chroma_amount={sharp/2}` — if sharpness > 0
- Joined with commas

## Filter Persistence

All 6 filter values are saved to `AppSettings` whenever any slider changes:
```
AppSettings.FilterSaturation, FilterVibrance, FilterBrightness,
FilterContrast, FilterSharpness, FilterGamma
```

On launch, `MainViewModel` restores these into `Filters.Preset.*` before any media is loaded. Filters carry over between sessions and apply to any video file.

Reset button saves defaults back to settings.

## Preview Flow

When any filter slider changes:
1. `FiltersViewModel.FiltersChanged` event fires
2. `MainViewModel` saves filter values to settings
3. If `HasFilters`: triggers `UpdatePreviewFrameAsync()`
4. FFmpeg extracts frame: `ffmpeg [-hwaccel auto] -ss {pos} -i {file} -vf "{chain}" -frames:v 1 -f image2pipe -vcodec png pipe:1`
5. Result `BitmapImage` shown in WPF `Image` overlay (VLC `VideoView` hidden due to airspace issue)
6. Debounced by 150ms `DispatcherTimer` on `CurrentPosition` changes

## Export Integration

`ExportViewModel` calls `Filters.BuildVfChain()` and passes it to `FFmpegService.BuildEncodeProcessStartInfo()` as the `-vf` argument. If no filters active, `-vf` is omitted entirely.

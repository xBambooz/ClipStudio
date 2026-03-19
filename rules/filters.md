# Filters Rules

## Filter Panel

Accessible from the toolbar. Opens as a side panel or flyout within the main window. Shows sliders/controls per filter. A preview of the filter effect updates the video preview above the timeline in real time (or near-real-time via FFmpeg frame extraction).

## FilterPreset Model

```csharp
public class FilterPreset
{
    public double Saturation { get; set; }   // 0.0–3.0, default 1.0
    public double Vibrance { get; set; }     // custom eq approximation
    public double Brightness { get; set; }   // -1.0 to 1.0
    public double Contrast { get; set; }     // -1.0 to 1.0
    public double Sharpness { get; set; }    // 0.0–5.0
    public double Gamma { get; set; }        // 0.1–10.0
}
```

## FilterService — Building vf Chains

`FilterService.BuildVfChain(FilterPreset)` returns a `-vf` argument string for FFmpeg:

```
eq=saturation={sat}:brightness={br}:contrast={con}:gamma={gam},unsharp=luma_amount={sharp}
```

Vibrance is approximated via `eq` + `hue` filters or a custom `curves` filter.

## Preview Flow

When any filter slider changes:
1. `FiltersViewModel` calls `FFmpegService.ExtractFrameWithFilterAsync(filePath, currentPosition, vfChain)`
2. FFmpeg spawns: `ffmpeg -ss {pos} -i {file} -vf "{chain}" -frames:v 1 -f image2pipe -vcodec png pipe:1`
3. Result `BitmapImage` shown in preview panel
4. Debounce slider changes by ~200 ms to avoid FFmpeg spam

## Export Integration

When exporting, `ExportViewModel` calls `FilterService.BuildVfChain(activePreset)` and appends it to the FFmpeg encode command's `-vf` argument. If no filters are active (all values at default), omit `-vf` entirely.

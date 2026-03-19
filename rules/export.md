# Export Rules

## Export Dialog

Triggered by the Export button (top-right, upload icon + "Export" label). Opens a modal `ExportDialog` window.

### ExportSettings Model

```csharp
public class ExportSettings
{
    public string OutputFolder { get; set; }       // last used, persisted
    public string Container { get; set; }          // "mp4", "mkv", "webm"
    public string VideoCodec { get; set; }         // "h264", "h265", "vp9"
    public string AudioCodec { get; set; }         // "aac", "opus"
    public BitrateMode BitrateMode { get; set; }   // CRF or CBR
    public int Crf { get; set; }                   // 0-51 for h264/h265
    public int Bitrate { get; set; }               // kbps for CBR
    public string Preset { get; set; }             // "ultrafast"…"veryslow" (default: "slow" = high quality)
    public string Profile { get; set; }            // "baseline", "main", "high" (default: "high")
    public bool MixAudioTracks { get; set; }       // merge all audio tracks into one (default: true), persisted
    public bool EmbedUpload { get; set; }          // persisted toggle
}
```

### File Size Estimation

Estimate before encoding:
```
duration_seconds = (OutPoint - InPoint).TotalSeconds
estimated_bytes  = (bitrate_kbps * 1000 / 8) * duration_seconds
                 + audio_bitrate_kbps * 1000 / 8 * duration_seconds
```
For CRF mode use a heuristic based on resolution × duration × quality factor. Display as `~X.X MB`.

## FFmpeg Encode Command

Built by `FFmpegService.BuildEncodeArgs(ExportSettings, inPoint, outPoint, inputPath, outputPath)`:

```
ffmpeg -y -hide_banner
  -ss {inPoint}
  -to {outPoint}
  -i {inputPath}
  -c:v {videoCodec} -preset {preset} -profile:v {profile}
  [-crf {crf} | -b:v {bitrate}k]
  [if MixAudioTracks: -filter_complex "[0:a]amerge=inputs={trackCount}[aout]" -map 0:v -map "[aout]"]
  [else: -map 0:v -map 0:a]
  -c:a {audioCodec} -b:a 192k -ac 2
  -threads 0
  -movflags +faststart
  {outputPath}
```

- `-threads 0` — tells FFmpeg to use all available CPU cores for encoding (multithreaded by default for h264/h265).
- `-ac 2` — downmix to stereo after merge (required when using `amerge`).
- `amerge=inputs=N` — N is the number of audio streams detected in the source file via FFprobe.

Progress is parsed from FFmpeg stderr `time=HH:MM:SS.ss` lines and reported as a 0–100 double to the export progress bar.

## Audio Track Merging

### Why It Matters

Game capture software (OBS, NVIDIA ShadowPlay, etc.) often records multiple audio tracks (e.g., track 1 = game, track 2 = mic, track 3 = desktop). Discord and many platforms only play the first audio track, silently ignoring the rest.

### Option in Export Dialog

Toggle: **"Merge audio tracks into one"** (default: ON, persisted in settings).

- **ON** — `amerge` filter combines all audio streams into a single stereo track. All audio is audible everywhere.
- **OFF** — all audio tracks are mapped as separate streams with `-map 0:a`. Useful for video editors that want separate tracks.

### Detecting Track Count

Before building encode args, probe the source:
```
ffprobe -v error -select_streams a -show_entries stream=index -of csv=p=0 {inputPath}
```
Count the returned lines → `trackCount`. If `trackCount <= 1`, skip `amerge` even if the toggle is ON (no-op).

## Export Toggle (Embed Upload)

- A toggle button/checkbox in the Export dialog: "Upload as embed link"
- State persisted in `settings.json` → `EmbedUpload`
- If enabled, after encode completes, hand the output file to `UploadService` automatically
- Show the `UploadProgressDialog` as the next step

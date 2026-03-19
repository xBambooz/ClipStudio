# Export Rules

## Export Dialog

Triggered by the Export button (top-right toolbar). Opens a modal `ExportDialog` window.

## ExportSettings Model

```csharp
public class ExportSettings : ObservableObject
{
    string OutputFolder;        // last used, persisted
    string Container;           // "mp4", "mkv", "webm"
    string VideoCodec;          // "libx264", "libx265", "libvpx-vp9"
    string AudioCodec;          // "aac", "libopus", "mp3"
    BitrateMode BitrateMode;    // CRF or CBR
    int Crf;                    // 0-51 (default: 18)
    int Bitrate;                // kbps for CBR (default: 8000)
    string Preset;              // "ultrafast"…"veryslow" (default: "slow")
    string Profile;             // "baseline", "main", "high" (default: "high")
    bool MixAudioTracks;        // merge all audio (default: true), persisted
    bool EmbedUpload;           // upload after export, persisted
    bool HardwareAcceleration;  // use GPU encoding (default: true), persisted
}
```

## GPU Hardware Acceleration

When `HardwareAcceleration` is true:

### Decoding
`-hwaccel auto` added before `-i` on all FFmpeg decode commands (encode input + frame extraction).

### Encoding — Auto-Detection
`FFmpegService.ProbeGpuEncodersAsync()` runs `ffmpeg -encoders` and caches available GPU encoders:

| Priority | H.264 | HEVC |
|----------|-------|------|
| 1 (NVIDIA) | `h264_nvenc` | `hevc_nvenc` |
| 2 (Intel) | `h264_qsv` | `hevc_qsv` |
| 3 (AMD) | `h264_amf` | `hevc_amf` |

### Encoding — Transparent Substitution
User selects `libx264`/`libx265` in the UI — `ResolveCodec()` swaps to the GPU encoder behind the scenes. VP9 stays software (no GPU encoder).

### Preset Mapping
| Software | NVENC (p1-p7) | QSV | AMF |
|----------|---------------|-----|-----|
| ultrafast | p1 | veryfast | speed |
| fast | p3 | fast | speed |
| medium | p4 | medium | balanced |
| slow | p5 | slow | quality |
| veryslow | p7 | veryslow | quality |

### Rate Control Mapping
| Mode | Software | NVENC | QSV | AMF |
|------|----------|-------|-----|-----|
| CRF | `-crf N` | `-cq N -rc vbr` | `-global_quality N` | `-qp_i N -qp_p N -rc cqp` |
| CBR | `-b:v Nk` | `-b:v Nk` | `-b:v Nk` | `-b:v Nk` |

### Profile
NVENC supports `baseline/main/high`. QSV/AMF: profile flag omitted (auto-select).

## FFmpeg Encode Command

Built by `FFmpegService.BuildEncodeProcessStartInfo()`:

```
ffmpeg -y -hide_banner
  [-hwaccel auto]                    # if HW accel enabled
  -ss {inPoint} -to {outPoint}
  -i {inputPath}
  -c:v {resolvedCodec}              # GPU or software
  -preset {resolvedPreset}          # mapped to GPU presets
  [-profile:v {profile}]            # omitted for QSV/AMF
  [-filter_complex amerge ...]      # if mixing audio
  [-vf "{vfChain}"]                 # if filters active
  -c:a {audioCodec} -b:a 192k -ac 2 -threads 0
  [-crf N | -cq N -rc vbr | ...]   # rate control per encoder
  -movflags +faststart {outputPath}
```

## Audio Track Merging

Toggle: **"Merge audio tracks"** (default: ON, persisted).

- **ON**: `amerge=inputs=N` combines all audio into stereo. Handles multi-track game captures (OBS, ShadowPlay).
- **OFF**: `-map 0:a` keeps separate tracks.
- Skipped if `audioTrackCount <= 1`.

## File Size Estimation

CRF mode uses heuristic: `pixels × duration × quality_factor`. CBR mode: `bitrate × duration`. Displayed as `~X.X MB`.

## Export Flow

1. `ExportViewModel.RunExportAsync()` → `FFmpegService.ProbeGpuEncodersAsync()` (if HW accel)
2. `FFmpegService.EncodeAsync()` with progress parsed from stderr `time=` lines
3. On success: `ExportCompleted` event fires → `MainViewModel.HasExported = true`
4. If `EmbedUpload`: launches `UploadProgressDialog` automatically
5. Cancellation kills the FFmpeg process tree

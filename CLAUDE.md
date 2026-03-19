# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Bambooz Clip Studio** — A WPF desktop application for trimming video clips, applying filters, and optionally uploading them as embeddable links (via catbox.moe + x266.mov) that can be shared in Discord. Built on .NET 10.0-windows with C# using MVVM.

The CatBoxModeUploader console app (`C:\Users\Ryan\source\repos\CatBoxModeUploader`) has been **absorbed** into this project — its upload/embed logic lives here as a service layer; that external project is no longer needed.

## Build & Run

```bash
# Build
dotnet build TrimClipEmbedToDiscord.slnx

# Run (Windows only — WPF)
dotnet run --project TrimClipEmbedToDiscord/TrimClipEmbedToDiscord.csproj

# Publish
dotnet publish TrimClipEmbedToDiscord/TrimClipEmbedToDiscord.csproj -c Release
```

No test projects exist. Open in Visual Studio 2022+ for the WPF designer.

## Architecture Overview

MVVM pattern. `MainViewModel` owns all child ViewModels. The main window has a fixed layout: toolbar (row 0), preview area + filters panel (row 1), timeline (row 2). No ContentControl navigation — all sections are always present.

```
App.xaml              → Converters, theme merges (dark/light auto-detected from Windows)
App.xaml.cs           → DI wiring, theme switching (ApplyTheme), dark mode detection
MainWindow.xaml       → Toolbar + VLC preview + filters panel + timeline, custom chrome (no OS title bar)
MainWindow.xaml.cs    → LibVLC lifecycle, VLC↔VM sync, keyboard shortcuts, window bounds persistence
Views/                → TimelineView, FiltersPanel, ExportDialog, UploadProgressDialog
ViewModels/           → MainViewModel, TimelineViewModel, FiltersViewModel, ExportViewModel, UploadProgressViewModel
Services/             → FFmpegService, UploadService, FilterService, SettingsService, UpdateService
Models/               → ClipProject, ExportSettings, FilterPreset, AppSettings
Core/                 → ObservableObject, RelayCommand, AsyncRelayCommand
Converters/           → InverseBoolToVis, InverseBool, StringToVis, EnumToIndex, DoneToCloseText
Theme/                → UIColors*.xaml (10 themes), UIStyles.xaml
```

## Key Design Decisions

- **Playback engine:** LibVLCSharp.WPF (`VideoView`) — replaced WPF's native MediaElement because it requires Windows Media Player/WMF which may be disabled. VLC handles all playback and seeking natively.
  - LibVLC initialized with `--avcodec-hw=any` for GPU-accelerated decoding
  - VLC events fire on VLC's internal thread — always use `Dispatcher.BeginInvoke` (never `Invoke`) to avoid deadlocks
  - WinForms airspace issue: VLC's `VideoView` uses `WindowsFormsHost` (HWND), WPF elements can't render on top of it. Filter preview uses a WPF `Image` overlay that hides the `VideoView` when active.
  - `ShowVideoPlayer => HasMedia && !ShowPreviewImage` — VLC visible except during filter preview
  - `ShowPreviewImage => HasMedia && !IsPlaying && HasFilters && PreviewFrame != null` — FFmpeg frame only with active filters while paused

- **Theme:** Auto-detect Windows dark/light mode via `AppsUseLightTheme` registry key; user can override in settings. 10 themes total: Auto, Dark, Light, MintDark, RedDark, PremierePro, OLED, Discord, TwilightBlurple, YouTube. All use `DynamicResource` for live switching.

- **GPU hardware acceleration:**
  - VLC playback: `--avcodec-hw=any`
  - FFmpeg decoding: `-hwaccel auto` on all decode commands when enabled
  - FFmpeg encoding: auto-detects GPU encoders (NVENC → QSV → AMF priority), maps software presets to GPU presets, handles GPU-specific rate control (`-cq` for NVENC, `-global_quality` for QSV, `-qp` for AMF)
  - Toggle in Settings gear menu, persisted in AppSettings, default ON

- **Timeline:** Premiere Pro-style — track header (V1 label), clip container with accent color bar, filename overlay, edge-to-edge thumbnails, thin 6px trim handles with dim regions outside in/out. Hover tooltip shows nearest thumbnail + timecode. Handles snap to whole seconds within 8px threshold.
  - Ruler ticks and labels are drawn in code-behind using theme resources, not hardcoded colors
  - Main tick labels use stable integer-based cadence rather than floating-point modulo
  - The transport bar now shows filename on the left and current position / zoom / volume on the right; the old In/Out/Dur readout was removed

- **FFmpeg:** Auto-downloaded to `%LocalAppData%\BamboozClipStudio\ffmpeg\ffmpeg.exe` if not on PATH.
  - First-time setup surfaces this status in the loading overlay and a themed startup dialog

- **Export:** Modal `ExportDialog` with format options (container, codec, bitrate mode, preset, profile) and live estimated file size. GPU encoders auto-substituted when HW accel is ON. Toggle to embed-upload persisted in settings.
  - Export/upload share a background-job status surface in the main toolbar with cancellation

- **Upload pipeline (from absorbed CatBoxModeUploader):**
  1. `ffmpeg -movflags +faststart` remux to temp file
  2. POST multipart to `https://catbox.moe/user/api.php` with progress tracking
  3. Parse catbox URL → build `https://x266.mov/e/{url}?i={previewSecond}&w={width}&h={height}`
  4. Copy final embed URL to clipboard; show in progress popup

- **Settings persistence:** `%AppData%\BamboozClipStudio\settings.json` — stores theme, volume, window bounds, filter values, export toggle, HW accel, mix audio, auto-update, last output folder.
  - Also stores last open media folder for the clip picker, first-run/bootstrap flags, and crash recovery session state

- **Filter persistence:** All 6 filter slider values (saturation, vibrance, brightness, contrast, sharpness, gamma) saved to settings and restored on launch — filters carry over between sessions even with different video files.
  - `FilterService.BuildVfChain()` now applies all six sliders, including `vibrance=intensity=...`

- **Window bounds persistence:** Window position, size, and maximized state saved on close, restored on launch. Multi-monitor aware — validates saved position against virtual screen bounds.
  - When maximized, root content applies a work-area inset so bottom UI is not clipped by the custom borderless window chrome

- **Close confirmation:** When a clip is loaded but hasn't been exported, closing shows a confirmation dialog. `HasExported` flag resets when loading new media.
  - Confirmation and status prompts use a themed custom dialog with rounded corners

- **Volume:** Slider with live `%` label. Muting sets slider to 0, unmuting restores previous value. Volume persisted in settings.

- **Keyboard shortcuts:** `PreviewKeyDown` (tunneling) used instead of `KeyDown` to intercept before focused buttons process Space as a click. Space=play/pause, Ctrl+O=open, Left/Right=step frame.
  - Resuming playback does not re-seek on unpause; it continues from VLC's current paused frame to avoid jumping back a frame or two

- **Title bar:** Shows `"filename.mp4 — Bambooz Clip Studio"` when media is loaded.

- **Threading rules:**
  - `Clip = clip` must be marshaled to UI thread via `Dispatcher.InvokeAsync` — its setter triggers 6+ PropertyChanged events through Timeline/Export
  - Thumbnails: collected in `ConcurrentBag` on thread pool, batch-added on UI thread in one pass
  - Thumbnail parallelism capped at `Min(3, ProcessorCount / 4)` to avoid I/O starvation
  - VLC position sync: 60fps `DispatcherTimer` during playback, with `_syncingFromMedia` guard to prevent feedback loops
  - On pause: read VLC's `_player.Time` INTO the VM (not the other way around) to prevent snapping back to stale position

## Context Rules (CARL)

Detailed rules per feature domain live in `rules/`:

| File | Topic |
|------|-------|
| [rules/architecture.md](rules/architecture.md) | MVVM pattern, services, DI |
| [rules/timeline.md](rules/timeline.md) | LibVLC playback, in/out handles, zoom, thumbnails, hover preview, snap |
| [rules/export.md](rules/export.md) | Export dialog, audio merging, FFmpeg encode args, GPU acceleration, file size estimation |
| [rules/upload.md](rules/upload.md) | CatBox upload pipeline, x266 embed link, progress popup |
| [rules/filters.md](rules/filters.md) | Filter presets, FFmpeg vf chains, filter persistence, preview flow |
| [rules/ui-theming.md](rules/ui-theming.md) | 10 themes, XAML styles, DynamicResource switching |
| [rules/updates.md](rules/updates.md) | GitHub Releases auto-update, toolbar toggle, installer download |
| [rules/installer.md](rules/installer.md) | Inno Setup script, publish pipeline, FFmpeg bundling decision |

See [agents.md](agents.md) for specialized agent definitions per domain.

## Key Paths

| Location | Purpose |
|----------|---------|
| `%AppData%\BamboozClipStudio\settings.json` | App settings (theme, volume, window bounds, filters, export toggle) |
| `%AppData%\BamboozClipStudio\settings.json` | Also stores last open folder, crash recovery state, and first-run setup state |
| `%LocalAppData%\BamboozClipStudio\ffmpeg\ffmpeg.exe` | Auto-installed FFmpeg binary |
| `TrimClipEmbedToDiscord/Services/` | FFmpegService, UploadService, FilterService, SettingsService, UpdateService |
| `TrimClipEmbedToDiscord/Theme/` | 10 XAML color resource dictionaries + UIStyles.xaml |
| `TrimClipEmbedToDiscord/Converters/` | Value converters (BoolToVis, InverseBool, StringToVis, etc.) |

## NuGet Dependencies

- `LibVLCSharp.WPF` — WPF video player backed by VLC (replaced FFME/MediaElement)
- `VideoLAN.LibVLC.Windows` — Native VLC libraries for Windows x64
- `ModernWpfUI` — Modern WPF controls with auto dark/light theme
- `System.Text.Json` — Settings serialization (built-in to .NET 10)

## Packaging

- **Installer tool:** Inno Setup (installed separately, not a NuGet package)
- **Distribute:** Upload `BamboozClipStudioSetup-{version}.exe` to GitHub Releases; the app fetches `https://api.github.com/repos/xBambooz/ClipStudio/releases/latest` to check for updates

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

MVVM pattern. `MainViewModel` owns all child ViewModels and swaps `CurrentView` in a `ContentControl`. DataTemplates in `App.xaml` map ViewModel types → UserControl views.

```
App.xaml              → DataTemplates, theme merges (dark/light auto-detected from Windows)
MainWindow.xaml       → Toolbar + timeline area + ContentControl, no OS chrome
Views/                → UserControl XAML files (MediaDropView, TimelineView, ExportView, etc.)
ViewModels/           → ViewModel classes
Services/             → FFmpegService, UploadService, FilterService, SettingsService
Models/               → ClipProject, ExportSettings, FilterPreset
Core/                 → ObservableObject, RelayCommand
Theme/                → UIColors.xaml, UIStyles.xaml (light/dark variants)
```

## Key Design Decisions

- **Theme:** Auto-detect Windows dark/light mode on launch via `AppsUseLightTheme` registry key; user can override in settings. Accent color is `#00C8D4` (light cyan/teal).
- **Media formats:** Accept MP4, MKV, MOV, AVI, WebM via `OpenFileDialog` filter. FFmpeg handles all decoding/encoding internally.
- **Timeline:** Full-clip canvas — no empty space. Drag handles to set in/out points. Zoom via scroll wheel. Playback via a play button on the timeline. Ruler shows timecodes.
- **FFmpeg:** Auto-downloaded to `%LocalAppData%\BamboozClipStudio\ffmpeg\ffmpeg.exe` if not on PATH (same logic as absorbed CatBoxModeUploader).
- **Export:** Modal dialog with format options (container, codec, bitrate mode, preset quality, profile) and live estimated file size (like Premiere). Toggle to embed-upload is persisted in settings.
- **Upload pipeline (from absorbed CatBoxModeUploader):**
  1. `ffmpeg -movflags +faststart` remux to temp file
  2. POST multipart to `https://catbox.moe/user/api.php` with progress tracking via `ProgressableStreamContent`
  3. Parse catbox URL → build `https://x266.mov/e/{url}?i={previewSecond}&w={width}&h={height}`
  4. Copy final embed URL to clipboard; show in progress popup
- **Context menu registration:** Toolbar option registers/unregisters `HKCR\VideoFiles\shell\BamboozClipStudio` for supported video formats.
- **Audio track merging:** Export option (default ON) merges all audio streams into one stereo track via `amerge` — prevents Discord/platforms only playing the first track. Uses `ffprobe` to count tracks; skips merge if only one track exists.
- **Multithreading:** Export uses `-threads 0` (all CPU cores). Timeline thumbnail strip generation uses `Parallel.ForEach` capped at `ProcessorCount / 2` off the UI thread.
- **Playback:** FFME (`Unosquare.FFME.Windows`) for smooth hardware-accelerated preview and seeking. FFME uses the same auto-downloaded FFmpeg binary.
- **Auto-update:** Checks GitHub Releases API on startup (toggle in toolbar, default ON). Manual "Check for updates" button also in toolbar. Downloads installer `.exe` from release assets and launches it.
- **Installer:** Inno Setup script at `Installer/BamboozClipStudio.iss`. Produces `BamboozClipStudioSetup-{version}.exe` uploaded to GitHub Releases as the distributable. Desktop + Start Menu shortcuts. FFmpeg is NOT bundled — auto-downloaded on first launch.
- **Settings persistence:** `%AppData%\BamboozClipStudio\settings.json` — stores export toggle state, last output folder, theme override, `MixAudioTracks`, `AutoCheckUpdates`.

## Context Rules (CARL)

Detailed rules per feature domain live in `rules/`:

| File | Topic |
|------|-------|
| [rules/architecture.md](rules/architecture.md) | MVVM pattern, navigation, DataTemplates |
| [rules/timeline.md](rules/timeline.md) | FFME playback, in/out handles, zoom, multithreaded thumbnails |
| [rules/export.md](rules/export.md) | Export dialog, audio merging, FFmpeg encode args, multithreading, file size estimation |
| [rules/upload.md](rules/upload.md) | CatBox upload pipeline, x266 embed link, progress popup |
| [rules/filters.md](rules/filters.md) | Filter presets, FFmpeg vf chains, filter UI |
| [rules/ui-theming.md](rules/ui-theming.md) | Color palette, XAML styles, dark/light switching, scrollbar/dropdown styles |
| [rules/updates.md](rules/updates.md) | GitHub Releases auto-update, toolbar toggle, manual check, installer download |
| [rules/installer.md](rules/installer.md) | Inno Setup script, publish pipeline, FFmpeg bundling decision |

See [agents.md](agents.md) for specialized agent definitions per domain.

## Key Paths

| Location | Purpose |
|----------|---------|
| `%AppData%\BamboozClipStudio\settings.json` | App settings (theme, last folder, export toggle) |
| `%LocalAppData%\BamboozClipStudio\ffmpeg\ffmpeg.exe` | Auto-installed FFmpeg binary |
| `TrimClipEmbedToDiscord/Services/` | FFmpegService, UploadService, FilterService |
| `TrimClipEmbedToDiscord/Theme/` | XAML resource dictionaries |

## NuGet Dependencies (planned)

- `ModernWpfUI` — Modern WPF controls with auto dark/light theme
- `Unosquare.FFME.Windows` — WPF MediaElement backed by FFmpeg; used for playback and hardware-accelerated seeking
- `System.Text.Json` — Settings serialization (built-in to .NET 10, no extra package needed)

## Packaging

- **Installer tool:** Inno Setup (installed separately, not a NuGet package)
- **Distribute:** Upload `BamboozClipStudioSetup-{version}.exe` to GitHub Releases; the app fetches `https://api.github.com/repos/xBambooz/ClipStudio/releases/latest` to check for updates

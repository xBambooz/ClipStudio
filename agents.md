# agents.md — CARL Agent Definitions

Specialized Claude Code agents for each feature domain in Bambooz Clip Studio.

---

## Agent: Architecture Agent

**Trigger:** Adding new views/sections, restructuring MVVM, changing DI wiring, cross-cutting concerns.

**Context file:** [`rules/architecture.md`](rules/architecture.md)

**Scope:**
- `App.xaml.cs` (DI wiring, theme switching)
- `MainWindow.xaml` / `.cs` (layout, LibVLC lifecycle, window bounds)
- `Core/ObservableObject.cs`, `Core/RelayCommand.cs`
- `Converters/Converters.cs`
- Adding new View + ViewModel pairs

---

## Agent: Timeline Agent

**Trigger:** Any work on the video timeline — playback, in/out trimming, zoom, ruler, drag handles, hover preview, volume, transport controls.

**Context file:** [`rules/timeline.md`](rules/timeline.md)

**Scope:**
- `Views/TimelineView.xaml` / `.cs`
- `ViewModels/TimelineViewModel.cs`
- `MainWindow.xaml.cs` (VLC ↔ VM sync, position timer, keyboard shortcuts)
- `Services/FFmpegService.cs` (frame extraction, thumbnails)

**Key concerns:** VLC threading (BeginInvoke), pause position sync, _syncingFromMedia guard, snap-to-seconds, hover tooltip

---

## Agent: Export Agent

**Trigger:** Export dialog, FFmpeg encode settings, format/codec/bitrate options, GPU acceleration, file size estimation.

**Context file:** [`rules/export.md`](rules/export.md)

**Scope:**
- `Views/ExportDialog.xaml`
- `ViewModels/ExportViewModel.cs`
- `Services/FFmpegService.cs` (BuildEncodeProcessStartInfo, EncodeAsync, GPU detection)
- `Models/ExportSettings.cs`

**Key concerns:** GPU encoder auto-detection/substitution, preset/rate-control mapping, ExportCompleted event

---

## Agent: Upload Agent

**Trigger:** CatBox upload, x266 embed link generation, progress popup, clipboard copy.

**Context file:** [`rules/upload.md`](rules/upload.md)

**Scope:**
- `Services/UploadService.cs`
- `Views/UploadProgressDialog.xaml`
- `ViewModels/UploadProgressViewModel.cs`

---

## Agent: Filters Agent

**Trigger:** Color/visual filters, FFmpeg vf chain building, filter UI, filter persistence.

**Context file:** [`rules/filters.md`](rules/filters.md)

**Scope:**
- `Services/FilterService.cs`
- `Views/FiltersPanel.xaml`
- `ViewModels/FiltersViewModel.cs`
- `Models/FilterPreset.cs`
- `MainViewModel.cs` (filter persistence to AppSettings, preview frame extraction)

**Key concerns:** Vibrance not yet wired, InvariantCulture formatting, preview via FFmpeg (not VLC), airspace workaround

---

## Agent: UI/Theme Agent

**Trigger:** Styling, theming, XAML layout, theme switching, new themes, control templates.

**Context file:** [`rules/ui-theming.md`](rules/ui-theming.md)

**Scope:**
- `Theme/UIColors*.xaml` (10 theme files), `Theme/UIStyles.xaml`
- `App.xaml` (resource dictionary merging, converter registration)
- `App.xaml.cs` (`ApplyTheme` switch)
- `MainWindow.xaml` (toolbar, layout)
- `MainViewModel.cs` (ThemeMode* properties, SetTheme)

**Key concerns:** DynamicResource required for live switching, all 10 themes must stay consistent

---

## Agent: Update Agent

**Trigger:** Auto-update check, GitHub Releases API, toolbar update toggle, installer download flow.

**Context file:** [`rules/updates.md`](rules/updates.md)

**Scope:**
- `Services/UpdateService.cs`
- Toolbar menu items (AutoCheckUpdates toggle, manual check button)
- `MainViewModel.cs` (CheckUpdatesAsync, update download progress)

---

## Agent: Installer Agent

**Trigger:** Building the distributable installer, Inno Setup script, publish pipeline.

**Context file:** [`rules/installer.md`](rules/installer.md)

**Scope:**
- `Installer/BamboozClipStudio.iss`
- `.csproj` publish settings
- GitHub Release asset upload

---

## Using Multiple Agents

For cross-domain work, combine agents:

> "Act as both the Export Agent and Upload Agent. I need to wire the export-then-upload flow end to end."

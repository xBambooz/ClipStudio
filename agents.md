# agents.md — CARL Agent Definitions

Specialized Claude Code agents for each feature domain in Bambooz Clip Studio.

---

## Agent: Architecture Agent

**Trigger:** Adding new views/sections, restructuring MVVM, changing navigation, cross-cutting concerns.

**Context file:** [`rules/architecture.md`](rules/architecture.md)

**Scope:**
- Adding View + ViewModel + nav entry
- Changing DataTemplate/ContentControl system in `App.xaml`
- Modifying `MainViewModel` navigation
- `Core/ObservableObject.cs`, `Core/RelayCommand.cs`

**Prompt pattern:**
> "Act as the Architecture Agent. I need to add a new [FeatureName] section."

---

## Agent: Timeline Agent

**Trigger:** Any work on the video timeline — playback, in/out trimming, zoom, ruler, drag handles.

**Context file:** [`rules/timeline.md`](rules/timeline.md)

**Scope:**
- `Views/TimelineView.xaml`
- `ViewModels/TimelineViewModel.cs`
- `Services/FFmpegService.cs` (seek/preview frames)
- Drag handle logic, zoom scroll, playback timer

**Prompt pattern:**
> "Act as the Timeline Agent. I need to implement [trim handles / zoom / playback]."

---

## Agent: Export Agent

**Trigger:** Export dialog, FFmpeg encode settings, format/codec/bitrate options, file size estimation.

**Context file:** [`rules/export.md`](rules/export.md)

**Scope:**
- `Views/ExportDialog.xaml`
- `ViewModels/ExportViewModel.cs`
- `Services/FFmpegService.cs` (encode path)
- `Models/ExportSettings.cs`

**Prompt pattern:**
> "Act as the Export Agent. I need to add [format option / bitrate control / size estimation]."

---

## Agent: Upload Agent

**Trigger:** CatBox upload, x266 embed link generation, progress popup, clipboard copy.

**Context file:** [`rules/upload.md`](rules/upload.md)

**Scope:**
- `Services/UploadService.cs`
- `Views/UploadProgressDialog.xaml`
- `ViewModels/UploadProgressViewModel.cs`
- `ProgressableStreamContent` helper

**Prompt pattern:**
> "Act as the Upload Agent. I need to [add upload progress / fix catbox POST / change embed URL format]."

---

## Agent: Filters Agent

**Trigger:** Color/visual filters, FFmpeg vf chain building, filter preset UI.

**Context file:** [`rules/filters.md`](rules/filters.md)

**Scope:**
- `Services/FilterService.cs`
- `Views/FiltersPanel.xaml`
- `ViewModels/FiltersViewModel.cs`
- `Models/FilterPreset.cs`

**Prompt pattern:**
> "Act as the Filters Agent. I need to add [vibrance / saturation / color grading] filter support."

---

## Agent: UI/Theme Agent

**Trigger:** Styling, theming, XAML layout, dark/light mode, scrollbar/dropdown styles, color palette.

**Context file:** [`rules/ui-theming.md`](rules/ui-theming.md)

**Scope:**
- `Theme/UIColors.xaml`, `Theme/UIStyles.xaml`
- `MainWindow.xaml` toolbar and layout
- Dark/light auto-detection logic
- All custom control templates (scrollbars, dropdowns, buttons)

**Prompt pattern:**
> "Act as the UI/Theme Agent. I need to style [component] consistently with the app."

---

## Agent: Update Agent

**Trigger:** Auto-update check, GitHub Releases API, toolbar update toggle, installer download flow.

**Context file:** [`rules/updates.md`](rules/updates.md)

**Scope:**
- `Services/UpdateService.cs`
- `Views/UpdateDialog.xaml`
- Toolbar menu items for update toggle + manual check
- GitHub API call, version comparison, installer download + launch

**Prompt pattern:**
> "Act as the Update Agent. I need to implement [auto-check on startup / manual check button / installer download]."

---

## Agent: Installer Agent

**Trigger:** Building the distributable installer, Inno Setup script, publish pipeline, GitHub Release packaging.

**Context file:** [`rules/installer.md`](rules/installer.md)

**Scope:**
- `Installer/BamboozClipStudio.iss`
- `.csproj` publish settings
- GitHub Release asset upload workflow

**Prompt pattern:**
> "Act as the Installer Agent. I need to [set up the Inno Setup script / add a new file to the install / configure the publish profile]."

---

## Using Multiple Agents

For cross-domain work, combine agents:

> "Act as both the Export Agent and Upload Agent. I need to wire the export-then-upload flow end to end."

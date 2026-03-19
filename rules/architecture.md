# Architecture Rules

## MVVM Pattern

Strict MVVM. Views contain zero business logic. All state is in ViewModels. `MainViewModel` owns all child VMs. The main window uses a fixed grid layout (toolbar / preview+filters / timeline) — no ContentControl navigation.

```
MainViewModel
  ├── TimelineViewModel       (clip, in/out points, playback, volume, filename)
  ├── FiltersViewModel        (filter presets, visibility, vf chain building)
  └── ExportViewModel         (export settings, size estimate, encode+upload)
```

`UploadProgressViewModel` is created on-demand during export when embed upload is enabled.

## MainWindow Layout

```
Row 0 (46px):  Toolbar — app name, File/Filters/Tools/Settings menus, Export button, window controls
Row 1 (*):     Preview area (VLC VideoView + filter preview Image + drop zone) + FiltersPanel side panel
Row 2 (160px): TimelineView (canvas + transport bar)
```

No OS chrome — `WindowStyle="None"` with custom title bar, minimize/maximize/close buttons, and `WindowChrome` for resize grip.

## Services

Services are instantiated once in `App.xaml.cs` and injected into ViewModels and MainWindow via constructor.

| Service | Role |
|---------|------|
| `FFmpegService` | FFmpeg/FFprobe process spawning (encode, frame extract, thumbnails, media info, GPU detection) |
| `UploadService` | CatBox multipart POST, x266 URL generation, remux |
| `FilterService` | Build FFmpeg `-vf` chains from `FilterPreset` model |
| `SettingsService` | Load/save `settings.json` to `%AppData%\BamboozClipStudio\` |
| `UpdateService` | GitHub Releases API check, installer download |

## DI Wiring (App.xaml.cs)

```csharp
var settings = new SettingsService();
var ffmpeg = new FFmpegService();
var filter = new FilterService();
var upload = new UploadService(ffmpeg);
var update = new UpdateService();
var vm = new MainViewModel(ffmpeg, filter, upload, settings, update);
var window = new MainWindow(vm, settings);  // settings needed for window bounds persistence
```

## Core Base Classes

- `ObservableObject` — implements `INotifyPropertyChanged`, `SetProperty<T>` helper
- `RelayCommand` / `RelayCommand<T>` — `ICommand` with `CanExecute` support
- `AsyncRelayCommand` — async `ICommand` wrapper

## Converters (Converters/Converters.cs)

Registered in `App.xaml` as static resources:

| Key | Type | Purpose |
|-----|------|---------|
| `BoolToVis` | Built-in | bool → Visible/Collapsed |
| `InverseBoolToVis` | Custom | bool → Collapsed/Visible |
| `InverseBool` | Custom | bool → !bool (two-way) |
| `StringToVis` | Custom | non-empty string → Visible, else Collapsed |
| `DoneToClose` | Custom | bool → "Close"/"Cancel" text |
| `EnumToIndex` | Custom | BitrateMode ↔ int index |

## Threading Rules

- **Never use `ConfigureAwait(false)` in code that sets VM properties** — continuations must run on UI thread
- **Never use `Dispatcher.Invoke` from thread pool threads** — causes deadlocks. Use `Dispatcher.InvokeAsync` or `BeginInvoke`
- VLC events fire on VLC's internal thread — always marshal with `Dispatcher.BeginInvoke`
- `Clip = clip` assignment must be on UI thread (triggers cascading PropertyChanged through Timeline/Export)

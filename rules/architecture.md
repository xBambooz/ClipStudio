# Architecture Rules

## MVVM Pattern

Strict MVVM. Views contain zero business logic. All state is in ViewModels. `MainViewModel` owns all child VMs and exposes `CurrentView` bound to a `ContentControl` in `MainWindow.xaml`.

```
MainViewModel
  ├── TimelineViewModel       (active clip, in/out points, playback)
  ├── FiltersViewModel        (active filter presets)
  ├── ExportViewModel         (export settings, size estimate)
  └── UploadProgressViewModel (upload state, progress)
```

## Adding a New View

1. Create `Views/FooView.xaml` (UserControl)
2. Create `ViewModels/FooViewModel.cs` (extends `ObservableObject`)
3. Add DataTemplate in `App.xaml`:
   ```xml
   <DataTemplate DataType="{x:Type vm:FooViewModel}">
       <views:FooView/>
   </DataTemplate>
   ```
4. Add property + RelayCommand on `MainViewModel`

## Navigation

`MainViewModel.CurrentView` setter — assign a ViewModel instance, the DataTemplate system renders the matching View automatically. No frames, no page navigation.

## Services

Services are instantiated once in `App.xaml.cs` (or a simple service locator) and injected into ViewModels via constructor. Never instantiate services inside Views.

| Service | Role |
|---------|------|
| `FFmpegService` | All FFmpeg process spawning (trim, encode, frame extract, filter preview) |
| `UploadService` | CatBox multipart POST, x266 URL generation |
| `FilterService` | Build FFmpeg `-vf` chains from `FilterPreset` model |
| `SettingsService` | Load/save `settings.json`, expose typed settings object |

## Core Base Classes

- `ObservableObject` — implements `INotifyPropertyChanged`, `SetProperty<T>` helper
- `RelayCommand` / `RelayCommand<T>` — `ICommand` implementations with `CanExecute` support

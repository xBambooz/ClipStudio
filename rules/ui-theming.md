# UI & Theming Rules

## Theme System

10 themes available, all switchable live via `DynamicResource`. Theme selected in Settings gear menu → Theme submenu.

| Theme | File | Accent | Background Style |
|-------|------|--------|-----------------|
| Auto (System) | UIColors.xaml or UIColors-Light.xaml | `#00C8D4` | Follows Windows dark/light |
| Dark | UIColors.xaml | `#00C8D4` | Dark gray `#141414` |
| Light | UIColors-Light.xaml | `#00A8B4` | Light gray `#F5F5F5` |
| Mint Dark | UIColors-MintDark.xaml | Mint green | Dark |
| Red Dark | UIColors-RedDark.xaml | Red | Dark |
| Premiere Pro | UIColors-PremierePro.xaml | Adobe blue | Premiere gray tones |
| OLED | UIColors-OLED.xaml | `#FFFFFF` | Pure black `#000000` |
| Discord | UIColors-Discord.xaml | Blurple `#5865F2` | Discord dark `#36393F` |
| Twilight Blurple | UIColors-TwilightBlurple.xaml | Blurple `#5865F2` | AMOLED Discord `#1E1F22` |
| YouTube Dark | UIColors-YouTube.xaml | Red `#FF0000` | YouTube dark `#0F0F0F` |

## Theme Switching

`App.ApplyTheme(string? themeOverride)`:
1. Removes existing `UIColors*` dictionary from `MergedDictionaries`
2. Inserts the new one at position 0
3. All controls use `DynamicResource` so they update immediately

Auto mode reads `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme`.

## Color Resource Keys

All views reference colors by key — **never hardcode hex in XAML views**.

Key groups defined in each UIColors file:
- `AppBg`, `PanelBg`, `ToolbarBg`, `TimelineBg`, `DropZoneBg` — backgrounds
- `TextPrimary`, `TextSecondary`, `TextDisabled`, `TextOnAccent` — text
- `Accent`, `AccentHover`, `AccentPressed` — interactive elements
- `Border`, `BorderSubtle` — borders
- `Danger` — destructive/mute indicators
- `ScrollThumb`, `ScrollThumbHover` — scrollbar

## MainViewModel Theme Properties

10 bool properties (`ThemeModeAuto`, `ThemeModeDark`, ..., `ThemeModeYouTube`) for radio-style menu checkmarks. `SetTheme(string?)` updates all via `OnPropertyChanged`.

## Control Styles (UIStyles.xaml)

Custom styles override OS defaults for:
- **Scrollbar** — thin (6px), rounded thumb, accent hover, transparent track
- **ComboBox** — matches app background, accent highlight on selected
- **Slider** — thin track, circular thumb
- **Buttons**: `PrimaryButton` (accent bg), `SecondaryButton` (transparent), `IconButton` (flat), `WinControlButton` (title bar), `CloseButton` (red hover)
- **ContextMenu/MenuItem** — themed background and hover
- **ProgressBar** — accent fill
- **ToolTips** — dark background

Custom dialogs:
- `ThemedDialog` replaces default `MessageBox` prompts for first-run setup, crash restore, close confirmation, and status/error prompts
- Dialogs use theme resources, rounded outer corners, rounded header/footer sections, and custom shadow

## Window Chrome

`WindowStyle="None"`, `ResizeMode="CanResizeWithGrip"`, `WindowChrome` with `CaptionHeight="0"`. Custom title bar with:
- Drag to move (`DragMove()`)
- Double-click to maximize/restore
- Custom minimize/maximize/close buttons
- Maximized windows apply a root content inset based on the desktop work area so edge UI is not clipped

## Typography

System font — `Segoe UI` / `Segoe UI Variable`. Monospace values use `Consolas` (timecodes, volume %, filter values).

## Title Bar Spacing

The main title bar content grid has matching top/bottom vertical margin so buttons and text are not visually bottom-heavy.

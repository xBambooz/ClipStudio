# UI & Theming Rules

## Theme Library

Use **ModernWpfUI** (`ModernWpf` NuGet). It provides OS-native dark/light theme switching, modern control styles, and fluent design primitives.

```xml
<!-- App.xaml -->
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ui:ThemeResources/>
            <ui:XamlControlsResources/>
            <ResourceDictionary Source="Theme/UIColors.xaml"/>
            <ResourceDictionary Source="Theme/UIStyles.xaml"/>
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

## Auto Dark/Light Detection

On startup, read `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme`. If `0` → dark; if `1` → light. Apply via `ThemeManager.Current.ApplicationTheme`.

User can override in Settings (stored in `settings.json` as `"ThemeOverride": "Dark" | "Light" | null`).

## Color Palette (UIColors.xaml)

Always reference by key — never hardcode hex in views.

| Key | Dark Value | Light Value | Use |
|-----|-----------|-------------|-----|
| `AccentColor` | `#00C8D4` | `#00A8B4` | Primary cyan accent (buttons, highlights, handles) |
| `TimelineHighlight` | `#00C8D420` | `#00C8D430` | In/out region overlay |
| `ToolbarBackground` | `#1E1E1E` | `#F3F3F3` | Top toolbar |
| `TimelineBackground` | `#141414` | `#E8E8E8` | Timeline canvas background |

## Accent Color Usage

All interactive elements use `AccentColor` (light cyan `#00C8D4`):
- Timeline in/out drag handles
- Selected/active toolbar buttons
- Toggle button "on" state
- Progress bar fill
- Focus rings on inputs

## Control Styles (UIStyles.xaml)

**Always style these explicitly — never leave at OS default:**

### Scrollbar
```xml
<Style TargetType="ScrollBar">
    <!-- Thin (6px), rounded thumb, accent color on hover, transparent track -->
</Style>
```

### ComboBox / DropDown
```xml
<Style TargetType="ComboBox">
    <!-- Match app background, 1px border in BorderColorSecondary, accent highlight on selected item -->
</Style>
```

### Slider
```xml
<Style TargetType="Slider">
    <!-- Thin track, circular thumb in AccentColor, no default Windows chrome -->
</Style>
```

### Buttons
- **Primary** (Export, Upload): `AccentColor` background, white text, 4px corner radius
- **Secondary** (Cancel, toolbar actions): transparent background, `AccentColor` border, 4px radius
- **Icon buttons** (toolbar): 32×32 flat, no border, `AccentColor` icon tint on hover

## Window Chrome

`WindowStyle="None"`, `AllowsTransparency="False"`. Custom title bar strip with drag support (`MouseLeftButtonDown → DragMove()`). Custom minimize/maximize/close buttons in the title bar.

## Toolbar

Top toolbar has:
- Left: app icon + name
- Middle: File menu items (Open Media, Exit) + Filters toggle + Context Menu registration option
- Right: Export button (upload icon + "Export" text)

## Typography

Use **Segoe UI Variable** (Windows 11 system font) as the default. No embedded fonts needed — `FontFamily="Segoe UI Variable"` or inherit from system default via ModernWpf.

## Styling Checklist

Before considering any UI component done, verify all of these are custom-styled (not OS default):
- [ ] Scrollbars (vertical and horizontal)
- [ ] All ComboBox/DropDown menus and item containers
- [ ] All Sliders
- [ ] All TextBoxes (border, focus ring)
- [ ] ContextMenu and MenuItem
- [ ] ToolTips
- [ ] ProgressBar

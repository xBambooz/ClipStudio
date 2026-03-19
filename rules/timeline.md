# Timeline Rules

## Layout

The timeline is always anchored to the bottom of the main window (row 2, 160px). It consists of two rows: a scrollable canvas area (row 0, `*`) and a transport bar (row 1, 44px).

### Canvas Area (Premiere Pro Style)

- **Track header** (30px left column): "V1" label with accent color indicator
- **Scrollable timeline** (remaining width): ruler + clip container + thumbnails + handles + playhead

```
Constants:
  RulerHeight       = 20px   (top ruler with timecodes)
  ClipColorBarH     = 3px    (accent color strip at top of clip)
  ClipTop           = 20px   (clip area starts below ruler)
  ClipHeight        = 90px   (total clip container height)
  ThumbTop          = 37px   (below color bar + 14px filename)
  ThumbHeight       = 73px   (thumbnail display area)
  HandleWidth       = 6px    (thin trim brackets)
```

### Transport Bar

Left: filename pill (with `StringToVis` converter)
Center: transport controls (go-to-start, step-back, play/pause, step-forward, go-to-end)
Right: position timecode + zoom buttons + divider + mute button + volume slider + volume % label

## Playback Engine

Uses **LibVLCSharp.WPF** (`VideoView`) — NOT FFME or WPF MediaElement.

- LibVLC initialized with `--avcodec-hw=any` for GPU-accelerated decoding
- VLC events (`Playing`, `EndReached`, `EncounteredError`) fire on VLC's internal thread — always use `Dispatcher.BeginInvoke`
- `_openingMedia` flag: set before `_player.Play()`, cleared in `OnVlcPlaying()` which auto-pauses to show the first frame
- Position sync: 60fps `DispatcherTimer` reads `_player.Time` → `CurrentPosition` during playback
- `_syncingFromMedia` guard prevents feedback loop (VLC→VM→VLC seek cycle)
- On resume, do **not** re-seek `_player.Time` from the VM before unpausing; resume from VLC's actual paused frame

### Critical: Pause Position Sync

When stopping playback, **read VLC's position INTO the VM first**, not the other way around:
```csharp
_positionTimer.Stop();
_player.SetPause(true);
_syncingFromMedia = true;
_vm.Timeline.CurrentPosition = TimeSpan.FromMilliseconds(Math.Max(0, _player.Time));
_syncingFromMedia = false;
```
This prevents the playhead snapping back to a stale position when pausing after short playback.

### Resume Sync

When playback resumes, call `_player.SetPause(false)` directly. Reapplying `_vm.Timeline.CurrentPosition` on resume can jump playback backward by a frame or two because the VM may be slightly behind VLC's actual paused frame.

## Zoom

- Ctrl+scroll wheel zooms in/out (1.15x factor per tick)
- Plain scroll wheel scrolls horizontally
- Zoom buttons: +, -, reset (fit to view)
- `ZoomLevel` range: 1.0 – 50.0
- Canvas width = `(ScrollerWidth - 20) * ZoomLevel`
- In maximized view, the root window layout applies a work-area inset so the transport bar stays visible inside the usable desktop bounds

## In/Out Points

- `InPoint` and `OutPoint` are `TimeSpan` values on `TimelineViewModel`
- Dragging handles updates these with **snap-to-seconds** (snaps to nearest whole second within 8px threshold)
- Dim regions (55% black overlay) cover areas outside in/out
- Highlight region (12% accent overlay) covers the selected range
- Export always trims to `[InPoint, OutPoint]`

## Hover Preview Tooltip

When hovering over the timeline canvas (not dragging):
- Shows a floating `Border` above the cursor with the nearest thumbnail image + timecode
- Positioned centered on cursor X, clamped to canvas bounds
- Hidden on mouse leave and during any drag operation
- Uses existing thumbnails from `Thumbnails` collection (no FFmpeg calls)

## Thumbnails

- 10 evenly-spaced frames extracted via `FFmpegService.GenerateThumbnailsAsync`
- Parallelism capped at `Min(3, ProcessorCount / 4)` to avoid I/O starvation
- Collected in `ConcurrentBag` on thread pool, batch-added on UI thread via `Dispatcher.InvokeAsync`
- `DispatcherTimer` debounce (50ms) prevents rapid canvas redraws from `CollectionChanged`
- Edge-to-edge layout with +1px overlap to prevent sub-pixel gaps

## Ruler

- The ruler background, tick marks, and labels are drawn in code-behind with theme resources (`PanelBg`, `Accent`, `TextPrimary`, `TextSecondary`)
- Main tick labeling uses an integer tick counter, not floating-point modulo
- Whole-second labels under 1 minute render as `5s` instead of `5.0s`

## Volume

- Slider bound to `Volume` (0.0–1.0) with live `VolumePercent` text label ("75%")
- Muting saves current volume to `_preMuteVolume`, sets slider to 0
- Unmuting restores `_preMuteVolume`
- Volume persisted in `AppSettings.Volume`, saved on change (ignored during mute transitions)
- VLC volume: `_player.Volume = IsMuted ? 0 : (int)(Volume * 100)`

## Filter Preview (WinForms Airspace Workaround)

VLC's `VideoView` uses `WindowsFormsHost` — WPF elements can't render on top of it. When filters are active and playback is paused:
1. Hide `VideoView` (`ShowVideoPlayer = false`)
2. Show WPF `Image` with FFmpeg-extracted frame (`ShowPreviewImage = true`)
3. Frame extraction debounced by 150ms `DispatcherTimer`
4. Only extract when `!IsPlaying && Filters.HasFilters`

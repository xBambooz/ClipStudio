# Timeline Rules

## Layout

The timeline is always anchored to the bottom of the main window below the media drop/preview area. It is a custom WPF Canvas (not a standard slider). The timeline spans the full clip duration with no empty space — the clip fills 100% of the width.

## Components

| Element | Purpose |
|---------|---------|
| Ruler (top strip) | Shows timecodes at regular intervals; density adapts to zoom level |
| Waveform/thumbnail strip | Video keyframe thumbnails or audio waveform beneath ruler |
| In-point handle | Left drag handle (cyan); sets trim start |
| Out-point handle | Right drag handle (cyan); sets trim end |
| Playhead | Thin vertical line; draggable, also moves during playback |
| Play/Pause button | Embedded below or beside timeline |

## Zoom

- Mouse scroll wheel on the timeline canvas zooms in/out.
- Zoom is centered on the cursor position.
- Minimum zoom = full clip visible. Maximum = ~1 frame per 50px.
- `TimelineViewModel.ZoomLevel` (double) + `TimelineViewModel.ScrollOffset` (double) drive the canvas transform.

## In/Out Points

- `TimelineViewModel.InPoint` and `OutPoint` are `TimeSpan` values.
- Dragging the handles updates these. The region between them is highlighted (semi-transparent cyan overlay).
- The export always trims to `[InPoint, OutPoint]`.

## Playback

Use **Unosquare.FFME.Windows** (`ffme` NuGet) for the preview panel above the timeline. FFME is a WPF `MediaElement` drop-in backed by FFmpeg — it handles hardware-accelerated decode, correct frame timing, and smooth seeking without spawning external processes.

- Bind `ffme:MediaElement.Position` to `TimelineViewModel.CurrentPosition` (`TimeSpan`) two-way.
- Playback stops at `OutPoint` via a `PositionChanged` event that calls `Pause()` and resets.
- The playhead `Canvas.Left` is computed from `CurrentPosition` on every `PositionChanged` tick.
- FFME's `MediaElement` must be told the FFmpeg lib path on startup: `Library.FFmpegDirectory = ffmpegFolder`.

## Timeline Scrubbing (Frame-Accurate)

When the user drags the playhead or drag-seeks (not during active playback), use **FFME's `SeekAsync(position)`** — this is hardware-accelerated and far faster than spawning a new FFmpeg process per frame.

For filter preview (when filters panel is open), fall back to FFmpegService pipe extraction since FFME can't apply live vf chains:
```
ffmpeg -ss {position} -i {file} -vf "{chain}" -frames:v 1 -f image2pipe -vcodec png pipe:1
```

## Multithreaded Thumbnail Generation

Timeline thumbnail strip (keyframe images along the bottom of the timeline) is generated off the UI thread:
- Use `Task.Run` + `Parallel.ForEach` to extract N evenly-spaced frames concurrently via FFmpegService.
- Each frame result is marshalled back via `Dispatcher.InvokeAsync` to update the thumbnail strip incrementally.
- Limit parallelism to `Environment.ProcessorCount / 2` to avoid starving the UI thread.

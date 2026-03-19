# Project Summary

## Product

Bambooz Clip Studio is a WPF desktop app for opening a video, trimming a clip, applying lightweight visual adjustments, exporting through FFmpeg, and optionally uploading the result to catbox.moe to generate an embeddable `x266.mov` link for Discord.

## Core Features

- LibVLC-backed playback with keyboard shortcuts, persisted volume, and drag-and-drop media loading
- Open-media picker remembers the last folder used for selecting clips
- Premiere-style trim timeline with thumbnails, in/out handles, frame stepping, themed ruler ticks, and a simplified transport bar
- Export dialog with codec/container/preset controls, estimated size, and cancellation
- Optional upload flow that remuxes for fast-start, uploads to catbox, and returns an embed URL
- Theme switching and persisted app settings
- Auto-update checks against GitHub Releases
- All six filter sliders are now active in the FFmpeg filter chain, including vibrance

## Stability And UX

- Crash recovery now stores the last media path plus trim and playhead state and offers session restore after an unclean exit
- Startup now surfaces clearer FFmpeg/bootstrap status in both the loading overlay and the empty-state UI
- Export and upload now run through a shared background-job tracker with visible status and cancellation from the main window
- App prompts now use a themed custom dialog for first-run setup, crash recovery, close confirmation, and other status/error prompts instead of default system message boxes
- Themed dialogs use rounded custom chrome instead of default system message boxes
- Pause/resume now continues from VLC's current paused frame instead of re-seeking slightly backward on resume
- Maximized window layout now applies a work-area inset so bottom timeline controls stay visible in fullscreen/maximized view

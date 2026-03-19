# Installer Rules

## Tool: Inno Setup

Use **Inno Setup** (free, industry-standard) to build the installer `.exe`. The script lives at `Installer/BamboozClipStudio.iss`.

Why Inno Setup over NSIS or WiX:
- Single `.iss` script, easy to read/edit
- Produces a polished single-file installer
- Supports desktop shortcut, Start Menu entry, uninstaller, version check
- Free with no watermark

## Installer Script Key Sections

```ini
[Setup]
AppName=Bambooz Clip Studio
AppVersion={#AppVersion}          ; passed via /DAppVersion=x.y.z at build time
DefaultDirName={autopf}\BamboozClipStudio
DefaultGroupName=Bambooz Clip Studio
OutputDir=Installer\Output
OutputBaseFilename=BamboozClipStudioSetup-{#AppVersion}
SetupIconFile=TrimClipEmbedToDiscord\Resources\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin            ; needed for context-menu registry write

[Files]
Source: "TrimClipEmbedToDiscord\bin\Release\net10.0-windows\publish\*"; \
        DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{autodesktop}\Bambooz Clip Studio"; Filename: "{app}\BamboozClipStudio.exe"
Name: "{group}\Bambooz Clip Studio";       Filename: "{app}\BamboozClipStudio.exe"
Name: "{group}\Uninstall";                 Filename: "{uninstallexe}"

[Run]
Filename: "{app}\BamboozClipStudio.exe"; Description: "Launch Bambooz Clip Studio"; \
          Flags: nowait postinstall skipifsilent
```

## Build Pipeline

1. Publish the WPF app first (self-contained, single-file or folder):
   ```bash
   dotnet publish TrimClipEmbedToDiscord/TrimClipEmbedToDiscord.csproj \
     -c Release -r win-x64 --self-contained true \
     -p:PublishSingleFile=false
   ```
   Single-file publish is NOT recommended because FFME needs native libs loose on disk.

2. Run Inno Setup compiler:
   ```bash
   iscc /DAppVersion=1.0.0 Installer/BamboozClipStudio.iss
   ```

3. Upload `Installer/Output/BamboozClipStudioSetup-1.0.0.exe` as the GitHub Release asset.

## FFmpeg Bundling Decision

FFmpeg is **not** bundled in the installer (would add ~100 MB). Instead, `FFmpegService.EnsureFfmpegAvailable()` auto-downloads it to `%LocalAppData%\BamboozClipStudio\ffmpeg\` on first launch (same pattern as the absorbed CatBoxModeUploader).

A first-launch progress dialog shows "Downloading FFmpeg…" so the user knows what's happening.

## Uninstall

Inno Setup generates an uninstaller automatically. The uninstaller should also clean up:
- `%AppData%\BamboozClipStudio\` (settings) — ask user or always delete
- `%LocalAppData%\BamboozClipStudio\` (FFmpeg cache) — always delete
- Context menu registry keys if they were registered

Handle cleanup in an `[UninstallRun]` section or via a `{code}` block.

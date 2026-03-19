# Auto-Update Rules

## GitHub Releases

Updates are distributed via GitHub Releases on the app's repository. Each release has a tag like `v1.2.3`. The latest release's `tag_name` is fetched from the GitHub API to compare against the running version.

API endpoint (no auth needed for public repos):
```
GET https://api.github.com/repos/xBambooz/ClipStudio/releases/latest
```
Parse `tag_name` from the JSON response. Compare against `Assembly.GetExecutingAssembly().GetName().Version`.

## UpdateService

```csharp
public class UpdateService
{
    Task<UpdateCheckResult> CheckForUpdateAsync();
    Task DownloadAndLaunchInstallerAsync(string downloadUrl, IProgress<double> progress);
}

public record UpdateCheckResult(bool UpdateAvailable, Version? LatestVersion, string? InstallerUrl);
```

The installer URL comes from the release's `assets[].browser_download_url` where the asset name ends in `.exe`.

## Toolbar Options

Two separate items in the toolbar/menu:

1. **"Check for updates on startup"** — Toggle (checkbox menu item). Persisted in `settings.json` → `AutoCheckUpdates` (default: `true`). When enabled, `UpdateService.CheckForUpdateAsync()` is called in the background on app launch (non-blocking — never delays startup).

2. **"Check for updates"** — A manual button always visible in the toolbar/menu. Triggers the same check on demand and shows the result in a small dialog.

## Update Flow

1. `UpdateService` detects a newer version.
2. Show a non-intrusive banner or dialog: "Bambooz Clip Studio v{new} is available. Update now?"
3. User clicks Update → `DownloadAndLaunchInstallerAsync` downloads the installer `.exe` to `%Temp%`, shows a progress bar, then launches it via `Process.Start` and exits the app (`Application.Current.Shutdown()`).
4. The installer runs, replaces the old install, optionally restarts the app.

## Version Format

Use standard `Major.Minor.Patch` (e.g., `1.0.0`). Set in the `.csproj`:
```xml
<Version>1.0.0</Version>
<AssemblyVersion>1.0.0.0</AssemblyVersion>
```
GitHub release tag must match: `v1.0.0`.

## HTTP Setup

Use `HttpClient` with a `User-Agent` header (GitHub API requires it):
```csharp
client.DefaultRequestHeaders.UserAgent.ParseAdd("BamboozClipStudio/1.0");
```
Timeout: 10 seconds for the version check. Silently swallow network errors for the auto-check path (never crash on update check failure).

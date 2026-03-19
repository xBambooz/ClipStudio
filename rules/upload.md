# Upload Rules

## Pipeline (absorbed from CatBoxModeUploader)

1. **Fast-start remux** — `ffmpeg -c copy -movflags +faststart` to temp file (improves streaming)
2. **Dimension detection** — parse MP4 `tkhd` atom via `Mp4Parser.GetDimensions()` or read from FFmpeg probe
3. **Upload to CatBox** — multipart POST to `https://catbox.moe/user/api.php`
4. **Preview offset** — detect black frames via `blackdetect` filter to pick a non-black thumbnail second
5. **Embed URL** — `https://x266.mov/e/{catboxUrl}?i={previewSecond}&w={width}&h={height}`
6. **Clipboard** — copy final embed URL; show it in the progress dialog

## UploadService

```csharp
public class UploadService
{
    // Returns the final x266 embed URL, or throws on failure
    Task<string> UploadAndEmbedAsync(string filePath, IProgress<UploadProgress> progress, CancellationToken ct);
}

public record UploadProgress(UploadStage Stage, double Percent, string Message);

public enum UploadStage { Remuxing, Analyzing, Uploading, BuildingLink, Done }
```

## HTTP Client Setup

Force TLS 1.2/1.3. Disable `ExpectContinue`. Infinite timeout. Browser User-Agent header. Use `ProgressableStreamContent` to report bytes sent.

```csharp
ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
client.DefaultRequestHeaders.ExpectContinue = false;
client.Timeout = Timeout.InfiniteTimeSpan;
```

## ProgressableStreamContent

Wraps a `Stream` and fires `Action<long bytesSent, long totalBytes>` after each buffer write. Buffer size: 8192 bytes. Implements `TryComputeLength` by returning `_content.Length`.

## Upload Progress Dialog

A modal `UploadProgressDialog` window shows:
- Current stage label (e.g. "Uploading to catbox.moe…")
- A progress bar (indeterminate for remux/analyze stages, determinate for upload)
- Final embed URL in a read-only TextBox once done
- "Link copied!" confirmation text
- Cancel button (cancels via `CancellationToken`)

## File Size Limit

CatBox limit: 200 MB. Check before upload; show error if exceeded.

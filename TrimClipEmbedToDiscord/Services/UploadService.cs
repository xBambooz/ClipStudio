using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using BamboozClipStudio.Helpers;

namespace BamboozClipStudio.Services;

// ─── Progress types ──────────────────────────────────────────────────────────

public enum UploadStage
{
    Remuxing,
    Analyzing,
    Uploading,
    Done
}

public record UploadProgress(UploadStage Stage, int Percent, string Message);

// ─── Service ─────────────────────────────────────────────────────────────────

public class UploadService
{
    private readonly FFmpegService _ffmpeg;
    private static readonly HttpClient _http = CreateHttpClient();

    public UploadService(FFmpegService ffmpegService)
    {
        _ffmpeg = ffmpegService;
    }

    private static HttpClient CreateHttpClient()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

        var handler = new HttpClientHandler
        {
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        var client = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        client.DefaultRequestHeaders.ExpectContinue = false;
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");

        return client;
    }

    /// <summary>
    /// Remuxes the file for fast-start, uploads to catbox.moe, and returns
    /// the x266.mov embed URL.
    /// </summary>
    public async Task<string> UploadAndEmbedAsync(
        string filePath,
        IProgress<UploadProgress> progress,
        CancellationToken ct)
    {
        string tempFile = Path.Combine(Path.GetTempPath(),
            $"bambooz_upload_{Guid.NewGuid():N}.mp4");

        try
        {
            // Step 1 — Remux for fast-start.
            progress.Report(new UploadProgress(UploadStage.Remuxing, 0, "Optimizing for streaming..."));
            await _ffmpeg.RemuxFastStartAsync(filePath, tempFile).ConfigureAwait(false);

            // Step 2 — Check file size.
            var fileInfo = new FileInfo(tempFile);
            const long MaxBytes = 200L * 1024 * 1024; // 200 MB
            if (fileInfo.Length > MaxBytes)
            {
                throw new InvalidOperationException(
                    $"File is {fileInfo.Length / (1024 * 1024):F1} MB, which exceeds the 200 MB upload limit.");
            }

            // Step 3 — Analyze.
            progress.Report(new UploadProgress(UploadStage.Analyzing, 0, "Analyzing video..."));

            var (w, h) = Mp4Parser.GetDimensions(tempFile);
            if (w == 0 && h == 0)
            {
                w = 1920;
                h = 1080;
            }

            // Step 4 — Determine preview thumbnail offset.
            progress.Report(new UploadProgress(UploadStage.Analyzing, 50, "Selecting thumbnail frame..."));
            int previewSecond = await _ffmpeg.GetBlackdetectOffsetAsync(tempFile).ConfigureAwait(false);

            // Step 5 — Upload to catbox.moe.
            progress.Report(new UploadProgress(UploadStage.Uploading, 0, "Uploading to catbox.moe..."));

            string catboxUrl = await UploadToCatboxAsync(tempFile, fileInfo.Length, progress, ct)
                .ConfigureAwait(false);

            // Step 6 — Build x266 embed URL.
            string embedUrl = $"https://x266.mov/e/{catboxUrl}?i={previewSecond}&w={w}&h={h}";

            // Step 7 — Done.
            progress.Report(new UploadProgress(UploadStage.Done, 100, "Done!"));

            return embedUrl;
        }
        finally
        {
            // Always clean up the temp remux file.
            try
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
            catch { /* best-effort */ }
        }
    }

    private async Task<string> UploadToCatboxAsync(
        string filePath,
        long fileSize,
        IProgress<UploadProgress> progress,
        CancellationToken ct)
    {
        await using var fileStream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);

        var progressContent = new ProgressableStreamContent(fileStream, (sent, total) =>
        {
            int pct = total > 0 ? (int)(sent * 100L / total) : 0;
            progress.Report(new UploadProgress(UploadStage.Uploading, pct,
                $"Uploading to catbox.moe... {pct}%"));
        }, bufferSize: 8192);

        progressContent.Headers.ContentType =
            new MediaTypeHeaderValue("application/octet-stream");

        string fileName = Path.GetFileName(filePath);
        string contentDisposition =
            $"form-data; name=\"fileToUpload\"; filename=\"{fileName}\"";

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("fileupload"), "reqtype");
        form.Add(progressContent, "fileToUpload", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post,
            "https://catbox.moe/user/api.php") { Content = form };

        using var response = await _http.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        body = body.Trim();

        if (!body.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unexpected catbox response: {body}");

        return body;
    }
}

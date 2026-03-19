using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BamboozClipStudio.Services;

public record UpdateCheckResult(
    bool UpdateAvailable,
    Version? LatestVersion,
    string? InstallerUrl,
    string? ReleaseNotes);

public class UpdateService
{
    private const string ApiUrl =
        "https://api.github.com/repos/xBambooz/ClipStudio/releases/latest";

    private static readonly HttpClient _http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("BamboozClipStudio/1.0");
        client.Timeout = TimeSpan.FromSeconds(10);
        return client;
    }

    /// <summary>
    /// Checks the GitHub Releases API for a newer version.
    /// Returns a result with UpdateAvailable=false on any error.
    /// </summary>
    public async Task<UpdateCheckResult> CheckForUpdateAsync()
    {
        try
        {
            using var response = await _http.GetAsync(ApiUrl).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // tag_name e.g. "v1.2.3" → strip leading 'v'.
            string tagName = root.TryGetProperty("tag_name", out var tag)
                ? tag.GetString() ?? "" : "";
            string versionStr = tagName.TrimStart('v', 'V');

            if (!Version.TryParse(versionStr, out var latestVersion))
                return new UpdateCheckResult(false, null, null, null);

            // Release notes.
            string? releaseNotes = null;
            if (root.TryGetProperty("body", out var body))
                releaseNotes = body.GetString();

            // Find .exe installer asset.
            string? installerUrl = null;
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (asset.TryGetProperty("browser_download_url", out var urlProp))
                    {
                        string? url = urlProp.GetString();
                        if (url != null && url.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            installerUrl = url;
                            break;
                        }
                    }
                }
            }

            // Compare against running assembly version.
            Version? currentVersion = Assembly.GetExecutingAssembly().GetName().Version;

            bool updateAvailable = currentVersion != null && latestVersion > currentVersion;

            return new UpdateCheckResult(updateAvailable, latestVersion, installerUrl, releaseNotes);
        }
        catch
        {
            return new UpdateCheckResult(false, null, null, null);
        }
    }

    /// <summary>
    /// Downloads the installer to %Temp% with progress reporting and returns the local path.
    /// </summary>
    public async Task<string> DownloadInstallerAsync(
        string downloadUrl,
        IProgress<double> progress,
        CancellationToken ct)
    {
        string fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
        string tempPath = Path.Combine(Path.GetTempPath(), fileName);

        // Use a separate client without the 10s timeout for the download.
        using var downloadClient = new HttpClient();
        downloadClient.DefaultRequestHeaders.UserAgent.ParseAdd("BamboozClipStudio/1.0");
        downloadClient.Timeout = Timeout.InfiniteTimeSpan;

        using var response = await downloadClient
            .GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;

        await using var srcStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var destStream = new FileStream(
            tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

        var buffer = new byte[81920];
        long bytesCopied = 0;
        int bytesRead;

        while ((bytesRead = await srcStream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
        {
            await destStream.WriteAsync(buffer, 0, bytesRead, ct).ConfigureAwait(false);
            bytesCopied += bytesRead;

            if (totalBytes.HasValue && totalBytes.Value > 0)
                progress.Report((double)bytesCopied / totalBytes.Value * 100.0);
        }

        progress.Report(100.0);
        return tempPath;
    }
}

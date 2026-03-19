using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using BamboozClipStudio.Models;

namespace BamboozClipStudio.Services;

public class FFmpegService
{
    private static string _ffmpegExe = "ffmpeg";
    private static string _ffprobeExe = "ffprobe";

    private static readonly string LocalFfmpegDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "BamboozClipStudio", "ffmpeg");

    private static readonly string LocalFfmpegExe =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "BamboozClipStudio", "ffmpeg", "ffmpeg.exe");

    private static readonly string LocalFfprobeExe =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "BamboozClipStudio", "ffmpeg", "ffprobe.exe");

    private const string FfmpegDownloadUrl =
        "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

    // ─────────────────────────────────────────────────────────────────────────
    // Ensure ffmpeg is available
    // ─────────────────────────────────────────────────────────────────────────

    public async Task EnsureFfmpegAvailableAsync(IProgress<string>? progress)
    {
        // 1. Try PATH first.
        if (IsOnPath("ffmpeg") && IsOnPath("ffprobe"))
        {
            _ffmpegExe = "ffmpeg";
            _ffprobeExe = "ffprobe";
            return;
        }

        // 2. Try local AppData install.
        if (File.Exists(LocalFfmpegExe) && File.Exists(LocalFfprobeExe))
        {
            _ffmpegExe = LocalFfmpegExe;
            _ffprobeExe = LocalFfprobeExe;
            return;
        }

        // 3. Download.
        progress?.Report("FFmpeg not found. Downloading FFmpeg (this is a one-time setup)...");

        Directory.CreateDirectory(LocalFfmpegDir);
        string tempZip = Path.Combine(Path.GetTempPath(), "ffmpeg-release-essentials.zip");

        using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
        {
            http.DefaultRequestHeaders.UserAgent.ParseAdd("BamboozClipStudio/1.0");
            progress?.Report("Downloading FFmpeg release package...");
            var data = await http.GetByteArrayAsync(FfmpegDownloadUrl).ConfigureAwait(false);
            await File.WriteAllBytesAsync(tempZip, data).ConfigureAwait(false);
        }

        progress?.Report("Extracting FFmpeg...");
        string extractDir = Path.Combine(Path.GetTempPath(), "ffmpeg-extract-" + Guid.NewGuid().ToString("N"));
        ZipFile.ExtractToDirectory(tempZip, extractDir, overwriteFiles: true);

        // The zip contains a single top-level folder, then bin/ inside it.
        string ffmpegInZip = FindFileInDirectory(extractDir, "ffmpeg.exe")
            ?? throw new FileNotFoundException("ffmpeg.exe not found inside downloaded zip.");
        string ffprobeInZip = FindFileInDirectory(extractDir, "ffprobe.exe")
            ?? throw new FileNotFoundException("ffprobe.exe not found inside downloaded zip.");

        File.Copy(ffmpegInZip, LocalFfmpegExe, overwrite: true);
        File.Copy(ffprobeInZip, LocalFfprobeExe, overwrite: true);

        // Cleanup temp files.
        try
        {
            File.Delete(tempZip);
            Directory.Delete(extractDir, recursive: true);
        }
        catch { /* best-effort cleanup */ }

        _ffmpegExe = LocalFfmpegExe;
        _ffprobeExe = LocalFfprobeExe;
        progress?.Report("FFmpeg installed successfully.");
    }

    private static bool IsOnPath(string executable)
    {
        try
        {
            var psi = new ProcessStartInfo(executable, "-version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(3000);
            return p?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindFileInDirectory(string root, string fileName)
    {
        foreach (var f in Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories))
            return f;
        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Media info
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ClipProject> GetMediaInfoAsync(string filePath)
    {
        var args = $"-v quiet -print_format json -show_streams -show_format \"{filePath}\"";
        var (stdout, _) = await RunProcessAsync(_ffprobeExe, args).ConfigureAwait(false);

        using var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;

        double durationSeconds = 0;
        int width = 0, height = 0;
        double frameRate = 0;
        int audioTrackCount = 0;

        // Parse format duration as fallback.
        if (root.TryGetProperty("format", out var fmt) &&
            fmt.TryGetProperty("duration", out var fmtDur) &&
            double.TryParse(fmtDur.GetString(), System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out double fd))
        {
            durationSeconds = fd;
        }

        if (root.TryGetProperty("streams", out var streams))
        {
            foreach (var stream in streams.EnumerateArray())
            {
                string codecType = stream.TryGetProperty("codec_type", out var ct)
                    ? ct.GetString() ?? "" : "";

                if (codecType == "video" && width == 0)
                {
                    if (stream.TryGetProperty("width", out var w)) width = w.GetInt32();
                    if (stream.TryGetProperty("height", out var h)) height = h.GetInt32();

                    // Parse frame rate from "r_frame_rate" = "30000/1001" style string.
                    if (stream.TryGetProperty("r_frame_rate", out var rfr))
                    {
                        frameRate = ParseFraction(rfr.GetString());
                    }

                    // Override duration from stream if available.
                    if (stream.TryGetProperty("duration", out var dur) &&
                        double.TryParse(dur.GetString(), System.Globalization.NumberStyles.Any,
                                        System.Globalization.CultureInfo.InvariantCulture, out double sd))
                    {
                        durationSeconds = sd;
                    }
                }

                if (codecType == "audio")
                    audioTrackCount++;
            }
        }

        return new ClipProject
        {
            FilePath = filePath,
            Duration = TimeSpan.FromSeconds(durationSeconds),
            Width = width,
            Height = height,
            FrameRate = frameRate,
            AudioTrackCount = audioTrackCount
        };
    }

    private static double ParseFraction(string? fraction)
    {
        if (string.IsNullOrEmpty(fraction)) return 0;
        var parts = fraction.Split('/');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out double num) &&
            double.TryParse(parts[1], System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out double den) &&
            den != 0)
        {
            return num / den;
        }
        return 0;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Extract single frame
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<BitmapImage> ExtractFrameAsync(string filePath, TimeSpan position, string? vfChain = null)
    {
        string posStr = FormatTimeSpan(position);
        string vfArg = string.IsNullOrEmpty(vfChain) ? "" : $"-vf \"{vfChain}\" ";
        string args = $"-y -hide_banner -ss {posStr} -i \"{filePath}\" {vfArg}-frames:v 1 -f image2pipe -vcodec png pipe:1";

        var psi = new ProcessStartInfo(_ffmpegExe, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var ms = new MemoryStream();
        await process.StandardOutput.BaseStream.CopyToAsync(ms).ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);

        ms.Seek(0, SeekOrigin.Begin);

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = ms;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Generate thumbnails
    // ─────────────────────────────────────────────────────────────────────────

    public async Task GenerateThumbnailsAsync(
        string filePath,
        int count,
        Action<int, BitmapImage> onThumbnailReady,
        CancellationToken ct)
    {
        // Need duration to space thumbnails.
        var info = await GetMediaInfoAsync(filePath).ConfigureAwait(false);
        double totalSeconds = info.Duration.TotalSeconds;
        if (totalSeconds <= 0) totalSeconds = 1;

        // Compute evenly-spaced positions (avoid exact start/end).
        var positions = new TimeSpan[count];
        for (int i = 0; i < count; i++)
        {
            double t = totalSeconds * (i + 0.5) / count;
            positions[i] = TimeSpan.FromSeconds(t);
        }

        int degreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2);

        await Task.Run(() =>
        {
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = degreeOfParallelism,
                CancellationToken = ct
            };

            Parallel.For(0, count, options, i =>
            {
                ct.ThrowIfCancellationRequested();

                // Run synchronously on thread-pool thread.
                var bitmap = ExtractFrameAsync(filePath, positions[i]).GetAwaiter().GetResult();
                onThumbnailReady(i, bitmap);
            });
        }, ct).ConfigureAwait(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Build encode ProcessStartInfo
    // ─────────────────────────────────────────────────────────────────────────

    public ProcessStartInfo BuildEncodeProcessStartInfo(
        ExportSettings settings,
        TimeSpan inPoint,
        TimeSpan outPoint,
        string inputPath,
        string outputPath,
        int audioTrackCount,
        string? vfChain = null)
    {
        var args = new System.Text.StringBuilder();

        args.Append($"-y -hide_banner");
        args.Append($" -ss {FormatTimeSpan(inPoint)}");
        args.Append($" -to {FormatTimeSpan(outPoint)}");
        args.Append($" -i \"{inputPath}\"");
        args.Append($" -c:v {settings.VideoCodec}");
        args.Append($" -preset {settings.Preset}");
        args.Append($" -profile:v {settings.Profile}");

        // Audio mapping.
        if (settings.MixAudioTracks && audioTrackCount > 1)
        {
            args.Append($" -filter_complex \"[0:a]amerge=inputs={audioTrackCount}[aout]\"");
            args.Append(" -map 0:v -map \"[aout]\"");
        }
        else
        {
            args.Append(" -map 0:v -map 0:a");
        }

        // Video filter chain (from FiltersPanel)
        if (!string.IsNullOrEmpty(vfChain))
            args.Append($" -vf \"{vfChain}\"");

        args.Append($" -c:a {settings.AudioCodec}");
        args.Append(" -b:a 192k -ac 2 -threads 0");

        // Rate control.
        if (settings.BitrateMode == BitrateMode.Crf)
            args.Append($" -crf {settings.Crf}");
        else
            args.Append($" -b:v {settings.Bitrate}k");

        args.Append($" -movflags +faststart \"{outputPath}\"");

        return new ProcessStartInfo(_ffmpegExe, args.ToString())
        {
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Encode
    // ─────────────────────────────────────────────────────────────────────────

    public async Task EncodeAsync(
        ExportSettings settings,
        string? vfChain,
        TimeSpan inPoint,
        TimeSpan outPoint,
        string inputPath,
        string outputPath,
        int audioTrackCount,
        TimeSpan duration,
        IProgress<double> progress,
        CancellationToken ct)
    {
        var psi = BuildEncodeProcessStartInfo(settings, inPoint, outPoint, inputPath, outputPath, audioTrackCount, vfChain);

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.Start();

        // Regex to parse time= from ffmpeg stderr.
        var timeRegex = new Regex(@"time=(\d+):(\d+):(\d+\.\d+)", RegexOptions.Compiled);
        double totalSeconds = duration.TotalSeconds;

        var readTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardError.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                var match = timeRegex.Match(line);
                if (match.Success)
                {
                    int hours = int.Parse(match.Groups[1].Value);
                    int minutes = int.Parse(match.Groups[2].Value);
                    double seconds = double.Parse(match.Groups[3].Value,
                        System.Globalization.CultureInfo.InvariantCulture);
                    double encoded = hours * 3600 + minutes * 60 + seconds;
                    double pct = totalSeconds > 0 ? Math.Min(100.0, encoded / totalSeconds * 100.0) : 0;
                    progress?.Report(pct);
                }
            }
        });

        // Poll for cancellation.
        var cancelTask = Task.Run(async () =>
        {
            while (!process.HasExited)
            {
                if (ct.IsCancellationRequested)
                {
                    try { process.Kill(entireProcessTree: true); } catch { }
                    return;
                }
                await Task.Delay(200, CancellationToken.None).ConfigureAwait(false);
            }
        });

        await readTask.ConfigureAwait(false);
        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

        ct.ThrowIfCancellationRequested();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"FFmpeg exited with code {process.ExitCode}. Check the output path and settings.");

        progress?.Report(100.0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Count audio tracks
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<int> CountAudioTracksAsync(string filePath)
    {
        var args = $"-v quiet -select_streams a -show_entries stream=index -of csv=p=0 \"{filePath}\"";
        var (stdout, _) = await RunProcessAsync(_ffprobeExe, args).ConfigureAwait(false);
        int count = 0;
        foreach (var line in stdout.Split('\n'))
        {
            if (!string.IsNullOrWhiteSpace(line)) count++;
        }
        return count;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fast-start remux (used by UploadService)
    // ─────────────────────────────────────────────────────────────────────────

    public async Task RemuxFastStartAsync(string inputPath, string outputPath)
    {
        string args = $"-y -hide_banner -i \"{inputPath}\" -c copy -movflags +faststart \"{outputPath}\"";
        var (_, stderr) = await RunProcessAsync(_ffmpegExe, args).ConfigureAwait(false);
        // The process ran; output path will exist if successful.
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Blackdetect (used by UploadService)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs ffmpeg blackdetect on the first 8 seconds of the file and returns
    /// the first non-black second (clamped 1–8). Returns 1 if no black detected.
    /// </summary>
    public async Task<int> GetBlackdetectOffsetAsync(string filePath)
    {
        string args = $"-hide_banner -loglevel info -t 8 -i \"{filePath}\" -vf \"blackdetect=d=0.1:pix_th=0.10\" -an -f null -";
        var (_, stderr) = await RunProcessAsync(_ffmpegExe, args).ConfigureAwait(false);

        var blackEndRegex = new Regex(@"black_end:(\d+(?:\.\d+)?)", RegexOptions.Compiled);
        double latestBlackEnd = 0;
        foreach (Match m in blackEndRegex.Matches(stderr))
        {
            if (double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out double be))
            {
                latestBlackEnd = Math.Max(latestBlackEnd, be);
            }
        }

        int offset = (int)Math.Ceiling(latestBlackEnd) + 1;
        return Math.Clamp(offset, 1, 8);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static string FormatTimeSpan(TimeSpan ts)
    {
        return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
    }

    private static async Task<(string stdout, string stderr)> RunProcessAsync(string exe, string args)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync().ConfigureAwait(false);

        string stdout = await stdoutTask.ConfigureAwait(false);
        string stderr = await stderrTask.ConfigureAwait(false);

        return (stdout, stderr);
    }
}

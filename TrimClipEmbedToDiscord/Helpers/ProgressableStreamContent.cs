using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace BamboozClipStudio.Helpers;

/// <summary>
/// An <see cref="HttpContent"/> implementation that wraps a <see cref="Stream"/> and reports
/// upload progress via a callback after every buffer flush.
/// </summary>
public class ProgressableStreamContent : HttpContent
{
    private readonly Stream _content;
    private readonly int _bufferSize;
    private readonly Action<long, long> _progress;

    /// <summary>
    /// Creates a new instance of <see cref="ProgressableStreamContent"/>.
    /// </summary>
    /// <param name="content">The stream to send as the request body.</param>
    /// <param name="progress">
    /// Callback invoked after each buffer write with (bytesSent, totalBytes).
    /// <c>totalBytes</c> is -1 if the stream length is not known.
    /// </param>
    /// <param name="bufferSize">Size of the copy buffer in bytes (default 8192).</param>
    public ProgressableStreamContent(Stream content, Action<long, long> progress, int bufferSize = 8192)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        _bufferSize = bufferSize > 0 ? bufferSize : throw new ArgumentOutOfRangeException(nameof(bufferSize));
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        long totalBytes = -1;
        try { totalBytes = _content.Length; } catch { /* length unavailable */ }

        var buffer = new byte[_bufferSize];
        long bytesSent = 0;

        // Reset to the start in case the stream was partially read before this call.
        if (_content.CanSeek)
            _content.Seek(0, SeekOrigin.Begin);

        int bytesRead;
        while ((bytesRead = await _content.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
        {
            await stream.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);
            bytesSent += bytesRead;
            _progress(bytesSent, totalBytes);
        }
    }

    protected override bool TryComputeLength(out long length)
    {
        if (_content.CanSeek)
        {
            length = _content.Length;
            return true;
        }

        length = -1;
        return false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _content.Dispose();
        base.Dispose(disposing);
    }
}

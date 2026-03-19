using BamboozClipStudio.Core;
using BamboozClipStudio.Services;
using System.Windows;

namespace BamboozClipStudio.ViewModels;

public class UploadProgressViewModel : ObservableObject
{
    readonly UploadService _upload;
    CancellationTokenSource _cts = new();

    UploadStage _stage = UploadStage.Remuxing;
    double _progressPercent;
    string _statusMessage = "Starting…";
    string _embedUrl = string.Empty;
    bool _isDone;
    bool _isError;
    bool _urlCopied;

    public UploadStage Stage       { get => _stage;           private set => SetProperty(ref _stage,           value); }
    public double ProgressPercent  { get => _progressPercent; private set => SetProperty(ref _progressPercent, value); }
    public string StatusMessage    { get => _statusMessage;   private set => SetProperty(ref _statusMessage,   value); }
    public string EmbedUrl         { get => _embedUrl;        private set => SetProperty(ref _embedUrl,        value); }
    public bool IsDone             { get => _isDone;          private set => SetProperty(ref _isDone,          value); }
    public bool IsError            { get => _isError;         private set => SetProperty(ref _isError,         value); }
    public bool UrlCopied          { get => _urlCopied;       private set => SetProperty(ref _urlCopied,       value); }
    public bool IsIndeterminate    => Stage != UploadStage.Uploading && !IsDone && !IsError;

    public RelayCommand CancelCommand { get; }
    public RelayCommand CopyUrlCommand { get; }

    public Action? CloseRequested { get; set; }

    public UploadProgressViewModel(UploadService upload)
    {
        _upload = upload;
        CancelCommand = new RelayCommand(() => _cts.Cancel(), () => !IsDone && !IsError);
        CopyUrlCommand = new RelayCommand(CopyUrl, () => !string.IsNullOrEmpty(EmbedUrl));
    }

    public async Task StartUploadAsync(string filePath)
    {
        _cts = new CancellationTokenSource();

        var progress = new Progress<UploadProgress>(p =>
        {
            Stage = p.Stage;
            ProgressPercent = p.Percent;
            StatusMessage = p.Message;
        });

        try
        {
            var url = await _upload.UploadAndEmbedAsync(filePath, progress, _cts.Token);
            EmbedUrl = url;
            IsDone = true;
            StatusMessage = "Link ready!";
            CopyUrl();
        }
        catch (OperationCanceledException)
        {
            IsError = true;
            StatusMessage = "Upload cancelled.";
        }
        catch (Exception ex)
        {
            IsError = true;
            StatusMessage = $"Upload failed: {ex.Message}";
        }
    }

    void CopyUrl()
    {
        if (string.IsNullOrEmpty(EmbedUrl)) return;
        try
        {
            Clipboard.SetText(EmbedUrl);
            UrlCopied = true;
        }
        catch { /* clipboard unavailable */ }
    }
}

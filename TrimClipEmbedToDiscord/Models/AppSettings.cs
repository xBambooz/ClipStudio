namespace BamboozClipStudio.Models;

public class AppSettings
{
    public string? ThemeOverride { get; set; }
    public string LastOutputFolder { get; set; } =
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    public bool EmbedUpload { get; set; } = false;
    public bool MixAudioTracks { get; set; } = true;
    public bool AutoCheckUpdates { get; set; } = true;
}

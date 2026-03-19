namespace BamboozClipStudio.Models;

public class ClipProject
{
    public string FilePath { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double FrameRate { get; set; } = 30.0;
    public int AudioTrackCount { get; set; } = 1;
}

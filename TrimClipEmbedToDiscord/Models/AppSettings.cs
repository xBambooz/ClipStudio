namespace BamboozClipStudio.Models;

public class AppSettings
{
    public string? ThemeOverride { get; set; }
    public string LastOpenFolder { get; set; } =
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
    public string LastOutputFolder { get; set; } =
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    public bool EmbedUpload { get; set; } = false;
    public bool MixAudioTracks { get; set; } = true;
    public bool AutoCheckUpdates { get; set; } = true;
    public bool HardwareAcceleration { get; set; } = true;
    public double Volume { get; set; } = 1.0;

    // Window position/size persistence (-1 = not set)
    public double WindowLeft { get; set; } = -1;
    public double WindowTop { get; set; } = -1;
    public double WindowWidth { get; set; } = 1100;
    public double WindowHeight { get; set; } = 720;
    public bool WindowMaximized { get; set; }

    // Persisted filter values
    public double FilterSaturation { get; set; } = 1.0;
    public double FilterVibrance { get; set; }
    public double FilterBrightness { get; set; }
    public double FilterContrast { get; set; } = 1.0;
    public double FilterSharpness { get; set; }
    public double FilterGamma { get; set; } = 1.0;

    // Startup/bootstrap state
    public bool HasCompletedInitialSetup { get; set; }
    public bool LastRunExitedCleanly { get; set; } = true;

    // Crash recovery snapshot
    public string? RecoveryMediaPath { get; set; }
    public double RecoveryInPointSeconds { get; set; }
    public double RecoveryOutPointSeconds { get; set; }
    public double RecoveryPositionSeconds { get; set; }
}

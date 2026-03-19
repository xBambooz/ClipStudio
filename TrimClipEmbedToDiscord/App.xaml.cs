using BamboozClipStudio.Models;
using BamboozClipStudio.Services;
using BamboozClipStudio.ViewModels;
using Microsoft.Win32;
using System.Windows;

namespace BamboozClipStudio;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ApplyWindowsTheme();

        var settings   = new SettingsService();
        var ffmpeg     = new FFmpegService();
        var filter     = new FilterService();
        var upload     = new UploadService(ffmpeg);
        var update     = new UpdateService();

        var vm = new MainViewModel(ffmpeg, filter, upload, settings, update);
        var window = new MainWindow(vm);
        MainWindow = window;
        window.Show();

        // Handle command-line file argument (from context menu)
        if (e.Args.Length > 0 && System.IO.File.Exists(e.Args[0]))
            _ = vm.LoadFileAsync(e.Args[0]);
    }

    static void ApplyWindowsTheme()
    {
        // Read Windows dark/light mode setting
        const string regKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        using var key = Registry.CurrentUser.OpenSubKey(regKey);
        bool isLight = key?.GetValue("AppsUseLightTheme") is int val && val == 1;

        // For now we always use our dark theme palette.
        // If isLight, a future update can swap UIColors to a light variant.
        _ = isLight; // reserved
    }
}

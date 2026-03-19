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

        var settings = new SettingsService();
        var appSettings = settings.Load();
        bool crashedPreviously = !appSettings.LastRunExitedCleanly;

        appSettings.LastRunExitedCleanly = false;
        settings.Save(appSettings);

        ApplyTheme(appSettings.ThemeOverride);

        var ffmpeg = new FFmpegService();
        var filter = new FilterService();
        var upload = new UploadService(ffmpeg);
        var update = new UpdateService();
        var jobs = new BackgroundJobService();

        var startupPath = e.Args.Length > 0 && System.IO.File.Exists(e.Args[0]) ? e.Args[0] : null;
        var vm = new MainViewModel(ffmpeg, filter, upload, settings, update, jobs);
        vm.ConfigureStartup(startupPath, crashedPreviously);
        var window = new MainWindow(vm, settings);
        MainWindow = window;
        window.Show();
    }

    public static void ApplyTheme(string? themeOverride)
    {
        string colorFile = themeOverride switch
        {
            "Dark"            => "Theme/UIColors.xaml",
            "Light"           => "Theme/UIColors-Light.xaml",
            "MintDark"        => "Theme/UIColors-MintDark.xaml",
            "RedDark"         => "Theme/UIColors-RedDark.xaml",
            "PremierePro"     => "Theme/UIColors-PremierePro.xaml",
            "OLED"            => "Theme/UIColors-OLED.xaml",
            "Discord"         => "Theme/UIColors-Discord.xaml",
            "TwilightBlurple" => "Theme/UIColors-TwilightBlurple.xaml",
            "YouTube"         => "Theme/UIColors-YouTube.xaml",
            _                 => IsWindowsDark() ? "Theme/UIColors.xaml" : "Theme/UIColors-Light.xaml"
        };

        var app = (App)Current;
        var dicts = app.Resources.MergedDictionaries;

        // Remove existing UIColors dictionary
        var existing = dicts.FirstOrDefault(d =>
            d.Source?.OriginalString?.Contains("UIColors") == true);
        if (existing != null) dicts.Remove(existing);

        // Insert the correct one at position 0 so styles that follow can reference its keys
        dicts.Insert(0, new ResourceDictionary
        {
            Source = new Uri(colorFile, UriKind.Relative)
        });
    }

    static bool IsWindowsDark()
    {
        const string regKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        using var key = Registry.CurrentUser.OpenSubKey(regKey);
        return key?.GetValue("AppsUseLightTheme") is not (int v and 1);
    }
}

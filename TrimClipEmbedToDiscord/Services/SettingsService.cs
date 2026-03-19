using System;
using System.IO;
using System.Text.Json;
using BamboozClipStudio.Models;

namespace BamboozClipStudio.Services;

public class SettingsService
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BamboozClipStudio");

    private static readonly string SettingsPath = Path.Combine(SettingsFolder, "settings.json");

    private static readonly JsonSerializerOptions LoadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions SaveOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Loads application settings from disk.
    /// Returns a default <see cref="AppSettings"/> instance if the file does not
    /// exist or cannot be deserialized.
    /// </summary>
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AppSettings();

            string json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, LoadOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    /// <summary>
    /// Saves application settings to disk, creating the settings directory if needed.
    /// </summary>
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsFolder);
        string json = JsonSerializer.Serialize(settings, SaveOptions);
        File.WriteAllText(SettingsPath, json);
    }
}

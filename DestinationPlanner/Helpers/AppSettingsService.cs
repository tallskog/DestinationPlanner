using System.IO;
using System.Text.Json;

namespace DestinationPlanner.Helpers;

public static class AppSettingsService
{
    private static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = true };

    private static string SettingsPath =>
        Path.Combine(AppDataHelper.AppDataPath, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath))
                       ?? new AppSettings();
        }
        catch { }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            File.WriteAllText(SettingsPath,
                JsonSerializer.Serialize(settings, _writeOptions));
        }
        catch { }
    }
}

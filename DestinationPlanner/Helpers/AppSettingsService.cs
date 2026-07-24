using System.IO;
using System.Text.Json;

namespace DestinationPlanner.Helpers;

public static class AppSettingsService
{
    private static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = true };

    // Test seam: when set, Save/Load use this path instead of the real AppData settings.json.
    // Without it, automated tests (which also run in the DEBUG configuration) would read/write
    // the same settings.json used by a real dev build, clobbering LastLogbookPath and filters.
    internal static string? TestOverridePath { get; set; }

    private static string SettingsPath =>
        TestOverridePath ?? Path.Combine(AppDataHelper.AppDataPath, "settings.json");

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

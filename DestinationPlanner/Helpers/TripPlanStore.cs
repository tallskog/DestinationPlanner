using System.IO;
using System.Text.Json;
using DestinationPlanner.Models;

namespace DestinationPlanner.Helpers;

// Persists AI-assisted trip plans (US41) to tripplans.local.json under AppDataHelper.AppDataPath.
// Global — never scoped to or referencing a specific logbook, so switching or importing a
// different logbook never affects saved plans. Mirrors AppSettingsService's tolerant
// missing/corrupt-file-returns-default discipline.
public static class TripPlanStore
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    // Test seam: when set, Load/Save use this path instead of the real AppData file — see
    // AppSettingsService.TestOverridePath and CLAUDE.md's BUG-06 rule.
    internal static string? TestOverridePath { get; set; }

    private static string FilePath =>
        TestOverridePath ?? Path.Combine(AppDataHelper.AppDataPath, "tripplans.local.json");

    public static List<TripPlan> LoadAll()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<List<TripPlan>>(File.ReadAllText(FilePath))
                       ?? [];
        }
        catch { }
        return [];
    }

    public static void SaveAll(IReadOnlyList<TripPlan> plans)
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(plans, WriteOptions));
        }
        catch { }
    }
}

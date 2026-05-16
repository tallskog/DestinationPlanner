using System.IO;

namespace DestinationPlanner.Helpers;

public static class AppDataHelper
{
    private static readonly string _appDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
#if DEBUG
        "DestinationPlanner-dev");
#else
        "DestinationPlanner");
#endif

    public static string AppDataPath => _appDataPath;

    public static void EnsureCreated()
        => Directory.CreateDirectory(_appDataPath);

    public static IReadOnlyList<string> GetLogbookFiles()
        => Directory.GetFiles(_appDataPath, "logbook*.xml")
                    .OrderBy(f => f)
                    .ToList();

    /// <summary>
    /// Returns a unique path for a new logbook: logbook-dd-mm-yyyy.xml,
    /// or logbook1-dd-mm-yyyy.xml, logbook2-… if the base name already exists.
    /// </summary>
    public static string GetNewLogbookPath(DateTime date)
    {
        string dateSuffix = $"{date:dd}-{date:MM}-{date:yyyy}";
        string candidate = Path.Combine(_appDataPath, $"logbook-{dateSuffix}.xml");
        if (!File.Exists(candidate)) return candidate;

        int n = 1;
        while (true)
        {
            candidate = Path.Combine(_appDataPath, $"logbook{n}-{dateSuffix}.xml");
            if (!File.Exists(candidate)) return candidate;
            n++;
        }
    }
}

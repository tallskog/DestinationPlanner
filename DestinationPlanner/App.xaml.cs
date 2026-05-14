using DestinationPlanner.Helpers;
using DestinationPlanner.Serialization;
using DestinationPlanner.Views;
using System.IO;
using System.Windows;
using Velopack;

namespace DestinationPlanner;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Must run before any windows open so Velopack can handle install/uninstall hooks.
        VelopackApp.Build().Run();

        base.OnStartup(e);

        // Prevent WPF from shutting down when the logbook-selection dialog closes
        // (it would be the only open window at that point under OnLastWindowClose).
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        AppDataHelper.EnsureCreated();
        string logbookPath = SelectOrCreateLogbook();

        ShutdownMode = ShutdownMode.OnMainWindowClose;
        var window = new MainWindow(logbookPath);
        MainWindow = window;
        window.Show();
    }

    private static string SelectOrCreateLogbook()
    {
        var files = AppDataHelper.GetLogbookFiles();

        if (files.Count == 0)
        {
            string path = AppDataHelper.GetNewLogbookPath(DateTime.Now);
            NativeLogbookSerializer.Save([], path);
            return path;
        }

        if (files.Count == 1)
            return files[0];

        // US15.3 – ask user which logbook to open
        var dlg = new LogbookSelectionDialog(files, "Multiple logbook files found. Select which one to use:");
        dlg.ShowDialog();
        return dlg.SelectedPath ?? files[0];
    }
}

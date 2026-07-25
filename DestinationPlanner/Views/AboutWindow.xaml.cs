using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace DestinationPlanner.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version is null ? "" : $"Version {version.Major}.{version.Minor}.{version.Build}";
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch { /* best-effort — link opening failures are non-fatal */ }
        e.Handled = true;
    }
}

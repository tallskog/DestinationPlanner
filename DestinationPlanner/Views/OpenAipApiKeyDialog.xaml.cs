using System.Windows;
using System.Windows.Navigation;

namespace DestinationPlanner.Views;

public partial class OpenAipApiKeyDialog : Window
{
    public string? ApiKey { get; private set; }

    public OpenAipApiKeyDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => ApiKeyBox.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string key = ApiKeyBox.Text.Trim();
        if (string.IsNullOrEmpty(key)) return;

        ApiKey = key;
        DialogResult = true;
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch { /* best-effort — link opening failures are non-fatal */ }
        e.Handled = true;
    }
}

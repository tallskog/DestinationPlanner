using System.IO;
using System.Windows;
using System.Windows.Input;

namespace DestinationPlanner.Views;

public partial class LogbookSelectionDialog : Window
{
    public string? SelectedPath { get; private set; }

    public LogbookSelectionDialog(IReadOnlyList<string> logbookPaths,
                                   string description = "Select logbook to open:")
    {
        InitializeComponent();
        DescriptionText.Text = description;
        // Show only file names in the list but map back to full paths on selection
        LogbookList.ItemsSource = logbookPaths.Select(p => new LogbookItem(p)).ToList();
        LogbookList.SelectedIndex = 0;
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => Confirm();

    private void LogbookList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Confirm();

    private void Confirm()
    {
        if (LogbookList.SelectedItem is LogbookItem item)
            SelectedPath = item.FullPath;
        DialogResult = true;
    }

    private sealed record LogbookItem(string FullPath)
    {
        public override string ToString() => Path.GetFileName(FullPath);
    }
}

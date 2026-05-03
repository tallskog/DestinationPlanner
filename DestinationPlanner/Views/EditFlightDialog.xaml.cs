using DestinationPlanner.ViewModels;
using System.Windows;

namespace DestinationPlanner.Views;

public partial class EditFlightDialog : Window
{
    public EditFlightViewModel ViewModel { get; }

    public EditFlightDialog(EditFlightViewModel vm)
    {
        InitializeComponent();
        ViewModel = vm;
        DataContext = vm;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsValid) return;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
        => DialogResult = false;
}

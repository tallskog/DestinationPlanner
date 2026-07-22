using DestinationPlanner.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace DestinationPlanner.Views;

public partial class NavigraphSignInDialog : Window
{
    public NavigraphSignInViewModel ViewModel { get; }

    public NavigraphSignInDialog(NavigraphSignInViewModel vm)
    {
        InitializeComponent();
        ViewModel = vm;
        DataContext = vm;

        vm.PropertyChanged += ViewModel_PropertyChanged;
        vm.Completed += ViewModel_Completed;
        Loaded += (_, _) => _ = vm.StartAsync();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ViewModel.State)) return;

        StartingPanel.Visibility = ViewModel.State == NavigraphSignInState.Starting
            ? Visibility.Visible : Visibility.Collapsed;
        WaitingPanel.Visibility = ViewModel.State == NavigraphSignInState.WaitingForUser
            ? Visibility.Visible : Visibility.Collapsed;
        ErrorPanel.Visibility = ViewModel.State is NavigraphSignInState.Denied
            or NavigraphSignInState.Expired or NavigraphSignInState.Error
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ViewModel_Completed(object? sender, EventArgs e)
    {
        // Success/Cancelled close immediately; Denied/Expired/Error stay open showing
        // ErrorPanel (see ViewModel_PropertyChanged) until the user clicks Close.
        if (ViewModel.State is NavigraphSignInState.Success or NavigraphSignInState.Cancelled)
        {
            Dispatcher.Invoke(() =>
            {
                DialogResult = ViewModel.State == NavigraphSignInState.Success;
                Close();
            });
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

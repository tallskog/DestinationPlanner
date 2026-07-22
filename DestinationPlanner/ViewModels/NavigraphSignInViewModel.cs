using DestinationPlanner.Helpers;
using DestinationPlanner.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace DestinationPlanner.ViewModels;

public enum NavigraphSignInState { Starting, WaitingForUser, Success, Denied, Expired, Error, Cancelled }

public class NavigraphSignInViewModel : ViewModelBase
{
    private readonly INavigraphAuthService _auth;
    private readonly CancellationTokenSource _cts = new();

    private NavigraphSignInState _state = NavigraphSignInState.Starting;
    private string _userCode = string.Empty;
    private string _verificationUri = string.Empty;
    private string? _verificationUriComplete;
    private string _errorMessage = string.Empty;

    public NavigraphSignInState State { get => _state; private set => SetField(ref _state, value); }
    public string UserCode { get => _userCode; private set => SetField(ref _userCode, value); }
    public string VerificationUri { get => _verificationUri; private set => SetField(ref _verificationUri, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetField(ref _errorMessage, value); }

    // Populated once State reaches Success.
    public NavigraphTokenResult? Result { get; private set; }

    public ICommand OpenBrowserCommand { get; }
    public ICommand CopyCodeCommand { get; }
    public ICommand CancelCommand { get; }

    // Fired exactly once, when a terminal state (Success/Denied/Expired/Error/Cancelled) is reached.
    public event EventHandler? Completed;

    public NavigraphSignInViewModel(INavigraphAuthService auth)
    {
        _auth = auth;
        OpenBrowserCommand = new RelayCommand(OpenBrowser);
        CopyCodeCommand = new RelayCommand(CopyCode);
        CancelCommand = new RelayCommand(() => _cts.Cancel());
    }

    public async Task StartAsync()
    {
        try
        {
            var authorization = await _auth.StartDeviceAuthorizationAsync(_cts.Token);
            UserCode = authorization.UserCode;
            VerificationUri = authorization.VerificationUri;
            _verificationUriComplete = authorization.VerificationUriComplete;
            State = NavigraphSignInState.WaitingForUser;

            Result = await _auth.PollForTokenAsync(authorization, _cts.Token);
            State = NavigraphSignInState.Success;
        }
        catch (OperationCanceledException)
        {
            State = NavigraphSignInState.Cancelled;
        }
        catch (NavigraphAuthException ex)
        {
            State = ex.Kind switch
            {
                NavigraphAuthErrorKind.AccessDenied => NavigraphSignInState.Denied,
                NavigraphAuthErrorKind.ExpiredToken => NavigraphSignInState.Expired,
                _ => NavigraphSignInState.Error,
            };
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            State = NavigraphSignInState.Error;
            ErrorMessage = ex.Message;
        }

        Completed?.Invoke(this, EventArgs.Empty);
    }

    private void OpenBrowser()
    {
        string url = _verificationUriComplete ?? _verificationUri;
        if (string.IsNullOrEmpty(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* best-effort — user can still navigate to VerificationUri manually */ }
    }

    private void CopyCode()
    {
        if (string.IsNullOrEmpty(_userCode)) return;
        try { Clipboard.SetText(_userCode); }
        catch { /* clipboard access can fail transiently — non-fatal */ }
    }
}

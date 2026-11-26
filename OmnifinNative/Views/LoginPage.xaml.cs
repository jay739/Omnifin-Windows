using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OmnifinNative.Services;

namespace OmnifinNative;

public sealed partial class LoginPage : Page
{
    private bool _loginInProgress;

    public LoginPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ErrorText.Visibility = Visibility.Collapsed;
        var savedServerUrl = ServerSettings.LoadServerUrl();
        if (savedServerUrl is not null)
        {
            ServerUrlBox.Text = savedServerUrl.ToString();
        }
        if (App.Auth.RestoreError is { } error)
        {
            ShowError(error);
        }
        SetLoginBusy(e.Parameter is true && App.Auth.HasSavedSession
            && !App.Auth.IsAuthenticated && App.Auth.RestoreError is null);
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void UpdateRecoveryOptions()
    {
        var hasSavedServer = ServerSettings.LoadServerUrl() is not null;
        RecoveryPanel.Visibility = hasSavedServer ? Visibility.Visible : Visibility.Collapsed;
        RetrySessionButton.IsEnabled = !_loginInProgress && hasSavedServer && App.Auth.HasSavedSession;
    }

    private void SetLoginBusy(bool busy)
    {
        _loginInProgress = busy;
        LoginButton.IsEnabled = !busy;
        ForgetServerButton.IsEnabled = !busy;
        ServerUrlBox.IsEnabled = !busy;
        UsernameBox.IsEnabled = !busy;
        PasswordBox.IsEnabled = !busy;
        AdminLoginCheckBox.IsEnabled = !busy;
        LoginProgress.IsActive = busy;
        UpdateRecoveryOptions();
    }

    private void OpenAccountPage()
    {
        Frame.Navigate(App.Auth.IsAdmin ? typeof(OmnifinNative.Views.MainPage) : typeof(OmnifinNative.Views.UserPage));
    }

    private async void RetrySessionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_loginInProgress)
        {
            return;
        }
        var savedServerUrl = ServerSettings.LoadServerUrl();
        if (savedServerUrl is null)
        {
            ShowError("Enter your Omnifin server URL and sign in.");
            UpdateRecoveryOptions();
            return;
        }
        ErrorText.Visibility = Visibility.Collapsed;
        SetLoginBusy(true);
        try
        {
            ServerUrlBox.Text = savedServerUrl.ToString();
            App.Api.ServerBaseAddress = savedServerUrl;
            if (await App.Auth.TryRestoreSessionAsync(CancellationToken.None))
            {
                OpenAccountPage();
            }
            else
            {
                ShowError(App.Auth.RestoreError ?? "No saved session is available. Sign in again.");
            }
        }
        finally
        {
            SetLoginBusy(false);
        }
    }

    private void ForgetServerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_loginInProgress)
        {
            return;
        }
        App.Auth.Logout();
        ServerSettings.ClearServerUrl();
        App.Api.ServerBaseAddress = null;
        ServerUrlBox.Text = string.Empty;
        UsernameBox.Text = string.Empty;
        PasswordBox.Password = string.Empty;
        AdminLoginCheckBox.IsChecked = true;
        ErrorText.Visibility = Visibility.Collapsed;
        UpdateRecoveryOptions();
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (_loginInProgress)
        {
            return;
        }
        ErrorText.Visibility = Visibility.Collapsed;
        if (!Uri.TryCreate(ServerUrlBox.Text.Trim(), UriKind.Absolute, out var serverUrl)
            || (serverUrl.Scheme != Uri.UriSchemeHttp && serverUrl.Scheme != Uri.UriSchemeHttps))
        {
            ShowError("Enter a valid server URL, e.g. https://your-omnifin-server.example.com");
            return;
        }
        SetLoginBusy(true);
        try
        {
            App.Api.ServerBaseAddress = serverUrl;
            bool isAdmin = AdminLoginCheckBox.IsChecked == true;
            await App.Auth.LoginAsync(UsernameBox.Text, PasswordBox.Password, isAdmin, CancellationToken.None);
            ServerSettings.SaveServerUrl(serverUrl);
            OpenAccountPage();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            SetLoginBusy(false);
        }
    }

    private void PasswordBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            LoginButton_Click(LoginButton, new RoutedEventArgs());
        }
    }
}

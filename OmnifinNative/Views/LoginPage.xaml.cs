using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OmnifinNative.Services;

namespace OmnifinNative;

public sealed partial class LoginPage : Page
{
    public LoginPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        var savedServerUrl = ServerSettings.LoadServerUrl();
        if (savedServerUrl is not null)
        {
            ServerUrlBox.Text = savedServerUrl.ToString();
        }
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        if (!Uri.TryCreate(ServerUrlBox.Text.Trim(), UriKind.Absolute, out var serverUrl)
            || (serverUrl.Scheme != Uri.UriSchemeHttp && serverUrl.Scheme != Uri.UriSchemeHttps))
        {
            ErrorText.Text = "Enter a valid server URL, e.g. https://your-omnifin-server.example.com";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        LoginButton.IsEnabled = false;
        LoginProgress.IsActive = true;

        try
        {
            App.Api.ServerBaseAddress = serverUrl;
            await App.Auth.LoginAsync(UsernameBox.Text, PasswordBox.Password, CancellationToken.None);
            ServerSettings.SaveServerUrl(serverUrl);
            Frame.Navigate(typeof(AccountListPage));
        }
        catch (OmnifinApiException ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoginProgress.IsActive = false;
        }
    }
}

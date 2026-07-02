using Microsoft.UI.Xaml;
using OmnifinNative.Services;

namespace OmnifinNative;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private static readonly OmnifinApiClient ApiClient = new();

    public static IOmnifinApiClient Api => ApiClient;
    public static AuthService Auth { get; } = new(ApiClient);

    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();

        var savedServerUrl = ServerSettings.LoadServerUrl();
        if (savedServerUrl is not null)
        {
            ApiClient.ServerBaseAddress = savedServerUrl;
        }

        var restored = savedServerUrl is not null && await Auth.TryRestoreSessionAsync(CancellationToken.None);
        var startPage = restored ? typeof(AccountListPage) : typeof(LoginPage);
        ((MainWindow)_window).Navigate(startPage);
    }
}

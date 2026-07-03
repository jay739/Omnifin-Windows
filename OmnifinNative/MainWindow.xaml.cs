using Microsoft.UI.Xaml;

namespace OmnifinNative;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(new Windows.Graphics.SizeInt32(900, 600));

        // Navigation happens from App.OnLaunched once the async session
        // restore check (AuthService.TryRestoreSessionAsync) completes, so
        // the app can land on AccountListPage directly instead of always
        // flashing LoginPage first.
    }

    public void Navigate(Type pageType) => RootFrame.Navigate(pageType);
}

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

        // Show the login page while a saved session is being restored so
        // an unavailable server leaves a visible recovery path.
    }

    public void Navigate(Type pageType, object? parameter = null) => RootFrame.Navigate(pageType, parameter);
}

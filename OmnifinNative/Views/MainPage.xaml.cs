using Microsoft.UI.Xaml.Controls;
using OmnifinNative.Views;

namespace OmnifinNative.Views;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
        MainNav.SelectedItem = DashboardItem;
    }

    private void MainNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is null)
        {
            return;
        }

        var tag = args.SelectedItemContainer.Tag as string;
        sender.Header = args.SelectedItemContainer.Content;

        switch (tag)
        {
            case "dashboard":
                ContentFrame.Navigate(typeof(DashboardPage));
                break;
            case "accounts":
                ContentFrame.Navigate(typeof(AccountListPage));
                break;
            case "invites":
                ContentFrame.Navigate(typeof(InviteListPage));
                break;
            case "profiles":
                ContentFrame.Navigate(typeof(ProfileListPage));
                break;
            case "activity":
                ContentFrame.Navigate(typeof(ActivityPage));
                break;
            case "announce":
                ContentFrame.Navigate(typeof(AnnouncePage));
                break;
            case "system":
                ContentFrame.Navigate(typeof(SystemPage));
                break;
            case "backup":
                ContentFrame.Navigate(typeof(BackupPage));
                break;
            case "config":
                ContentFrame.Navigate(typeof(ConfigPage));
                break;
            case "logout":
                App.Auth.Logout();
                this.Frame.Navigate(typeof(LoginPage));
                break;
        }
    }
}

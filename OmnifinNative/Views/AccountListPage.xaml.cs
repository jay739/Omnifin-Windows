using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OmnifinNative.Models;
using OmnifinNative.Services;

namespace OmnifinNative;

public sealed partial class AccountListPage : Page
{
    public ObservableCollection<RespUser> Users { get; } = [];

    public AccountListPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadUsersAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await LoadUsersAsync();

    private async void EnableButton_Click(object sender, RoutedEventArgs e) =>
        await RunOnSelectionAsync(ids => App.Api.EnableDisableUsersAsync(ids, enabled: true, CancellationToken.None));

    private async void DisableButton_Click(object sender, RoutedEventArgs e) =>
        await RunOnSelectionAsync(ids => App.Api.EnableDisableUsersAsync(ids, enabled: false, CancellationToken.None));

    private async void ExtendButton_Click(object sender, RoutedEventArgs e) =>
        await RunOnSelectionAsync(ids => App.Api.ExtendExpiryAsync(ids, months: 0, days: 30, CancellationToken.None));

    private async void DeleteButton_Click(object sender, RoutedEventArgs e) =>
        await RunOnSelectionAsync(ids => App.Api.DeleteUsersAsync(ids, CancellationToken.None));

    private async Task RunOnSelectionAsync(Func<List<string>, Task> action)
    {
        var selectedIds = UsersListView.SelectedItems
            .Cast<RespUser>()
            .Select(u => u.Id)
            .ToList();

        if (selectedIds.Count == 0)
        {
            return;
        }

        LoadingRing.IsActive = true;
        try
        {
            await action(selectedIds);
            await LoadUsersAsync();
        }
        catch (OmnifinApiException)
        {
            // TODO: surface via an InfoBar once the error-shape handling
            // is unified; the API's error responses aren't consistently
            // shaped across handlers (see the API map notes).
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }

    private async Task LoadUsersAsync()
    {
        LoadingRing.IsActive = true;
        try
        {
            var request = new UserSearchRequest { Limit = 200 };
            var users = await App.Api.SearchUsersAsync(request, CancellationToken.None);
            var watchTime = await App.Api.GetWatchTimeAsync(CancellationToken.None);

            foreach (var user in users)
            {
                if (watchTime.TryGetValue(user.Name, out var seconds))
                {
                    user.WatchTimeSeconds = seconds;
                }
            }

            Users.Clear();
            foreach (var user in users)
            {
                Users.Add(user);
            }
        }
        catch (OmnifinApiException)
        {
            // TODO: surface via an InfoBar - same error-shape caveat as above.
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }
}

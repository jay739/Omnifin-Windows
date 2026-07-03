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
            var users = await App.Api.SearchUsersAsync(request, CancellationToken.None) ?? [];
            var watchTime = await App.Api.GetWatchTimeAsync(CancellationToken.None) ?? [];

            foreach (var user in users)
            {
                if (user is not null && watchTime.TryGetValue(user.Name, out var seconds))
                {
                    user.WatchTimeSeconds = seconds;
                }
            }

            Users.Clear();
            foreach (var user in users)
            {
                if (user is not null)
                {
                    Users.Add(user);
                }
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to load users: {ex.Message}", isError: true);
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }

    private async void CreateUserFlyout_Opened(object sender, object e)
    {
        try
        {
            var response = await App.Api.GetProfilesAsync(CancellationToken.None);
            var selected = ProfileComboBox.SelectedItem as string;

            ProfileComboBox.Items.Clear();
            ProfileComboBox.Items.Add("none");
            foreach (var name in response.Profiles.Keys)
            {
                ProfileComboBox.Items.Add(name);
            }

            if (selected is not null && (selected == "none" || response.Profiles.ContainsKey(selected)))
            {
                ProfileComboBox.SelectedItem = selected;
            }
            else if (response.Profiles.ContainsKey(response.DefaultProfile))
            {
                ProfileComboBox.SelectedItem = response.DefaultProfile;
            }
            else
            {
                ProfileComboBox.SelectedIndex = 0;
            }
        }
        catch (OmnifinApiException)
        {
            ProfileComboBox.Items.Clear();
            ProfileComboBox.Items.Add("none");
            ProfileComboBox.SelectedIndex = 0;
        }
    }

    private async void ConfirmCreateUserButton_Click(object sender, RoutedEventArgs e)
    {
        var username = NewUsernameBox.Text.Trim();
        var password = NewPasswordBox.Password;
        var email = NewEmailBox.Text.Trim();
        var emailContact = EmailContactCheckBox.IsChecked == true;
        var profile = ProfileComboBox.SelectedItem as string ?? "none";

        if (string.IsNullOrEmpty(username))
        {
            ShowStatus("Username is required.", isError: true);
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowStatus("Password is required.", isError: true);
            return;
        }

        var request = new CreateUserRequest
        {
            Username = username,
            Password = password,
            Email = email,
            EmailContact = emailContact,
            Profile = profile
        };

        ConfirmCreateButton.IsEnabled = false;
        LoadingRing.IsActive = true;

        try
        {
            var response = await App.Api.CreateUserAsync(request, CancellationToken.None);
            if (response.User)
            {
                CreateUserFlyout.Hide();
                ResetCreateForm();
                ShowStatus(response.Email ? "User created successfully!" : "User created successfully (Welcome email failed).", isError: false);
                await LoadUsersAsync();
            }
            else
            {
                ShowStatus(response.Error, isError: true);
            }
        }
        catch (OmnifinApiException ex)
        {
            ShowStatus(ex.Message, isError: true);
        }
        finally
        {
            ConfirmCreateButton.IsEnabled = true;
            LoadingRing.IsActive = false;
        }
    }

    private void ResetCreateForm()
    {
        NewUsernameBox.Text = string.Empty;
        NewPasswordBox.Password = string.Empty;
        NewEmailBox.Text = string.Empty;
        EmailContactCheckBox.IsChecked = true;
        ProfileComboBox.SelectedIndex = -1;
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ? 
            new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 68, 68)) : 
            new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 34, 197, 94));
        StatusText.Visibility = Visibility.Visible;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += (s, e) =>
        {
            StatusText.Visibility = Visibility.Collapsed;
            timer.Stop();
        };
        timer.Start();
    }

    private async void EditUserFlyout_Opened(object sender, object e)
    {
        var selectedUser = UsersListView.SelectedItem as RespUser;
        if (selectedUser is null)
        {
            EditUserFlyout.Hide();
            ShowStatus("Please select a user to edit first.", isError: true);
            return;
        }

        EditUserHeadingText.Text = $"Target User: {selectedUser.Name}";
        EditEmailBox.Text = selectedUser.Email ?? string.Empty;
        EditLabelBox.Text = selectedUser.Label ?? string.Empty;
        EditAdminCheckBox.IsChecked = selectedUser.Admin;

        try
        {
            var response = await App.Api.GetProfilesAsync(CancellationToken.None);
            var selected = EditProfileComboBox.SelectedItem as string;

            EditProfileComboBox.Items.Clear();
            EditProfileComboBox.Items.Add("none");
            foreach (var name in response.Profiles.Keys)
            {
                EditProfileComboBox.Items.Add(name);
            }

            if (selected is not null && (selected == "none" || response.Profiles.ContainsKey(selected)))
            {
                EditProfileComboBox.SelectedItem = selected;
            }
            else
            {
                EditProfileComboBox.SelectedIndex = 0;
            }
        }
        catch (OmnifinApiException)
        {
            EditProfileComboBox.Items.Clear();
            EditProfileComboBox.Items.Add("none");
            EditProfileComboBox.SelectedIndex = 0;
        }
    }

    private async void ConfirmEditUserButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedUser = UsersListView.SelectedItem as RespUser;
        if (selectedUser is null) return;

        var email = EditEmailBox.Text.Trim();
        var label = EditLabelBox.Text.Trim();
        var isAdmin = EditAdminCheckBox.IsChecked == true;
        var selectedProfile = EditProfileComboBox.SelectedItem as string ?? "none";

        ConfirmEditButton.IsEnabled = false;
        LoadingRing.IsActive = true;

        try
        {
            // Modify email if changed
            if (email != (selectedUser.Email ?? string.Empty))
            {
                await App.Api.ModifyEmailsAsync(new Dictionary<string, string> { [selectedUser.Id] = email }, CancellationToken.None);
            }

            // Modify label if changed
            if (label != (selectedUser.Label ?? string.Empty))
            {
                await App.Api.ModifyLabelsAsync(new Dictionary<string, string> { [selectedUser.Id] = label }, CancellationToken.None);
            }

            // Modify admin status if changed
            if (isAdmin != selectedUser.Admin)
            {
                await App.Api.SetAccountsAdminAsync(new Dictionary<string, bool> { [selectedUser.Id] = isAdmin }, CancellationToken.None);
            }

            // Apply profile/preset settings if selected
            if (selectedProfile != "none")
            {
                var applyRequest = new UserSettingsRequest
                {
                    From = "profile",
                    Profile = selectedProfile,
                    ApplyTo = new List<string> { selectedUser.Id },
                    Configuration = ApplyPolicyCheckBox.IsChecked == true,
                    Homescreen = ApplyHomescreenCheckBox.IsChecked == true
                };
                await App.Api.ApplySettingsAsync(applyRequest, CancellationToken.None);
            }

            EditUserFlyout.Hide();
            ShowStatus("User settings updated successfully!", isError: false);
            await LoadUsersAsync();
        }
        catch (OmnifinApiException ex)
        {
            ShowStatus($"Failed to update settings: {ex.Message}", isError: true);
        }
        finally
        {
            ConfirmEditButton.IsEnabled = true;
            LoadingRing.IsActive = false;
        }
    }
}

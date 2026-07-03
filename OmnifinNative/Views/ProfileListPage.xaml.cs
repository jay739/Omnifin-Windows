using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OmnifinNative.Models;
using OmnifinNative.Services;

namespace OmnifinNative.Views;

public sealed partial class ProfileListPage : Page
{
    public ObservableCollection<ProfileInfo> Profiles { get; } = [];

    public ProfileListPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadProfilesAsync();
    }

    private async Task LoadProfilesAsync()
    {
        LoadingRing.IsActive = true;
        try
        {
            var response = await App.Api.GetProfilesAsync(CancellationToken.None);
            Profiles.Clear();
            if (response?.Profiles is not null)
            {
                foreach (var kv in response.Profiles)
                {
                    var profile = kv.Value;
                    if (profile is not null)
                    {
                        profile.Name = kv.Key;
                        profile.IsDefault = kv.Key == response.DefaultProfile;
                        Profiles.Add(profile);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ShowStatus(ex.Message, isError: true);
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await LoadProfilesAsync();

    private async void CreateFlyout_Opened(object sender, object e)
    {
        try
        {
            var users = await App.Api.SearchUsersAsync(new UserSearchRequest { Limit = 200 }, CancellationToken.None);
            SourceUserComboBox.ItemsSource = users;
            if (users.Count > 0)
            {
                SourceUserComboBox.SelectedIndex = 0;
            }
        }
        catch (OmnifinApiException)
        {
            SourceUserComboBox.ItemsSource = null;
        }
    }

    private async void ConfirmCreateButton_Click(object sender, RoutedEventArgs e)
    {
        var name = ProfileNameBox.Text.Trim();
        var selectedUser = SourceUserComboBox.SelectedItem as RespUser;

        if (string.IsNullOrEmpty(name))
        {
            ShowStatus("Please enter a profile name.", isError: true);
            return;
        }

        if (selectedUser is null)
        {
            ShowStatus("Please select a source user.", isError: true);
            return;
        }

        var request = new CreateProfileRequest
        {
            Name = name,
            Id = selectedUser.Id,
            Homescreen = HomescreenCheckBox.IsChecked == true,
            Jellyseerr = JellyseerrCheckBox.IsChecked == true
        };

        ConfirmCreateButton.IsEnabled = false;
        try
        {
            await App.Api.CreateProfileAsync(request, CancellationToken.None);
            CreateFlyout.Hide();
            ResetCreateForm();
            ShowStatus("Profile created successfully!", isError: false);
            await LoadProfilesAsync();
        }
        catch (OmnifinApiException ex)
        {
            ShowStatus(ex.Message, isError: true);
        }
        finally
        {
            ConfirmCreateButton.IsEnabled = true;
        }
    }

    private void ResetCreateForm()
    {
        ProfileNameBox.Text = string.Empty;
        SourceUserComboBox.SelectedIndex = -1;
        HomescreenCheckBox.IsChecked = true;
        JellyseerrCheckBox.IsChecked = true;
    }

    private async void SetDefaultButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = ProfilesListView.SelectedItem as ProfileInfo;
        if (selected is null)
        {
            return;
        }

        LoadingRing.IsActive = true;
        try
        {
            await App.Api.SetDefaultProfileAsync(selected.Name, CancellationToken.None);
            ShowStatus($"Default profile set to '{selected.Name}'.", isError: false);
            await LoadProfilesAsync();
        }
        catch (OmnifinApiException ex)
        {
            ShowStatus(ex.Message, isError: true);
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = ProfilesListView.SelectedItem as ProfileInfo;
        if (selected is null)
        {
            return;
        }

        if (selected.IsDefault)
        {
            ShowStatus("Cannot delete the default profile.", isError: true);
            return;
        }

        LoadingRing.IsActive = true;
        try
        {
            await App.Api.DeleteProfileAsync(selected.Name, CancellationToken.None);
            ShowStatus("Profile deleted successfully!", isError: false);
            await LoadProfilesAsync();
        }
        catch (OmnifinApiException ex)
        {
            ShowStatus(ex.Message, isError: true);
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ?
            new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 68, 68)) :
            new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 34, 197, 94));
        StatusText.Visibility = Visibility.Visible;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        timer.Tick += (s, e) =>
        {
            StatusText.Visibility = Visibility.Collapsed;
            timer.Stop();
        };
        timer.Start();
    }
}

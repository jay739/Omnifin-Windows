using System.Collections.ObjectModel;
using Windows.ApplicationModel.DataTransfer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OmnifinNative.Models;
using OmnifinNative.Services;

namespace OmnifinNative.Views;

public sealed partial class InviteListPage : Page
{
    public ObservableCollection<Invite> Invites { get; } = [];

    public InviteListPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadInvitesAsync();
    }

    private async Task LoadInvitesAsync()
    {
        LoadingRing.IsActive = true;
        try
        {
            var invites = await App.Api.GetInvitesAsync(CancellationToken.None) ?? [];
            Invites.Clear();
            foreach (var invite in invites)
            {
                if (invite is not null)
                {
                    Invites.Add(invite);
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

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await LoadInvitesAsync();

    private async void CreateFlyout_Opened(object sender, object e)
    {
        try
        {
            var response = await App.Api.GetProfilesAsync(CancellationToken.None);
            var selected = ProfileComboBox.SelectedItem as string;

            ProfileComboBox.Items.Clear();
            foreach (var name in response.Profiles.Keys)
            {
                ProfileComboBox.Items.Add(name);
            }

            if (selected is not null && response.Profiles.ContainsKey(selected))
            {
                ProfileComboBox.SelectedItem = selected;
            }
            else if (response.Profiles.ContainsKey(response.DefaultProfile))
            {
                ProfileComboBox.SelectedItem = response.DefaultProfile;
            }
            else if (ProfileComboBox.Items.Count > 0)
            {
                ProfileComboBox.SelectedIndex = 0;
            }
        }
        catch (OmnifinApiException)
        {
            ProfileComboBox.Items.Clear();
            ProfileComboBox.Items.Add("Default");
            ProfileComboBox.SelectedIndex = 0;
        }
    }

    private void MultipleUses_Changed(object sender, RoutedEventArgs e)
    {
        if (UsesSettingsPanel is null) return;
        UsesSettingsPanel.Visibility = MultipleUsesCheckBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void NoLimit_Changed(object sender, RoutedEventArgs e)
    {
        if (RemainingUsesBox is null) return;
        RemainingUsesBox.IsEnabled = NoLimitCheckBox.IsChecked != true;
    }

    private void UserExpiry_Changed(object sender, RoutedEventArgs e)
    {
        if (UserExpirySettingsPanel is null) return;
        UserExpirySettingsPanel.Visibility = UserExpiryCheckBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void ConfirmCreateButton_Click(object sender, RoutedEventArgs e)
    {
        var request = new GenerateInviteRequest
        {
            Label = InviteLabelBox.Text.Trim(),
            Profile = ProfileComboBox.SelectedItem as string ?? "Default",
            UserLabel = UserLabelBox.Text.Trim(),
            MultipleUses = MultipleUsesCheckBox.IsChecked == true,
            NoLimit = NoLimitCheckBox.IsChecked == true,
            RemainingUses = (int)RemainingUsesBox.Value,
            UserExpiry = UserExpiryCheckBox.IsChecked == true,
            UserDays = (int)UserDaysBox.Value,
            // Expiry of invite itself defaults to 30 days
            Days = 30
        };

        ConfirmCreateButton.IsEnabled = false;
        try
        {
            await App.Api.GenerateInviteAsync(request, CancellationToken.None);
            CreateFlyout.Hide();
            ResetCreateForm();
            ShowStatus("Invite generated successfully!", isError: false);
            await LoadInvitesAsync();
        }
        catch (Exception ex)
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
        InviteLabelBox.Text = string.Empty;
        UserLabelBox.Text = string.Empty;
        MultipleUsesCheckBox.IsChecked = false;
        NoLimitCheckBox.IsChecked = false;
        RemainingUsesBox.Value = 5;
        UserExpiryCheckBox.IsChecked = false;
        UserDaysBox.Value = 30;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = InvitesListView.SelectedItem as Invite;
        if (selected is null)
        {
            return;
        }

        var dp = new DataPackage();
        dp.SetText(selected.Code);
        Clipboard.SetContent(dp);
        ShowStatus("Invite code copied to clipboard!", isError: false);
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = InvitesListView.SelectedItem as Invite;
        if (selected is null)
        {
            return;
        }

        LoadingRing.IsActive = true;
        try
        {
            await App.Api.DeleteInviteAsync(selected.Code, CancellationToken.None);
            ShowStatus("Invite deleted successfully!", isError: false);
            await LoadInvitesAsync();
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

using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OmnifinNative.Models;
using OmnifinNative.Services;

namespace OmnifinNative.Views;

public sealed partial class AnnouncePage : Page
{
    private bool _isUpdatingSelection = false;

    public ObservableCollection<RespUser> Users { get; } = [];

    public AnnouncePage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadUsersAsync();
    }

    private async Task LoadUsersAsync()
    {
        LoadingRing.IsActive = true;
        try
        {
            var request = new UserSearchRequest { Limit = 500 };
            var users = await App.Api.SearchUsersAsync(request, CancellationToken.None) ?? [];
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
            ShowStatus(ex.Message, isError: true);
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }

    private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
    {
        _isUpdatingSelection = true;
        if (SelectAllCheckBox.IsChecked == true)
        {
            UsersListView.SelectAll();
        }
        else
        {
            UsersListView.SelectedItems.Clear();
        }
        _isUpdatingSelection = false;
    }

    private void UsersListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSelection) return;
        
        if (UsersListView.SelectedItems.Count == Users.Count)
        {
            SelectAllCheckBox.IsChecked = true;
        }
        else if (UsersListView.SelectedItems.Count == 0)
        {
            SelectAllCheckBox.IsChecked = false;
        }
        else
        {
            SelectAllCheckBox.IsChecked = null; // Indeterminate
        }
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        var subject = SubjectBox.Text.Trim();
        var message = MessageBox.Text.Trim();
        var isTest = TestCheckBox.IsChecked == true;

        if (string.IsNullOrEmpty(subject))
        {
            ShowStatus("Subject is required.", isError: true);
            return;
        }

        if (string.IsNullOrEmpty(message))
        {
            ShowStatus("Message is required.", isError: true);
            return;
        }

        var selectedUserIds = UsersListView.SelectedItems
            .Cast<RespUser>()
            .Select(u => u.Id)
            .ToList();

        if (!isTest && selectedUserIds.Count == 0)
        {
            ShowStatus("Please select at least one recipient.", isError: true);
            return;
        }

        var request = new AnnouncementRequest
        {
            Subject = subject,
            Message = message,
            Test = isTest,
            Users = selectedUserIds
        };

        SendButton.IsEnabled = false;
        LoadingRing.IsActive = true;
        try
        {
            await App.Api.AnnounceAsync(request, CancellationToken.None);
            ShowStatus(isTest ? "Test announcement sent successfully!" : "Announcement sent successfully!", isError: false);
            if (!isTest)
            {
                ResetForm();
            }
        }
        catch (OmnifinApiException ex)
        {
            ShowStatus(ex.Message, isError: true);
        }
        finally
        {
            SendButton.IsEnabled = true;
            LoadingRing.IsActive = false;
        }
    }

    private void ResetForm()
    {
        SubjectBox.Text = string.Empty;
        MessageBox.Text = string.Empty;
        UsersListView.SelectedItems.Clear();
        SelectAllCheckBox.IsChecked = false;
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
}

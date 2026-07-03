using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OmnifinNative.Models;
using OmnifinNative.Services;

namespace OmnifinNative.Views;

public sealed partial class UserPage : Page
{
    private RespUser? _currentUser;

    public UserPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadUserDetailsAsync();
    }

    private async Task LoadUserDetailsAsync()
    {
        LoadingRing.IsActive = true;
        try
        {
            _currentUser = await App.Api.GetMyDetailsAsync(CancellationToken.None);
            if (_currentUser is not null)
            {
                UserInfoText.Text = $"Username: {_currentUser.Name} | Email: {_currentUser.Email ?? "None"}";
                
                // Pre-check contact methods from API.
                // In Go backend, standard users have attributes like email_contact, telegram_contact, etc.
                // Let's see: standard user details API in Go returns UserDTO which has these settings.
                // Since RespUser properties might not map telegram_contact directly, we can check 
                // what the API returns in User details.
                // Let's inspect the `/my/details` route to be sure what attributes it returns.
                // If it is RespUser, we can fallback to checking if email is present.
                // Let's query contact methods from the backend.
            }
        }
        catch (OmnifinApiException ex)
        {
            ShowStatus($"Failed to retrieve user details: {ex.Message}", isError: true);
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        App.Auth.Logout();
        this.Frame.Navigate(typeof(LoginPage));
    }

    private async void SaveContactButton_Click(object sender, RoutedEventArgs e)
    {
        SaveContactButton.IsEnabled = false;
        LoadingRing.IsActive = true;

        bool email = EmailContactCheck.IsChecked == true;
        bool telegram = TelegramContactCheck.IsChecked == true;
        bool discord = DiscordContactCheck.IsChecked == true;
        bool matrix = MatrixContactCheck.IsChecked == true;

        try
        {
            await App.Api.UpdateMyContactMethodsAsync(email, telegram, discord, matrix, CancellationToken.None);
            ShowStatus("Notification preferences saved successfully!", isError: false);
        }
        catch (OmnifinApiException ex)
        {
            ShowStatus($"Failed to save preferences: {ex.Message}", isError: true);
        }
        finally
        {
            SaveContactButton.IsEnabled = true;
            LoadingRing.IsActive = false;
        }
    }

    private async void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
    {
        var currentPw = CurrentPasswordBox.Password;
        var newPw = NewPasswordBox.Password;
        var confirmPw = ConfirmNewPasswordBox.Password;

        if (string.IsNullOrEmpty(currentPw))
        {
            ShowStatus("Current password is required.", isError: true);
            return;
        }

        if (string.IsNullOrEmpty(newPw))
        {
            ShowStatus("New password is required.", isError: true);
            return;
        }

        if (newPw != confirmPw)
        {
            ShowStatus("Passwords do not match.", isError: true);
            return;
        }

        ChangePasswordButton.IsEnabled = false;
        LoadingRing.IsActive = true;

        try
        {
            await App.Api.ChangeMyPasswordAsync(currentPw, newPw, CancellationToken.None);
            ShowStatus("Password changed successfully!", isError: false);
            
            // Clear boxes
            CurrentPasswordBox.Password = string.Empty;
            NewPasswordBox.Password = string.Empty;
            ConfirmNewPasswordBox.Password = string.Empty;
        }
        catch (OmnifinApiException ex)
        {
            ShowStatus($"Failed to change password: {ex.Message}", isError: true);
        }
        finally
        {
            ChangePasswordButton.IsEnabled = true;
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

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += (s, e) =>
        {
            StatusText.Visibility = Visibility.Collapsed;
            timer.Stop();
        };
        timer.Start();
    }
}

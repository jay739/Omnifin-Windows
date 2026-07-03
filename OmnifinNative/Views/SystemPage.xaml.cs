using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OmnifinNative.Models;
using OmnifinNative.Services;

namespace OmnifinNative.Views;

public sealed partial class SystemPage : Page
{
    public ObservableCollection<TaskInfo> Tasks { get; } = [];

    public SystemPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        LoadingRing.IsActive = true;
        try
        {
            await Task.WhenAll(LoadTasksAsync(), LoadLogsAsync());
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

    private async Task LoadTasksAsync()
    {
        try
        {
            var tasks = await App.Api.GetTasksAsync(CancellationToken.None) ?? [];
            Tasks.Clear();
            foreach (var task in tasks)
            {
                if (task is not null)
                {
                    Tasks.Add(task);
                }
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to load tasks: {ex.Message}", isError: true);
        }
    }

    private async Task LoadLogsAsync()
    {
        try
        {
            var logs = await App.Api.GetLogsAsync(CancellationToken.None);
            LogsTextBox.Text = logs;
            
            // Scroll to end of logs
            LogsTextBox.Select(LogsTextBox.Text.Length, 0);
        }
        catch (OmnifinApiException ex)
        {
            LogsTextBox.Text = $"Failed to retrieve logs: {ex.Message}";
        }
    }

    private async void RefreshLogsButton_Click(object sender, RoutedEventArgs e)
    {
        LoadingRing.IsActive = true;
        await LoadLogsAsync();
        LoadingRing.IsActive = false;
        ShowStatus("Logs refreshed.", isError: false);
    }

    private async void RunTaskButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = TasksListView.SelectedItem as TaskInfo;
        if (selected is null)
        {
            ShowStatus("Please select a task to run.", isError: true);
            return;
        }

        RunTaskButton.IsEnabled = false;
        LoadingRing.IsActive = true;

        try
        {
            await App.Api.RunTaskAsync(selected.Url, CancellationToken.None);
            ShowStatus($"Task '{selected.Name}' triggered successfully!", isError: false);
        }
        catch (OmnifinApiException ex)
        {
            ShowStatus($"Task trigger failed: {ex.Message}", isError: true);
        }
        finally
        {
            RunTaskButton.IsEnabled = true;
            LoadingRing.IsActive = false;
        }
    }

    private async void RestartButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Restart Server?",
            Content = "Are you sure you want to restart the Omnifin backend server?",
            PrimaryButtonText = "Restart",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            RestartButton.IsEnabled = false;
            LoadingRing.IsActive = true;
            try
            {
                await App.Api.RestartServerAsync(CancellationToken.None);
                ShowStatus("Restart command sent. The server is restarting...", isError: false);
                
                // Clear local session because server restarts invalidate state
                App.Auth.Logout();
                
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                timer.Tick += (s, ev) =>
                {
                    timer.Stop();
                    this.Frame.Navigate(typeof(LoginPage));
                };
                timer.Start();
            }
            catch (OmnifinApiException ex)
            {
                ShowStatus($"Restart failed: {ex.Message}", isError: true);
                RestartButton.IsEnabled = true;
                LoadingRing.IsActive = false;
            }
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

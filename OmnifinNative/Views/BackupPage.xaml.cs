using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OmnifinNative.Models;
using OmnifinNative.Services;

namespace OmnifinNative.Views;

public sealed partial class BackupPage : Page
{
    public ObservableCollection<BackupInfo> Backups { get; } = [];

    public BackupPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadBackupsAsync();
    }

    private async Task LoadBackupsAsync()
    {
        LoadingRing.IsActive = true;
        try
        {
            var backups = await App.Api.GetBackupsAsync(CancellationToken.None) ?? [];
            Backups.Clear();
            foreach (var b in backups)
            {
                if (b is not null)
                {
                    Backups.Add(b);
                }
            }
            EmptyState.Visibility = Backups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to load backups: {ex.Message}", isError: true);
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadBackupsAsync();
    }

    private async void CreateBackupButton_Click(object sender, RoutedEventArgs e)
    {
        LoadingRing.IsActive = true;
        try
        {
            await App.Api.CreateBackupAsync(CancellationToken.None);
            ShowStatus("Database backup created successfully!", isError: false);
            await LoadBackupsAsync();
        }
        catch (OmnifinApiException ex)
        {
            ShowStatus($"Failed to create backup: {ex.Message}", isError: true);
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }

    private async void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = BackupsListView.SelectedItem as BackupInfo;
        if (selected is null)
        {
            ShowStatus("Please select a backup to restore.", isError: true);
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Restore Backup?",
            Content = $"Are you sure you want to restore the backup '{selected.Name}'? This will overwrite the current database and log you out.",
            PrimaryButtonText = "Restore",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            RestoreButton.IsEnabled = false;
            LoadingRing.IsActive = true;
            try
            {
                await App.Api.RestoreBackupAsync(selected.Name, CancellationToken.None);
                ShowStatus("Database restored successfully! Logging out...", isError: false);

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
                ShowStatus($"Restore failed: {ex.Message}", isError: true);
                RestoreButton.IsEnabled = true;
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

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using OmnifinNative.Models;
using OmnifinNative.Services;

namespace OmnifinNative.Views;

public sealed partial class DashboardPage : Page, INotifyPropertyChanged
{
    private static readonly Brush RedBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 68, 68));
    private static readonly Brush GreenBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 34, 197, 94));
    private static readonly Brush GrayBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 55, 65, 81));

    private int _totalUsersCount;
    private int _activeUsersCount;
    private string _totalUsersText = "0";
    private string _activeUsersText = "0 Active / 0 Disabled";
    private string _watchTimeText = "0.0 hrs";
    private string _streamersCountText = "Across 0 active accounts";
    private string _invitesCountText = "0";
    private string _invitesUsesText = "0 remaining uses";
    private Brush _smtpBrush = GrayBrush;
    private Brush _tgBrush = GrayBrush;
    private Brush _discordBrush = GrayBrush;
    private Brush _matrixBrush = GrayBrush;

    public event PropertyChangedEventHandler? PropertyChanged;

    public DashboardPage()
    {
        InitializeComponent();
    }

    public int TotalUsersCount
    {
        get => _totalUsersCount;
        set { _totalUsersCount = value; OnPropertyChanged(); }
    }

    public int ActiveUsersCount
    {
        get => _activeUsersCount;
        set { _activeUsersCount = value; OnPropertyChanged(); }
    }

    public string TotalUsersText
    {
        get => _totalUsersText;
        set { _totalUsersText = value; OnPropertyChanged(); }
    }

    public string ActiveUsersText
    {
        get => _activeUsersText;
        set { _activeUsersText = value; OnPropertyChanged(); }
    }

    public string WatchTimeText
    {
        get => _watchTimeText;
        set { _watchTimeText = value; OnPropertyChanged(); }
    }

    public string StreamersCountText
    {
        get => _streamersCountText;
        set { _streamersCountText = value; OnPropertyChanged(); }
    }

    public string InvitesCountText
    {
        get => _invitesCountText;
        set { _invitesCountText = value; OnPropertyChanged(); }
    }

    public string InvitesUsesText
    {
        get => _invitesUsesText;
        set { _invitesUsesText = value; OnPropertyChanged(); }
    }

    public Brush SmtpBrush
    {
        get => _smtpBrush;
        set { _smtpBrush = value; OnPropertyChanged(); }
    }

    public Brush TgBrush
    {
        get => _tgBrush;
        set { _tgBrush = value; OnPropertyChanged(); }
    }

    public Brush DiscordBrush
    {
        get => _discordBrush;
        set { _discordBrush = value; OnPropertyChanged(); }
    }

    public Brush MatrixBrush
    {
        get => _matrixBrush;
        set { _matrixBrush = value; OnPropertyChanged(); }
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadDashboardDataAsync();
    }

    private async Task LoadDashboardDataAsync()
    {
        LoadingRing.IsActive = true;
        try
        {
            // 1. Fetch data concurrently
            var usersTask = App.Api.SearchUsersAsync(new UserSearchRequest { Limit = 1000 }, CancellationToken.None);
            var watchTimeTask = App.Api.GetWatchTimeAsync(CancellationToken.None);
            var invitesTask = App.Api.GetInvitesAsync(CancellationToken.None);
            var configTask = App.Api.GetConfigAsync(CancellationToken.None);

            await Task.WhenAll(usersTask, watchTimeTask, invitesTask, configTask);

            var users = await usersTask ?? [];
            var watchTime = await watchTimeTask ?? [];
            var invites = await invitesTask ?? [];
            var config = await configTask;

            // 2. Parse User Statistics
            TotalUsersCount = users.Count;
            ActiveUsersCount = users.Count(u => !u.Disabled);
            TotalUsersText = $"{TotalUsersCount}";
            ActiveUsersText = $"{ActiveUsersCount} Active / {TotalUsersCount - ActiveUsersCount} Disabled";

            // 3. Parse Watch Time
            long totalSeconds = watchTime.Values.Sum();
            double totalHours = totalSeconds / 3600.0;
            if (totalHours >= 24)
            {
                WatchTimeText = $"{totalHours / 24.0:F1} days";
            }
            else
            {
                WatchTimeText = $"{totalHours:F1} hrs";
            }
            StreamersCountText = $"Across {watchTime.Count} active accounts";

            // 4. Parse Invites
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var activeInvites = invites.Count(i => (i.NoLimit || i.RemainingUses > 0) && (i.ValidTill == 0 || i.ValidTill > now));
            var totalRemainingUses = invites.Where(i => !i.NoLimit && (i.ValidTill == 0 || i.ValidTill > now)).Sum(i => i.RemainingUses);
            var infiniteInvites = invites.Count(i => i.NoLimit && (i.ValidTill == 0 || i.ValidTill > now));

            InvitesCountText = $"{activeInvites}";
            InvitesUsesText = infiniteInvites > 0 ? $"{totalRemainingUses} remaining + infinite" : $"{totalRemainingUses} remaining uses";

            // 5. Parse Integration Health status from config
            bool smtpEnabled = GetBoolValue(config, "email", "enabled", false);
            bool tgEnabled = GetBoolValue(config, "telegram", "enabled", false);
            bool discordEnabled = GetBoolValue(config, "discord", "enabled", false);
            bool matrixEnabled = GetBoolValue(config, "matrix", "enabled", false);

            SmtpBrush = smtpEnabled ? GreenBrush : RedBrush;
            TgBrush = tgEnabled ? GreenBrush : RedBrush;
            DiscordBrush = discordEnabled ? GreenBrush : RedBrush;
            MatrixBrush = matrixEnabled ? GreenBrush : RedBrush;

            // Force compiled bindings update on the UI thread
            this.Bindings.Update();
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to retrieve server analytics: {ex.Message}", isError: true);
            try
            {
                var dialog = new ContentDialog
                {
                    Title = "Dashboard Load Error",
                    Content = $"Details:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                    CloseButtonText = "Close",
                    XamlRoot = this.XamlRoot
                };
                _ = dialog.ShowAsync();
            }
            catch
            {
                // Fallback if XamlRoot is not yet ready
            }
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }

    private string GetValue(Models.GetServerConfigResponse config, string sectionName, string settingName, string defaultValue)
    {
        if (config?.Sections is null) return defaultValue;
        var sect = config.Sections.FirstOrDefault(s => s.Section.Equals(sectionName, StringComparison.OrdinalIgnoreCase));
        if (sect?.Settings is null) return defaultValue;
        var setting = sect.Settings.FirstOrDefault(s => s.Setting.Equals(settingName, StringComparison.OrdinalIgnoreCase));
        if (setting is null) return defaultValue;

        if (setting.Value is System.Text.Json.JsonElement je)
        {
            return je.ValueKind == System.Text.Json.JsonValueKind.String ? (je.GetString() ?? defaultValue) : je.ToString();
        }
        return setting.Value?.ToString() ?? defaultValue;
    }

    private bool GetBoolValue(Models.GetServerConfigResponse config, string sectionName, string settingName, bool defaultValue)
    {
        if (config?.Sections is null) return defaultValue;
        var sect = config.Sections.FirstOrDefault(s => s.Section.Equals(sectionName, StringComparison.OrdinalIgnoreCase));
        if (sect?.Settings is null) return defaultValue;
        var setting = sect.Settings.FirstOrDefault(s => s.Setting.Equals(settingName, StringComparison.OrdinalIgnoreCase));
        if (setting is null) return defaultValue;

        var val = setting.Value;
        if (val is bool b) return b;
        if (val is string s && bool.TryParse(s, out var sb)) return sb;
        if (val is System.Text.Json.JsonElement je)
        {
            if (je.ValueKind == System.Text.Json.JsonValueKind.True) return true;
            if (je.ValueKind == System.Text.Json.JsonValueKind.False) return false;
            if (je.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                return bool.TryParse(je.GetString(), out var jsb) && jsb;
            }
        }
        return defaultValue;
    }

    private async void BackupButton_Click(object sender, RoutedEventArgs e)
    {
        BackupButton.IsEnabled = false;
        LoadingRing.IsActive = true;
        try
        {
            await App.Api.CreateBackupAsync(CancellationToken.None);
            ShowStatus("Database backup completed successfully!", isError: false);
        }
        catch (Exception ex)
        {
            ShowStatus($"Backup failed: {ex.Message}", isError: true);
        }
        finally
        {
            BackupButton.IsEnabled = true;
            LoadingRing.IsActive = false;
        }
    }

    private async void HousekeepingButton_Click(object sender, RoutedEventArgs e)
    {
        HousekeepingButton.IsEnabled = false;
        LoadingRing.IsActive = true;
        try
        {
            var tasks = await App.Api.GetTasksAsync(CancellationToken.None);
            var task = tasks.Find(t => 
                t.Name.Contains("housekeeping", System.StringComparison.OrdinalIgnoreCase) || 
                t.Name.Contains("clean", System.StringComparison.OrdinalIgnoreCase) ||
                t.Url.Contains("housekeeping", System.StringComparison.OrdinalIgnoreCase));

            if (task is null)
            {
                ShowStatus("Housekeeping task not registered on server.", isError: true);
                return;
            }

            await App.Api.RunTaskAsync(task.Url, CancellationToken.None);
            ShowStatus($"Housekeeping task '{task.Name}' triggered successfully!", isError: false);
        }
        catch (Exception ex)
        {
            ShowStatus($"Housekeeping trigger failed: {ex.Message}", isError: true);
        }
        finally
        {
            HousekeepingButton.IsEnabled = true;
            LoadingRing.IsActive = false;
        }
    }

    private async void RestartButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Restart Server?",
            Content = "Are you sure you want to restart the Omnifin backend server? This will end active logins.",
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
                
                App.Auth.Logout();
                
                var timer = new DispatcherTimer { Interval = System.TimeSpan.FromSeconds(3) };
                timer.Tick += (s, ev) =>
                {
                    timer.Stop();
                    this.Frame.Navigate(typeof(LoginPage));
                };
                timer.Start();
            }
            catch (Exception ex)
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
            new SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 68, 68)) : 
            new SolidColorBrush(Windows.UI.Color.FromArgb(255, 34, 197, 94));
        StatusText.Visibility = Visibility.Visible;

        var timer = new DispatcherTimer { Interval = System.TimeSpan.FromSeconds(5) };
        timer.Tick += (s, e) =>
        {
            StatusText.Visibility = Visibility.Collapsed;
            timer.Stop();
        };
        timer.Start();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

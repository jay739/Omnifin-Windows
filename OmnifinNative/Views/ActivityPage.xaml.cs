using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OmnifinNative.Models;
using OmnifinNative.Services;

namespace OmnifinNative.Views;

public sealed partial class ActivityPage : Page
{
    private int _currentPage = 0;
    private const int PageSize = 25;
    private bool _lastPageReached = false;

    public ObservableCollection<Activity> Activities { get; } = [];

    public ActivityPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadActivitiesAsync();
    }

    private async Task LoadActivitiesAsync()
    {
        LoadingRing.IsActive = true;
        StatusText.Visibility = Visibility.Collapsed;

        try
        {
            var request = new SearchActivitiesRequest
            {
                Limit = PageSize,
                Page = _currentPage,
                SortByField = "time",
                Ascending = false
            };

            var response = await App.Api.GetActivitiesAsync(request, CancellationToken.None);
            
            Activities.Clear();
            if (response?.Activities is not null)
            {
                foreach (var activity in response.Activities)
                {
                    if (activity is not null)
                    {
                        Activities.Add(activity);
                    }
                }
            }

            _lastPageReached = response?.LastPage ?? true;
            PageNumText.Text = $"Page {_currentPage + 1}";
            PrevButton.IsEnabled = _currentPage > 0;
            NextButton.IsEnabled = !_lastPageReached;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await LoadActivitiesAsync();

    private async void PrevButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage > 0)
        {
            _currentPage--;
            await LoadActivitiesAsync();
        }
    }

    private async void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_lastPageReached)
        {
            _currentPage++;
            await LoadActivitiesAsync();
        }
    }

    private void ShowError(string message)
    {
        StatusText.Text = message;
        StatusText.Visibility = Visibility.Visible;
    }
}

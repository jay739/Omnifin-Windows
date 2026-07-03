using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OmnifinNative.Models;
using OmnifinNative.Services;

namespace OmnifinNative.Views;

public sealed partial class ConfigPage : Page
{
    private GetServerConfigResponse? _configResponse;
    private readonly Dictionary<string, Dictionary<string, Control>> _dynamicControls = [];

    private static readonly List<string> SectionOrder =
    [
        "jellyfin",
        "ui",
        "email",
        "telegram",
        "discord",
        "matrix",
        "ombi",
        "jellyseerr",
        "backups"
    ];

    public ConfigPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadConfigAsync();
    }

    private async Task LoadConfigAsync()
    {
        LoadingRing.IsActive = true;
        try
        {
            _configResponse = await App.Api.GetConfigAsync(CancellationToken.None);
            PopulateUi();
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to load configuration: {ex.Message}", isError: true);
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }

    private void PopulateUi()
    {
        ConfigPivot.Items.Clear();
        _dynamicControls.Clear();

        if (_configResponse?.Sections is null) return;

        var sortedSections = _configResponse.Sections
            .OrderBy(s => {
                int index = SectionOrder.IndexOf(s.Section.ToLowerInvariant());
                return index >= 0 ? index : 100;
            })
            .ThenBy(s => s.Meta.Name)
            .ToList();

        foreach (var section in sortedSections)
        {
            if (section is null || section.Settings is null || section.Settings.Count == 0) continue;

            var sectionName = section.Section;
            var scrollViewer = new ScrollViewer { Margin = new Thickness(0, 12, 0, 0) };
            var stackPanel = new StackPanel 
            { 
                Spacing = 16, 
                MaxWidth = 550, 
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(4, 0, 16, 16)
            };
            scrollViewer.Content = stackPanel;

            // Section Header
            stackPanel.Children.Add(new TextBlock 
            { 
                Text = section.Meta.Name, 
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 2)
            });

            // Section Description Subtitle
            if (!string.IsNullOrWhiteSpace(section.Meta.Description))
            {
                var secDesc = new TextBlock 
                { 
                    Text = section.Meta.Description, 
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 12),
                    Opacity = 0.6
                };
                stackPanel.Children.Add(secDesc);
            }

            var controlMap = new Dictionary<string, Control>();
            _dynamicControls[sectionName] = controlMap;

            foreach (var setting in section.Settings)
            {
                if (setting is null) continue;

                var settingName = setting.Setting;
                var displayName = setting.Name;
                var description = setting.Description;
                var settingType = setting.Type;
                var settingVal = setting.Value;

                if (setting.RequiresRestart)
                {
                    displayName += " *";
                }

                if (settingType == "bool")
                {
                    bool isChecked = false;
                    if (settingVal is bool b) isChecked = b;
                    else if (settingVal is JsonElement je && (je.ValueKind == JsonValueKind.True || je.ValueKind == JsonValueKind.False))
                    {
                        isChecked = je.ValueKind == JsonValueKind.True;
                    }
                    else if (settingVal?.ToString() == "true")
                    {
                        isChecked = true;
                    }

                    var checkBox = new CheckBox 
                    { 
                        Content = displayName, 
                        IsChecked = isChecked,
                        Margin = new Thickness(0, 4, 0, 4)
                    };
                    stackPanel.Children.Add(checkBox);
                    controlMap[settingName] = checkBox;
                }
                else if (settingType == "select" && setting.Options is not null && setting.Options.Count > 0)
                {
                    var valStr = GetSettingStringValue(settingVal);
                    var comboBox = new ComboBox 
                    { 
                        Header = displayName,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Width = 450
                    };
                    foreach (var option in setting.Options)
                    {
                        // Option is [value, display_name]
                        var displayText = option.Count > 1 ? option[1] : option[0];
                        comboBox.Items.Add(displayText);
                    }
                    // Match by value or display name
                    for (int i = 0; i < setting.Options.Count; i++)
                    {
                        var opt = setting.Options[i];
                        if (opt[0] == valStr || (opt.Count > 1 && opt[1] == valStr))
                        {
                            comboBox.SelectedIndex = i;
                            break;
                        }
                    }
                    if (comboBox.SelectedIndex == -1 && comboBox.Items.Count > 0)
                    {
                        comboBox.SelectedIndex = 0;
                    }

                    stackPanel.Children.Add(comboBox);
                    controlMap[settingName] = comboBox;
                }
                else
                {
                    var valStr = GetSettingStringValue(settingVal);
                    var isPassword = settingType == "password" || 
                                     settingName.Contains("password", StringComparison.OrdinalIgnoreCase) || 
                                     settingName.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                                     settingName.Contains("secret", StringComparison.OrdinalIgnoreCase);

                    if (isPassword)
                    {
                        var passwordBox = new PasswordBox 
                        { 
                            Header = displayName, 
                            Password = valStr,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            Width = 450
                        };
                        stackPanel.Children.Add(passwordBox);
                        controlMap[settingName] = passwordBox;
                    }
                    else
                    {
                        var textBox = new TextBox 
                        { 
                            Header = displayName, 
                            Text = valStr,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            Width = 450
                        };
                        stackPanel.Children.Add(textBox);
                        controlMap[settingName] = textBox;
                    }
                }

                // Add helper description block + restart info
                var helperText = description;
                if (setting.RequiresRestart)
                {
                    if (!string.IsNullOrWhiteSpace(helperText)) helperText += "\n";
                    helperText += "⚠️ Changes require server restart.";
                }

                if (!string.IsNullOrWhiteSpace(helperText))
                {
                    var textBlock = new TextBlock 
                    { 
                        Text = helperText, 
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, -8, 0, 8),
                        Width = 450,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    
                    if (setting.RequiresRestart)
                    {
                        textBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 245, 158, 11));
                    }
                    else if (Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out var secondaryBrushObj) && secondaryBrushObj is Microsoft.UI.Xaml.Media.Brush brush)
                    {
                        textBlock.Foreground = brush;
                    }
                    
                    stackPanel.Children.Add(textBlock);
                }
            }

            // Pivot Tab Header
            var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var sectionIcon = new FontIcon
            {
                Glyph = GetSectionIcon(sectionName),
                FontSize = 14
            };

            if (Application.Current.Resources.TryGetValue("SystemAccentColorBrush", out var accentBrushObj) &&
                accentBrushObj is Microsoft.UI.Xaml.Media.Brush accentBrush)
            {
                sectionIcon.Foreground = accentBrush;
            }
            else if (Application.Current.Resources.TryGetValue("SystemAccentColor", out var accentColorObj) &&
                     accentColorObj is Windows.UI.Color accentColor)
            {
                sectionIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(accentColor);
            }

            headerStack.Children.Add(sectionIcon);
            headerStack.Children.Add(new TextBlock 
            { 
                Text = section.Meta.Name, 
                VerticalAlignment = VerticalAlignment.Center 
            });

            var pivotItem = new PivotItem 
            { 
                Header = headerStack,
                Content = scrollViewer
            };
            ConfigPivot.Items.Add(pivotItem);
        }
    }

    private string GetSectionIcon(string section)
    {
        return section.ToLowerInvariant() switch
        {
            "jellyfin" => "\uE714", // WebMedia
            "ui" => "\uE7F4",       // Personalize
            "email" => "\uE715",     // Mail
            "telegram" => "\uE8BD",  // Chat message
            "discord" => "\uE8BD",
            "matrix" => "\uE8BD",
            "ombi" => "\uE95A",      // AddOns
            "jellyseerr" => "\uE95A",
            "backups" => "\uE8C8",    // Copy/Backup
            "messages" => "\uE715",   // Mail
            "password_validation" => "\uE8D7", // Shield/Password
            "user_page" => "\uE77B", // Person
            _ => "\uE713"            // Settings Gear
        };
    }

    private string GetSettingStringValue(object? val)
    {
        if (val is null) return string.Empty;
        if (val is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.String)
                return je.GetString() ?? string.Empty;
            return je.ToString();
        }
        return val.ToString() ?? string.Empty;
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        LoadingRing.IsActive = true;
        try
        {
            var savePayload = new Dictionary<string, Dictionary<string, string>>();

            foreach (var sectionPair in _dynamicControls)
            {
                var sectionName = sectionPair.Key;
                var controlMap = sectionPair.Value;

                var sectionDict = new Dictionary<string, string>();
                savePayload[sectionName] = sectionDict;

                foreach (var controlPair in controlMap)
                {
                    var settingName = controlPair.Key;
                    var control = controlPair.Value;

                    if (control is CheckBox checkBox)
                    {
                        sectionDict[settingName] = checkBox.IsChecked == true ? "true" : "false";
                    }
                    else if (control is ComboBox comboBox)
                    {
                        // Look up the option value from the original config
                        var selectedIndex = comboBox.SelectedIndex;
                        var sect = _configResponse?.Sections?.FirstOrDefault(s => s.Section == sectionName);
                        var stg = sect?.Settings?.FirstOrDefault(s => s.Setting == settingName);
                        if (stg?.Options is not null && selectedIndex >= 0 && selectedIndex < stg.Options.Count)
                        {
                            sectionDict[settingName] = stg.Options[selectedIndex][0]; // [0] is the value
                        }
                        else
                        {
                            sectionDict[settingName] = comboBox.SelectedItem?.ToString() ?? string.Empty;
                        }
                    }
                    else if (control is PasswordBox passwordBox)
                    {
                        sectionDict[settingName] = passwordBox.Password;
                    }
                    else if (control is TextBox textBox)
                    {
                        sectionDict[settingName] = textBox.Text.Trim();
                    }
                }
            }

            await App.Api.SaveConfigAsync(savePayload, CancellationToken.None);
            ShowStatus("Configuration saved successfully!", isError: false);
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to save configuration: {ex.Message}", isError: true);
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

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += (s, e) =>
        {
            StatusText.Visibility = Visibility.Collapsed;
            timer.Stop();
        };
        timer.Start();
    }
}

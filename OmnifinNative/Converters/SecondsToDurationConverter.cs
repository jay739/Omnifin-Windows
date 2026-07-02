using Microsoft.UI.Xaml.Data;

namespace OmnifinNative.Converters;

public sealed class SecondsToDurationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var totalSeconds = (long)value;
        if (totalSeconds <= 0)
        {
            return "—";
        }

        var span = TimeSpan.FromSeconds(totalSeconds);
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours}h {span.Minutes}m"
            : $"{span.Minutes}m";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace OmnifinNative.Converters;

public sealed class BoolToStatusBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush ActiveBrush = new(Windows.UI.Color.FromArgb(255, 34, 197, 94));
    private static readonly SolidColorBrush DisabledBrush = new(Windows.UI.Color.FromArgb(255, 239, 68, 68));

    public object Convert(object value, Type targetType, object parameter, string language) =>
        (bool)value ? DisabledBrush : ActiveBrush;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

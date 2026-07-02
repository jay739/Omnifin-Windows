using Microsoft.UI.Xaml.Data;

namespace OmnifinNative.Converters;

public sealed class BoolToStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        (bool)value ? "Disabled" : "Active";

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

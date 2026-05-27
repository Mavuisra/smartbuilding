using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SmartBuilding.Desktop.WPF.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = value is true;
        if (IsInverse(parameter))
            visible = !visible;

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static bool IsInverse(object? parameter) =>
        parameter is string s && s.Equals("Inverse", StringComparison.OrdinalIgnoreCase);
}

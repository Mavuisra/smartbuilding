using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SmartBuilding.Desktop.WPF.Converters;

public class GuidNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Guid g && g != Guid.Empty ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

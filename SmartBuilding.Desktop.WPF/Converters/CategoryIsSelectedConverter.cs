using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SmartBuilding.Desktop.WPF.Converters;

public class CategoryIsSelectedConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return Brushes.Transparent;

        var selected = values[0]?.ToString();
        var categoryId = values[1]?.ToString();
        return string.Equals(selected, categoryId, StringComparison.OrdinalIgnoreCase)
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F5EE")!)
            : Brushes.Transparent;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

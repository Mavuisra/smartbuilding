using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SmartBuilding.Desktop.WPF.Converters;

public class ModuleNavStyleMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var selected = values.Length > 0 ? values[0] as string : null;
        var moduleId = values.Length > 1 ? values[1] as string : null;
        var key = string.Equals(selected, moduleId, StringComparison.OrdinalIgnoreCase)
            ? "SbmsSidebarButtonActive"
            : "SbmsSidebarButton";
        return System.Windows.Application.Current.FindResource(key);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

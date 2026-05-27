using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SmartBuilding.Desktop.WPF.Converters;

public class ModuleNavStyleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var selected = value as string;
        var moduleId = parameter as string;
        var key = selected == moduleId ? "SbmsSidebarButtonActive" : "SbmsSidebarButton";
        return System.Windows.Application.Current.FindResource(key);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

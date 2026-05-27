using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace SmartBuilding.Desktop.WPF.Converters;

public class StringToPackIconKindConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value as string;
        if (!string.IsNullOrEmpty(s) && Enum.TryParse<PackIconKind>(s, true, out var kind))
            return kind;
        return PackIconKind.Database;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

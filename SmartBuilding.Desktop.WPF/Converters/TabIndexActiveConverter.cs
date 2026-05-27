using System.Globalization;
using System.Windows.Data;

namespace SmartBuilding.Desktop.WPF.Converters;

/// <summary>
/// Compare l'onglet sélectionné (values[0]) à l'index de l'onglet (values[1]).
/// </summary>
public class TabIndexActiveConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return false;

        var selected = values[0] is int s ? s : int.TryParse(values[0]?.ToString(), out var p) ? p : -1;
        var index = values[1] is int i ? i : int.TryParse(values[1]?.ToString(), out var q) ? q : -1;
        return selected == index;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

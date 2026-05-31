using System.Globalization;
using System.Windows.Data;
using SmartBuilding.Desktop.WPF.Services;

namespace SmartBuilding.Desktop.WPF.Converters;

public sealed class DecimalToMoneyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal d)
            return MoneyFormatter.Format(d);
        if (value is double dbl)
            return MoneyFormatter.Format((decimal)dbl);
        if (value is int i)
            return MoneyFormatter.Format(i);
        return MoneyFormatter.ZeroDisplay;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

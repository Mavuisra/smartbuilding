using System.Globalization;
using System.Windows.Data;
using SmartBuilding.Desktop.WPF.ViewModels;
using SmartBuilding.Desktop.WPF.Views;

namespace SmartBuilding.Desktop.WPF.Converters;

public class ViewModelToViewConverter : IValueConverter
{
    private static readonly Dictionary<Type, Type> Map = new()
    {
        [typeof(DashboardViewModel)] = typeof(DashboardView)
    };

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null) return null;
        var vmType = value.GetType();
        if (!Map.TryGetValue(vmType, out var viewType)) return null;
        return Activator.CreateInstance(viewType);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

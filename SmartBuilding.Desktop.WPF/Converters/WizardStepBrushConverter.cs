using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SmartBuilding.Desktop.WPF.Converters;

/// <summary>
/// [CurrentStep, StepIndex] → couleur du cercle d'étape (actif / complété / à venir).
/// </summary>
public class WizardStepCircleBrushConverter : IMultiValueConverter
{
    private static readonly SolidColorBrush Active = new(Color.FromRgb(0x0B, 0x63, 0xF6));
    private static readonly SolidColorBrush Completed = new(Color.FromRgb(0x22, 0xC5, 0x5E));
    private static readonly SolidColorBrush Inactive = new(Color.FromRgb(0xEE, 0xF2, 0xF7));

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || !TryParse(values[0], out var current) || !TryParse(values[1], out var index))
            return Inactive;

        if (current == index) return Active;
        if (current > index) return Completed;
        return Inactive;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static bool TryParse(object? value, out int result)
    {
        result = 0;
        return value is not null && int.TryParse(value.ToString(), out result);
    }
}

/// <summary>
/// [CurrentStep, StepIndex] → couleur du titre d'étape.
/// </summary>
public class WizardStepTitleBrushConverter : IMultiValueConverter
{
    private static readonly SolidColorBrush Active = new(Color.FromRgb(0x0B, 0x63, 0xF6));
    private static readonly SolidColorBrush Completed = new(Color.FromRgb(0x16, 0x65, 0x34));
    private static readonly SolidColorBrush Inactive = new(Color.FromRgb(0x11, 0x18, 0x27));

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || !TryParse(values[0], out var current) || !TryParse(values[1], out var index))
            return Inactive;

        if (current == index) return Active;
        if (current > index) return Completed;
        return Inactive;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static bool TryParse(object? value, out int result)
    {
        result = 0;
        return value is not null && int.TryParse(value.ToString(), out result);
    }
}

/// <summary>
/// [CurrentStep, StepIndex] → couleur du numéro dans le cercle d'étape.
/// </summary>
public class WizardStepNumberBrushConverter : IMultiValueConverter
{
    private static readonly SolidColorBrush Active = new(Colors.White);
    private static readonly SolidColorBrush Completed = new(Colors.White);
    private static readonly SolidColorBrush Inactive = new(Color.FromRgb(0x11, 0x18, 0x27));

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || !TryParse(values[0], out var current) || !TryParse(values[1], out var index))
            return Inactive;

        if (current == index) return Active;
        if (current > index) return Completed;
        return Inactive;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static bool TryParse(object? value, out int result)
    {
        result = 0;
        return value is not null && int.TryParse(value.ToString(), out result);
    }
}

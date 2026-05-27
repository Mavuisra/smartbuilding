namespace SmartBuilding.Desktop.WPF.Helpers;

/// <summary>
/// Normalise les CommandParameter WPF (int depuis AlternationIndex, string depuis XAML "0", etc.).
/// </summary>
public static class TabNavigationHelper
{
    public static int ParseIndex(object? parameter, int fallback = 0) => parameter switch
    {
        int i => i,
        long l => (int)l,
        string s when int.TryParse(s, out var tab) => tab,
        _ => fallback
    };
}

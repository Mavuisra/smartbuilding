using System.Windows;
using System.Windows.Media;

namespace SmartBuilding.Desktop.WPF.Services;

/// <summary>
/// Met à jour les brushes du thème SBMS dans le dictionnaire fusionné (SbmsTheme.xaml),
/// afin que StaticResource et DynamicResource voient les changements immédiatement.
/// </summary>
public static class ThemeResourceHelper
{
    public static ResourceDictionary GetSbmsThemeDictionary()
    {
        if (System.Windows.Application.Current?.Resources is not ResourceDictionary root)
            return new ResourceDictionary();

        foreach (var merged in root.MergedDictionaries)
        {
            if (merged.Contains("SbmsAccentGreenBrush"))
                return merged;
        }

        return root;
    }

    public static void EnsureMutableThemeBrushes()
    {
        var dict = GetSbmsThemeDictionary();
        foreach (var key in dict.Keys.Cast<object>().ToList())
        {
            switch (dict[key])
            {
                case SolidColorBrush { IsFrozen: true } frozen:
                    dict[key] = new SolidColorBrush(frozen.Color);
                    break;
                case LinearGradientBrush { IsFrozen: true } gradient:
                    dict[key] = gradient.CloneCurrentValue();
                    break;
            }
        }
    }

    public static void SetBrushColor(ResourceDictionary dict, string key, Color color)
    {
        if (dict[key] is SolidColorBrush brush && !brush.IsFrozen)
        {
            brush.Color = color;
            return;
        }

        dict[key] = new SolidColorBrush(color);
    }

    public static void ApplyMainWindowChrome(bool isDark)
    {
        if (System.Windows.Application.Current?.MainWindow is not MainWindow main)
            return;

        var pageBrush = GetBrush("SbmsPageBackgroundBrush");

        main.Background = pageBrush ?? main.Background;
        main.ShellPanel.Background = pageBrush ?? Brushes.Transparent;
    }

    public static Brush? GetBrush(string key) =>
        GetSbmsThemeDictionary()[key] as Brush;
}

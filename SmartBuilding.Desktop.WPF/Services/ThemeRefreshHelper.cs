using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
namespace SmartBuilding.Desktop.WPF.Services;

/// <summary>Force le rafraîchissement visuel après changement de ressources thème.</summary>
public static class ThemeRefreshHelper
{
    public static void RefreshOpenWindows()
    {
        if (System.Windows.Application.Current is null)
            return;

        foreach (Window window in System.Windows.Application.Current.Windows)
        {
            if (window is null)
                continue;

            RefreshResourcesOnElement(window);
            window.InvalidateMeasure();
            window.InvalidateArrange();
            window.InvalidateVisual();
        }
    }

    public static void RefreshElement(DependencyObject root)
    {
        RefreshResourcesOnElement(root);
        if (root is UIElement ui)
        {
            ui.InvalidateMeasure();
            ui.InvalidateArrange();
            ui.InvalidateVisual();
        }
    }

    private static void RefreshResourcesOnElement(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);

            if (child is FrameworkElement fe)
            {
                fe.InvalidateProperty(Control.BackgroundProperty);
                fe.InvalidateProperty(Control.ForegroundProperty);
                fe.InvalidateProperty(Control.BorderBrushProperty);
                fe.InvalidateProperty(Panel.BackgroundProperty);
                fe.InvalidateProperty(Border.BackgroundProperty);
                fe.InvalidateProperty(Border.BorderBrushProperty);
                if (fe is DataGrid grid)
                {
                    var rowHeight = ThemeResourceHelper.GetSbmsThemeDictionary()["SbmsDataGridRowHeight"];
                    grid.RowHeight = rowHeight is double h ? h : 40d;
                }

                if (fe is Button or ToggleButton)
                    fe.InvalidateProperty(Control.BackgroundProperty);
            }

            RefreshResourcesOnElement(child);
        }
    }
}

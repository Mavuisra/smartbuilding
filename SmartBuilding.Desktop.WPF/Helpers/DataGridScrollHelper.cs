using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SmartBuilding.Desktop.WPF.Helpers;

/// <summary>
/// Active le défilement horizontal sur tous les DataGrid SBMS en imposant une largeur minimale
/// basée sur les colonnes (y compris les colonnes en étoile).
/// </summary>
public static class DataGridScrollHelper
{
    private const double DefaultStarMinWidth = 96;
    private const double DefaultAutoMinWidth = 80;
    private const double GridChromePadding = 20;

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(DataGridScrollHelper),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    public static void Refresh(DataGrid grid)
    {
        if (!GetIsEnabled(grid))
            return;

        Apply(grid);
    }

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid)
            return;

        if ((bool)e.NewValue)
            Attach(grid);
        else
            Detach(grid);
    }

    private static void Attach(DataGrid grid)
    {
        grid.Loaded -= OnGridLoaded;
        grid.SizeChanged -= OnGridSizeChanged;
        grid.Loaded += OnGridLoaded;
        grid.SizeChanged += OnGridSizeChanged;

        if (grid.IsLoaded)
            Apply(grid);
    }

    private static void Detach(DataGrid grid)
    {
        grid.Loaded -= OnGridLoaded;
        grid.SizeChanged -= OnGridSizeChanged;
    }

    private static void OnGridLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid grid)
            Apply(grid);
    }

    private static void OnGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is DataGrid grid)
            Apply(grid);
    }

    private static void Apply(DataGrid grid)
    {
        EnsureColumnMinWidths(grid);
        var minWidth = ComputeMinWidth(grid);
        grid.MinWidth = minWidth;
        grid.HorizontalAlignment = HorizontalAlignment.Left;
        EnsureInternalScrollViewer(grid);

        if (VisualTreeHelper.GetParent(grid) is ScrollViewer parentScroll)
        {
            parentScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            parentScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        }
    }

    private static void EnsureColumnMinWidths(DataGrid grid)
    {
        foreach (var column in grid.Columns)
        {
            if (column.Width.IsStar && column.MinWidth < 1)
                column.MinWidth = DefaultStarMinWidth;
            else if (column.Width.IsAuto && column.MinWidth < 1)
                column.MinWidth = DefaultAutoMinWidth;
        }
    }

    private static double ComputeMinWidth(DataGrid grid)
    {
        var total = 0.0;
        foreach (var column in grid.Columns)
        {
            if (column.Visibility != Visibility.Visible)
                continue;

            var width = column.Width;
            if (width.IsAbsolute)
                total += width.Value;
            else if (width.IsStar)
                total += Math.Max(column.MinWidth, grid.MinColumnWidth);
            else
                total += column.ActualWidth > 1
                    ? column.ActualWidth
                    : Math.Max(column.MinWidth, DefaultAutoMinWidth);
        }

        return Math.Max(total + GridChromePadding, 320);
    }

    private static void EnsureInternalScrollViewer(DataGrid grid)
    {
        var scrollViewer = FindScrollViewer(grid);
        if (scrollViewer is null)
            return;

        scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        scrollViewer.CanContentScroll = true;
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer sv)
            return sv;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            var found = FindScrollViewer(child);
            if (found is not null)
                return found;
        }

        return null;
    }
}

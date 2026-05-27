namespace SmartBuilding.Desktop.WPF.Helpers;

/// <summary>
/// Points de rupture pour la page Personnel (KPI, filtres, panneau détail, colonnes du tableau).
/// </summary>
public sealed class ResponsiveLayoutMetrics
{
    public int KpiColumns { get; init; } = 6;
    public bool IsCompactHeader { get; init; }
    public bool UseCompactFilters { get; init; }
    public bool ShowDetailSidebar { get; init; }
    public bool ShowDetailStacked { get; init; }
    public bool ShowDetailOverlay { get; init; }
    public double DetailSidebarWidth { get; init; } = 360;

    /// <summary>Toutes les colonnes visibles ; le défilement horizontal gère l'espace restreint.</summary>
    public bool ShowColumnMatricule { get; init; } = true;
    public bool ShowColumnPhone { get; init; } = true;
    public bool ShowColumnHireDate { get; init; } = true;
    public bool ShowColumnPresence { get; init; } = true;
    public bool ShowColumnDepartment { get; init; } = true;
    public bool ShowCompactEmployeeCell { get; init; }
    public bool UseHorizontalTableScroll { get; init; } = true;

    public static ResponsiveLayoutMetrics FromWidth(double width, bool detailOpen)
    {
        width = Math.Max(width, 320);
        var sidebar = detailOpen ? ResolveSidebarWidth(width) : 0;
        var tableWidth = width - sidebar - 80;
        var needsScroll = tableWidth < 1320;

        if (width < 880)
        {
            return new ResponsiveLayoutMetrics
            {
                KpiColumns = 2,
                IsCompactHeader = true,
                UseCompactFilters = true,
                ShowDetailOverlay = detailOpen,
                ShowDetailSidebar = false,
                ShowDetailStacked = false,
                DetailSidebarWidth = 0,
                ShowCompactEmployeeCell = true,
                UseHorizontalTableScroll = true
            };
        }

        if (width < 1180)
        {
            return new ResponsiveLayoutMetrics
            {
                KpiColumns = 3,
                IsCompactHeader = true,
                UseCompactFilters = true,
                ShowDetailStacked = detailOpen,
                ShowDetailSidebar = false,
                ShowDetailOverlay = false,
                DetailSidebarWidth = 0,
                ShowCompactEmployeeCell = true,
                UseHorizontalTableScroll = true
            };
        }

        if (width < 1420)
        {
            return new ResponsiveLayoutMetrics
            {
                KpiColumns = 4,
                IsCompactHeader = true,
                UseCompactFilters = tableWidth < 760,
                ShowDetailSidebar = detailOpen,
                ShowDetailStacked = false,
                DetailSidebarWidth = sidebar,
                ShowCompactEmployeeCell = detailOpen || tableWidth < 900,
                UseHorizontalTableScroll = needsScroll
            };
        }

        if (detailOpen && tableWidth < 920)
        {
            return new ResponsiveLayoutMetrics
            {
                KpiColumns = width >= 1600 ? 6 : 5,
                UseCompactFilters = tableWidth < 860,
                ShowDetailSidebar = true,
                DetailSidebarWidth = sidebar,
                ShowCompactEmployeeCell = true,
                UseHorizontalTableScroll = true
            };
        }

        return new ResponsiveLayoutMetrics
        {
            KpiColumns = width >= 1680 ? 6 : width >= 1400 ? 5 : 4,
            UseCompactFilters = tableWidth < 900,
            ShowDetailSidebar = detailOpen,
            DetailSidebarWidth = sidebar,
            ShowCompactEmployeeCell = detailOpen && tableWidth < 950,
            UseHorizontalTableScroll = needsScroll
        };
    }

    private static double ResolveSidebarWidth(double viewWidth) =>
        viewWidth >= 1180 ? 280 : 0;
}

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Domain.Entities.Inventory;
using SmartBuilding.Desktop.WPF.Helpers;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class InventoryViewModel : BaseViewModel
{
    private readonly InventoryService _inventoryService;
    private readonly ISyncService _syncService;
    private List<InventoryListItem> _allItems = [];

    public const string AllCategories = "Toutes catégories";
    public const string AllLocations = "Tous emplacements";
    public const string AllStatuses = "Tous états";
    public const string AllMaintenance = "Toutes maintenances";
    public const string AllBuildings = "Tous bâtiments";

    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _userRole = string.Empty;
    [ObservableProperty] private string _userInitials = "AD";
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _tableSearchQuery = string.Empty;
    [ObservableProperty] private string _filterCategory = AllCategories;
    [ObservableProperty] private string _filterLocation = AllLocations;
    [ObservableProperty] private string _filterStatus = AllStatuses;
    [ObservableProperty] private string _filterMaintenance = AllMaintenance;
    [ObservableProperty] private string _filterBuilding = AllBuildings;
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private string _paginationText = string.Empty;
    [ObservableProperty] private int _notificationCount = 6;
    [ObservableProperty] private int _pageSize = 10;
    [ObservableProperty] private int _filteredTotal;
    [ObservableProperty] private bool _isDetailPanelOpen;
    [ObservableProperty] private int _selectedDetailTab;
    [ObservableProperty] private bool _isAddFormOpen;
    [ObservableProperty] private InventoryListItem? _selectedItem;

    [ObservableProperty] private int _totalItems;
    [ObservableProperty] private int _operationalCount;
    [ObservableProperty] private string _operationalPercent = "0%";
    [ObservableProperty] private int _maintenanceCount;
    [ObservableProperty] private int _outOfServiceCount;
    [ObservableProperty] private int _criticalCount;
    [ObservableProperty] private string _totalValueDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _availableBalanceDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private int _interventionsThisMonth;

    [ObservableProperty] private string _formCode = string.Empty;
    [ObservableProperty] private string _formName = string.Empty;
    [ObservableProperty] private string _formCategory = "Équipement bâtiment";
    [ObservableProperty] private string _formLocation = "Salle technique";
    [ObservableProperty] private string _formResponsible = string.Empty;
    [ObservableProperty] private string? _formError;

    [ObservableProperty] private ISeries[] _categoryPieSeries = [];
    [ObservableProperty] private ISeries[] _maintenanceCostSeries = [];
    [ObservableProperty] private ISeries[] _criticalBarSeries = [];
    [ObservableProperty] private ISeries[] _interventionHistorySeries = [];

    public ObservableCollection<InventoryListItem> Items { get; } = [];
    public ObservableCollection<InventoryAlertItem> Alerts { get; } = [];
    public ObservableCollection<string> CategoryFilters { get; } = [AllCategories];
    public ObservableCollection<string> LocationFilters { get; } = [AllLocations];
    public ObservableCollection<string> StatusFilters { get; } = [AllStatuses, "Opérationnel", "Maintenance", "Hors service", "Critique"];
    public ObservableCollection<string> MaintenanceFilters { get; } = [AllMaintenance, "Maintenance due", "En retard", "Planifiée (14j)"];
    public ObservableCollection<string> BuildingFilters { get; } = [AllBuildings];
    public ObservableCollection<int> PageSizeOptions { get; } = [10, 20, 50];
    public ObservableCollection<string> ItemCategories { get; } =
    [
        "Équipement bâtiment", "Matériel technique", "Outils maintenance", "Mobilier administratif",
        "Équipement sécurité", "Équipement informatique", "Consommables internes", "Matériel électrique",
        "Équipement plomberie", "Climatisation", "Générateur", "Caméras sécurité", "Matériel réseau",
        "Électricité", "Sécurité", "Informatique", "Réseau", "Ascenseur", "Nettoyage"
    ];
    public ObservableCollection<string> ItemLocations { get; } =
    [
        "Salle technique", "Parking", "Réception", "Bureau administratif",
        "Sous-sol", "Hall principal", "Toiture", "Réserve", "RDC — Local technique"
    ];

    public InventoryViewModel(
        InventoryService inventoryService,
        ISyncService syncService,
        AppConfigurationService appConfiguration,
        SessionService session)
    {
        _inventoryService = inventoryService;
        _syncService = syncService;
        appConfiguration.ConfigurationChanged += (_, _) => _ = LoadAsync();
        UserName = session.CurrentUser?.FullName ?? "Admin Principal";
        UserRole = session.CurrentUser?.Role ?? "Administrateur";
        UserInitials = GetInitials(UserName);
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var data = await _inventoryService.LoadAsync();
            _allItems = data.Items.ToList();

            TotalItems = data.TotalItems;
            OperationalCount = data.OperationalCount;
            MaintenanceCount = data.MaintenanceCount;
            OutOfServiceCount = data.OutOfServiceCount;
            CriticalCount = data.CriticalCount;
            OperationalPercent = data.OperationalPercent;
            TotalValueDisplay = Fc(data.TotalValue);
            AvailableBalanceDisplay = MoneyFormatter.Format(data.AvailableBalance);
            InterventionsThisMonth = data.InterventionsThisMonth;
            NotificationCount = data.Alerts.Count(a => a.Title != "Parc en bon état");

            Alerts.Clear();
            foreach (var a in data.Alerts) Alerts.Add(a);

            CategoryFilters.Clear();
            CategoryFilters.Add(AllCategories);
            foreach (var c in _allItems.Select(i => i.Category).Distinct().OrderBy(x => x)) CategoryFilters.Add(c);

            LocationFilters.Clear();
            LocationFilters.Add(AllLocations);
            foreach (var l in _allItems.Select(i => i.Location).Where(x => x != "—").Distinct().OrderBy(x => x)) LocationFilters.Add(l);

            BuildingFilters.Clear();
            BuildingFilters.Add(AllBuildings);
            foreach (var b in _allItems.Select(i => i.Building).Where(x => x != "—").Distinct().OrderBy(x => x)) BuildingFilters.Add(b);

            FilterCategory = PageFilterHelper.RestoreSelection(FilterCategory, CategoryFilters, AllCategories);
            FilterLocation = PageFilterHelper.RestoreSelection(FilterLocation, LocationFilters, AllLocations);
            FilterBuilding = PageFilterHelper.RestoreSelection(FilterBuilding, BuildingFilters, AllBuildings);
            FilterStatus = PageFilterHelper.RestoreSelection(FilterStatus, StatusFilters, AllStatuses);
            FilterMaintenance = PageFilterHelper.RestoreSelection(FilterMaintenance, MaintenanceFilters, AllMaintenance);

            BuildCharts(data);
            CurrentPage = 1;
            ApplyFilters();
            if (SelectedItem is null || !_allItems.Any(i => i.Id == SelectedItem.Id))
                SelectedItem = Items.FirstOrDefault();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand] private void CloseDetailPanel() { IsDetailPanelOpen = false; SelectedItem = null; }

    [RelayCommand]
    private void SetDetailTab(object? parameter) => SelectedDetailTab = TabNavigationHelper.ParseIndex(parameter);

    [RelayCommand]
    private void OpenAddForm()
    {
        FormCode = $"INV-{DateTime.Today:yyyyMM}-{_allItems.Count + 1:D3}";
        FormName = string.Empty;
        FormCategory = "Équipement bâtiment";
        FormLocation = "Salle technique";
        FormResponsible = "Paul Ngoy";
        FormError = null;
        IsAddFormOpen = true;
    }

    [RelayCommand] private void CloseAddForm() => IsAddFormOpen = false;

    [RelayCommand]
    private async Task SaveItemAsync()
    {
        FormError = null;
        IsBusy = true;
        try
        {
            var error = await _inventoryService.CreateItemAsync(new InventoryItem
            {
                Code = FormCode,
                Name = FormName,
                Category = FormCategory,
                Location = FormLocation,
                Building = "Tour SBMS",
                Responsible = FormResponsible,
                Status = "Opérationnel",
                Quantity = 1,
                UnitValue = 0,
                LastMaintenanceDate = DateTime.Today.AddMonths(-2),
                NextMaintenanceDate = DateTime.Today.AddMonths(4)
            });

            if (!string.IsNullOrEmpty(error)) { FormError = error; return; }

            IsAddFormOpen = false;
            StatusMessage = "Équipement inventorié.";
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private void ExportCsv()
    {
        StatusMessage = $"Export : {InventoryExportService.ExportCsv(_allItems)}";
    }

    [RelayCommand]
    private async Task SyncAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _syncService.SyncAsync(manual: true);
            StatusMessage = result.Success ? $"Sync OK — {result.Pushed}/{result.Pulled}" : $"Échec : {result.Error}";
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand] private void PreviousPage() { if (CurrentPage > 1) { CurrentPage--; ApplyFilters(); } }
    [RelayCommand] private void NextPage() { if (CurrentPage < TotalPages) { CurrentPage++; ApplyFilters(); } }

    partial void OnSelectedItemChanged(InventoryListItem? value) => IsDetailPanelOpen = value is not null;
    partial void OnSearchQueryChanged(string value) => ResetPageAndFilter();
    partial void OnTableSearchQueryChanged(string value) => ResetPageAndFilter();
    partial void OnFilterCategoryChanged(string value) => ResetPageAndFilter();
    partial void OnFilterLocationChanged(string value) => ResetPageAndFilter();
    partial void OnFilterStatusChanged(string value) => ResetPageAndFilter();
    partial void OnFilterMaintenanceChanged(string value) => ResetPageAndFilter();
    partial void OnFilterBuildingChanged(string value) => ResetPageAndFilter();
    partial void OnPageSizeChanged(int value) => ResetPageAndFilter();

    private void ResetPageAndFilter() { CurrentPage = 1; ApplyFilters(); }

    private void ApplyFilters()
    {
        var today = DateTime.Today;
        var query = $"{SearchQuery} {TableSearchQuery}".Trim();

        var filtered = _allItems.Where(i =>
            PageFilterHelper.Matches(FilterCategory, AllCategories, i.Category) &&
            PageFilterHelper.Matches(FilterLocation, AllLocations, i.Location) &&
            PageFilterHelper.Matches(FilterStatus, AllStatuses, i.StatusLabel) &&
            PageFilterHelper.Matches(FilterBuilding, AllBuildings, i.Building) &&
            MatchesMaintenanceFilter(i, today) &&
            (string.IsNullOrWhiteSpace(query) ||
             i.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             i.Code.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             i.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             i.Responsible.Contains(query, StringComparison.OrdinalIgnoreCase)));

        var list = filtered.ToList();
        FilteredTotal = list.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling(list.Count / (double)PageSize));
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;

        var skip = (CurrentPage - 1) * PageSize;
        Items.Clear();
        foreach (var i in list.Skip(skip).Take(PageSize)) Items.Add(i);

        var start = list.Count == 0 ? 0 : skip + 1;
        PaginationText = $"Affichage de {start} à {skip + Items.Count} sur {list.Count} équipement(s)";
    }

    private bool MatchesMaintenanceFilter(InventoryListItem i, DateTime today)
    {
        if (PageFilterHelper.IsAll(FilterMaintenance, AllMaintenance)) return true;
        if (FilterMaintenance == "En retard")
            return DateTime.TryParseExact(i.NextMaintenanceDisplay, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) && d < today;
        if (FilterMaintenance == "Maintenance due")
            return i.StatusLabel is "Maintenance" or "Critique";
        if (FilterMaintenance == "Planifiée (14j)")
            return DateTime.TryParseExact(i.NextMaintenanceDisplay, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var p) && p >= today && p <= today.AddDays(14);
        return true;
    }

    private void BuildCharts(InventoryPageData data)
    {
        var palette = new[] { "#2D6A4F", "#2563EB", "#EA580C", "#6D28D9", "#DC2626", "#0F766E", "#64748B", "#B45309" };
        CategoryPieSeries = data.CategoryDistribution.Select((s, i) => new PieSeries<int>
        {
            Name = s.Category,
            Values = [s.Count],
            Fill = new SolidColorPaint(SKColor.Parse(palette[i % palette.Length]))
        }).Cast<ISeries>().ToArray();

        MaintenanceCostSeries =
        [
            new LineSeries<decimal>
            {
                Name = "Coûts maintenance",
                Values = data.MaintenanceCostTrend.Select(p => p.Cost).ToArray(),
                Stroke = new SolidColorPaint(SKColor.Parse("#2D6A4F")) { StrokeThickness = 2 },
                Fill = null,
                GeometrySize = 5
            }
        ];

        CriticalBarSeries = data.CriticalByStatus.Select(s => new ColumnSeries<int>
        {
            Name = s.Status,
            Values = [s.Count],
            Fill = new SolidColorPaint(SKColor.Parse(s.Status switch
            {
                "Critique" => "#B45309",
                "Hors service" => "#DC2626",
                _ => "#EA580C"
            }))
        }).Cast<ISeries>().ToArray();

        InterventionHistorySeries =
        [
            new ColumnSeries<int>
            {
                Name = "Interventions",
                Values = data.InterventionHistory.Select(p => p.Count).ToArray(),
                Fill = new SolidColorPaint(SKColor.Parse("#2563EB"))
            }
        ];
    }

    private static string Fc(decimal amount) => MoneyFormatter.Format(amount);

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant() : name.Length >= 2 ? name[..2].ToUpperInvariant() : "AD";
    }
}

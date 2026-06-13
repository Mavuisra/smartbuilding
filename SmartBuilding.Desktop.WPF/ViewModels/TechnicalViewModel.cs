using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Domain.Entities.Technical;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Desktop.WPF.Helpers;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class TechnicalViewModel : BaseViewModel
{
    private readonly TechnicalService _technicalService;
    private readonly IncidentsViewModel _incidents;
    private readonly ISyncService _syncService;
    private List<TechnicalEquipmentItem> _allEquipment = [];

    public IncidentsViewModel Incidents => _incidents;

    public const string AllCategories = "Toutes catégories";
    public const string AllStatuses = "Tous statuts";
    public const string AllLocations = "Tous emplacements";

    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _userRole = string.Empty;
    [ObservableProperty] private string _userInitials = "AD";
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _tableSearchQuery = string.Empty;
    [ObservableProperty] private string _filterCategory = AllCategories;
    [ObservableProperty] private string _filterStatus = AllStatuses;
    [ObservableProperty] private string _filterLocation = AllLocations;
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private string _paginationText = string.Empty;
    [ObservableProperty] private int _notificationCount = 6;
    [ObservableProperty] private int _pageSize = 10;
    [ObservableProperty] private int _filteredTotal;
    [ObservableProperty] private bool _isDetailPanelOpen;
    [ObservableProperty] private int _selectedDetailTab;
    [ObservableProperty] private bool _isAddFormOpen;
    [ObservableProperty] private TechnicalEquipmentItem? _selectedEquipment;
    [ObservableProperty] private int _selectedSectionTab;
    [ObservableProperty] private string _sectionSubtitle = string.Empty;
    [ObservableProperty] private string _searchHint = "Rechercher équipement, incident, emplacement...";
    [ObservableProperty] private string _securityStatusLabel = "Sécurité normale";
    [ObservableProperty] private string _securityStatusColor = "#166534";
    [ObservableProperty] private string _syncStatusLabel = "Hors ligne";
    [ObservableProperty] private string _lastSyncDisplay = "Dernière sync : —";
    [ObservableProperty] private int _openIncidentsCount;
    [ObservableProperty] private int _criticalIncidentsCount;
    [ObservableProperty] private string _brandCompanyName = BuildingInfoDefaults.CompanyName;

    [ObservableProperty] private int _totalEquipment;
    [ObservableProperty] private int _operationalCount;
    [ObservableProperty] private string _operationalPercent = "0%";
    [ObservableProperty] private int _maintenanceCount;
    [ObservableProperty] private string _maintenancePercent = "0%";
    [ObservableProperty] private int _brokenCount;
    [ObservableProperty] private string _brokenPercent = "0%";
    [ObservableProperty] private string _monthlyMaintenanceDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _availableBalanceDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private int _plannedThisWeek;

    [ObservableProperty] private string _formCode = string.Empty;
    [ObservableProperty] private string _formName = string.Empty;
    [ObservableProperty] private string _formCategory = "Électricité";
    [ObservableProperty] private string _formLocation = string.Empty;
    [ObservableProperty] private string _formBrand = string.Empty;
    [ObservableProperty] private string _formPurchasePriceText = string.Empty;
    [ObservableProperty] private DateTime? _formPurchaseDate = DateTime.Today;
    [ObservableProperty] private string _formStatus = "Opérationnel";
    [ObservableProperty] private string? _formError;

    [ObservableProperty] private ISeries[] _categoryPieSeries = [];
    [ObservableProperty] private ISeries[] _statusPieSeries = [];
    [ObservableProperty] private ISeries[] _maintenanceCostSeries = [];

    public ObservableCollection<TechnicalEquipmentItem> Equipment { get; } = [];
    public ObservableCollection<string> CategoryFilters { get; } = [AllCategories];
    public ObservableCollection<string> StatusFilters { get; } = [AllStatuses, "Opérationnel", "En maintenance", "En panne", "Hors service"];
    public ObservableCollection<string> LocationFilters { get; } = [AllLocations];
    public ObservableCollection<int> PageSizeOptions { get; } = [10, 20, 50];
    public ObservableCollection<string> EquipmentCategories { get; } =
        ["Électricité", "Climatisation", "Plomberie", "Sécurité", "Ascenseur", "Autre"];
    public ObservableCollection<string> EquipmentStatusOptions { get; } =
        ["Opérationnel", "En maintenance", "En panne", "Hors service"];

    public ObservableCollection<string> SectionTabs { get; } = ["Équipements", "Incidents & Sécurité"];

    public TechnicalViewModel(
        TechnicalService technicalService,
        IncidentsViewModel incidentsViewModel,
        ISyncService syncService,
        AppConfigurationService appConfiguration,
        SessionService session)
    {
        _technicalService = technicalService;
        _incidents = incidentsViewModel;
        BrandCompanyName = appConfiguration.Current.CompanyName;
        appConfiguration.ConfigurationChanged += (_, _) =>
        {
            BrandCompanyName = appConfiguration.Current.CompanyName;
            _ = LoadAsync();
        };
        _incidents.IsEmbedded = true;
        _syncService = syncService;
        UserName = session.CurrentUser?.FullName ?? "Admin Principal";
        UserRole = session.CurrentUser?.Role ?? "Administrateur";
        UserInitials = GetInitials(UserName);
        UpdateSyncStatus();
        UpdateSectionChrome();
    }

    public void NavigateToSection(int tabIndex) => SelectedSectionTab = tabIndex;

    [RelayCommand]
    private void SetSectionTab(object? parameter) => SelectedSectionTab = TabNavigationHelper.ParseIndex(parameter);

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            await _incidents.LoadCommand.ExecuteAsync(null);
            SecurityStatusLabel = _incidents.SecurityStatusLabel;
            SecurityStatusColor = _incidents.SecurityStatusColor;
            OpenIncidentsCount = _incidents.OpenIncidentsCount;
            CriticalIncidentsCount = _incidents.CriticalCount;
            NotificationCount = _incidents.NotificationCount + (MaintenanceCount > 0 ? 2 : 0);
            UpdateSectionChrome();

            var data = await _technicalService.LoadAsync();
            _allEquipment = data.Equipment.ToList();

            TotalEquipment = data.TotalEquipment;
            OperationalCount = data.OperationalCount;
            MaintenanceCount = data.MaintenanceCount;
            BrokenCount = data.BrokenCount;
            OperationalPercent = data.OperationalPercent;
            MaintenancePercent = data.MaintenancePercent;
            BrokenPercent = data.BrokenPercent;
            MonthlyMaintenanceDisplay = Fc(data.MonthlyMaintenanceCost);
            AvailableBalanceDisplay = MoneyFormatter.Format(data.AvailableBalance);
            PlannedThisWeek = data.PlannedThisWeek;

            CategoryFilters.Clear();
            CategoryFilters.Add(AllCategories);
            foreach (var c in _allEquipment.Select(e => e.Category).Distinct().OrderBy(x => x))
                CategoryFilters.Add(c);

            LocationFilters.Clear();
            LocationFilters.Add(AllLocations);
            foreach (var l in _allEquipment.Select(e => e.Location).Where(x => x != "—").Distinct().OrderBy(x => x))
                LocationFilters.Add(l);

            FilterCategory = PageFilterHelper.RestoreSelection(FilterCategory, CategoryFilters, AllCategories);
            FilterLocation = PageFilterHelper.RestoreSelection(FilterLocation, LocationFilters, AllLocations);
            FilterStatus = PageFilterHelper.RestoreSelection(FilterStatus, StatusFilters, AllStatuses);

            BuildCharts(data);
            CurrentPage = 1;
            ApplyFilters();
            if (SelectedEquipment is not null)
            {
                var selectedId = SelectedEquipment.Id;
                SelectedEquipment = _allEquipment.FirstOrDefault(e => e.Id == selectedId)
                    ?? Equipment.FirstOrDefault(e => e.Id == selectedId);
            }
            else
                SelectedEquipment = Equipment.FirstOrDefault();

            UpdateSyncStatus();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectEquipment(TechnicalEquipmentItem? item)
    {
        if (item is not null)
        {
            SelectedEquipment = item;
            SelectedDetailTab = 0;
        }
    }

    [RelayCommand]
    private void CloseDetailPanel()
    {
        IsDetailPanelOpen = false;
        SelectedEquipment = null;
    }

    [RelayCommand]
    private void SetDetailTab(object? parameter) => SelectedDetailTab = TabNavigationHelper.ParseIndex(parameter);

    [RelayCommand]
    private void OpenAddForm()
    {
        FormCode = $"EQ-{DateTime.Today:yyyyMM}-{_allEquipment.Count + 1:D3}";
        FormName = string.Empty;
        FormCategory = "Électricité";
        FormLocation = "RDC — Local technique";
        FormBrand = string.Empty;
        FormPurchasePriceText = string.Empty;
        FormPurchaseDate = DateTime.Today;
        FormStatus = "Opérationnel";
        FormError = null;
        IsAddFormOpen = true;
    }

    [RelayCommand]
    private void CloseAddForm() => IsAddFormOpen = false;

    [RelayCommand]
    private async Task SaveEquipmentAsync()
    {
        FormError = null;

        decimal purchaseValue = 0;
        if (!string.IsNullOrWhiteSpace(FormPurchasePriceText))
        {
            if (!decimal.TryParse(FormPurchasePriceText.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out purchaseValue)
                || purchaseValue < 0)
            {
                FormError = "Prix d'achat invalide.";
                return;
            }
        }

        IsBusy = true;
        try
        {
            var error = await _technicalService.CreateEquipmentAsync(new Equipment
            {
                Code = FormCode,
                Name = FormName,
                Category = FormCategory,
                Location = FormLocation,
                Brand = FormBrand,
                PurchaseValue = purchaseValue,
                InstallationDate = FormPurchaseDate,
                Status = ParseEquipmentStatus(FormStatus),
                LastMaintenanceDate = DateTime.Today.AddMonths(-3),
                NextMaintenanceDate = DateTime.Today.AddMonths(3)
            });

            if (!string.IsNullOrEmpty(error))
            {
                FormError = error;
                return;
            }

            IsAddFormOpen = false;
            StatusMessage = "Équipement enregistré.";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private async Task SyncAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _syncService.SyncAsync(manual: true);
            StatusMessage = result.Success
                ? $"Sync OK — {result.Pushed} envoyés, {result.Pulled} reçus"
                : $"Échec : {result.Error}";
            UpdateSyncStatus();
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UpdateSyncStatus()
    {
        if (_syncService.LastSyncAt.HasValue)
        {
            LastSyncDisplay = $"Dernière sync : {_syncService.LastSyncAt.Value:dd/MM/yyyy HH:mm}";
            SyncStatusLabel = "À jour";
        }
        else
        {
            LastSyncDisplay = "Dernière sync : jamais";
            SyncStatusLabel = "Hors ligne";
        }
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CurrentPage > 1) { CurrentPage--; ApplyFilters(); }
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage < TotalPages) { CurrentPage++; ApplyFilters(); }
    }

    partial void OnSelectedEquipmentChanged(TechnicalEquipmentItem? value) =>
        IsDetailPanelOpen = value is not null;

    partial void OnSearchQueryChanged(string value)
    {
        _incidents.SearchQuery = value;
        ResetPageAndFilter();
    }

    partial void OnSelectedSectionTabChanged(int value)
    {
        UpdateSectionChrome();
        if (value == 1)
            _incidents.SearchQuery = SearchQuery;
    }

    private void UpdateSectionChrome()
    {
        if (SelectedSectionTab == 1)
        {
            SectionSubtitle = "Surveillance, alertes et suivi des interventions";
            SearchHint = "Rechercher incident, emplacement, ID...";
            return;
        }

        SectionSubtitle = $"{TotalEquipment} équipements · {OpenIncidentsCount} incidents ouverts";
        SearchHint = "Rechercher équipement, incident, emplacement...";
    }
    partial void OnTableSearchQueryChanged(string value) => ResetPageAndFilter();
    partial void OnFilterCategoryChanged(string value) => ResetPageAndFilter();
    partial void OnFilterStatusChanged(string value) => ResetPageAndFilter();
    partial void OnFilterLocationChanged(string value) => ResetPageAndFilter();
    partial void OnPageSizeChanged(int value) => ResetPageAndFilter();

    private void ResetPageAndFilter()
    {
        CurrentPage = 1;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var query = $"{SearchQuery} {TableSearchQuery}".Trim();
        var filtered = _allEquipment.Where(e =>
            PageFilterHelper.Matches(FilterCategory, AllCategories, e.Category) &&
            PageFilterHelper.Matches(FilterStatus, AllStatuses, e.StatusLabel) &&
            PageFilterHelper.Matches(FilterLocation, AllLocations, e.Location) &&
            (string.IsNullOrWhiteSpace(query) ||
             e.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             e.Code.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             e.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             e.Location.Contains(query, StringComparison.OrdinalIgnoreCase)));

        var list = filtered.ToList();
        FilteredTotal = list.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling(list.Count / (double)PageSize));
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;

        var skip = (CurrentPage - 1) * PageSize;
        var page = list.Skip(skip).Take(PageSize).ToList();

        Equipment.Clear();
        foreach (var e in page) Equipment.Add(e);

        var start = list.Count == 0 ? 0 : skip + 1;
        var end = skip + page.Count;
        PaginationText = $"Affichage de {start} à {end} sur {list.Count} équipement(s)";
    }

    private void BuildCharts(TechnicalPageData data)
    {
        var palette = new[] { "#2D6A4F", "#40916C", "#2563EB", "#EA580C", "#7B2CBF", "#64748B", "#DC2626" };
        CategoryPieSeries = data.CategoryDistribution.Select((s, i) => new PieSeries<int>
        {
            Name = s.Category,
            Values = [s.Count],
            Fill = new SolidColorPaint(SKColor.Parse(palette[i % palette.Length]))
        }).Cast<ISeries>().ToArray();

        var statusColors = new Dictionary<string, string>
        {
            ["Opérationnels"] = "#2D6A4F",
            ["En maintenance"] = "#EA580C",
            ["En panne"] = "#DC2626",
            ["Hors service"] = "#94A3B8"
        };
        StatusPieSeries = data.StatusDistribution.Select(s => new PieSeries<int>
        {
            Name = s.Status,
            Values = [s.Count],
            Fill = new SolidColorPaint(SKColor.Parse(statusColors.GetValueOrDefault(s.Status, "#64748B")))
        }).Cast<ISeries>().ToArray();

        MaintenanceCostSeries =
        [
            new ColumnSeries<decimal>
            {
                Name = "Coût maintenance",
                Values = data.MaintenanceCostTrend.Select(p => p.Cost).ToArray(),
                Fill = new SolidColorPaint(SKColor.Parse("#2D6A4F"))
            }
        ];
    }

    private static string Fc(decimal amount) => MoneyFormatter.Format(amount);

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
            : name.Length >= 2 ? name[..2].ToUpperInvariant() : "AD";
    }

    private static EquipmentStatus ParseEquipmentStatus(string label) => label switch
    {
        "En maintenance" => EquipmentStatus.Maintenance,
        "En panne" => EquipmentStatus.EnPanne,
        "Hors service" => EquipmentStatus.HorsService,
        _ => EquipmentStatus.Operationnel
    };
}

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
using SmartBuilding.Domain.Entities.Incidents;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Desktop.WPF.Helpers;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class IncidentsViewModel : BaseViewModel
{
    private readonly IncidentsService _incidentsService;
    private readonly ISyncService _syncService;
    private List<IncidentListItem> _allIncidents = [];

    public const string AllTypes = "Tous types";
    public const string AllSeverities = "Toutes gravités";
    public const string AllStatuses = "Tous statuts";
    public const string AllBuildings = "Tous bâtiments";
    public const string AllPeriods = "Toute période";

    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _userRole = string.Empty;
    [ObservableProperty] private string _userInitials = "AD";
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _tableSearchQuery = string.Empty;
    [ObservableProperty] private string _filterType = AllTypes;
    [ObservableProperty] private string _filterSeverity = AllSeverities;
    [ObservableProperty] private string _filterStatus = AllStatuses;
    [ObservableProperty] private string _filterBuilding = AllBuildings;
    [ObservableProperty] private string _filterPeriod = AllPeriods;
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private string _paginationText = string.Empty;
    [ObservableProperty] private int _notificationCount = 5;
    [ObservableProperty] private int _pageSize = 10;
    [ObservableProperty] private int _filteredTotal;
    [ObservableProperty] private bool _isDetailPanelOpen;
    [ObservableProperty] private int _selectedDetailTab;
    [ObservableProperty] private bool _isAddFormOpen;
    [ObservableProperty] private IncidentListItem? _selectedIncident;
    [ObservableProperty] private string _syncStatusLabel = "Hors ligne";
    [ObservableProperty] private string _lastSyncDisplay = "Dernière sync : —";
    [ObservableProperty] private bool _isEmbedded;
    [ObservableProperty] private string _securityStatusLabel = "Sécurité normale";
    [ObservableProperty] private string _securityStatusColor = "#166534";
    [ObservableProperty] private string _brandCompanyName = BuildingInfoDefaults.CompanyName;

    [ObservableProperty] private int _totalIncidents;
    [ObservableProperty] private int _openIncidentsCount;
    [ObservableProperty] private int _criticalCount;
    [ObservableProperty] private int _resolvedCount;
    [ObservableProperty] private int _activeSecurityAlerts;
    [ObservableProperty] private string _totalCostDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private string _availableBalanceDisplay = MoneyFormatter.ZeroDisplay;
    [ObservableProperty] private int _interventionsToday;

    [ObservableProperty] private string _formTitle = string.Empty;
    [ObservableProperty] private string _formDescription = string.Empty;
    [ObservableProperty] private string _formType = "Panne électrique";
    [ObservableProperty] private string _formLocation = "Hall principal";
    [ObservableProperty] private Guid _formEquipmentId;
    [ObservableProperty] private string _formSeverity = "Moyen";
    [ObservableProperty] private string? _formError;

    [ObservableProperty] private ISeries[] _monthlyTrendSeries = [];
    [ObservableProperty] private ISeries[] _typePieSeries = [];
    [ObservableProperty] private ISeries[] _severityBarSeries = [];
    [ObservableProperty] private ISeries[] _resolutionSeries = [];

    public ObservableCollection<IncidentListItem> Incidents { get; } = [];
    public ObservableCollection<IncidentInterventionItem> Interventions { get; } = [];
    public ObservableCollection<IncidentAlertItem> Alerts { get; } = [];
    public ObservableCollection<SecurityMonitorItem> Monitoring { get; } = [];
    public ObservableCollection<IncidentInsightLine> Insights { get; } = [];
    public ObservableCollection<IncidentEquipmentOption> EquipmentOptions { get; } = [];
    public ObservableCollection<IncidentTechnicianOption> TechnicianOptions { get; } = [];
    public ObservableCollection<string> TypeFilters { get; } = [AllTypes];
    public ObservableCollection<string> SeverityFilters { get; } = [AllSeverities, "Faible", "Moyen", "Élevé", "Critique"];
    public ObservableCollection<string> FormSeverityOptions { get; } = ["Faible", "Moyen", "Élevé", "Critique"];
    public ObservableCollection<string> StatusFilters { get; } = [AllStatuses, "En attente", "En cours", "Intervention programmée", "Résolu"];
    public ObservableCollection<string> BuildingFilters { get; } = [AllBuildings];
    public ObservableCollection<string> PeriodFilters { get; } = [AllPeriods, "Ce mois", "3 derniers mois", "12 derniers mois"];
    public ObservableCollection<int> PageSizeOptions { get; } = [10, 20, 50];
    public ObservableCollection<string> IncidentTypes { get; } =
    [
        "Incendie", "Intrusion", "Vol", "Panne électrique", "Fuite plomberie", "Problème réseau",
        "Climatisation", "Générateur", "Caméras sécurité", "Ascenseur", "Accident", "Court-circuit"
    ];
    public ObservableCollection<string> IncidentLocations { get; } =
    [
        "Parking", "Hall principal", "Salle technique", "Bureau administratif",
        "Sous-sol", "Toiture", "Réception", "Réserve", "Ascenseur"
    ];

    public IncidentsViewModel(
        IncidentsService incidentsService,
        ISyncService syncService,
        AppConfigurationService appConfiguration,
        SessionService session)
    {
        _incidentsService = incidentsService;
        _syncService = syncService;
        BrandCompanyName = appConfiguration.Current.CompanyName;
        appConfiguration.ConfigurationChanged += (_, _) =>
        {
            BrandCompanyName = appConfiguration.Current.CompanyName;
            _ = LoadAsync();
        };
        UserName = session.CurrentUser?.FullName ?? "Admin Principal";
        UserRole = session.CurrentUser?.Role ?? "Administrateur";
        UserInitials = GetInitials(UserName);
        UpdateSyncStatus();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var data = await _incidentsService.LoadAsync();
            _allIncidents = data.Incidents.ToList();

            TotalIncidents = data.TotalIncidents;
            OpenIncidentsCount = data.OpenIncidentsCount;
            CriticalCount = data.CriticalCount;
            ResolvedCount = data.ResolvedCount;
            ActiveSecurityAlerts = data.ActiveSecurityAlerts;
            TotalCostDisplay = data.TotalCostDisplay;
            AvailableBalanceDisplay = MoneyFormatter.Format(data.AvailableBalance);
            InterventionsToday = data.InterventionsToday;
            SecurityStatusLabel = data.SecurityStatusLabel;
            SecurityStatusColor = data.SecurityStatusColor;
            NotificationCount = data.ActiveSecurityAlerts;

            Alerts.Clear();
            foreach (var a in data.Alerts) Alerts.Add(a);

            Monitoring.Clear();
            foreach (var m in data.Monitoring) Monitoring.Add(m);
            EquipmentOptions.Clear();
            foreach (var e in data.EquipmentOptions) EquipmentOptions.Add(e);
            TechnicianOptions.Clear();
            foreach (var t in data.TechnicianOptions) TechnicianOptions.Add(t);

            Insights.Clear();
            foreach (var i in data.Insights) Insights.Add(i);

            Interventions.Clear();
            foreach (var iv in data.Interventions) Interventions.Add(iv);

            TypeFilters.Clear();
            TypeFilters.Add(AllTypes);
            foreach (var t in _allIncidents.Select(i => i.TypeLabel).Distinct().OrderBy(x => x)) TypeFilters.Add(t);

            BuildingFilters.Clear();
            BuildingFilters.Add(AllBuildings);
            foreach (var b in _allIncidents.Select(i => i.Building).Where(x => x != "—").Distinct().OrderBy(x => x)) BuildingFilters.Add(b);

            FilterType = PageFilterHelper.RestoreSelection(FilterType, TypeFilters, AllTypes);
            FilterSeverity = PageFilterHelper.RestoreSelection(FilterSeverity, SeverityFilters, AllSeverities);
            FilterStatus = PageFilterHelper.RestoreSelection(FilterStatus, StatusFilters, AllStatuses);
            FilterBuilding = PageFilterHelper.RestoreSelection(FilterBuilding, BuildingFilters, AllBuildings);

            BuildCharts(data);
            UpdateSyncStatus();
            CurrentPage = 1;
            ApplyFilters();
            if (SelectedIncident is not null)
            {
                var selectedId = SelectedIncident.Id;
                SelectedIncident = _allIncidents.FirstOrDefault(i => i.Id == selectedId)
                    ?? Incidents.FirstOrDefault(i => i.Id == selectedId);
            }
            else
                SelectedIncident = Incidents.FirstOrDefault();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand] private void CloseDetailPanel() { IsDetailPanelOpen = false; SelectedIncident = null; }
    [RelayCommand]
    private void SetDetailTab(object? parameter) => SelectedDetailTab = TabNavigationHelper.ParseIndex(parameter);

    [RelayCommand]
    private void OpenAddForm()
    {
        FormTitle = string.Empty;
        FormDescription = string.Empty;
        FormType = "Panne électrique";
        FormEquipmentId = EquipmentOptions.FirstOrDefault()?.Id ?? Guid.Empty;
        FormLocation = EquipmentOptions.FirstOrDefault()?.Location ?? "Hall principal";
        FormSeverity = "Moyen";
        FormError = null;
        IsAddFormOpen = true;
    }

    [RelayCommand] private void CloseAddForm() => IsAddFormOpen = false;

    [RelayCommand]
    private async Task SaveIncidentAsync()
    {
        FormError = null;
        if (string.IsNullOrWhiteSpace(FormTitle)) { FormError = "Le titre est obligatoire."; return; }
        if (FormEquipmentId == Guid.Empty) { FormError = "Sélectionnez le matériel concerné."; return; }

        var equipment = EquipmentOptions.FirstOrDefault(e => e.Id == FormEquipmentId);
        var location = string.IsNullOrWhiteSpace(FormLocation)
            ? equipment?.Location ?? "—"
            : FormLocation;

        IsBusy = true;
        try
        {
            var error = await _incidentsService.CreateIncidentAsync(new Incident
            {
                Title = FormTitle,
                Description = FormDescription,
                IncidentType = FormType,
                EquipmentId = FormEquipmentId,
                Location = location,
                Building = "Tour SBMS",
                Responsible = UserName,
                Severity = IncidentsService.ParseSeverity(FormSeverity),
                Status = IncidentStatus.Ouvert,
                ReportedAt = DateTime.Now,
                HasPhoto = false
            });
            if (!string.IsNullOrEmpty(error)) { FormError = error; return; }
            IsAddFormOpen = false;
            StatusMessage = "Incident signalé.";
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private async Task SyncAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _syncService.SyncAsync(manual: true);
            StatusMessage = result.Success ? $"Sync OK — {result.Pushed}/{result.Pulled}" : $"Échec : {result.Error}";
            UpdateSyncStatus();
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand] private void PreviousPage() { if (CurrentPage > 1) { CurrentPage--; ApplyFilters(); } }
    [RelayCommand] private void NextPage() { if (CurrentPage < TotalPages) { CurrentPage++; ApplyFilters(); } }

    partial void OnSelectedIncidentChanged(IncidentListItem? value) => IsDetailPanelOpen = value is not null;
    partial void OnSearchQueryChanged(string value) => ResetPageAndFilter();
    partial void OnTableSearchQueryChanged(string value) => ResetPageAndFilter();
    partial void OnFilterTypeChanged(string value) => ResetPageAndFilter();
    partial void OnFilterSeverityChanged(string value) => ResetPageAndFilter();
    partial void OnFilterStatusChanged(string value) => ResetPageAndFilter();
    partial void OnFilterBuildingChanged(string value) => ResetPageAndFilter();
    partial void OnFilterPeriodChanged(string value) => ResetPageAndFilter();
    partial void OnPageSizeChanged(int value) => ResetPageAndFilter();

    private void ResetPageAndFilter() { CurrentPage = 1; ApplyFilters(); }

    private void ApplyFilters()
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var query = $"{SearchQuery} {TableSearchQuery}".Trim();

        var filtered = _allIncidents.Where(i =>
            PageFilterHelper.Matches(FilterType, AllTypes, i.TypeLabel) &&
            PageFilterHelper.Matches(FilterSeverity, AllSeverities, i.SeverityLabel) &&
            PageFilterHelper.Matches(FilterStatus, AllStatuses, i.StatusLabel) &&
            PageFilterHelper.Matches(FilterBuilding, AllBuildings, i.Building) &&
            InPeriod(i.DateDisplay, monthStart, today) &&
            (string.IsNullOrWhiteSpace(query) ||
             i.Code.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             i.TypeLabel.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             i.Location.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             i.Title.Contains(query, StringComparison.OrdinalIgnoreCase)));

        var list = filtered.ToList();
        FilteredTotal = list.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling(list.Count / (double)PageSize));
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;

        var skip = (CurrentPage - 1) * PageSize;
        Incidents.Clear();
        foreach (var i in list.Skip(skip).Take(PageSize)) Incidents.Add(i);

        var start = list.Count == 0 ? 0 : skip + 1;
        PaginationText = $"Affichage de {start} à {skip + Incidents.Count} sur {list.Count} incident(s)";
    }

    private bool InPeriod(string dateDisplay, DateTime monthStart, DateTime today)
    {
        if (!DateTime.TryParseExact(dateDisplay.Split(' ')[0], "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return true;

        return FilterPeriod switch
        {
            "Ce mois" => d >= monthStart,
            "3 derniers mois" => d >= monthStart.AddMonths(-2),
            "12 derniers mois" => d >= monthStart.AddMonths(-11),
            _ => true
        };
    }

    private void BuildCharts(IncidentPageData data)
    {
        var palette = new[] { "#DC2626", "#EA580C", "#B45309", "#2563EB", "#6D28D9", "#0EA5E9", "#2D6A4F", "#64748B" };

        MonthlyTrendSeries =
        [
            new LineSeries<int>
            {
                Name = "Incidents",
                Values = data.MonthlyTrend.Select(p => p.Count).ToArray(),
                Stroke = new SolidColorPaint(SKColor.Parse("#DC2626")) { StrokeThickness = 2 },
                Fill = null,
                GeometrySize = 5
            }
        ];

        TypePieSeries = data.TypeDistribution.Select((s, i) => new PieSeries<int>
        {
            Name = s.Type,
            Values = [s.Count],
            Fill = new SolidColorPaint(SKColor.Parse(palette[i % palette.Length]))
        }).Cast<ISeries>().ToArray();

        SeverityBarSeries = data.SeverityDistribution.Select(s => new ColumnSeries<int>
        {
            Name = s.Severity,
            Values = [s.Count],
            Fill = new SolidColorPaint(SKColor.Parse(s.Severity switch
            {
                "Faible" => "#166534",
                "Moyen" => "#EA580C",
                "Élevé" => "#DC2626",
                _ => "#7F1D1D"
            }))
        }).Cast<ISeries>().ToArray();

        ResolutionSeries =
        [
            new ColumnSeries<double>
            {
                Name = "Heures résolution",
                Values = data.ResolutionTrend.Select(p => p.AverageHours).ToArray(),
                Fill = new SolidColorPaint(SKColor.Parse("#2563EB"))
            }
        ];
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

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant() : name.Length >= 2 ? name[..2].ToUpperInvariant() : "AD";
    }

    partial void OnFormEquipmentIdChanged(Guid value)
    {
        var equipment = EquipmentOptions.FirstOrDefault(e => e.Id == value);
        if (equipment is null || string.IsNullOrWhiteSpace(equipment.Location) || equipment.Location == "—")
            return;

        FormLocation = equipment.Location;
        if (!IncidentLocations.Contains(equipment.Location))
            IncidentLocations.Add(equipment.Location);
    }
}

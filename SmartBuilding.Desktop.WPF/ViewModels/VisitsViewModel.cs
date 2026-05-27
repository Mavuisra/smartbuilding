using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Domain.Entities.Visitors;
using SmartBuilding.Desktop.WPF.Helpers;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class VisitsViewModel : BaseViewModel
{
    private readonly VisitsService _visitsService;
    private readonly ISyncService _syncService;
    private List<VisitListItem> _allVisits = [];

    public const string AllTypes = "Tous types";
    public const string AllStatuses = "Tous statuts";
    public const string AllBuildings = "Tous bâtiments";
    public const string AllPeriods = "Toute période";
    public const string AllHosts = "Toutes personnes";

    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _userRole = string.Empty;
    [ObservableProperty] private string _userInitials = "AD";
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _tableSearchQuery = string.Empty;
    [ObservableProperty] private string _filterType = AllTypes;
    [ObservableProperty] private string _filterStatus = AllStatuses;
    [ObservableProperty] private string _filterBuilding = AllBuildings;
    [ObservableProperty] private string _filterPeriod = AllPeriods;
    [ObservableProperty] private string _filterHost = AllHosts;
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private string _paginationText = string.Empty;
    [ObservableProperty] private int _notificationCount = 5;
    [ObservableProperty] private int _pageSize = 10;
    [ObservableProperty] private int _filteredTotal;
    [ObservableProperty] private bool _isDetailPanelOpen;
    [ObservableProperty] private int _selectedDetailTab;
    [ObservableProperty] private bool _isAddFormOpen;
    [ObservableProperty] private VisitListItem? _selectedVisit;
    [ObservableProperty] private string _syncStatusLabel = "Hors ligne";
    [ObservableProperty] private string _lastSyncDisplay = "Dernière sync : —";
    [ObservableProperty] private string _securityStatusLabel = "Accès normal";
    [ObservableProperty] private string _securityStatusColor = "#166534";

    [ObservableProperty] private int _visitorsToday;
    [ObservableProperty] private int _activeVisits;
    [ObservableProperty] private int _accessGranted;
    [ObservableProperty] private int _accessDenied;
    [ObservableProperty] private int _scheduledAppointments;
    [ObservableProperty] private int _pendingCheckouts;

    [ObservableProperty] private string _formFullName = string.Empty;
    [ObservableProperty] private string _formPhone = string.Empty;
    [ObservableProperty] private string _formHost = string.Empty;
    [ObservableProperty] private string _formPurpose = string.Empty;
    [ObservableProperty] private string _formVisitType = "Réunion";
    [ObservableProperty] private string _formZone = "Réception";
    [ObservableProperty] private string? _formError;

    [ObservableProperty] private ISeries[] _dailyTrendSeries = [];
    [ObservableProperty] private ISeries[] _typePieSeries = [];
    [ObservableProperty] private ISeries[] _accessBarSeries = [];
    [ObservableProperty] private ISeries[] _hourlySeries = [];

    public ObservableCollection<VisitListItem> Visits { get; } = [];
    public ObservableCollection<VisitAppointmentItem> Appointments { get; } = [];
    public ObservableCollection<VisitAlertItem> Alerts { get; } = [];
    public ObservableCollection<AccessZoneItem> AccessZones { get; } = [];
    public ObservableCollection<VisitInsightLine> Insights { get; } = [];
    public ObservableCollection<string> TypeFilters { get; } = [AllTypes];
    public ObservableCollection<string> StatusFilters { get; } = [AllStatuses, "Actif", "En attente", "Refusé", "Sorti"];
    public ObservableCollection<string> BuildingFilters { get; } = [AllBuildings];
    public ObservableCollection<string> PeriodFilters { get; } = [AllPeriods, "Aujourd'hui", "Cette semaine", "Ce mois"];
    public ObservableCollection<string> HostFilters { get; } = [AllHosts];
    public ObservableCollection<int> PageSizeOptions { get; } = [10, 20, 50];
    public ObservableCollection<string> VisitTypes { get; } =
        ["Réunion", "Livraison", "Maintenance", "Audit", "Prestataire", "Visite technique", "Autre"];
    public ObservableCollection<string> VisitZones { get; } =
        ["Réception", "Parking", "Bureau administratif", "Salle réunion", "Salle technique", "Hall principal", "Sous-sol", "Zone sécurisée"];

    public VisitsViewModel(VisitsService visitsService, ISyncService syncService, SessionService session)
    {
        _visitsService = visitsService;
        _syncService = syncService;
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
            var data = await _visitsService.LoadAsync();
            _allVisits = data.Visits.ToList();

            VisitorsToday = data.VisitorsToday;
            ActiveVisits = data.ActiveVisits;
            AccessGranted = data.AccessGranted;
            AccessDenied = data.AccessDenied;
            ScheduledAppointments = data.ScheduledAppointments;
            PendingCheckouts = data.PendingCheckouts;
            SecurityStatusLabel = data.SecurityStatusLabel;
            SecurityStatusColor = data.SecurityStatusColor;
            NotificationCount = data.Alerts.Count(a => a.Title != "Réception sous contrôle");

            Alerts.Clear();
            foreach (var a in data.Alerts) Alerts.Add(a);

            Appointments.Clear();
            foreach (var a in data.Appointments) Appointments.Add(a);

            AccessZones.Clear();
            foreach (var z in data.AccessZones) AccessZones.Add(z);

            Insights.Clear();
            foreach (var i in data.Insights) Insights.Add(i);

            TypeFilters.Clear();
            TypeFilters.Add(AllTypes);
            foreach (var t in _allVisits.Select(v => v.VisitType).Distinct().OrderBy(x => x)) TypeFilters.Add(t);

            HostFilters.Clear();
            HostFilters.Add(AllHosts);
            foreach (var h in _allVisits.Select(v => v.HostName).Distinct().OrderBy(x => x)) HostFilters.Add(h);

            BuildCharts(data);
            CurrentPage = 1;
            ApplyFilters();
            if (SelectedVisit is null || !_allVisits.Any(v => v.Id == SelectedVisit.Id))
                SelectedVisit = Visits.FirstOrDefault();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand] private void CloseDetailPanel() { IsDetailPanelOpen = false; SelectedVisit = null; }
    [RelayCommand] private void SetDetailTab(object? p) => SelectedDetailTab = TabNavigationHelper.ParseIndex(p);

    [RelayCommand]
    private void OpenAddForm()
    {
        FormFullName = string.Empty;
        FormPhone = string.Empty;
        FormHost = string.Empty;
        FormPurpose = string.Empty;
        FormVisitType = "Réunion";
        FormZone = "Réception";
        FormError = null;
        IsAddFormOpen = true;
    }

    [RelayCommand] private void CloseAddForm() => IsAddFormOpen = false;

    [RelayCommand]
    private async Task SaveVisitorAsync()
    {
        FormError = null;
        IsBusy = true;
        try
        {
            var error = await _visitsService.CreateVisitorAsync(new Visitor
            {
                FullName = FormFullName,
                Phone = FormPhone,
                HostName = FormHost,
                Purpose = FormPurpose,
                VisitType = FormVisitType,
                Zone = FormZone,
                Building = "Tour SBMS",
                AllowedZones = $"Réception,{FormZone}",
                AccessStatus = "Actif"
            });
            if (!string.IsNullOrEmpty(error)) { FormError = error; return; }
            IsAddFormOpen = false;
            StatusMessage = "Visiteur enregistré.";
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ValidateCheckoutAsync()
    {
        if (SelectedVisit is null) return;
        IsBusy = true;
        try
        {
            var error = await _visitsService.CheckoutVisitorAsync(SelectedVisit.Id);
            if (!string.IsNullOrEmpty(error)) { StatusMessage = error; return; }
            StatusMessage = "Sortie validée.";
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private void ExportCsv() => StatusMessage = $"Export : {VisitsExportService.ExportCsv(_allVisits)}";

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

    partial void OnSelectedVisitChanged(VisitListItem? value) => IsDetailPanelOpen = value is not null;
    partial void OnSearchQueryChanged(string value) => ResetPageAndFilter();
    partial void OnTableSearchQueryChanged(string value) => ResetPageAndFilter();
    partial void OnFilterTypeChanged(string value) => ResetPageAndFilter();
    partial void OnFilterStatusChanged(string value) => ResetPageAndFilter();
    partial void OnFilterBuildingChanged(string value) => ResetPageAndFilter();
    partial void OnFilterPeriodChanged(string value) => ResetPageAndFilter();
    partial void OnFilterHostChanged(string value) => ResetPageAndFilter();
    partial void OnPageSizeChanged(int value) => ResetPageAndFilter();

    private void ResetPageAndFilter() { CurrentPage = 1; ApplyFilters(); }

    private void ApplyFilters()
    {
        var today = DateTime.Today;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var query = $"{SearchQuery} {TableSearchQuery}".Trim().ToLowerInvariant();

        var filtered = _allVisits.Where(v =>
            (FilterType == AllTypes || v.VisitType == FilterType) &&
            (FilterStatus == AllStatuses || v.AccessStatus == FilterStatus) &&
            (FilterBuilding == AllBuildings || v.Building == FilterBuilding) &&
            (FilterHost == AllHosts || v.HostName == FilterHost) &&
            (FilterPeriod == AllPeriods
                || (FilterPeriod == "Aujourd'hui" && v.CheckInDisplay.StartsWith(today.ToString("dd/MM/yyyy")))
                || (FilterPeriod == "Cette semaine" && DateTime.TryParse(v.CheckInDisplay[..10], out var d) && d >= weekStart)
                || (FilterPeriod == "Ce mois" && DateTime.TryParse(v.CheckInDisplay[..10], out var dm) && dm >= monthStart)) &&
            (string.IsNullOrWhiteSpace(query) ||
             v.FullName.ToLowerInvariant().Contains(query) ||
             v.VisitCode.ToLowerInvariant().Contains(query) ||
             v.HostName.ToLowerInvariant().Contains(query) ||
             v.Phone.ToLowerInvariant().Contains(query))).ToList();

        FilteredTotal = filtered.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)PageSize));
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;

        var skip = (CurrentPage - 1) * PageSize;
        var list = filtered.ToList();
        Visits.Clear();
        foreach (var v in list.Skip(skip).Take(PageSize)) Visits.Add(v);

        var start = filtered.Count == 0 ? 0 : skip + 1;
        PaginationText = $"Affichage de {start} à {skip + Visits.Count} sur {list.Count} visite(s)";
    }

    private void BuildCharts(VisitsPageData data)
    {
        var palette = new[] { "#2563EB", "#2D6A4F", "#6D28D9", "#EA580C", "#DC2626", "#0EA5E9", "#64748B" };

        DailyTrendSeries =
        [
            new LineSeries<int>
            {
                Name = "Visites",
                Values = data.DailyTrend.Select(p => p.Count).ToArray(),
                Fill = new SolidColorPaint(SKColor.Parse("#2563EB").WithAlpha(40)),
                Stroke = new SolidColorPaint(SKColor.Parse("#2563EB")) { StrokeThickness = 2 },
                GeometrySize = 6
            }
        ];

        TypePieSeries = data.TypeDistribution.Select((s, i) => new PieSeries<int>
        {
            Name = s.Type,
            Values = [s.Count],
            Fill = new SolidColorPaint(SKColor.Parse(palette[i % palette.Length]))
        }).Cast<ISeries>().ToArray();

        AccessBarSeries = data.AccessDistribution.Select((s, i) => new ColumnSeries<int>
        {
            Name = s.Label,
            Values = [s.Count],
            Fill = new SolidColorPaint(SKColor.Parse(palette[i % palette.Length]))
        }).Cast<ISeries>().ToArray();

        HourlySeries =
        [
            new ColumnSeries<int>
            {
                Name = "Entrées",
                Values = data.HourlyTraffic.Select(h => h.Count).ToArray(),
                Fill = new SolidColorPaint(SKColor.Parse("#023E8A"))
            }
        ];
    }

    private void UpdateSyncStatus()
    {
        var last = _syncService.LastSyncAt;
        LastSyncDisplay = last.HasValue ? $"Dernière sync : {last.Value.ToLocalTime():dd/MM HH:mm}" : "Dernière sync : jamais";
        SyncStatusLabel = last.HasValue && (DateTime.Now - last.Value.ToLocalTime()).TotalMinutes < 30 ? "En ligne" : "Hors ligne";
    }

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
            : name.Length >= 2 ? name[..2].ToUpperInvariant() : name.ToUpperInvariant();
    }
}

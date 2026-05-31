using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Shared.Constants;

namespace SmartBuilding.Desktop.WPF.ViewModels;

/// <summary>Liste et gestion des locataires (personnes qui louent un local).</summary>
public partial class LocationsTenantsViewModel : BaseViewModel
{
    private const string AllStatuses = "Tous statuts";

    private readonly LocationsService _locationsService;
    private readonly ShellNavigationService _shellNavigation;
    private readonly SessionService _session;

    private List<LocationsTenantItem> _allTenants = [];

    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _filterStatus = AllStatuses;
    [ObservableProperty] private string? _formError;
    [ObservableProperty] private int _totalTenants;
    [ObservableProperty] private int _activeTenants;
    [ObservableProperty] private int _tenantsWithContract;
    [ObservableProperty] private int _latePaymentsCount;
    [ObservableProperty] private int _displayedCount;

    public bool CanManage => _session.HasPermission(PermissionCodes.LocationManage);

    public ObservableCollection<LocationsTenantItem> Tenants { get; } = [];

    public ObservableCollection<string> StatusFilters { get; } =
    [
        AllStatuses,
        LocationConstants.TenantStatus.Active,
        LocationConstants.TenantStatus.Suspended,
        LocationConstants.TenantStatus.Terminated,
        LocationConstants.TenantStatus.Pending,
        LocationConstants.TenantStatus.Archived
    ];

    public LocationsTenantsViewModel(
        LocationsService locationsService,
        ShellNavigationService shellNavigation,
        SessionService session)
    {
        _locationsService = locationsService;
        _shellNavigation = shellNavigation;
        _session = session;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        FormError = null;
        try
        {
            var stats = await _locationsService.GetTenantStatsAsync();
            TotalTenants = stats.Total;
            ActiveTenants = stats.Active;
            TenantsWithContract = stats.WithActiveContract;
            LatePaymentsCount = stats.LatePayments;

            _allTenants = (await _locationsService.GetAllTenantsListedAsync()).ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            FormError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilter();
    partial void OnFilterStatusChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var filtered = _allTenants.AsEnumerable();

        if (!string.Equals(FilterStatus, AllStatuses, StringComparison.OrdinalIgnoreCase))
            filtered = filtered.Where(t => t.RentalStatus.Equals(FilterStatus, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var q = SearchQuery.Trim();
            filtered = filtered.Where(t =>
                t.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                t.DossierNumber.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                t.Phone.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                t.Email.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                t.Company.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                t.Profession.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        var list = filtered.ToList();
        Tenants.Clear();
        foreach (var item in list)
            Tenants.Add(item);
        DisplayedCount = list.Count;
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (!CanManage) { FormError = "Permission refusée."; return; }
        await _shellNavigation.OpenTenantFormAsync(null);
    }

    [RelayCommand]
    private async Task OpenDetailAsync(LocationsTenantItem? item)
    {
        if (item is null) return;
        await _shellNavigation.OpenTenantDetailAsync(item.Id);
    }

    [RelayCommand]
    private async Task EditAsync(LocationsTenantItem? item)
    {
        if (item is null || !CanManage) return;
        await _shellNavigation.OpenTenantFormAsync(item.Id);
    }

    [RelayCommand]
    private async Task DeleteAsync(LocationsTenantItem? item)
    {
        if (item is null || !CanManage) return;
        if (!SbmsDialogService.Confirm("Archiver le locataire",
                $"Archiver {item.Name} ? Impossible s'il reste un contrat actif."))
            return;

        var error = await _locationsService.DeleteTenantAsync(item.Id);
        if (!string.IsNullOrEmpty(error))
        {
            FormError = error;
            return;
        }

        StatusMessage = "Locataire archivé.";
        await LoadAsync();
    }
}

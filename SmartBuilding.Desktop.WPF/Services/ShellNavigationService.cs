namespace SmartBuilding.Desktop.WPF.Services;

/// <summary>
/// Navigation secondaire dans le shell (sous-pages Locations, fiche locataire, etc.).
/// </summary>
public class ShellNavigationService
{
    private Func<Guid, Task>? _openTenantDetail;
    private Func<Task>? _backToLocations;
    private Func<Guid?, Task>? _openBuildingForm;
    private Func<Guid?, Task>? _openTenantForm;
    private Func<Task>? _openContractForm;
    private Func<Task>? _openRentForm;
    private Func<Task>? _openLocationCreate;
    private Func<Task>? _openLocationList;

    public void Register(
        Func<Guid, Task> openTenantDetail,
        Func<Task> backToLocations,
        Func<Guid?, Task> openBuildingForm,
        Func<Guid?, Task> openTenantForm,
        Func<Task> openContractForm,
        Func<Task> openRentForm,
        Func<Task> openLocationCreate,
        Func<Task> openLocationList)
    {
        _openTenantDetail = openTenantDetail;
        _backToLocations = backToLocations;
        _openBuildingForm = openBuildingForm;
        _openTenantForm = openTenantForm;
        _openContractForm = openContractForm;
        _openRentForm = openRentForm;
        _openLocationCreate = openLocationCreate;
        _openLocationList = openLocationList;
    }

    public Task OpenTenantDetailAsync(Guid tenantId) =>
        _openTenantDetail?.Invoke(tenantId) ?? Task.CompletedTask;

    public Task BackToLocationsAsync() =>
        _backToLocations?.Invoke() ?? Task.CompletedTask;

    public Task OpenBuildingFormAsync(Guid? buildingId = null) =>
        _openBuildingForm?.Invoke(buildingId) ?? Task.CompletedTask;

    public Task OpenTenantFormAsync(Guid? tenantId = null) =>
        _openTenantForm?.Invoke(tenantId) ?? Task.CompletedTask;

    public Task OpenContractFormAsync() =>
        _openContractForm?.Invoke() ?? Task.CompletedTask;

    public Task OpenRentFormAsync() =>
        _openRentForm?.Invoke() ?? Task.CompletedTask;

    public Task OpenLocationCreateAsync() =>
        _openLocationCreate?.Invoke() ?? Task.CompletedTask;

    public Task OpenLocationListAsync() =>
        _openLocationList?.Invoke() ?? Task.CompletedTask;
}

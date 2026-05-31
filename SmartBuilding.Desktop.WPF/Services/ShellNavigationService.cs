using SmartBuilding.Desktop.WPF.ViewModels;

namespace SmartBuilding.Desktop.WPF.Services;

/// <summary>Navigation secondaire dans le shell (sous-pages Locations, fiches, etc.).</summary>
public class ShellNavigationService
{
    private sealed class ContractSubFlowState
    {
        public required LocationContractFormViewModel ContractForm { get; init; }
        public Guid? SelectTenantIdOnResume { get; set; }
    }

    private Func<Guid, Task>? _openTenantDetail;
    private Func<Task>? _backToLocations;
    private Func<Guid?, Task>? _openBuildingForm;
    private Func<Guid?, Task>? _openTenantForm;
    private Func<Task>? _openContractForm;
    private Func<Task>? _openRentForm;
    private Func<Task>? _openLocationCreate;
    private Func<Task>? _openLocationList;
    private Func<Task>? _openTenantsList;
    private Func<int, Task>? _openPatrimoineTab;
    private Func<LocationContractFormViewModel, Guid?, Task>? _resumeContractForm;

    private ContractSubFlowState? _contractSubFlow;

    public bool HasPendingContractResume => _contractSubFlow is not null;

    public void Register(
        Func<Guid, Task> openTenantDetail,
        Func<Task> backToLocations,
        Func<Guid?, Task> openBuildingForm,
        Func<Guid?, Task> openTenantForm,
        Func<Task> openContractForm,
        Func<Task> openRentForm,
        Func<Task> openLocationCreate,
        Func<Task> openLocationList,
        Func<Task> openTenantsList,
        Func<int, Task> openPatrimoineTab,
        Func<LocationContractFormViewModel, Guid?, Task> resumeContractForm)
    {
        _openTenantDetail = openTenantDetail;
        _backToLocations = backToLocations;
        _openBuildingForm = openBuildingForm;
        _openTenantForm = openTenantForm;
        _openContractForm = openContractForm;
        _openRentForm = openRentForm;
        _openLocationCreate = openLocationCreate;
        _openLocationList = openLocationList;
        _openTenantsList = openTenantsList;
        _openPatrimoineTab = openPatrimoineTab;
        _resumeContractForm = resumeContractForm;
    }

    public void BeginContractSubFlow(LocationContractFormViewModel contractForm) =>
        _contractSubFlow = new ContractSubFlowState { ContractForm = contractForm };

    public void SetSelectTenantOnContractResume(Guid tenantId)
    {
        if (_contractSubFlow is not null)
            _contractSubFlow.SelectTenantIdOnResume = tenantId;
    }

    public Task ResumeContractFormAsync()
    {
        if (_contractSubFlow is null)
            return _openContractForm?.Invoke() ?? Task.CompletedTask;

        var state = _contractSubFlow;
        _contractSubFlow = null;
        return _resumeContractForm?.Invoke(state.ContractForm, state.SelectTenantIdOnResume) ?? Task.CompletedTask;
    }

    public Task OpenTenantDetailAsync(Guid tenantId) =>
        _openTenantDetail?.Invoke(tenantId) ?? Task.CompletedTask;

    public Task BackToLocationsAsync() =>
        _backToLocations?.Invoke() ?? Task.CompletedTask;

    public Task BackToTenantsAsync() =>
        _openTenantsList?.Invoke() ?? Task.CompletedTask;

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

    public Task OpenTenantsListAsync() =>
        _openTenantsList?.Invoke() ?? Task.CompletedTask;

    public Task OpenPatrimoineTabAsync(int tabIndex) =>
        _openPatrimoineTab?.Invoke(tabIndex) ?? Task.CompletedTask;
}

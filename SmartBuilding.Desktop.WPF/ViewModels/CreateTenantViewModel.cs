using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartBuilding.Infrastructure.Persistence;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class CreateTenantViewModel : ObservableObject
{
    private readonly OrganizationProvisioningService _provisioning;
    private readonly OrganizationCloudSyncService _cloudSync;

    [ObservableProperty] private string _tenantName = "";
    [ObservableProperty] private string _city = "";
    [ObservableProperty] private string _adminUsername = "";
    [ObservableProperty] private string _adminPassword = "Admin@2026";
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private bool _isBusy;

    public bool Succeeded { get; private set; }
    public OrganizationEntry? CreatedOrganization { get; private set; }

    public CreateTenantViewModel(
        OrganizationProvisioningService provisioning,
        OrganizationCloudSyncService cloudSync)
    {
        _provisioning = provisioning;
        _cloudSync = cloudSync;
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        ErrorMessage = "";
        IsBusy = true;
        try
        {
            var result = await _provisioning.CreateOrganizationAsync(
                new CreateOrganizationRequest(
                    TenantName,
                    City,
                    AdminUsername,
                    AdminPassword));

            if (!result.Success || result.Organization is null)
            {
                ErrorMessage = result.Message;
                return;
            }

            CreatedOrganization = result.Organization;
            Succeeded = true;

            await _cloudSync.RegisterActiveOrganizationAsync(AdminUsername, AdminPassword);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

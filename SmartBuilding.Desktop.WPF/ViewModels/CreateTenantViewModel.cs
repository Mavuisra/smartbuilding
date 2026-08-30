using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class CreateTenantViewModel : ObservableObject
{
    private readonly OrganizationProvisioningService _provisioning;
    private readonly OrganizationCloudSyncService _cloudSync;
    private readonly OrganizationRegistry _registry;

    [ObservableProperty] private string _tenantName = "";
    [ObservableProperty] private string _adminUsername = "";
    [ObservableProperty] private string _adminPassword = "";
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isBusy;

    public bool Succeeded { get; private set; }
    public OrganizationEntry? CreatedOrganization { get; private set; }

    public event Action? TenantCreated;

    public CreateTenantViewModel(
        OrganizationProvisioningService provisioning,
        OrganizationCloudSyncService cloudSync,
        OrganizationRegistry registry)
    {
        _provisioning = provisioning;
        _cloudSync = cloudSync;
        _registry = registry;
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        ErrorMessage = "";
        StatusMessage = "";
        IsBusy = true;
        Succeeded = false;
        CreatedOrganization = null;

        try
        {
            if (!ValidateInputs(out var validationError))
            {
                ErrorMessage = validationError;
                return;
            }

            StatusMessage = "Création de la base MySQL et du compte administrateur…";
            _registry.ReloadFromDisk();

            var result = await _provisioning.CreateOrganizationAsync(
                new CreateOrganizationRequest(
                    TenantName.Trim(),
                    City: string.Empty,
                    AdminUsername.Trim(),
                    AdminPassword));

            if (!result.Success || result.Organization is null)
            {
                ErrorMessage = result.Message;
                StatusMessage = "";
                return;
            }

            CreatedOrganization = result.Organization;
            Succeeded = true;
            StatusMessage = "Tenant créé avec succès.";

            try
            {
                await _cloudSync.RegisterActiveOrganizationAsync(AdminUsername.Trim(), AdminPassword);
            }
            catch
            {
                // Cloud optionnel — la création locale reste valide.
            }

            TenantCreated?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = DbSaveExceptionTranslator.ToUserMessage(ex);
            StatusMessage = "";
            Succeeded = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnTenantNameChanged(string value) => ClearErrorIfTyping();
    partial void OnAdminUsernameChanged(string value) => ClearErrorIfTyping();
    partial void OnAdminPasswordChanged(string value) => ClearErrorIfTyping();

    private void ClearErrorIfTyping()
    {
        if (!string.IsNullOrWhiteSpace(ErrorMessage))
            ErrorMessage = "";
    }

    private bool ValidateInputs(out string error)
    {
        if (TenantName.Trim().Length < 2)
        {
            error = "Le nom du tenant doit contenir au moins 2 caractères.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(AdminUsername))
        {
            error = "L'identifiant administrateur est obligatoire.";
            return false;
        }

        if (AdminPassword.Length < 6)
        {
            error = "Le mot de passe doit contenir au moins 6 caractères.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

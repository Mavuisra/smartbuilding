using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Shared.Constants;
using SmartBuilding.Shared.DTOs.Auth;

namespace SmartBuilding.Desktop.WPF.Services;

public class SessionService
{
    public LoginResponse? CurrentUser { get; private set; }
    public OrganizationEntry? CurrentOrganization { get; private set; }

    /// <summary>Dernier message de liaison identifiants local ↔ cloud.</summary>
    public string? CloudIdentityMessage { get; private set; }

    public bool IsCloudIdentityLinked { get; private set; }

    /// <summary>Rediriger vers Paramètres → Profil entreprise après la première connexion.</summary>
    public bool PendingCompanyProfileSetup { get; private set; }

    public bool IsAuthenticated => CurrentUser is not null;

    public void SetUser(LoginResponse user) => CurrentUser = user;

    public void SetOrganization(OrganizationEntry? organization) =>
        CurrentOrganization = organization;

    public void SetCloudIdentityStatus(bool linked, string? message)
    {
        IsCloudIdentityLinked = linked;
        CloudIdentityMessage = message;
    }

    public void SetPendingCompanyProfileSetup(bool pending) =>
        PendingCompanyProfileSetup = pending;

    public void Clear()
    {
        CurrentUser = null;
        CurrentOrganization = null;
        CloudIdentityMessage = null;
        IsCloudIdentityLinked = false;
        PendingCompanyProfileSetup = false;
    }

    public bool HasPermission(string code) =>
        CurrentUser?.Permissions.Contains("*") == true ||
        CurrentUser?.Permissions.Contains(code) == true;

    /// <summary>Compte limité au module réception (visites / accès).</summary>
    public bool IsReceptionOnly() =>
        HasPermission(PermissionCodes.VisitorsManage)
        && !HasPermission(PermissionCodes.DashboardView)
        && !HasPermission(PermissionCodes.UsersManage);
}

using SmartBuilding.Shared.Constants;
using SmartBuilding.Shared.DTOs.Auth;

namespace SmartBuilding.Desktop.WPF.Services;

public class SessionService
{
    public LoginResponse? CurrentUser { get; private set; }

    public bool IsAuthenticated => CurrentUser is not null;

    public void SetUser(LoginResponse user) => CurrentUser = user;

    public void Clear() => CurrentUser = null;

    public bool HasPermission(string code) =>
        CurrentUser?.Permissions.Contains("*") == true ||
        CurrentUser?.Permissions.Contains(code) == true;

    /// <summary>Compte limité au module réception (visites / accès).</summary>
    public bool IsReceptionOnly() =>
        HasPermission(PermissionCodes.VisitorsManage)
        && !HasPermission(PermissionCodes.DashboardView)
        && !HasPermission(PermissionCodes.UsersManage);
}

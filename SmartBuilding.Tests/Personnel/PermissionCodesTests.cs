using SmartBuilding.Shared.Constants;
using Xunit;

namespace SmartBuilding.Tests.Personnel;

public class PermissionCodesTests
{
    [Fact]
    public void Administrator_Has_Wildcard_Permission()
    {
        var perms = PermissionCodes.RolePermissions["Administrateur"];
        Assert.Contains("*", perms);
    }

    [Fact]
    public void Gestionnaire_Has_Personnel_Manage()
    {
        var perms = PermissionCodes.RolePermissions["Gestionnaire"];
        Assert.Contains(PermissionCodes.PersonnelManage, perms);
    }

    [Fact]
    public void Comptable_Has_Personnel_View_Only()
    {
        var perms = PermissionCodes.RolePermissions["Comptable"];
        Assert.Contains(PermissionCodes.PersonnelView, perms);
        Assert.DoesNotContain(PermissionCodes.PersonnelManage, perms);
    }

    [Fact]
    public void HasPermission_Wildcard_Grants_All()
    {
        var permissions = new List<string> { "*" };
        Assert.True(HasPermission(permissions, PermissionCodes.PersonnelManage));
    }

    [Fact]
    public void Receptionniste_Has_Visitors_Manage_Only()
    {
        var perms = PermissionCodes.RolePermissions["Réceptionniste"];
        Assert.Contains(PermissionCodes.VisitorsManage, perms);
        Assert.DoesNotContain(PermissionCodes.DashboardView, perms);
        Assert.DoesNotContain(PermissionCodes.UsersManage, perms);
    }

    [Fact]
    public void HasPermission_Exact_Code_Required()
    {
        var permissions = new List<string> { PermissionCodes.PersonnelView };
        Assert.True(HasPermission(permissions, PermissionCodes.PersonnelView));
        Assert.False(HasPermission(permissions, PermissionCodes.PersonnelManage));
    }

    private static bool HasPermission(IReadOnlyList<string> permissions, string code) =>
        permissions.Contains("*") || permissions.Contains(code);
}

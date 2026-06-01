namespace SmartBuilding.Shared.Constants;

public static class PermissionCodes
{
    public const string DashboardView = "dashboard.view";
    public const string PersonnelManage = "personnel.manage";
    public const string PersonnelView = "personnel.view";
    public const string TechnicalManage = "technical.manage";
    public const string LocationManage = "location.manage";
    public const string FinanceManage = "finance.manage";
    public const string FinanceView = "finance.view";
    public const string SuppliersManage = "suppliers.manage";
    public const string IncidentsManage = "incidents.manage";
    public const string ConsumptionManage = "consumption.manage";
    public const string VisitorsManage = "visitors.manage";
    public const string InventoryManage = "inventory.manage";
    public const string EmailManage = "email.manage";
    public const string UsersManage = "users.manage";
    public const string SyncManage = "sync.manage";
    public const string ReportsExport = "reports.export";

    public static readonly IReadOnlyDictionary<string, string[]> RolePermissions = new Dictionary<string, string[]>
    {
        ["Administrateur"] = ["*"],
        ["Comptable"] =
        [
            DashboardView, FinanceManage, FinanceView, LocationManage,
            SuppliersManage, ReportsExport, PersonnelView
        ],
        ["Technique"] =
        [
            DashboardView, TechnicalManage, IncidentsManage,
            ConsumptionManage, InventoryManage, PersonnelView
        ],
        ["Gestionnaire"] =
        [
            DashboardView, LocationManage, VisitorsManage, IncidentsManage,
            PersonnelManage, ConsumptionManage, EmailManage, ReportsExport
        ],
        ["Réceptionniste"] = [VisitorsManage],
        ["Receptionniste"] = [VisitorsManage]
    };
}

namespace SmartBuilding.Shared.Constants;

public static class SyncConstants
{
    public const int AutoSyncIntervalSeconds = 60;
    public const int MaxConcurrentUsers = 4;
    public const string ConflictStrategy = "LastWriteWins";

    public static readonly IReadOnlyList<string> EntityTypes =
    [
        "Users", "Employees", "Attendances", "SalaryPayments", "DisciplinaryNotes",
        "Buildings", "RentPayments", "TenantActivities", "LeaseGuarantees",
        "Equipment", "Premises", "Tenants",
        "LeaseContracts", "FinancialTransactions", "Suppliers", "Incidents",
        "ConsumptionRecords", "Visitors", "VisitorAppointments", "InventoryItems"
    ];
}

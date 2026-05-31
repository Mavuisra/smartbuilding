namespace SmartBuilding.Shared.Constants;

public static class SyncConstants
{
    public const int AutoSyncIntervalSeconds = 60;
    public const int MaxConcurrentUsers = 4;
    public const string ConflictStrategy = "LastWriteWins";

    public static readonly IReadOnlyList<string> EntityTypes =
    [
        "Users", "Employees", "Attendances", "SalaryPayments", "DisciplinaryNotes",
        "Buildings", "BuildingInfos", "Landlords", "LandlordActivities",
        "PropertyFloors", "PropertyApartments", "PropertyRooms",
        "RentPayments", "TenantActivities", "LeaseGuarantees", "TenantDependents",
        "Equipment", "MaintenanceRecords", "RepairRecords", "TechnicalAlerts",
        "Premises", "Tenants", "LeaseContracts",
        "FinancialTransactions", "Suppliers", "SupplierContracts", "SupplierPayments",
        "Incidents", "IncidentInterventions",
        "ConsumptionRecords", "Visitors", "VisitorAppointments",
        "InventoryItems", "InventoryMaintenanceRecords"
    ];
}

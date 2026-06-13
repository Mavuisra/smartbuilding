namespace SmartBuilding.Shared.Constants;

public static class SyncConstants
{
    public const int AutoSyncIntervalSeconds = 60;
    public const int MaxConcurrentUsers = 4;
    public const string ConflictStrategy = "LastWriteWins";

    /// <summary>Ordre respectant les dépendances FK (parents avant enfants).</summary>
    public static readonly IReadOnlyList<string> EntityTypes =
    [
        "Users", "Employees", "Attendances", "SalaryPayments", "DisciplinaryNotes",
        "Buildings", "BuildingInfos", "Landlords", "LandlordActivities",
        "PropertyFloors", "PropertyApartments", "PropertyRooms",
        "Premises", "Tenants", "TenantDependents",
        "LeaseContracts",
        "RentPayments", "TenantActivities", "LeaseGuarantees",
        "Equipment", "MaintenanceRecords", "RepairRecords", "TechnicalAlerts",
        "FinancialTransactions", "Suppliers", "SupplierContracts", "SupplierPayments",
        "Incidents", "IncidentInterventions",
        "ConsumptionRecords", "Visitors", "VisitorAppointments",
        "InventoryItems", "InventoryMaintenanceRecords"
    ];
}

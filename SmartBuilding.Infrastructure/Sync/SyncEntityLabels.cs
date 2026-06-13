namespace SmartBuilding.Infrastructure.Sync;

public static class SyncEntityLabels
{
    private static readonly Dictionary<string, string> FrenchLabels =
        new(StringComparer.Ordinal)
        {
            ["Users"] = "Utilisateurs",
            ["Employees"] = "Employés",
            ["Attendances"] = "Présences",
            ["SalaryPayments"] = "Salaires",
            ["DisciplinaryNotes"] = "Notes disciplinaires",
            ["Buildings"] = "Immeubles",
            ["BuildingInfos"] = "Bâtiments",
            ["Landlords"] = "Bailleurs",
            ["LandlordActivities"] = "Activités bailleurs",
            ["PropertyFloors"] = "Étages",
            ["PropertyApartments"] = "Appartements",
            ["PropertyRooms"] = "Pièces",
            ["Premises"] = "Locaux",
            ["Tenants"] = "Locataires",
            ["TenantDependents"] = "Personnes à charge",
            ["LeaseContracts"] = "Contrats",
            ["RentPayments"] = "Loyers",
            ["TenantActivities"] = "Activités locataires",
            ["LeaseGuarantees"] = "Garanties",
            ["Equipment"] = "Équipements",
            ["MaintenanceRecords"] = "Maintenances",
            ["RepairRecords"] = "Réparations",
            ["TechnicalAlerts"] = "Alertes techniques",
            ["FinancialTransactions"] = "Transactions",
            ["Suppliers"] = "Fournisseurs",
            ["SupplierContracts"] = "Contrats fournisseurs",
            ["SupplierPayments"] = "Paiements fournisseurs",
            ["Incidents"] = "Incidents",
            ["IncidentInterventions"] = "Interventions",
            ["ConsumptionRecords"] = "Consommations",
            ["Visitors"] = "Visiteurs",
            ["VisitorAppointments"] = "Rendez-vous visiteurs",
            ["InventoryItems"] = "Inventaire",
            ["InventoryMaintenanceRecords"] = "Maintenance inventaire"
        };

    public static string ToFrench(string entityType) =>
        FrenchLabels.TryGetValue(entityType, out var label) ? label : entityType;
}

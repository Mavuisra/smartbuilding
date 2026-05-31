using SmartBuilding.Domain.Entities.Auth;
using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Domain.Entities.Consumption;
using SmartBuilding.Domain.Entities.Finance;
using SmartBuilding.Domain.Entities.Incidents;
using SmartBuilding.Domain.Entities.Inventory;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Domain.Entities.Personnel;
using SmartBuilding.Domain.Entities.Suppliers;
using SmartBuilding.Domain.Entities.Technical;
using SmartBuilding.Domain.Entities.Visitors;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Shared.Constants;

namespace SmartBuilding.Infrastructure.Sync;

public static class SyncEntityRegistry
{
    private static readonly IReadOnlyList<IEntitySyncAdapter> Adapters =
    [
        new EntitySyncAdapter<User>("Users", ctx => ctx.Users),
        new EntitySyncAdapter<Employee>("Employees", ctx => ctx.Employees),
        new EntitySyncAdapter<Attendance>("Attendances", ctx => ctx.Attendances),
        new EntitySyncAdapter<SalaryPayment>("SalaryPayments", ctx => ctx.SalaryPayments),
        new EntitySyncAdapter<DisciplinaryNote>("DisciplinaryNotes", ctx => ctx.DisciplinaryNotes),
        new EntitySyncAdapter<BuildingInfo>("BuildingInfos", ctx => ctx.BuildingInfos),
        new EntitySyncAdapter<Landlord>("Landlords", ctx => ctx.Landlords),
        new EntitySyncAdapter<LandlordActivity>("LandlordActivities", ctx => ctx.LandlordActivities),
        new EntitySyncAdapter<PropertyFloor>("PropertyFloors", ctx => ctx.PropertyFloors),
        new EntitySyncAdapter<PropertyApartment>("PropertyApartments", ctx => ctx.PropertyApartments),
        new EntitySyncAdapter<PropertyRoom>("PropertyRooms", ctx => ctx.PropertyRooms),
        new EntitySyncAdapter<Equipment>("Equipment", ctx => ctx.Equipment),
        new EntitySyncAdapter<MaintenanceRecord>("MaintenanceRecords", ctx => ctx.MaintenanceRecords),
        new EntitySyncAdapter<RepairRecord>("RepairRecords", ctx => ctx.RepairRecords),
        new EntitySyncAdapter<TechnicalAlert>("TechnicalAlerts", ctx => ctx.TechnicalAlerts),
        new EntitySyncAdapter<Premise>("Premises", ctx => ctx.Premises),
        new EntitySyncAdapter<Tenant>("Tenants", ctx => ctx.Tenants),
        new EntitySyncAdapter<TenantDependent>("TenantDependents", ctx => ctx.TenantDependents),
        new EntitySyncAdapter<Building>("Buildings", ctx => ctx.Buildings),
        new EntitySyncAdapter<LeaseContract>("LeaseContracts", ctx => ctx.LeaseContracts),
        new EntitySyncAdapter<RentPayment>("RentPayments", ctx => ctx.RentPayments),
        new EntitySyncAdapter<TenantActivity>("TenantActivities", ctx => ctx.TenantActivities),
        new EntitySyncAdapter<LeaseGuarantee>("LeaseGuarantees", ctx => ctx.LeaseGuarantees),
        new EntitySyncAdapter<FinancialTransaction>("FinancialTransactions", ctx => ctx.FinancialTransactions),
        new EntitySyncAdapter<Supplier>("Suppliers", ctx => ctx.Suppliers),
        new EntitySyncAdapter<SupplierContract>("SupplierContracts", ctx => ctx.SupplierContracts),
        new EntitySyncAdapter<SupplierPayment>("SupplierPayments", ctx => ctx.SupplierPayments),
        new EntitySyncAdapter<Incident>("Incidents", ctx => ctx.Incidents),
        new EntitySyncAdapter<IncidentIntervention>("IncidentInterventions", ctx => ctx.IncidentInterventions),
        new EntitySyncAdapter<ConsumptionRecord>("ConsumptionRecords", ctx => ctx.ConsumptionRecords),
        new EntitySyncAdapter<Visitor>("Visitors", ctx => ctx.Visitors),
        new EntitySyncAdapter<VisitorAppointment>("VisitorAppointments", ctx => ctx.VisitorAppointments),
        new EntitySyncAdapter<InventoryItem>("InventoryItems", ctx => ctx.InventoryItems),
        new EntitySyncAdapter<InventoryMaintenanceRecord>(
            "InventoryMaintenanceRecords", ctx => ctx.InventoryMaintenanceRecords)
    ];

    private static readonly Dictionary<string, IEntitySyncAdapter> ByType =
        Adapters.ToDictionary(a => a.EntityType, StringComparer.Ordinal);

    public static IReadOnlyList<string> SyncableTypes { get; } =
        SyncConstants.EntityTypes;

    public static IReadOnlyList<IEntitySyncAdapter> AllAdapters => Adapters;

    public static IEntitySyncAdapter? TryGet(string entityType) =>
        ByType.TryGetValue(entityType, out var adapter) ? adapter : null;

    public static IEntitySyncAdapter GetRequired(string entityType) =>
        TryGet(entityType) ?? throw new InvalidOperationException($"Type de sync inconnu : {entityType}");
}

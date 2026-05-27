using SmartBuilding.Domain.Entities.Auth;
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
        new EntitySyncAdapter<Equipment>("Equipment", ctx => ctx.Equipment),
        new EntitySyncAdapter<Premise>("Premises", ctx => ctx.Premises),
        new EntitySyncAdapter<Tenant>("Tenants", ctx => ctx.Tenants),
        new EntitySyncAdapter<Building>("Buildings", ctx => ctx.Buildings),
        new EntitySyncAdapter<LeaseContract>("LeaseContracts", ctx => ctx.LeaseContracts),
        new EntitySyncAdapter<RentPayment>("RentPayments", ctx => ctx.RentPayments),
        new EntitySyncAdapter<TenantActivity>("TenantActivities", ctx => ctx.TenantActivities),
        new EntitySyncAdapter<LeaseGuarantee>("LeaseGuarantees", ctx => ctx.LeaseGuarantees),
        new EntitySyncAdapter<FinancialTransaction>("FinancialTransactions", ctx => ctx.FinancialTransactions),
        new EntitySyncAdapter<Supplier>("Suppliers", ctx => ctx.Suppliers),
        new EntitySyncAdapter<Incident>("Incidents", ctx => ctx.Incidents),
        new EntitySyncAdapter<ConsumptionRecord>("ConsumptionRecords", ctx => ctx.ConsumptionRecords),
        new EntitySyncAdapter<Visitor>("Visitors", ctx => ctx.Visitors),
        new EntitySyncAdapter<VisitorAppointment>("VisitorAppointments", ctx => ctx.VisitorAppointments),
        new EntitySyncAdapter<InventoryItem>("InventoryItems", ctx => ctx.InventoryItems)
    ];

    private static readonly Dictionary<string, IEntitySyncAdapter> ByType =
        Adapters.ToDictionary(a => a.EntityType, StringComparer.Ordinal);

    public static IReadOnlyList<string> SyncableTypes { get; } =
        SyncConstants.EntityTypes;

    public static IEntitySyncAdapter? TryGet(string entityType) =>
        ByType.TryGetValue(entityType, out var adapter) ? adapter : null;

    public static IEntitySyncAdapter GetRequired(string entityType) =>
        TryGet(entityType) ?? throw new InvalidOperationException($"Type de sync inconnu : {entityType}");
}

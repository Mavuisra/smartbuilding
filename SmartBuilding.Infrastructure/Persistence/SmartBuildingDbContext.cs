using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Auth;
using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Domain.Entities.Consumption;
using SmartBuilding.Domain.Entities.Email;
using SmartBuilding.Domain.Entities.Finance;
using SmartBuilding.Domain.Entities.Incidents;
using SmartBuilding.Domain.Entities.Inventory;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Domain.Entities.Personnel;
using SmartBuilding.Domain.Entities.Suppliers;
using SmartBuilding.Domain.Entities.Sync;
using SmartBuilding.Domain.Entities.System;
using SmartBuilding.Domain.Entities.Technical;
using SmartBuilding.Domain.Entities.Visitors;

namespace SmartBuilding.Infrastructure.Persistence;

public class SmartBuildingDbContext : DbContext
{
    public SmartBuildingDbContext(DbContextOptions<SmartBuildingDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<SalaryPayment> SalaryPayments => Set<SalaryPayment>();
    public DbSet<DisciplinaryNote> DisciplinaryNotes => Set<DisciplinaryNote>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();
    public DbSet<RepairRecord> RepairRecords => Set<RepairRecord>();
    public DbSet<TechnicalAlert> TechnicalAlerts => Set<TechnicalAlert>();
    public DbSet<Landlord> Landlords => Set<Landlord>();
    public DbSet<LandlordActivity> LandlordActivities => Set<LandlordActivity>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Premise> Premises => Set<Premise>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantDependent> TenantDependents => Set<TenantDependent>();
    public DbSet<LeaseGuarantee> LeaseGuarantees => Set<LeaseGuarantee>();
    public DbSet<TenantActivity> TenantActivities => Set<TenantActivity>();
    public DbSet<LeaseContract> LeaseContracts => Set<LeaseContract>();
    public DbSet<RentPayment> RentPayments => Set<RentPayment>();
    public DbSet<FinancialTransaction> FinancialTransactions => Set<FinancialTransaction>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierContract> SupplierContracts => Set<SupplierContract>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentIntervention> IncidentInterventions => Set<IncidentIntervention>();
    public DbSet<ConsumptionRecord> ConsumptionRecords => Set<ConsumptionRecord>();
    public DbSet<Visitor> Visitors => Set<Visitor>();
    public DbSet<VisitorAppointment> VisitorAppointments => Set<VisitorAppointment>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryMaintenanceRecord> InventoryMaintenanceRecords => Set<InventoryMaintenanceRecord>();
    public DbSet<CachedEmail> CachedEmails => Set<CachedEmail>();
    public DbSet<EmailAccount> EmailAccounts => Set<EmailAccount>();
    public DbSet<SyncLog> SyncLogs => Set<SyncLog>();
    public DbSet<SystemLog> SystemLogs => Set<SystemLog>();
    public DbSet<BuildingInfo> BuildingInfos => Set<BuildingInfo>();
    public DbSet<PropertyFloor> PropertyFloors => Set<PropertyFloor>();
    public DbSet<PropertyApartment> PropertyApartments => Set<PropertyApartment>();
    public DbSet<PropertyRoom> PropertyRooms => Set<PropertyRoom>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SmartBuildingDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            modelBuilder.Entity(entityType.ClrType)
                .HasQueryFilter(BuildSoftDeleteFilter(entityType.ClrType));
        }
    }

    private static System.Linq.Expressions.LambdaExpression BuildSoftDeleteFilter(Type entityType)
    {
        var parameter = System.Linq.Expressions.Expression.Parameter(entityType, "e");
        var property = System.Linq.Expressions.Expression.Property(parameter, "DeletedAt");
        var condition = System.Linq.Expressions.Expression.Equal(
            property,
            System.Linq.Expressions.Expression.Constant(null, typeof(DateTime?)));
        return System.Linq.Expressions.Expression.Lambda(condition, parameter);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Domain.Common.BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
                entry.Entity.IsSynced = false;
            }
            else if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
                entry.Entity.UpdatedAt = DateTime.UtcNow;
                entry.Entity.IsSynced = false;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}

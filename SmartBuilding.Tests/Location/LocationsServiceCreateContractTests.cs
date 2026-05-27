using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Desktop.WPF.Services;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Services;
using Xunit;

namespace SmartBuilding.Tests.Location;

public class LocationsServiceCreateContractTests
{
    [Fact]
    public async Task CreateContractAsync_Succeeds_WithGuaranteeAndRentPayment()
    {
        await using var db = await CreateDbAsync();
        var (tenantId, premiseId) = await SeedTenantAndPremiseAsync(db);
        var service = new LocationsService(db, new FinanceLedgerService(db));

        var result = await service.CreateContractAsync(
            premiseId,
            tenantId,
            DateTime.Today,
            DateTime.Today.AddYears(1),
            1500m,
            3000m);

        Assert.Equal(string.Empty, result.Error);
        Assert.NotNull(result.ContractId);
        Assert.False(string.IsNullOrWhiteSpace(result.SummaryPdfPath));
        var contract = await db.LeaseContracts.SingleAsync();
        Assert.StartsWith("CTR-", contract.ContractNumber, StringComparison.OrdinalIgnoreCase);
        Assert.True(await db.LeaseGuarantees.AnyAsync());
        Assert.True(await db.RentPayments.AnyAsync());
        var premise = await db.Premises.FindAsync(premiseId);
        Assert.True(premise!.IsOccupied);
    }

    [Fact]
    public async Task CreateContractAsync_ReturnsMessage_WhenDuplicateTenantPremise()
    {
        await using var db = await CreateDbAsync();
        var (tenantId, premiseId) = await SeedTenantAndPremiseAsync(db);
        db.LeaseContracts.Add(new LeaseContract
        {
            TenantId = tenantId,
            PremiseId = premiseId,
            ContractNumber = "CTR-EXISTING",
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddYears(1),
            MonthlyRent = 1000,
            Status = LeaseStatus.Actif
        });
        await db.SaveChangesAsync();

        var service = new LocationsService(db, new FinanceLedgerService(db));
        var result = await service.CreateContractAsync(
            premiseId,
            tenantId,
            DateTime.Today,
            DateTime.Today.AddYears(1),
            1000,
            0);

        Assert.Contains("existe déjà", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await db.LeaseContracts.CountAsync(c => c.PremiseId == premiseId));
    }

    [Fact]
    public async Task GenerateNextContractNumberAsync_SkipsExistingNumbers_IncludingSoftDeleted()
    {
        await using var db = await CreateDbAsync();
        var (tenantId, premiseId) = await SeedTenantAndPremiseAsync(db);
        var contract = new LeaseContract
        {
            TenantId = tenantId,
            PremiseId = premiseId,
            ContractNumber = "CTR-001",
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddYears(1),
            MonthlyRent = 1000,
            Status = LeaseStatus.Resilie
        };
        contract.SoftDelete();
        db.LeaseContracts.Add(contract);
        await db.SaveChangesAsync();

        var service = new LocationsService(db, new FinanceLedgerService(db));
        var next = await service.GenerateNextContractNumberAsync();

        Assert.Equal("CTR-002", next);
    }

    [Fact]
    public async Task CreateContractAsync_AssignsCtr002_WhenCtr001Exists()
    {
        await using var db = await CreateDbAsync();
        var (tenantId, premiseId) = await SeedTenantAndPremiseAsync(db);
        var otherPremiseId = Guid.NewGuid();
        db.Premises.Add(new Premise { Id = otherPremiseId, Code = "LOC-002", Name = "Local 2" });
        db.LeaseContracts.Add(new LeaseContract
        {
            TenantId = tenantId,
            PremiseId = otherPremiseId,
            ContractNumber = "CTR-001",
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddYears(1),
            MonthlyRent = 1000,
            Status = LeaseStatus.Resilie
        });
        await db.SaveChangesAsync();

        var service = new LocationsService(db, new FinanceLedgerService(db));
        var result = await service.CreateContractAsync(
            premiseId,
            tenantId,
            DateTime.Today,
            DateTime.Today.AddYears(1),
            1000,
            0);

        Assert.Equal(string.Empty, result.Error);
        var created = await db.LeaseContracts.SingleAsync(c => c.PremiseId == premiseId);
        Assert.Equal("CTR-002", created.ContractNumber);
    }

    private static async Task<SmartBuildingDbContext> CreateDbAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sbms-test-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<SmartBuildingDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        var db = new SmartBuildingDbContext(options);
        await db.Database.EnsureCreatedAsync();
        await DatabaseSchemaUpgrader.UpgradeAsync(db);
        return db;
    }

    private static async Task<(Guid TenantId, Guid PremiseId)> SeedTenantAndPremiseAsync(SmartBuildingDbContext db)
    {
        var tenantId = Guid.NewGuid();
        var premiseId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test Locataire", Phone = "0812345678", Email = "t@test.com" });
        db.Premises.Add(new Premise { Id = premiseId, Code = "LOC-T", Name = "Local test", IsOccupied = false });
        await db.SaveChangesAsync();
        return (tenantId, premiseId);
    }
}

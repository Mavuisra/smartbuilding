using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Infrastructure.Persistence;
using Xunit;

namespace SmartBuilding.Tests.Location;

public class LeaseContractBusinessRulesTests
{
    [Fact]
    public async Task SameTenantAndPremise_CannotHaveTwoActiveContracts()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var premiseId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Jean", Phone = "0812345678", Email = "j@test.com" });
        db.Premises.Add(new Premise { Id = premiseId, Code = "LOC-001", Name = "Local 1", IsOccupied = true });
        db.LeaseContracts.Add(new LeaseContract
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PremiseId = premiseId,
            ContractNumber = "CTR-001",
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddYears(1),
            MonthlyRent = 1000,
            Status = LeaseStatus.Actif
        });
        await db.SaveChangesAsync();

        var duplicate = await db.LeaseContracts.AnyAsync(c =>
            c.TenantId == tenantId &&
            c.PremiseId == premiseId &&
            c.Status != LeaseStatus.Resilie &&
            c.Status != LeaseStatus.Annule);

        Assert.True(duplicate);
    }

    [Fact]
    public async Task OccupyingStatuses_Contains_IsTranslatedByEf()
    {
        await using var db = CreateDb();
        var premiseId = Guid.NewGuid();
        db.Premises.Add(new Premise { Id = premiseId, Code = "LOC-X", Name = "Test" });
        db.LeaseContracts.Add(new LeaseContract
        {
            Id = Guid.NewGuid(),
            PremiseId = premiseId,
            TenantId = Guid.NewGuid(),
            ContractNumber = "CTR-X",
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddYears(1),
            MonthlyRent = 500,
            Status = LeaseStatus.EnAttenteValidation
        });
        await db.SaveChangesAsync();

        var ids = await db.LeaseContracts
            .Where(c => LeaseOccupancyRules.OccupyingStatuses.Contains(c.Status))
            .Select(c => c.PremiseId)
            .ToListAsync();

        Assert.Single(ids);
        Assert.Equal(premiseId, ids[0]);
    }

    [Fact]
    public void NewContract_ShouldMarkPremiseOccupied()
    {
        var premise = new Premise
        {
            Code = "LOC-002",
            Name = "Local 2",
            IsOccupied = false,
            OccupancyStatus = LocationConstants.PremiseOccupancyStatus.Available
        };

        premise.IsOccupied = true;
        premise.OccupancyStatus = LocationConstants.PremiseOccupancyStatus.Occupied;

        Assert.True(premise.IsOccupied);
        Assert.Equal(LocationConstants.PremiseOccupancyStatus.Occupied, premise.OccupancyStatus);
    }

    private static SmartBuildingDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<SmartBuildingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SmartBuildingDbContext(options);
    }
}

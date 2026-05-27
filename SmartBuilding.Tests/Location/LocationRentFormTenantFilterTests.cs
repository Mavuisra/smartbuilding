using SmartBuilding.Desktop.WPF.Models;
using Xunit;

namespace SmartBuilding.Tests.Location;

/// <summary>
/// Régression : locataire sélectionné via Id (TenantId vide) doit filtrer les contrats.
/// </summary>
public class LocationRentFormTenantFilterTests
{
    [Fact]
    public void TenantContracts_FilterMatches_WhenOnlyTenantIdIsSetOnPickItem()
    {
        var tenantId = Guid.NewGuid();
        var contracts = new[]
        {
            new LocationsPickItem { Id = Guid.NewGuid(), TenantId = tenantId, Code = "LOC-001", Name = "Bureau A" },
            new LocationsPickItem { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Code = "LOC-002", Name = "Bureau B" }
        };

        var tenantPick = new LocationsPickItem { Id = tenantId, TenantId = Guid.Empty, Label = "KABONGO" };
        var resolvedId = tenantPick.TenantId != Guid.Empty ? tenantPick.TenantId : tenantPick.Id;

        var premises = contracts.Where(c => c.TenantId == resolvedId).ToList();

        Assert.Single(premises);
        Assert.Equal("LOC-001", premises[0].Code);
    }
}

using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Domain.Enums;
using Xunit;

namespace SmartBuilding.Tests.Location;

public class LocationConstantsTests
{
    [Theory]
    [InlineData(LeaseStatus.Actif, "Actif")]
    [InlineData(LeaseStatus.EnAttenteValidation, "En attente validation")]
    [InlineData(LeaseStatus.Annule, "Annulé")]
    [InlineData(LeaseStatus.Resilie, "Résilié")]
    public void ContractStatusLabel_ReturnsFrenchLabel(LeaseStatus status, string expected) =>
        Assert.Equal(expected, LocationContractStatusHelper.ToLabel(status));

    [Fact]
    public void PaymentStatus_Constants_AreDefined()
    {
        Assert.Equal("Payé", LocationConstants.PaymentStatus.Paid);
        Assert.Equal("En attente", LocationConstants.PaymentStatus.Pending);
        Assert.Equal("Retard", LocationConstants.PaymentStatus.Late);
    }

    [Fact]
    public void TenantStatus_Archived_IsDefined() =>
        Assert.Equal("Archivé", LocationConstants.TenantStatus.Archived);
}

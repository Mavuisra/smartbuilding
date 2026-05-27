using SmartBuilding.Domain.Entities.Location;
using Xunit;

namespace SmartBuilding.Tests.Location;

public class RentPaymentDuplicateTests
{
    [Fact]
    public void IsFullyPaid_WhenAmountPaidEqualsDue_ReturnsTrue()
    {
        Assert.True(RentPaymentRules.IsFullyPaid(1000, 1000, LocationConstants.PaymentStatus.Paid));
    }

    [Fact]
    public void RemainingDue_WhenFullyPaid_ReturnsZero()
    {
        Assert.Equal(0, RentPaymentRules.RemainingDue(1000, 1000, LocationConstants.PaymentStatus.Paid));
    }

    [Fact]
    public void HasOverpayment_DetectsExcess()
    {
        Assert.True(RentPaymentRules.HasOverpayment(1000, 1500));
        Assert.False(RentPaymentRules.HasOverpayment(1000, 1000));
    }
}

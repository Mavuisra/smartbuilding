using SmartBuilding.Domain.Entities.Location;
using Xunit;

namespace SmartBuilding.Tests.Location;

public class RentPaymentLedgerDatesTests
{
    [Fact]
    public void TransactionDate_uses_paid_date_when_present()
    {
        var payment = new RentPayment
        {
            Year = 2026,
            Month = 5,
            PaidDate = new DateTime(2026, 5, 15)
        };

        Assert.Equal(new DateTime(2026, 5, 15), RentPaymentLedgerDates.TransactionDate(payment));
    }

    [Fact]
    public void TransactionDate_falls_back_to_rent_period()
    {
        var payment = new RentPayment { Year = 2026, Month = 6 };

        Assert.Equal(new DateTime(2026, 6, 1), RentPaymentLedgerDates.TransactionDate(payment));
    }
}

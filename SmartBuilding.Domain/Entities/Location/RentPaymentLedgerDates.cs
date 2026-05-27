namespace SmartBuilding.Domain.Entities.Location;

/// <summary>Date comptable d'un encaissement loyer (période ou date de paiement réelle).</summary>
public static class RentPaymentLedgerDates
{
    public static DateTime TransactionDate(RentPayment payment)
    {
        if (payment.PaidDate is { } paid && paid != default)
            return paid.Date;

        return new DateTime(payment.Year, payment.Month, 1);
    }
}

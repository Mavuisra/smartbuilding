namespace SmartBuilding.Domain.Entities.Location;

/// <summary>Règles métier : un seul encaissement par mois de loyer (pas de double paiement).</summary>
public static class RentPaymentRules
{
    public static bool IsFullyPaid(decimal amountDue, decimal amountPaid, string? paymentStatus) =>
        amountDue > 0 &&
        amountPaid >= amountDue &&
        !string.Equals(paymentStatus, LocationConstants.PaymentStatus.Cancelled, StringComparison.OrdinalIgnoreCase);

    public static decimal RemainingDue(decimal amountDue, decimal amountPaid, string? paymentStatus)
    {
        if (string.Equals(paymentStatus, LocationConstants.PaymentStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
            return amountDue;
        return Math.Max(0, amountDue - amountPaid);
    }

    public static bool HasOverpayment(decimal amountDue, decimal amountPaid) =>
        amountDue > 0 && amountPaid > amountDue;
}

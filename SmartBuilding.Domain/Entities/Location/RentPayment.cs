using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Location;

public class RentPayment : BaseEntity
{
    public Guid LeaseContractId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public bool IsLate { get; set; }
    public decimal PenaltyAmount { get; set; }
    public string PaymentStatus { get; set; } = LocationConstants.PaymentStatus.Pending;
    public string PaymentMethod { get; set; } = "Virement bancaire";
    public string? TransactionReference { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? ReceiptPdfPath { get; set; }

    public LeaseContract LeaseContract { get; set; } = null!;
}

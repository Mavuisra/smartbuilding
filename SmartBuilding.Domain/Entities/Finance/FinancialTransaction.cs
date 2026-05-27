using SmartBuilding.Domain.Common;
using SmartBuilding.Domain.Enums;

namespace SmartBuilding.Domain.Entities.Finance;

public class FinancialTransaction : BaseEntity
{
    public TransactionType Type { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? Reference { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = "Virement";
    public string Status { get; set; } = "Payé";
    public string RecordedBy { get; set; } = string.Empty;
}

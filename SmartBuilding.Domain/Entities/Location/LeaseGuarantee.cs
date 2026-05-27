using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Location;

public class LeaseGuarantee : BaseEntity
{
    public Guid LeaseContractId { get; set; }
    public decimal Amount { get; set; }
    public decimal AmountRefunded { get; set; }
    public string Status { get; set; } = LocationConstants.GuaranteeStatus.Active;
    public DateTime? RefundedAt { get; set; }
    public string? Notes { get; set; }
    public string? DischargePdfPath { get; set; }

    public LeaseContract LeaseContract { get; set; } = null!;
}

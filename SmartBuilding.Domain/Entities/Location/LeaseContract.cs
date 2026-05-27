using SmartBuilding.Domain.Common;
using SmartBuilding.Domain.Enums;

namespace SmartBuilding.Domain.Entities.Location;

public class LeaseContract : BaseEntity
{
    public Guid PremiseId { get; set; }
    public Guid TenantId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal MonthlyRent { get; set; }
    public decimal Deposit { get; set; }
    public string ContractType { get; set; } = LocationConstants.ContractTypes.Office;
    public string Clauses { get; set; } = string.Empty;
    public LeaseStatus Status { get; set; } = LeaseStatus.Brouillon;
    public string? CreatedBy { get; set; }
    public string? ValidatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public string? CancelledBy { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? ContractPdfPath { get; set; }

    public Premise Premise { get; set; } = null!;
    public Tenant Tenant { get; set; } = null!;
    public ICollection<RentPayment> RentPayments { get; set; } = [];
    public ICollection<LeaseGuarantee> Guarantees { get; set; } = [];
}

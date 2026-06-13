using SmartBuilding.Domain.Common;
using SmartBuilding.Domain.Enums;

namespace SmartBuilding.Domain.Entities.Consumption;

public class ConsumptionRecord : BaseEntity
{
    public ConsumptionType Type { get; set; }
    public string? CustomTypeLabel { get; set; }
    public string? ExpenseMotif { get; set; }
    public string PaidBy { get; set; } = string.Empty;
    public string ReimbursementStatus { get; set; } = ConsumptionReimbursementStatus.NotApplicable;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public string Currency { get; set; } = "USD";
    public string? MeterReference { get; set; }
    public string? Notes { get; set; }
    public string Building { get; set; } = string.Empty;
    public string EquipmentSource { get; set; } = string.Empty;
    public string Responsible { get; set; } = string.Empty;
    public string Status { get; set; } = "Normal";
    public string PeriodType { get; set; } = "Mensuel";
    public decimal VariationPercent { get; set; }
    public bool IsAnomaly { get; set; }
}

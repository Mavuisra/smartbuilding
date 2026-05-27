using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Inventory;

public class InventoryItem : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public string Location { get; set; } = string.Empty;
    public string Condition { get; set; } = "Bon";
    public decimal UnitValue { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "Opérationnel";
    public string Responsible { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string Building { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public DateTime? LastMaintenanceDate { get; set; }
    public DateTime? NextMaintenanceDate { get; set; }
    public decimal EstimatedValue { get; set; }
    public string UsageDuration { get; set; } = string.Empty;

    public ICollection<InventoryMaintenanceRecord> MaintenanceRecords { get; set; } = [];
}

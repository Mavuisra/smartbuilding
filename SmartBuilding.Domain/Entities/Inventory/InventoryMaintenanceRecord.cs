using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Inventory;

public class InventoryMaintenanceRecord : BaseEntity
{
    public Guid InventoryItemId { get; set; }
    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public string Technician { get; set; } = string.Empty;
    public string RecordType { get; set; } = "Maintenance";

    public InventoryItem InventoryItem { get; set; } = null!;
}

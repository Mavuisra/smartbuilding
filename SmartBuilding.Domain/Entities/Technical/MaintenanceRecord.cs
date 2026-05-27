using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Technical;

public class MaintenanceRecord : BaseEntity
{
    public Guid EquipmentId { get; set; }
    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public string? Technician { get; set; }

    public Equipment Equipment { get; set; } = null!;
}

using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Technical;

public class RepairRecord : BaseEntity
{
    public Guid EquipmentId { get; set; }
    public DateTime ReportedDate { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public string Issue { get; set; } = string.Empty;
    public string? Resolution { get; set; }
    public decimal Cost { get; set; }

    public Equipment Equipment { get; set; } = null!;
}

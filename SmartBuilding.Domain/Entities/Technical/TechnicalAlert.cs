using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Technical;

public class TechnicalAlert : BaseEntity
{
    public Guid? EquipmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime AlertDate { get; set; }
    public bool IsAcknowledged { get; set; }

    public Equipment? Equipment { get; set; }
}

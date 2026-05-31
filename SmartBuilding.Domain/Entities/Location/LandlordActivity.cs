using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Location;

public class LandlordActivity : BaseEntity
{
    public Guid LandlordId { get; set; }
    public DateTime OccurredAt { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public Landlord Landlord { get; set; } = null!;
}

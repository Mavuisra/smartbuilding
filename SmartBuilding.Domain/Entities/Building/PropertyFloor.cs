using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Building;

/// <summary>Étage du patrimoine configuré dans Paramètres.</summary>
public class PropertyFloor : BaseEntity
{
    public Guid BuildingInfoId { get; set; }
    public int LevelNumber { get; set; }
    public string Label { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public BuildingInfo BuildingInfo { get; set; } = null!;
    public ICollection<PropertyApartment> Apartments { get; set; } = [];
}

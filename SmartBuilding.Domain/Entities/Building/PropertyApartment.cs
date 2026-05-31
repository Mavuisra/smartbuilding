using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Building;

/// <summary>Appartement ou local commercial rattaché à un étage.</summary>
public class PropertyApartment : BaseEntity
{
    public Guid FloorId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string UnitType { get; set; } = PropertyStructureConstants.UnitTypes.Apartment;
    public decimal AreaSqM { get; set; }
    public decimal MonthlyRent { get; set; }
    public int SortOrder { get; set; }
    public Guid? PremiseId { get; set; }

    public PropertyFloor Floor { get; set; } = null!;
    public ICollection<PropertyRoom> Rooms { get; set; } = [];
}

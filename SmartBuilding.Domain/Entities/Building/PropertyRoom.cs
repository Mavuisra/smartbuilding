using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Building;

/// <summary>Pièce d'un appartement (chambre, salon, cuisine…).</summary>
public class PropertyRoom : BaseEntity
{
    public Guid ApartmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RoomType { get; set; } = PropertyStructureConstants.RoomTypes.Bedroom;
    public decimal AreaSqM { get; set; }
    public int SortOrder { get; set; }

    public PropertyApartment Apartment { get; set; } = null!;
}

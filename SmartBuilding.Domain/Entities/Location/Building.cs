using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Location;

public class Building : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int FloorCount { get; set; }
    public int PremiseCount { get; set; }
    public string BuildingType { get; set; } = LocationConstants.BuildingTypes.Office;
    public int Capacity { get; set; }
    public string Status { get; set; } = "Actif";
    public string Equipment { get; set; } = string.Empty;
    public string Zones { get; set; } = string.Empty;
    public string? PhotoPath { get; set; }
    public string? Notes { get; set; }

    public ICollection<Premise> Premises { get; set; } = [];
}

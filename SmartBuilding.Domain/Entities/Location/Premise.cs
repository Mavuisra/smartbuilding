using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Location;

public class Premise : BaseEntity
{
    public Guid? BuildingId { get; set; }
    public Guid? PropertyApartmentId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Floor { get; set; } = string.Empty;
    public string Building { get; set; } = string.Empty;
    public string PremiseType { get; set; } = string.Empty;
    public string OccupancyStatus { get; set; } = LocationConstants.PremiseOccupancyStatus.Available;
    public int Capacity { get; set; } = 1;
    public string Equipment { get; set; } = string.Empty;
    public string ConditionNotes { get; set; } = string.Empty;
    public string? PhotoPath { get; set; }
    public decimal AreaSqM { get; set; }
    public decimal MonthlyRent { get; set; }
    public bool IsOccupied { get; set; }
    public string? Description { get; set; }

    public Building? BuildingEntity { get; set; }
    public ICollection<LeaseContract> LeaseContracts { get; set; } = [];
}

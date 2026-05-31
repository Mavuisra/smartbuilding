namespace SmartBuilding.Desktop.WPF.Models;

public sealed class PropertyStructureSummary
{
    public int FloorCount { get; init; }
    public int ApartmentCount { get; init; }
    public int CommercialCount { get; init; }
    public int RoomCount { get; init; }
    public decimal TotalAreaSqM { get; init; }
}

public sealed class PropertyFloorDraft
{
    public Guid? Id { get; init; }
    public int LevelNumber { get; init; }
    public string Label { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public IReadOnlyList<PropertyApartmentDraft> Apartments { get; init; } = [];
}

public sealed class PropertyApartmentDraft
{
    public Guid? Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string UnitType { get; init; } = string.Empty;
    public decimal AreaSqM { get; init; }
    public decimal MonthlyRent { get; init; }
    public int SortOrder { get; init; }
    public IReadOnlyList<PropertyRoomDraft> Rooms { get; init; } = [];
}

public sealed class PropertyRoomDraft
{
    public Guid? Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string RoomType { get; init; } = string.Empty;
    public decimal AreaSqM { get; init; }
    public int SortOrder { get; init; }
}

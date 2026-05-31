namespace SmartBuilding.Desktop.WPF.Models;

/// <summary>Ligne du tableau de gestion patrimoine (unité louable).</summary>
public sealed class PatrimoineUnitRow
{
    public Guid ApartmentId { get; init; }
    public Guid FloorId { get; init; }
    public Guid? PremiseId { get; init; }
    public string BuildingName { get; init; } = string.Empty;
    public string FloorLabel { get; init; } = string.Empty;
    public int LevelNumber { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string UnitType { get; init; } = string.Empty;
    public decimal AreaSqM { get; init; }
    public decimal MonthlyRent { get; init; }
    public int RoomCount { get; init; }
    public string RoomsSummary { get; init; } = string.Empty;
    public string OccupancyStatus { get; init; } = string.Empty;
    public string OccupancyLabel { get; init; } = "Libre";
    public string OccupancyBadgeBackground { get; init; } = "#DCFCE7";
    public string OccupancyBadgeForeground { get; init; } = "#166534";
    public bool IsOccupied { get; init; }

    public bool IsCommercial =>
        UnitType.Contains("commercial", StringComparison.OrdinalIgnoreCase);

    public string Initials
    {
        get
        {
            var parts = Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant();
            return string.IsNullOrEmpty(Code) ? "?" : Code.Length >= 2 ? Code[..2].ToUpperInvariant() : Code.ToUpperInvariant();
        }
    }

    public string AreaDisplay => AreaSqM > 0 ? $"{AreaSqM:0.##} m²" : "—";
    public string RentDisplay => MonthlyRent > 0 ? $"{MonthlyRent:0} USD" : "—";
    public string FloorDisplay => string.IsNullOrWhiteSpace(FloorLabel) ? $"Niv. {LevelNumber}" : FloorLabel;
}

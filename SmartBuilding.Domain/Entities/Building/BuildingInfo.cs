using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Building;

public class BuildingInfo : BaseEntity
{
    public string Name { get; set; } = "Smart Building";
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public int TotalFloors { get; set; }
    public int TotalPremises { get; set; }
    public decimal TotalAreaSqM { get; set; }

    public string TimeZoneId { get; set; } = "Africa/Kinshasa";
    public string Currency { get; set; } = "USD";
    /// <summary>Nombre de CDF pour 1 USD (obligatoire si devise = USD).</summary>
    public decimal UsdExchangeRate { get; set; }
    public string DateFormat { get; set; } = "dd/MM/yyyy";
    public string Language { get; set; } = "Français";
    public string TimeFormat { get; set; } = "24 heures";
    public bool MaintenanceMode { get; set; }
    public string? LogoPath { get; set; }
}

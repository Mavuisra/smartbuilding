using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Building;

/// <summary>
/// Profil unique du bailleur (propriétaire) et du patrimoine immobilier géré.
/// Source de vérité pour les quittances PDF et la configuration métier — une seule ligne attendue.
/// </summary>
public class BuildingInfo : BaseEntity
{
    // —— Bailleur (propriétaire) ——
    public string Name { get; set; } = "Smart Building";
    public string OwnerType { get; set; } = "Particulier";
    public string? LegalRepresentative { get; set; }
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? SecondaryPhone { get; set; }
    public string Email { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string? TaxId { get; set; }
    public string Website { get; set; } = string.Empty;
    public string? BankName { get; set; }
    public string? BankAccount { get; set; }

    // —— Bâtiment / patrimoine ——
    public string BuildingDisplayName { get; set; } = string.Empty;
    public string BuildingType { get; set; } = string.Empty;
    public int TotalFloors { get; set; }
    public int TotalPremises { get; set; }
    public int ApartmentCount { get; set; }
    public int CommercialUnitCount { get; set; }
    public decimal TotalAreaSqM { get; set; }
    public int ParkingSpaces { get; set; }
    public bool HasElevator { get; set; }
    public int? YearBuilt { get; set; }
    public string EquipmentAndInstallations { get; set; } = string.Empty;
    public string ManagementRules { get; set; } = string.Empty;

    // —— Application ——
    public string TimeZoneId { get; set; } = "Africa/Kinshasa";
    public string Currency { get; set; } = "USD";
    public decimal UsdExchangeRate { get; set; }
    public string DateFormat { get; set; } = "dd/MM/yyyy";
    public string Language { get; set; } = "Français";
    public string TimeFormat { get; set; } = "24 heures";
    public bool MaintenanceMode { get; set; }
    public string? LogoPath { get; set; }
}

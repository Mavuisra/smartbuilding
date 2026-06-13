namespace SmartBuilding.Domain.Entities.Building;

/// <summary>Coordonnées par défaut — Kinshasa, Gombe (RDC).</summary>
public static class BuildingInfoDefaults
{
    public const string CompanyName = "BLOOM PROSPERTY INVESTISSEMENT";
    /// <summary>Immeuble unique géré par l'application (étages et locaux).</summary>
    public const string ManagedBuildingName = "Bloom Prosperity";
    public const string Address = "123, Avenue de la Gombe";
    public const string City = "Kinshasa";
    public const string Country = "RDC";
    public const string Phone = "+243 81 234 5678";
    public const string Email = "contact@bloomprosperity.cd";
    public const string Website = "www.bloomprosperity.cd";
    public const string NationalId = "ID Nat. —";

    public static void ApplyKinshasaDefaults(BuildingInfo building)
    {
        building.Name = CompanyName;
        building.Address = Address;
        building.City = City;
        building.Country = Country;
        building.Phone = Phone;
        building.Email = Email;
        building.Website = Website;
        building.NationalId = NationalId;
        building.TimeZoneId = "Africa/Kinshasa";
        building.Currency = "USD";
        building.Language = "Français";
        building.BuildingDisplayName = ManagedBuildingName;
    }

    public static bool NeedsKinshasaNormalization(BuildingInfo building) =>
        building.Country.Equals("France", StringComparison.OrdinalIgnoreCase) ||
        building.City.Contains("configurer", StringComparison.OrdinalIgnoreCase) ||
        building.Name.Contains("SBMS", StringComparison.OrdinalIgnoreCase) ||
        building.Name.Contains("Smart Building", StringComparison.OrdinalIgnoreCase) ||
        string.IsNullOrWhiteSpace(building.Phone);
}

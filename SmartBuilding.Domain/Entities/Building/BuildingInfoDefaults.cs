namespace SmartBuilding.Domain.Entities.Building;

/// <summary>Coordonnées officielles SBMS — Kinshasa, Gombe (RDC).</summary>
public static class BuildingInfoDefaults
{
    public const string CompanyName = "SBMS Immobilier SARL";
    public const string Address = "123, Avenue de la Gombe";
    public const string City = "Kinshasa";
    public const string Country = "RDC";
    public const string Phone = "+243 81 234 5678";
    public const string Email = "contact@sbms.cd";
    public const string Website = "www.sbms.cd";
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
        building.Currency = "CDF";
        building.Language = "Français";
    }

    public static bool NeedsKinshasaNormalization(BuildingInfo building) =>
        building.Country.Equals("France", StringComparison.OrdinalIgnoreCase) ||
        building.City.Contains("configurer", StringComparison.OrdinalIgnoreCase) ||
        building.Name.Equals("Smart Building (SB)", StringComparison.OrdinalIgnoreCase) ||
        string.IsNullOrWhiteSpace(building.Phone);
}

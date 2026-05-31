using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Location;

/// <summary>Propriétaire / locateur — personne ou société qui possède des biens loués.</summary>
public class Landlord : BaseEntity
{
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LandlordType { get; set; } = LocationConstants.LandlordTypes.Individual;
    public string Status { get; set; } = LocationConstants.LandlordStatus.Active;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? SecondaryPhone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? NationalId { get; set; }
    public string? TaxId { get; set; }
    public string? ContactPerson { get; set; }
    public string? BankName { get; set; }
    public string? BankAccount { get; set; }
    public string? Notes { get; set; }

    public ICollection<LandlordActivity> Activities { get; set; } = [];
    public ICollection<Building> Buildings { get; set; } = [];
}

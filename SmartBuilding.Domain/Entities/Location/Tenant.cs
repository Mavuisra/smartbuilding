using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Location;

public class Tenant : BaseEntity
{
    public string DossierNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RentalStatus { get; set; } = LocationConstants.TenantStatus.Active;
    public string? ProfilePhotoPath { get; set; }
    public string? Nationality { get; set; }
    public string? BusinessActivity { get; set; }
    public int PersonCount { get; set; } = 1;
    public string? IdentityDocumentPath { get; set; }
    public string? ContractDocumentPath { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string? Address { get; set; }
    public string TenantCategory { get; set; } = "Particulier";
    public string? NationalId { get; set; }
    public string? IdDocumentType { get; set; }
    public DateTime? IdDocumentExpiry { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? SecondaryPhone { get; set; }
    public string? Employer { get; set; }
    public string? PreviousAddress { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string MaritalStatus { get; set; } = string.Empty;
    public string? SpouseName { get; set; }
    public int ChildrenCount { get; set; }
    public string? Profession { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? Notes { get; set; }

    public ICollection<LeaseContract> LeaseContracts { get; set; } = [];
    public ICollection<TenantActivity> Activities { get; set; } = [];
    public ICollection<TenantDependent> Dependents { get; set; } = [];
}

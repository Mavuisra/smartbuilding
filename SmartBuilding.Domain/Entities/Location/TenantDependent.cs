using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Location;

/// <summary>Personne à charge ou membre du foyer rattaché à un locataire.</summary>
public class TenantDependent : BaseEntity
{
    public Guid TenantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string? NationalId { get; set; }
    public string? Notes { get; set; }

    public Tenant Tenant { get; set; } = null!;
}

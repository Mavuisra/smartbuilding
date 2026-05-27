using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Suppliers;

public class Supplier : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? TaxId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public string Status { get; set; } = "Actif";
    public string ContactName { get; set; } = string.Empty;
    public string Building { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public ICollection<SupplierContract> Contracts { get; set; } = [];
    public ICollection<SupplierPayment> Payments { get; set; } = [];
}

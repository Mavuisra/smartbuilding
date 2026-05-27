using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Suppliers;

public class SupplierContract : BaseEntity
{
    public Guid SupplierId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal TotalValue { get; set; }
    public string Status { get; set; } = "Actif";
    public string Building { get; set; } = string.Empty;

    public Supplier Supplier { get; set; } = null!;
}

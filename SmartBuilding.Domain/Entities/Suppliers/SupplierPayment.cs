using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Suppliers;

public class SupplierPayment : BaseEntity
{
    public Guid SupplierId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? InvoiceReference { get; set; }
    public string? Notes { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsPaid { get; set; } = true;

    public Supplier Supplier { get; set; } = null!;
}

using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Personnel;

public class SalaryPayment : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Amount { get; set; }
    public decimal GrossSalary { get; set; }
    public decimal Bonuses { get; set; }
    public decimal Penalties { get; set; }
    public decimal OvertimePay { get; set; }
    public decimal Advances { get; set; }
    public decimal Deductions { get; set; }
    public decimal NetAmount { get; set; }
    public string Status { get; set; } = RhConstants.PayrollStatus.Pending;
    public DateTime? ValidatedAt { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? Notes { get; set; }
    public string? PaySlipPdfPath { get; set; }

    public Employee Employee { get; set; } = null!;
}

using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Personnel;

public class DisciplinaryNote : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public string Category { get; set; } = RhConstants.DisciplinaryCategory.Remark;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string? IssuedBy { get; set; }
    public int Severity { get; set; } = 1;

    public Employee Employee { get; set; } = null!;
}

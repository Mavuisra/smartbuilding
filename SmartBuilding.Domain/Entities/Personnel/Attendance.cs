using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Personnel;

public class Attendance : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public DateTime Date { get; set; }
    public DateTime? CheckIn { get; set; }
    public DateTime? CheckOut { get; set; }
    public string? Notes { get; set; }
    public string PresenceStatus { get; set; } = RhConstants.PresenceStatus.NotChecked;
    public int LateMinutes { get; set; }
    public decimal WorkedHours { get; set; }
    public decimal OvertimeHours { get; set; }

    public Employee Employee { get; set; } = null!;
}

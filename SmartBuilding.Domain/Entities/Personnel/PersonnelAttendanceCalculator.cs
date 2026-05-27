namespace SmartBuilding.Domain.Entities.Personnel;

public static class PersonnelAttendanceCalculator
{
    public static void ApplyPresenceMetrics(Attendance attendance, Employee employee)
    {
        if (!employee.IsActive || employee.RhStatus is RhConstants.EmployeeStatus.Dismissed or RhConstants.EmployeeStatus.Suspended)
        {
            attendance.PresenceStatus = RhConstants.PresenceStatus.Inactive;
            attendance.LateMinutes = 0;
            attendance.WorkedHours = 0;
            attendance.OvertimeHours = 0;
            return;
        }

        if (attendance.Notes?.Contains("congé", StringComparison.OrdinalIgnoreCase) == true)
        {
            attendance.PresenceStatus = RhConstants.PresenceStatus.Leave;
            attendance.LateMinutes = 0;
            attendance.WorkedHours = 0;
            attendance.OvertimeHours = 0;
            return;
        }

        if (attendance.CheckIn is null && string.Equals(attendance.Notes, "Absent", StringComparison.OrdinalIgnoreCase))
        {
            attendance.PresenceStatus = RhConstants.PresenceStatus.Absent;
            attendance.LateMinutes = 0;
            attendance.WorkedHours = 0;
            attendance.OvertimeHours = 0;
            return;
        }

        if (attendance.CheckIn is null)
        {
            attendance.PresenceStatus = RhConstants.PresenceStatus.NotChecked;
            attendance.LateMinutes = 0;
            attendance.WorkedHours = 0;
            attendance.OvertimeHours = 0;
            return;
        }

        var checkInTime = attendance.CheckIn.Value.TimeOfDay;
        attendance.LateMinutes = checkInTime > RhConstants.WorkDayStart
            ? (int)(checkInTime - RhConstants.WorkDayStart).TotalMinutes
            : 0;

        if (attendance.CheckOut is { } checkOut)
        {
            var worked = (decimal)(checkOut - attendance.CheckIn.Value).TotalHours;
            attendance.WorkedHours = Math.Round(Math.Max(0, worked), 2);
            var standard = (decimal)RhConstants.StandardWorkHours;
            attendance.OvertimeHours = Math.Round(Math.Max(0, attendance.WorkedHours - standard), 2);

            if (checkOut.TimeOfDay < RhConstants.WorkDayEnd)
                attendance.PresenceStatus = RhConstants.PresenceStatus.EarlyLeave;
            else if (attendance.LateMinutes > 0)
                attendance.PresenceStatus = RhConstants.PresenceStatus.Late;
            else
                attendance.PresenceStatus = RhConstants.PresenceStatus.Present;
        }
        else
        {
            attendance.WorkedHours = 0;
            attendance.OvertimeHours = 0;
            attendance.PresenceStatus = attendance.LateMinutes > 0
                ? RhConstants.PresenceStatus.Late
                : RhConstants.PresenceStatus.Present;
        }
    }

    public static (string Label, string Color, string BadgeBg, string BadgeFg) ToDisplay(string presenceStatus) =>
        presenceStatus switch
        {
            RhConstants.PresenceStatus.Present => ("Présent", "#22C55E", "#DCFCE7", "#166534"),
            RhConstants.PresenceStatus.Late => ("Retard", "#EAB308", "#FEF9C3", "#854D0E"),
            RhConstants.PresenceStatus.Absent => ("Absent", "#F97316", "#FFEDD5", "#9A3412"),
            RhConstants.PresenceStatus.Leave => ("En congé", "#8B5CF6", "#F3E8FF", "#6B21A8"),
            RhConstants.PresenceStatus.EarlyLeave => ("Sortie anticipée", "#EA580C", "#FFEDD5", "#9A3412"),
            RhConstants.PresenceStatus.Inactive => ("Inactif", "#94A3B8", "#F1F5F9", "#64748B"),
            _ => ("Non pointé", "#94A3B8", "#F1F5F9", "#64748B")
        };
}

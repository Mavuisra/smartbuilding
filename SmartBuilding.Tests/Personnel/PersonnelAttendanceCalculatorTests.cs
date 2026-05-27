using SmartBuilding.Domain.Entities.Personnel;
using Xunit;

namespace SmartBuilding.Tests.Personnel;

public class PersonnelAttendanceCalculatorTests
{
    private static Employee ActiveEmployee() => new()
    {
        IsActive = true,
        RhStatus = RhConstants.EmployeeStatus.Active
    };

    [Fact]
    public void CheckIn_At_08_00_Is_Present_Not_Late()
    {
        var today = DateTime.Today;
        var attendance = new Attendance
        {
            Date = today,
            CheckIn = today.AddHours(8)
        };

        PersonnelAttendanceCalculator.ApplyPresenceMetrics(attendance, ActiveEmployee());

        Assert.Equal(RhConstants.PresenceStatus.Present, attendance.PresenceStatus);
        Assert.Equal(0, attendance.LateMinutes);
    }

    [Fact]
    public void CheckIn_At_08_15_Is_Late_15_Minutes()
    {
        var today = DateTime.Today;
        var attendance = new Attendance
        {
            Date = today,
            CheckIn = today.AddHours(8).AddMinutes(15)
        };

        PersonnelAttendanceCalculator.ApplyPresenceMetrics(attendance, ActiveEmployee());

        Assert.Equal(RhConstants.PresenceStatus.Late, attendance.PresenceStatus);
        Assert.Equal(15, attendance.LateMinutes);
    }

    [Fact]
    public void Early_CheckOut_Before_17_00_Is_Early_Leave()
    {
        var today = DateTime.Today;
        var attendance = new Attendance
        {
            Date = today,
            CheckIn = today.AddHours(8),
            CheckOut = today.AddHours(16)
        };

        PersonnelAttendanceCalculator.ApplyPresenceMetrics(attendance, ActiveEmployee());

        Assert.Equal(RhConstants.PresenceStatus.EarlyLeave, attendance.PresenceStatus);
        Assert.True(attendance.WorkedHours > 0);
    }

    [Fact]
    public void Absent_Note_Marks_Absent()
    {
        var attendance = new Attendance
        {
            Date = DateTime.Today,
            Notes = "Absent"
        };

        PersonnelAttendanceCalculator.ApplyPresenceMetrics(attendance, ActiveEmployee());

        Assert.Equal(RhConstants.PresenceStatus.Absent, attendance.PresenceStatus);
    }

    [Fact]
    public void Leave_Note_Marks_Leave()
    {
        var attendance = new Attendance
        {
            Date = DateTime.Today,
            Notes = "Congé: maladie"
        };

        PersonnelAttendanceCalculator.ApplyPresenceMetrics(attendance, ActiveEmployee());

        Assert.Equal(RhConstants.PresenceStatus.Leave, attendance.PresenceStatus);
    }
}

using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartBuilding.Desktop.WPF.Models;

public partial class PersonnelEmployeeItem : ObservableObject
{
    public Guid Id { get; init; }
    public string Matricule { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Initials { get; init; } = string.Empty;
    public string Position { get; init; } = string.Empty;
    public string Department { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string SalaryDisplay { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = "Actif";
    [ObservableProperty] private string _presenceLabel = "Présent";
    [ObservableProperty] private string _presenceColor = "#22C55E";
    [ObservableProperty] private string _presenceBadgeBackground = "#DCFCE7";
    [ObservableProperty] private string _presenceBadgeForeground = "#166534";

    public string PresenceShortLabel => PresenceLabel switch
    {
        "Non pointé" => "Non pointé",
        "En congé" => "Congé",
        _ => PresenceLabel
    };

    public void SetPresence(string label, string color, string badgeBg, string badgeFg)
    {
        PresenceLabel = label;
        PresenceColor = color;
        PresenceBadgeBackground = badgeBg;
        PresenceBadgeForeground = badgeFg;
    }
    public DateTime HireDate { get; init; }
    public decimal BaseSalary { get; init; }
    public string ContractType { get; init; } = "—";
    public string Supervisor { get; init; } = "—";
    public string Address { get; init; } = "—";
    public string Gender { get; init; } = "—";
    public DateTime? BirthDate { get; init; }
    public string SeniorityDisplay { get; init; } = "—";
    public string? ProfilePhotoPath { get; init; }
    public bool HasProfilePhoto => !string.IsNullOrWhiteSpace(ProfilePhotoPath) && File.Exists(ProfilePhotoPath);

    [ObservableProperty] private bool _isSelected;
}

public class PersonnelDepartmentSlice
{
    public string Department { get; init; } = string.Empty;
    public int Count { get; init; }
}

public class PersonnelBirthdayItem
{
    public string FullName { get; init; } = string.Empty;
    public string Initials { get; init; } = string.Empty;
    public string DateLabel { get; init; } = string.Empty;
}

public class PersonnelAlertItem
{
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string IconKind { get; init; } = "AlertCircleOutline";
    public string Color { get; init; } = "#F97316";
    public string AccentColor => Color;
    public string Background { get; init; } = "#FFEDD5";
}

public class PersonnelSummaryLine
{
    public string Label { get; init; } = string.Empty;
    public string ValueDisplay { get; init; } = string.Empty;
    public string Color { get; init; } = "#1B3D3B";
    public bool IsBold { get; init; }
}

public enum PersonnelPointageKind
{
    Present,
    CheckOut,
    Absent,
    Leave
}

public class PersonnelPageData
{
    public int TotalEmployees { get; init; }
    public int PresentToday { get; init; }
    public int AbsentToday { get; init; }
    public int OnLeaveToday { get; init; }
    public int LateToday { get; init; }
    public int NewThisMonth { get; init; }
    public decimal MonthlyPayroll { get; init; }
    public decimal RentCollectedTotal { get; init; }
    public decimal RentCollectedThisMonth { get; init; }
    public decimal TotalExpenses { get; init; }
    public decimal AvailableBalance { get; init; }
    public double PresenceRate { get; init; }
    public IReadOnlyList<PersonnelEmployeeItem> Employees { get; init; } = [];
    public IReadOnlyList<PersonnelDepartmentSlice> Departments { get; init; } = [];
    public IReadOnlyList<PersonnelBirthdayItem> Birthdays { get; init; } = [];
    public IReadOnlyList<PersonnelAlertItem> Alerts { get; init; } = [];
    public IReadOnlyList<decimal> PayrollTrend { get; init; } = [];
    public IReadOnlyList<string> PayrollLabels { get; init; } = [];
}

public class PersonnelEmployeeDetailData
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Initials { get; init; } = string.Empty;
    public string Matricule { get; init; } = string.Empty;
    public string Position { get; init; } = string.Empty;
    public string Department { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string SummaryLine { get; init; } = string.Empty;
    public string Phone { get; init; } = "—";
    public string Email { get; init; } = "—";
    public string Address { get; init; } = "—";
    public string Gender { get; init; } = "—";
    public string DateOfBirthDisplay { get; init; } = "—";
    public string AgeDisplay { get; init; } = "—";
    public string NationalId { get; init; } = "—";
    public string MaritalStatus { get; init; } = "—";
    public string EmergencyContactName { get; init; } = "—";
    public string EmergencyContactPhone { get; init; } = "—";
    public string Notes { get; init; } = "—";
    public string HireDateDisplay { get; init; } = "—";
    public string Supervisor { get; init; } = "—";
    public string WorkSchedule { get; init; } = "—";
    public string BaseSalaryDisplay { get; init; } = "—";
    public string ContractNumber { get; init; } = "—";
    public string ContractType { get; init; } = "—";
    public string ContractStartDisplay { get; init; } = "—";
    public string ContractEndDisplay { get; init; } = "—";
    public string ContractStatusLabel { get; init; } = "—";
    public string ContractStatusColor { get; init; } = "#22C55E";
    public string PresenceLabel { get; init; } = "—";
    public string PresenceBadgeBackground { get; init; } = "#F1F5F9";
    public string PresenceBadgeForeground { get; init; } = "#64748B";
    public int SalaryPaymentsCount { get; init; }
    public IReadOnlyList<PersonnelContractRow> Contracts { get; init; } = [];
    public IReadOnlyList<PersonnelSalaryRow> SalaryPayments { get; init; } = [];
    public IReadOnlyList<PersonnelAttendanceRow> Attendances { get; init; } = [];
    public string SeniorityDisplay { get; init; } = "—";
    public string? ContractPdfPath { get; init; }
    public string? ProfilePhotoPath { get; init; }
    public PersonnelEmployeePresenceStats PresenceStats { get; init; } = new();
    public IReadOnlyList<PersonnelDisciplinaryRow> DisciplinaryNotes { get; init; } = [];
    public IReadOnlyList<PersonnelActivityRow> Activities { get; init; } = [];
}

public class PersonnelEmployeePresenceStats
{
    public int PresentDays { get; init; }
    public int LateDays { get; init; }
    public int AbsentDays { get; init; }
    public int LeaveDays { get; init; }
    public decimal TotalWorkedHours { get; init; }
    public decimal TotalOvertimeHours { get; init; }
}

public class PersonnelDisciplinaryRow
{
    public string DateDisplay { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int Severity { get; init; }
}

public class PersonnelContractRow
{
    public string ContractNumber { get; init; } = string.Empty;
    public string ContractType { get; init; } = string.Empty;
    public string PeriodDisplay { get; init; } = string.Empty;
    public string SalaryDisplay { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string StatusColor { get; init; } = "#22C55E";
}

public class PersonnelSalaryRow
{
    public Guid Id { get; init; }
    public string PeriodDisplay { get; init; } = string.Empty;
    public string AmountDisplay { get; init; } = string.Empty;
    public string GrossDisplay { get; init; } = string.Empty;
    public string PaymentDateDisplay { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = "Payé";
    public string StatusColor { get; init; } = "#22C55E";
    public string? PaySlipPdfPath { get; init; }
}

public class PersonnelAttendanceHistoryRow
{
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public string Matricule { get; init; } = string.Empty;
    public string Department { get; init; } = string.Empty;
    public string DateDisplay { get; init; } = string.Empty;
    public string CheckInDisplay { get; init; } = "—";
    public string CheckOutDisplay { get; init; } = "—";
    public string StatusLabel { get; init; } = string.Empty;
    public string StatusColor { get; init; } = "#64748B";
    public string LateDisplay { get; init; } = "—";
    public string WorkedHoursDisplay { get; init; } = "—";
    public string OvertimeHoursDisplay { get; init; } = "—";
    public int LateMinutes { get; init; }
    public decimal WorkedHours { get; init; }
    public decimal OvertimeHours { get; init; }
}

public class PersonnelAttendanceRow
{
    public string DateDisplay { get; init; } = string.Empty;
    public string CheckInDisplay { get; init; } = "—";
    public string CheckOutDisplay { get; init; } = "—";
    public string StatusLabel { get; init; } = string.Empty;
    public string StatusColor { get; init; } = "#64748B";
    public string WorkedHoursDisplay { get; init; } = "—";
    public string LateDisplay { get; init; } = "—";
}

public class PersonnelActivityRow
{
    public string DateDisplay { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

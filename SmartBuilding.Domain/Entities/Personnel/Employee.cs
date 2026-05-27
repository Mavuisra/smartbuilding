using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Personnel;

public class Employee : BaseEntity
{
    public string Matricule { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public DateTime HireDate { get; set; }
    public decimal BaseSalary { get; set; }
    public bool IsActive { get; set; } = true;
    public string RhStatus { get; set; } = RhConstants.EmployeeStatus.Active;
    public string? ProfilePhotoPath { get; set; }
    public string? ContractPdfPath { get; set; }
    public DateTime? SuspendedUntil { get; set; }
    public string? SuspensionReason { get; set; }
    public DateTime? DismissedAt { get; set; }
    public string? DismissalReason { get; set; }

    public string Address { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public DateTime? BirthDate { get; set; }
    public string NationalId { get; set; } = string.Empty;
    public string MaritalStatus { get; set; } = string.Empty;
    public string EmergencyContactName { get; set; } = string.Empty;
    public string EmergencyContactPhone { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public string ContractNumber { get; set; } = string.Empty;
    public string ContractType { get; set; } = "CDI";
    public DateTime? ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }
    public string Supervisor { get; set; } = string.Empty;
    public string WorkSchedule { get; set; } = string.Empty;

    public ICollection<Attendance> Attendances { get; set; } = [];
    public ICollection<SalaryPayment> SalaryPayments { get; set; } = [];
    public ICollection<DisciplinaryNote> DisciplinaryNotes { get; set; } = [];
}

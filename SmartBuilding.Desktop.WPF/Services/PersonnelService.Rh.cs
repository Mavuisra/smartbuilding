using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Personnel;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Infrastructure.Services;

namespace SmartBuilding.Desktop.WPF.Services;

public partial class PersonnelService
{
    private readonly PersonnelPaySlipPdfService _paySlipPdf = new();

    public async Task<string> DeleteEmployeeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (employee is null)
            return "Employé introuvable.";
        employee.SoftDelete();
        await _db.SaveChangesAsync(cancellationToken);
        return string.Empty;
    }

    public async Task<string> SuspendEmployeeAsync(
        Guid id,
        string reason,
        DateTime until,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return "Le motif de suspension est obligatoire.";

        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (employee is null)
            return "Employé introuvable.";

        employee.RhStatus = RhConstants.EmployeeStatus.Suspended;
        employee.SuspensionReason = reason.Trim();
        employee.SuspendedUntil = until.Date;
        employee.MarkUpdated();

        _db.DisciplinaryNotes.Add(new DisciplinaryNote
        {
            EmployeeId = id,
            Category = RhConstants.DisciplinaryCategory.Suspension,
            Title = "Suspension",
            Description = $"Suspension jusqu'au {until:dd/MM/yyyy}. Motif : {reason.Trim()}",
            OccurredAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        return string.Empty;
    }

    public async Task<string> DismissEmployeeAsync(
        Guid id,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return "Le motif de renvoi est obligatoire.";

        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (employee is null)
            return "Employé introuvable.";

        employee.RhStatus = RhConstants.EmployeeStatus.Dismissed;
        employee.IsActive = false;
        employee.DismissalReason = reason.Trim();
        employee.DismissedAt = DateTime.UtcNow;
        employee.MarkUpdated();

        _db.DisciplinaryNotes.Add(new DisciplinaryNote
        {
            EmployeeId = id,
            Category = RhConstants.DisciplinaryCategory.Incident,
            Title = "Renvoi",
            Description = reason.Trim(),
            OccurredAt = DateTime.UtcNow,
            Severity = 5
        });

        await _db.SaveChangesAsync(cancellationToken);
        return string.Empty;
    }

    public async Task<string> AddDisciplinaryNoteAsync(
        DisciplinaryNote note,
        CancellationToken cancellationToken = default)
    {
        if (note.EmployeeId == Guid.Empty)
            return "Employé requis.";
        if (string.IsNullOrWhiteSpace(note.Title))
            return "Le titre est obligatoire.";

        note.Title = note.Title.Trim();
        note.Description = note.Description.Trim();
        note.Category = string.IsNullOrWhiteSpace(note.Category)
            ? RhConstants.DisciplinaryCategory.Remark
            : note.Category.Trim();
        if (note.OccurredAt == default)
            note.OccurredAt = DateTime.UtcNow;

        _db.DisciplinaryNotes.Add(note);
        await _db.SaveChangesAsync(cancellationToken);
        return string.Empty;
    }

    public async Task<PersonnelEmployeeItem?> RecordCheckOutAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken);
        if (employee is null)
            return null;

        var today = DateTime.Today;
        var attendance = await _db.Attendances
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date.Date == today, cancellationToken);

        if (attendance?.CheckIn is null)
            return null;

        attendance.CheckOut = DateTime.Now;
        PersonnelAttendanceCalculator.ApplyPresenceMetrics(attendance, employee);
        attendance.MarkUpdated();
        await _db.SaveChangesAsync(cancellationToken);
        return MapEmployee(employee, attendance);
    }

    public async Task<PayrollCalculationResult> CalculatePayrollAsync(
        Guid employeeId,
        int year,
        int month,
        decimal bonuses = 0,
        decimal penalties = 0,
        decimal advances = 0,
        decimal deductions = 0,
        CancellationToken cancellationToken = default)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken)
            ?? throw new InvalidOperationException("Employé introuvable.");

        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1);

        var attendances = await _db.Attendances
            .Where(a => a.EmployeeId == employeeId && a.Date >= monthStart && a.Date < monthEnd)
            .ToListAsync(cancellationToken);

        var overtimeHours = attendances.Sum(a => a.OvertimeHours);
        var overtimePay = PersonnelPayrollCalculator.ComputeOvertimePay(overtimeHours, employee.BaseSalary);
        var gross = employee.BaseSalary;
        var net = PersonnelPayrollCalculator.ComputeNet(gross, bonuses, overtimePay, penalties, advances, deductions);

        return new PayrollCalculationResult
        {
            GrossSalary = gross,
            Bonuses = bonuses,
            Penalties = penalties,
            OvertimeHours = overtimeHours,
            OvertimePay = overtimePay,
            Advances = advances,
            Deductions = deductions,
            NetAmount = net
        };
    }

    public Task<string?> ValidatePayrollAgainstTreasuryAsync(decimal netAmount, CancellationToken cancellationToken = default) =>
        _financeLedger.ValidateExpenseAsync(netAmount, cancellationToken);

    public async Task<(string Error, SalaryPayment? Payment)> CreateSalaryPaymentAsync(
        Guid employeeId,
        int year,
        int month,
        PayrollCalculationResult calc,
        bool validate = false,
        CancellationToken cancellationToken = default)
    {
        if (calc.NetAmount <= 0)
            return ("Le net à payer doit être supérieur à zéro.", null);

        var treasuryError = await _financeLedger.ValidateExpenseAsync(calc.NetAmount, cancellationToken);
        if (treasuryError is not null)
            return (treasuryError, null);

        var exists = await _db.SalaryPayments.AnyAsync(
            s => s.EmployeeId == employeeId && s.Year == year && s.Month == month,
            cancellationToken);
        if (exists)
            return ("Une paie existe déjà pour cette période.", null);

        var payment = new SalaryPayment
        {
            EmployeeId = employeeId,
            Year = year,
            Month = month,
            GrossSalary = calc.GrossSalary,
            Bonuses = calc.Bonuses,
            Penalties = calc.Penalties,
            OvertimePay = calc.OvertimePay,
            Advances = calc.Advances,
            Deductions = calc.Deductions,
            NetAmount = calc.NetAmount,
            Amount = calc.NetAmount,
            PaymentDate = DateTime.Today,
            Status = validate ? RhConstants.PayrollStatus.Validated : RhConstants.PayrollStatus.Pending,
            ValidatedAt = validate ? DateTime.UtcNow : null
        };

        _db.SalaryPayments.Add(payment);
        await _db.SaveChangesAsync(cancellationToken);

        var employee = await _db.Employees.FirstAsync(e => e.Id == employeeId, cancellationToken);
        var company = AppConfigurationService.Instance?.Current.CompanyName ?? "SBMS Smart Building";
        payment.PaySlipPdfPath = _paySlipPdf.Generate(payment, employee, company);
        payment.MarkUpdated();

        if (validate)
        {
            try
            {
                var cashError = await _financeLedger.ValidateExpenseAsync(payment.NetAmount, cancellationToken);
                if (cashError is not null)
                    return (cashError, null);

                payment.Status = RhConstants.PayrollStatus.Paid;
                await _financeLedger.RecordSalaryExpenseAsync(payment, employee, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return (ex.Message, null);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await PushDocumentAsync(payment.PaySlipPdfPath, "SalaryPayments", payment.Id, "personnel", cancellationToken);

        return (string.Empty, payment);
    }

    public async Task<string> ValidateSalaryPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        var payment = await _db.SalaryPayments
            .Include(s => s.Employee)
            .FirstOrDefaultAsync(s => s.Id == paymentId, cancellationToken);
        if (payment is null)
            return "Paie introuvable.";
        if (payment.Employee is null)
            return "Employé introuvable.";

        try
        {
            var cashError = await _financeLedger.ValidateExpenseAsync(payment.NetAmount, cancellationToken);
            if (cashError is not null)
                return cashError;

            payment.Status = RhConstants.PayrollStatus.Paid;
            payment.ValidatedAt = DateTime.UtcNow;
            payment.MarkUpdated();
            await _financeLedger.RecordSalaryExpenseAsync(payment, payment.Employee, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return string.Empty;
    }

    public async Task<string?> GeneratePaySlipPdfAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        var payment = await _db.SalaryPayments
            .Include(s => s.Employee)
            .FirstOrDefaultAsync(s => s.Id == paymentId, cancellationToken);
        if (payment?.Employee is null)
            return null;

        payment.PaySlipPdfPath = _paySlipPdf.Generate(payment, payment.Employee);
        payment.MarkUpdated();
        await _db.SaveChangesAsync(cancellationToken);
        await PushDocumentAsync(payment.PaySlipPdfPath, "SalaryPayments", payment.Id, "personnel", cancellationToken);
        return payment.PaySlipPdfPath;
    }

    public async Task<string> UpdateAttendanceAsync(
        Guid attendanceId,
        string? checkInTimeText,
        string? checkOutTimeText,
        string? statusOverride,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var attendance = await _db.Attendances
            .Include(a => a.Employee)
            .FirstOrDefaultAsync(a => a.Id == attendanceId, cancellationToken);
        if (attendance is null)
            return "Pointage introuvable.";

        var day = attendance.Date.Date;

        if (string.Equals(statusOverride, RhConstants.PresenceStatus.Absent, StringComparison.OrdinalIgnoreCase)
            || string.Equals(statusOverride, "Absent", StringComparison.OrdinalIgnoreCase))
        {
            attendance.CheckIn = null;
            attendance.CheckOut = null;
            attendance.Notes = "Absent";
        }
        else if (string.Equals(statusOverride, RhConstants.PresenceStatus.Leave, StringComparison.OrdinalIgnoreCase)
                 || string.Equals(statusOverride, "En congé", StringComparison.OrdinalIgnoreCase))
        {
            attendance.CheckIn = null;
            attendance.CheckOut = null;
            attendance.Notes = string.IsNullOrWhiteSpace(notes) ? "Congé" : $"Congé: {notes.Trim()}";
        }
        else
        {
            if (!TryParseTimeOnDate(checkInTimeText, day, out var checkIn, out var checkInError))
                return checkInError!;
            if (!TryParseTimeOnDate(checkOutTimeText, day, out var checkOut, out var checkOutError))
                return checkOutError!;

            attendance.CheckIn = checkIn;
            attendance.CheckOut = checkOut;
            if (!string.IsNullOrWhiteSpace(notes))
                attendance.Notes = notes.Trim();
            else if (statusOverride is null)
                attendance.Notes = attendance.CheckIn is null && attendance.CheckOut is null ? attendance.Notes : null;
        }

        PersonnelAttendanceCalculator.ApplyPresenceMetrics(attendance, attendance.Employee);
        attendance.MarkUpdated();
        await _db.SaveChangesAsync(cancellationToken);
        return string.Empty;
    }

    private static bool TryParseTimeOnDate(
        string? text,
        DateTime day,
        out DateTime? result,
        out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(text) || text.Trim() == "—")
        {
            result = null;
            return true;
        }

        var value = text.Trim().Replace('h', ':');
        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var ts))
        {
            result = day.Add(ts);
            return true;
        }

        if (DateTime.TryParseExact(value, ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            result = day.Add(parsed.TimeOfDay);
            return true;
        }

        result = null;
        error = $"Heure invalide : « {text} » (format HH:mm).";
        return false;
    }

    public async Task<IReadOnlyList<PersonnelAttendanceRow>> GetAttendanceHistoryAsync(
        Guid? employeeId = null,
        DateTime? from = null,
        DateTime? to = null,
        string? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Attendances.Include(a => a.Employee).AsQueryable();

        if (employeeId.HasValue)
            query = query.Where(a => a.EmployeeId == employeeId.Value);
        if (from.HasValue)
            query = query.Where(a => a.Date >= from.Value.Date);
        if (to.HasValue)
            query = query.Where(a => a.Date <= to.Value.Date);
        if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "Tous")
            query = query.Where(a => a.PresenceStatus == statusFilter);

        var rows = await query.OrderByDescending(a => a.Date).Take(500).ToListAsync(cancellationToken);

        return rows.Select(a => MapAttendanceRow(a, a.Employee)).ToList();
    }

    public async Task<IReadOnlyList<PersonnelAttendanceHistoryRow>> GetAttendanceHistoryDetailedAsync(
        Guid? employeeId = null,
        DateTime? from = null,
        DateTime? to = null,
        string? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Attendances.Include(a => a.Employee).AsQueryable();

        if (employeeId.HasValue)
            query = query.Where(a => a.EmployeeId == employeeId.Value);
        if (from.HasValue)
            query = query.Where(a => a.Date >= from.Value.Date);
        if (to.HasValue)
            query = query.Where(a => a.Date <= to.Value.Date);
        if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "Tous")
            query = query.Where(a => a.PresenceStatus == statusFilter);

        var rows = await query.OrderByDescending(a => a.Date).Take(500).ToListAsync(cancellationToken);

        return rows.Select(a =>
        {
            var presence = PersonnelAttendanceCalculator.ToDisplay(a.PresenceStatus);
            return new PersonnelAttendanceHistoryRow
            {
                EmployeeId = a.EmployeeId,
                EmployeeName = $"{a.Employee.FirstName} {a.Employee.LastName}",
                Matricule = a.Employee.Matricule,
                Department = a.Employee.Department,
                DateDisplay = a.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                CheckInDisplay = a.CheckIn.HasValue ? a.CheckIn.Value.ToString("HH:mm") : "—",
                CheckOutDisplay = a.CheckOut.HasValue ? a.CheckOut.Value.ToString("HH:mm") : "—",
                StatusLabel = presence.Label,
                StatusColor = presence.Color,
                LateDisplay = a.LateMinutes > 0 ? $"{a.LateMinutes} min" : "—",
                WorkedHoursDisplay = a.WorkedHours > 0 ? $"{a.WorkedHours:N1} h" : "—",
                OvertimeHoursDisplay = a.OvertimeHours > 0 ? $"{a.OvertimeHours:N1} h" : "—",
                LateMinutes = a.LateMinutes,
                WorkedHours = a.WorkedHours,
                OvertimeHours = a.OvertimeHours
            };
        }).ToList();
    }

    public async Task<string> ExportPayrollExcelAsync(
        int? year = null,
        int? month = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.SalaryPayments.Include(s => s.Employee).AsQueryable();
        if (year.HasValue)
            query = query.Where(s => s.Year == year.Value);
        if (month.HasValue)
            query = query.Where(s => s.Month == month.Value);

        var payments = await query.OrderByDescending(s => s.Year).ThenByDescending(s => s.Month).ToListAsync(cancellationToken);
        var employees = payments.Select(p => p.Employee).DistinctBy(e => e.Id).ToDictionary(e => e.Id);
        return PersonnelExcelExportService.ExportPayroll(payments, employees);
    }

    public async Task<string> ExportAttendanceExcelAsync(
        DateTime? from = null,
        DateTime? to = null,
        Guid? employeeId = null,
        string? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await GetAttendanceHistoryDetailedAsync(employeeId, from, to, statusFilter, cancellationToken);
        var exportRows = rows.Select(r => new AttendanceExportRow
        {
            DateDisplay = r.DateDisplay,
            Matricule = r.Matricule,
            EmployeeName = r.EmployeeName,
            CheckInDisplay = r.CheckInDisplay,
            CheckOutDisplay = r.CheckOutDisplay,
            StatusLabel = r.StatusLabel,
            LateMinutes = r.LateMinutes,
            WorkedHours = r.WorkedHours,
            OvertimeHours = r.OvertimeHours
        }).ToList();

        return PersonnelExcelExportService.ExportAttendance(exportRows);
    }

    public async Task<PersonnelAttendanceStats> GetAttendanceStatsAsync(
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var start = from?.Date ?? DateTime.Today.AddDays(-30);
        var end = to?.Date ?? DateTime.Today;

        var attendances = await _db.Attendances
            .Where(a => a.Date >= start && a.Date <= end)
            .ToListAsync(cancellationToken);

        return new PersonnelAttendanceStats
        {
            TotalRecords = attendances.Count,
            PresentCount = attendances.Count(a => a.PresenceStatus == RhConstants.PresenceStatus.Present),
            LateCount = attendances.Count(a => a.PresenceStatus == RhConstants.PresenceStatus.Late),
            AbsentCount = attendances.Count(a => a.PresenceStatus == RhConstants.PresenceStatus.Absent),
            LeaveCount = attendances.Count(a => a.PresenceStatus == RhConstants.PresenceStatus.Leave),
            EarlyLeaveCount = attendances.Count(a => a.PresenceStatus == RhConstants.PresenceStatus.EarlyLeave),
            TotalWorkedHours = attendances.Sum(a => a.WorkedHours),
            TotalOvertimeHours = attendances.Sum(a => a.OvertimeHours)
        };
    }

    public static string ResolveEmployeeStatusLabel(Employee e)
    {
        if (!string.IsNullOrWhiteSpace(e.RhStatus))
            return e.RhStatus;
        return e.IsActive ? RhConstants.EmployeeStatus.Active : "Inactif";
    }

    public static string ComputeSeniority(DateTime hireDate)
    {
        var span = DateTime.Today - hireDate.Date;
        if (span.TotalDays < 30)
            return $"{span.Days} jour(s)";
        if (span.TotalDays < 365)
            return $"{span.Days / 30} mois";
        var years = span.Days / 365;
        var months = (span.Days % 365) / 30;
        return months > 0 ? $"{years} an(s) {months} mois" : $"{years} an(s)";
    }
}

public class PayrollCalculationResult
{
    public decimal GrossSalary { get; init; }
    public decimal Bonuses { get; init; }
    public decimal Penalties { get; init; }
    public decimal OvertimeHours { get; init; }
    public decimal OvertimePay { get; init; }
    public decimal Advances { get; init; }
    public decimal Deductions { get; init; }
    public decimal NetAmount { get; init; }
}

public class PersonnelAttendanceStats
{
    public int TotalRecords { get; init; }
    public int PresentCount { get; init; }
    public int LateCount { get; init; }
    public int AbsentCount { get; init; }
    public int LeaveCount { get; init; }
    public int EarlyLeaveCount { get; init; }
    public decimal TotalWorkedHours { get; init; }
    public decimal TotalOvertimeHours { get; init; }
}

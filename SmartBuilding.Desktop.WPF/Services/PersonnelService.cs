using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Personnel;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Services;

namespace SmartBuilding.Desktop.WPF.Services;

public partial class PersonnelService
{
    private readonly SmartBuildingDbContext _db;
    private readonly FinanceLedgerService _financeLedger;

    public PersonnelService(SmartBuildingDbContext db, FinanceLedgerService financeLedger)
    {
        _db = db;
        _financeLedger = financeLedger;
    }

    public async Task<PersonnelPageData> LoadAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var employees = await _db.Employees.OrderBy(e => e.LastName).ToListAsync(cancellationToken);
        var attendances = await _db.Attendances
            .Where(a => a.Date.Date == today)
            .ToListAsync(cancellationToken);

        await _financeLedger.ReconcileAllAsync(cancellationToken);
        var cashPosition = await _financeLedger.GetCashPositionAsync(cancellationToken);

        var paidStatuses = new[] { RhConstants.PayrollStatus.Paid, RhConstants.PayrollStatus.Validated };
        var salaryPayments = await _db.SalaryPayments
            .Where(s => s.PaymentDate >= monthStart.AddMonths(-4) &&
                        paidStatuses.Contains(s.Status))
            .Select(s => new { s.Year, s.Month, s.NetAmount })
            .ToListAsync(cancellationToken);

        var salaryByMonth = salaryPayments
            .GroupBy(s => new { s.Year, s.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(x => x.NetAmount) })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToList();

        var items = employees.Select(e => MapEmployee(e, attendances.FirstOrDefault(a => a.EmployeeId == e.Id))).ToList();

        var present = items.Count(i => i.PresenceLabel is "Présent" or "Retard");
        var absent = items.Count(i => i.PresenceLabel == "Absent");
        var onLeave = items.Count(i => i.PresenceLabel == "En congé");
        var late = items.Count(i => i.PresenceLabel == "Retard");
        var total = items.Count;
        var payrollAmounts = await _db.SalaryPayments
            .Where(s => s.Year == today.Year && s.Month == today.Month &&
                        paidStatuses.Contains(s.Status))
            .Select(s => s.NetAmount)
            .ToListAsync(cancellationToken);
        var payroll = payrollAmounts.Sum();

        var departments = items
            .GroupBy(e => string.IsNullOrWhiteSpace(e.Department) ? "Autre" : e.Department)
            .OrderByDescending(g => g.Count())
            .Select(g => new PersonnelDepartmentSlice { Department = g.Key, Count = g.Count() })
            .ToList();

        var payrollTrend = new List<decimal>();
        var payrollLabels = new List<string>();
        for (var i = 4; i >= 0; i--)
        {
            var d = monthStart.AddMonths(-i);
            var sum = salaryByMonth
                .Where(s => s.Year == d.Year && s.Month == d.Month)
                .Select(s => s.Total)
                .FirstOrDefault();
            payrollTrend.Add(sum);
            payrollLabels.Add(d.ToString("MMM"));
        }

        var alerts = new List<PersonnelAlertItem>();
        if (absent > 0)
            alerts.Add(new()
            {
                Title = "Absences du jour",
                Message = $"{absent} employé(s) marqué(s) absent(s)",
                IconKind = "AccountOffOutline",
                Color = "#F97316",
                Background = "#FFEDD5"
            });
        if (onLeave > 0)
            alerts.Add(new()
            {
                Title = "Congés en cours",
                Message = $"{onLeave} employé(s) en congé aujourd'hui",
                IconKind = "Beach",
                Color = "#8B5CF6",
                Background = "#EDE9FE"
            });
        if (late > 0)
            alerts.Add(new()
            {
                Title = "Retards signalés",
                Message = $"{late} employé(s) en retard ce matin",
                IconKind = "ClockAlertOutline",
                Color = "#EAB308",
                Background = "#FEF9C3"
            });

        var expiringContracts = employees.Count(e =>
            e.ContractEndDate is { } end && end.Date <= today.AddDays(30) && end.Date >= today);
        if (expiringContracts > 0)
            alerts.Add(new()
            {
                Title = "Contrats à échéance",
                Message = $"{expiringContracts} contrat(s) expire(nt) sous 30 jours",
                IconKind = "FileAlertOutline",
                Color = "#EA580C",
                Background = "#FFEDD5"
            });

        var suspended = employees.Count(e => e.RhStatus == RhConstants.EmployeeStatus.Suspended);
        if (suspended > 0)
            alerts.Add(new()
            {
                Title = "Suspensions actives",
                Message = $"{suspended} employé(s) suspendu(s)",
                IconKind = "AccountCancelOutline",
                Color = "#DC2626",
                Background = "#FEE2E2"
            });

        var pendingPayroll = await _db.SalaryPayments
            .CountAsync(s => s.Status == RhConstants.PayrollStatus.Pending, cancellationToken);
        if (pendingPayroll > 0)
            alerts.Add(new()
            {
                Title = "Paies en attente",
                Message = $"{pendingPayroll} fiche(s) de paie à valider",
                IconKind = "CashClock",
                Color = "#2563EB",
                Background = "#DBEAFE"
            });

        if (cashPosition.AvailableThisMonth <= 0 && cashPosition.RentCollectedThisMonth > 0)
            alerts.Add(new()
            {
                Title = "Trésorerie insuffisante",
                Message = "Les dépenses du mois ont atteint les loyers encaissés ce mois.",
                IconKind = "CashRemove",
                Color = "#DC2626",
                Background = "#FEE2E2"
            });
        else if (payroll > cashPosition.AvailableThisMonth)
            alerts.Add(new()
            {
                Title = "Paies vs trésorerie",
                Message = $"Paies du mois ({MoneyFormatter.Format(payroll)}) dépassent le disponible ({MoneyFormatter.Format(cashPosition.AvailableThisMonth)}).",
                IconKind = "Alert",
                Color = "#EA580C",
                Background = "#FFEDD5"
            });

        if (alerts.Count == 0)
            alerts.Add(new()
            {
                Title = "Effectif sous contrôle",
                Message = "Aucune alerte RH critique aujourd'hui",
                IconKind = "CheckCircleOutline",
                Color = "#22C55E",
                Background = "#DCFCE7"
            });

        return new PersonnelPageData
        {
            TotalEmployees = total,
            PresentToday = present,
            AbsentToday = absent,
            OnLeaveToday = onLeave,
            LateToday = late,
            NewThisMonth = employees.Count(e => e.HireDate >= monthStart),
            MonthlyPayroll = payroll,
            RentCollectedTotal = cashPosition.RentCollectedTotal,
            RentCollectedThisMonth = cashPosition.RentCollectedThisMonth,
            AvailableBalance = cashPosition.AvailableThisMonth,
            TotalExpenses = cashPosition.TotalExpensesThisMonth,
            PresenceRate = total > 0 ? Math.Round((double)present / total * 100, 1) : 0,
            Employees = items,
            Departments = departments,
            Birthdays = [],
            Alerts = alerts,
            PayrollTrend = payrollTrend,
            PayrollLabels = payrollLabels
        };
    }

    public async Task<string> CreateEmployeeAsync(Employee employee, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(employee.Matricule))
            return "Le matricule est obligatoire.";
        if (string.IsNullOrWhiteSpace(employee.FirstName) || string.IsNullOrWhiteSpace(employee.LastName))
            return "Le prénom et le nom sont obligatoires.";

        var matriculeExists = await _db.Employees
            .AnyAsync(e => e.Matricule == employee.Matricule.Trim(), cancellationToken);
        if (matriculeExists)
            return "Ce matricule existe déjà.";

        if (employee.HireDate == default)
            employee.HireDate = DateTime.UtcNow.Date;

        employee.Matricule = employee.Matricule.Trim();
        employee.FirstName = employee.FirstName.Trim();
        employee.LastName = employee.LastName.Trim();
        employee.Email = employee.Email.Trim();
        employee.Phone = employee.Phone.Trim();
        employee.Position = employee.Position.Trim();
        employee.Department = employee.Department.Trim();
        employee.Address = employee.Address.Trim();
        employee.Gender = employee.Gender.Trim();
        employee.NationalId = employee.NationalId.Trim();
        employee.MaritalStatus = employee.MaritalStatus.Trim();
        employee.EmergencyContactName = employee.EmergencyContactName.Trim();
        employee.EmergencyContactPhone = employee.EmergencyContactPhone.Trim();
        employee.Notes = employee.Notes.Trim();
        employee.ContractNumber = employee.ContractNumber.Trim();
        employee.ContractType = string.IsNullOrWhiteSpace(employee.ContractType) ? "CDI" : employee.ContractType.Trim();
        employee.Supervisor = employee.Supervisor.Trim();
        employee.WorkSchedule = employee.WorkSchedule.Trim();
        if (!employee.ContractStartDate.HasValue)
            employee.ContractStartDate = employee.HireDate;
        if (string.IsNullOrWhiteSpace(employee.ContractNumber))
            employee.ContractNumber = $"CTR-{employee.Matricule}";
        if (string.IsNullOrWhiteSpace(employee.RhStatus))
            employee.RhStatus = RhConstants.EmployeeStatus.Active;
        employee.IsSynced = false;

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync(cancellationToken);
        return string.Empty;
    }

    public async Task<string> GenerateNextMatriculeAsync(CancellationToken cancellationToken = default)
    {
        var count = await _db.Employees.CountAsync(cancellationToken);
        return $"EMP-{(count + 1):D4}";
    }

    public async Task<Employee?> GetEmployeeAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.Employees.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<PersonnelEmployeeDetailData?> GetEmployeeDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (employee is null)
            return null;

        var attendanceToday = await _db.Attendances
            .FirstOrDefaultAsync(a => a.EmployeeId == id && a.Date.Date == today, cancellationToken);

        var salaryPayments = await _db.SalaryPayments
            .Where(s => s.EmployeeId == id)
            .OrderByDescending(s => s.Year).ThenByDescending(s => s.Month)
            .Take(24)
            .ToListAsync(cancellationToken);

        var disciplinaryNotes = await _db.DisciplinaryNotes
            .Where(n => n.EmployeeId == id)
            .OrderByDescending(n => n.OccurredAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        var attendances = await _db.Attendances
            .Where(a => a.EmployeeId == id)
            .OrderByDescending(a => a.Date)
            .Take(30)
            .ToListAsync(cancellationToken);

        var item = MapEmployee(employee, attendanceToday);
        var contract = BuildContractRow(employee);
        var contractStatus = ResolveContractStatus(employee);

        var activities = new List<PersonnelActivityRow>
        {
            new()
            {
                DateDisplay = FormatDate(employee.HireDate),
                Category = "RH",
                Title = "Embauche",
                Description = $"Intégration en poste : {employee.Position} ({employee.Department})."
            }
        };

        if (employee.ContractStartDate.HasValue)
        {
            activities.Add(new PersonnelActivityRow
            {
                DateDisplay = FormatDate(employee.ContractStartDate.Value),
                Category = "Contrat",
                Title = $"Contrat {employee.ContractType}",
                Description = $"Début du contrat {ResolveContractNumber(employee)}."
            });
        }

        if (employee.ContractEndDate.HasValue)
        {
            activities.Add(new PersonnelActivityRow
            {
                DateDisplay = FormatDate(employee.ContractEndDate.Value),
                Category = "Contrat",
                Title = "Fin de contrat prévue",
                Description = $"Échéance du contrat {ResolveContractNumber(employee)}."
            });
        }

        foreach (var s in salaryPayments.Take(5))
        {
            activities.Add(new PersonnelActivityRow
            {
                DateDisplay = FormatDate(s.PaymentDate),
                Category = "Paie",
                Title = $"Salaire {s.Month:00}/{s.Year}",
                Description = $"Versement de {MoneyFormatter.Format(s.NetAmount > 0 ? s.NetAmount : s.Amount)} — {s.Status}."
            });
        }

        foreach (var n in disciplinaryNotes.Take(10))
        {
            activities.Add(new PersonnelActivityRow
            {
                DateDisplay = FormatDate(n.OccurredAt),
                Category = n.Category,
                Title = n.Title,
                Description = n.Description
            });
        }

        activities = activities.OrderByDescending(a => TryParseDisplayDate(a.DateDisplay)).ToList();

        var monthStart = new DateTime(today.Year, today.Month, 1);
        var monthAttendances = await _db.Attendances
            .Where(a => a.EmployeeId == id && a.Date >= monthStart)
            .ToListAsync(cancellationToken);
        var presenceStats = new PersonnelEmployeePresenceStats
        {
            PresentDays = monthAttendances.Count(a => a.PresenceStatus == RhConstants.PresenceStatus.Present),
            LateDays = monthAttendances.Count(a => a.PresenceStatus == RhConstants.PresenceStatus.Late),
            AbsentDays = monthAttendances.Count(a => a.PresenceStatus == RhConstants.PresenceStatus.Absent),
            LeaveDays = monthAttendances.Count(a => a.PresenceStatus == RhConstants.PresenceStatus.Leave),
            TotalWorkedHours = monthAttendances.Sum(a => a.WorkedHours),
            TotalOvertimeHours = monthAttendances.Sum(a => a.OvertimeHours)
        };

        return new PersonnelEmployeeDetailData
        {
            Id = employee.Id,
            FullName = item.FullName,
            Initials = item.Initials,
            Matricule = employee.Matricule,
            Position = employee.Position,
            Department = employee.Department,
            StatusLabel = item.StatusLabel,
            SummaryLine = $"{employee.Matricule} · {employee.Position} · {employee.Department}",
            Phone = DisplayOrDash(employee.Phone),
            Email = DisplayOrDash(employee.Email),
            Address = DisplayOrDash(employee.Address),
            Gender = DisplayOrDash(employee.Gender),
            DateOfBirthDisplay = employee.BirthDate.HasValue ? FormatDate(employee.BirthDate.Value) : "—",
            AgeDisplay = employee.BirthDate.HasValue ? $"{(int)((today - employee.BirthDate.Value).TotalDays / 365.25)} ans" : "—",
            NationalId = DisplayOrDash(employee.NationalId),
            MaritalStatus = DisplayOrDash(employee.MaritalStatus),
            EmergencyContactName = DisplayOrDash(employee.EmergencyContactName),
            EmergencyContactPhone = DisplayOrDash(employee.EmergencyContactPhone),
            Notes = DisplayOrDash(employee.Notes),
            HireDateDisplay = FormatDate(employee.HireDate),
            Supervisor = DisplayOrDash(employee.Supervisor),
            WorkSchedule = DisplayOrDash(employee.WorkSchedule),
            BaseSalaryDisplay = MoneyFormatter.Format(employee.BaseSalary),
            ContractNumber = ResolveContractNumber(employee),
            ContractType = DisplayOrDash(employee.ContractType),
            ContractStartDisplay = employee.ContractStartDate.HasValue
                ? FormatDate(employee.ContractStartDate.Value)
                : FormatDate(employee.HireDate),
            ContractEndDisplay = employee.ContractEndDate.HasValue
                ? FormatDate(employee.ContractEndDate.Value)
                : employee.ContractType.Equals("CDI", StringComparison.OrdinalIgnoreCase) ? "Indéterminée" : "—",
            ContractStatusLabel = contractStatus.Label,
            ContractStatusColor = contractStatus.Color,
            PresenceLabel = item.PresenceLabel,
            PresenceBadgeBackground = item.PresenceBadgeBackground,
            PresenceBadgeForeground = item.PresenceBadgeForeground,
            SalaryPaymentsCount = salaryPayments.Count,
            Contracts = [contract],
            SeniorityDisplay = ComputeSeniority(employee.HireDate),
            ContractPdfPath = employee.ContractPdfPath,
            ProfilePhotoPath = employee.ProfilePhotoPath,
            SalaryPayments = salaryPayments.Select(s => new PersonnelSalaryRow
            {
                Id = s.Id,
                PeriodDisplay = $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(s.Month)} {s.Year}",
                AmountDisplay = MoneyFormatter.Format(s.NetAmount > 0 ? s.NetAmount : s.Amount),
                GrossDisplay = MoneyFormatter.Format(s.GrossSalary > 0 ? s.GrossSalary : s.Amount),
                PaymentDateDisplay = FormatDate(s.PaymentDate),
                StatusLabel = s.Status,
                StatusColor = s.Status switch
                {
                    RhConstants.PayrollStatus.Paid => "#22C55E",
                    RhConstants.PayrollStatus.Validated => "#2563EB",
                    _ => "#EAB308"
                },
                PaySlipPdfPath = s.PaySlipPdfPath
            }).ToList(),
            Attendances = attendances.Select(a => MapAttendanceRow(a, employee)).ToList(),
            DisciplinaryNotes = disciplinaryNotes.Select(n => new PersonnelDisciplinaryRow
            {
                DateDisplay = FormatDate(n.OccurredAt),
                Category = n.Category,
                Title = n.Title,
                Description = n.Description,
                Severity = n.Severity
            }).ToList(),
            PresenceStats = presenceStats,
            Activities = activities
        };
    }

    public async Task<string> UpdateEmployeeAsync(Employee employee, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(employee.Matricule))
            return "Le matricule est obligatoire.";
        if (string.IsNullOrWhiteSpace(employee.FirstName) || string.IsNullOrWhiteSpace(employee.LastName))
            return "Le prénom et le nom sont obligatoires.";

        var existing = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employee.Id, cancellationToken);
        if (existing is null)
            return "Employé introuvable.";

        var matriculeExists = await _db.Employees
            .AnyAsync(e => e.Matricule == employee.Matricule.Trim() && e.Id != employee.Id, cancellationToken);
        if (matriculeExists)
            return "Ce matricule existe déjà.";

        existing.Matricule = employee.Matricule.Trim();
        existing.FirstName = employee.FirstName.Trim();
        existing.LastName = employee.LastName.Trim();
        existing.Email = employee.Email.Trim();
        existing.Phone = employee.Phone.Trim();
        existing.Position = employee.Position.Trim();
        existing.Department = employee.Department.Trim();
        existing.BaseSalary = employee.BaseSalary;
        existing.HireDate = employee.HireDate;
        existing.IsActive = employee.IsActive;
        existing.RhStatus = string.IsNullOrWhiteSpace(employee.RhStatus)
            ? (employee.IsActive ? RhConstants.EmployeeStatus.Active : RhConstants.EmployeeStatus.Dismissed)
            : employee.RhStatus.Trim();
        existing.Address = employee.Address.Trim();
        existing.Gender = employee.Gender.Trim();
        existing.BirthDate = employee.BirthDate;
        existing.NationalId = employee.NationalId.Trim();
        existing.MaritalStatus = employee.MaritalStatus.Trim();
        existing.EmergencyContactName = employee.EmergencyContactName.Trim();
        existing.EmergencyContactPhone = employee.EmergencyContactPhone.Trim();
        existing.Notes = employee.Notes.Trim();
        existing.ContractNumber = string.IsNullOrWhiteSpace(employee.ContractNumber)
            ? $"CTR-{employee.Matricule.Trim()}"
            : employee.ContractNumber.Trim();
        existing.ContractType = string.IsNullOrWhiteSpace(employee.ContractType) ? "CDI" : employee.ContractType.Trim();
        existing.ContractStartDate = employee.ContractStartDate ?? employee.HireDate;
        existing.ContractEndDate = employee.ContractEndDate;
        existing.Supervisor = employee.Supervisor.Trim();
        existing.WorkSchedule = employee.WorkSchedule.Trim();
        if (!string.IsNullOrWhiteSpace(employee.ContractPdfPath))
            existing.ContractPdfPath = employee.ContractPdfPath;
        if (!string.IsNullOrWhiteSpace(employee.ProfilePhotoPath))
            existing.ProfilePhotoPath = employee.ProfilePhotoPath;
        existing.MarkUpdated();

        await _db.SaveChangesAsync(cancellationToken);
        return string.Empty;
    }

    public async Task<PersonnelEmployeeItem?> RecordPointageAsync(
        Guid employeeId,
        PersonnelPointageKind kind,
        string? leaveReason = null,
        CancellationToken cancellationToken = default)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken);
        if (employee is null)
            return null;

        var today = DateTime.Today;
        var attendance = await _db.Attendances
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date.Date == today, cancellationToken);

        if (attendance is null)
        {
            attendance = new Attendance
            {
                EmployeeId = employeeId,
                Date = today
            };
            _db.Attendances.Add(attendance);
        }

        switch (kind)
        {
            case PersonnelPointageKind.Present:
                attendance.CheckIn ??= DateTime.Now;
                attendance.Notes = null;
                break;
            case PersonnelPointageKind.CheckOut:
                attendance.CheckOut = DateTime.Now;
                break;
            case PersonnelPointageKind.Absent:
                attendance.CheckIn = null;
                attendance.CheckOut = null;
                attendance.Notes = "Absent";
                break;
            case PersonnelPointageKind.Leave:
                attendance.CheckIn = null;
                attendance.CheckOut = null;
                attendance.Notes = string.IsNullOrWhiteSpace(leaveReason)
                    ? "Congé"
                    : $"Congé: {leaveReason.Trim()}";
                break;
        }

        PersonnelAttendanceCalculator.ApplyPresenceMetrics(attendance, employee);
        attendance.MarkUpdated();
        await _db.SaveChangesAsync(cancellationToken);
        return MapEmployee(employee, attendance);
    }

    private static PersonnelEmployeeItem MapEmployee(Employee e, Attendance? attendance)
    {
        var presence = ResolvePresence(e, attendance);
        return new PersonnelEmployeeItem
        {
            Id = e.Id,
            Matricule = e.Matricule,
            FullName = $"{e.FirstName} {e.LastName}",
            Initials = GetInitials(e.FirstName, e.LastName),
            Position = e.Position,
            Department = e.Department,
            Phone = e.Phone,
            Email = e.Email,
            SalaryDisplay = MoneyFormatter.Format(e.BaseSalary),
            StatusLabel = ResolveEmployeeStatusLabel(e),
            SeniorityDisplay = ComputeSeniority(e.HireDate),
            PresenceLabel = presence.Label,
            PresenceColor = presence.Color,
            PresenceBadgeBackground = presence.BadgeBg,
            PresenceBadgeForeground = presence.BadgeFg,
            HireDate = e.HireDate,
            BaseSalary = e.BaseSalary,
            ContractType = DisplayOrDash(e.ContractType),
            Supervisor = DisplayOrDash(e.Supervisor),
            Address = DisplayOrDash(e.Address),
            Gender = DisplayOrDash(e.Gender),
            BirthDate = e.BirthDate,
            ProfilePhotoPath = e.ProfilePhotoPath
        };
    }

    private static PersonnelContractRow BuildContractRow(Employee e)
    {
        var status = ResolveContractStatus(e);
        var start = e.ContractStartDate ?? e.HireDate;
        var endText = e.ContractEndDate.HasValue
            ? FormatDate(e.ContractEndDate.Value)
            : e.ContractType.Equals("CDI", StringComparison.OrdinalIgnoreCase) ? "Indéterminée" : "—";

        return new PersonnelContractRow
        {
            ContractNumber = ResolveContractNumber(e),
            ContractType = DisplayOrDash(e.ContractType),
            PeriodDisplay = $"{FormatDate(start)} → {endText}",
            SalaryDisplay = MoneyFormatter.Format(e.BaseSalary),
            StatusLabel = status.Label,
            StatusColor = status.Color
        };
    }

    private static (string Label, string Color) ResolveContractStatus(Employee e)
    {
        if (!e.IsActive)
            return ("Inactif", "#94A3B8");

        if (e.ContractEndDate is { } end && end.Date < DateTime.Today)
            return ("Expiré", "#DC2626");

        if (e.ContractEndDate is { } soon && soon.Date <= DateTime.Today.AddDays(60))
            return ("Expire bientôt", "#EA580C");

        return ("Actif", "#22C55E");
    }

    private static PersonnelAttendanceRow MapAttendanceRow(Attendance a, Employee e)
    {
        var presence = ResolvePresence(e, a);
        return new PersonnelAttendanceRow
        {
            DateDisplay = FormatDate(a.Date),
            CheckInDisplay = a.CheckIn.HasValue ? a.CheckIn.Value.ToString("HH:mm") : "—",
            CheckOutDisplay = a.CheckOut.HasValue ? a.CheckOut.Value.ToString("HH:mm") : "—",
            StatusLabel = presence.Label,
            StatusColor = presence.Color,
            WorkedHoursDisplay = a.WorkedHours > 0 ? $"{a.WorkedHours:N1} h" : "—",
            LateDisplay = a.LateMinutes > 0 ? $"{a.LateMinutes} min" : "—"
        };
    }

    private static string ResolveContractNumber(Employee e) =>
        string.IsNullOrWhiteSpace(e.ContractNumber) ? $"CTR-{e.Matricule}" : e.ContractNumber;

    private static string DisplayOrDash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private static string FormatDate(DateTime date) =>
        date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static DateTime TryParseDisplayDate(string display) =>
        DateTime.TryParseExact(display, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : DateTime.MinValue;

    /// <summary>Statut de présence pour la journée en cours (un enregistrement Attendance par employé et par date).</summary>
    private static (string Label, string Color, string BadgeBg, string BadgeFg) ResolvePresence(Employee e, Attendance? attendance)
    {
        if (!e.IsActive || e.RhStatus is RhConstants.EmployeeStatus.Dismissed or RhConstants.EmployeeStatus.Suspended)
            return PersonnelAttendanceCalculator.ToDisplay(RhConstants.PresenceStatus.Inactive);

        if (attendance is null)
            return PersonnelAttendanceCalculator.ToDisplay(RhConstants.PresenceStatus.NotChecked);

        if (string.IsNullOrWhiteSpace(attendance.PresenceStatus) ||
            attendance.PresenceStatus == RhConstants.PresenceStatus.NotChecked)
            PersonnelAttendanceCalculator.ApplyPresenceMetrics(attendance, e);

        return PersonnelAttendanceCalculator.ToDisplay(attendance.PresenceStatus);
    }

    private static string GetInitials(string first, string last) =>
        $"{(first.Length > 0 ? first[0] : '?')}{(last.Length > 0 ? last[0] : '?')}".ToUpper();
}

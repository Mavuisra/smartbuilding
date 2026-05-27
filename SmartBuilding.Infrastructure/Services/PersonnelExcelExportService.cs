using OfficeOpenXml;
using SmartBuilding.Domain.Entities.Personnel;

namespace SmartBuilding.Infrastructure.Services;

public static class PersonnelExcelExportService
{
    static PersonnelExcelExportService()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    public static string ExportPayroll(
        IReadOnlyList<SalaryPayment> payments,
        IReadOnlyDictionary<Guid, Employee> employeesById)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SBMS", "Exports");
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"Paies_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add("Paies");

        var headers = new[]
        {
            "Matricule", "Employé", "Département", "Période", "Brut", "Primes", "HS",
            "Pénalités", "Avances", "Retenues", "Net", "Statut", "Date paiement"
        };
        for (var c = 0; c < headers.Length; c++)
            sheet.Cells[1, c + 1].Value = headers[c];

        var row = 2;
        foreach (var p in payments.OrderByDescending(x => x.Year).ThenByDescending(x => x.Month))
        {
            employeesById.TryGetValue(p.EmployeeId, out var emp);
            sheet.Cells[row, 1].Value = emp?.Matricule ?? "—";
            sheet.Cells[row, 2].Value = emp is null ? "—" : $"{emp.FirstName} {emp.LastName}";
            sheet.Cells[row, 3].Value = emp?.Department ?? "—";
            sheet.Cells[row, 4].Value = $"{p.Month:00}/{p.Year}";
            sheet.Cells[row, 5].Value = p.GrossSalary > 0 ? p.GrossSalary : p.Amount;
            sheet.Cells[row, 6].Value = p.Bonuses;
            sheet.Cells[row, 7].Value = p.OvertimePay;
            sheet.Cells[row, 8].Value = p.Penalties;
            sheet.Cells[row, 9].Value = p.Advances;
            sheet.Cells[row, 10].Value = p.Deductions;
            sheet.Cells[row, 11].Value = p.NetAmount > 0 ? p.NetAmount : p.Amount;
            sheet.Cells[row, 12].Value = p.Status;
            sheet.Cells[row, 13].Value = p.PaymentDate.ToString("dd/MM/yyyy");
            row++;
        }

        sheet.Cells[1, 1, 1, headers.Length].Style.Font.Bold = true;
        sheet.Cells.AutoFitColumns();
        package.SaveAs(new FileInfo(path));
        return path;
    }

    public static string ExportAttendance(IReadOnlyList<AttendanceExportRow> rows)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SBMS", "Exports");
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"Pointages_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add("Pointages");

        var headers = new[]
        {
            "Date", "Matricule", "Employé", "Arrivée", "Départ", "Statut", "Retard (min)", "Heures", "HS"
        };
        for (var c = 0; c < headers.Length; c++)
            sheet.Cells[1, c + 1].Value = headers[c];

        var row = 2;
        foreach (var r in rows)
        {
            sheet.Cells[row, 1].Value = r.DateDisplay;
            sheet.Cells[row, 2].Value = r.Matricule;
            sheet.Cells[row, 3].Value = r.EmployeeName;
            sheet.Cells[row, 4].Value = r.CheckInDisplay;
            sheet.Cells[row, 5].Value = r.CheckOutDisplay;
            sheet.Cells[row, 6].Value = r.StatusLabel;
            sheet.Cells[row, 7].Value = r.LateMinutes;
            sheet.Cells[row, 8].Value = r.WorkedHours;
            sheet.Cells[row, 9].Value = r.OvertimeHours;
            row++;
        }

        sheet.Cells[1, 1, 1, headers.Length].Style.Font.Bold = true;
        sheet.Cells.AutoFitColumns();
        package.SaveAs(new FileInfo(path));
        return path;
    }
}

public class AttendanceExportRow
{
    public string DateDisplay { get; init; } = string.Empty;
    public string Matricule { get; init; } = string.Empty;
    public string EmployeeName { get; init; } = string.Empty;
    public string CheckInDisplay { get; init; } = "—";
    public string CheckOutDisplay { get; init; } = "—";
    public string StatusLabel { get; init; } = string.Empty;
    public int LateMinutes { get; init; }
    public decimal WorkedHours { get; init; }
    public decimal OvertimeHours { get; init; }
}

namespace SmartBuilding.Domain.Entities.Personnel;

public static class PersonnelPayrollCalculator
{
    public static decimal ComputeHourlyRate(decimal baseSalary) =>
        baseSalary / (decimal)RhConstants.StandardWorkHours / 22m;

    public static decimal ComputeOvertimePay(decimal overtimeHours, decimal baseSalary) =>
        Math.Round(overtimeHours * ComputeHourlyRate(baseSalary) * 1.5m, 2);

    public static decimal ComputeNet(
        decimal grossSalary,
        decimal bonuses,
        decimal overtimePay,
        decimal penalties,
        decimal advances,
        decimal deductions) =>
        Math.Max(0, grossSalary + bonuses + overtimePay - penalties - advances - deductions);
}

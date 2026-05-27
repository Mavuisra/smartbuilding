using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Technical;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Services;

namespace SmartBuilding.Desktop.WPF.Services;

public class TechnicalService
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    private readonly SmartBuildingDbContext _db;
    private readonly FinanceLedgerService _financeLedger;
    private readonly TechnicalDataCleaner _technicalCleaner;

    public TechnicalService(
        SmartBuildingDbContext db,
        FinanceLedgerService financeLedger,
        TechnicalDataCleaner technicalCleaner)
    {
        _db = db;
        _financeLedger = financeLedger;
        _technicalCleaner = technicalCleaner;
    }

    /// <summary>Supprime les maintenances et dépenses fictives du module Technique.</summary>
    public Task<int> ClearFictitiousMaintenanceAsync(CancellationToken cancellationToken = default) =>
        _technicalCleaner.ClearFictitiousMaintenanceAsync(cancellationToken);

    public async Task<TechnicalPageData> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _technicalCleaner.ClearFictitiousMaintenanceAsync(cancellationToken);

        var cash = await TreasuryLoader.LoadAsync(_financeLedger, cancellationToken);
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var weekEnd = today.AddDays(7);

        var equipment = await _db.Equipment
            .Include(e => e.MaintenanceRecords)
            .OrderBy(e => e.Code)
            .ToListAsync(cancellationToken);

        var maintenance = await _db.MaintenanceRecords.ToListAsync(cancellationToken);

        var total = equipment.Count;
        var t = Math.Max(total, 1);
        var operational = equipment.Count(e => e.Status == EquipmentStatus.Operationnel);
        var inMaint = equipment.Count(e => e.Status == EquipmentStatus.Maintenance);
        var broken = equipment.Count(e => e.Status == EquipmentStatus.EnPanne);
        var monthEnd = monthStart.AddMonths(1);
        var monthlyCost = maintenance
            .Where(m => m.CompletedDate is { } cd && cd >= monthStart && cd < monthEnd)
            .Sum(m => m.Cost);

        var plannedWeek = equipment.Count(e =>
            e.NextMaintenanceDate is { } d && d >= today && d <= weekEnd)
            + maintenance.Count(m => m.CompletedDate is null && m.ScheduledDate >= today && m.ScheduledDate <= weekEnd);

        var items = equipment.Select(e => MapEquipment(e, maintenance.Where(m => m.EquipmentId == e.Id))).ToList();

        var categoryDist = equipment
            .GroupBy(e => string.IsNullOrWhiteSpace(e.Category) ? "Autre" : e.Category)
            .OrderByDescending(g => g.Count())
            .Select(g => new TechnicalCategorySlice { Category = g.Key, Count = g.Count() })
            .ToList();

        var statusDist = new[]
        {
            new TechnicalStatusSlice { Status = "Opérationnels", Count = operational },
            new TechnicalStatusSlice { Status = "En maintenance", Count = inMaint },
            new TechnicalStatusSlice { Status = "En panne", Count = broken },
            new TechnicalStatusSlice { Status = "Hors service", Count = equipment.Count(e => e.Status == EquipmentStatus.HorsService) }
        }.Where(s => s.Count > 0).ToList();

        var costTrend = new List<TechnicalMonthPoint>();
        for (var i = 5; i >= 0; i--)
        {
            var m = monthStart.AddMonths(-i);
            var end = m.AddMonths(1);
            var sum = maintenance
                .Where(x => x.CompletedDate is { } cd && cd >= m && cd < end)
                .Sum(x => x.Cost);
            costTrend.Add(new TechnicalMonthPoint { Label = m.ToString("MMM", Fr), Cost = sum });
        }

        return new TechnicalPageData
        {
            RentCollectedTotal = cash.RentCollectedTotal,
            AvailableBalance = cash.AvailableBalance,
            TotalExpenses = cash.TotalExpenses,
            TotalEquipment = total,
            OperationalCount = operational,
            MaintenanceCount = inMaint,
            BrokenCount = broken,
            OperationalPercent = $"{operational * 100.0 / t:F2}%",
            MaintenancePercent = $"{inMaint * 100.0 / t:F2}%",
            BrokenPercent = $"{broken * 100.0 / t:F2}%",
            MonthlyMaintenanceCost = monthlyCost,
            PlannedThisWeek = plannedWeek,
            Equipment = items,
            CategoryDistribution = categoryDist,
            StatusDistribution = statusDist,
            MaintenanceCostTrend = costTrend
        };
    }

    public async Task<string> CreateEquipmentAsync(Equipment equipment, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(equipment.Code))
            return "Le code équipement est obligatoire.";
        if (string.IsNullOrWhiteSpace(equipment.Name))
            return "Le nom est obligatoire.";

        if (await _db.Equipment.AnyAsync(e => e.Code == equipment.Code.Trim(), cancellationToken))
            return "Ce code existe déjà.";

        equipment.Code = equipment.Code.Trim();
        equipment.Name = equipment.Name.Trim();
        equipment.Category = string.IsNullOrWhiteSpace(equipment.Category) ? "Autre" : equipment.Category.Trim();
        equipment.Location = equipment.Location.Trim();
        equipment.IsSynced = false;

        _db.Equipment.Add(equipment);
        await _db.SaveChangesAsync(cancellationToken);
        return string.Empty;
    }

    private static TechnicalEquipmentItem MapEquipment(Equipment e, IEnumerable<MaintenanceRecord> records)
    {
        var list = records.OrderByDescending(m => m.ScheduledDate).ToList();
        var yearCost = list.Where(m => m.CompletedDate?.Year == DateTime.Today.Year).Sum(m => m.Cost);
        var lastCost = list.FirstOrDefault()?.Cost ?? 0;
        var (label, bg, fg) = StatusStyle(e.Status);

        return new TechnicalEquipmentItem
        {
            Id = e.Id,
            Code = e.Code,
            Name = e.Name,
            Category = e.Category,
            Location = string.IsNullOrWhiteSpace(e.Location) ? "—" : e.Location,
            StatusLabel = label,
            StatusBadgeBackground = bg,
            StatusBadgeForeground = fg,
            LastMaintenanceDisplay = e.LastMaintenanceDate?.ToString("dd/MM/yyyy") ?? "—",
            NextMaintenanceDisplay = e.NextMaintenanceDate?.ToString("dd/MM/yyyy") ?? "—",
            MaintenanceCostDisplay = lastCost > 0 ? Fc(lastCost) : "—",
            Brand = string.IsNullOrWhiteSpace(e.Brand) ? "—" : e.Brand,
            Model = string.IsNullOrWhiteSpace(e.Model) ? "—" : e.Model,
            SerialNumber = string.IsNullOrWhiteSpace(e.SerialNumber) ? "—" : e.SerialNumber,
            InstallationDisplay = e.InstallationDate?.ToString("dd/MM/yyyy") ?? "—",
            PurchaseValueDisplay = e.PurchaseValue > 0 ? Fc(e.PurchaseValue) : "—",
            WarrantyDisplay = e.WarrantyUntil?.ToString("dd/MM/yyyy") ?? "—",
            PowerSpec = string.IsNullOrWhiteSpace(e.PowerSpec) ? "—" : e.PowerSpec,
            VoltageSpec = string.IsNullOrWhiteSpace(e.VoltageSpec) ? "—" : e.VoltageSpec,
            FrequencySpec = string.IsNullOrWhiteSpace(e.FrequencySpec) ? "—" : e.FrequencySpec,
            FuelType = string.IsNullOrWhiteSpace(e.FuelType) ? "—" : e.FuelType,
            OperatingHours = string.IsNullOrWhiteSpace(e.OperatingHours) ? "—" : e.OperatingHours,
            YearMaintenanceCostDisplay = yearCost > 0 ? Fc(yearCost) : "—",
            Interventions = list.Take(8).Select(m => new TechnicalInterventionItem
            {
                DateDisplay = (m.CompletedDate ?? m.ScheduledDate).ToString("dd/MM/yyyy"),
                Description = m.Description,
                CostDisplay = Fc(m.Cost),
                Technician = m.Technician ?? "—",
                StatusLabel = m.CompletedDate.HasValue ? "Terminée" : "Planifiée"
            }).ToList()
        };
    }

    private static (string Label, string Bg, string Fg) StatusStyle(EquipmentStatus status) => status switch
    {
        EquipmentStatus.Operationnel => ("Opérationnel", "#DCFCE7", "#166534"),
        EquipmentStatus.Maintenance => ("En maintenance", "#FFEDD5", "#EA580C"),
        EquipmentStatus.EnPanne => ("En panne", "#FEE2E2", "#DC2626"),
        EquipmentStatus.HorsService => ("Hors service", "#F1F5F9", "#64748B"),
        _ => ("—", "#F1F5F9", "#64748B")
    };

    private static string Fc(decimal amount) => MoneyFormatter.Format(amount);
}

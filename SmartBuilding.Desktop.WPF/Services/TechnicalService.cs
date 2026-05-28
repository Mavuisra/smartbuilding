using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Finance;
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

    public async Task<string> UpdateEquipmentAsync(Equipment equipment, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Equipment.FirstOrDefaultAsync(e => e.Id == equipment.Id, cancellationToken);
        if (entity is null)
            return "Équipement introuvable.";

        if (string.IsNullOrWhiteSpace(equipment.Name))
            return "Le nom est obligatoire.";

        entity.Name = equipment.Name.Trim();
        entity.Category = string.IsNullOrWhiteSpace(equipment.Category) ? "Autre" : equipment.Category.Trim();
        entity.Location = equipment.Location.Trim();
        entity.Brand = equipment.Brand.Trim();
        entity.Model = equipment.Model.Trim();
        entity.SerialNumber = equipment.SerialNumber.Trim();
        entity.PowerSpec = equipment.PowerSpec.Trim();
        entity.VoltageSpec = equipment.VoltageSpec.Trim();
        entity.FrequencySpec = equipment.FrequencySpec.Trim();
        entity.FuelType = equipment.FuelType.Trim();
        entity.OperatingHours = equipment.OperatingHours.Trim();
        entity.MarkUpdated();
        await _db.SaveChangesAsync(cancellationToken);
        return string.Empty;
    }

    public async Task<string> ScheduleMaintenanceAsync(
        Guid equipmentId,
        DateTime scheduledDate,
        string maintenanceType,
        string description,
        string technician,
        CancellationToken cancellationToken = default)
    {
        var equipment = await _db.Equipment.FirstOrDefaultAsync(e => e.Id == equipmentId, cancellationToken);
        if (equipment is null)
            return "Équipement introuvable.";

        if (scheduledDate.Date < DateTime.Today)
            return "La date planifiée ne peut pas être dans le passé.";

        var label = string.IsNullOrWhiteSpace(maintenanceType) ? "Maintenance préventive" : maintenanceType.Trim();
        var desc = string.IsNullOrWhiteSpace(description)
            ? label
            : $"{label} — {description.Trim()}";

        var record = new MaintenanceRecord
        {
            EquipmentId = equipmentId,
            ScheduledDate = scheduledDate.Date,
            Description = desc,
            Technician = string.IsNullOrWhiteSpace(technician) ? "—" : technician.Trim(),
            Cost = 0,
            IsSynced = false
        };

        _db.MaintenanceRecords.Add(record);
        equipment.NextMaintenanceDate = scheduledDate.Date;
        if (equipment.Status == EquipmentStatus.Operationnel && scheduledDate.Date <= DateTime.Today.AddDays(2))
            equipment.Status = EquipmentStatus.Maintenance;
        equipment.MarkUpdated();

        await _db.SaveChangesAsync(cancellationToken);
        return string.Empty;
    }

    public async Task<IReadOnlyList<TechnicalInterventionHistoryRow>> GetInterventionsHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.MaintenanceRecords
            .Include(m => m.Equipment)
            .OrderByDescending(m => m.ScheduledDate)
            .Take(500)
            .ToListAsync(cancellationToken);

        return rows.Select(m =>
        {
            var planned = !m.CompletedDate.HasValue;
            var (bg, fg) = planned ? ("#FFEDD5", "#EA580C") : ("#DCFCE7", "#166534");
            return new TechnicalInterventionHistoryRow
            {
                MaintenanceId = m.Id,
                EquipmentId = m.EquipmentId,
                EquipmentCode = m.Equipment?.Code ?? "—",
                EquipmentName = m.Equipment?.Name ?? "—",
                DateDisplay = (m.CompletedDate ?? m.ScheduledDate).ToString("dd/MM/yyyy"),
                Description = m.Description,
                Technician = m.Technician ?? "—",
                CostDisplay = m.Cost > 0 ? Fc(m.Cost) : planned ? "À définir" : "—",
                StatusLabel = planned ? "Planifiée" : "Terminée",
                StatusBadgeBackground = bg,
                StatusBadgeForeground = fg,
                IsPlanned = planned
            };
        }).ToList();
    }

    public async Task<string> CompleteMaintenanceAsync(
        Guid maintenanceId,
        decimal actualCost,
        string recordedBy,
        CancellationToken cancellationToken = default)
    {
        var record = await _db.MaintenanceRecords
            .Include(m => m.Equipment)
            .FirstOrDefaultAsync(m => m.Id == maintenanceId, cancellationToken);
        if (record is null)
            return "Intervention introuvable.";
        if (record.CompletedDate.HasValue)
            return "Cette intervention est déjà clôturée.";

        if (actualCost < 0)
            return "Le coût ne peut pas être négatif.";

        if (actualCost > 0)
        {
            try
            {
                await _financeLedger.RecordExpenseAsync(
                    actualCost,
                    FinanceConstants.CategoryMaintenance,
                    $"Maintenance {record.Equipment?.Code} — {record.Description}",
                    FinanceConstants.SourceTechnique,
                    string.IsNullOrWhiteSpace(recordedBy) ? FinanceConstants.RecordedByTechnique : recordedBy,
                    record.Id,
                    cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message;
            }
        }

        var today = DateTime.Today;
        record.CompletedDate = today;
        record.Cost = actualCost;
        record.MarkUpdated();

        if (record.Equipment is not null)
        {
            record.Equipment.LastMaintenanceDate = today;
            record.Equipment.NextMaintenanceDate = today.AddMonths(3);
            if (record.Equipment.Status == EquipmentStatus.Maintenance)
                record.Equipment.Status = EquipmentStatus.Operationnel;
            record.Equipment.MarkUpdated();
        }

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

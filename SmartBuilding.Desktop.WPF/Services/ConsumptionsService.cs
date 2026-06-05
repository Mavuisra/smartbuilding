using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Domain.Entities.Consumption;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Domain.Entities.Finance;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Services;

namespace SmartBuilding.Desktop.WPF.Services;

public class ConsumptionsService
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    private readonly SmartBuildingDbContext _db;
    private readonly FinanceLedgerService _financeLedger;

    public ConsumptionsService(SmartBuildingDbContext db, FinanceLedgerService financeLedger)
    {
        _db = db;
        _financeLedger = financeLedger;
    }

    public async Task<ConsumptionPageData> LoadAsync(CancellationToken cancellationToken = default)
    {
        var cash = await TreasuryLoader.LoadAsync(_financeLedger, cancellationToken);
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var prevMonthStart = monthStart.AddMonths(-1);

        var records = await _db.ConsumptionRecords
            .OrderByDescending(r => r.PeriodEnd)
            .ToListAsync(cancellationToken);

        var monthRecords = records.Where(r => r.PeriodEnd >= monthStart).ToList();
        var prevMonthRecords = records.Where(r => r.PeriodEnd >= prevMonthStart && r.PeriodEnd < monthStart).ToList();

        var electricityCost = monthRecords.Where(r => r.Type == ConsumptionType.Electricite).Sum(r => r.Cost);
        var waterCost = monthRecords.Where(r => r.Type == ConsumptionType.Eau).Sum(r => r.Cost);
        var fuelCost = monthRecords.Where(r => r.Type is ConsumptionType.Carburant or ConsumptionType.GroupeElectrogene).Sum(r => r.Cost);
        var internetCost = monthRecords.Where(r => r.Type == ConsumptionType.Internet).Sum(r => r.Cost);
        var totalEnergy = monthRecords.Sum(r => r.Cost);
        var prevTotal = prevMonthRecords.Sum(r => r.Cost);
        var variation = prevTotal > 0 ? (totalEnergy - prevTotal) / prevTotal * 100 : 0;

        var monthlyTrend = new List<ConsumptionMonthPoint>();
        for (var i = 11; i >= 0; i--)
        {
            var m = monthStart.AddMonths(-i);
            var end = m.AddMonths(1);
            var slice = records.Where(r => r.PeriodEnd >= m && r.PeriodEnd < end);
            monthlyTrend.Add(new ConsumptionMonthPoint
            {
                Label = m.ToString("MMM", Fr),
                TotalCost = slice.Sum(r => r.Cost),
                TotalQuantity = 0
            });
        }

        var distribution = monthRecords
            .GroupBy(DisplayTypeLabel)
            .OrderByDescending(g => g.Sum(x => x.Cost))
            .Select(g => new ConsumptionTypeSlice { Type = g.Key, Cost = g.Sum(x => x.Cost) })
            .ToList();

        var costByType = distribution
            .Select(d => new ConsumptionCostBar { Type = d.Type, Cost = d.Cost })
            .ToList();

        var compare = new List<ConsumptionComparePoint>();
        for (var i = 5; i >= 0; i--)
        {
            var m = monthStart.AddMonths(-i);
            var end = m.AddMonths(1);
            var prev = m.AddMonths(-1);
            compare.Add(new ConsumptionComparePoint
            {
                Label = m.ToString("MMM", Fr),
                CurrentCost = records.Where(r => r.PeriodEnd >= m && r.PeriodEnd < end).Sum(r => r.Cost),
                PreviousCost = records.Where(r => r.PeriodEnd >= prev && r.PeriodEnd < m).Sum(r => r.Cost)
            });
        }

        var topConsumer = monthRecords
            .GroupBy(r => string.IsNullOrWhiteSpace(r.EquipmentSource) ? TypeLabel(r.Type) : r.EquipmentSource)
            .OrderByDescending(g => g.Sum(x => x.Cost))
            .Select(g => $"{g.Key} ({Fc(g.Sum(x => x.Cost))})")
            .FirstOrDefault() ?? "—";

        var avgMonthly = records.Count == 0 ? 0 : records.GroupBy(r => new { r.PeriodEnd.Year, r.PeriodEnd.Month })
            .Average(g => g.Sum(x => x.Cost));

        var savings = monthRecords.Where(r => r.Status == "Économie").Sum(r => r.Cost);
        var futureEst = totalEnergy * (1 + Math.Max(variation, 0) / 100m);

        var items = records.Select(r => MapItem(r, records)).ToList();
        var alerts = BuildAlerts(monthRecords, records, variation, fuelCost);

        return new ConsumptionPageData
        {
            RentCollectedTotal = cash.RentCollectedTotal,
            AvailableBalance = cash.AvailableBalance,
            TotalExpenses = cash.TotalExpenses,
            ElectricityDisplay = Fc(electricityCost),
            WaterBillDisplay = Fc(waterCost),
            FuelCostDisplay = Fc(fuelCost),
            InternetCostDisplay = Fc(internetCost),
            TotalEnergyCostDisplay = Fc(totalEnergy),
            TotalEnergyCost = totalEnergy,
            MonthlyVariationDisplay = $"{variation:+0.0;-0.0}%",
            MonthlyVariationTrend = variation > 5 ? "Hausse" : variation < -3 ? "Baisse" : "Stable",
            MonthlyVariationPercent = variation,
            TopConsumer = topConsumer,
            AverageMonthlyCostDisplay = Fc(avgMonthly),
            ConsumptionTrendLabel = variation > 8 ? "Tendance à la hausse" : variation < -5 ? "Tendance à la baisse" : "Consommation stable",
            FutureEstimateDisplay = Fc(futureEst),
            SavingsDisplay = savings > 0 ? Fc(savings) : "—",
            Records = items,
            Alerts = alerts,
            MonthlyTrend = monthlyTrend,
            EnergyDistribution = distribution,
            CostByType = costByType,
            MonthComparison = compare
        };
    }

    public async Task<string> CreateRecordAsync(ConsumptionRecord record, CancellationToken cancellationToken = default)
    {
        if (record.Cost <= 0)
            return "Le montant doit être supérieur à zéro.";

        record.Building = string.IsNullOrWhiteSpace(record.Building) ? DefaultBuildingName() : record.Building.Trim();
        record.EquipmentSource = record.EquipmentSource?.Trim() ?? DisplayTypeLabel(record);
        record.PaidBy = record.PaidBy?.Trim() ?? string.Empty;
        record.Responsible = string.IsNullOrWhiteSpace(record.Responsible) ? record.PaidBy : record.Responsible.Trim();
        record.ExpenseMotif = string.IsNullOrWhiteSpace(record.ExpenseMotif) ? null : record.ExpenseMotif.Trim();
        record.ReimbursementStatus = string.IsNullOrWhiteSpace(record.ReimbursementStatus)
            ? ConsumptionReimbursementStatus.NotApplicable
            : record.ReimbursementStatus.Trim();
        record.Status = string.IsNullOrWhiteSpace(record.Status) ? "Normal" : record.Status;
        record.Unit = "USD";
        record.Currency = "USD";
        record.Quantity = record.Cost;
        record.PeriodType = string.IsNullOrWhiteSpace(record.PeriodType) ? "Mensuel" : record.PeriodType;
        if (record.PeriodEnd == default)
            record.PeriodEnd = DateTime.Today;
        if (record.PeriodStart == default)
            record.PeriodStart = record.PeriodEnd.AddDays(-30);
        record.IsSynced = false;

        _db.ConsumptionRecords.Add(record);
        await _db.SaveChangesAsync(cancellationToken);

        if (record.Cost > 0)
        {
            try
            {
                await _financeLedger.RecordExpenseAsync(
                    record.Cost,
                    FinanceConstants.CategoryEnergy,
                    BuildFinanceDescription(record),
                    FinanceConstants.SourceConsumptions,
                    FinanceConstants.RecordedByConsumptions,
                    record.Id,
                    cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                _db.ConsumptionRecords.Remove(record);
                await _db.SaveChangesAsync(cancellationToken);
                return ex.Message;
            }
        }

        return string.Empty;
    }

    public async Task<string> UpdateRecordAsync(
        Guid recordId,
        ConsumptionRecord updates,
        CancellationToken cancellationToken = default)
    {
        var record = await _db.ConsumptionRecords.FirstOrDefaultAsync(r => r.Id == recordId, cancellationToken);
        if (record is null)
            return "Consommation introuvable.";

        if (updates.Cost <= 0)
            return "Le montant doit être supérieur à zéro.";

        var oldCost = record.Cost;
        record.Type = updates.Type;
        record.CustomTypeLabel = updates.CustomTypeLabel;
        record.EquipmentSource = updates.EquipmentSource?.Trim() ?? DisplayTypeLabel(record);
        record.ExpenseMotif = string.IsNullOrWhiteSpace(updates.ExpenseMotif) ? null : updates.ExpenseMotif.Trim();
        record.PaidBy = updates.PaidBy?.Trim() ?? string.Empty;
        record.Responsible = string.IsNullOrWhiteSpace(updates.Responsible) ? record.PaidBy : updates.Responsible.Trim();
        record.ReimbursementStatus = string.IsNullOrWhiteSpace(updates.ReimbursementStatus)
            ? ConsumptionReimbursementStatus.NotApplicable
            : updates.ReimbursementStatus.Trim();
        record.Cost = updates.Cost;
        record.Quantity = updates.Cost;
        record.PeriodType = string.IsNullOrWhiteSpace(updates.PeriodType) ? "Mensuel" : updates.PeriodType;
        record.Status = string.IsNullOrWhiteSpace(updates.Status) ? "Normal" : updates.Status;
        record.Notes = string.IsNullOrWhiteSpace(updates.Notes) ? null : updates.Notes.Trim();
        record.Building = string.IsNullOrWhiteSpace(updates.Building) ? DefaultBuildingName() : updates.Building.Trim();
        record.MarkUpdated();

        if (record.Cost != oldCost)
        {
            var cashError = await _financeLedger.ValidateExpenseAsync(record.Cost - oldCost, cancellationToken);
            if (cashError is not null && record.Cost > oldCost)
                return cashError;

            var tx = await _db.FinancialTransactions.FirstOrDefaultAsync(
                t => t.RelatedEntityId == recordId
                     && t.Category == FinanceConstants.CategoryEnergy
                     && t.Type == TransactionType.Depense,
                cancellationToken);
            if (tx is not null)
            {
                tx.Amount = record.Cost;
                tx.Description = BuildFinanceDescription(record);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return string.Empty;
    }

    public async Task<string> MarkReimbursedAsync(Guid recordId, CancellationToken cancellationToken = default)
    {
        var record = await _db.ConsumptionRecords.FirstOrDefaultAsync(r => r.Id == recordId, cancellationToken);
        if (record is null)
            return "Consommation introuvable.";
        if (!string.Equals(record.ReimbursementStatus, ConsumptionReimbursementStatus.Pending, StringComparison.OrdinalIgnoreCase))
            return "Cette dépense n'est pas en attente de remboursement.";

        record.ReimbursementStatus = ConsumptionReimbursementStatus.Reimbursed;
        record.MarkUpdated();
        await _db.SaveChangesAsync(cancellationToken);
        return string.Empty;
    }

    private static string BuildFinanceDescription(ConsumptionRecord record)
    {
        var motif = string.IsNullOrWhiteSpace(record.ExpenseMotif) ? record.EquipmentSource : record.ExpenseMotif.Trim();
        var paidBy = string.IsNullOrWhiteSpace(record.PaidBy) ? string.Empty : $" — payé par {record.PaidBy}";
        var reimbursement = string.Equals(record.ReimbursementStatus, ConsumptionReimbursementStatus.Pending, StringComparison.OrdinalIgnoreCase)
            ? " [à rembourser]"
            : string.Empty;
        return $"Consommation {DisplayTypeLabel(record)} — {motif}{paidBy}{reimbursement}";
    }

    private static ConsumptionListItem MapItem(ConsumptionRecord r, List<ConsumptionRecord> all)
    {
        var typeLabel = DisplayTypeLabel(r);
        var history = all
            .Where(x => x.Type == r.Type &&
                        (x.EquipmentSource == r.EquipmentSource || string.IsNullOrWhiteSpace(r.EquipmentSource)))
            .GroupBy(x => new { x.PeriodEnd.Year, x.PeriodEnd.Month })
            .OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.Month)
            .Take(6)
            .Select(g =>
            {
                var slice = g.ToList();
                return new ConsumptionHistoryPoint
                {
                    Label = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy", Fr),
                    CostDisplay = Fc(slice.Sum(x => x.Cost)),
                    QuantityDisplay = Fc(slice.Sum(x => x.Cost))
                };
            })
            .ToList();

        var (bg, fg) = StatusStyle(r.Status);
        var (reimbBg, reimbFg, reimbDisplay) = ReimbursementStyle(r);
        var varColor = r.VariationPercent > 15 ? "#DC2626" : r.VariationPercent > 5 ? "#EA580C" : r.VariationPercent < -5 ? "#2563EB" : "#64748B";
        var paidBy = string.IsNullOrWhiteSpace(r.PaidBy)
            ? (string.IsNullOrWhiteSpace(r.Responsible) ? "—" : r.Responsible)
            : r.PaidBy;

        return new ConsumptionListItem
        {
            Id = r.Id,
            DateDisplay = r.PeriodEnd.ToString("dd/MM/yyyy"),
            TypeLabel = typeLabel,
            TypeIconColor = TypeColor(r.Type),
            EquipmentSource = string.IsNullOrWhiteSpace(r.EquipmentSource) ? typeLabel : r.EquipmentSource,
            QuantityDisplay = Fc(r.Cost),
            Unit = "USD",
            CostDisplay = Fc(r.Cost),
            VariationDisplay = $"{r.VariationPercent:+0.0;-0.0}%",
            VariationColor = varColor,
            Responsible = paidBy,
            ExpenseMotif = string.IsNullOrWhiteSpace(r.ExpenseMotif) ? "—" : r.ExpenseMotif,
            PaidBy = paidBy,
            ReimbursementStatus = r.ReimbursementStatus,
            ReimbursementDisplay = reimbDisplay,
            ReimbursementBadgeBackground = reimbBg,
            ReimbursementBadgeForeground = reimbFg,
            CanMarkReimbursed = string.Equals(r.ReimbursementStatus, ConsumptionReimbursementStatus.Pending, StringComparison.OrdinalIgnoreCase),
            HasReimbursementInfo = !string.Equals(r.ReimbursementStatus, ConsumptionReimbursementStatus.NotApplicable, StringComparison.OrdinalIgnoreCase),
            StatusLabel = r.Status,
            StatusBadgeBackground = bg,
            StatusBadgeForeground = fg,
            Building = string.IsNullOrWhiteSpace(r.Building) ? "—" : r.Building,
            PeriodType = r.PeriodType,
            MeterReference = r.MeterReference ?? "—",
            Notes = r.Notes ?? "—",
            Currency = r.Currency,
            Cost = r.Cost,
            Quantity = r.Quantity,
            VariationPercent = r.VariationPercent,
            IsAnomaly = r.IsAnomaly,
            MonthlyHistory = history
        };
    }

    private static List<ConsumptionAlertItem> BuildAlerts(
        List<ConsumptionRecord> monthRecords, List<ConsumptionRecord> all, decimal variation, decimal fuelCost)
    {
        var alerts = new List<ConsumptionAlertItem>();

        foreach (var r in monthRecords.Where(x => x.IsAnomaly || x.Status is "Critique" or "Élevé").Take(3))
        {
            alerts.Add(new ConsumptionAlertItem
            {
                Title = r.Status == "Critique" ? "Anomalie énergétique" : "Surconsommation détectée",
                Message = $"{DisplayTypeLabel(r)} — {r.EquipmentSource} ({r.VariationPercent:+0.0}% vs N-1)",
                AccentColor = r.Status == "Critique" ? "#DC2626" : "#EA580C",
                Background = r.Status == "Critique" ? "#FEE2E2" : "#FFEDD5"
            });
        }

        if (variation > 12)
        {
            alerts.Add(new ConsumptionAlertItem
            {
                Title = "Hausse facture globale",
                Message = $"Coût énergétique +{variation:F1}% par rapport au mois précédent",
                AccentColor = "#EA580C",
                Background = "#FFEDD5"
            });
        }

        if (fuelCost > 0 && monthRecords.Any(r => r.Type == ConsumptionType.Carburant && r.Cost < 200))
        {
            alerts.Add(new ConsumptionAlertItem
            {
                Title = "Carburant faible",
                Message = "Budget carburant générateur sous le seuil recommandé",
                AccentColor = "#DC2626",
                Background = "#FEE2E2"
            });
        }

        foreach (var r in monthRecords.Where(x => x.VariationPercent > 25 && !x.IsAnomaly).Take(2))
        {
            alerts.Add(new ConsumptionAlertItem
            {
                Title = "Consommation inhabituelle",
                Message = $"{r.EquipmentSource} — pic de {r.VariationPercent:F0}%",
                AccentColor = "#B45309",
                Background = "#FEF3C7"
            });
        }

        if (alerts.Count == 0)
        {
            alerts.Add(new ConsumptionAlertItem
            {
                Title = "Consommations sous contrôle",
                Message = "Aucune anomalie énergétique majeure ce mois",
                AccentColor = "#2D6A4F",
                Background = "#E8F5EE"
            });
        }

        return alerts.Take(6).ToList();
    }

    public static string DisplayTypeLabel(ConsumptionRecord record) =>
        !string.IsNullOrWhiteSpace(record.CustomTypeLabel)
            ? record.CustomTypeLabel.Trim()
            : TypeLabel(record.Type);

    public static bool IsKnownType(string label) => label.Trim() switch
    {
        "Électricité" or "Eau" or "Carburant générateur" or "Internet" or "Climatisation"
            or "Éclairage" or "Groupe électrogène" or "Réseau technique" or "Énergie" => true,
        _ => false
    };

    public static string TypeLabel(ConsumptionType type) => type switch
    {
        ConsumptionType.Eau => "Eau",
        ConsumptionType.Electricite => "Électricité",
        ConsumptionType.Carburant => "Carburant générateur",
        ConsumptionType.Internet => "Internet",
        ConsumptionType.Climatisation => "Climatisation",
        ConsumptionType.Eclairage => "Éclairage",
        ConsumptionType.GroupeElectrogene => "Groupe électrogène",
        ConsumptionType.ReseauTechnique => "Réseau technique",
        ConsumptionType.Energie => "Énergie",
        _ => type.ToString()
    };

    public static ConsumptionType ParseType(string label) => label switch
    {
        "Eau" => ConsumptionType.Eau,
        "Carburant générateur" => ConsumptionType.Carburant,
        "Internet" => ConsumptionType.Internet,
        "Climatisation" => ConsumptionType.Climatisation,
        "Éclairage" => ConsumptionType.Eclairage,
        "Groupe électrogène" => ConsumptionType.GroupeElectrogene,
        "Réseau technique" => ConsumptionType.ReseauTechnique,
        "Énergie" => ConsumptionType.Energie,
        _ => ConsumptionType.Electricite
    };

    private static string TypeColor(ConsumptionType type) => type switch
    {
        ConsumptionType.Eau => "#0EA5E9",
        ConsumptionType.Electricite => "#2563EB",
        ConsumptionType.Carburant or ConsumptionType.GroupeElectrogene => "#EA580C",
        ConsumptionType.Internet or ConsumptionType.ReseauTechnique => "#6D28D9",
        ConsumptionType.Climatisation => "#0369A1",
        ConsumptionType.Eclairage => "#B45309",
        _ => "#2D6A4F"
    };

    private static (string Bg, string Fg, string Display) ReimbursementStyle(ConsumptionRecord record)
    {
        if (string.Equals(record.ReimbursementStatus, ConsumptionReimbursementStatus.Pending, StringComparison.OrdinalIgnoreCase))
            return ("#FFEDD5", "#EA580C", $"À rembourser — {record.PaidBy}");
        if (string.Equals(record.ReimbursementStatus, ConsumptionReimbursementStatus.Reimbursed, StringComparison.OrdinalIgnoreCase))
            return ("#DCFCE7", "#166534", $"Remboursé — {record.PaidBy}");
        return ("#F1F5F9", "#64748B", "—");
    }

    private static (string Bg, string Fg) StatusStyle(string status) => status switch
    {
        "Élevé" => ("#FFEDD5", "#EA580C"),
        "Critique" => ("#FEE2E2", "#DC2626"),
        "Économie" => ("#DBEAFE", "#1D4ED8"),
        _ => ("#DCFCE7", "#166534")
    };

    private static string DefaultBuildingName() =>
        AppConfigurationService.Instance?.Current.CompanyName ?? BuildingInfoDefaults.CompanyName;

    private static string Fc(decimal amount) => MoneyFormatter.Format(amount);
}

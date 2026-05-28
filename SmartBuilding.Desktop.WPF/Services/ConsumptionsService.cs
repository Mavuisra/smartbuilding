using System.Globalization;
using Microsoft.EntityFrameworkCore;
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

        var electricityQty = monthRecords.Where(r => r.Type == ConsumptionType.Electricite).Sum(r => r.Quantity);
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
                TotalQuantity = slice.Where(r => r.Type == ConsumptionType.Electricite).Sum(r => r.Quantity)
            });
        }

        var distribution = monthRecords
            .GroupBy(r => TypeLabel(r.Type))
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
            ElectricityDisplay = $"{electricityQty:N0} kWh",
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
        if (record.Quantity <= 0)
            return "La valeur de consommation doit être positive.";
        if (record.Cost < 0)
            return "Le coût ne peut pas être négatif.";

        record.Building = string.IsNullOrWhiteSpace(record.Building) ? "Tour SBMS" : record.Building.Trim();
        record.EquipmentSource = record.EquipmentSource?.Trim() ?? TypeLabel(record.Type);
        record.Responsible = string.IsNullOrWhiteSpace(record.Responsible) ? "Paul Ngoy" : record.Responsible.Trim();
        record.Status = string.IsNullOrWhiteSpace(record.Status) ? "Normal" : record.Status;
        record.Unit = string.IsNullOrWhiteSpace(record.Unit) ? DefaultUnit(record.Type) : record.Unit;
        record.Currency = string.IsNullOrWhiteSpace(record.Currency) ? MoneyFormatter.CurrencyCode : record.Currency;
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
                    $"Consommation {TypeLabel(record.Type)} — {record.EquipmentSource}",
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

    private static ConsumptionListItem MapItem(ConsumptionRecord r, List<ConsumptionRecord> all)
    {
        var typeLabel = TypeLabel(r.Type);
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
                    QuantityDisplay = $"{slice.Sum(x => x.Quantity):N1} {r.Unit}"
                };
            })
            .ToList();

        var (bg, fg) = StatusStyle(r.Status);
        var varColor = r.VariationPercent > 15 ? "#DC2626" : r.VariationPercent > 5 ? "#EA580C" : r.VariationPercent < -5 ? "#2563EB" : "#64748B";

        return new ConsumptionListItem
        {
            Id = r.Id,
            DateDisplay = r.PeriodEnd.ToString("dd/MM/yyyy"),
            TypeLabel = typeLabel,
            TypeIconColor = TypeColor(r.Type),
            EquipmentSource = string.IsNullOrWhiteSpace(r.EquipmentSource) ? typeLabel : r.EquipmentSource,
            QuantityDisplay = $"{r.Quantity:N2}",
            Unit = r.Unit,
            CostDisplay = Fc(r.Cost),
            VariationDisplay = $"{r.VariationPercent:+0.0;-0.0}%",
            VariationColor = varColor,
            Responsible = string.IsNullOrWhiteSpace(r.Responsible) ? "—" : r.Responsible,
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
                Message = $"{TypeLabel(r.Type)} — {r.EquipmentSource} ({r.VariationPercent:+0.0}% vs N-1)",
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

        if (fuelCost > 0 && monthRecords.Any(r => r.Type == ConsumptionType.Carburant && r.Quantity < 200))
        {
            alerts.Add(new ConsumptionAlertItem
            {
                Title = "Carburant faible",
                Message = "Réserve diesel générateur sous le seuil recommandé",
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

    private static string DefaultUnit(ConsumptionType type) => type switch
    {
        ConsumptionType.Eau => "m³",
        ConsumptionType.Electricite or ConsumptionType.Eclairage or ConsumptionType.Climatisation or ConsumptionType.Energie => "kWh",
        ConsumptionType.Carburant or ConsumptionType.GroupeElectrogene => "L",
        ConsumptionType.Internet or ConsumptionType.ReseauTechnique => "GB",
        _ => "kWh"
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

    private static (string Bg, string Fg) StatusStyle(string status) => status switch
    {
        "Élevé" => ("#FFEDD5", "#EA580C"),
        "Critique" => ("#FEE2E2", "#DC2626"),
        "Économie" => ("#DBEAFE", "#1D4ED8"),
        _ => ("#DCFCE7", "#166534")
    };

    private static string Fc(decimal amount) => MoneyFormatter.Format(amount);
}

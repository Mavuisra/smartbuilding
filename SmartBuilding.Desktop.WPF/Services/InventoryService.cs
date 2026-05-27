using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Inventory;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Services;

namespace SmartBuilding.Desktop.WPF.Services;

public class InventoryService
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    private readonly SmartBuildingDbContext _db;
    private readonly FinanceLedgerService _financeLedger;

    public InventoryService(SmartBuildingDbContext db, FinanceLedgerService financeLedger)
    {
        _db = db;
        _financeLedger = financeLedger;
    }

    public async Task<InventoryPageData> LoadAsync(CancellationToken cancellationToken = default)
    {
        var cash = await TreasuryLoader.LoadAsync(_financeLedger, cancellationToken);
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var items = await _db.InventoryItems
            .Include(i => i.MaintenanceRecords)
            .OrderBy(i => i.Code)
            .ToListAsync(cancellationToken);

        var records = items.SelectMany(i => i.MaintenanceRecords).ToList();
        var monthRecords = records.Where(r =>
            (r.CompletedDate ?? r.ScheduledDate) >= monthStart).ToList();

        var total = items.Count;
        var t = Math.Max(total, 1);
        var operational = items.Count(i => i.Status == "Opérationnel");
        var maintenance = items.Count(i => i.Status == "Maintenance");
        var outOfService = items.Count(i => i.Status == "Hors service");
        var critical = items.Count(i => i.Status == "Critique");
        var totalValue = items.Sum(i => i.EstimatedValue > 0 ? i.EstimatedValue : i.UnitValue * Math.Max(i.Quantity, 1));

        var list = items.Select(i => MapItem(i, today)).ToList();

        var categoryDist = items
            .GroupBy(i => string.IsNullOrWhiteSpace(i.Category) ? "Autre" : i.Category)
            .OrderByDescending(g => g.Count())
            .Select(g => new InventoryCategorySlice { Category = g.Key, Count = g.Count() })
            .ToList();

        var costTrend = new List<InventoryMonthPoint>();
        for (var i = 5; i >= 0; i--)
        {
            var m = monthStart.AddMonths(-i);
            var end = m.AddMonths(1);
            costTrend.Add(new InventoryMonthPoint
            {
                Label = m.ToString("MMM", Fr),
                Cost = records.Where(r => (r.CompletedDate ?? r.ScheduledDate) >= m && (r.CompletedDate ?? r.ScheduledDate) < end)
                    .Sum(r => r.Cost)
            });
        }

        var criticalByStatus = new[]
        {
            new InventoryStatusSlice { Status = "Critique", Count = critical },
            new InventoryStatusSlice { Status = "Hors service", Count = outOfService },
            new InventoryStatusSlice { Status = "Maintenance", Count = maintenance }
        }.Where(s => s.Count > 0).ToList();

        var interventionHist = new List<InventoryInterventionPoint>();
        for (var i = 5; i >= 0; i--)
        {
            var m = monthStart.AddMonths(-i);
            var end = m.AddMonths(1);
            interventionHist.Add(new InventoryInterventionPoint
            {
                Label = m.ToString("MMM", Fr),
                Count = records.Count(r => (r.CompletedDate ?? r.ScheduledDate) >= m && (r.CompletedDate ?? r.ScheduledDate) < end)
            });
        }

        var alerts = BuildAlerts(items, today);

        return new InventoryPageData
        {
            RentCollectedTotal = cash.RentCollectedTotal,
            AvailableBalance = cash.AvailableBalance,
            TotalExpenses = cash.TotalExpenses,
            TotalItems = total,
            OperationalCount = operational,
            MaintenanceCount = maintenance,
            OutOfServiceCount = outOfService,
            CriticalCount = critical,
            OperationalPercent = $"{operational * 100.0 / t:F1}%",
            TotalValue = totalValue,
            InterventionsThisMonth = monthRecords.Count,
            Items = list,
            Alerts = alerts,
            CategoryDistribution = categoryDist,
            MaintenanceCostTrend = costTrend,
            CriticalByStatus = criticalByStatus,
            InterventionHistory = interventionHist
        };
    }

    public async Task<string> CreateItemAsync(InventoryItem item, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(item.Name))
            return "Le nom de l'équipement est obligatoire.";

        if (string.IsNullOrWhiteSpace(item.Code))
            item.Code = $"INV-{DateTime.Today:yyyyMM}-{(await _db.InventoryItems.CountAsync(cancellationToken) + 1):D3}";

        if (await _db.InventoryItems.AnyAsync(i => i.Code == item.Code.Trim(), cancellationToken))
            return "Ce code inventaire existe déjà.";

        item.Code = item.Code.Trim();
        item.Name = item.Name.Trim();
        item.Category = string.IsNullOrWhiteSpace(item.Category) ? "Équipement bâtiment" : item.Category.Trim();
        item.Status = string.IsNullOrWhiteSpace(item.Status) ? "Opérationnel" : item.Status;
        if (item.EstimatedValue <= 0)
            item.EstimatedValue = item.UnitValue * Math.Max(item.Quantity, 1);
        item.IsSynced = false;

        _db.InventoryItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken);
        return string.Empty;
    }

    private static InventoryListItem MapItem(InventoryItem i, DateTime today)
    {
        var records = i.MaintenanceRecords.OrderByDescending(r => r.ScheduledDate).ToList();
        var yearCost = records.Where(r => (r.CompletedDate ?? r.ScheduledDate).Year == today.Year).Sum(r => r.Cost);
        var value = i.EstimatedValue > 0 ? i.EstimatedValue : i.UnitValue * Math.Max(i.Quantity, 1);
        var (label, bg, fg) = StatusStyle(i.Status);
        var palette = CategoryColors(i.Category);

        return new InventoryListItem
        {
            Id = i.Id,
            Code = i.Code,
            Name = i.Name,
            Initials = GetInitials(i.Name),
            LogoBackground = palette.bg,
            LogoForeground = palette.fg,
            Category = i.Category,
            Location = string.IsNullOrWhiteSpace(i.Location) ? "—" : i.Location,
            Building = string.IsNullOrWhiteSpace(i.Building) ? "—" : i.Building,
            StatusLabel = label,
            StatusBadgeBackground = bg,
            StatusBadgeForeground = fg,
            Responsible = string.IsNullOrWhiteSpace(i.Responsible) ? "—" : i.Responsible,
            LastMaintenanceDisplay = i.LastMaintenanceDate?.ToString("dd/MM/yyyy") ?? "—",
            NextMaintenanceDisplay = i.NextMaintenanceDate?.ToString("dd/MM/yyyy") ?? "—",
            EstimatedValueDisplay = Fc(value),
            SerialNumber = string.IsNullOrWhiteSpace(i.SerialNumber) ? "—" : i.SerialNumber,
            Brand = string.IsNullOrWhiteSpace(i.Brand) ? "—" : i.Brand,
            Model = string.IsNullOrWhiteSpace(i.Model) ? "—" : i.Model,
            UsageDuration = string.IsNullOrWhiteSpace(i.UsageDuration) ? "—" : i.UsageDuration,
            Notes = i.Notes ?? "—",
            YearMaintenanceCostDisplay = yearCost > 0 ? Fc(yearCost) : "—",
            Quantity = i.Quantity,
            Maintenances = records.Where(r => r.RecordType == "Maintenance").Take(6).Select(MapRecord).ToList(),
            Interventions = records.Take(8).Select(MapRecord).ToList()
        };
    }

    private static InventoryMaintenanceRow MapRecord(InventoryMaintenanceRecord r) => new()
    {
        DateDisplay = (r.CompletedDate ?? r.ScheduledDate).ToString("dd/MM/yyyy"),
        Description = r.Description,
        CostDisplay = Fc(r.Cost),
        Technician = string.IsNullOrWhiteSpace(r.Technician) ? "—" : r.Technician,
        RecordType = r.RecordType,
        StatusLabel = r.CompletedDate.HasValue ? "Terminée" : "Planifiée"
    };

    private static List<InventoryAlertItem> BuildAlerts(List<InventoryItem> items, DateTime today)
    {
        var alerts = new List<InventoryAlertItem>();

        foreach (var i in items.Where(x => x.NextMaintenanceDate < today && x.Status != "Hors service").Take(3))
        {
            alerts.Add(new InventoryAlertItem
            {
                Title = "Maintenance en retard",
                Message = $"{i.Name} ({i.Code}) — échéance dépassée",
                AccentColor = "#DC2626",
                Background = "#FEE2E2"
            });
        }

        foreach (var i in items.Where(x => x.Status == "Critique").Take(2))
        {
            alerts.Add(new InventoryAlertItem
            {
                Title = "Équipement critique",
                Message = $"{i.Name} — intervention urgente requise",
                AccentColor = "#B45309",
                Background = "#FEF3C7"
            });
        }

        foreach (var i in items.Where(x => x.Status == "Hors service").Take(2))
        {
            alerts.Add(new InventoryAlertItem
            {
                Title = "Panne signalée",
                Message = $"{i.Name} — hors service",
                AccentColor = "#DC2626",
                Background = "#FEE2E2"
            });
        }

        foreach (var i in items.Where(x => x.NextMaintenanceDate >= today && x.NextMaintenanceDate <= today.AddDays(14)).Take(2))
        {
            alerts.Add(new InventoryAlertItem
            {
                Title = "Maintenance programmée",
                Message = $"{i.Name} — {i.NextMaintenanceDate:dd/MM/yyyy}",
                AccentColor = "#2563EB",
                Background = "#DBEAFE"
            });
        }

        if (alerts.Count == 0)
            alerts.Add(new InventoryAlertItem
            {
                Title = "Parc en bon état",
                Message = "Aucune alerte inventaire critique",
                AccentColor = "#2D6A4F",
                Background = "#E8F5EE"
            });

        return alerts.Take(6).ToList();
    }

    private static (string Label, string Bg, string Fg) StatusStyle(string status) => status switch
    {
        "Opérationnel" => ("Opérationnel", "#DCFCE7", "#166534"),
        "Maintenance" => ("Maintenance", "#FFEDD5", "#EA580C"),
        "Hors service" => ("Hors service", "#FEE2E2", "#DC2626"),
        "Critique" => ("Critique", "#FEF3C7", "#B45309"),
        _ => ("Opérationnel", "#DCFCE7", "#166534")
    };

    private static (string bg, string fg) CategoryColors(string category) => category switch
    {
        var c when c.Contains("Sécurité", StringComparison.OrdinalIgnoreCase) || c.Contains("Caméra", StringComparison.OrdinalIgnoreCase) => ("#DBEAFE", "#1D4ED8"),
        var c when c.Contains("Informatique", StringComparison.OrdinalIgnoreCase) || c.Contains("Réseau", StringComparison.OrdinalIgnoreCase) => ("#EDE9FE", "#6D28D9"),
        var c when c.Contains("Climatisation", StringComparison.OrdinalIgnoreCase) => ("#E0F2FE", "#0369A1"),
        var c when c.Contains("Électricité", StringComparison.OrdinalIgnoreCase) || c.Contains("Générateur", StringComparison.OrdinalIgnoreCase) => ("#FFEDD5", "#EA580C"),
        var c when c.Contains("Plomberie", StringComparison.OrdinalIgnoreCase) => ("#CCFBF1", "#0F766E"),
        var c when c.Contains("Mobilier", StringComparison.OrdinalIgnoreCase) => ("#F3E8FF", "#7E22CE"),
        _ => ("#E8F5EE", "#2D6A4F")
    };

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant()
            : name.Length >= 2 ? name[..2].ToUpperInvariant() : "EQ";
    }

    private static string Fc(decimal amount) => MoneyFormatter.Format(amount);
}

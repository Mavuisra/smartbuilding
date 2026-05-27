using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Incidents;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Domain.Entities.Finance;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Services;

namespace SmartBuilding.Desktop.WPF.Services;

public class IncidentsService
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    private readonly SmartBuildingDbContext _db;
    private readonly FinanceLedgerService _financeLedger;

    public IncidentsService(SmartBuildingDbContext db, FinanceLedgerService financeLedger)
    {
        _db = db;
        _financeLedger = financeLedger;
    }

    public async Task<IncidentPageData> LoadAsync(CancellationToken cancellationToken = default)
    {
        var cash = await TreasuryLoader.LoadAsync(_financeLedger, cancellationToken);
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var incidents = await _db.Incidents
            .Include(i => i.Interventions)
            .OrderByDescending(i => i.ReportedAt)
            .ToListAsync(cancellationToken);

        var allInterventions = incidents.SelectMany(i => i.Interventions).ToList();
        var todayInterventions = allInterventions.Count(iv =>
            iv.StartedAt.Date == today || (iv.EndedAt.HasValue && iv.EndedAt.Value.Date == today));

        var open = incidents.Where(i => i.Status is not IncidentStatus.Resolu and not IncidentStatus.Cloture).ToList();
        var critical = incidents.Count(i => i.Severity == IncidentSeverity.Critique);
        var resolved = incidents.Count(i => i.Status is IncidentStatus.Resolu or IncidentStatus.Cloture);
        var monthCost = incidents.Where(i => i.ReportedAt >= monthStart).Sum(i => i.Cost);
        var totalCost = incidents.Sum(i => i.Cost);

        var monthlyTrend = new List<IncidentMonthPoint>();
        for (var i = 11; i >= 0; i--)
        {
            var m = monthStart.AddMonths(-i);
            var end = m.AddMonths(1);
            monthlyTrend.Add(new IncidentMonthPoint
            {
                Label = m.ToString("MMM", Fr),
                Count = incidents.Count(x => x.ReportedAt >= m && x.ReportedAt < end)
            });
        }

        var typeDist = incidents
            .GroupBy(i => string.IsNullOrWhiteSpace(i.IncidentType) ? "Autre" : i.IncidentType)
            .OrderByDescending(g => g.Count())
            .Select(g => new IncidentTypeSlice { Type = g.Key, Count = g.Count() })
            .ToList();

        var severityDist = new[]
        {
            new IncidentSeveritySlice { Severity = "Faible", Count = incidents.Count(i => i.Severity == IncidentSeverity.Faible) },
            new IncidentSeveritySlice { Severity = "Moyen", Count = incidents.Count(i => i.Severity == IncidentSeverity.Moyenne) },
            new IncidentSeveritySlice { Severity = "Élevé", Count = incidents.Count(i => i.Severity == IncidentSeverity.Elevee) },
            new IncidentSeveritySlice { Severity = "Critique", Count = critical }
        }.Where(s => s.Count > 0).ToList();

        var resolutionTrend = new List<IncidentResolutionPoint>();
        for (var i = 5; i >= 0; i--)
        {
            var m = monthStart.AddMonths(-i);
            var end = m.AddMonths(1);
            var resolvedInMonth = incidents.Where(x =>
                x.ResolvedAt >= m && x.ResolvedAt < end && x.ReportedAt < x.ResolvedAt).ToList();
            var avgHours = resolvedInMonth.Count == 0
                ? 0
                : resolvedInMonth.Average(x => (x.ResolvedAt!.Value - x.ReportedAt).TotalHours);
            resolutionTrend.Add(new IncidentResolutionPoint
            {
                Label = m.ToString("MMM", Fr),
                AverageHours = Math.Round(avgHours, 1)
            });
        }

        var riskiestZone = incidents
            .GroupBy(i => i.Location)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key} ({g.Count()} incidents)")
            .FirstOrDefault() ?? "—";

        var problematic = incidents
            .GroupBy(i => i.IncidentType)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key} ({g.Count()}×)")
            .FirstOrDefault() ?? "—";

        var recurring = incidents
            .GroupBy(i => i.IncidentType)
            .Where(g => g.Count() >= 2)
            .Select(g => g.Key)
            .Take(3)
            .ToList();

        var items = incidents.Select(MapIncident).ToList();
        var interventionItems = allInterventions
            .OrderByDescending(iv => iv.StartedAt)
            .Select(iv => MapIntervention(iv, incidents))
            .Take(20)
            .ToList();

        var alerts = BuildAlerts(incidents, open);
        var monitoring = BuildMonitoring(incidents);
        var securityStatus = critical > 0 || open.Any(i => i.Severity == IncidentSeverity.Critique)
            ? ("Alerte sécurité", "#DC2626")
            : open.Count > 3 ? ("Surveillance renforcée", "#EA580C") : ("Sécurité normale", "#166534");

        var insights = new List<IncidentInsightLine>
        {
            new() { Label = "Zone la plus risquée", Value = riskiestZone, Accent = "#DC2626" },
            new() { Label = "Équipement problématique", Value = problematic, Accent = "#EA580C" },
            new() { Label = "Coût incidents (mois)", Value = Fc(monthCost), Accent = "#6D28D9" },
            new() { Label = "Évolution sécurité", Value = critical > 0 ? "Attention requise" : "Stable", Accent = "#2563EB" },
            new() { Label = "Incidents récurrents", Value = recurring.Count > 0 ? string.Join(", ", recurring) : "Aucun", Accent = "#1B3D3B" }
        };

        return new IncidentPageData
        {
            RentCollectedTotal = cash.RentCollectedTotal,
            AvailableBalance = cash.AvailableBalance,
            TotalExpenses = cash.TotalExpenses,
            TotalIncidents = incidents.Count,
            OpenIncidentsCount = open.Count,
            CriticalCount = critical,
            ResolvedCount = resolved,
            ActiveSecurityAlerts = alerts.Count(a => a.Title != "Sécurité sous contrôle"),
            TotalIncidentCost = totalCost,
            TotalCostDisplay = Fc(totalCost),
            InterventionsToday = todayInterventions,
            SecurityStatusLabel = securityStatus.Item1,
            SecurityStatusColor = securityStatus.Item2,
            RiskiestZone = riskiestZone,
            ProblematicEquipment = problematic,
            MonthlyCostDisplay = Fc(monthCost),
            SecurityTrendLabel = open.Count > 5 ? "Hausse des signalements" : "Tendance maîtrisée",
            RecurringIncidents = recurring.Count > 0 ? string.Join(", ", recurring) : "—",
            Incidents = items,
            Interventions = interventionItems,
            Alerts = alerts,
            Monitoring = monitoring,
            Insights = insights,
            MonthlyTrend = monthlyTrend,
            TypeDistribution = typeDist,
            SeverityDistribution = severityDist,
            ResolutionTrend = resolutionTrend
        };
    }

    public async Task<string> CreateIncidentAsync(Incident incident, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(incident.Title))
            return "Le titre de l'incident est obligatoire.";

        if (string.IsNullOrWhiteSpace(incident.Code))
            incident.Code = $"INC-{DateTime.Today:yyyyMM}-{(await _db.Incidents.CountAsync(cancellationToken) + 1):D3}";

        incident.Building = string.IsNullOrWhiteSpace(incident.Building) ? "Tour SBMS" : incident.Building;
        incident.Responsible = string.IsNullOrWhiteSpace(incident.Responsible) ? "Paul Ngoy" : incident.Responsible;
        incident.ReportedAt = incident.ReportedAt == default ? DateTime.Now : incident.ReportedAt;
        incident.RiskLevel = SeverityToRisk(incident.Severity);
        incident.IsSynced = false;

        _db.Incidents.Add(incident);
        await _db.SaveChangesAsync(cancellationToken);

        if (incident.Cost > 0)
        {
            try
            {
                await _financeLedger.RecordExpenseAsync(
                    incident.Cost,
                    FinanceConstants.CategoryIncident,
                    $"Incident {incident.Code} — {incident.Title}",
                    FinanceConstants.SourceIncidents,
                    FinanceConstants.RecordedByIncidents,
                    incident.Id,
                    cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                _db.Incidents.Remove(incident);
                await _db.SaveChangesAsync(cancellationToken);
                return ex.Message;
            }
        }

        return string.Empty;
    }

    private static IncidentListItem MapIncident(Incident i)
    {
        var (sBg, sFg) = SeverityStyle(i.Severity);
        var (stBg, stFg) = StatusStyle(i.Status);
        var interventions = i.Interventions.OrderByDescending(iv => iv.StartedAt).Select(MapInterventionRow).ToList();
        var duration = i.ResolvedAt.HasValue
            ? $"{(i.ResolvedAt.Value - i.ReportedAt).TotalHours:F0} h"
            : "—";

        return new IncidentListItem
        {
            Id = i.Id,
            Code = string.IsNullOrWhiteSpace(i.Code) ? $"INC-{i.Id.ToString()[..6]}" : i.Code,
            DateDisplay = i.ReportedAt.ToString("dd/MM/yyyy HH:mm"),
            TypeLabel = string.IsNullOrWhiteSpace(i.IncidentType) ? i.Title : i.IncidentType,
            Title = i.Title,
            Location = i.Location,
            Building = string.IsNullOrWhiteSpace(i.Building) ? "—" : i.Building,
            SeverityLabel = SeverityLabel(i.Severity),
            SeverityBadgeBackground = sBg,
            SeverityBadgeForeground = sFg,
            Responsible = string.IsNullOrWhiteSpace(i.Responsible) ? "—" : i.Responsible,
            StatusLabel = StatusLabel(i.Status),
            StatusBadgeBackground = stBg,
            StatusBadgeForeground = stFg,
            CostDisplay = i.Cost > 0 ? Fc(i.Cost) : "—",
            InterventionSummary = interventions.Count > 0
                ? $"{interventions.Count} intervention(s)"
                : "Aucune",
            Description = i.Description,
            RiskLevel = i.RiskLevel,
            ResolutionDurationDisplay = duration,
            HasPhoto = i.HasPhoto,
            ResolutionNotes = i.ResolutionNotes ?? "—",
            Interventions = interventions
        };
    }

    private static IncidentInterventionItem MapInterventionRow(IncidentIntervention iv) => new()
    {
        Id = iv.Id,
        IncidentId = iv.IncidentId,
        Technician = iv.Technician,
        InterventionType = iv.InterventionType,
        StartDisplay = iv.StartedAt.ToString("dd/MM HH:mm"),
        EndDisplay = iv.EndedAt?.ToString("dd/MM HH:mm") ?? "—",
        CostDisplay = iv.Cost > 0 ? Fc(iv.Cost) : "—",
        StatusLabel = iv.Status,
        Result = iv.Result
    };

    private static IncidentInterventionItem MapIntervention(IncidentIntervention iv, List<Incident> incidents)
    {
        var inc = incidents.FirstOrDefault(i => i.Id == iv.IncidentId);
        var row = MapInterventionRow(iv);
        return new IncidentInterventionItem
        {
            Id = row.Id,
            IncidentId = row.IncidentId,
            IncidentCode = inc?.Code ?? "—",
            Technician = row.Technician,
            InterventionType = row.InterventionType,
            StartDisplay = row.StartDisplay,
            EndDisplay = row.EndDisplay,
            CostDisplay = row.CostDisplay,
            StatusLabel = row.StatusLabel,
            Result = row.Result
        };
    }

    private static List<IncidentAlertItem> BuildAlerts(List<Incident> all, List<Incident> open)
    {
        var alerts = new List<IncidentAlertItem>();

        foreach (var i in open.Where(x => x.IncidentType.Contains("Intrusion", StringComparison.OrdinalIgnoreCase)).Take(1))
        {
            alerts.Add(new IncidentAlertItem
            {
                Title = "Intrusion détectée",
                Message = $"{i.Location} — {i.Code}",
                AccentColor = "#7F1D1D",
                Background = "#FEE2E2"
            });
        }

        foreach (var i in open.Where(x => x.IncidentType.Contains("Caméra", StringComparison.OrdinalIgnoreCase) ||
                                          x.IncidentType.Contains("CCTV", StringComparison.OrdinalIgnoreCase)).Take(1))
        {
            alerts.Add(new IncidentAlertItem
            {
                Title = "Caméra hors ligne",
                Message = i.Description,
                AccentColor = "#EA580C",
                Background = "#FFEDD5"
            });
        }

        foreach (var i in open.Where(x => x.IncidentType.Contains("Incendie", StringComparison.OrdinalIgnoreCase)).Take(1))
        {
            alerts.Add(new IncidentAlertItem
            {
                Title = "Incendie signalé",
                Message = $"{i.Location} — intervention urgente",
                AccentColor = "#DC2626",
                Background = "#FEE2E2"
            });
        }

        foreach (var i in open.Where(x => x.IncidentType.Contains("Fuite", StringComparison.OrdinalIgnoreCase)).Take(1))
        {
            alerts.Add(new IncidentAlertItem
            {
                Title = "Fuite détectée",
                Message = i.Title,
                AccentColor = "#0EA5E9",
                Background = "#E0F2FE"
            });
        }

        alerts.Add(new IncidentAlertItem
        {
            Title = "Maintenance sécurité",
            Message = open.Count > 0 ? "Contrôle détecteurs — planifié sous 7 jours" : "À jour",
            AccentColor = "#B45309",
            Background = "#FEF3C7"
        });

        foreach (var i in open.Where(x => x.Severity == IncidentSeverity.Critique).Take(2))
        {
            alerts.Add(new IncidentAlertItem
            {
                Title = "Incident critique actif",
                Message = $"{i.Code} — {(string.IsNullOrWhiteSpace(i.IncidentType) ? i.Title : i.IncidentType)}",
                AccentColor = "#7F1D1D",
                Background = "#FEE2E2"
            });
        }

        if (alerts.Count == 0 || alerts.All(a => a.Title == "Maintenance sécurité"))
        {
            alerts.Insert(0, new IncidentAlertItem
            {
                Title = "Sécurité sous contrôle",
                Message = "Aucune alerte critique active",
                AccentColor = "#166534",
                Background = "#E8F5EE"
            });
        }

        return alerts.Take(6).ToList();
    }

    private static List<SecurityMonitorItem> BuildMonitoring(List<Incident> incidents)
    {
        var camOffline = incidents.Any(i =>
            i.Status != IncidentStatus.Resolu &&
            i.IncidentType.Contains("Caméra", StringComparison.OrdinalIgnoreCase));

        return
        [
            new() { Name = "Caméras CCTV", Category = "Vidéosurveillance", StatusLabel = camOffline ? "Alerte" : "En ligne", StatusColor = camOffline ? "#DC2626" : "#166534", Detail = "32 / 32 actives" },
            new() { Name = "Groupe électrogène", Category = "Alimentation", StatusLabel = "Opérationnel", StatusColor = "#166534", Detail = "Test auto — OK" },
            new() { Name = "Alarmes incendie", Category = "Sécurité", StatusLabel = "Armées", StatusColor = "#166534", Detail = "Dernier test : hier" },
            new() { Name = "Contrôle accès", Category = "Accès bâtiment", StatusLabel = "Actif", StatusColor = "#2563EB", Detail = "Parking + Hall" },
            new() { Name = "Détecteurs fumée", Category = "Équipements sécurité", StatusLabel = "Normal", StatusColor = "#166534", Detail = "Tous étages" }
        ];
    }

    public static string SeverityLabel(IncidentSeverity s) => s switch
    {
        IncidentSeverity.Faible => "Faible",
        IncidentSeverity.Moyenne => "Moyen",
        IncidentSeverity.Elevee => "Élevé",
        IncidentSeverity.Critique => "Critique",
        _ => "Moyen"
    };

    public static string StatusLabel(IncidentStatus s) => s switch
    {
        IncidentStatus.Ouvert => "En attente",
        IncidentStatus.EnCours => "En cours",
        IncidentStatus.InterventionProgrammee => "Intervention programmée",
        IncidentStatus.Resolu => "Résolu",
        IncidentStatus.Cloture => "Résolu",
        _ => "En attente"
    };

    public static IncidentSeverity ParseSeverity(string label) => label switch
    {
        "Faible" => IncidentSeverity.Faible,
        "Moyen" => IncidentSeverity.Moyenne,
        "Élevé" => IncidentSeverity.Elevee,
        "Critique" => IncidentSeverity.Critique,
        _ => IncidentSeverity.Moyenne
    };

    private static (string Bg, string Fg) SeverityStyle(IncidentSeverity s) => s switch
    {
        IncidentSeverity.Faible => ("#DCFCE7", "#166534"),
        IncidentSeverity.Moyenne => ("#FFEDD5", "#EA580C"),
        IncidentSeverity.Elevee => ("#FEE2E2", "#DC2626"),
        IncidentSeverity.Critique => ("#7F1D1D", "#FFFFFF"),
        _ => ("#FFEDD5", "#EA580C")
    };

    private static (string Bg, string Fg) StatusStyle(IncidentStatus s) => s switch
    {
        IncidentStatus.Resolu or IncidentStatus.Cloture => ("#DCFCE7", "#166534"),
        IncidentStatus.InterventionProgrammee => ("#DBEAFE", "#1D4ED8"),
        IncidentStatus.EnCours => ("#FFEDD5", "#EA580C"),
        _ => ("#FEF3C7", "#B45309")
    };

    private static string SeverityToRisk(IncidentSeverity s) => s switch
    {
        IncidentSeverity.Critique => "Critique",
        IncidentSeverity.Elevee => "Élevé",
        IncidentSeverity.Faible => "Faible",
        _ => "Moyen"
    };

    private static string Fc(decimal amount) => MoneyFormatter.Format(amount);
}

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Incidents;
using SmartBuilding.Domain.Entities.Personnel;
using SmartBuilding.Domain.Entities.Technical;
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
        var allEquipment = await _db.Equipment
            .OrderBy(e => e.Name)
            .ToListAsync(cancellationToken);
        var activeTechnicians = await GetActiveTechniciansAsync(cancellationToken);

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

        var equipmentById = allEquipment.ToDictionary(e => e.Id);
        var problematic = incidents
            .Where(i => i.EquipmentId.HasValue && equipmentById.ContainsKey(i.EquipmentId.Value))
            .GroupBy(i => equipmentById[i.EquipmentId!.Value].Name)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key} ({g.Count()}×)")
            .FirstOrDefault()
            ?? incidents
                .GroupBy(i => string.IsNullOrWhiteSpace(i.IncidentType) ? "Autre" : i.IncidentType)
                .OrderByDescending(g => g.Count())
                .Select(g => $"{g.Key} ({g.Count()}×)")
                .FirstOrDefault()
            ?? "—";

        var recurring = incidents
            .GroupBy(i => i.IncidentType)
            .Where(g => g.Count() >= 2)
            .Select(g => g.Key)
            .Take(3)
            .ToList();

        var items = incidents.Select(i => MapIncident(i, equipmentById)).ToList();
        var interventionItems = allInterventions
            .OrderByDescending(iv => iv.StartedAt)
            .Select(iv => MapIntervention(iv, incidents))
            .Take(20)
            .ToList();

        var alerts = BuildAlerts(incidents, open, allEquipment);
        var monitoring = BuildMonitoring(incidents, allEquipment);
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
            ,
            EquipmentOptions = allEquipment.Select(e => new IncidentEquipmentOption
            {
                Id = e.Id,
                Name = e.Name,
                Category = e.Category,
                Location = string.IsNullOrWhiteSpace(e.Location) ? "—" : e.Location,
                Label = $"{e.Name} — {e.Category} ({(string.IsNullOrWhiteSpace(e.Location) ? "—" : e.Location)})"
            }).ToList(),
            TechnicianOptions = activeTechnicians.Select(e => new IncidentTechnicianOption
            {
                Id = e.Id,
                FullName = $"{e.FirstName} {e.LastName}".Trim(),
                Matricule = e.Matricule,
                Department = e.Department,
                Position = e.Position
            }).ToList()
        };
    }

    public async Task<string> CreateIncidentAsync(Incident incident, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(incident.Title))
            return "Le titre de l'incident est obligatoire.";
        if (!incident.EquipmentId.HasValue || incident.EquipmentId == Guid.Empty)
            return "Sélectionnez le matériel concerné.";

        var equipment = await _db.Equipment.FirstOrDefaultAsync(e => e.Id == incident.EquipmentId, cancellationToken);
        if (equipment is null)
            return "Matériel introuvable.";

        if (string.IsNullOrWhiteSpace(incident.Code))
            incident.Code = $"INC-{DateTime.Today:yyyyMM}-{(await _db.Incidents.CountAsync(cancellationToken) + 1):D3}";

        incident.Building = string.IsNullOrWhiteSpace(incident.Building) ? "Tour SBMS" : incident.Building;
        incident.Responsible = string.IsNullOrWhiteSpace(incident.Responsible) ? "—" : incident.Responsible.Trim();
        if (string.IsNullOrWhiteSpace(incident.Location))
            incident.Location = string.IsNullOrWhiteSpace(equipment.Location) ? "—" : equipment.Location.Trim();
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

    public async Task<string> CreateInterventionAsync(
        Guid incidentId,
        string interventionType,
        string technician,
        DateTime scheduledAt,
        string notes,
        CancellationToken cancellationToken = default)
    {
        var incident = await _db.Incidents.FirstOrDefaultAsync(i => i.Id == incidentId, cancellationToken);
        if (incident is null)
            return "Incident introuvable.";

        if (string.IsNullOrWhiteSpace(interventionType))
            return "Le type d'intervention est obligatoire.";
        if (string.IsNullOrWhiteSpace(technician) || technician == "—")
            return "Sélectionnez un technicien actif.";

        var intervention = new IncidentIntervention
        {
            IncidentId = incidentId,
            InterventionType = interventionType.Trim(),
            Technician = string.IsNullOrWhiteSpace(technician) ? "—" : technician.Trim(),
            StartedAt = scheduledAt,
            Status = scheduledAt.Date <= DateTime.Today ? "En cours" : "Planifiée",
            Result = string.IsNullOrWhiteSpace(notes) ? "Intervention programmée" : notes.Trim(),
            Cost = 0,
            IsSynced = false
        };

        _db.IncidentInterventions.Add(intervention);
        incident.Status = scheduledAt.Date <= DateTime.Today
            ? IncidentStatus.EnCours
            : IncidentStatus.InterventionProgrammee;
        incident.MarkUpdated();
        await _db.SaveChangesAsync(cancellationToken);
        return string.Empty;
    }

    public async Task<string> CreateSecurityAlertAsync(
        string title,
        string message,
        string location,
        string severityLabel,
        string reportedBy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "Le titre de l'alerte est obligatoire.";

        return await CreateIncidentAsync(new Incident
        {
            Title = title.Trim(),
            Description = string.IsNullOrWhiteSpace(message) ? title.Trim() : message.Trim(),
            IncidentType = "Alerte sécurité",
            Location = string.IsNullOrWhiteSpace(location) ? "Hall principal" : location.Trim(),
            Building = "Tour SBMS",
            Responsible = string.IsNullOrWhiteSpace(reportedBy) ? "Sécurité SBMS" : reportedBy.Trim(),
            Severity = ParseSeverity(severityLabel),
            Status = IncidentStatus.Ouvert,
            ReportedAt = DateTime.Now,
            RiskLevel = severityLabel is "Critique" or "Élevé" ? "Élevé" : "Moyen",
            HasPhoto = false
        }, cancellationToken);
    }

    public async Task<IncidentTechnicianOption> QuickCreateTechnicianAsync(
        string fullName,
        string? department = null,
        CancellationToken cancellationToken = default)
    {
        var safeName = string.IsNullOrWhiteSpace(fullName) ? "Technicien SBMS" : fullName.Trim();
        var parts = safeName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var first = parts.FirstOrDefault() ?? "Technicien";
        var last = parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : "SBMS";

        var count = await _db.Employees.CountAsync(cancellationToken);
        var employee = new Employee
        {
            Matricule = $"EMP-{count + 1:D4}",
            FirstName = first,
            LastName = last,
            Position = "Technicien",
            Department = string.IsNullOrWhiteSpace(department) ? "Technique" : department.Trim(),
            HireDate = DateTime.Today,
            ContractStartDate = DateTime.Today,
            ContractType = "CDI",
            IsActive = true,
            RhStatus = RhConstants.EmployeeStatus.Active,
            IsSynced = false
        };
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync(cancellationToken);

        return new IncidentTechnicianOption
        {
            Id = employee.Id,
            FullName = $"{employee.FirstName} {employee.LastName}".Trim(),
            Matricule = employee.Matricule,
            Department = employee.Department,
            Position = employee.Position
        };
    }

    public async Task<IncidentEquipmentOption> QuickCreateEquipmentAsync(
        string equipmentName,
        string category,
        string location,
        CancellationToken cancellationToken = default)
    {
        var count = await _db.Equipment.CountAsync(cancellationToken);
        var equipment = new Equipment
        {
            Code = $"EQ-{DateTime.Today:yyyyMM}-{count + 1:D3}",
            Name = string.IsNullOrWhiteSpace(equipmentName) ? $"Équipement sécurité {count + 1}" : equipmentName.Trim(),
            Category = string.IsNullOrWhiteSpace(category) ? "Sécurité" : category.Trim(),
            Location = string.IsNullOrWhiteSpace(location) ? "Zone sécurité" : location.Trim(),
            Status = EquipmentStatus.Operationnel,
            LastMaintenanceDate = DateTime.Today.AddMonths(-3),
            NextMaintenanceDate = DateTime.Today.AddMonths(3),
            IsSynced = false
        };
        _db.Equipment.Add(equipment);
        await _db.SaveChangesAsync(cancellationToken);

        return new IncidentEquipmentOption
        {
            Id = equipment.Id,
            Name = equipment.Name,
            Category = equipment.Category,
            Location = equipment.Location,
            Label = $"{equipment.Name} — {equipment.Category} ({equipment.Location})"
        };
    }

    public async Task<string> ResolveIncidentAsync(
        Guid incidentId,
        string resolutionNotes,
        decimal repairCost,
        CancellationToken cancellationToken = default)
    {
        var incident = await _db.Incidents
            .Include(i => i.Interventions)
            .FirstOrDefaultAsync(i => i.Id == incidentId, cancellationToken);
        if (incident is null)
            return "Incident introuvable.";

        if (incident.Status is IncidentStatus.Resolu or IncidentStatus.Cloture)
            return "Cet incident est déjà résolu.";

        incident.ResolutionNotes = string.IsNullOrWhiteSpace(resolutionNotes) ? "Résolu" : resolutionNotes.Trim();
        incident.ResolvedAt = DateTime.UtcNow;
        incident.Status = IncidentStatus.Resolu;
        incident.MarkUpdated();

        foreach (var iv in incident.Interventions.Where(x => x.Status == "En cours" || x.Status == "Planifiée"))
        {
            iv.Status = "Terminée";
            iv.EndedAt ??= DateTime.Now;
            iv.MarkUpdated();
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (repairCost > 0 && incident.Cost <= 0)
        {
            try
            {
                await _financeLedger.RecordExpenseAsync(
                    repairCost,
                    FinanceConstants.CategoryIncident,
                    $"Résolution {incident.Code} — {incident.Title}",
                    FinanceConstants.SourceIncidents,
                    FinanceConstants.RecordedByIncidents,
                    incident.Id,
                    cancellationToken);
                incident.Cost = repairCost;
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                incident.Status = IncidentStatus.EnCours;
                incident.ResolvedAt = null;
                await _db.SaveChangesAsync(cancellationToken);
                return ex.Message;
            }
        }
        else if (repairCost > 0)
        {
            incident.Cost = repairCost;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return string.Empty;
    }

    public async Task<string> UpdateIncidentAsync(
        Guid incidentId,
        string title,
        string description,
        string severityLabel,
        CancellationToken cancellationToken = default)
    {
        var incident = await _db.Incidents.FirstOrDefaultAsync(i => i.Id == incidentId, cancellationToken);
        if (incident is null)
            return "Incident introuvable.";

        if (string.IsNullOrWhiteSpace(title))
            return "Le titre est obligatoire.";

        incident.Title = title.Trim();
        incident.Description = description.Trim();
        incident.Severity = ParseSeverity(severityLabel);
        incident.RiskLevel = SeverityToRisk(incident.Severity);
        incident.MarkUpdated();
        await _db.SaveChangesAsync(cancellationToken);
        return string.Empty;
    }

    public async Task<IReadOnlyList<IncidentListItem>> GetAllIncidentsHistoryAsync(CancellationToken cancellationToken = default)
    {
        var incidents = await _db.Incidents
            .Include(i => i.Interventions)
            .OrderByDescending(i => i.ReportedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        var equipment = await _db.Equipment.ToListAsync(cancellationToken);
        var equipmentById = equipment.ToDictionary(e => e.Id);
        return incidents.Select(i => MapIncident(i, equipmentById)).ToList();
    }

    private static IncidentListItem MapIncident(Incident i, IReadOnlyDictionary<Guid, Equipment>? equipmentById = null)
    {
        string equipmentLabel = "—";
        if (i.EquipmentId.HasValue && equipmentById is not null && equipmentById.TryGetValue(i.EquipmentId.Value, out var eq))
            equipmentLabel = $"{eq.Name} — {eq.Category}";
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
            EquipmentId = i.EquipmentId,
            EquipmentLabel = equipmentLabel,
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

    private static List<IncidentAlertItem> BuildAlerts(
        List<Incident> all,
        List<Incident> open,
        List<Equipment> equipment)
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
            var fireEquipments = equipment
                .Where(e => e.Name.Contains("incend", StringComparison.OrdinalIgnoreCase) ||
                            e.Category.Contains("incend", StringComparison.OrdinalIgnoreCase) ||
                            e.Name.Contains("fum", StringComparison.OrdinalIgnoreCase) ||
                            e.Category.Contains("fum", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var coverage = fireEquipments.Count == 0
                ? "Aucun équipement incendie enregistré en base."
                : $"{fireEquipments.Count} équipement(s) incendie disponible(s).";
            alerts.Add(new IncidentAlertItem
            {
                Title = "Incendie signalé",
                Message = $"{i.Location} — intervention urgente. {coverage}",
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

        var safetyEquipmentCount = equipment.Count(e =>
            e.Category.Contains("sécur", StringComparison.OrdinalIgnoreCase) ||
            e.Name.Contains("cam", StringComparison.OrdinalIgnoreCase) ||
            e.Name.Contains("alarme", StringComparison.OrdinalIgnoreCase) ||
            e.Name.Contains("incend", StringComparison.OrdinalIgnoreCase));
        if (safetyEquipmentCount > 0)
        {
            alerts.Add(new IncidentAlertItem
            {
                Title = "Maintenance sécurité",
                Message = open.Count > 0
                    ? $"Contrôle planifié sur {safetyEquipmentCount} équipement(s) sécurité."
                    : $"Parc sécurité : {safetyEquipmentCount} équipement(s) en base.",
                AccentColor = "#B45309",
                Background = "#FEF3C7"
            });
        }

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

    private static List<SecurityMonitorItem> BuildMonitoring(List<Incident> incidents, List<Equipment> equipment)
    {
        var monitoring = new List<SecurityMonitorItem>();
        foreach (var eq in equipment
                     .Where(e => e.Category.Contains("sécur", StringComparison.OrdinalIgnoreCase) ||
                                 e.Name.Contains("cam", StringComparison.OrdinalIgnoreCase) ||
                                 e.Name.Contains("alarme", StringComparison.OrdinalIgnoreCase) ||
                                 e.Name.Contains("incend", StringComparison.OrdinalIgnoreCase))
                     .Take(20))
        {
            var hasAlert = incidents.Any(i =>
                i.Status != IncidentStatus.Resolu &&
                i.Status != IncidentStatus.Cloture &&
                (i.Description.Contains(eq.Name, StringComparison.OrdinalIgnoreCase) ||
                 i.IncidentType.Contains(eq.Category, StringComparison.OrdinalIgnoreCase) ||
                 i.Location.Contains(eq.Location, StringComparison.OrdinalIgnoreCase)));
            monitoring.Add(new SecurityMonitorItem
            {
                Name = eq.Name,
                Category = string.IsNullOrWhiteSpace(eq.Category) ? "Sécurité" : eq.Category,
                StatusLabel = hasAlert ? "Alerte" : "Normal",
                StatusColor = hasAlert ? "#DC2626" : "#166534",
                Detail = string.IsNullOrWhiteSpace(eq.Location) ? "Emplacement non défini" : eq.Location
            });
        }

        return monitoring;
    }

    private async Task<List<Employee>> GetActiveTechniciansAsync(CancellationToken cancellationToken)
    {
        return await _db.Employees
            .Where(e => e.IsActive &&
                        ((e.Department ?? string.Empty).ToLower().Contains("tech") ||
                         (e.Position ?? string.Empty).ToLower().Contains("tech") ||
                         (e.Department ?? string.Empty).ToLower().Contains("maintenance") ||
                         (e.Position ?? string.Empty).ToLower().Contains("maintenance")))
            .OrderBy(e => e.FirstName)
            .ThenBy(e => e.LastName)
            .ToListAsync(cancellationToken);
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

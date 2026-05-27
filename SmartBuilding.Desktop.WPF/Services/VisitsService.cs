using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Visitors;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Infrastructure.Persistence;

namespace SmartBuilding.Desktop.WPF.Services;

public class VisitsService
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly string[] ZoneNames =
    [
        "Réception", "Parking", "Bureau administratif", "Salle réunion",
        "Salle technique", "Hall principal", "Sous-sol", "Zone sécurisée"
    ];

    private readonly SmartBuildingDbContext _db;

    public VisitsService(SmartBuildingDbContext db) => _db = db;

    public async Task<VisitsPageData> LoadAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var visitors = await _db.Visitors.OrderByDescending(v => v.CheckInAt).ToListAsync(cancellationToken);
        var appointments = await _db.VisitorAppointments.OrderBy(a => a.ScheduledAt).ToListAsync(cancellationToken);

        var todayVisits = visitors.Where(v => v.CheckInAt.Date == today).ToList();
        var active = visitors.Where(v => v.AccessStatus == "Actif" && !v.CheckOutAt.HasValue).ToList();
        var granted = visitors.Count(v => v.AccessStatus is "Actif" or "Sorti");
        var denied = visitors.Count(v => v.AccessStatus == "Refusé");
        var pendingOut = active.Count(v => v.ExpectedCheckOutAt.HasValue && v.ExpectedCheckOutAt.Value < DateTime.Now);
        var scheduled = appointments.Count(a => a.ScheduledAt.Date >= today && a.Status is "Confirmé" or "En attente");

        var dailyTrend = new List<VisitDayPoint>();
        for (var i = 6; i >= 0; i--)
        {
            var d = today.AddDays(-i);
            dailyTrend.Add(new VisitDayPoint
            {
                Label = d.ToString("ddd", Fr),
                Count = visitors.Count(v => v.CheckInAt.Date == d)
            });
        }

        var typeDist = visitors
            .GroupBy(v => string.IsNullOrWhiteSpace(v.VisitType) ? "Autre" : v.VisitType)
            .OrderByDescending(g => g.Count())
            .Select(g => new VisitTypeSlice { Type = g.Key, Count = g.Count() })
            .ToList();

        var accessDist = new[]
        {
            new VisitAccessSlice { Label = "Validés", Count = granted },
            new VisitAccessSlice { Label = "Refusés", Count = denied },
            new VisitAccessSlice { Label = "En attente", Count = visitors.Count(v => v.AccessStatus == "En attente") }
        }.Where(x => x.Count > 0).ToList();

        var hourly = Enumerable.Range(7, 12).Select(h => new VisitHourPoint
        {
            Label = $"{h}h",
            Count = visitors.Count(v => v.CheckInAt.Hour == h && v.CheckInAt.Date >= today.AddDays(-30))
        }).ToList();

        var busiest = visitors.GroupBy(v => v.Zone).OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key} ({g.Count()})").FirstOrDefault() ?? "Réception";

        var peak = hourly.OrderByDescending(h => h.Count).FirstOrDefault();
        var completed = visitors.Where(v => v.CheckOutAt.HasValue).ToList();
        var avgHours = completed.Count == 0 ? 0 : completed.Average(v => (v.CheckOutAt!.Value - v.CheckInAt).TotalHours);

        var items = visitors.Select(MapVisit).ToList();
        var apptItems = appointments
            .Where(a => a.ScheduledAt.Date >= today.AddDays(-1))
            .OrderBy(a => a.ScheduledAt)
            .Take(15)
            .Select(MapAppointment)
            .ToList();

        var alerts = BuildAlerts(visitors, active, appointments);
        var zones = BuildAccessZones(visitors, active);
        var security = denied > 2 || active.Any(v => v.Zone == "Zone sécurisée" && v.AccessStatus == "En attente")
            ? ("Contrôle renforcé", "#EA580C")
            : denied > 0 ? ("Accès surveillé", "#2563EB") : ("Accès normal", "#166534");

        var frequent = visitors.GroupBy(v => v.FullName).OrderByDescending(g => g.Count()).FirstOrDefault();
        var insights = new List<VisitInsightLine>
        {
            new() { Label = "Visiteurs fréquents", Value = frequent is null ? "—" : $"{frequent.Key} ({frequent.Count()}×)", Accent = "#2563EB" },
            new() { Label = "Zone la plus visitée", Value = busiest, Accent = "#023E8A" },
            new() { Label = "Heure de pointe", Value = peak is null ? "—" : $"{peak.Label} ({peak.Count} entrées)", Accent = "#6D28D9" },
            new() { Label = "Durée moyenne visite", Value = avgHours > 0 ? $"{avgHours:F1} h" : "—", Accent = "#1B3D3B" },
            new() { Label = "Historique fréquentation", Value = todayVisits.Count > 5 ? "Forte affluence" : "Affluence normale", Accent = "#166534" }
        };

        return new VisitsPageData
        {
            VisitorsToday = todayVisits.Count,
            ActiveVisits = active.Count,
            AccessGranted = granted,
            AccessDenied = denied,
            ScheduledAppointments = scheduled,
            PendingCheckouts = pendingOut,
            SecurityStatusLabel = security.Item1,
            SecurityStatusColor = security.Item2,
            BusiestZone = busiest,
            PeakHourLabel = peak is null ? "—" : peak.Label,
            AverageDurationDisplay = avgHours > 0 ? $"{avgHours:F1} h" : "—",
            Visits = items,
            Appointments = apptItems,
            Alerts = alerts,
            AccessZones = zones,
            Insights = insights,
            DailyTrend = dailyTrend,
            TypeDistribution = typeDist,
            AccessDistribution = accessDist,
            HourlyTraffic = hourly
        };
    }

    public async Task<string> CreateVisitorAsync(Visitor visitor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(visitor.FullName))
            return "Le nom du visiteur est obligatoire.";
        if (string.IsNullOrWhiteSpace(visitor.HostName))
            return "La personne visitée est obligatoire.";

        if (string.IsNullOrWhiteSpace(visitor.VisitCode))
            visitor.VisitCode = $"VIS-{DateTime.Today:yyyyMMdd}-{(await _db.Visitors.CountAsync(cancellationToken) + 1):D3}";

        visitor.CheckInAt = visitor.CheckInAt == default ? DateTime.Now : visitor.CheckInAt;
        visitor.AccessStatus = string.IsNullOrWhiteSpace(visitor.AccessStatus) ? "Actif" : visitor.AccessStatus;
        visitor.BadgeNumber ??= $"B-{DateTime.Now:HHmm}";
        visitor.IsSynced = false;

        _db.Visitors.Add(visitor);
        await _db.SaveChangesAsync(cancellationToken);
        return string.Empty;
    }

    public async Task<string> CheckoutVisitorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var v = await _db.Visitors.FindAsync([id], cancellationToken);
        if (v is null) return "Visite introuvable.";
        v.CheckOutAt = DateTime.Now;
        v.AccessStatus = "Sorti";
        v.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return string.Empty;
    }

    private static VisitListItem MapVisit(Visitor v)
    {
        var (bg, fg) = StatusStyle(v.AccessStatus);
        var duration = v.CheckOutAt.HasValue
            ? $"{(v.CheckOutAt.Value - v.CheckInAt).TotalHours:F1} h"
            : $"{(DateTime.Now - v.CheckInAt).TotalHours:F1} h (en cours)";

        var palette = AvatarPalette(v.FullName);

        return new VisitListItem
        {
            Id = v.Id,
            VisitCode = string.IsNullOrWhiteSpace(v.VisitCode) ? "—" : v.VisitCode,
            FullName = v.FullName,
            Initials = GetInitials(v.FullName),
            LogoBackground = palette.bg,
            LogoForeground = palette.fg,
            Phone = v.Phone ?? "—",
            HostName = v.HostName,
            Purpose = v.Purpose,
            VisitType = v.VisitType,
            CheckInDisplay = v.CheckInAt.ToString("dd/MM/yyyy HH:mm", Fr),
            CheckOutDisplay = v.CheckOutAt?.ToString("dd/MM/yyyy HH:mm", Fr) ?? "En cours",
            AccessStatus = v.AccessStatus,
            StatusBadgeBackground = bg,
            StatusBadgeForeground = fg,
            BadgeNumber = v.BadgeNumber ?? "—",
            Building = v.Building,
            Zone = v.Zone,
            Company = v.Company ?? "—",
            Email = v.Email ?? "—",
            IdDocument = v.IdDocument ?? "—",
            IdDocumentType = v.IdDocumentType,
            AllowedZones = v.AllowedZones,
            PresenceDurationDisplay = duration,
            Notes = v.Notes ?? "—",
            VisitHistory = [$"Entrée {v.CheckInAt:dd/MM/yyyy HH:mm} — {v.Zone}"]
        };
    }

    private static VisitAppointmentItem MapAppointment(VisitorAppointment a)
    {
        var (bg, fg) = AppointmentStyle(a.Status);
        return new VisitAppointmentItem
        {
            Id = a.Id,
            VisitorName = a.VisitorName,
            HostName = a.HostName,
            ScheduledDisplay = a.ScheduledAt.ToString("dd/MM/yyyy HH:mm", Fr),
            Room = a.Room,
            Status = a.Status,
            StatusBadgeBackground = bg,
            StatusBadgeForeground = fg,
            DurationDisplay = $"{a.DurationMinutes} min",
            Purpose = a.Purpose
        };
    }

    private static List<VisitAlertItem> BuildAlerts(
        List<Visitor> visitors, List<Visitor> active, List<VisitorAppointment> appointments)
    {
        var alerts = new List<VisitAlertItem>();
        var now = DateTime.Now;

        foreach (var v in active.Where(x => x.ExpectedCheckOutAt.HasValue && x.ExpectedCheckOutAt < now))
            alerts.Add(new() { Title = "Présence prolongée", Message = $"{v.FullName} — dépassement horaire prévu", Background = "#FFEDD5", AccentColor = "#EA580C" });

        foreach (var v in visitors.Where(x => x.AccessStatus == "Refusé").Take(2))
            alerts.Add(new() { Title = "Accès refusé", Message = $"{v.FullName} — {v.Zone}", Background = "#FEE2E2", AccentColor = "#DC2626" });

        foreach (var a in appointments.Where(x => x.ScheduledAt > now && x.ScheduledAt <= now.AddHours(1)))
            alerts.Add(new() { Title = "Rendez-vous imminent", Message = $"{a.VisitorName} → {a.HostName} à {a.ScheduledAt:HH:mm}", Background = "#DBEAFE", AccentColor = "#2563EB" });

        foreach (var v in active.Where(x => (now - x.CheckInAt).TotalHours > 8))
            alerts.Add(new() { Title = "Visiteur toujours présent", Message = $"{v.FullName} depuis {v.CheckInAt:HH:mm}", Background = "#FEF3C7", AccentColor = "#D97706" });

        if (alerts.Count == 0)
            alerts.Add(new() { Title = "Réception sous contrôle", Message = "Aucune alerte accès critique", Background = "#DCFCE7", AccentColor = "#166534" });

        return alerts.Take(6).ToList();
    }

    private static List<AccessZoneItem> BuildAccessZones(List<Visitor> visitors, List<Visitor> active)
    {
        return ZoneNames.Select(zone =>
        {
            var count = active.Count(v => v.Zone == zone || v.AllowedZones.Contains(zone, StringComparison.OrdinalIgnoreCase));
            var denied = visitors.Any(v => v.Zone == zone && v.AccessStatus == "Refusé");
            var (label, color, bg) = denied ? ("Restreint", "#DC2626", "#FEE2E2")
                : count > 3 ? ("Affluence", "#EA580C", "#FFEDD5")
                : ("Ouvert", "#166534", "#DCFCE7");
            return new AccessZoneItem
            {
                ZoneName = zone,
                StatusLabel = label,
                StatusColor = color,
                StatusBackground = bg,
                ActiveCount = count,
                Detail = count > 0 ? $"{count} visiteur(s)" : "Aucune présence"
            };
        }).ToList();
    }

    private static (string bg, string fg) StatusStyle(string status) => status switch
    {
        "Actif" => ("#DCFCE7", "#166534"),
        "En attente" => ("#FFEDD5", "#EA580C"),
        "Refusé" => ("#FEE2E2", "#DC2626"),
        "Sorti" => ("#F1F5F9", "#64748B"),
        _ => ("#E0F2FE", "#0369A1")
    };

    private static (string bg, string fg) AppointmentStyle(string status) => status switch
    {
        "Confirmé" => ("#DCFCE7", "#166534"),
        "En attente" => ("#FFEDD5", "#EA580C"),
        "Terminé" => ("#F1F5F9", "#64748B"),
        "Annulé" => ("#FEE2E2", "#DC2626"),
        _ => ("#DBEAFE", "#2563EB")
    };

    private static (string bg, string fg) AvatarPalette(string name)
    {
        var palettes = new (string, string)[]
        {
            ("#DBEAFE", "#1D4ED8"), ("#DCFCE7", "#166534"), ("#EDE9FE", "#6D28D9"),
            ("#FFEDD5", "#EA580C"), ("#E0F2FE", "#0369A1"), ("#FCE7F3", "#BE185D")
        };
        return palettes[Math.Abs(name.GetHashCode()) % palettes.Length];
    }

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
            : name.Length >= 2 ? name[..2].ToUpperInvariant() : name.ToUpperInvariant();
    }
}

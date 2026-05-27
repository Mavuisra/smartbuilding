using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Auth;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Domain.Entities.Sync;
using SmartBuilding.Domain.Entities.System;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Infrastructure.Persistence;

namespace SmartBuilding.Desktop.WPF.Services;

public class ActivityLogModuleService
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    private readonly SmartBuildingDbContext _db;

    public ActivityLogModuleService(SmartBuildingDbContext db) => _db = db;

    public async Task<ActivityLogPageData> LoadAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var rangeEnd = today;
        var rangeStart = today.AddDays(-3);
        var yesterday = today.AddDays(-1);

        var users = await _db.Users.IgnoreQueryFilters()
            .Where(u => u.DeletedAt == null)
            .ToListAsync(cancellationToken);
        var userMap = users.ToDictionary(u => u.Id);

        var building = await _db.BuildingInfos.FirstOrDefaultAsync(cancellationToken);
        var location = building is null
            ? "—"
            : string.Join(", ", new[] { building.City, building.Country }.Where(s => !string.IsNullOrWhiteSpace(s)));

        var raw = new List<RawActivity>();
        raw.AddRange(await LoadSystemLogsAsync(userMap, location, cancellationToken));
        raw.AddRange(await LoadSyncLogsAsync(location, cancellationToken));
        raw.AddRange(LoadLoginActivities(users, location));
        raw.AddRange(await LoadTenantActivitiesAsync(location, cancellationToken));

        var inRange = raw.Where(a => a.OccurredAt.Date >= rangeStart && a.OccurredAt.Date <= rangeEnd).ToList();
        var allItems = raw.OrderByDescending(a => a.OccurredAt).Select(a => MapItem(a, location)).ToList();

        var todayItems = raw.Where(a => a.OccurredAt.Date == today).ToList();
        var yesterdayItems = raw.Where(a => a.OccurredAt.Date == yesterday).ToList();

        int CountToday(Func<RawActivity, bool> pred) => todayItems.Count(pred);
        int CountYesterday(Func<RawActivity, bool> pred) => yesterdayItems.Count(pred);

        var loginsToday = CountToday(a => a.ActivityType == "Connexion");
        var modsToday = CountToday(a => a.ActivityType == "Modification");
        var secToday = CountToday(a => a.ActivityType == "Sécurité");
        var errToday = CountToday(a => a.ActivityType == "Erreur");
        var syncToday = CountToday(a => a.ActivityType == "Synchronisation");

        var typeFilters = new List<string> { "Tous les types" };
        typeFilters.AddRange(raw.Select(a => a.ActivityType).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t));

        var moduleFilters = new List<string> { "Tous les modules" };
        moduleFilters.AddRange(raw.Select(a => a.Module).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(m => m));

        var userFilters = new List<string> { "Tous les utilisateurs" };
        userFilters.AddRange(raw.Select(a => a.UserName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(u => u));

        var statusFilters = new List<string> { "Tous les statuts", "Succès", "Échec", "Erreur", "Avertissement" };

        return new ActivityLogPageData
        {
            ActivitiesToday = todayItems.Count,
            LoginsCount = loginsToday,
            ModificationsCount = modsToday,
            SecurityAlertsCount = secToday,
            SystemErrorsCount = errToday,
            SyncCount = syncToday,
            ActivitiesTodayTrend = FormatTrend(todayItems.Count, yesterdayItems.Count),
            LoginsTrend = FormatTrend(loginsToday, CountYesterday(a => a.ActivityType == "Connexion")),
            ModificationsTrend = FormatTrend(modsToday, CountYesterday(a => a.ActivityType == "Modification")),
            SecurityAlertsTrend = FormatTrend(secToday, CountYesterday(a => a.ActivityType == "Sécurité")),
            SystemErrorsTrend = FormatTrend(errToday, CountYesterday(a => a.ActivityType == "Erreur")),
            SyncTrend = FormatTrend(syncToday, CountYesterday(a => a.ActivityType == "Synchronisation")),
            ActivitiesSparkline = BuildDailySparkline(raw, today, 7),
            LoginsSparkline = BuildTypeSparkline(raw, today, "Connexion", 7),
            ModificationsSparkline = BuildTypeSparkline(raw, today, "Modification", 7),
            SecuritySparkline = BuildTypeSparkline(raw, today, "Sécurité", 7),
            ErrorsSparkline = BuildTypeSparkline(raw, today, "Erreur", 7),
            SyncSparkline = BuildTypeSparkline(raw, today, "Synchronisation", 7),
            Activities = allItems,
            TypeFilters = typeFilters,
            ModuleFilters = moduleFilters,
            UserFilters = userFilters,
            StatusFilters = statusFilters,
            DateRangeStart = rangeStart,
            DateRangeEnd = rangeEnd
        };
    }

    public IReadOnlyList<ActivityLogRelatedItem> BuildRelatedActivities(
        ActivityLogListItem selected,
        IReadOnlyList<ActivityLogListItem> all)
    {
        return all
            .Where(a => a.Id != selected.Id &&
                        (a.Module == selected.Module || a.UserName == selected.UserName))
            .OrderByDescending(a => a.OccurredAt)
            .Take(4)
            .Select(a => new ActivityLogRelatedItem
            {
                Title = a.ActionTitle,
                TimeDisplay = a.TimeDisplay,
                IconKind = a.IconKind
            })
            .ToList();
    }

    private async Task<List<RawActivity>> LoadSystemLogsAsync(
        Dictionary<Guid, User> userMap,
        string location,
        CancellationToken ct)
    {
        var logs = await _db.SystemLogs.OrderByDescending(l => l.CreatedAt).Take(500).ToListAsync(ct);
        return logs.Select(l => MapSystemLog(l, userMap, location)).ToList();
    }

    private static RawActivity MapSystemLog(SystemLog l, Dictionary<Guid, User> userMap, string location)
    {
        var user = l.UserId.HasValue && userMap.TryGetValue(l.UserId.Value, out var u) ? u : null;
        var (type, title, desc) = ClassifyMessage(l.Message, l.Level);
        var status = MapStatus(l.Level, l.Message);
        var module = MapModule(l.Source, l.Message);

        return new RawActivity
        {
            Id = l.Id,
            OccurredAt = l.CreatedAt,
            UserName = user?.FullName ?? "Système Automatique",
            UserRole = user is null ? "Système" : RoleLabel(user.Role),
            ActivityType = type,
            ActionTitle = title,
            ActionDescription = desc,
            Module = module,
            Details = l.Message,
            Status = status,
            Level = l.Level,
            Source = l.Source,
            IpAddress = "—",
            DeviceInfo = "SBMS Desktop",
            Location = location,
            FileName = ExtractFileName(l.Message),
            OldValues = l.Level is "Error" or "Warning" ? l.Message : "—",
            NewValues = "—"
        };
    }

    private async Task<List<RawActivity>> LoadSyncLogsAsync(string location, CancellationToken ct)
    {
        var logs = await _db.SyncLogs.OrderByDescending(l => l.StartedAt).Take(200).ToListAsync(ct);
        return logs.Select(l => new RawActivity
        {
            Id = l.Id,
            OccurredAt = l.StartedAt,
            UserName = "Système Automatique",
            UserRole = "Système",
            ActivityType = "Synchronisation",
            ActionTitle = l.Success ? "Synchronisation réussie" : "Erreur de synchronisation",
            ActionDescription = l.Success
                ? $"{l.RecordsPushed} envoyés, {l.RecordsPulled} reçus"
                : l.ErrorMessage ?? "Échec de synchronisation",
            Module = "Synchronisation",
            Details = l.ErrorMessage ?? $"Direction : {l.Direction}",
            Status = l.Success ? "Succès" : "Erreur",
            Level = l.Success ? "Info" : "Error",
            Source = "Sync",
            IpAddress = "—",
            DeviceInfo = "SBMS Desktop",
            Location = location,
            OldValues = "—",
            NewValues = l.Success ? $"{l.RecordsPushed}/{l.RecordsPulled}" : l.ErrorMessage ?? "—"
        }).ToList();
    }

    private static List<RawActivity> LoadLoginActivities(List<User> users, string location)
    {
        return users
            .Where(u => u.LastLoginAt.HasValue)
            .Select(u => new RawActivity
            {
                Id = Guid.NewGuid(),
                OccurredAt = u.LastLoginAt!.Value,
                UserName = u.FullName,
                UserRole = RoleLabel(u.Role),
                ActivityType = "Connexion",
                ActionTitle = "Connexion réussie",
                ActionDescription = $"Identifiant {u.Username}",
                Module = "Système",
                Details = u.Email,
                Status = "Succès",
                Level = "Info",
                Source = "Auth",
                IpAddress = "—",
                DeviceInfo = "SBMS Desktop",
                Location = location,
                OldValues = "—",
                NewValues = "Session ouverte"
            }).ToList();
    }

    private async Task<List<RawActivity>> LoadTenantActivitiesAsync(string location, CancellationToken ct)
    {
        var items = await _db.TenantActivities
            .Include(t => t.Tenant)
            .OrderByDescending(t => t.OccurredAt)
            .Take(150)
            .ToListAsync(ct);

        return items.Select(t => new RawActivity
        {
            Id = t.Id,
            OccurredAt = t.OccurredAt,
            UserName = t.Tenant?.Name ?? "Locataire",
            UserRole = "Gestion",
            ActivityType = "Modification",
            ActionTitle = t.Title,
            ActionDescription = t.Description,
            Module = "Locations",
            Details = t.Category,
            Status = "Succès",
            Level = "Info",
            Source = "Locations",
            IpAddress = "—",
            DeviceInfo = "SBMS Desktop",
            Location = location,
            OldValues = "—",
            NewValues = t.Description
        }).ToList();
    }

    private static ActivityLogListItem MapItem(RawActivity a, string defaultLocation)
    {
        var (icon, iconColor, titleFg) = StyleForType(a.ActivityType, a.Status);
        var (modBg, modFg) = ModuleBadge(a.Module);
        var palette = AvatarPalette(a.UserName);
        var isFail = a.Status is "Échec" or "Erreur";

        return new ActivityLogListItem
        {
            Id = a.Id,
            ActivityCode = $"ACT-{a.OccurredAt:yyyy}-{Math.Abs(a.Id.GetHashCode()) % 1_000_000:D6}",
            OccurredAt = a.OccurredAt,
            TimeDisplay = a.OccurredAt.ToLocalTime().ToString("HH:mm", Fr),
            DateDisplay = a.OccurredAt.ToLocalTime().ToString("dd MMM yyyy", Fr),
            UserName = a.UserName,
            UserRole = a.UserRole,
            UserInitials = GetInitials(a.UserName),
            AvatarBackground = palette.bg,
            AvatarForeground = palette.fg,
            ActionTitle = a.ActionTitle,
            ActionDescription = a.ActionDescription,
            ActivityType = a.ActivityType,
            Module = a.Module,
            ModuleBadgeBackground = modBg,
            ModuleBadgeForeground = modFg,
            Details = Truncate(a.Details, 80),
            DeviceInfo = a.DeviceInfo,
            IpAddress = a.IpAddress,
            StatusLabel = a.Status,
            StatusDotColor = StatusColor(a.Status),
            IconKind = icon,
            IconColor = iconColor,
            TitleForeground = isFail ? "#DC2626" : titleFg,
            FileName = a.FileName,
            FilePath = a.FileName == "—" ? "—" : $"/documents/{a.FileName}",
            Browser = "SBMS Desktop",
            Location = string.IsNullOrWhiteSpace(a.Location) ? defaultLocation : a.Location,
            OldValues = a.OldValues,
            NewValues = a.NewValues
        };
    }

    private static (string type, string title, string desc) ClassifyMessage(string message, string level)
    {
        var m = message.ToLowerInvariant();
        if (m.Contains("connexion") && (m.Contains("échou") || m.Contains("fail") || m.Contains("invalid")))
            return ("Sécurité", "Tentative de connexion échouée", message);
        if (m.Contains("connexion") || m.Contains("login"))
            return ("Connexion", "Connexion réussie", message);
        if (m.Contains("document") || m.Contains("fichier") || m.Contains(".pdf"))
            return ("Modification", "Document modifié", message);
        if (m.Contains("modif") || m.Contains("mise à jour") || m.Contains("update"))
            return ("Modification", "Modification enregistrée", message);
        if (level.Equals("Error", StringComparison.OrdinalIgnoreCase))
            return ("Erreur", "Erreur système", message);
        if (level.Equals("Warning", StringComparison.OrdinalIgnoreCase))
            return ("Sécurité", "Alerte système", message);
        return ("Information", "Activité système", message);
    }

    private static string MapStatus(string level, string message)
    {
        var m = message.ToLowerInvariant();
        if (m.Contains("échou") || m.Contains("fail")) return "Échec";
        return level.ToLowerInvariant() switch
        {
            "error" => "Erreur",
            "warning" => "Avertissement",
            _ => "Succès"
        };
    }

    private static string MapModule(string source, string message)
    {
        var s = source.ToLowerInvariant();
        var m = message.ToLowerInvariant();
        if (s.Contains("sync")) return "Synchronisation";
        if (s.Contains("auth") || m.Contains("connexion")) return "Système";
        if (s.Contains("email")) return "Emails";
        if (s.Contains("finance") || m.Contains("facture")) return "Finances";
        if (s.Contains("location") || m.Contains("locataire")) return "Locations";
        if (s.Contains("document") || m.Contains("fichier")) return "Documents";
        if (s.Contains("personnel")) return "Personnel";
        if (s.Contains("technical") || m.Contains("maintenance")) return "Technique";
        if (string.IsNullOrWhiteSpace(source)) return "Système";
        return char.ToUpper(source[0]) + source[1..];
    }

    private static string ExtractFileName(string message)
    {
        var match = Regex.Match(message, @"[\w\-]+\.(pdf|docx?|xlsx?|png|jpg)", RegexOptions.IgnoreCase);
        return match.Success ? match.Value : "—";
    }

    private static (string icon, string color, string titleFg) StyleForType(string type, string status)
    {
        if (status is "Échec") return ("ShieldAlert", "#DC2626", "#DC2626");
        if (status is "Erreur") return ("AlertCircle", "#EA580C", "#EA580C");
        return type switch
        {
            "Connexion" => ("Login", "#2D6A4F", "#1B3D3B"),
            "Modification" => ("FileEdit", "#EA580C", "#1B3D3B"),
            "Synchronisation" => ("Sync", "#0EA5E9", "#1B3D3B"),
            "Sécurité" => ("ShieldAlert", "#DC2626", "#DC2626"),
            "Erreur" => ("AlertCircle", "#6D28D9", "#6D28D9"),
            _ => ("InformationOutline", "#2563EB", "#1B3D3B")
        };
    }

    private static (string bg, string fg) ModuleBadge(string module) => module switch
    {
        "Système" => ("#F1F5F9", "#475569"),
        "Synchronisation" => ("#E0F2FE", "#0369A1"),
        "Documents" => ("#EDE9FE", "#6D28D9"),
        "Finances" => ("#DCFCE7", "#166534"),
        "Locations" => ("#FFEDD5", "#EA580C"),
        "Emails" => ("#DBEAFE", "#2563EB"),
        "Sécurité" => ("#FEE2E2", "#DC2626"),
        _ => ("#F1F5F9", "#475569")
    };

    private static string StatusColor(string status) => status switch
    {
        "Succès" => "#22C55E",
        "Échec" => "#EF4444",
        "Erreur" => "#EA580C",
        "Avertissement" => "#F59E0B",
        _ => "#94A3B8"
    };

    private static string RoleLabel(UserRole role) => role switch
    {
        UserRole.Administrateur => "Administrateur",
        UserRole.Comptable => "Comptable",
        UserRole.Technique => "Technicien",
        UserRole.Gestionnaire => "Gestionnaire",
        _ => role.ToString()
    };

    private static (string bg, string fg) AvatarPalette(string name)
    {
        var hash = Math.Abs(name.GetHashCode());
        var palettes = new[]
        {
            ("#DBEAFE", "#2563EB"),
            ("#DCFCE7", "#166534"),
            ("#EDE9FE", "#6D28D9"),
            ("#FFEDD5", "#EA580C")
        };
        return palettes[hash % palettes.Length];
    }

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
            : name.Length >= 2 ? name[..2].ToUpperInvariant() : "??";
    }

    private static string FormatTrend(int current, int previous)
    {
        if (previous == 0)
            return current == 0 ? "0% vs hier" : "+100% vs hier";
        var pct = (current - previous) * 100.0 / previous;
        return $"{(pct >= 0 ? "+" : "")}{pct:0.#}% vs hier";
    }

    private static List<int> BuildDailySparkline(List<RawActivity> items, DateTime today, int days)
    {
        var result = new List<int>();
        for (var i = days - 1; i >= 0; i--)
        {
            var d = today.AddDays(-i);
            result.Add(items.Count(a => a.OccurredAt.Date == d));
        }
        return result;
    }

    private static List<int> BuildTypeSparkline(List<RawActivity> items, DateTime today, string type, int days)
    {
        var result = new List<int>();
        for (var i = days - 1; i >= 0; i--)
        {
            var d = today.AddDays(-i);
            result.Add(items.Count(a => a.OccurredAt.Date == d && a.ActivityType == type));
        }
        return result;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";

    private sealed class RawActivity
    {
        public Guid Id { get; init; }
        public DateTime OccurredAt { get; init; }
        public string UserName { get; init; } = string.Empty;
        public string UserRole { get; init; } = string.Empty;
        public string ActivityType { get; init; } = string.Empty;
        public string ActionTitle { get; init; } = string.Empty;
        public string ActionDescription { get; init; } = string.Empty;
        public string Module { get; init; } = string.Empty;
        public string Details { get; init; } = string.Empty;
        public string Status { get; init; } = "Succès";
        public string Level { get; init; } = "Info";
        public string Source { get; init; } = string.Empty;
        public string IpAddress { get; init; } = "—";
        public string DeviceInfo { get; init; } = string.Empty;
        public string Location { get; init; } = "—";
        public string FileName { get; init; } = "—";
        public string OldValues { get; init; } = "—";
        public string NewValues { get; init; } = "—";
    }
}

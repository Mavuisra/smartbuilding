using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Auth;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Services;
using SmartBuilding.Shared.Constants;

namespace SmartBuilding.Desktop.WPF.Services;

public class UsersModuleService
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    private readonly SmartBuildingDbContext _db;

    public UsersModuleService(SmartBuildingDbContext db) => _db = db;

    public async Task<UsersPageData> LoadAsync(Guid? currentUserId, CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var prevMonthStart = monthStart.AddMonths(-1);
        var onlineThreshold = DateTime.UtcNow.AddMinutes(-30);

        var users = await _db.Users.IgnoreQueryFilters()
            .Where(u => u.DeletedAt == null)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync(cancellationToken);

        var building = await _db.BuildingInfos.FirstOrDefaultAsync(cancellationToken);
        var location = building is null
            ? "—"
            : string.Join(", ", new[] { building.City, building.Country }.Where(s => !string.IsNullOrWhiteSpace(s)));

        var items = users.Select(u => MapUser(u, onlineThreshold, currentUserId)).ToList();

        var total = users.Count;
        var admins = users.Count(u => u.Role == UserRole.Administrateur);
        var active = users.Count(u => u.IsActive);
        var suspended = users.Count(u => !u.IsActive);
        var loginsToday = users.Count(u => u.LastLoginAt?.Date == today);
        var activeSessions = users.Count(u => u.IsActive && u.LastLoginAt >= onlineThreshold);

        var thisMonth = users.Count(u => u.CreatedAt >= monthStart);
        var prevMonth = users.Count(u => u.CreatedAt >= prevMonthStart && u.CreatedAt < monthStart);
        var activeYesterday = users.Count(u => u.IsActive && u.LastLoginAt?.Date == today.AddDays(-1));

        var roleDist = users.GroupBy(u => RoleLabel(u.Role))
            .Select(g => new UserRoleSlice { Role = g.Key, Count = g.Count() })
            .OrderByDescending(r => r.Count)
            .ToList();

        var statusDist = new List<UserStatusSlice>
        {
            new() { Status = "Actif", Count = active },
            new() { Status = "Suspendu", Count = suspended },
            new() { Status = "Inactif", Count = users.Count(u => !u.IsActive && u.DeletedAt == null) }
        }.Where(s => s.Count > 0).ToList();

        var loginTrend = new List<UserDayPoint>();
        for (var i = 6; i >= 0; i--)
        {
            var d = today.AddDays(-i);
            loginTrend.Add(new UserDayPoint
            {
                Label = d.ToString("ddd", Fr),
                Count = users.Count(u => u.LastLoginAt?.Date == d)
            });
        }

        var recentSignups = users.OrderByDescending(u => u.CreatedAt).Take(3)
            .Select(u =>
            {
                var palette = AvatarPalette(u.FullName);
                return new UserRecentSignupItem
                {
                    FullName = u.FullName,
                    RoleLabel = RoleLabel(u.Role),
                    DateDisplay = u.CreatedAt.ToLocalTime().ToString("dd MMM yyyy", Fr),
                    Initials = GetInitials(u.FullName),
                    AvatarBackground = palette.bg,
                    AvatarForeground = palette.fg
                };
            }).ToList();

        var roleFilters = new List<string> { "Tous les rôles" };
        roleFilters.AddRange(roleDist.Select(r => r.Role).Distinct());

        return new UsersPageData
        {
            TotalCount = total,
            AdministratorsCount = admins,
            ActiveCount = active,
            SuspendedCount = suspended,
            LoginsTodayCount = loginsToday,
            ActiveSessionsCount = Math.Max(activeSessions, currentUserId.HasValue ? 1 : 0),
            TotalTrend = FormatTrend(thisMonth, prevMonth, "Ce mois"),
            AdministratorsTrend = FormatTrend(admins, users.Count(u => u.Role == UserRole.Administrateur && u.CreatedAt < monthStart), "Ce mois"),
            ActiveTrend = FormatTrend(active, activeYesterday, "Aujourd'hui"),
            SuspendedTrend = FormatTrend(suspended, users.Count(u => !u.IsActive && u.UpdatedAt < monthStart), "Ce mois"),
            LoginsTodayTrend = FormatTrend(loginsToday, users.Count(u => u.LastLoginAt?.Date == today.AddDays(-1)), "Aujourd'hui"),
            ActiveSessionsTrend = "Temps réel",
            TotalSparkline = BuildMonthlySparkline(users, today),
            AdministratorsSparkline = [admins],
            ActiveSparkline = BuildWeeklyActiveSparkline(users, today),
            SuspendedSparkline = [suspended],
            LoginsSparkline = loginTrend.Select(p => p.Count).ToList(),
            SessionsSparkline = [Math.Max(activeSessions, 1)],
            Users = items,
            RoleDistribution = roleDist,
            StatusDistribution = statusDist,
            LoginTrend = loginTrend,
            RecentSignups = recentSignups,
            RoleFilters = roleFilters,
            DefaultLocation = string.IsNullOrWhiteSpace(location) ? "—" : location
        };
    }

    public async Task<IReadOnlyList<UserActivityItem>> LoadActivitiesAsync(
        UserListItem user,
        CancellationToken cancellationToken = default)
    {
        var items = new List<UserActivityItem>();
        var entity = await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == user.Id, cancellationToken);
        if (entity is null) return items;

        if (entity.LastLoginAt.HasValue)
        {
            items.Add(new UserActivityItem
            {
                Title = "Connexion réussie",
                Description = $"Identifiant {entity.Username}",
                TimeDisplay = entity.LastLoginAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Fr),
                IconKind = "Login",
                IconColor = "#2D6A4F"
            });
        }

        items.Add(new UserActivityItem
        {
            Title = "Compte créé",
            Description = entity.Email,
            TimeDisplay = entity.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Fr),
            IconKind = "AccountPlus",
            IconColor = "#2563EB"
        });

        if (entity.UpdatedAt > entity.CreatedAt.AddMinutes(1))
        {
            items.Add(new UserActivityItem
            {
                Title = "Mise à jour du compte",
                Description = $"Rôle : {RoleLabel(entity.Role)}",
                TimeDisplay = entity.UpdatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Fr),
                IconKind = "AccountEdit",
                IconColor = "#6D28D9"
            });
        }

        var logs = await _db.SystemLogs
            .Where(l => l.UserId == entity.Id || l.Message.Contains(entity.Username))
            .OrderByDescending(l => l.CreatedAt)
            .Take(5)
            .ToListAsync(cancellationToken);

        foreach (var log in logs)
        {
            items.Add(new UserActivityItem
            {
                Title = log.Source,
                Description = log.Message.Length > 60 ? log.Message[..60] + "…" : log.Message,
                TimeDisplay = log.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Fr),
                IconKind = "ClipboardText",
                IconColor = "#64748B"
            });
        }

        return items.OrderByDescending(a => a.TimeDisplay).Take(6).ToList();
    }

    public async Task<IReadOnlyList<UserSessionItem>> LoadSessionsAsync(
        UserListItem user,
        string location,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Users.FirstOrDefaultAsync(u => u.Id == user.Id, cancellationToken);
        if (entity is null || !entity.IsActive) return [];

        var sessions = new List<UserSessionItem>();
        if (entity.LastLoginAt.HasValue)
        {
            sessions.Add(new UserSessionItem
            {
                DeviceLabel = "Windows Desktop",
                ClientInfo = "SBMS Desktop — WPF",
                Location = location,
                StatusLabel = entity.LastLoginAt >= DateTime.UtcNow.AddMinutes(-30) ? "Actif" : "Récent",
                IconKind = "Monitor"
            });
        }

        return sessions;
    }

    public async Task<(bool Ok, string? Error)> CreateUserAsync(
        string username,
        string fullName,
        string email,
        string password,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        username = username.Trim();
        if (string.IsNullOrWhiteSpace(username))
            return (false, "L'identifiant est obligatoire.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            return (false, "Le mot de passe doit contenir au moins 6 caractères.");
        if (await _db.Users.AnyAsync(u => u.Username == username, cancellationToken))
            return (false, "Cet identifiant existe déjà.");

        var user = new User
        {
            Username = username,
            FullName = string.IsNullOrWhiteSpace(fullName) ? username : fullName.Trim(),
            Email = email.Trim(),
            PasswordHash = AuthService.HashPassword(password),
            Role = role,
            IsActive = true
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> UpdateUserAsync(
        Guid userId,
        string fullName,
        string email,
        UserRole role,
        string? newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return (false, "Utilisateur introuvable.");

        if (user.Role == UserRole.Administrateur && role != UserRole.Administrateur)
        {
            var otherAdmins = await _db.Users.CountAsync(
                u => u.Id != userId && u.IsActive && u.Role == UserRole.Administrateur && u.DeletedAt == null,
                cancellationToken);
            if (otherAdmins == 0)
                return (false, "Impossible de retirer le rôle du dernier administrateur actif.");
        }

        user.FullName = string.IsNullOrWhiteSpace(fullName) ? user.Username : fullName.Trim();
        user.Email = email.Trim();
        user.Role = role;
        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            if (newPassword.Length < 6)
                return (false, "Le mot de passe doit contenir au moins 6 caractères.");
            user.PasswordHash = AuthService.HashPassword(newPassword);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> ResetPasswordAsync(
        Guid userId,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            return (false, "Le mot de passe doit contenir au moins 6 caractères.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return (false, "Utilisateur introuvable.");

        user.PasswordHash = AuthService.HashPassword(newPassword);
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> SetUserActiveAsync(
        Guid userId,
        bool isActive,
        Guid? actingUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return (false, "Utilisateur introuvable.");

        if (!isActive && actingUserId == userId)
            return (false, "Vous ne pouvez pas suspendre votre propre compte.");

        if (!isActive && user.Role == UserRole.Administrateur)
        {
            var activeAdmins = await _db.Users.CountAsync(
                u => u.IsActive && u.Role == UserRole.Administrateur && u.DeletedAt == null,
                cancellationToken);
            if (activeAdmins <= 1)
                return (false, "Impossible de suspendre le dernier administrateur actif.");
        }

        user.IsActive = isActive;
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<IReadOnlyList<UserPermissionItem>> LoadPermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var links = await _db.UserPermissions
            .Include(up => up.Permission)
            .Where(up => up.UserId == userId)
            .ToListAsync(cancellationToken);

        if (links.Count > 0)
        {
            return links.Select(l => new UserPermissionItem
            {
                Name = l.Permission.Name,
                Module = l.Permission.Module,
                Code = l.Permission.Code
            }).ToList();
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return [];

        var roleLabel = UserRoleCatalog.ToLabel(user.Role);
        if (!PermissionCodes.RolePermissions.TryGetValue(roleLabel, out var codes) || codes.Length == 0)
            codes = PermissionCodes.RolePermissions.GetValueOrDefault(user.Role.ToString(), []);

        if (codes.Contains("*"))
        {
            var all = await _db.Permissions.ToListAsync(cancellationToken);
            return all.Select(p => new UserPermissionItem
            {
                Name = p.Name,
                Module = p.Module,
                Code = p.Code
            }).ToList();
        }

        var dbPerms = await _db.Permissions
            .Where(p => codes.Contains(p.Code))
            .ToListAsync(cancellationToken);

        return codes.Select(code =>
        {
            var p = dbPerms.FirstOrDefault(x => x.Code == code);
            return new UserPermissionItem
            {
                Name = p?.Name ?? PermissionDisplayName(code),
                Module = p?.Module ?? PermissionModule(code),
                Code = code
            };
        }).ToList();
    }

    private static string PermissionDisplayName(string code) => code switch
    {
        PermissionCodes.VisitorsManage => "Gestion des visites",
        PermissionCodes.DashboardView => "Tableau de bord",
        PermissionCodes.UsersManage => "Gestion des utilisateurs",
        PermissionCodes.FinanceView => "Consultation finances",
        PermissionCodes.FinanceManage => "Gestion finances",
        PermissionCodes.LocationManage => "Gestion locations",
        PermissionCodes.PersonnelView => "Consultation personnel",
        PermissionCodes.PersonnelManage => "Gestion personnel",
        _ => code
    };

    private static string PermissionModule(string code) => code switch
    {
        PermissionCodes.VisitorsManage => "Réception",
        PermissionCodes.DashboardView => "Accueil",
        PermissionCodes.UsersManage or PermissionCodes.SyncManage => "Administration",
        PermissionCodes.FinanceView or PermissionCodes.FinanceManage => "Finances",
        PermissionCodes.LocationManage => "Location",
        PermissionCodes.PersonnelView or PermissionCodes.PersonnelManage => "Personnel",
        _ => "SBMS"
    };

    private static UserListItem MapUser(User u, DateTime onlineThreshold, Guid? currentUserId)
    {
        var role = RoleLabel(u.Role);
        var (roleBg, roleFg) = RoleBadgeColors(u.Role);
        var palette = AvatarPalette(u.FullName);
        var isOnline = u.IsActive && u.LastLoginAt >= onlineThreshold;
        if (currentUserId == u.Id && u.IsActive)
            isOnline = true;

        return new UserListItem
        {
            Id = u.Id,
            Username = u.Username,
            FullName = u.FullName,
            Email = u.Email,
            JobTitle = JobTitle(u.Role),
            RoleLabel = role,
            RoleBadgeBackground = roleBg,
            RoleBadgeForeground = roleFg,
            Department = Department(u.Role),
            StatusLabel = u.IsActive ? "Actif" : "Suspendu",
            StatusDotColor = u.IsActive ? "#22C55E" : "#EF4444",
            IsActive = u.IsActive,
            IsOnline = isOnline,
            LastLoginDisplay = u.LastLoginAt.HasValue
                ? u.LastLoginAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Fr)
                : "Jamais",
            CreatedAtDisplay = u.CreatedAt.ToLocalTime().ToString("dd MMMM yyyy", Fr),
            Phone = "—",
            Initials = GetInitials(u.FullName),
            AvatarBackground = palette.bg,
            AvatarForeground = palette.fg,
            OnlineStatusLabel = isOnline ? "En ligne" : "Hors ligne",
            OnlineStatusColor = isOnline ? "#22C55E" : "#94A3B8"
        };
    }

    private static string RoleLabel(UserRole role) => UserRoleCatalog.ToLabel(role) switch
    {
        "Technique" => "Technicien",
        _ => UserRoleCatalog.ToLabel(role)
    };

    private static string JobTitle(UserRole role) => role switch
    {
        UserRole.Administrateur => "Super Administrateur",
        UserRole.Comptable => "Comptable",
        UserRole.Technique => "Technicien",
        UserRole.Gestionnaire => "Gestionnaire",
        UserRole.Receptionniste => "Réceptionniste",
        _ => "Utilisateur"
    };

    private static string Department(UserRole role) => role switch
    {
        UserRole.Administrateur => "Direction",
        UserRole.Comptable => "Finance",
        UserRole.Technique => "Technique",
        UserRole.Gestionnaire => "Gestion",
        UserRole.Receptionniste => "Accueil",
        _ => "—"
    };

    private static (string bg, string fg) RoleBadgeColors(UserRole role) => role switch
    {
        UserRole.Administrateur => ("#FEE2E2", "#DC2626"),
        UserRole.Gestionnaire => ("#DBEAFE", "#2563EB"),
        UserRole.Comptable => ("#EDE9FE", "#6D28D9"),
        UserRole.Technique => ("#FFEDD5", "#EA580C"),
        UserRole.Receptionniste => ("#D1FAE5", "#059669"),
        _ => ("#F1F5F9", "#475569")
    };

    private static (string bg, string fg) AvatarPalette(string name)
    {
        var hash = Math.Abs(name.GetHashCode());
        var palettes = new[]
        {
            ("#DBEAFE", "#2563EB"),
            ("#DCFCE7", "#166534"),
            ("#EDE9FE", "#6D28D9"),
            ("#FFEDD5", "#EA580C"),
            ("#FEE2E2", "#DC2626")
        };
        return palettes[hash % palettes.Length];
    }

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
            : name.Length >= 2 ? name[..2].ToUpperInvariant() : "??";
    }

    private static string FormatTrend(int current, int previous, string suffix)
    {
        if (previous == 0)
            return current == 0 ? $"0% {suffix}" : $"+100% {suffix}";
        var pct = (current - previous) * 100.0 / previous;
        return $"{(pct >= 0 ? "+" : "")}{pct:0.#}% {suffix}";
    }

    private static List<int> BuildMonthlySparkline(List<User> users, DateTime today)
    {
        var result = new List<int>();
        for (var i = 5; i >= 0; i--)
        {
            var start = new DateTime(today.Year, today.Month, 1).AddMonths(-i);
            var end = start.AddMonths(1);
            result.Add(users.Count(u => u.CreatedAt >= start && u.CreatedAt < end));
        }
        return result;
    }

    private static List<int> BuildWeeklyActiveSparkline(List<User> users, DateTime today)
    {
        var result = new List<int>();
        for (var i = 6; i >= 0; i--)
        {
            var d = today.AddDays(-i);
            result.Add(users.Count(u => u.IsActive && u.LastLoginAt?.Date == d));
        }
        return result;
    }
}

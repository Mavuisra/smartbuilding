using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Auth;
using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Shared.Constants;

namespace SmartBuilding.Infrastructure.Services;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(SmartBuildingDbContext context)
    {
        // Schéma : DesktopDatabaseInitializer (App) — ici uniquement données de référence.

        if (!await context.Permissions.AnyAsync())
        {
            var permissions = new[]
            {
                (PermissionCodes.DashboardView, "Voir tableau de bord", "Dashboard"),
                (PermissionCodes.PersonnelManage, "Gérer personnel", "Personnel"),
                (PermissionCodes.FinanceManage, "Gérer finances", "Finance"),
                (PermissionCodes.TechnicalManage, "Gérer technique", "Technical"),
                (PermissionCodes.LocationManage, "Gérer locations", "Location"),
                (PermissionCodes.EmailManage, "Gérer emails", "Email"),
                (PermissionCodes.UsersManage, "Gérer utilisateurs", "Auth"),
                (PermissionCodes.SyncManage, "Gérer synchronisation", "Sync"),
                (PermissionCodes.ReportsExport, "Exporter rapports", "Reports")
            };

            foreach (var (code, name, module) in permissions)
            {
                context.Permissions.Add(new Permission
                {
                    Code = code,
                    Name = name,
                    Module = module,
                    IsSynced = true
                });
            }
        }

        if (!await context.BuildingInfos.AnyAsync())
        {
            var building = new BuildingInfo
            {
                Name = "Smart Building",
                IsSynced = false
            };
            context.BuildingInfos.Add(building);
        }

        await context.SaveChangesAsync();

        await EnsureBootstrapLocalAdminAsync(context);
        await EnsureAdditionalBootstrapAdministratorsAsync(context);

        // Données de démo uniquement si explicitement demandé (évite de repeupler après une purge).
        if (string.Equals(Environment.GetEnvironmentVariable("SMARTBUILDING_DEMO_DATA"), "true", StringComparison.OrdinalIgnoreCase))
            await SampleDataSeeder.SeedAsync(context);
    }

    /// <summary>
    /// Comptes administrateurs locaux créés automatiquement si absents (mot de passe : Admin@2026).
    /// admin, admini, admin2
    /// </summary>
    private static readonly (string Username, string Email, string FullName)[] AdditionalBootstrapAdministrators =
    {
        ("admin2", "admin2@sbms.local", "Administrateur SBMS")
    };

    public const string BootstrapAdminPassword = "Admin@2026";

    /// <summary>
    /// Compte local par défaut — aucune connexion web requise pour se connecter.
    /// Identifiants : admin / Admin@2026
    /// </summary>
    private static async Task EnsureBootstrapLocalAdminAsync(SmartBuildingDbContext context)
    {
        var hasActiveUser = await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.DeletedAt == null);

        if (!hasActiveUser)
        {
            context.Users.Add(new User
            {
                Username = "admin",
                Email = "admin@sbms.local",
                FullName = "Administrateur",
                Role = UserRole.Administrateur,
                PasswordHash = AuthService.HashPassword(BootstrapAdminPassword),
                IsActive = true,
                IsSynced = false
            });
            await context.SaveChangesAsync();
            return;
        }

        var legacy = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u =>
                u.DeletedAt == null
                && u.Username.ToLower() == "admin"
                && u.Email.ToLower() == "admin@smartbuilding.local");

        if (legacy is not null)
        {
            legacy.Email = "admin@sbms.local";
            legacy.PasswordHash = AuthService.HashPassword(BootstrapAdminPassword);
            legacy.IsActive = true;
            legacy.MarkUpdated();
        }

        await EnsureReservedAdminAccountsAsync(context);
    }

    private static async Task EnsureAdditionalBootstrapAdministratorsAsync(SmartBuildingDbContext context)
    {
        var added = false;
        foreach (var (username, email, fullName) in AdditionalBootstrapAdministrators)
        {
            var exists = await context.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => u.DeletedAt == null && u.Username.ToLower() == username);

            if (exists)
                continue;

            context.Users.Add(new User
            {
                Username = username,
                Email = email,
                FullName = fullName,
                Role = UserRole.Administrateur,
                PasswordHash = AuthService.HashPassword(BootstrapAdminPassword),
                IsActive = true,
                IsSynced = false
            });
            added = true;
        }

        if (added)
            await context.SaveChangesAsync();

        await EnsureReservedAdminAccountsAsync(context);
    }

    /// <summary>Identifiants réservés qui doivent toujours être Administrateur.</summary>
    public static bool IsReservedAdminUsername(string? username) =>
        !string.IsNullOrWhiteSpace(username)
        && ReservedAdminUsernames.Contains(username.Trim().ToLowerInvariant());

    private static readonly HashSet<string> ReservedAdminUsernames =
        new(StringComparer.OrdinalIgnoreCase) { "admin", "admini", "admin2" };

    /// <summary>
    /// Les comptes « admin » et « admini » doivent toujours être Administrateur (accès complet).
    /// Corrige les bases existantes après sync ou création avec un mauvais rôle (ex. Réceptionniste).
    /// </summary>
    public static async Task EnsureReservedAdminAccountsAsync(
        SmartBuildingDbContext context,
        CancellationToken cancellationToken = default)
    {
        var admins = await context.Users
            .IgnoreQueryFilters()
            .Where(u => u.DeletedAt == null)
            .ToListAsync(cancellationToken);

        admins = admins
            .Where(u => IsReservedAdminUsername(u.Username))
            .ToList();

        if (admins.Count == 0)
            return;

        var changed = false;
        foreach (var user in admins)
        {
            if (user.Role != UserRole.Administrateur)
            {
                user.Role = UserRole.Administrateur;
                changed = true;
            }

            if (!user.IsActive)
            {
                user.IsActive = true;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(user.FullName)
                || user.FullName.Equals(user.Username, StringComparison.OrdinalIgnoreCase))
            {
                user.FullName = "Administrateur";
                changed = true;
            }
        }

        if (!changed)
            return;

        foreach (var user in admins)
            user.MarkUpdated();

        await context.SaveChangesAsync(cancellationToken);
    }
}

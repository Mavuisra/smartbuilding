using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Auth;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Shared.Constants;

namespace SmartBuilding.Infrastructure.Services;

public static class DatabaseSeeder
{
    /// <summary>Données de référence uniquement (permissions). Pas de comptes ni de bâtiment — l'assistant s'en charge.</summary>
    public static async Task SeedAsync(SmartBuildingDbContext context)
    {
        await SeedReferenceDataAsync(context);

        if (string.Equals(Environment.GetEnvironmentVariable("SMARTBUILDING_DEMO_DATA"), "true", StringComparison.OrdinalIgnoreCase))
            await SampleDataSeeder.SeedAsync(context);
    }

    public static async Task SeedReferenceDataAsync(SmartBuildingDbContext context)
    {
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

            await context.SaveChangesAsync();
        }
    }

    public const string BootstrapAdminPassword = "Admin@2026";

    /// <summary>Identifiants réservés qui doivent toujours être Administrateur.</summary>
    public static bool IsReservedAdminUsername(string? username) =>
        !string.IsNullOrWhiteSpace(username)
        && ReservedAdminUsernames.Contains(username.Trim().ToLowerInvariant());

    private static readonly HashSet<string> ReservedAdminUsernames =
        new(StringComparer.OrdinalIgnoreCase) { "admin", "admini" };

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

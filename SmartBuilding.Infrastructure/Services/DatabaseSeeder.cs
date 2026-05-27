using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Auth;
using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Shared.Constants;

namespace SmartBuilding.Infrastructure.Services;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(SmartBuildingDbContext context)
    {
        await context.Database.EnsureCreatedAsync();
        await DatabaseSchemaUpgrader.UpgradeAsync(context);

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

        // Données de démo uniquement si explicitement demandé (évite de repeupler après une purge).
        if (string.Equals(Environment.GetEnvironmentVariable("SMARTBUILDING_DEMO_DATA"), "true", StringComparison.OrdinalIgnoreCase))
            await SampleDataSeeder.SeedAsync(context);
    }
}

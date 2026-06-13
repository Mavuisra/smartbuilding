using Microsoft.EntityFrameworkCore;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Sync;

namespace SmartBuilding.Infrastructure.Services;

/// <summary>Réinitialise la base MySQL locale (serveur ou poste autonome).</summary>
public static class DesktopDatabaseResetService
{
    private static readonly string SetupFlagPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SBMS",
        "setup-completed.flag");

    private static readonly string ApiTokenPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SBMS",
        "api-token.txt");

    public static async Task ResetLocalDatabaseAsync(
        SmartBuildingDbContext activeContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activeContext);

        if (!DesktopDatabaseInitializer.IsMySqlProvider(activeContext))
        {
            throw new InvalidOperationException(
                "La réinitialisation locale ne concerne que MySQL (XAMPP).");
        }

        await activeContext.Database.CloseConnectionAsync();
        await activeContext.Database.EnsureDeletedAsync(cancellationToken);
        await activeContext.Database.MigrateAsync(cancellationToken);
        await DatabaseSeeder.SeedAsync(activeContext);

        DeleteIfExists(SetupFlagPath);
        DeleteIfExists(ApiTokenPath);
        CloudIdentityStore.Clear();
        InitialSyncStore.Clear();
        SyncCloudTokenStore.Clear();
        SyncPullConflictStore.Clear();
    }

    private static void DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }
}

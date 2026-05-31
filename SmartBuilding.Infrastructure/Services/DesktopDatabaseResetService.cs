using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartBuilding.Infrastructure.Persistence;

namespace SmartBuilding.Infrastructure.Services;

/// <summary>
/// Réinitialise la base SQLite locale du Desktop (hors dossier d'installation).
/// </summary>
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
        SmartBuildingDbContext? activeContext = null,
        CancellationToken cancellationToken = default)
    {
        if (activeContext is not null)
        {
            await activeContext.Database.CloseConnectionAsync();
        }

        SqliteConnection.ClearAllPools();

        var dbPath = DesktopSqlitePaths.DatabaseFilePath;
        DeleteIfExists(dbPath);
        DeleteIfExists(dbPath + "-wal");
        DeleteIfExists(dbPath + "-shm");
        DeleteIfExists(SetupFlagPath);
        DeleteIfExists(ApiTokenPath);

        DesktopSqlitePaths.ResetInitializationState();
        DesktopSqlitePaths.EnsureInitialized();

        await using var fresh = new SmartBuildingDbContext(
            new DbContextOptionsBuilder<SmartBuildingDbContext>()
                .UseSqlite(DesktopSqlitePaths.ConnectionString)
                .Options);

        await DatabaseSeeder.SeedAsync(fresh);
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

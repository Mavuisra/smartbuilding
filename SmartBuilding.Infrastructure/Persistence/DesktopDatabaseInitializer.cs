using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartBuilding.Infrastructure.Services;

namespace SmartBuilding.Infrastructure.Persistence;

/// <summary>Applique le schéma local : migrations EF MySQL (XAMPP).</summary>
public static class DesktopDatabaseInitializer
{
    public static async Task InitializeAsync(
        SmartBuildingDbContext context,
        DesktopLocalDatabaseConfig localDb,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        if (!localDb.IsMySql)
        {
            throw new InvalidOperationException(
                "SBMS desktop utilise uniquement MySQL (XAMPP). Vérifiez LocalDatabase:DeploymentMode et démarrez MySQL.");
        }

        await InitializeMySqlAsync(context, localDb, logger, cancellationToken);
    }

    private static async Task InitializeMySqlAsync(
        SmartBuildingDbContext context,
        DesktopLocalDatabaseConfig localDb,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        if (localDb.RunsSchemaMigrations)
        {
            DesktopLocalDatabaseBootstrap.EnsureMySqlDatabaseExists(localDb.ConnectionString);

            var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pending.Count == 0)
            {
                logger?.LogDebug("MySQL : schéma à jour, aucune migration en attente.");
                return;
            }

            if (await LegacyEnsureCreatedSchemaExistsAsync(context, cancellationToken)
                && (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList().Count == 0)
            {
                logger?.LogWarning(
                    "MySQL : base créée sans migrations EF — application des migrations (peut nécessiter une base vide).");
            }

            logger?.LogInformation(
                "MySQL : application de {Count} migration(s) : {Names}",
                pending.Count,
                string.Join(", ", pending));

            await context.Database.MigrateAsync(cancellationToken);
            return;
        }

        logger?.LogInformation(
            "MySQL client : connexion au serveur {Host} (migrations gérées sur le PC serveur uniquement).",
            localDb.ServerHost);

        if (!await context.Database.CanConnectAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                $"Impossible d'accéder à la base sur le serveur {localDb.ServerHost}.");
        }
    }

    /// <summary>Détecte une base créée par l'ancien EnsureCreated (tables sans __EFMigrationsHistory).</summary>
    private static async Task<bool> LegacyEnsureCreatedSchemaExistsAsync(
        SmartBuildingDbContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var connection = context.Database.GetDbConnection();
            var wasOpen = connection.State == System.Data.ConnectionState.Open;
            if (!wasOpen)
                await connection.OpenAsync(cancellationToken);

            try
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                    SELECT COUNT(*) FROM information_schema.tables
                    WHERE table_schema = DATABASE() AND table_name = 'Users'
                    """;
                var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
                return count > 0;
            }
            finally
            {
                if (!wasOpen)
                    await connection.CloseAsync();
            }
        }
        catch
        {
            return false;
        }
    }

    public static bool IsMySqlProvider(SmartBuildingDbContext context) =>
        context.Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true;
}

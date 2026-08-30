using System.Data.Common;
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
            var applied = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();

            if (pending.Count == 0)
            {
                logger?.LogDebug("MySQL : schéma à jour, aucune migration en attente.");
                await EnsureMySqlSchemaPatchesAsync(context, cancellationToken);
                return;
            }

            if (applied.Count == 0
                && await LegacyEnsureCreatedSchemaExistsAsync(context, cancellationToken))
            {
                logger?.LogWarning(
                    "MySQL : schéma existant sans historique EF — enregistrement des migrations sans recréer les tables.");
                await BaselineAppliedMigrationsAsync(context, pending, logger, cancellationToken);
                await EnsureMySqlSchemaPatchesAsync(context, cancellationToken);
                return;
            }

            logger?.LogInformation(
                "MySQL : application de {Count} migration(s) : {Names}",
                pending.Count,
                string.Join(", ", pending));

            await context.Database.MigrateAsync(cancellationToken);
            await EnsureMySqlSchemaPatchesAsync(context, cancellationToken);
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

        await EnsureMySqlSchemaPatchesAsync(context, cancellationToken);
    }

    /// <summary>Correctifs idempotents pour colonnes ajoutées hors migration ou base déjà en production.</summary>
    private static async Task EnsureMySqlSchemaPatchesAsync(
        SmartBuildingDbContext context,
        CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider(context))
            return;

        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await EnsureMySqlColumnAsync(connection, "ConsumptionRecords", "CustomTypeLabel", "longtext NULL", cancellationToken);
            await EnsureMySqlColumnAsync(connection, "ConsumptionRecords", "ExpenseMotif", "longtext NULL", cancellationToken);
            await EnsureMySqlColumnAsync(connection, "ConsumptionRecords", "PaidBy", "longtext NOT NULL DEFAULT ''", cancellationToken);
            await EnsureMySqlColumnAsync(connection, "ConsumptionRecords", "ReimbursementStatus", "longtext NOT NULL DEFAULT 'Non applicable'", cancellationToken);
            await EnsureMySqlColumnAsync(connection, "Incidents", "EquipmentId", "char(36) NULL", cancellationToken);
        }
        finally
        {
            if (!wasOpen)
                await connection.CloseAsync();
        }
    }

    private static async Task EnsureMySqlColumnAsync(
        DbConnection connection,
        string table,
        string column,
        string columnDefinition,
        CancellationToken cancellationToken)
    {
        await using var check = connection.CreateCommand();
        check.CommandText = """
            SELECT COUNT(*) FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND table_name = @table
              AND column_name = @column
            """;
        var tableParam = check.CreateParameter();
        tableParam.ParameterName = "@table";
        tableParam.Value = table;
        check.Parameters.Add(tableParam);
        var columnParam = check.CreateParameter();
        columnParam.ParameterName = "@column";
        columnParam.Value = column;
        check.Parameters.Add(columnParam);

        var exists = Convert.ToInt32(await check.ExecuteScalarAsync(cancellationToken)) > 0;
        if (exists)
            return;

        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE `{table}` ADD COLUMN `{column}` {columnDefinition}";
        await alter.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task BaselineAppliedMigrationsAsync(
        SmartBuildingDbContext context,
        IReadOnlyList<string> pendingMigrations,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        if (pendingMigrations.Count == 0)
            return;

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
                `MigrationId` varchar(150) NOT NULL,
                `ProductVersion` varchar(32) NOT NULL,
                PRIMARY KEY (`MigrationId`)
            ) ENGINE=InnoDB;
            """,
            cancellationToken);

        var productVersion = typeof(DesktopDatabaseInitializer).Assembly.GetName().Version?.ToString(3) ?? "8.0.0";

        foreach (var migrationId in pendingMigrations)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
                 VALUES ({migrationId}, {productVersion});
                 """,
                cancellationToken);
            logger?.LogDebug("MySQL : migration baselinée {MigrationId}", migrationId);
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
                    WHERE table_schema = DATABASE()
                      AND table_name IN ('Users', 'BuildingInfos')
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

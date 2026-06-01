using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace SmartBuilding.Infrastructure.Persistence;

/// <summary>
/// Résout la connexion base : serveur unique (MySQL sur PC admin) ou poste autonome.
/// </summary>
public static class DesktopLocalDatabaseBootstrap
{
    public const string DefaultMySqlConnectionString =
        "Server=127.0.0.1;Port=3306;Database=sbms_local;User=root;Password=;CharSet=utf8mb4;";

    public static DesktopLocalDatabaseConfig Resolve(IConfiguration configuration)
    {
        var section = configuration.GetSection(DesktopLocalDatabaseConfig.SectionName);
        var modeRaw = section.GetValue<string>("DeploymentMode") ?? nameof(DesktopDatabaseDeploymentMode.Standalone);
        if (!Enum.TryParse<DesktopDatabaseDeploymentMode>(modeRaw, ignoreCase: true, out var deploymentMode))
            deploymentMode = DesktopDatabaseDeploymentMode.Standalone;

        return deploymentMode switch
        {
            DesktopDatabaseDeploymentMode.Server => ResolveServer(section),
            DesktopDatabaseDeploymentMode.Client => ResolveClient(section),
            _ => ResolveStandalone(section)
        };
    }

    /// <summary>PC serveur : MySQL local, une seule base sbms_local pour tout le site.</summary>
    private static DesktopLocalDatabaseConfig ResolveServer(IConfigurationSection section)
    {
        var connectionString = DesktopMySqlConnectionBuilder.Build(section, "127.0.0.1");
        EnsureMySqlDatabaseExists(connectionString);

        if (!CanConnectToMySql(connectionString))
        {
            throw new InvalidOperationException(
                "Mode Serveur : impossible de joindre MySQL sur ce PC. Démarrez MySQL dans XAMPP.");
        }

        return new DesktopLocalDatabaseConfig
        {
            Provider = DesktopLocalDatabaseProvider.MySql,
            ConnectionString = connectionString,
            DisplayLabel = "MySQL serveur (base unique)",
            DeploymentMode = DesktopDatabaseDeploymentMode.Server,
            ServerHost = "127.0.0.1",
            AutoFallbackToSqlite = false,
            RunsSchemaMigrations = true
        };
    }

    /// <summary>PC client : connexion à la base du serveur (pas de base locale).</summary>
    private static DesktopLocalDatabaseConfig ResolveClient(IConfigurationSection section)
    {
        var serverHost = section.GetValue<string>("ServerHost")?.Trim();
        if (string.IsNullOrWhiteSpace(serverHost))
        {
            throw new InvalidOperationException(
                "Mode Client : renseignez LocalDatabase:ServerHost avec l'adresse IP du PC serveur (ex. 192.168.1.10).");
        }

        var connectionString = DesktopMySqlConnectionBuilder.Build(section, serverHost);

        if (!CanConnectToMySql(connectionString))
        {
            throw new InvalidOperationException(
                $"Mode Client : impossible de joindre MySQL sur {serverHost}. " +
                "Vérifiez : MySQL démarré sur le serveur, IP correcte, pare-feu port 3306, utilisateur MySQL autorisé depuis le réseau.");
        }

        return new DesktopLocalDatabaseConfig
        {
            Provider = DesktopLocalDatabaseProvider.MySql,
            ConnectionString = connectionString,
            DisplayLabel = $"MySQL client → {serverHost}",
            DeploymentMode = DesktopDatabaseDeploymentMode.Client,
            ServerHost = serverHost,
            AutoFallbackToSqlite = false,
            RunsSchemaMigrations = false
        };
    }

    private static DesktopLocalDatabaseConfig ResolveStandalone(IConfigurationSection section)
    {
        var providerRaw = section.GetValue<string>("Provider") ?? "Auto";
        var autoFallback = section.GetValue("AutoFallbackToSqlite", true);
        var mySqlCs = section.GetValue<string>("MySql") ?? DefaultMySqlConnectionString;

        if (!Enum.TryParse<DesktopLocalDatabaseProvider>(providerRaw, ignoreCase: true, out var provider))
            provider = DesktopLocalDatabaseProvider.Auto;

        if (provider == DesktopLocalDatabaseProvider.Auto)
        {
            if (CanConnectToMySql(mySqlCs))
            {
                EnsureMySqlDatabaseExists(mySqlCs);
                return new DesktopLocalDatabaseConfig
                {
                    Provider = DesktopLocalDatabaseProvider.MySql,
                    ConnectionString = mySqlCs,
                    DisplayLabel = "MySQL (XAMPP)",
                    DeploymentMode = DesktopDatabaseDeploymentMode.Standalone,
                    AutoFallbackToSqlite = autoFallback,
                    RunsSchemaMigrations = true
                };
            }

            if (!autoFallback)
            {
                throw new InvalidOperationException(
                    "MySQL (XAMPP) est indisponible. Démarrez MySQL dans le panneau XAMPP ou définissez LocalDatabase:Provider=Sqlite.");
            }

            DesktopSqlitePaths.EnsureInitialized();
            return new DesktopLocalDatabaseConfig
            {
                Provider = DesktopLocalDatabaseProvider.Sqlite,
                ConnectionString = DesktopSqlitePaths.ConnectionString,
                DisplayLabel = "SQLite (secours)",
                DeploymentMode = DesktopDatabaseDeploymentMode.Standalone,
                AutoFallbackToSqlite = true,
                RunsSchemaMigrations = false
            };
        }

        if (provider == DesktopLocalDatabaseProvider.MySql)
        {
            EnsureMySqlDatabaseExists(mySqlCs);
            if (!CanConnectToMySql(mySqlCs))
            {
                throw new InvalidOperationException(
                    "Impossible de se connecter à MySQL. Vérifiez que XAMPP / MySQL est démarré et la chaîne LocalDatabase:MySql.");
            }

            return new DesktopLocalDatabaseConfig
            {
                Provider = DesktopLocalDatabaseProvider.MySql,
                ConnectionString = mySqlCs,
                DisplayLabel = "MySQL (XAMPP)",
                DeploymentMode = DesktopDatabaseDeploymentMode.Standalone,
                AutoFallbackToSqlite = autoFallback,
                RunsSchemaMigrations = true
            };
        }

        DesktopSqlitePaths.EnsureInitialized();
        return new DesktopLocalDatabaseConfig
        {
            Provider = DesktopLocalDatabaseProvider.Sqlite,
            ConnectionString = DesktopSqlitePaths.ConnectionString,
            DisplayLabel = "SQLite",
            DeploymentMode = DesktopDatabaseDeploymentMode.Standalone,
            AutoFallbackToSqlite = autoFallback,
            RunsSchemaMigrations = false
        };
    }

    public static bool CanConnectToMySql(string connectionString)
    {
        try
        {
            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void EnsureMySqlDatabaseExists(string connectionString)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString);
        var databaseName = builder.Database;
        if (string.IsNullOrWhiteSpace(databaseName))
            return;

        builder.Database = "";
        using var connection = new MySqlConnection(builder.ConnectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            $"CREATE DATABASE IF NOT EXISTS `{databaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
        cmd.ExecuteNonQuery();
    }
}

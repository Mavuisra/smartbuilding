using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace SmartBuilding.Infrastructure.Persistence;

/// <summary>
/// Résout la connexion MySQL (XAMPP) : serveur unique, poste client ou poste autonome.
/// </summary>
public static class DesktopLocalDatabaseBootstrap
{
    public const string DefaultMySqlConnectionString =
        "Server=127.0.0.1;Port=3306;Database=sbms_local;User=root;Password=;CharSet=utf8mb4;";

    public static DesktopLocalDatabaseConfig Resolve(IConfiguration configuration)
    {
        var section = configuration.GetSection(DesktopLocalDatabaseConfig.SectionName);
        var modeRaw = section.GetValue<string>("DeploymentMode") ?? nameof(DesktopDatabaseDeploymentMode.Server);
        if (!Enum.TryParse<DesktopDatabaseDeploymentMode>(modeRaw, ignoreCase: true, out var deploymentMode))
            deploymentMode = DesktopDatabaseDeploymentMode.Server;

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
        return BuildMySqlConfig(
            section,
            connectionString,
            "MySQL serveur (base unique)",
            DesktopDatabaseDeploymentMode.Server,
            "127.0.0.1",
            runsSchemaMigrations: true);
    }

    /// <summary>PC client : connexion à la base du serveur (découverte auto IP sur le LAN).</summary>
    private static DesktopLocalDatabaseConfig ResolveClient(IConfigurationSection section)
    {
        var configuredHost = section.GetValue<string>("ServerHost")?.Trim();
        var serverHost = DesktopMySqlServerDiscovery.ResolveClientHost(section, configuredHost);

        if (serverHost is null)
        {
            var placeholderHost = string.IsNullOrWhiteSpace(configuredHost) ? "127.0.0.1" : configuredHost;
            var pendingCs = DesktopMySqlConnectionBuilder.Build(section, placeholderHost);
            return new DesktopLocalDatabaseConfig
            {
                Provider = DesktopLocalDatabaseProvider.MySql,
                ConnectionString = pendingCs,
                DisplayLabel = "Poste client — connexion MySQL requise",
                DeploymentMode = DesktopDatabaseDeploymentMode.Client,
                ServerHost = configuredHost,
                RunsSchemaMigrations = false,
                RequiresClientDatabaseConnection = true
            };
        }

        var connectionString = DesktopMySqlConnectionBuilder.Build(section, serverHost);
        return BuildMySqlConfig(
            section,
            connectionString,
            $"MySQL client → {serverHost}",
            DesktopDatabaseDeploymentMode.Client,
            serverHost,
            runsSchemaMigrations: false);
    }

    private static DesktopLocalDatabaseConfig ResolveStandalone(IConfigurationSection section)
    {
        var connectionString = DesktopMySqlConnectionBuilder.Build(section, "127.0.0.1");
        return BuildMySqlConfig(
            section,
            connectionString,
            "MySQL (XAMPP)",
            DesktopDatabaseDeploymentMode.Standalone,
            "127.0.0.1",
            runsSchemaMigrations: true);
    }

    private static DesktopLocalDatabaseConfig BuildMySqlConfig(
        IConfigurationSection section,
        string connectionString,
        string displayLabel,
        DesktopDatabaseDeploymentMode deploymentMode,
        string serverHost,
        bool runsSchemaMigrations)
    {
        EnsureMySqlDatabaseExists(connectionString);
        EnsureNetworkClientUser(section, connectionString, deploymentMode, serverHost);

        if (!CanConnectToMySql(connectionString))
        {
            var hint = deploymentMode == DesktopDatabaseDeploymentMode.Client
                ? $"Impossible de joindre MySQL sur {serverHost}. Vérifiez XAMPP sur le serveur, l'IP et le pare-feu (port 3306)."
                : "Impossible de joindre MySQL sur ce PC. Démarrez MySQL dans XAMPP.";

            throw new InvalidOperationException(hint);
        }

        return new DesktopLocalDatabaseConfig
        {
            Provider = DesktopLocalDatabaseProvider.MySql,
            ConnectionString = connectionString,
            DisplayLabel = displayLabel,
            DeploymentMode = deploymentMode,
            ServerHost = serverHost,
            RunsSchemaMigrations = runsSchemaMigrations,
            RequiresClientDatabaseConnection = false
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

    private static void EnsureNetworkClientUser(
        IConfigurationSection section,
        string adminConnectionString,
        DesktopDatabaseDeploymentMode deploymentMode,
        string serverHost)
    {
        if (deploymentMode == DesktopDatabaseDeploymentMode.Client)
            return;

        if (!string.Equals(serverHost, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(serverHost, "localhost", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            var user = section.GetValue<string>("NetworkUser")?.Trim();
            if (string.IsNullOrWhiteSpace(user))
                user = "sbms";

            var password = section.GetValue<string>("NetworkPassword");
            if (string.IsNullOrWhiteSpace(password))
                password = "Sbms@2026!";

            var builder = new MySqlConnectionStringBuilder(adminConnectionString);
            var databaseName = builder.Database;
            if (string.IsNullOrWhiteSpace(databaseName))
                return;

            var userSql = EscapeSqlLiteral(user);
            var passwordSql = EscapeSqlLiteral(password);
            var dbSql = EscapeSqlIdentifier(databaseName);

            builder.Database = "";
            using var connection = new MySqlConnection(builder.ConnectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
CREATE USER IF NOT EXISTS '{userSql}'@'%' IDENTIFIED BY '{passwordSql}';
ALTER USER '{userSql}'@'%' IDENTIFIED BY '{passwordSql}';
GRANT ALL PRIVILEGES ON `{dbSql}`.* TO '{userSql}'@'%';
FLUSH PRIVILEGES;";
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Ne bloque pas le démarrage si l'utilisateur courant n'a pas les droits CREATE USER/GRANT.
            // L'application peut continuer avec l'utilisateur admin local.
        }
    }

    private static string EscapeSqlLiteral(string value) => value.Replace("'", "''");
    private static string EscapeSqlIdentifier(string value) => value.Replace("`", "``");
}

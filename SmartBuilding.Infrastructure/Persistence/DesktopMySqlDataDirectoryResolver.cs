using MySqlConnector;
using SmartBuilding.Infrastructure.Services;

namespace SmartBuilding.Infrastructure.Persistence;

/// <summary>Résout le dossier physique MySQL (datadir ou base sbms_local).</summary>
public static class DesktopMySqlDataDirectoryResolver
{
    public static string? TryResolve(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return null;

        var databaseName = ParseDatabaseName(connectionString);

        var fromServer = TryQueryDataDirectory(connectionString, databaseName);
        if (fromServer is not null)
            return fromServer;

        return TryResolveFromXampp(databaseName);
    }

    public static bool IsLocalServer(string connectionString)
    {
        try
        {
            var host = new MySqlConnectionStringBuilder(connectionString).Server?.Trim() ?? string.Empty;
            return host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
                   || host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string? TryQueryDataDirectory(string connectionString, string? databaseName)
    {
        try
        {
            var builder = new MySqlConnectionStringBuilder(connectionString) { Database = "" };
            using var connection = new MySqlConnection(builder.ConnectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT @@datadir";
            var datadir = cmd.ExecuteScalar()?.ToString()?.Trim();
            return NormalizeDirectory(datadir, databaseName);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryResolveFromXampp(string? databaseName)
    {
        var xampp = DesktopPrerequisiteChecker.FindXamppRoot();
        if (xampp is null)
            return null;

        return NormalizeDirectory(Path.Combine(xampp, "mysql", "data"), databaseName);
    }

    private static string? NormalizeDirectory(string? directory, string? databaseName)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return null;

        var datadir = directory
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimEnd(Path.DirectorySeparatorChar);

        if (!string.IsNullOrWhiteSpace(databaseName))
        {
            var dbFolder = Path.Combine(datadir, databaseName);
            if (Directory.Exists(dbFolder))
                return dbFolder;
        }

        return Directory.Exists(datadir) ? datadir : null;
    }

    private static string? ParseDatabaseName(string connectionString)
    {
        try
        {
            return new MySqlConnectionStringBuilder(connectionString).Database;
        }
        catch
        {
            return null;
        }
    }
}

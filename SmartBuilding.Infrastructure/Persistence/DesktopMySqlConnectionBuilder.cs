using Microsoft.Extensions.Configuration;

namespace SmartBuilding.Infrastructure.Persistence;

public static class DesktopMySqlConnectionBuilder
{
    public const string DefaultDatabase = "sbms_local";
    public const int DefaultPort = 3306;

    public static string Build(IConfigurationSection section, string serverHost)
    {
        var explicitCs = section.GetValue<string>("MySql");
        if (!string.IsNullOrWhiteSpace(explicitCs) && !explicitCs.Contains("REMPLACER", StringComparison.OrdinalIgnoreCase))
        {
            return ReplaceServerHost(explicitCs, serverHost);
        }

        var port = section.GetValue<int?>("MySqlPort") ?? DefaultPort;
        var database = section.GetValue<string>("Database") ?? DefaultDatabase;
        var user = section.GetValue<string>("User") ?? "root";
        var password = section.GetValue<string>("Password") ?? "";

        return
            $"Server={serverHost};Port={port};Database={database};User={user};Password={password};CharSet=utf8mb4;";
    }

    private static string ReplaceServerHost(string connectionString, string serverHost)
    {
        var builder = new MySqlConnector.MySqlConnectionStringBuilder(connectionString)
        {
            Server = serverHost
        };
        return builder.ConnectionString;
    }
}

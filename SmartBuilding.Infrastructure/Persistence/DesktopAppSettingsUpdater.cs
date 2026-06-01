using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;

namespace SmartBuilding.Infrastructure.Persistence;

/// <summary>Met à jour appsettings.json à côté de l'exe (ex. IP serveur découverte).</summary>
public static class DesktopAppSettingsUpdater
{
    public static string AppSettingsPath =>
        Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    public static bool TryUpdateServerHost(string serverHost)
    {
        if (string.IsNullOrWhiteSpace(serverHost))
            return false;

        try
        {
            var path = AppSettingsPath;
            if (!File.Exists(path))
                return false;

            var text = File.ReadAllText(path);
            var root = JsonNode.Parse(text)?.AsObject();
            if (root is null)
                return false;

            var localDb = root[DesktopLocalDatabaseConfig.SectionName] as JsonObject
                            ?? new JsonObject();
            localDb["ServerHost"] = serverHost.Trim();
            root[DesktopLocalDatabaseConfig.SectionName] = localDb;

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, root.ToJsonString(options));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static IConfigurationSection GetLocalDatabaseSection(IConfiguration configuration) =>
        configuration.GetSection(DesktopLocalDatabaseConfig.SectionName);
}

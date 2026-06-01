using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SmartBuilding.Desktop.WPF.Services;

/// <summary>Persiste la section LocalDatabase dans appsettings.json à côté de l'exe.</summary>
public static class DesktopAppSettingsWriter
{
    public static string AppSettingsPath =>
        Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    public static void SaveLocalDatabase(LocalDatabaseSetupSettings settings)
    {
        var path = AppSettingsPath;
        JsonObject root;

        if (File.Exists(path))
        {
            var text = File.ReadAllText(path);
            root = JsonNode.Parse(text)?.AsObject() ?? new JsonObject();
        }
        else
        {
            root = new JsonObject();
        }

        var localDb = new JsonObject
        {
            ["DeploymentMode"] = settings.DeploymentMode,
            ["Database"] = settings.Database,
            ["MySqlPort"] = settings.MySqlPort,
            ["User"] = settings.User,
            ["Password"] = settings.Password,
            ["AutoFallbackToSqlite"] = false
        };

        if (string.Equals(settings.DeploymentMode, "Client", StringComparison.OrdinalIgnoreCase))
            localDb["ServerHost"] = settings.ServerHost ?? "";

        root["LocalDatabase"] = localDb;

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(path, root.ToJsonString(options));
    }
}

public sealed class LocalDatabaseSetupSettings
{
    public string DeploymentMode { get; init; } = "Server";
    public string? ServerHost { get; init; }
    public string Database { get; init; } = "sbms_local";
    public int MySqlPort { get; init; } = 3306;
    public string User { get; init; } = "root";
    public string Password { get; init; } = "";
}

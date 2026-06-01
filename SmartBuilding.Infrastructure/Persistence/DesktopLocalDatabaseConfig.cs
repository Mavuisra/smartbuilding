namespace SmartBuilding.Infrastructure.Persistence;

/// <summary>Configuration de la base locale desktop (SQLite fichier ou MySQL XAMPP).</summary>
public sealed class DesktopLocalDatabaseConfig
{
    public const string SectionName = "LocalDatabase";

    public DesktopLocalDatabaseProvider Provider { get; init; }
    public string ConnectionString { get; init; } = string.Empty;
    public string DisplayLabel { get; init; } = "SQLite";
    public bool AutoFallbackToSqlite { get; init; } = true;
    public DesktopDatabaseDeploymentMode DeploymentMode { get; init; } = DesktopDatabaseDeploymentMode.Standalone;
    public string? ServerHost { get; init; }

    /// <summary>Seul le PC serveur applique les migrations EF (création / évolution des tables).</summary>
    public bool RunsSchemaMigrations { get; init; } = true;

    public bool IsMySql => Provider == DesktopLocalDatabaseProvider.MySql;
    public bool IsSqlite => Provider == DesktopLocalDatabaseProvider.Sqlite;
    public bool IsCentralServer => DeploymentMode == DesktopDatabaseDeploymentMode.Server;
    public bool IsLanClient => DeploymentMode == DesktopDatabaseDeploymentMode.Client;
}

public enum DesktopLocalDatabaseProvider
{
    Sqlite,
    MySql,
    Auto
}

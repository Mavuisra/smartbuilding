namespace SmartBuilding.Infrastructure.Persistence;

/// <summary>Configuration de la base locale desktop (MySQL / XAMPP uniquement).</summary>
public sealed class DesktopLocalDatabaseConfig
{
    public const string SectionName = "LocalDatabase";

    public DesktopLocalDatabaseProvider Provider { get; init; } = DesktopLocalDatabaseProvider.MySql;
    public string ConnectionString { get; init; } = string.Empty;
    public string DisplayLabel { get; init; } = "MySQL";
    public DesktopDatabaseDeploymentMode DeploymentMode { get; init; } = DesktopDatabaseDeploymentMode.Server;
    public string? ServerHost { get; init; }

    /// <summary>Seul le PC serveur applique les migrations EF (création / évolution des tables).</summary>
    public bool RunsSchemaMigrations { get; init; } = true;

    public bool IsMySql => Provider == DesktopLocalDatabaseProvider.MySql;
    public bool IsCentralServer => DeploymentMode == DesktopDatabaseDeploymentMode.Server;
    public bool IsLanClient => DeploymentMode == DesktopDatabaseDeploymentMode.Client;

    /// <summary>Poste client : MySQL injoignable — l'assistant doit configurer la connexion.</summary>
    public bool RequiresClientDatabaseConnection { get; init; }
}

public enum DesktopLocalDatabaseProvider
{
    MySql
}

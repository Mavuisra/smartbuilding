namespace SmartBuilding.Infrastructure.Persistence;

/// <summary>
/// Standalone = une base par PC (+ sync cloud optionnelle).
/// Server = une seule base MySQL sur CE PC (XAMPP).
/// Client = ce PC se connecte à la base du PC serveur (réseau local).
/// </summary>
public enum DesktopDatabaseDeploymentMode
{
    Standalone,
    Server,
    Client
}

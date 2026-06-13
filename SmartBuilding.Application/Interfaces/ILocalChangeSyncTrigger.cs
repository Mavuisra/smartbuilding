namespace SmartBuilding.Application.Interfaces;

/// <summary>
/// Déclenche une synchronisation cloud après des modifications locales (CRUD).
/// </summary>
public interface ILocalChangeSyncTrigger
{
    void RequestPush();
}

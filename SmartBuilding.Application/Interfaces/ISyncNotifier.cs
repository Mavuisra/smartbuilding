namespace SmartBuilding.Application.Interfaces;

/// <summary>Notifications inter-modules après une synchronisation (auto ou manuelle).</summary>
public interface ISyncNotifier
{
    event EventHandler<SyncResult>? SyncCompleted;

    void Notify(SyncResult result);
}

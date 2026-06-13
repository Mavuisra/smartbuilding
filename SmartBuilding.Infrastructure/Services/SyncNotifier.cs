using SmartBuilding.Application.Interfaces;

namespace SmartBuilding.Infrastructure.Services;

public sealed class SyncNotifier : ISyncNotifier
{
    public event EventHandler<SyncResult>? SyncCompleted;

    public void Notify(SyncResult result) => SyncCompleted?.Invoke(this, result);
}

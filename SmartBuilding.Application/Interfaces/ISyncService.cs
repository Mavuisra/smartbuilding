namespace SmartBuilding.Application.Interfaces;

public interface ISyncService
{
    Task<bool> IsOnlineAsync(CancellationToken cancellationToken = default);
    Task<bool> IsCloudStoreEmptyAsync(CancellationToken cancellationToken = default);
    Task MarkAllLocalDataForPushAsync(CancellationToken cancellationToken = default);
    Task<SyncResult> SyncAsync(bool manual = false, CancellationToken cancellationToken = default);
    Task EnsureMetadataLoadedAsync(CancellationToken cancellationToken = default);
    DateTime? LastSyncAt { get; }
    bool IsSyncing { get; }
}

public record SyncResult(bool Success, int Pushed, int Pulled, int Conflicts, string? Error);

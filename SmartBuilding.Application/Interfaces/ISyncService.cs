using SmartBuilding.Shared.DTOs.Sync;

namespace SmartBuilding.Application.Interfaces;

public interface ISyncService
{
    Task<bool> IsOnlineAsync(CancellationToken cancellationToken = default);
    Task<bool> IsCloudStoreEmptyAsync(CancellationToken cancellationToken = default);
    /// <summary>True si le cloud contient des données et que le poste n'a pas encore reçu la copie initiale.</summary>
    Task<bool> NeedsInitialCloudPullAsync(CancellationToken cancellationToken = default);
    /// <summary>Télécharge toutes les données cloud (PostgreSQL) vers MySQL local — sans envoi local.</summary>
    Task<SyncResult> PerformInitialCloudPullAsync(CancellationToken cancellationToken = default);
    /// <summary>Télécharge depuis PostgreSQL vers MySQL local (pull-only, avec gestion des conflits).</summary>
    Task<SyncResult> PerformCloudToLocalPullAsync(bool fullPull = true, CancellationToken cancellationToken = default);
    /// <summary>Envoie uniquement les changements locaux (create/update/delete) vers le cloud PostgreSQL.</summary>
    Task<SyncResult> PushLocalChangesAsync(CancellationToken cancellationToken = default);
    IReadOnlyList<SyncConflictDetail> GetLastPullConflicts();
    Task MarkAllLocalDataForPushAsync(CancellationToken cancellationToken = default);
    Task<SyncResult> SyncAsync(bool manual = false, CancellationToken cancellationToken = default);
    Task EnsureMetadataLoadedAsync(CancellationToken cancellationToken = default);
    DateTime? LastSyncAt { get; }
    bool IsSyncing { get; }
}

public record SyncResult(
    bool Success,
    int Pushed,
    int Pulled,
    int Conflicts,
    string? Error,
    IReadOnlyList<SyncConflictDetail>? ConflictDetails = null);

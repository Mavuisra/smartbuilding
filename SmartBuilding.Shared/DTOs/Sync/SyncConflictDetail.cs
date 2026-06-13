namespace SmartBuilding.Shared.DTOs.Sync;

/// <summary>Détail d'un conflit détecté lors d'un téléchargement cloud → local.</summary>
public sealed record SyncConflictDetail(
    string EntityType,
    string EntityTypeLabel,
    Guid EntityId,
    string RecordLabel,
    DateTime LocalUpdatedAt,
    DateTime RemoteUpdatedAt,
    string Resolution);

using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Shared.DTOs.Sync;

namespace SmartBuilding.Infrastructure.Sync;

public interface IEntitySyncAdapter
{
    string EntityType { get; }

    Task<IReadOnlyList<SyncEntityPayload>> GetChangesSinceAsync(
        SmartBuildingDbContext context,
        DateTime since,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SyncEntityPayload>> GetLocalChangesAsync(
        SmartBuildingDbContext context,
        CancellationToken cancellationToken);

    Task MarkAsSyncedAsync(
        SmartBuildingDbContext context,
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken);

    Task<int> CountUnsyncedAsync(
        SmartBuildingDbContext context,
        CancellationToken cancellationToken);

    Task<bool> ApplyRemoteAsync(
        SmartBuildingDbContext context,
        SyncEntityPayload remote,
        CancellationToken cancellationToken);
}

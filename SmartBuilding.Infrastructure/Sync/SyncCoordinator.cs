using Microsoft.EntityFrameworkCore;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Shared.DTOs.Sync;

namespace SmartBuilding.Infrastructure.Sync;

public static class SyncCoordinator
{
    public static async Task<int> ApplyPushAsync(
        SmartBuildingDbContext context,
        SyncPushRequest request,
        CancellationToken cancellationToken)
    {
        var adapter = SyncEntityRegistry.TryGet(request.EntityType);
        if (adapter is null)
            return 0;

        var applied = 0;
        foreach (var entity in request.Entities)
        {
            if (await adapter.ApplyRemoteAsync(context, entity, cancellationToken))
                applied++;
        }

        await context.SaveChangesAsync(cancellationToken);
        return applied;
    }

    public static async Task<int> ApplyPullAsync(
        SmartBuildingDbContext context,
        string entityType,
        IReadOnlyList<SyncEntityPayload> entities,
        CancellationToken cancellationToken)
    {
        var adapter = SyncEntityRegistry.TryGet(entityType);
        if (adapter is null)
            return 0;

        var conflicts = 0;
        foreach (var remote in entities)
        {
            if (!await adapter.ApplyRemoteAsync(context, remote, cancellationToken))
                conflicts++;
        }

        await context.SaveChangesAsync(cancellationToken);
        return conflicts;
    }

    public static async Task<DateTime?> GetLastSuccessfulSyncAtAsync(
        SmartBuildingDbContext context,
        CancellationToken cancellationToken)
    {
        return await context.SyncLogs
            .AsNoTracking()
            .Where(x => x.Success && x.CompletedAt != null)
            .OrderByDescending(x => x.CompletedAt)
            .Select(x => x.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public static async Task<int> CountAllUnsyncedAsync(
        SmartBuildingDbContext context,
        CancellationToken cancellationToken)
    {
        var total = 0;
        foreach (var adapter in SyncEntityRegistry.AllAdapters)
            total += await adapter.CountUnsyncedAsync(context, cancellationToken);
        return total;
    }
}

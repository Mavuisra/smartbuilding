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

    public static async Task<(int Conflicts, int Applied)> ApplyPullAsync(
        SmartBuildingDbContext context,
        string entityType,
        IReadOnlyList<SyncEntityPayload> entities,
        CancellationToken cancellationToken,
        IList<SyncConflictDetail>? conflictDetails = null)
    {
        var adapter = SyncEntityRegistry.TryGet(entityType);
        if (adapter is null)
            return (0, 0);

        var conflicts = 0;
        var applied = 0;
        foreach (var remote in entities)
        {
            var localUpdatedAt = await adapter.TryGetLocalUpdatedAtAsync(context, remote.Id, cancellationToken);
            if (localUpdatedAt.HasValue && remote.UpdatedAt < localUpdatedAt.Value)
            {
                conflicts++;
                conflictDetails?.Add(new SyncConflictDetail(
                    entityType,
                    SyncEntityLabels.ToFrench(entityType),
                    remote.Id,
                    $"{SyncEntityLabels.ToFrench(entityType)} #{remote.Id.ToString()[..8]}",
                    localUpdatedAt.Value,
                    remote.UpdatedAt,
                    "Local conservé (plus récent — Last Write Wins)"));
                continue;
            }

            if (!await adapter.ApplyRemoteAsync(context, remote, cancellationToken))
            {
                conflicts++;
                continue;
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                applied++;
            }
            catch (DbUpdateException)
            {
                DetachPendingEntries(context);
                conflicts++;
            }
        }

        return (conflicts, applied);
    }

    private static void DetachPendingEntries(DbContext context)
    {
        foreach (var entry in context.ChangeTracker.Entries()
                     .Where(e => e.State != EntityState.Unchanged)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    public static async Task<string?> DescribeUnsyncedAsync(
        SmartBuildingDbContext context,
        int maxSamples,
        CancellationToken cancellationToken)
    {
        var parts = new List<string>();
        foreach (var adapter in SyncEntityRegistry.AllAdapters)
        {
            var count = await adapter.CountUnsyncedAsync(context, cancellationToken);
            if (count == 0)
                continue;

            var label = adapter.EntityType;
            if (adapter.EntityType == "FinancialTransactions")
            {
                var samples = await context.FinancialTransactions
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(x => !x.IsSynced)
                    .OrderByDescending(x => x.UpdatedAt)
                    .Take(maxSamples)
                    .Select(x => x.Description)
                    .ToListAsync(cancellationToken);
                if (samples.Count > 0)
                    label += $" ({string.Join("; ", samples)})";
            }

            parts.Add($"{label}: {count}");
        }

        return parts.Count == 0 ? null : string.Join(" | ", parts);
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

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Common;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Shared.DTOs.Sync;

namespace SmartBuilding.Infrastructure.Sync;

public sealed class EntitySyncAdapter<TEntity> : IEntitySyncAdapter
    where TEntity : BaseEntity
{
    private readonly string _entityType;
    private readonly Func<SmartBuildingDbContext, DbSet<TEntity>> _dbSet;

    public EntitySyncAdapter(string entityType, Func<SmartBuildingDbContext, DbSet<TEntity>> dbSet)
    {
        _entityType = entityType;
        _dbSet = dbSet;
    }

    public string EntityType => _entityType;

    public async Task<IReadOnlyList<SyncEntityPayload>> GetChangesSinceAsync(
        SmartBuildingDbContext context,
        DateTime since,
        CancellationToken cancellationToken)
    {
        var items = await _dbSet(context)
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(x => x.UpdatedAt > since)
            .OrderBy(x => x.UpdatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        return items.Select(i => new SyncEntityPayload
        {
            Id = i.Id,
            UpdatedAt = i.UpdatedAt,
            DeletedAt = i.DeletedAt,
            JsonData = JsonSerializer.Serialize(i, SyncJson.Options)
        }).ToList();
    }

    public async Task<IReadOnlyList<SyncEntityPayload>> GetLocalChangesAsync(
        SmartBuildingDbContext context,
        CancellationToken cancellationToken)
    {
        var items = await _dbSet(context)
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(x => !x.IsSynced && x.DeletedAt == null)
            .OrderBy(x => x.UpdatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        return items.Select(i => new SyncEntityPayload
        {
            Id = i.Id,
            UpdatedAt = i.UpdatedAt,
            DeletedAt = i.DeletedAt,
            JsonData = JsonSerializer.Serialize(i, SyncJson.Options)
        }).ToList();
    }

    public async Task MarkAsSyncedAsync(
        SmartBuildingDbContext context,
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
            return;

        await _dbSet(context)
            .IgnoreQueryFilters()
            .Where(x => ids.Contains(x.Id))
            .ExecuteUpdateAsync(
                s => s.SetProperty(e => e.IsSynced, true),
                cancellationToken);
    }

    public Task<int> CountUnsyncedAsync(
        SmartBuildingDbContext context,
        CancellationToken cancellationToken) =>
        _dbSet(context)
            .IgnoreQueryFilters()
            .CountAsync(x => !x.IsSynced && x.DeletedAt == null, cancellationToken);

    public async Task<bool> ApplyRemoteAsync(
        SmartBuildingDbContext context,
        SyncEntityPayload remote,
        CancellationToken cancellationToken)
    {
        var set = _dbSet(context);
        var local = await set
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == remote.Id, cancellationToken);

        if (local is null)
        {
            var entity = JsonSerializer.Deserialize<TEntity>(remote.JsonData, SyncJson.Options);
            if (entity is null)
                return false;

            entity.IsSynced = true;
            set.Add(entity);
            return true;
        }

        if (remote.UpdatedAt >= local.UpdatedAt)
        {
            var updated = JsonSerializer.Deserialize<TEntity>(remote.JsonData, SyncJson.Options);
            if (updated is null)
                return false;

            updated.IsSynced = true;
            context.Entry(local).CurrentValues.SetValues(updated);
            local.IsSynced = true;
            context.Entry(local).Property(e => e.IsSynced).IsModified = true;
            return true;
        }

        return false;
    }
}

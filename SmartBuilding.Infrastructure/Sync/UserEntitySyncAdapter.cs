using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Auth;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Shared.DTOs.Sync;

namespace SmartBuilding.Infrastructure.Sync;

/// <summary>
/// Utilisateurs cloud : résolution par Id, puis email, puis nom d'utilisateur (évite IX_Users_Email).
/// </summary>
public sealed class UserEntitySyncAdapter : IEntitySyncAdapter
{
    private readonly EntitySyncAdapter<User> _inner = new("Users", ctx => ctx.Users);

    public string EntityType => _inner.EntityType;

    public Task<IReadOnlyList<SyncEntityPayload>> GetChangesSinceAsync(
        SmartBuildingDbContext context, DateTime since, CancellationToken cancellationToken) =>
        _inner.GetChangesSinceAsync(context, since, cancellationToken);

    public Task<IReadOnlyList<SyncEntityPayload>> GetLocalChangesAsync(
        SmartBuildingDbContext context, CancellationToken cancellationToken) =>
        _inner.GetLocalChangesAsync(context, cancellationToken);

    public Task MarkAsSyncedAsync(
        SmartBuildingDbContext context, IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        _inner.MarkAsSyncedAsync(context, ids, cancellationToken);

    public Task<int> MarkAllUnsyncedAsync(
        SmartBuildingDbContext context, CancellationToken cancellationToken) =>
        _inner.MarkAllUnsyncedAsync(context, cancellationToken);

    public Task<int> CountUnsyncedAsync(
        SmartBuildingDbContext context, CancellationToken cancellationToken) =>
        _inner.CountUnsyncedAsync(context, cancellationToken);

    public Task<DateTime?> TryGetLocalUpdatedAtAsync(
        SmartBuildingDbContext context, Guid id, CancellationToken cancellationToken) =>
        _inner.TryGetLocalUpdatedAtAsync(context, id, cancellationToken);

    public async Task<bool> ApplyRemoteAsync(
        SmartBuildingDbContext context,
        SyncEntityPayload remote,
        CancellationToken cancellationToken)
    {
        var updated = JsonSerializer.Deserialize<User>(remote.JsonData, SyncJson.Options);
        if (updated is null)
            return false;

        SyncEntityGraphSanitizer.ClearNavigations(context, updated);

        var set = context.Users;
        var local = await set
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == remote.Id, cancellationToken);

        if (local is null && !string.IsNullOrWhiteSpace(updated.Email))
        {
            local = await set
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Email == updated.Email, cancellationToken);
        }

        if (local is null && !string.IsNullOrWhiteSpace(updated.Username))
        {
            local = await set
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Username == updated.Username, cancellationToken);
        }

        if (local is null)
        {
            updated.IsSynced = true;
            set.Add(updated);
            return true;
        }

        if (remote.UpdatedAt >= local.UpdatedAt)
        {
            updated.IsSynced = true;
            context.Entry(local).CurrentValues.SetValues(updated);
            local.IsSynced = true;
            context.Entry(local).Property(e => e.IsSynced).IsModified = true;
            return true;
        }

        return false;
    }
}

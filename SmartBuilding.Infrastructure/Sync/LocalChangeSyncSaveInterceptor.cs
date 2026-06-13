using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Domain.Common;
using SmartBuilding.Domain.Entities.Email;
using SmartBuilding.Domain.Entities.Sync;
using SmartBuilding.Domain.Entities.System;

namespace SmartBuilding.Infrastructure.Sync;

/// <summary>
/// Détecte les CRUD locaux sur entités synchronisables et déclenche l'envoi cloud.
/// </summary>
public sealed class LocalChangeSyncSaveInterceptor : SaveChangesInterceptor
{
    private static readonly AsyncLocal<bool> HasSyncableChanges = new();

    private static readonly HashSet<Type> IgnoredTypes =
    [
        typeof(SyncLog),
        typeof(SystemLog),
        typeof(CachedEmail),
        typeof(EmailAccount)
    ];

    private readonly ILocalChangeSyncTrigger _trigger;

    public LocalChangeSyncSaveInterceptor(ILocalChangeSyncTrigger trigger) => _trigger = trigger;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        HasSyncableChanges.Value = HasPendingSyncableChanges(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        HasSyncableChanges.Value = HasPendingSyncableChanges(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        NotifyIfNeeded(result);
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        NotifyIfNeeded(result);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private static bool HasPendingSyncableChanges(DbContext? context)
    {
        if (context is null)
            return false;

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            if (IgnoredTypes.Contains(entry.Entity.GetType()))
                continue;

            if (entry.Entity.IsSynced)
                continue;

            if (SyncEntityRegistry.IsSyncableEntity(entry.Entity))
                return true;
        }

        return false;
    }

    private void NotifyIfNeeded(int savedCount)
    {
        if (savedCount > 0 && HasSyncableChanges.Value)
            _trigger.RequestPush();

        HasSyncableChanges.Value = false;
    }
}

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Common;
using SmartBuilding.Domain.Interfaces;

namespace SmartBuilding.Infrastructure.Persistence;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly SmartBuildingDbContext Context;
    protected readonly DbSet<T> DbSet;

    public Repository(SmartBuildingDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await DbSet.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await DbSet.OrderByDescending(x => x.UpdatedAt).ToListAsync(cancellationToken);

    public virtual async Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        await DbSet.Where(predicate).ToListAsync(cancellationToken);

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await DbSet.AddAsync(entity, cancellationToken);
        return entity;
    }

    public virtual Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity.MarkUpdated();
        DbSet.Update(entity);
        return Task.CompletedTask;
    }

    public virtual Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity.SoftDelete();
        DbSet.Update(entity);
        return Task.CompletedTask;
    }

    public virtual async Task<IReadOnlyList<T>> GetUnsyncedAsync(CancellationToken cancellationToken = default) =>
        await DbSet.Where(x => !x.IsSynced).ToListAsync(cancellationToken);
}

using SmartBuilding.Domain.Interfaces;

namespace SmartBuilding.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly SmartBuildingDbContext _context;

    public UnitOfWork(SmartBuildingDbContext context) => _context = context;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);

    public ValueTask DisposeAsync() => _context.DisposeAsync();
}

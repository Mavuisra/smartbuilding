using Microsoft.EntityFrameworkCore;
using SmartBuilding.Infrastructure.Persistence;

namespace SmartBuilding.Infrastructure.Services;

public class LocationDataCleaner
{
    private readonly SmartBuildingDbContext _db;

    public LocationDataCleaner(SmartBuildingDbContext db) => _db = db;

    public async Task<int> ClearAllAsync(CancellationToken cancellationToken = default)
    {
        var count = 0;
        count += await _db.RentPayments.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        count += await _db.LeaseGuarantees.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        count += await _db.LeaseContracts.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        count += await _db.TenantActivities.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        count += await _db.Premises.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        count += await _db.Tenants.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        count += await _db.Buildings.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        return count;
    }
}

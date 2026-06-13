using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Infrastructure.Persistence;

namespace SmartBuilding.Infrastructure.Sync;

/// <summary>
/// Repousse les parents (locataires, locaux, contrats) avant les paiements de loyer.
/// </summary>
public static class SyncDependencyPusher
{
    public static readonly string[] RentPaymentChain =
    [
        "Tenants",
        "Premises",
        "LeaseContracts"
    ];

    public static async Task PrepareRentPaymentChainAsync(
        SmartBuildingDbContext context,
        CancellationToken cancellationToken = default)
    {
        var leaseIds = await context.RentPayments
            .IgnoreQueryFilters()
            .Where(r => !r.IsSynced && r.DeletedAt == null)
            .Select(r => r.LeaseContractId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (leaseIds.Count == 0)
            return;

        var contracts = await context.LeaseContracts
            .IgnoreQueryFilters()
            .Where(c => leaseIds.Contains(c.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var contract in contracts)
        {
            contract.IsSynced = false;
            contract.MarkUpdated();
        }

        var premiseIds = contracts.Select(c => c.PremiseId).Distinct().ToList();
        var tenantIds = contracts.Select(c => c.TenantId).Distinct().ToList();

        if (premiseIds.Count > 0)
        {
            await context.Premises
                .IgnoreQueryFilters()
                .Where(p => premiseIds.Contains(p.Id))
                .ExecuteUpdateAsync(
                    s => s.SetProperty(p => p.IsSynced, false),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (tenantIds.Count > 0)
        {
            await context.Tenants
                .IgnoreQueryFilters()
                .Where(t => tenantIds.Contains(t.Id))
                .ExecuteUpdateAsync(
                    s => s.SetProperty(t => t.IsSynced, false),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

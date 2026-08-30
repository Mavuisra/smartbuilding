using Microsoft.EntityFrameworkCore;
using SmartBuilding.Infrastructure.Persistence;

namespace SmartBuilding.Desktop.WPF.Services;

/// <summary>Détecte si le profil entreprise doit être complété après création d'un tenant.</summary>
public sealed class CompanyProfileCompletionService
{
    private readonly OrganizationRegistry _registry;
    private readonly IDbContextFactory<SmartBuildingDbContext> _dbContextFactory;

    public CompanyProfileCompletionService(
        OrganizationRegistry registry,
        IDbContextFactory<SmartBuildingDbContext> dbContextFactory)
    {
        _registry = registry;
        _dbContextFactory = dbContextFactory;
    }

    public async Task<bool> NeedsSetupAsync(CancellationToken cancellationToken = default)
    {
        var org = _registry.Active;
        if (org is not null && org.CompanyProfileCompleted)
            return false;

        return !await IsDatabaseProfileCompleteAsync(cancellationToken);
    }

    public async Task<bool> IsDatabaseProfileCompleteAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var building = await db.BuildingInfos
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (building is null)
            return false;

        return !string.IsNullOrWhiteSpace(building.Name)
               && !string.IsNullOrWhiteSpace(building.Phone)
               && !string.IsNullOrWhiteSpace(building.Email)
               && !string.IsNullOrWhiteSpace(building.Address)
               && !string.IsNullOrWhiteSpace(building.City);
    }

    public void MarkCompleted()
    {
        var org = _registry.Active;
        if (org is null)
            return;

        _registry.MarkCompanyProfileCompleted(org.Id);
    }
}

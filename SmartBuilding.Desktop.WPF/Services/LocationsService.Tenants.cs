using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public partial class LocationsService
{
    public async Task<LocationsTenantStats> GetTenantStatsAsync(CancellationToken cancellationToken = default)
    {
        var tenants = await _db.Tenants
            .Select(t => new { t.RentalStatus })
            .ToListAsync(cancellationToken);

        var activeContractTenantIds = await _db.LeaseContracts
            .Where(c => c.Status == LeaseStatus.Actif)
            .Select(c => c.TenantId)
            .Distinct()
            .CountAsync(cancellationToken);

        var today = DateTime.Today;
        var latePayments = await _db.RentPayments
            .CountAsync(p =>
                (p.IsLate || p.AmountPaid < p.AmountDue && p.DueDate.Date < today) &&
                p.AmountPaid < p.AmountDue,
                cancellationToken);

        return new LocationsTenantStats
        {
            Total = tenants.Count,
            Active = tenants.Count(t => t.RentalStatus == LocationConstants.TenantStatus.Active),
            WithActiveContract = activeContractTenantIds,
            LatePayments = latePayments
        };
    }

    public async Task<IReadOnlyList<LocationsTenantItem>> GetAllTenantsListedAsync(
        CancellationToken cancellationToken = default)
    {
        var tenants = await _db.Tenants.OrderBy(t => t.Name).ToListAsync(cancellationToken);
        var contracts = await _db.LeaseContracts.ToListAsync(cancellationToken);
        return MapTenantsForList(tenants, contracts);
    }

    private static List<LocationsTenantItem> MapTenantsForList(
        IEnumerable<Tenant> tenants,
        IEnumerable<LeaseContract> contracts)
    {
        var byTenant = contracts.GroupBy(c => c.TenantId).ToDictionary(g => g.Key, g => g.ToList());
        return tenants.Select(t =>
        {
            var (bg, fg) = t.RentalStatus switch
            {
                LocationConstants.TenantStatus.Active => ("#DCFCE7", "#166534"),
                LocationConstants.TenantStatus.Suspended => ("#FEF3C7", "#92400E"),
                LocationConstants.TenantStatus.Terminated => ("#FEE2E2", "#991B1B"),
                LocationConstants.TenantStatus.Pending => ("#DBEAFE", "#1D4ED8"),
                LocationConstants.TenantStatus.Archived => ("#F1F5F9", "#64748B"),
                _ => ("#F1F5F9", "#64748B")
            };

            return new LocationsTenantItem
            {
                Id = t.Id,
                Initials = GetTenantInitials(t.Name),
                DossierNumber = string.IsNullOrWhiteSpace(t.DossierNumber) ? "—" : t.DossierNumber,
                RentalStatus = t.RentalStatus,
                Name = t.Name,
                Phone = t.Phone,
                Email = string.IsNullOrWhiteSpace(t.Email) ? "—" : t.Email,
                Company = string.IsNullOrWhiteSpace(t.Company) ? "—" : t.Company,
                TenantCategory = string.IsNullOrWhiteSpace(t.TenantCategory) ? "Particulier" : t.TenantCategory,
                Profession = string.IsNullOrWhiteSpace(t.Profession) ? "—" : t.Profession,
                Nationality = string.IsNullOrWhiteSpace(t.Nationality) ? "—" : t.Nationality,
                ActiveContracts = byTenant.TryGetValue(t.Id, out var list)
                    ? list.Count(c => c.Status == LeaseStatus.Actif)
                    : 0,
                StatusBadgeBackground = bg,
                StatusBadgeForeground = fg
            };
        }).OrderBy(t => t.Name).ToList();
    }

    private static string GetTenantInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
            : name.Length >= 2 ? name[..2].ToUpperInvariant() : "L";
    }
}

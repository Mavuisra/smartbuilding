using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Services;

namespace SmartBuilding.Desktop.WPF.Services;

public partial class LocationsService
{
    public async Task<IReadOnlyList<TenantDependentItem>> GetTenantDependentsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var fr = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
        var rows = await _db.TenantDependents
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.DeletedAt == null)
            .OrderBy(d => d.FullName)
            .ToListAsync(cancellationToken);

        return rows.Select(d => new TenantDependentItem
        {
            Id = d.Id,
            FullName = d.FullName,
            Relationship = d.Relationship,
            DateOfBirthDisplay = d.DateOfBirth.HasValue
                ? d.DateOfBirth.Value.ToString("dd/MM/yyyy", fr)
                : "—",
            NationalId = string.IsNullOrWhiteSpace(d.NationalId) ? "—" : d.NationalId
        }).ToList();
    }

    public async Task<string> ReplaceTenantDependentsAsync(
        Guid tenantId,
        IReadOnlyList<TenantDependentDraft> dependents,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null)
            return "Locataire introuvable.";

        var existing = await _db.TenantDependents
            .Where(d => d.TenantId == tenantId && d.DeletedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var row in existing)
            row.SoftDelete();

        foreach (var draft in dependents.Where(d => !string.IsNullOrWhiteSpace(d.FullName)))
        {
            _db.TenantDependents.Add(new TenantDependent
            {
                TenantId = tenantId,
                FullName = draft.FullName.Trim(),
                Relationship = string.IsNullOrWhiteSpace(draft.Relationship)
                    ? LocationConstants.DependentRelationships.Other
                    : draft.Relationship.Trim(),
                DateOfBirth = draft.DateOfBirth,
                NationalId = draft.NationalId?.Trim(),
                Notes = draft.Notes?.Trim()
            });
        }

        var fromDependents = Math.Max(1, dependents.Count(d => !string.IsNullOrWhiteSpace(d.FullName)) + 1);
        tenant.PersonCount = Math.Max(tenant.PersonCount, fromDependents);
        tenant.MarkUpdated();
        return await _db.SaveChangesWithMessageAsync(cancellationToken);
    }
}

public sealed class TenantDependentDraft
{
    public string FullName { get; init; } = string.Empty;
    public string Relationship { get; init; } = string.Empty;
    public DateTime? DateOfBirth { get; init; }
    public string? NationalId { get; init; }
    public string? Notes { get; init; }
}

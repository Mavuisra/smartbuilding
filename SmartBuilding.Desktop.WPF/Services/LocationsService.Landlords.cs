using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Services;

namespace SmartBuilding.Desktop.WPF.Services;

public partial class LocationsService
{
    public async Task<LocationsLandlordStats> GetLandlordStatsAsync(CancellationToken cancellationToken = default)
    {
        var landlords = await _db.Landlords
            .Select(l => new { l.Status, l.LandlordType })
            .ToListAsync(cancellationToken);

        var linkedBuildings = await _db.Buildings
            .CountAsync(b => b.LandlordId != null && b.DeletedAt == null, cancellationToken);

        return new LocationsLandlordStats
        {
            Total = landlords.Count,
            Active = landlords.Count(l => l.Status == LocationConstants.LandlordStatus.Active),
            Companies = landlords.Count(l =>
                l.LandlordType.Equals(LocationConstants.LandlordTypes.Company, StringComparison.OrdinalIgnoreCase)),
            LinkedBuildings = linkedBuildings
        };
    }

    public async Task<IReadOnlyList<LocationsLandlordItem>> GetAllLandlordsAsync(CancellationToken cancellationToken = default)
    {
        var landlords = await _db.Landlords
            .OrderBy(l => l.Name)
            .ToListAsync(cancellationToken);

        var buildingCounts = await _db.Buildings
            .Where(b => b.LandlordId != null && b.DeletedAt == null)
            .GroupBy(b => b.LandlordId!.Value)
            .Select(g => new { LandlordId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.LandlordId, x => x.Count, cancellationToken);

        return landlords.Select(l => MapLandlord(l, buildingCounts.GetValueOrDefault(l.Id))).ToList();
    }

    public async Task<Landlord?> GetLandlordAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.Landlords.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public async Task<string> GenerateNextLandlordReferenceAsync(CancellationToken cancellationToken = default)
    {
        var count = await _db.Landlords.CountAsync(cancellationToken);
        return $"LOC-{(count + 1):D4}";
    }

    public async Task<string> CreateLandlordAsync(Landlord landlord, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(landlord.Name))
            return "Le nom du locateur est obligatoire.";
        if (string.IsNullOrWhiteSpace(landlord.Phone))
            return "Le téléphone est obligatoire.";

        landlord.Name = landlord.Name.Trim();
        landlord.Phone = landlord.Phone.Trim();
        landlord.Email = landlord.Email.Trim();
        landlord.LandlordType = string.IsNullOrWhiteSpace(landlord.LandlordType)
            ? LocationConstants.LandlordTypes.Individual
            : landlord.LandlordType.Trim();
        landlord.Status = string.IsNullOrWhiteSpace(landlord.Status)
            ? LocationConstants.LandlordStatus.Active
            : landlord.Status.Trim();

        if (string.IsNullOrWhiteSpace(landlord.ReferenceNumber))
            landlord.ReferenceNumber = await GenerateNextLandlordReferenceAsync(cancellationToken);

        _db.Landlords.Add(landlord);
        await LogLandlordActivityAsync(
            landlord.Id,
            "Création",
            "Locateur enregistré",
            $"Dossier {landlord.ReferenceNumber} — {landlord.Name}",
            cancellationToken);

        return await _db.SaveChangesWithMessageAsync(cancellationToken);
    }

    public async Task<string> UpdateLandlordAsync(Landlord landlord, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(landlord.Name))
            return "Le nom est obligatoire.";

        var existing = await _db.Landlords.FirstOrDefaultAsync(l => l.Id == landlord.Id, cancellationToken);
        if (existing is null)
            return "Locateur introuvable.";

        existing.Name = landlord.Name.Trim();
        existing.Phone = landlord.Phone.Trim();
        existing.SecondaryPhone = landlord.SecondaryPhone?.Trim();
        existing.Email = landlord.Email.Trim();
        existing.LandlordType = string.IsNullOrWhiteSpace(landlord.LandlordType)
            ? LocationConstants.LandlordTypes.Individual
            : landlord.LandlordType.Trim();
        existing.Status = string.IsNullOrWhiteSpace(landlord.Status)
            ? LocationConstants.LandlordStatus.Active
            : landlord.Status.Trim();
        existing.Address = landlord.Address?.Trim();
        existing.City = landlord.City?.Trim();
        existing.Country = landlord.Country?.Trim();
        existing.NationalId = landlord.NationalId?.Trim();
        existing.TaxId = landlord.TaxId?.Trim();
        existing.ContactPerson = landlord.ContactPerson?.Trim();
        existing.BankName = landlord.BankName?.Trim();
        existing.BankAccount = landlord.BankAccount?.Trim();
        existing.Notes = landlord.Notes?.Trim();
        existing.MarkUpdated();

        await LogLandlordActivityAsync(
            existing.Id,
            "Modification",
            "Fiche mise à jour",
            "Informations du locateur modifiées.",
            cancellationToken);

        return await _db.SaveChangesWithMessageAsync(cancellationToken);
    }

    public async Task<string> DeleteLandlordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var landlord = await _db.Landlords.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (landlord is null)
            return "Locateur introuvable.";

        var hasBuildings = await _db.Buildings.AnyAsync(
            b => b.LandlordId == id && b.DeletedAt == null, cancellationToken);
        if (hasBuildings)
            return "Ce locateur est lié à un ou plusieurs bâtiments. Retirez le lien avant de supprimer.";

        landlord.Status = LocationConstants.LandlordStatus.Archived;
        landlord.SoftDelete();

        await LogLandlordActivityAsync(
            id,
            "Archivage",
            "Locateur archivé",
            $"Le locateur {landlord.Name} a été retiré de la liste active.",
            cancellationToken);

        return await _db.SaveChangesWithMessageAsync(cancellationToken);
    }

    public Task LogLandlordActivityAsync(
        Guid landlordId,
        string category,
        string title,
        string description,
        CancellationToken cancellationToken = default)
    {
        _db.LandlordActivities.Add(new LandlordActivity
        {
            LandlordId = landlordId,
            OccurredAt = DateTime.UtcNow,
            Category = category.Trim(),
            Title = title.Trim(),
            Description = description.Trim()
        });
        return Task.CompletedTask;
    }

    private static LocationsLandlordItem MapLandlord(Landlord l, int buildingCount)
    {
        var (bg, fg) = l.Status switch
        {
            LocationConstants.LandlordStatus.Active => ("#DCFCE7", "#166534"),
            LocationConstants.LandlordStatus.Inactive => ("#FEF3C7", "#92400E"),
            LocationConstants.LandlordStatus.Archived => ("#F1F5F9", "#64748B"),
            _ => ("#F1F5F9", "#64748B")
        };

        var addressParts = new[] { l.Address, l.City, l.Country }
            .Where(s => !string.IsNullOrWhiteSpace(s));
        var addressDisplay = addressParts.Any() ? string.Join(", ", addressParts) : "—";

        return new LocationsLandlordItem
        {
            Id = l.Id,
            ReferenceNumber = l.ReferenceNumber,
            Name = l.Name,
            LandlordType = l.LandlordType,
            Phone = l.Phone,
            Email = string.IsNullOrWhiteSpace(l.Email) ? "—" : l.Email,
            AddressDisplay = addressDisplay,
            ContactPerson = string.IsNullOrWhiteSpace(l.ContactPerson) ? "—" : l.ContactPerson,
            BuildingCount = buildingCount,
            StatusLabel = l.Status,
            StatusBadgeBackground = bg,
            StatusBadgeForeground = fg
        };
    }
}

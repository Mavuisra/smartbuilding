using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Infrastructure.Persistence;

namespace SmartBuilding.Desktop.WPF.Services;

public sealed class PropertyStructureService
{
    private readonly SmartBuildingDbContext _db;

    public PropertyStructureService(SmartBuildingDbContext db) => _db = db;

    public async Task<IReadOnlyList<PropertyFloorDraft>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var buildingInfoId = await GetBuildingInfoIdAsync(cancellationToken);
        if (buildingInfoId is null)
            return [];

        var floors = await _db.PropertyFloors
            .Where(f => f.BuildingInfoId == buildingInfoId.Value)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.LevelNumber)
            .ToListAsync(cancellationToken);

        var floorIds = floors.Select(f => f.Id).ToList();
        var apartments = await _db.PropertyApartments
            .Where(a => floorIds.Contains(a.FloorId))
            .OrderBy(a => a.SortOrder).ThenBy(a => a.Code)
            .ToListAsync(cancellationToken);

        var apartmentIds = apartments.Select(a => a.Id).ToList();
        var rooms = await _db.PropertyRooms
            .Where(r => apartmentIds.Contains(r.ApartmentId))
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return floors.Select(f => new PropertyFloorDraft
        {
            Id = f.Id,
            LevelNumber = f.LevelNumber,
            Label = f.Label,
            SortOrder = f.SortOrder,
            Apartments = apartments.Where(a => a.FloorId == f.Id).Select(a => new PropertyApartmentDraft
            {
                Id = a.Id,
                Code = a.Code,
                Name = a.Name,
                UnitType = a.UnitType,
                AreaSqM = a.AreaSqM,
                MonthlyRent = a.MonthlyRent,
                SortOrder = a.SortOrder,
                Rooms = rooms.Where(r => r.ApartmentId == a.Id).Select(r => new PropertyRoomDraft
                {
                    Id = r.Id,
                    Name = r.Name,
                    RoomType = r.RoomType,
                    AreaSqM = r.AreaSqM,
                    SortOrder = r.SortOrder
                }).ToList()
            }).ToList()
        }).ToList();
    }

    public static PropertyStructureSummary ComputeSummary(IReadOnlyList<PropertyFloorDraft> floors)
    {
        var apartments = floors.SelectMany(f => f.Apartments).ToList();
        var residential = apartments.Count(a =>
            a.UnitType.Contains("Appartement", StringComparison.OrdinalIgnoreCase));
        var commercial = apartments.Count - residential;
        return new PropertyStructureSummary
        {
            FloorCount = floors.Count,
            ApartmentCount = residential,
            CommercialCount = commercial,
            RoomCount = apartments.SelectMany(a => a.Rooms).Count(),
            TotalAreaSqM = apartments.Sum(a => a.AreaSqM)
        };
    }

    /// <summary>Aucune contrainte obligatoire — les champs vides reçoivent des valeurs par défaut à l'enregistrement.</summary>
    public static string? Validate(IReadOnlyList<PropertyFloorDraft> floors, string? buildingDisplayName) => null;

    public async Task<string> SaveAsync(
        IReadOnlyList<PropertyFloorDraft> floors,
        string buildingDisplayName,
        CancellationToken cancellationToken = default)
    {
        using (await DbContextAccessLock.AcquireAsync(cancellationToken))
        {
        var displayName = string.IsNullOrWhiteSpace(buildingDisplayName)
            ? BuildingInfoDefaults.CompanyName
            : buildingDisplayName.Trim();

        var buildingInfo = await _db.BuildingInfos.FirstOrDefaultAsync(cancellationToken);
        if (buildingInfo is null)
        {
            buildingInfo = new BuildingInfo { Name = displayName };
            _db.BuildingInfos.Add(buildingInfo);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var existingFloors = await _db.PropertyFloors
            .Where(f => f.BuildingInfoId == buildingInfo.Id)
            .ToListAsync(cancellationToken);

        var keptFloorIds = floors.Where(f => f.Id.HasValue).Select(f => f.Id!.Value).ToHashSet();
        foreach (var old in existingFloors.Where(f => !keptFloorIds.Contains(f.Id)))
            await SoftDeleteFloorTreeAsync(old.Id, cancellationToken);

        var sortFloor = 0;
        foreach (var floorDraft in floors)
        {
            var floor = floorDraft.Id.HasValue
                ? existingFloors.FirstOrDefault(f => f.Id == floorDraft.Id.Value)
                : null;

            if (floor is null)
            {
                floor = new PropertyFloor { BuildingInfoId = buildingInfo.Id };
                _db.PropertyFloors.Add(floor);
            }

            floor.LevelNumber = floorDraft.LevelNumber;
            floor.Label = string.IsNullOrWhiteSpace(floorDraft.Label)
                ? (floorDraft.LevelNumber == 0 ? "RDC" : $"Niveau {floorDraft.LevelNumber}")
                : floorDraft.Label.Trim();
            floor.SortOrder = sortFloor++;
            floor.MarkUpdated();

            await _db.SaveChangesAsync(cancellationToken);

            var existingApts = await _db.PropertyApartments
                .Where(a => a.FloorId == floor.Id)
                .ToListAsync(cancellationToken);

            var keptAptIds = floorDraft.Apartments.Where(a => a.Id.HasValue).Select(a => a.Id!.Value).ToHashSet();
            foreach (var oldApt in existingApts.Where(a => !keptAptIds.Contains(a.Id)))
                await SoftDeleteApartmentTreeAsync(oldApt.Id, cancellationToken);

            var sortApt = 0;
            foreach (var aptDraft in floorDraft.Apartments)
            {
                if (!aptDraft.Id.HasValue
                    && string.IsNullOrWhiteSpace(aptDraft.Code)
                    && string.IsNullOrWhiteSpace(aptDraft.Name))
                    continue;

                var apt = aptDraft.Id.HasValue
                    ? existingApts.FirstOrDefault(a => a.Id == aptDraft.Id.Value)
                    : null;

                if (apt is null)
                {
                    apt = new PropertyApartment { FloorId = floor.Id };
                    _db.PropertyApartments.Add(apt);
                }

                var unitIndex = sortApt + 1;
                apt.Code = string.IsNullOrWhiteSpace(aptDraft.Code)
                    ? $"U{unitIndex}"
                    : aptDraft.Code.Trim();
                apt.Name = string.IsNullOrWhiteSpace(aptDraft.Name)
                    ? apt.Code
                    : aptDraft.Name.Trim();
                apt.UnitType = string.IsNullOrWhiteSpace(aptDraft.UnitType)
                    ? PropertyStructureConstants.UnitTypes.Apartment
                    : aptDraft.UnitType.Trim();
                apt.AreaSqM = Math.Max(0, aptDraft.AreaSqM);
                apt.MonthlyRent = Math.Max(0, aptDraft.MonthlyRent);
                apt.SortOrder = sortApt++;
                apt.MarkUpdated();

                await _db.SaveChangesAsync(cancellationToken);

                var existingRooms = await _db.PropertyRooms
                    .Where(r => r.ApartmentId == apt.Id)
                    .ToListAsync(cancellationToken);

                var keptRoomIds = aptDraft.Rooms.Where(r => r.Id.HasValue).Select(r => r.Id!.Value).ToHashSet();
                foreach (var oldRoom in existingRooms.Where(r => !keptRoomIds.Contains(r.Id)))
                {
                    oldRoom.SoftDelete();
                }

                var sortRoom = 0;
                foreach (var roomDraft in aptDraft.Rooms)
                {
                    if (!roomDraft.Id.HasValue && string.IsNullOrWhiteSpace(roomDraft.Name))
                        continue;

                    var room = roomDraft.Id.HasValue
                        ? existingRooms.FirstOrDefault(r => r.Id == roomDraft.Id.Value)
                        : null;

                    if (room is null)
                    {
                        room = new PropertyRoom { ApartmentId = apt.Id };
                        _db.PropertyRooms.Add(room);
                    }

                    room.Name = string.IsNullOrWhiteSpace(roomDraft.Name)
                        ? $"Pièce {sortRoom + 1}"
                        : roomDraft.Name.Trim();
                    room.RoomType = string.IsNullOrWhiteSpace(roomDraft.RoomType)
                        ? PropertyStructureConstants.RoomTypes.Bedroom
                        : roomDraft.RoomType.Trim();
                    room.AreaSqM = Math.Max(0, roomDraft.AreaSqM);
                    room.SortOrder = sortRoom++;
                    room.MarkUpdated();
                }

                await SyncPremiseForApartmentAsync(apt, floor, buildingInfo, cancellationToken);
            }
        }

        var summary = ComputeSummary(floors);
        buildingInfo.BuildingDisplayName = displayName;
        buildingInfo.TotalFloors = summary.FloorCount;
        buildingInfo.ApartmentCount = summary.ApartmentCount;
        buildingInfo.CommercialUnitCount = summary.CommercialCount;
        buildingInfo.TotalPremises = summary.ApartmentCount + summary.CommercialCount;
        if (summary.TotalAreaSqM > 0)
            buildingInfo.TotalAreaSqM = summary.TotalAreaSqM;
        buildingInfo.MarkUpdated();

        await _db.SaveChangesAsync(cancellationToken);
        return string.Empty;
        }
    }

    private async Task SyncPremiseForApartmentAsync(
        PropertyApartment apt,
        PropertyFloor floor,
        BuildingInfo buildingInfo,
        CancellationToken cancellationToken)
    {
        Premise? premise = null;
        if (apt.PremiseId.HasValue)
            premise = await _db.Premises.FirstOrDefaultAsync(p => p.Id == apt.PremiseId.Value, cancellationToken);

        if (premise is null)
            premise = await _db.Premises.FirstOrDefaultAsync(p => p.PropertyApartmentId == apt.Id, cancellationToken);

        var premiseType = apt.UnitType.Contains("commercial", StringComparison.OrdinalIgnoreCase)
            ? LocationConstants.PremiseTypes.Commercial
            : LocationConstants.PremiseTypes.Apartment;

        var roomSummary = await _db.PropertyRooms
            .Where(r => r.ApartmentId == apt.Id && r.DeletedAt == null)
            .OrderBy(r => r.SortOrder)
            .Select(r => r.Name)
            .ToListAsync(cancellationToken);

        var description = roomSummary.Count > 0
            ? $"Pièces : {string.Join(", ", roomSummary)}"
            : apt.UnitType;

        if (premise is null)
        {
            premise = new Premise
            {
                Code = apt.Code,
                Name = apt.Name,
                Building = buildingInfo.BuildingDisplayName,
                Floor = floor.Label,
                PremiseType = premiseType,
                AreaSqM = apt.AreaSqM,
                MonthlyRent = apt.MonthlyRent,
                PropertyApartmentId = apt.Id,
                Description = description,
                Capacity = Math.Max(1, roomSummary.Count)
            };
            _db.Premises.Add(premise);
            await _db.SaveChangesAsync(cancellationToken);
            apt.PremiseId = premise.Id;
        }
        else
        {
            premise.Code = apt.Code;
            premise.Name = apt.Name;
            premise.Building = buildingInfo.BuildingDisplayName;
            premise.Floor = floor.Label;
            premise.PremiseType = premiseType;
            premise.AreaSqM = apt.AreaSqM;
            premise.MonthlyRent = apt.MonthlyRent;
            premise.PropertyApartmentId = apt.Id;
            premise.Description = description;
            premise.Capacity = Math.Max(1, roomSummary.Count);
            premise.MarkUpdated();
        }
    }

    private async Task SoftDeleteFloorTreeAsync(Guid floorId, CancellationToken cancellationToken)
    {
        var apartments = await _db.PropertyApartments.Where(a => a.FloorId == floorId).ToListAsync(cancellationToken);
        foreach (var apt in apartments)
            await SoftDeleteApartmentTreeAsync(apt.Id, cancellationToken);

        var floor = await _db.PropertyFloors.FirstOrDefaultAsync(f => f.Id == floorId, cancellationToken);
        floor?.SoftDelete();
    }

    private async Task SoftDeleteApartmentTreeAsync(Guid apartmentId, CancellationToken cancellationToken)
    {
        var rooms = await _db.PropertyRooms.Where(r => r.ApartmentId == apartmentId).ToListAsync(cancellationToken);
        foreach (var room in rooms)
            room.SoftDelete();

        var apt = await _db.PropertyApartments.FirstOrDefaultAsync(a => a.Id == apartmentId, cancellationToken);
        if (apt is null)
            return;

        if (apt.PremiseId.HasValue)
        {
            var premise = await _db.Premises.FirstOrDefaultAsync(p => p.Id == apt.PremiseId.Value, cancellationToken);
            premise?.SoftDelete();
        }

        apt.SoftDelete();
    }

    public async Task<IReadOnlyList<PatrimoineUnitRow>> GetManagementUnitsAsync(CancellationToken cancellationToken = default)
    {
        var buildingInfo = await _db.BuildingInfos.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var buildingName = buildingInfo?.BuildingDisplayName ?? buildingInfo?.Name ?? "Bâtiment";

        var floors = await LoadAsync(cancellationToken);
        var premiseByApartment = await _db.Premises.AsNoTracking()
            .Where(p => p.DeletedAt == null && p.PropertyApartmentId != null)
            .ToDictionaryAsync(p => p.PropertyApartmentId!.Value, cancellationToken);

        var rows = new List<PatrimoineUnitRow>();
        foreach (var floor in floors)
        {
            foreach (var apt in floor.Apartments)
            {
                if (!apt.Id.HasValue)
                    continue;

                premiseByApartment.TryGetValue(apt.Id.Value, out var premise);
                var status = premise?.OccupancyStatus ?? LocationConstants.PremiseOccupancyStatus.Available;
                var (bg, fg, label) = MapOccupancyBadge(status);
                var rooms = apt.Rooms.Select(r => r.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();

                rows.Add(new PatrimoineUnitRow
                {
                    ApartmentId = apt.Id.Value,
                    FloorId = floor.Id ?? Guid.Empty,
                    PremiseId = premise?.Id,
                    BuildingName = buildingName,
                    FloorLabel = floor.Label,
                    LevelNumber = floor.LevelNumber,
                    Code = apt.Code,
                    Name = apt.Name,
                    UnitType = apt.UnitType,
                    AreaSqM = apt.AreaSqM,
                    MonthlyRent = apt.MonthlyRent,
                    RoomCount = apt.Rooms.Count,
                    RoomsSummary = rooms.Count > 0 ? string.Join(", ", rooms) : "—",
                    OccupancyStatus = status,
                    OccupancyLabel = label,
                    OccupancyBadgeBackground = bg,
                    OccupancyBadgeForeground = fg,
                    IsOccupied = premise?.IsOccupied == true
                        || string.Equals(status, LocationConstants.PremiseOccupancyStatus.Occupied, StringComparison.OrdinalIgnoreCase)
                });
            }
        }

        return rows.OrderBy(r => r.LevelNumber).ThenBy(r => r.Code).ToList();
    }

    public async Task<string> DeleteUnitAsync(Guid apartmentId, CancellationToken cancellationToken = default)
    {
        var apt = await _db.PropertyApartments.FirstOrDefaultAsync(a => a.Id == apartmentId, cancellationToken);
        if (apt is null)
            return "Unité introuvable.";

        if (apt.PremiseId.HasValue)
        {
            var hasActiveLease = await _db.LeaseContracts.AnyAsync(
                c => c.PremiseId == apt.PremiseId.Value
                     && c.DeletedAt == null
                     && LeaseOccupancyRules.OccupyingStatuses.Contains(c.Status),
                cancellationToken);
            if (hasActiveLease)
                return "Impossible de supprimer : un contrat actif est lié à ce local.";
        }

        await SoftDeleteApartmentTreeAsync(apartmentId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return string.Empty;
    }

    private static (string Bg, string Fg, string Label) MapOccupancyBadge(string status)
    {
        if (string.Equals(status, LocationConstants.PremiseOccupancyStatus.Occupied, StringComparison.OrdinalIgnoreCase))
            return ("#F5EBEB", "#A67B7B", "Occupé");
        if (string.Equals(status, LocationConstants.PremiseOccupancyStatus.Reserved, StringComparison.OrdinalIgnoreCase))
            return ("#F5F0E8", "#9A8B6B", "Réservé");
        if (string.Equals(status, LocationConstants.PremiseOccupancyStatus.Maintenance, StringComparison.OrdinalIgnoreCase))
            return ("#EEF1F5", "#7B8A9A", "Maintenance");
        return ("#E8F9EF", "#4cc26b", "Libre");
    }

    private async Task<Guid?> GetBuildingInfoIdAsync(CancellationToken cancellationToken)
    {
        var info = await _db.BuildingInfos.Select(b => b.Id).FirstOrDefaultAsync(cancellationToken);
        return info == Guid.Empty ? null : info;
    }
}

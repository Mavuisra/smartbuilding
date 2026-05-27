using SmartBuilding.Domain.Enums;

namespace SmartBuilding.Domain.Entities.Location;

/// <summary>
/// Statuts de contrat qui réservent un local. Utiliser <see cref="OccupyingStatuses"/> dans les requêtes EF
/// (<c>.Contains(c.Status)</c>) — ne pas appeler de méthode C# personnalisée dans un IQueryable.
/// </summary>
public static class LeaseOccupancyRules
{
    public static readonly LeaseStatus[] OccupyingStatuses =
    [
        LeaseStatus.Actif,
        LeaseStatus.EnAttenteValidation
    ];

    public static bool OccupiesPremise(LeaseStatus status) =>
        status == LeaseStatus.Actif || status == LeaseStatus.EnAttenteValidation;
}

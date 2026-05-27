using SmartBuilding.Domain.Enums;

namespace SmartBuilding.Domain.Entities.Location;

public static class LocationContractStatusHelper
{
    public static string ToLabel(LeaseStatus status) => status switch
    {
        LeaseStatus.Actif => "Actif",
        LeaseStatus.Expire => "Expiré",
        LeaseStatus.Brouillon => "Brouillon",
        LeaseStatus.Resilie => "Résilié",
        LeaseStatus.EnAttenteValidation => "En attente validation",
        LeaseStatus.Suspendu => "Suspendu",
        LeaseStatus.Annule => "Annulé",
        _ => status.ToString()
    };
}

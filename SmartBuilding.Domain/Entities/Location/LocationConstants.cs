namespace SmartBuilding.Domain.Entities.Location;

public static class LocationConstants
{
    public static class TenantStatus
    {
        public const string Active = "Actif";
        public const string Suspended = "Suspendu";
        public const string Terminated = "Résilié";
        public const string Pending = "En attente";
        public const string Archived = "Archivé";
    }

    public static class PremiseOccupancyStatus
    {
        public const string Available = "Disponible";
        public const string Occupied = "Occupé";
        public const string Maintenance = "Maintenance";
        public const string Reserved = "Réservé";
        public const string Suspended = "Suspendu";
    }

    public static class BuildingTypes
    {
        public const string Residential = "Résidentiel";
        public const string Office = "Bureau";
        public const string Commercial = "Commercial";
        public const string MeetingRoom = "Salle réunion";
        public const string ConferenceRoom = "Salle conférence";
        public const string Mixed = "Mixte";
    }

    public static class PremiseTypes
    {
        public const string Residence = "Résidence";
        public const string Apartment = "Appartement";
        public const string Office = "Bureau";
        public const string MeetingRoom = "Salle réunion";
        public const string ConferenceRoom = "Salle conférence";
        public const string Commercial = "Local commercial";
        public const string Coworking = "Coworking";
        public const string Store = "Magasin";
        public const string Warehouse = "Entrepôt";
    }

    public static class ContractTypes
    {
        public const string Residence = "Résidence";
        public const string Office = "Bureau de travail";
        public const string MeetingRoom = "Salle réunion";
        public const string ConferenceRoom = "Salle conférence";
        public const string Residential = "Appartement résidentiel";
        public const string Commercial = "Local commercial";
        public const string Coworking = "Coworking";
        public const string Warehouse = "Entrepôt";

        public static readonly string[] All =
        [
            Residence,
            Residential,
            Office,
            MeetingRoom,
            ConferenceRoom,
            Commercial,
            Coworking,
            Warehouse
        ];
    }

    public static string DefaultContractType => ContractTypes.Residence;
    public static string DefaultPremiseType => PremiseTypes.Residence;

    public static class PaymentStatus
    {
        public const string Paid = "Payé";
        public const string Pending = "En attente";
        public const string Late = "Retard";
        public const string Partial = "Partiel";
        public const string Cancelled = "Annulé";
    }

    public static class GuaranteeStatus
    {
        public const string Active = "Active";
        public const string Refunded = "Remboursée";
        public const string Partial = "Partielle";
        public const string Suspended = "Suspendue";
    }

    public static class LandlordStatus
    {
        public const string Active = "Actif";
        public const string Inactive = "Inactif";
        public const string Archived = "Archivé";
    }

    public static class LandlordTypes
    {
        public const string Individual = "Particulier";
        public const string Company = "Société";
    }

    public static class DependentRelationships
    {
        public const string Spouse = "Conjoint(e)";
        public const string Child = "Enfant";
        public const string Parent = "Parent";
        public const string Other = "Autre";
    }

    public static class TenantGenders
    {
        public const string Male = "Homme";
        public const string Female = "Femme";
        public const string Other = "Autre";
    }

    public static class TenantMaritalStatuses
    {
        public const string Single = "Célibataire";
        public const string Married = "Marié(e)";
        public const string Divorced = "Divorcé(e)";
        public const string Widowed = "Veuf(ve)";
        public const string UnionLibre = "Union libre";
        public const string Separated = "Séparé(e)";
    }

    public static class TenantCategories
    {
        public const string Individual = "Particulier";
        public const string Company = "Société";
    }
}

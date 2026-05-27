namespace SmartBuilding.Domain.Entities.Finance;

/// <summary>
/// Catégories et sources alignées sur la gestion locative (vision financière SBMS).
/// </summary>
public static class FinanceConstants
{
    public const string SourceLocations = "Locations";

    /// <summary>Recette opérationnelle principale — encaissements loyer.</summary>
    public const string CategoryRent = "Loyers";

    /// <summary>Encaissement de dépôt de garantie (passif locataire).</summary>
    public const string CategoryGuarantee = "Caution";

    /// <summary>Sortie trésorerie — restitution de garantie.</summary>
    public const string CategoryGuaranteeRefund = "Remboursement caution";

    public const string RecordedByLocations = "SBMS — Locations";

    public const string CategorySalaries = "Salaires";
    public const string SourcePersonnel = "Personnel";
    public const string RecordedByPersonnel = "SBMS — Personnel";

    public const string SourceFinances = "Finances";
    public const string SourceConsumptions = "Consommations";
    public const string SourceIncidents = "Incidents";
    public const string SourceTechnique = "Technique";
    public const string RecordedByFinances = "SBMS — Finances";
    public const string RecordedByConsumptions = "SBMS — Consommations";
    public const string RecordedByIncidents = "SBMS — Incidents";
    public const string RecordedByTechnique = "SBMS — Technique";
    public const string CategoryEnergy = "Énergie";
    public const string CategoryMaintenance = "Maintenance";
    public const string CategoryIncident = "Incidents";
}

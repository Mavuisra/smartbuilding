namespace SmartBuilding.Desktop.WPF.Models;

public class RapportsPageData
{
    public IReadOnlyList<RapportPersonnelRow> Personnel { get; init; } = [];
    public IReadOnlyList<RapportLoyerRow> Loyers { get; init; } = [];
    public IReadOnlyList<RapportDepenseRow> Depenses { get; init; } = [];
    public IReadOnlyList<RapportConsommationRow> Consommations { get; init; } = [];
    public RapportFinancierSummary Financier { get; init; } = new();
    public IReadOnlyList<RapportFinancierLigne> FinancierLignes { get; init; } = [];
    public IReadOnlyList<RapportContratRow> Contrats { get; init; } = [];
    public IReadOnlyList<RapportIncidentRow> Incidents { get; init; } = [];
    public IReadOnlyList<RapportVisiteRow> Visites { get; init; } = [];
    public IReadOnlyList<RapportActiviteRow> Activites { get; init; } = [];
    public IReadOnlyList<string> DepartementFilters { get; init; } = [];
    public IReadOnlyList<string> StatutPersonnelFilters { get; init; } = [];
    public IReadOnlyList<string> PresenceFilters { get; init; } = [];
    public IReadOnlyList<string> StatutLoyerFilters { get; init; } = [];
    public IReadOnlyList<string> CategorieDepenseFilters { get; init; } = [];
    public IReadOnlyList<string> CategorieConsoFilters { get; init; } = [];
    public IReadOnlyList<string> TypeContratFilters { get; init; } = [];
    public IReadOnlyList<string> StatutContratFilters { get; init; } = [];
    public IReadOnlyList<string> StatutIncidentFilters { get; init; } = [];
    public IReadOnlyList<string> ModuleActiviteFilters { get; init; } = [];
    public IReadOnlyList<string> UtilisateurActiviteFilters { get; init; } = [];
    public IReadOnlyList<string> MonthlyLabels { get; init; } = [];
    public IReadOnlyList<decimal> MonthlyRevenues { get; init; } = [];
    public IReadOnlyList<decimal> MonthlyExpenses { get; init; } = [];
    public IReadOnlyList<decimal> MonthlyTreasury { get; init; } = [];
    public IReadOnlyList<decimal> MonthlyConsumptionCosts { get; init; } = [];
}

public class RapportPersonnelRow
{
    public Guid Id { get; init; }
    public string? PhotoPath { get; init; }
    public string Matricule { get; init; } = string.Empty;
    public string NomComplet { get; init; } = string.Empty;
    public string Fonction { get; init; } = string.Empty;
    public string Departement { get; init; } = string.Empty;
    public DateTime DateEmbauche { get; init; }
    public string DateEmbaucheDisplay { get; init; } = string.Empty;
    public string Anciennete { get; init; } = string.Empty;
    public int Presences { get; init; }
    public int Absences { get; init; }
    public int Retards { get; init; }
    public decimal Salaire { get; init; }
    public string SalaireDisplay { get; init; } = string.Empty;
    public string StatutPaiement { get; init; } = string.Empty;
    public string DernierPaiement { get; init; } = string.Empty;
    public string Statut { get; init; } = string.Empty;
    public string Observations { get; init; } = string.Empty;
    public string PresenceResume { get; init; } = string.Empty;
}

public class RapportLoyerRow
{
    public Guid Id { get; init; }
    public string? PhotoPath { get; init; }
    public string NomComplet { get; init; } = string.Empty;
    public string Profession { get; init; } = string.Empty;
    public string Telephone { get; init; } = string.Empty;
    public string Appartement { get; init; } = string.Empty;
    public string Batiment { get; init; } = string.Empty;
    public string TypeContrat { get; init; } = string.Empty;
    public string Periode { get; init; } = string.Empty;
    public decimal MontantLoyer { get; init; }
    public string MontantLoyerDisplay { get; init; } = string.Empty;
    public decimal MontantDu { get; init; }
    public decimal MontantPaye { get; init; }
    public string MontantDuDisplay { get; init; } = string.Empty;
    public string MontantPayeDisplay { get; init; } = string.Empty;
    public string PenaliteDisplay { get; init; } = string.Empty;
    public decimal Garantie { get; init; }
    public string GarantieDisplay { get; init; } = string.Empty;
    public string DateEcheance { get; init; } = string.Empty;
    public string DernierPaiement { get; init; } = string.Empty;
    public string ModePaiement { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
    public string NumeroRecu { get; init; } = string.Empty;
    public string StatutPaiement { get; init; } = string.Empty;
    public string StatutBadgeBackground { get; init; } = "#F1F5F9";
    public string StatutBadgeForeground { get; init; } = "#475569";
    public DateTime DueDate { get; init; }
}

public class RapportDepenseRow
{
    public Guid Id { get; init; }
    public DateTime Date { get; init; }
    public string DateDisplay { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
    public string Categorie { get; init; } = string.Empty;
    public decimal Montant { get; init; }
    public string MontantDisplay { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Responsable { get; init; } = string.Empty;
    public string Service { get; init; } = string.Empty;
    public string ModePaiement { get; init; } = string.Empty;
    public string Justificatif { get; init; } = string.Empty;
    public string StatutValidation { get; init; } = string.Empty;
    public string CreePar { get; init; } = string.Empty;
    public string ValidePar { get; init; } = string.Empty;
    public string DateValidation { get; init; } = string.Empty;
    public string Historique { get; init; } = string.Empty;
}

public class RapportConsommationRow
{
    public Guid Id { get; init; }
    public DateTime Date { get; init; }
    public string DateDisplay { get; init; } = string.Empty;
    public string PeriodeDebut { get; init; } = string.Empty;
    public string PeriodeFin { get; init; } = string.Empty;
    public string Categorie { get; init; } = string.Empty;
    public string Equipement { get; init; } = string.Empty;
    public string Batiment { get; init; } = string.Empty;
    public decimal Quantite { get; init; }
    public string Unite { get; init; } = string.Empty;
    public string QuantiteDisplay { get; init; } = string.Empty;
    public decimal CoutUnitaire { get; init; }
    public string CoutUnitaireDisplay { get; init; } = string.Empty;
    public decimal CoutTotal { get; init; }
    public string CoutTotalDisplay { get; init; } = string.Empty;
    public string Devise { get; init; } = string.Empty;
    public string Compteur { get; init; } = string.Empty;
    public string TypePeriode { get; init; } = string.Empty;
    public string Statut { get; init; } = string.Empty;
    public string VariationDisplay { get; init; } = string.Empty;
    public string Anomalie { get; init; } = string.Empty;
    public string Responsable { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
}

public class RapportFinancierLigne
{
    public Guid Id { get; init; }
    public DateTime Date { get; init; }
    public string DateDisplay { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Categorie { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Montant { get; init; }
    public string MontantDisplay { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string ModePaiement { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
    public string Statut { get; init; } = string.Empty;
    public string EnregistrePar { get; init; } = string.Empty;
}

public class RapportFinancierSummary
{
    public decimal LoyersEncaisses { get; init; }
    public decimal Garanties { get; init; }
    public decimal Services { get; init; }
    public decimal RevenusDivers { get; init; }
    public decimal Salaires { get; init; }
    public decimal Consommations { get; init; }
    public decimal Maintenance { get; init; }
    public decimal Fournisseurs { get; init; }
    public decimal ChargesDiverses { get; init; }
    public decimal TotalEntrees { get; init; }
    public decimal TotalSorties { get; init; }
    public decimal SoldeActuel { get; init; }
    public decimal Benefice { get; init; }
    public decimal Perte { get; init; }
    public string LoyersEncaissesDisplay { get; init; } = string.Empty;
    public string GarantiesDisplay { get; init; } = string.Empty;
    public string ServicesDisplay { get; init; } = string.Empty;
    public string RevenusDiversDisplay { get; init; } = string.Empty;
    public string SalairesDisplay { get; init; } = string.Empty;
    public string ConsommationsDisplay { get; init; } = string.Empty;
    public string MaintenanceDisplay { get; init; } = string.Empty;
    public string FournisseursDisplay { get; init; } = string.Empty;
    public string ChargesDiversesDisplay { get; init; } = string.Empty;
    public string TotalEntreesDisplay { get; init; } = string.Empty;
    public string TotalSortiesDisplay { get; init; } = string.Empty;
    public string SoldeActuelDisplay { get; init; } = string.Empty;
    public string BeneficeDisplay { get; init; } = string.Empty;
    public string PerteDisplay { get; init; } = string.Empty;
}

public class RapportContratRow
{
    public Guid Id { get; init; }
    public string NumeroContrat { get; init; } = string.Empty;
    public string Locataire { get; init; } = string.Empty;
    public string Appartement { get; init; } = string.Empty;
    public string DateDebut { get; init; } = string.Empty;
    public string DateFin { get; init; } = string.Empty;
    public string Statut { get; init; } = string.Empty;
    public string TypeContrat { get; init; } = string.Empty;
    public string ResponsableValidation { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
}

public class RapportIncidentRow
{
    public Guid Id { get; init; }
    public DateTime Date { get; init; }
    public string DateDisplay { get; init; } = string.Empty;
    public string Incident { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Responsable { get; init; } = string.Empty;
    public decimal CoutIntervention { get; init; }
    public string CoutInterventionDisplay { get; init; } = string.Empty;
    public string Statut { get; init; } = string.Empty;
    public string DateResolution { get; init; } = string.Empty;
}

public class RapportVisiteRow
{
    public Guid Id { get; init; }
    public string NomVisiteur { get; init; } = string.Empty;
    public string Motif { get; init; } = string.Empty;
    public string PersonneVisitee { get; init; } = string.Empty;
    public string HeureEntree { get; init; } = string.Empty;
    public string HeureSortie { get; init; } = string.Empty;
    public string DureePresence { get; init; } = string.Empty;
    public DateTime CheckInAt { get; init; }
}

public class RapportActiviteRow
{
    public Guid Id { get; init; }
    public string Utilisateur { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string Module { get; init; } = string.Empty;
    public string Date { get; init; } = string.Empty;
    public string Heure { get; init; } = string.Empty;
    public string AdresseIp { get; init; } = string.Empty;
    public string Appareil { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; }
}

public class RapportsSavedFilters
{
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int SelectedSectionTab { get; set; }
    public string SearchQuery { get; set; } = string.Empty;
    public string FilterDepartement { get; set; } = "Tous";
    public string FilterStatutPersonnel { get; set; } = "Tous";
    public string FilterPresence { get; set; } = "Tous";
    public string FilterStatutLoyer { get; set; } = "Tous";
    public string FilterCategorieDepense { get; set; } = "Toutes";
    public string FilterCategorieConso { get; set; } = "Toutes";
    public string FilterTypeContrat { get; set; } = "Tous";
    public string FilterStatutContrat { get; set; } = "Tous";
    public string FilterStatutIncident { get; set; } = "Tous";
    public string FilterModuleActivite { get; set; } = "Tous";
    public string FilterUtilisateurActivite { get; set; } = "Tous";
}

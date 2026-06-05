using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class RapportsViewModel : BaseViewModel
{
    private readonly RapportsService _rapportsService;
    private readonly RapportsReportPdfService _pdfService;
    private readonly SessionService _session;

    private RapportsPageData? _pageData;
    private List<RapportPersonnelRow> _allPersonnel = [];
    private List<RapportLoyerRow> _allLoyers = [];
    private List<RapportDepenseRow> _allDepenses = [];
    private List<RapportConsommationRow> _allConsommations = [];
    private List<RapportContratRow> _allContrats = [];
    private List<RapportIncidentRow> _allIncidents = [];
    private List<RapportVisiteRow> _allVisites = [];
    private List<RapportActiviteRow> _allActivites = [];

    public const string AllDepartements = "Tous";
    public const string AllStatuts = "Tous";
    public const string AllCategories = "Toutes";
    public const string AllPresences = "Tous";

    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _userRole = string.Empty;
    [ObservableProperty] private string _userInitials = "AD";
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private int _selectedSectionTab;
    [ObservableProperty] private DateTime _dateFrom = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime _dateTo = DateTime.Today;

    [ObservableProperty] private string _filterDepartement = AllDepartements;
    [ObservableProperty] private string _filterStatutPersonnel = AllStatuts;
    [ObservableProperty] private string _filterPresence = AllPresences;
    [ObservableProperty] private string _filterStatutLoyer = AllStatuts;
    [ObservableProperty] private string _filterCategorieDepense = AllCategories;
    [ObservableProperty] private string _filterCategorieConso = AllCategories;
    [ObservableProperty] private string _filterTypeContrat = AllStatuts;
    [ObservableProperty] private string _filterStatutContrat = AllStatuts;
    [ObservableProperty] private string _filterStatutIncident = AllStatuts;
    [ObservableProperty] private string _filterModuleActivite = AllStatuts;
    [ObservableProperty] private string _filterUtilisateurActivite = AllStatuts;

    [ObservableProperty] private string _kpi1Label = "—";
    [ObservableProperty] private string _kpi1Value = "—";
    [ObservableProperty] private string _kpi2Label = "—";
    [ObservableProperty] private string _kpi2Value = "—";
    [ObservableProperty] private string _kpi3Label = "—";
    [ObservableProperty] private string _kpi3Value = "—";
    [ObservableProperty] private string _kpi4Label = "—";
    [ObservableProperty] private string _kpi4Value = "—";
    [ObservableProperty] private string _kpi5Label = "—";
    [ObservableProperty] private string _kpi5Value = "—";

    [ObservableProperty] private RapportFinancierSummary _financierSummary = new();
    [ObservableProperty] private ISeries[] _revenueSeries = [];
    [ObservableProperty] private ISeries[] _expenseSeries = [];
    [ObservableProperty] private ISeries[] _treasurySeries = [];
    [ObservableProperty] private ISeries[] _consumptionSeries = [];
    [ObservableProperty] private string[] _chartLabels = [];

    public ObservableCollection<RapportPersonnelRow> PersonnelRows { get; } = [];
    public ObservableCollection<RapportLoyerRow> LoyerRows { get; } = [];
    public ObservableCollection<RapportDepenseRow> DepenseRows { get; } = [];
    public ObservableCollection<RapportConsommationRow> ConsommationRows { get; } = [];
    public ObservableCollection<RapportContratRow> ContratRows { get; } = [];
    public ObservableCollection<RapportIncidentRow> IncidentRows { get; } = [];
    public ObservableCollection<RapportVisiteRow> VisiteRows { get; } = [];
    public ObservableCollection<RapportActiviteRow> ActiviteRows { get; } = [];

    public ObservableCollection<string> SectionTabs { get; } =
    [
        "Personnel", "Loyers", "Dépenses", "Consommations", "Financier",
        "Contrats", "Incidents", "Visites", "Activités"
    ];

    public ObservableCollection<string> DepartementFilters { get; } = [AllDepartements];
    public ObservableCollection<string> StatutPersonnelFilters { get; } = [AllStatuts];
    public ObservableCollection<string> PresenceFilters { get; } = [AllPresences];
    public ObservableCollection<string> StatutLoyerFilters { get; } = [AllStatuts];
    public ObservableCollection<string> CategorieDepenseFilters { get; } = [AllCategories];
    public ObservableCollection<string> CategorieConsoFilters { get; } = [AllCategories];
    public ObservableCollection<string> TypeContratFilters { get; } = [AllStatuts];
    public ObservableCollection<string> StatutContratFilters { get; } = [AllStatuts];
    public ObservableCollection<string> StatutIncidentFilters { get; } = [AllStatuts];
    public ObservableCollection<string> ModuleActiviteFilters { get; } = [AllStatuts];
    public ObservableCollection<string> UtilisateurActiviteFilters { get; } = [AllStatuts];

    public RapportsViewModel(
        RapportsService rapportsService,
        RapportsReportPdfService pdfService,
        SessionService session)
    {
        _rapportsService = rapportsService;
        _pdfService = pdfService;
        _session = session;
        UserName = session.CurrentUser?.FullName ?? "Administrateur";
        UserRole = session.CurrentUser?.Role ?? "Administrateur";
        UserInitials = GetInitials(UserName);
        ApplySavedFilters();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var data = await _rapportsService.LoadAsync(DateFrom, DateTo);
            _pageData = data;
            _allPersonnel = data.Personnel.ToList();
            _allLoyers = data.Loyers.ToList();
            _allDepenses = data.Depenses.ToList();
            _allConsommations = data.Consommations.ToList();
            _allContrats = data.Contrats.ToList();
            _allIncidents = data.Incidents.ToList();
            _allVisites = data.Visites.ToList();
            _allActivites = data.Activites.ToList();
            FinancierSummary = data.Financier;

            ReplaceFilters(DepartementFilters, data.DepartementFilters);
            ReplaceFilters(StatutPersonnelFilters, data.StatutPersonnelFilters);
            ReplaceFilters(PresenceFilters, data.PresenceFilters);
            ReplaceFilters(StatutLoyerFilters, data.StatutLoyerFilters);
            ReplaceFilters(CategorieDepenseFilters, data.CategorieDepenseFilters);
            ReplaceFilters(CategorieConsoFilters, data.CategorieConsoFilters);
            ReplaceFilters(TypeContratFilters, data.TypeContratFilters);
            ReplaceFilters(StatutContratFilters, data.StatutContratFilters);
            ReplaceFilters(StatutIncidentFilters, data.StatutIncidentFilters);
            ReplaceFilters(ModuleActiviteFilters, data.ModuleActiviteFilters);
            ReplaceFilters(UtilisateurActiviteFilters, data.UtilisateurActiviteFilters);

            BuildCharts(data);
            ApplyFilters();
            StatusMessage = "Rapports chargés";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SetSectionTab(int index)
    {
        SelectedSectionTab = index;
        UpdateKpis();
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (_pageData is null) return;
        try
        {
            string path;
            if (SelectedSectionTab == 4)
            {
                path = _pdfService.ExportFinancierReport(FinancierSummary, _pageData);
            }
            else
            {
                var (title, headers, rows, kpis) = GetExportData();
                path = _pdfService.ExportSectionReport(title, kpis.labels, kpis.values, headers, rows);
            }

            StatusMessage = $"PDF exporté : {path}";
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ExportExcelAsync()
    {
        try
        {
            var (title, headers, rows, _) = GetExportData();
            var path = RapportsExportService.ExportExcel(title, headers, rows);
            StatusMessage = $"Excel exporté : {path}";
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private void PrintReport()
    {
        try
        {
            var (title, headers, rows, _) = GetExportData();
            RapportsExportService.PrintTable($"SBMS — {title}", headers, rows);
            StatusMessage = "Aperçu d'impression ouvert";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void SaveFilters()
    {
        RapportsExportService.SaveFilters(new RapportsSavedFilters
        {
            DateFrom = DateFrom,
            DateTo = DateTo,
            SelectedSectionTab = SelectedSectionTab,
            SearchQuery = SearchQuery,
            FilterDepartement = FilterDepartement,
            FilterStatutPersonnel = FilterStatutPersonnel,
            FilterPresence = FilterPresence,
            FilterStatutLoyer = FilterStatutLoyer,
            FilterCategorieDepense = FilterCategorieDepense,
            FilterCategorieConso = FilterCategorieConso,
            FilterTypeContrat = FilterTypeContrat,
            FilterStatutContrat = FilterStatutContrat,
            FilterStatutIncident = FilterStatutIncident,
            FilterModuleActivite = FilterModuleActivite,
            FilterUtilisateurActivite = FilterUtilisateurActivite
        });
        StatusMessage = "Filtres sauvegardés";
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilters();
    partial void OnSelectedSectionTabChanged(int value) => UpdateKpis();
    partial void OnFilterDepartementChanged(string value) => ApplyFilters();
    partial void OnFilterStatutPersonnelChanged(string value) => ApplyFilters();
    partial void OnFilterPresenceChanged(string value) => ApplyFilters();
    partial void OnFilterStatutLoyerChanged(string value) => ApplyFilters();
    partial void OnFilterCategorieDepenseChanged(string value) => ApplyFilters();
    partial void OnFilterCategorieConsoChanged(string value) => ApplyFilters();
    partial void OnFilterTypeContratChanged(string value) => ApplyFilters();
    partial void OnFilterStatutContratChanged(string value) => ApplyFilters();
    partial void OnFilterStatutIncidentChanged(string value) => ApplyFilters();
    partial void OnFilterModuleActiviteChanged(string value) => ApplyFilters();
    partial void OnFilterUtilisateurActiviteChanged(string value) => ApplyFilters();

    partial void OnDateFromChanged(DateTime value) => _ = ReloadIfLoaded();
    partial void OnDateToChanged(DateTime value) => _ = ReloadIfLoaded();

    private async Task ReloadIfLoaded()
    {
        if (_pageData is not null)
            await LoadAsync();
    }

    private void ApplyFilters()
    {
        if (_pageData is null) return;

        var q = SearchQuery.Trim();

        ReplaceCollection(PersonnelRows, FilterPersonnel(q));
        ReplaceCollection(LoyerRows, FilterLoyers(q));
        ReplaceCollection(DepenseRows, FilterDepenses(q));
        ReplaceCollection(ConsommationRows, FilterConsommations(q));
        ReplaceCollection(ContratRows, FilterContrats(q));
        ReplaceCollection(IncidentRows, FilterIncidents(q));
        ReplaceCollection(VisiteRows, FilterVisites(q));
        ReplaceCollection(ActiviteRows, FilterActivites(q));
        UpdateKpis();
    }

    private IEnumerable<RapportPersonnelRow> FilterPersonnel(string q) =>
        _allPersonnel.Where(p =>
            (FilterDepartement == AllDepartements || p.Departement.Equals(FilterDepartement, StringComparison.OrdinalIgnoreCase)) &&
            (FilterStatutPersonnel == AllStatuts || p.Statut.Equals(FilterStatutPersonnel, StringComparison.OrdinalIgnoreCase)) &&
            (FilterPresence == AllPresences || p.PresenceResume.Equals(FilterPresence, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(q) || Contains(p.NomComplet, q) || Contains(p.Matricule, q) || Contains(p.Fonction, q)));

    private IEnumerable<RapportLoyerRow> FilterLoyers(string q) =>
        _allLoyers.Where(l =>
            (FilterStatutLoyer == AllStatuts || l.StatutPaiement.Equals(FilterStatutLoyer, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(q) || Contains(l.NomComplet, q) || Contains(l.Appartement, q) || Contains(l.Batiment, q)));

    private IEnumerable<RapportDepenseRow> FilterDepenses(string q) =>
        _allDepenses.Where(d =>
            (FilterCategorieDepense == AllCategories || d.Categorie.Equals(FilterCategorieDepense, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(q) || Contains(d.Description, q) || Contains(d.Categorie, q) || Contains(d.Responsable, q)));

    private IEnumerable<RapportConsommationRow> FilterConsommations(string q) =>
        _allConsommations.Where(c =>
            (FilterCategorieConso == AllCategories || c.Categorie.Equals(FilterCategorieConso, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(q) || Contains(c.Categorie, q) || Contains(c.Responsable, q)));

    private IEnumerable<RapportContratRow> FilterContrats(string q) =>
        _allContrats.Where(c =>
            (FilterTypeContrat == AllStatuts || c.TypeContrat.Equals(FilterTypeContrat, StringComparison.OrdinalIgnoreCase)) &&
            (FilterStatutContrat == AllStatuts || c.Statut.Equals(FilterStatutContrat, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(q) || Contains(c.Locataire, q) || Contains(c.NumeroContrat, q)));

    private IEnumerable<RapportIncidentRow> FilterIncidents(string q) =>
        _allIncidents.Where(i =>
            (FilterStatutIncident == AllStatuts || i.Statut.Equals(FilterStatutIncident, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(q) || Contains(i.Incident, q) || Contains(i.Description, q)));

    private IEnumerable<RapportVisiteRow> FilterVisites(string q) =>
        _allVisites.Where(v =>
            string.IsNullOrEmpty(q) || Contains(v.NomVisiteur, q) || Contains(v.PersonneVisitee, q) || Contains(v.Motif, q));

    private IEnumerable<RapportActiviteRow> FilterActivites(string q) =>
        _allActivites.Where(a =>
            (FilterModuleActivite == AllStatuts || a.Module.Equals(FilterModuleActivite, StringComparison.OrdinalIgnoreCase)) &&
            (FilterUtilisateurActivite == AllStatuts || a.Utilisateur.Equals(FilterUtilisateurActivite, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(q) || Contains(a.Action, q) || Contains(a.Utilisateur, q) || Contains(a.Module, q)));

    private void UpdateKpis()
    {
        switch (SelectedSectionTab)
        {
            case 0:
                SetKpis(
                    ("Effectif total", PersonnelRows.Count.ToString()),
                    ("Présents", PersonnelRows.Count(p => p.PresenceResume == "Présent").ToString()),
                    ("Absents", PersonnelRows.Sum(p => p.Absences).ToString()),
                    ("Retards", PersonnelRows.Sum(p => p.Retards).ToString()),
                    ("Masse salariale", MoneyFormatter.Format(PersonnelRows.Sum(p => p.Salaire))));
                break;
            case 1:
                var attendu = LoyerRows.Sum(l => l.MontantLoyer);
                var encaisse = LoyerRows.Where(l => l.StatutPaiement == "Payé").Sum(l => l.MontantLoyer);
                var retard = LoyerRows.Where(l => l.StatutPaiement is "En retard" or "Partiellement payé").Sum(l => l.MontantLoyer);
                var taux = attendu > 0 ? $"{encaisse / attendu * 100:F1}%" : "0%";
                SetKpis(
                    ("Loyers attendus", MoneyFormatter.Format(attendu)),
                    ("Loyers encaissés", MoneyFormatter.Format(encaisse)),
                    ("En retard", MoneyFormatter.Format(retard)),
                    ("Locataires", LoyerRows.Count.ToString()),
                    ("Taux recouvrement", taux));
                break;
            case 2:
                var today = DateTime.Today;
                SetKpis(
                    ("Dépenses du jour", MoneyFormatter.Format(DepenseRows.Where(d => d.Date.Date == today).Sum(d => d.Montant))),
                    ("Dépenses du mois", MoneyFormatter.Format(DepenseRows.Where(d => d.Date.Month == today.Month && d.Date.Year == today.Year).Sum(d => d.Montant))),
                    ("Catégories", DepenseRows.Select(d => d.Categorie).Distinct().Count().ToString()),
                    ("Responsables", DepenseRows.Select(d => d.Responsable).Distinct().Count().ToString()),
                    ("Total filtré", MoneyFormatter.Format(DepenseRows.Sum(d => d.Montant))));
                break;
            case 3:
                SetKpis(
                    ("Mensuel", MoneyFormatter.Format(ConsommationRows.Where(c => c.Date.Month == DateTime.Today.Month).Sum(c => c.CoutTotal))),
                    ("Annuel", MoneyFormatter.Format(ConsommationRows.Where(c => c.Date.Year == DateTime.Today.Year).Sum(c => c.CoutTotal))),
                    ("Enregistrements", ConsommationRows.Count.ToString()),
                    ("Catégories", ConsommationRows.Select(c => c.Categorie).Distinct().Count().ToString()),
                    ("Coût total", MoneyFormatter.Format(ConsommationRows.Sum(c => c.CoutTotal))));
                break;
            case 4:
                SetKpis(
                    ("Total entrées", FinancierSummary.TotalEntreesDisplay),
                    ("Total sorties", FinancierSummary.TotalSortiesDisplay),
                    ("Solde actuel", FinancierSummary.SoldeActuelDisplay),
                    ("Bénéfice", FinancierSummary.BeneficeDisplay),
                    ("Perte", FinancierSummary.PerteDisplay));
                break;
            case 5:
                SetKpis(
                    ("Contrats", ContratRows.Count.ToString()),
                    ("Actifs", ContratRows.Count(c => c.Statut == "Actif").ToString()),
                    ("En attente", ContratRows.Count(c => c.Statut.Contains("attente", StringComparison.OrdinalIgnoreCase)).ToString()),
                    ("Types", ContratRows.Select(c => c.TypeContrat).Distinct().Count().ToString()),
                    ("—", "—"));
                break;
            case 6:
                var resolved = IncidentRows.Count(i => i.Statut is "Résolu" or "Clôturé");
                var open = IncidentRows.Count - resolved;
                var avgDays = IncidentRows
                    .Where(i => i.DateResolution != "—")
                    .Select(i => (DateTime.TryParse(i.DateResolution, out var d) ? (d - i.Date).TotalDays : 0))
                    .DefaultIfEmpty(0).Average();
                SetKpis(
                    ("Ouverts", open.ToString()),
                    ("Résolus", resolved.ToString()),
                    ("Temps moyen (j)", $"{avgDays:F1}"),
                    ("Coût total", MoneyFormatter.Format(IncidentRows.Sum(i => i.CoutIntervention))),
                    ("Incidents", IncidentRows.Count.ToString()));
                break;
            case 7:
                SetKpis(
                    ("Visites", VisiteRows.Count.ToString()),
                    ("Aujourd'hui", VisiteRows.Count(v => v.CheckInAt.Date == DateTime.Today).ToString()),
                    ("En cours", VisiteRows.Count(v => v.HeureSortie == "—").ToString()),
                    ("—", "—"),
                    ("—", "—"));
                break;
            case 8:
                SetKpis(
                    ("Actions", ActiviteRows.Count.ToString()),
                    ("Utilisateurs", ActiviteRows.Select(a => a.Utilisateur).Distinct().Count().ToString()),
                    ("Modules", ActiviteRows.Select(a => a.Module).Distinct().Count().ToString()),
                    ("Aujourd'hui", ActiviteRows.Count(a => a.OccurredAt.Date == DateTime.Today).ToString()),
                    ("—", "—"));
                break;
        }
    }

    private void SetKpis(params (string label, string value)[] kpis)
    {
        Kpi1Label = kpis.Length > 0 ? kpis[0].label : "—";
        Kpi1Value = kpis.Length > 0 ? kpis[0].value : "—";
        Kpi2Label = kpis.Length > 1 ? kpis[1].label : "—";
        Kpi2Value = kpis.Length > 1 ? kpis[1].value : "—";
        Kpi3Label = kpis.Length > 2 ? kpis[2].label : "—";
        Kpi3Value = kpis.Length > 2 ? kpis[2].value : "—";
        Kpi4Label = kpis.Length > 3 ? kpis[3].label : "—";
        Kpi4Value = kpis.Length > 3 ? kpis[3].value : "—";
        Kpi5Label = kpis.Length > 4 ? kpis[4].label : "—";
        Kpi5Value = kpis.Length > 4 ? kpis[4].value : "—";
    }

    private void BuildCharts(RapportsPageData data)
    {
        ChartLabels = data.MonthlyLabels.ToArray();
        var labels = ChartLabels;

        RevenueSeries =
        [
            new ColumnSeries<decimal>
            {
                Name = "Revenus",
                Values = data.MonthlyRevenues.ToArray(),
                Fill = new SolidColorPaint(SKColor.Parse("#22C55E")),
                Stroke = null
            }
        ];

        ExpenseSeries =
        [
            new ColumnSeries<decimal>
            {
                Name = "Dépenses",
                Values = data.MonthlyExpenses.ToArray(),
                Fill = new SolidColorPaint(SKColor.Parse("#EF4444")),
                Stroke = null
            }
        ];

        TreasurySeries =
        [
            new LineSeries<decimal>
            {
                Name = "Trésorerie",
                Values = data.MonthlyTreasury.ToArray(),
                Stroke = new SolidColorPaint(SKColor.Parse("#2563EB")) { StrokeThickness = 2 },
                Fill = null,
                GeometrySize = 6
            }
        ];

        ConsumptionSeries =
        [
            new LineSeries<decimal>
            {
                Name = "Consommations",
                Values = data.MonthlyConsumptionCosts.ToArray(),
                Stroke = new SolidColorPaint(SKColor.Parse("#F59E0B")) { StrokeThickness = 2 },
                Fill = null,
                GeometrySize = 6
            }
        ];
    }

    private (string title, List<string> headers, List<string[]> rows, (List<string> labels, List<string> values) kpis) GetExportData()
    {
        var labels = new List<string> { Kpi1Label, Kpi2Label, Kpi3Label, Kpi4Label, Kpi5Label }
            .Where(l => l != "—").ToList();
        var values = new List<string> { Kpi1Value, Kpi2Value, Kpi3Value, Kpi4Value, Kpi5Value }
            .Take(labels.Count).ToList();

        return SelectedSectionTab switch
        {
            0 => ("Rapport Personnel",
                ["Matricule", "Nom", "Fonction", "Département", "Embauche", "Présences", "Absences", "Retards", "Salaire", "Statut paiement"],
                PersonnelRows.Select(p => new[] { p.Matricule, p.NomComplet, p.Fonction, p.Departement, p.DateEmbaucheDisplay,
                    p.Presences.ToString(), p.Absences.ToString(), p.Retards.ToString(), p.SalaireDisplay, p.StatutPaiement }).ToList(),
                (labels, values)),
            1 => ("Rapport Loyers",
                ["Locataire", "Appartement", "Bâtiment", "Loyer", "Échéance", "Dernier paiement", "Statut"],
                LoyerRows.Select(l => new[] { l.NomComplet, l.Appartement, l.Batiment, l.MontantLoyerDisplay, l.DateEcheance, l.DernierPaiement, l.StatutPaiement }).ToList(),
                (labels, values)),
            2 => ("Rapport Dépenses",
                ["Date", "Catégorie", "Montant", "Description", "Responsable", "Statut", "Créé par", "Validé par"],
                DepenseRows.Select(d => new[] { d.DateDisplay, d.Categorie, d.MontantDisplay, d.Description, d.Responsable, d.StatutValidation, d.CreePar, d.ValidePar }).ToList(),
                (labels, values)),
            3 => ("Rapport Consommations",
                ["Date", "Catégorie", "Quantité", "Coût unit.", "Coût total", "Responsable"],
                ConsommationRows.Select(c => new[] { c.DateDisplay, c.Categorie, $"{c.Quantite} {c.Unite}", c.CoutUnitaireDisplay, c.CoutTotalDisplay, c.Responsable }).ToList(),
                (labels, values)),
            5 => ("Rapport Contrats",
                ["N° contrat", "Locataire", "Appartement", "Début", "Fin", "Type", "Statut", "Validé par"],
                ContratRows.Select(c => new[] { c.NumeroContrat, c.Locataire, c.Appartement, c.DateDebut, c.DateFin, c.TypeContrat, c.Statut, c.ResponsableValidation }).ToList(),
                (labels, values)),
            6 => ("Rapport Incidents",
                ["Date", "Incident", "Responsable", "Coût", "Statut", "Résolution"],
                IncidentRows.Select(i => new[] { i.DateDisplay, i.Incident, i.Responsable, i.CoutInterventionDisplay, i.Statut, i.DateResolution }).ToList(),
                (labels, values)),
            7 => ("Rapport Visites",
                ["Visiteur", "Motif", "Personne visitée", "Entrée", "Sortie", "Durée"],
                VisiteRows.Select(v => new[] { v.NomVisiteur, v.Motif, v.PersonneVisitee, v.HeureEntree, v.HeureSortie, v.DureePresence }).ToList(),
                (labels, values)),
            8 => ("Rapport Activités",
                ["Utilisateur", "Action", "Module", "Date", "Heure", "IP", "Appareil"],
                ActiviteRows.Select(a => new[] { a.Utilisateur, a.Action, a.Module, a.Date, a.Heure, a.AdresseIp, a.Appareil }).ToList(),
                (labels, values)),
            _ => ("Rapport Financier", new List<string> { "Libellé", "Montant" },
                new List<string[]>
                {
                    new[] { "Loyers encaissés", FinancierSummary.LoyersEncaissesDisplay },
                    new[] { "Total entrées", FinancierSummary.TotalEntreesDisplay },
                    new[] { "Total sorties", FinancierSummary.TotalSortiesDisplay },
                    new[] { "Solde", FinancierSummary.SoldeActuelDisplay }
                },
                (labels, values))
        };
    }

    private void ApplySavedFilters()
    {
        var saved = RapportsExportService.LoadFilters();
        if (saved is null) return;
        if (saved.DateFrom.HasValue) DateFrom = saved.DateFrom.Value;
        if (saved.DateTo.HasValue) DateTo = saved.DateTo.Value;
        SelectedSectionTab = saved.SelectedSectionTab;
        SearchQuery = saved.SearchQuery;
        FilterDepartement = saved.FilterDepartement;
        FilterStatutPersonnel = saved.FilterStatutPersonnel;
        FilterPresence = saved.FilterPresence;
        FilterStatutLoyer = saved.FilterStatutLoyer;
        FilterCategorieDepense = saved.FilterCategorieDepense;
        FilterCategorieConso = saved.FilterCategorieConso;
        FilterTypeContrat = saved.FilterTypeContrat;
        FilterStatutContrat = saved.FilterStatutContrat;
        FilterStatutIncident = saved.FilterStatutIncident;
        FilterModuleActivite = saved.FilterModuleActivite;
        FilterUtilisateurActivite = saved.FilterUtilisateurActivite;
    }

    private static void ReplaceFilters(ObservableCollection<string> target, IReadOnlyList<string> source)
    {
        target.Clear();
        foreach (var item in source)
            target.Add(item);
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
            target.Add(item);
    }

    private static bool Contains(string? haystack, string needle) =>
        (haystack ?? "").Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
            : name.Length >= 2 ? name[..2].ToUpperInvariant() : "AD";
    }
}

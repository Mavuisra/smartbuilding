using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Domain.Entities.Personnel;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Services;

namespace SmartBuilding.Desktop.WPF.Services;

public class RapportsService
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    private readonly SmartBuildingDbContext _db;
    private readonly ActivityLogModuleService _activityLog;
    private readonly FinanceLedgerService _financeLedger;

    public RapportsService(
        SmartBuildingDbContext db,
        ActivityLogModuleService activityLog,
        FinanceLedgerService financeLedger)
    {
        _db = db;
        _activityLog = activityLog;
        _financeLedger = financeLedger;
    }

    public async Task<RapportsPageData> LoadAsync(
        DateTime dateFrom,
        DateTime dateTo,
        CancellationToken cancellationToken = default)
    {
        await _financeLedger.ReconcileAllAsync(cancellationToken);

        var from = dateFrom.Date;
        var to = dateTo.Date.AddDays(1).AddTicks(-1);

        var personnel = await LoadPersonnelAsync(from, to, cancellationToken);
        var loyers = await LoadLoyersAsync(from, to, cancellationToken);
        var depenses = await LoadDepensesAsync(from, to, cancellationToken);
        var consommations = await LoadConsommationsAsync(from, to, cancellationToken);
        var financier = await LoadFinancierAsync(from, to, cancellationToken);
        var contrats = await LoadContratsAsync(from, to, cancellationToken);
        var incidents = await LoadIncidentsAsync(from, to, cancellationToken);
        var visites = await LoadVisitesAsync(from, to, cancellationToken);
        var activites = await LoadActivitesAsync(from, to, cancellationToken);

        var chartStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11);
        var monthlyLabels = new List<string>();
        var monthlyRevenues = new List<decimal>();
        var monthlyExpenses = new List<decimal>();
        var monthlyTreasury = new List<decimal>();
        var monthlyConso = new List<decimal>();

        var allTx = await _db.FinancialTransactions
            .Where(t => t.TransactionDate >= chartStart)
            .ToListAsync(cancellationToken);

        var allRent = await _db.RentPayments.ToListAsync(cancellationToken);
        var allConso = await _db.ConsumptionRecords
            .Where(c => c.PeriodStart >= chartStart)
            .ToListAsync(cancellationToken);

        decimal running = 0;
        for (var i = 0; i < 12; i++)
        {
            var m = chartStart.AddMonths(i);
            monthlyLabels.Add(m.ToString("MMM yy", Fr));

            var rent = allRent.Where(p => p.Year == m.Year && p.Month == m.Month).Sum(p => p.AmountPaid);
            var rev = rent + allTx
                .Where(t => t.Type == TransactionType.Recette &&
                            t.TransactionDate.Year == m.Year && t.TransactionDate.Month == m.Month &&
                            !IsRentCategory(t.Category))
                .Sum(t => t.Amount);
            var exp = allTx
                .Where(t => t.Type == TransactionType.Depense &&
                            t.TransactionDate.Year == m.Year && t.TransactionDate.Month == m.Month)
                .Sum(t => t.Amount);

            monthlyRevenues.Add(rev);
            monthlyExpenses.Add(exp);
            running += rev - exp;
            monthlyTreasury.Add(running);
            monthlyConso.Add(allConso
                .Where(c => c.PeriodStart.Year == m.Year && c.PeriodStart.Month == m.Month)
                .Sum(c => c.Cost));
        }

        return new RapportsPageData
        {
            Personnel = personnel,
            Loyers = loyers,
            Depenses = depenses,
            Consommations = consommations,
            Financier = financier,
            Contrats = contrats,
            Incidents = incidents,
            Visites = visites,
            Activites = activites,
            DepartementFilters = BuildFilters("Tous", personnel.Select(p => p.Departement)),
            StatutPersonnelFilters = BuildFilters("Tous", personnel.Select(p => p.Statut)),
            PresenceFilters = BuildFilters("Tous", ["Présent", "Absent", "Retard", "Mixte"]),
            StatutLoyerFilters = BuildFilters("Tous", loyers.Select(l => l.StatutPaiement)),
            CategorieDepenseFilters = BuildFilters("Toutes", depenses.Select(d => d.Categorie)),
            CategorieConsoFilters = BuildFilters("Toutes", consommations.Select(c => c.Categorie)),
            TypeContratFilters = BuildFilters("Tous", contrats.Select(c => c.TypeContrat)),
            StatutContratFilters = BuildFilters("Tous", contrats.Select(c => c.Statut)),
            StatutIncidentFilters = BuildFilters("Tous", incidents.Select(i => i.Statut)),
            ModuleActiviteFilters = BuildFilters("Tous", activites.Select(a => a.Module)),
            UtilisateurActiviteFilters = BuildFilters("Tous", activites.Select(a => a.Utilisateur)),
            MonthlyLabels = monthlyLabels,
            MonthlyRevenues = monthlyRevenues,
            MonthlyExpenses = monthlyExpenses,
            MonthlyTreasury = monthlyTreasury,
            MonthlyConsumptionCosts = monthlyConso
        };
    }

    private async Task<IReadOnlyList<RapportPersonnelRow>> LoadPersonnelAsync(
        DateTime from, DateTime to, CancellationToken ct)
    {
        var employees = await _db.Employees
            .Include(e => e.Attendances)
            .Include(e => e.SalaryPayments)
            .OrderBy(e => e.LastName)
            .ToListAsync(ct);

        var rows = new List<RapportPersonnelRow>();
        foreach (var e in employees)
        {
            var att = e.Attendances.Where(a => a.Date >= from && a.Date <= to).ToList();
            var presences = att.Count(a => a.PresenceStatus == RhConstants.PresenceStatus.Present);
            var absences = att.Count(a => a.PresenceStatus == RhConstants.PresenceStatus.Absent);
            var retards = att.Count(a => a.PresenceStatus == RhConstants.PresenceStatus.Late || a.LateMinutes > 0);

            var lastPay = e.SalaryPayments
                .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
                .FirstOrDefault();

            var presenceResume = retards > 0 ? "Retard" :
                absences > presences ? "Absent" :
                presences > 0 ? "Présent" : "Mixte";

            rows.Add(new RapportPersonnelRow
            {
                Id = e.Id,
                PhotoPath = e.ProfilePhotoPath,
                Matricule = e.Matricule,
                NomComplet = $"{e.FirstName} {e.LastName}".Trim(),
                Fonction = e.Position,
                Departement = string.IsNullOrWhiteSpace(e.Department) ? "—" : e.Department,
                DateEmbauche = e.HireDate,
                DateEmbaucheDisplay = e.HireDate.ToString("dd/MM/yyyy", Fr),
                Anciennete = FormatSeniority(e.HireDate),
                Presences = presences,
                Absences = absences,
                Retards = retards,
                Salaire = e.BaseSalary,
                SalaireDisplay = MoneyFormatter.Format(e.BaseSalary),
                StatutPaiement = lastPay?.Status ?? RhConstants.PayrollStatus.Pending,
                DernierPaiement = lastPay is null ? "—" : lastPay.PaymentDate.ToString("dd/MM/yyyy", Fr),
                Statut = e.RhStatus,
                Observations = string.IsNullOrWhiteSpace(e.Notes) ? "—" : e.Notes,
                PresenceResume = presenceResume
            });
        }

        return rows;
    }

    private async Task<IReadOnlyList<RapportLoyerRow>> LoadLoyersAsync(
        DateTime from, DateTime to, CancellationToken ct)
    {
        var contracts = await _db.LeaseContracts
            .Include(c => c.Tenant)
            .Include(c => c.Premise)
            .Include(c => c.Guarantees)
            .Include(c => c.RentPayments)
            .Where(c => c.Status == LeaseStatus.Actif || c.Status == LeaseStatus.EnAttenteValidation)
            .ToListAsync(ct);

        var rows = new List<RapportLoyerRow>();
        foreach (var c in contracts)
        {
            var payment = c.RentPayments
                .Where(p => p.DueDate >= from && p.DueDate <= to)
                .OrderByDescending(p => p.DueDate)
                .FirstOrDefault()
                ?? c.RentPayments.OrderByDescending(p => p.DueDate).FirstOrDefault();

            var statut = MapRentStatus(payment);
            var (bg, fg) = StatusBadge(statut);

            rows.Add(new RapportLoyerRow
            {
                Id = c.Id,
                PhotoPath = c.Tenant?.ProfilePhotoPath,
                NomComplet = c.Tenant?.Name ?? "—",
                Profession = c.Tenant?.Profession ?? c.Tenant?.BusinessActivity ?? "—",
                Telephone = c.Tenant?.Phone ?? "—",
                Appartement = c.Premise?.Name ?? c.Premise?.Code ?? "—",
                Batiment = c.Premise?.Building ?? "—",
                TypeContrat = c.ContractType,
                MontantLoyer = c.MonthlyRent,
                MontantLoyerDisplay = MoneyFormatter.Format(c.MonthlyRent),
                Garantie = c.Deposit > 0 ? c.Deposit : c.Guarantees.Sum(g => g.Amount),
                GarantieDisplay = MoneyFormatter.Format(c.Deposit > 0 ? c.Deposit : c.Guarantees.Sum(g => g.Amount)),
                DateEcheance = payment?.DueDate.ToString("dd/MM/yyyy", Fr) ?? "—",
                DernierPaiement = payment?.PaidDate?.ToString("dd/MM/yyyy", Fr) ?? "—",
                StatutPaiement = statut,
                StatutBadgeBackground = bg,
                StatutBadgeForeground = fg
            });
        }

        return rows.OrderBy(r => r.NomComplet).ToList();
    }

    private async Task<IReadOnlyList<RapportDepenseRow>> LoadDepensesAsync(
        DateTime from, DateTime to, CancellationToken ct)
    {
        var tx = await _db.FinancialTransactions
            .Where(t => t.Type == TransactionType.Depense &&
                        t.TransactionDate >= from && t.TransactionDate <= to)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync(ct);

        return tx.Select(t =>
        {
            var hist = t.CreatedAt != t.UpdatedAt
                ? $"Modifié le {t.UpdatedAt.ToLocalTime():dd/MM/yyyy HH:mm}"
                : "Aucune modification";

            return new RapportDepenseRow
            {
                Id = t.Id,
                Date = t.TransactionDate,
                DateDisplay = t.TransactionDate.ToString("dd/MM/yyyy", Fr),
                Categorie = t.Category,
                Montant = t.Amount,
                MontantDisplay = MoneyFormatter.Format(t.Amount),
                Description = t.Description,
                Responsable = string.IsNullOrWhiteSpace(t.RecordedBy) ? "—" : t.RecordedBy,
                Service = t.Source,
                Justificatif = string.IsNullOrWhiteSpace(t.Reference) ? "—" : t.Reference!,
                StatutValidation = t.Status,
                CreePar = t.RecordedBy,
                ValidePar = t.ApprovedBy ?? "—",
                DateValidation = t.ApprovedAt?.ToString("dd/MM/yyyy HH:mm", Fr) ?? "—",
                Historique = hist
            };
        }).ToList();
    }

    private async Task<IReadOnlyList<RapportConsommationRow>> LoadConsommationsAsync(
        DateTime from, DateTime to, CancellationToken ct)
    {
        var records = await _db.ConsumptionRecords
            .Where(c => c.PeriodStart >= from && c.PeriodStart <= to)
            .OrderByDescending(c => c.PeriodStart)
            .ToListAsync(ct);

        return records.Select(c =>
        {
            var unitCost = c.Quantity > 0 ? c.Cost / c.Quantity : c.Cost;
            return new RapportConsommationRow
            {
                Id = c.Id,
                Date = c.PeriodStart,
                DateDisplay = c.PeriodStart.ToString("dd/MM/yyyy", Fr),
                Categorie = MapConsumptionType(c.Type),
                Quantite = c.Quantity,
                Unite = string.IsNullOrWhiteSpace(c.Unit) ? "—" : c.Unit,
                CoutUnitaire = unitCost,
                CoutUnitaireDisplay = MoneyFormatter.Format(unitCost),
                CoutTotal = c.Cost,
                CoutTotalDisplay = MoneyFormatter.Format(c.Cost),
                Responsable = string.IsNullOrWhiteSpace(c.Responsible) ? "—" : c.Responsible
            };
        }).ToList();
    }

    private async Task<RapportFinancierSummary> LoadFinancierAsync(
        DateTime from, DateTime to, CancellationToken ct)
    {
        var tx = await _db.FinancialTransactions
            .Where(t => t.TransactionDate >= from && t.TransactionDate <= to)
            .ToListAsync(ct);

        var rent = await _db.RentPayments
            .Where(p => p.PaidDate >= from && p.PaidDate <= to)
            .SumAsync(p => p.AmountPaid, ct);

        var loyersEncaisses = rent;
        var garanties = tx.Where(t => t.Type == TransactionType.Recette && IsGuaranteeCategory(t.Category)).Sum(t => t.Amount);
        var services = tx.Where(t => t.Type == TransactionType.Recette && t.Category.Contains("Service", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Amount);
        var revenusDivers = tx.Where(t => t.Type == TransactionType.Recette && !IsRentCategory(t.Category) && !IsGuaranteeCategory(t.Category) && !t.Category.Contains("Service", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Amount);

        var salaires = tx.Where(t => t.Type == TransactionType.Depense && (t.Category.Contains("Salaire", StringComparison.OrdinalIgnoreCase) || t.Category.Contains("Paie", StringComparison.OrdinalIgnoreCase))).Sum(t => t.Amount);
        var consommations = tx.Where(t => t.Type == TransactionType.Depense && t.Category.Contains("Consommation", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Amount);
        var maintenance = tx.Where(t => t.Type == TransactionType.Depense && (t.Category.Contains("Maintenance", StringComparison.OrdinalIgnoreCase) || t.Category.Contains("Réparation", StringComparison.OrdinalIgnoreCase))).Sum(t => t.Amount);
        var fournisseurs = tx.Where(t => t.Type == TransactionType.Depense && t.Category.Contains("Fournisseur", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Amount);
        var chargesDiverses = tx.Where(t => t.Type == TransactionType.Depense).Sum(t => t.Amount) - salaires - consommations - maintenance - fournisseurs;

        var totalEntrees = loyersEncaisses + garanties + services + revenusDivers;
        var totalSorties = salaires + consommations + maintenance + fournisseurs + Math.Max(chargesDiverses, 0);
        var cashPosition = await _financeLedger.GetCashPositionAsync(ct);
        var solde = cashPosition.AvailableBalance;
        var net = totalEntrees - totalSorties;

        return new RapportFinancierSummary
        {
            LoyersEncaisses = loyersEncaisses,
            Garanties = garanties,
            Services = services,
            RevenusDivers = revenusDivers,
            Salaires = salaires,
            Consommations = consommations,
            Maintenance = maintenance,
            Fournisseurs = fournisseurs,
            ChargesDiverses = Math.Max(chargesDiverses, 0),
            TotalEntrees = totalEntrees,
            TotalSorties = totalSorties,
            SoldeActuel = solde,
            Benefice = net > 0 ? net : 0,
            Perte = net < 0 ? Math.Abs(net) : 0,
            LoyersEncaissesDisplay = MoneyFormatter.Format(loyersEncaisses),
            GarantiesDisplay = MoneyFormatter.Format(garanties),
            ServicesDisplay = MoneyFormatter.Format(services),
            RevenusDiversDisplay = MoneyFormatter.Format(revenusDivers),
            SalairesDisplay = MoneyFormatter.Format(salaires),
            ConsommationsDisplay = MoneyFormatter.Format(consommations),
            MaintenanceDisplay = MoneyFormatter.Format(maintenance),
            FournisseursDisplay = MoneyFormatter.Format(fournisseurs),
            ChargesDiversesDisplay = MoneyFormatter.Format(Math.Max(chargesDiverses, 0)),
            TotalEntreesDisplay = MoneyFormatter.Format(totalEntrees),
            TotalSortiesDisplay = MoneyFormatter.Format(totalSorties),
            SoldeActuelDisplay = MoneyFormatter.Format(solde),
            BeneficeDisplay = MoneyFormatter.Format(net > 0 ? net : 0),
            PerteDisplay = MoneyFormatter.Format(net < 0 ? Math.Abs(net) : 0)
        };
    }

    private async Task<IReadOnlyList<RapportContratRow>> LoadContratsAsync(
        DateTime from, DateTime to, CancellationToken ct)
    {
        var contracts = await _db.LeaseContracts
            .Include(c => c.Tenant)
            .Include(c => c.Premise)
            .Where(c => c.StartDate <= to && c.EndDate >= from)
            .OrderByDescending(c => c.StartDate)
            .ToListAsync(ct);

        return contracts.Select(c => new RapportContratRow
        {
            Id = c.Id,
            NumeroContrat = string.IsNullOrWhiteSpace(c.ContractNumber) ? c.Id.ToString()[..8] : c.ContractNumber,
            Locataire = c.Tenant?.Name ?? "—",
            Appartement = c.Premise?.Name ?? c.Premise?.Code ?? "—",
            DateDebut = c.StartDate.ToString("dd/MM/yyyy", Fr),
            DateFin = c.EndDate.ToString("dd/MM/yyyy", Fr),
            Statut = MapLeaseStatus(c.Status),
            TypeContrat = c.ContractType,
            ResponsableValidation = c.ValidatedBy ?? c.CreatedBy ?? "—",
            StartDate = c.StartDate
        }).ToList();
    }

    private async Task<IReadOnlyList<RapportIncidentRow>> LoadIncidentsAsync(
        DateTime from, DateTime to, CancellationToken ct)
    {
        var incidents = await _db.Incidents
            .Where(i => i.ReportedAt >= from && i.ReportedAt <= to)
            .OrderByDescending(i => i.ReportedAt)
            .ToListAsync(ct);

        return incidents.Select(i => new RapportIncidentRow
        {
            Id = i.Id,
            Date = i.ReportedAt,
            DateDisplay = i.ReportedAt.ToString("dd/MM/yyyy", Fr),
            Incident = string.IsNullOrWhiteSpace(i.Title) ? i.Code : i.Title,
            Description = i.Description,
            Responsable = string.IsNullOrWhiteSpace(i.Responsible) ? "—" : i.Responsible,
            CoutIntervention = i.Cost,
            CoutInterventionDisplay = MoneyFormatter.Format(i.Cost),
            Statut = MapIncidentStatus(i.Status),
            DateResolution = i.ResolvedAt?.ToString("dd/MM/yyyy", Fr) ?? "—"
        }).ToList();
    }

    private async Task<IReadOnlyList<RapportVisiteRow>> LoadVisitesAsync(
        DateTime from, DateTime to, CancellationToken ct)
    {
        var visitors = await _db.Visitors
            .Where(v => v.CheckInAt >= from && v.CheckInAt <= to)
            .OrderByDescending(v => v.CheckInAt)
            .ToListAsync(ct);

        return visitors.Select(v =>
        {
            var duration = v.CheckOutAt.HasValue
                ? v.CheckOutAt.Value - v.CheckInAt
                : TimeSpan.Zero;

            return new RapportVisiteRow
            {
                Id = v.Id,
                NomVisiteur = v.FullName,
                Motif = v.Purpose,
                PersonneVisitee = v.HostName,
                HeureEntree = v.CheckInAt.ToString("HH:mm", Fr),
                HeureSortie = v.CheckOutAt?.ToString("HH:mm", Fr) ?? "—",
                DureePresence = duration.TotalMinutes > 0
                    ? $"{(int)duration.TotalHours}h {duration.Minutes:00}min"
                    : "—",
                CheckInAt = v.CheckInAt
            };
        }).ToList();
    }

    private async Task<IReadOnlyList<RapportActiviteRow>> LoadActivitesAsync(
        DateTime from, DateTime to, CancellationToken ct)
    {
        var page = await _activityLog.LoadAsync(ct);
        return page.Activities
            .Where(a => a.OccurredAt >= from && a.OccurredAt <= to)
            .Select(a => new RapportActiviteRow
            {
                Id = a.Id,
                Utilisateur = a.UserName,
                Action = a.ActionTitle,
                Module = a.Module,
                Date = a.DateDisplay,
                Heure = a.TimeDisplay,
                AdresseIp = a.IpAddress,
                Appareil = a.DeviceInfo,
                OccurredAt = a.OccurredAt
            })
            .OrderByDescending(a => a.OccurredAt)
            .ToList();
    }

    private static string MapRentStatus(RentPayment? payment)
    {
        if (payment is null)
            return "En attente";

        if (payment.PaymentStatus == LocationConstants.PaymentStatus.Paid ||
            payment.AmountPaid >= payment.AmountDue)
            return "Payé";

        if (payment.IsLate || payment.PaymentStatus == LocationConstants.PaymentStatus.Late)
            return "En retard";

        if (payment.AmountPaid > 0 && payment.AmountPaid < payment.AmountDue)
            return "Partiellement payé";

        return "En attente";
    }

    private static (string Bg, string Fg) StatusBadge(string statut) => statut switch
    {
        "Payé" => ("#DCFCE7", "#166534"),
        "En retard" => ("#FEE2E2", "#DC2626"),
        "Partiellement payé" => ("#FEF3C7", "#D97706"),
        _ => ("#F1F5F9", "#475569")
    };

    private static string MapConsumptionType(ConsumptionType type) => type switch
    {
        ConsumptionType.Electricite => "Électricité",
        ConsumptionType.Eau => "Eau",
        ConsumptionType.Internet => "Internet",
        ConsumptionType.Carburant => "Carburant",
        ConsumptionType.GroupeElectrogene => "Groupe électrogène",
        ConsumptionType.Climatisation => "Climatisation",
        ConsumptionType.Eclairage => "Éclairage",
        ConsumptionType.ReseauTechnique => "Réseau technique",
        ConsumptionType.Energie => "Énergie",
        _ => type.ToString()
    };

    private static string MapLeaseStatus(LeaseStatus status) => status switch
    {
        LeaseStatus.Actif => "Actif",
        LeaseStatus.Brouillon => "Brouillon",
        LeaseStatus.Expire => "Expiré",
        LeaseStatus.Resilie => "Résilié",
        LeaseStatus.EnAttenteValidation => "En attente validation",
        LeaseStatus.Suspendu => "Suspendu",
        LeaseStatus.Annule => "Annulé",
        _ => status.ToString()
    };

    private static string MapIncidentStatus(IncidentStatus status) => status switch
    {
        IncidentStatus.Ouvert => "Ouvert",
        IncidentStatus.EnCours => "En cours",
        IncidentStatus.Resolu => "Résolu",
        IncidentStatus.Cloture => "Clôturé",
        IncidentStatus.InterventionProgrammee => "Intervention programmée",
        _ => status.ToString()
    };

    private static bool IsRentCategory(string category) =>
        category.Contains("Loyer", StringComparison.OrdinalIgnoreCase) ||
        category.Contains("Location", StringComparison.OrdinalIgnoreCase);

    private static bool IsGuaranteeCategory(string category) =>
        category.Contains("Garantie", StringComparison.OrdinalIgnoreCase) ||
        category.Contains("Caution", StringComparison.OrdinalIgnoreCase);

    private static string FormatSeniority(DateTime hireDate)
    {
        var span = DateTime.Today - hireDate.Date;
        var years = span.Days / 365;
        var months = (span.Days % 365) / 30;
        if (years > 0)
            return months > 0 ? $"{years} an(s) {months} mois" : $"{years} an(s)";
        return months > 0 ? $"{months} mois" : "< 1 mois";
    }

    private static List<string> BuildFilters(string allLabel, IEnumerable<string> values)
    {
        var list = new List<string> { allLabel };
        list.AddRange(values.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(v => v));
        return list;
    }
}

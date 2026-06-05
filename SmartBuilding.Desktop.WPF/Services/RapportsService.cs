using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Consumption;
using SmartBuilding.Domain.Entities.Finance;
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
        var financierLignes = await LoadFinancierLignesAsync(from, to, cancellationToken);
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
            .Where(c => c.PeriodEnd >= chartStart)
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
                .Where(c => c.PeriodEnd.Year == m.Year && c.PeriodEnd.Month == m.Month)
                .Sum(c => c.Cost));
        }

        return new RapportsPageData
        {
            Personnel = personnel,
            Loyers = loyers,
            Depenses = depenses,
            Consommations = consommations,
            Financier = financier,
            FinancierLignes = financierLignes,
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
        var payments = await _db.RentPayments
            .Include(p => p.LeaseContract)
            .ThenInclude(c => c!.Tenant)
            .Include(p => p.LeaseContract)
            .ThenInclude(c => c!.Premise)
            .Include(p => p.LeaseContract)
            .ThenInclude(c => c!.Guarantees)
            .Where(p => (p.DueDate >= from && p.DueDate <= to) ||
                        (p.PaidDate >= from && p.PaidDate <= to))
            .OrderByDescending(p => p.DueDate)
            .ToListAsync(ct);

        var rows = new List<RapportLoyerRow>();
        foreach (var p in payments)
        {
            var c = p.LeaseContract;
            if (c is null) continue;

            var statut = MapRentStatus(p);
            var (bg, fg) = StatusBadge(statut);
            var garantie = c.Deposit > 0 ? c.Deposit : c.Guarantees.Sum(g => g.Amount);

            rows.Add(new RapportLoyerRow
            {
                Id = p.Id,
                PhotoPath = c.Tenant?.ProfilePhotoPath,
                NomComplet = c.Tenant?.Name ?? "—",
                Profession = c.Tenant?.Profession ?? c.Tenant?.BusinessActivity ?? "—",
                Telephone = c.Tenant?.Phone ?? "—",
                Appartement = c.Premise?.Name ?? c.Premise?.Code ?? "—",
                Batiment = c.Premise?.Building ?? "—",
                TypeContrat = c.ContractType,
                Periode = $"{p.Month:00}/{p.Year}",
                MontantLoyer = c.MonthlyRent,
                MontantLoyerDisplay = MoneyFormatter.Format(c.MonthlyRent),
                MontantDu = p.AmountDue,
                MontantPaye = p.AmountPaid,
                MontantDuDisplay = MoneyFormatter.Format(p.AmountDue),
                MontantPayeDisplay = MoneyFormatter.Format(p.AmountPaid),
                PenaliteDisplay = MoneyFormatter.Format(p.PenaltyAmount),
                Garantie = garantie,
                GarantieDisplay = MoneyFormatter.Format(garantie),
                DateEcheance = p.DueDate.ToString("dd/MM/yyyy", Fr),
                DernierPaiement = p.PaidDate?.ToString("dd/MM/yyyy", Fr) ?? "—",
                ModePaiement = p.PaymentMethod,
                Reference = p.TransactionReference ?? "—",
                NumeroRecu = p.ReceiptNumber ?? "—",
                StatutPaiement = statut,
                StatutBadgeBackground = bg,
                StatutBadgeForeground = fg,
                DueDate = p.DueDate
            });
        }

        return rows;
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
                Reference = t.Reference ?? "—",
                Categorie = t.Category,
                Montant = t.Amount,
                MontantDisplay = MoneyFormatter.Format(t.Amount),
                Description = t.Description,
                Responsable = string.IsNullOrWhiteSpace(t.RecordedBy) ? "—" : t.RecordedBy,
                Service = t.Source,
                ModePaiement = t.PaymentMethod,
                Justificatif = t.Reference ?? "—",
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
            .Where(c => c.PeriodStart <= to && c.PeriodEnd >= from)
            .OrderByDescending(c => c.PeriodEnd)
            .ThenByDescending(c => c.PeriodStart)
            .ToListAsync(ct);

        return records.Select(MapConsommationRow).ToList();
    }

    private RapportConsommationRow MapConsommationRow(ConsumptionRecord c)
    {
        var unitCost = c.Quantity > 0 ? c.Cost / c.Quantity : c.Cost;
        var unite = string.IsNullOrWhiteSpace(c.Unit) ? "—" : c.Unit;
        return new RapportConsommationRow
        {
            Id = c.Id,
            Date = c.PeriodEnd,
            DateDisplay = c.PeriodEnd.ToString("dd/MM/yyyy", Fr),
            PeriodeDebut = c.PeriodStart.ToString("dd/MM/yyyy", Fr),
            PeriodeFin = c.PeriodEnd.ToString("dd/MM/yyyy", Fr),
            Categorie = ConsumptionsService.DisplayTypeLabel(c),
            Equipement = string.IsNullOrWhiteSpace(c.EquipmentSource) ? "—" : c.EquipmentSource,
            Batiment = string.IsNullOrWhiteSpace(c.Building) ? "—" : c.Building,
            Quantite = c.Quantity,
            Unite = unite,
            QuantiteDisplay = c.Quantity > 0 ? $"{c.Quantity:N0} {unite}" : "—",
            CoutUnitaire = unitCost,
            CoutUnitaireDisplay = MoneyFormatter.Format(unitCost),
            CoutTotal = c.Cost,
            CoutTotalDisplay = MoneyFormatter.Format(c.Cost),
            Devise = string.IsNullOrWhiteSpace(c.Currency) ? "USD" : c.Currency,
            Compteur = c.MeterReference ?? "—",
            TypePeriode = c.PeriodType,
            Statut = c.Status,
            VariationDisplay = $"{c.VariationPercent:+0.0;-0.0}%",
            Anomalie = c.IsAnomaly ? "Oui" : "Non",
            Responsable = string.IsNullOrWhiteSpace(c.PaidBy) ? (string.IsNullOrWhiteSpace(c.Responsible) ? "—" : c.Responsible) : c.PaidBy,
            Notes = string.IsNullOrWhiteSpace(c.ExpenseMotif)
                ? (string.IsNullOrWhiteSpace(c.Notes) ? "—" : c.Notes!)
                : $"{c.ExpenseMotif}{(string.IsNullOrWhiteSpace(c.Notes) ? "" : $" — {c.Notes}")}"
        };
    }

    private async Task<IReadOnlyList<RapportFinancierLigne>> LoadFinancierLignesAsync(
        DateTime from, DateTime to, CancellationToken ct)
    {
        var lignes = new List<RapportFinancierLigne>();
        var linkedIds = new HashSet<Guid>();

        var transactions = await _db.FinancialTransactions
            .Where(t => t.TransactionDate >= from && t.TransactionDate <= to && t.DeletedAt == null)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync(ct);

        foreach (var t in transactions)
        {
            if (t.RelatedEntityId is { } relId)
                linkedIds.Add(relId);

            lignes.Add(new RapportFinancierLigne
            {
                Id = t.Id,
                Date = t.TransactionDate,
                DateDisplay = t.TransactionDate.ToString("dd/MM/yyyy", Fr),
                Type = t.Type == TransactionType.Recette ? "Revenu" : "Dépense",
                Categorie = t.Category,
                Description = t.Description,
                Montant = t.Amount,
                MontantDisplay = MoneyFormatter.Format(t.Amount),
                Source = t.Source,
                ModePaiement = t.PaymentMethod,
                Reference = t.Reference ?? "—",
                Statut = t.Status,
                EnregistrePar = t.RecordedBy
            });
        }

        var rentPayments = await _db.RentPayments
            .Include(p => p.LeaseContract)
            .ThenInclude(c => c!.Tenant)
            .Where(p => p.PaidDate >= from && p.PaidDate <= to && p.AmountPaid > 0)
            .ToListAsync(ct);

        foreach (var p in rentPayments.Where(p => !linkedIds.Contains(p.Id)))
        {
            var tenant = p.LeaseContract?.Tenant?.Name ?? "Locataire";
            lignes.Add(new RapportFinancierLigne
            {
                Id = p.Id,
                Date = p.PaidDate!.Value,
                DateDisplay = p.PaidDate!.Value.ToString("dd/MM/yyyy", Fr),
                Type = "Revenu",
                Categorie = FinanceConstants.CategoryRent,
                Description = $"Loyer {tenant} — {p.Month:00}/{p.Year}",
                Montant = p.AmountPaid,
                MontantDisplay = MoneyFormatter.Format(p.AmountPaid),
                Source = FinanceConstants.SourceLocations,
                ModePaiement = p.PaymentMethod,
                Reference = p.TransactionReference ?? p.ReceiptNumber ?? "—",
                Statut = MapRentStatus(p),
                EnregistrePar = FinanceConstants.RecordedByLocations
            });
        }

        var consoRecords = await _db.ConsumptionRecords
            .Where(c => c.PeriodStart <= to && c.PeriodEnd >= from)
            .ToListAsync(ct);
        foreach (var c in consoRecords.Where(c => !linkedIds.Contains(c.Id)))
        {
            lignes.Add(new RapportFinancierLigne
            {
                Id = c.Id,
                Date = c.PeriodEnd,
                DateDisplay = c.PeriodEnd.ToString("dd/MM/yyyy", Fr),
                Type = "Dépense",
                Categorie = FinanceConstants.CategoryEnergy,
                Description = $"Consommation {ConsumptionsService.DisplayTypeLabel(c)} — {c.EquipmentSource}",
                Montant = c.Cost,
                MontantDisplay = MoneyFormatter.Format(c.Cost),
                Source = FinanceConstants.SourceConsumptions,
                ModePaiement = "—",
                Reference = c.MeterReference ?? "—",
                Statut = c.Status,
                EnregistrePar = FinanceConstants.RecordedByConsumptions
            });
        }

        var salaries = await _db.SalaryPayments
            .Include(p => p.Employee)
            .Where(p => p.PaymentDate >= from && p.PaymentDate <= to && p.Status == RhConstants.PayrollStatus.Paid)
            .ToListAsync(ct);
        foreach (var p in salaries.Where(p => !linkedIds.Contains(p.Id)))
        {
            var emp = p.Employee;
            var net = p.NetAmount > 0 ? p.NetAmount : p.Amount;
            lignes.Add(new RapportFinancierLigne
            {
                Id = p.Id,
                Date = p.PaymentDate,
                DateDisplay = p.PaymentDate.ToString("dd/MM/yyyy", Fr),
                Type = "Dépense",
                Categorie = FinanceConstants.CategorySalaries,
                Description = $"Paie {p.Month:00}/{p.Year} — {emp?.FirstName} {emp?.LastName}".Trim(),
                Montant = net,
                MontantDisplay = MoneyFormatter.Format(net),
                Source = FinanceConstants.SourcePersonnel,
                ModePaiement = "Virement",
                Reference = "—",
                Statut = p.Status,
                EnregistrePar = FinanceConstants.RecordedByPersonnel
            });
        }

        var incidents = await _db.Incidents
            .Where(i => i.ReportedAt >= from && i.ReportedAt <= to && i.Cost > 0)
            .ToListAsync(ct);
        foreach (var i in incidents.Where(i => !linkedIds.Contains(i.Id)))
        {
            lignes.Add(new RapportFinancierLigne
            {
                Id = i.Id,
                Date = i.ReportedAt,
                DateDisplay = i.ReportedAt.ToString("dd/MM/yyyy", Fr),
                Type = "Dépense",
                Categorie = FinanceConstants.CategoryIncident,
                Description = $"Incident {i.Code} — {i.Title}",
                Montant = i.Cost,
                MontantDisplay = MoneyFormatter.Format(i.Cost),
                Source = FinanceConstants.SourceIncidents,
                ModePaiement = "—",
                Reference = i.Code,
                Statut = MapIncidentStatus(i.Status),
                EnregistrePar = FinanceConstants.RecordedByIncidents
            });
        }

        var supplierPayments = await _db.SupplierPayments
            .Include(p => p.Supplier)
            .Where(p => p.PaymentDate >= from && p.PaymentDate <= to && p.IsPaid)
            .ToListAsync(ct);
        foreach (var p in supplierPayments.Where(p => !linkedIds.Contains(p.Id)))
        {
            lignes.Add(new RapportFinancierLigne
            {
                Id = p.Id,
                Date = p.PaymentDate,
                DateDisplay = p.PaymentDate.ToString("dd/MM/yyyy", Fr),
                Type = "Dépense",
                Categorie = string.IsNullOrWhiteSpace(p.Category) ? "Facture" : p.Category,
                Description = $"Facture {p.InvoiceReference} — {p.Supplier?.Name ?? "Fournisseur"}",
                Montant = p.Amount,
                MontantDisplay = MoneyFormatter.Format(p.Amount),
                Source = FinanceConstants.SourceFinances,
                ModePaiement = "Virement",
                Reference = p.InvoiceReference ?? "—",
                Statut = "Payé",
                EnregistrePar = "SBMS — Fournisseurs"
            });
        }

        var guarantees = await _db.LeaseGuarantees
            .Include(g => g.LeaseContract)
            .ThenInclude(c => c!.Tenant)
            .Where(g => g.CreatedAt >= from && g.CreatedAt <= to)
            .ToListAsync(ct);
        foreach (var g in guarantees.Where(g => !linkedIds.Contains(g.Id)))
        {
            lignes.Add(new RapportFinancierLigne
            {
                Id = g.Id,
                Date = g.CreatedAt,
                DateDisplay = g.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy", Fr),
                Type = "Revenu",
                Categorie = FinanceConstants.CategoryGuarantee,
                Description = $"Caution — {g.LeaseContract?.Tenant?.Name ?? "Locataire"}",
                Montant = g.Amount,
                MontantDisplay = MoneyFormatter.Format(g.Amount),
                Source = FinanceConstants.SourceLocations,
                ModePaiement = "—",
                Reference = "—",
                Statut = g.Status,
                EnregistrePar = FinanceConstants.RecordedByLocations
            });
        }

        return lignes.OrderByDescending(l => l.Date).ToList();
    }

    private async Task<RapportFinancierSummary> LoadFinancierAsync(
        DateTime from, DateTime to, CancellationToken ct)
    {
        var tx = await _db.FinancialTransactions
            .Where(t => t.TransactionDate >= from && t.TransactionDate <= to && t.DeletedAt == null)
            .ToListAsync(ct);

        var loyersEncaisses = await _db.RentPayments
            .Where(p => p.PaidDate >= from && p.PaidDate <= to)
            .SumAsync(p => p.AmountPaid, ct);

        var garantiesTx = tx.Where(t => t.Type == TransactionType.Recette && IsGuaranteeCategory(t.Category))
            .Sum(t => t.Amount);
        var garantiesCautions = await _db.LeaseGuarantees
            .Where(g => g.CreatedAt >= from && g.CreatedAt <= to)
            .SumAsync(g => g.Amount, ct);
        var garanties = garantiesTx + garantiesCautions;

        var services = tx.Where(t => t.Type == TransactionType.Recette &&
                                     t.Category.Contains("Service", StringComparison.OrdinalIgnoreCase))
            .Sum(t => t.Amount);

        var revenusDivers = tx.Where(t => t.Type == TransactionType.Recette &&
                                          !IsRentCategory(t.Category) &&
                                          !IsGuaranteeCategory(t.Category) &&
                                          !t.Category.Contains("Service", StringComparison.OrdinalIgnoreCase))
            .Sum(t => t.Amount);

        var salairesTx = tx.Where(t => t.Type == TransactionType.Depense && IsSalaryExpense(t)).Sum(t => t.Amount);
        var salairesRh = await _db.SalaryPayments
            .Where(p => p.PaymentDate >= from && p.PaymentDate <= to && p.Status == RhConstants.PayrollStatus.Paid)
            .SumAsync(p => p.NetAmount > 0 ? p.NetAmount : p.Amount, ct);
        var salaires = salairesTx > 0 ? salairesTx : salairesRh;

        var consommationsTx = tx.Where(t => t.Type == TransactionType.Depense && IsEnergyExpense(t)).Sum(t => t.Amount);
        var consommationsRecords = await _db.ConsumptionRecords
            .Where(c => c.PeriodStart <= to && c.PeriodEnd >= from)
            .SumAsync(c => c.Cost, ct);
        var consommations = consommationsRecords > 0 ? consommationsRecords : consommationsTx;

        var maintenanceTx = tx.Where(t => t.Type == TransactionType.Depense && IsMaintenanceExpense(t)).Sum(t => t.Amount);
        var maintenanceIncidents = await _db.Incidents
            .Where(i => i.ReportedAt >= from && i.ReportedAt <= to && i.Cost > 0)
            .SumAsync(i => i.Cost, ct);
        var maintenance = maintenanceTx > 0 ? maintenanceTx : maintenanceIncidents;

        var fournisseursTx = tx.Where(t => t.Type == TransactionType.Depense && IsSupplierExpense(t)).Sum(t => t.Amount);
        var fournisseursPay = await _db.SupplierPayments
            .Where(p => p.PaymentDate >= from && p.PaymentDate <= to && p.IsPaid)
            .SumAsync(p => p.Amount, ct);
        var fournisseurs = fournisseursTx > 0 ? fournisseursTx : fournisseursPay;

        var chargesDiverses = tx.Where(t => t.Type == TransactionType.Depense &&
                                            !IsSalaryExpense(t) &&
                                            !IsEnergyExpense(t) &&
                                            !IsMaintenanceExpense(t) &&
                                            !IsSupplierExpense(t))
            .Sum(t => t.Amount);

        var totalEntrees = loyersEncaisses + garanties + services + revenusDivers;
        var totalSorties = salaires + consommations + maintenance + fournisseurs + chargesDiverses;
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
            ChargesDiverses = chargesDiverses,
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
            ChargesDiversesDisplay = MoneyFormatter.Format(chargesDiverses),
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
        string.Equals(category, FinanceConstants.CategoryRent, StringComparison.OrdinalIgnoreCase) ||
        category.Contains("Loyer", StringComparison.OrdinalIgnoreCase);

    private static bool IsGuaranteeCategory(string category) =>
        string.Equals(category, FinanceConstants.CategoryGuarantee, StringComparison.OrdinalIgnoreCase) ||
        (category.Contains("Caution", StringComparison.OrdinalIgnoreCase) &&
         !category.Contains("Remboursement", StringComparison.OrdinalIgnoreCase));

    private static bool IsSalaryExpense(FinancialTransaction t) =>
        t.Type == TransactionType.Depense &&
        string.Equals(t.Category, FinanceConstants.CategorySalaries, StringComparison.OrdinalIgnoreCase);

    private static bool IsEnergyExpense(FinancialTransaction t) =>
        t.Type == TransactionType.Depense &&
        (string.Equals(t.Category, FinanceConstants.CategoryEnergy, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(t.Source, FinanceConstants.SourceConsumptions, StringComparison.OrdinalIgnoreCase));

    private static bool IsMaintenanceExpense(FinancialTransaction t) =>
        t.Type == TransactionType.Depense &&
        (string.Equals(t.Category, FinanceConstants.CategoryMaintenance, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(t.Category, FinanceConstants.CategoryIncident, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(t.Source, FinanceConstants.SourceTechnique, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(t.Source, FinanceConstants.SourceIncidents, StringComparison.OrdinalIgnoreCase) ||
         t.Category.Contains("Réparation", StringComparison.OrdinalIgnoreCase));

    private static bool IsSupplierExpense(FinancialTransaction t) =>
        t.Type == TransactionType.Depense &&
        (t.Category.Contains("Facture", StringComparison.OrdinalIgnoreCase) ||
         t.Description.Contains("fournisseur", StringComparison.OrdinalIgnoreCase));

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

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Finance;
using SmartBuilding.Domain.Entities.Suppliers;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Services;

namespace SmartBuilding.Desktop.WPF.Services;

public class SuppliersService
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    private readonly SmartBuildingDbContext _db;
    private readonly FinanceLedgerService _financeLedger;

    public SuppliersService(SmartBuildingDbContext db, FinanceLedgerService financeLedger)
    {
        _db = db;
        _financeLedger = financeLedger;
    }

    public async Task<SuppliersPageData> LoadAsync(CancellationToken cancellationToken = default)
    {
        var cash = await TreasuryLoader.LoadAsync(_financeLedger, cancellationToken);
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var expireThreshold = today.AddDays(30);

        var suppliers = await _db.Suppliers
            .Include(s => s.Contracts)
            .Include(s => s.Payments)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        var payments = suppliers.SelectMany(s => s.Payments).ToList();
        var monthPayments = payments.Where(p => p.PaymentDate >= monthStart).ToList();

        var total = suppliers.Count;
        var active = suppliers.Count(s => s.Status == "Actif");
        var unpaid = payments.Count(p => !p.IsPaid);
        var monthlyExpenses = monthPayments.Sum(p => p.Amount);
        var expiring = suppliers.SelectMany(s => s.Contracts)
            .Count(c => c.Status == "Actif" && c.EndDate >= today && c.EndDate <= expireThreshold);
        var interventions = monthPayments.Count;

        var items = suppliers.Select(s => MapSupplier(s, today)).ToList();

        var expenseByCategory = monthPayments
            .GroupBy(p => string.IsNullOrWhiteSpace(p.Category) ? "Autre" : p.Category)
            .OrderByDescending(g => g.Sum(x => x.Amount))
            .Select(g => new SupplierCategorySlice { Category = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToList();

        var trend = new List<SupplierMonthPoint>();
        for (var i = 5; i >= 0; i--)
        {
            var m = monthStart.AddMonths(-i);
            var end = m.AddMonths(1);
            trend.Add(new SupplierMonthPoint
            {
                Label = m.ToString("MMM", Fr),
                Amount = payments.Where(p => p.PaymentDate >= m && p.PaymentDate < end).Sum(p => p.Amount)
            });
        }

        var top = suppliers
            .Select(s => new SupplierTopItem
            {
                Name = s.Name,
                Amount = s.Payments.Where(p => p.PaymentDate >= monthStart.AddMonths(-11)).Sum(p => p.Amount)
            })
            .OrderByDescending(x => x.Amount)
            .Take(5)
            .ToList();

        var alerts = BuildAlerts(suppliers, today, expireThreshold);

        if (monthlyExpenses > cash.AvailableBalance && cash.RentCollectedTotal > 0)
            alerts.Insert(0, new SupplierAlertItem
            {
                Title = "Dépenses fournisseurs vs trésorerie",
                Message = $"Factures du mois ({Fc(monthlyExpenses)}) dépassent le disponible ({Fc(cash.AvailableBalance)}).",
                AccentColor = "#EA580C",
                Background = "#FFEDD5"
            });

        return new SuppliersPageData
        {
            RentCollectedTotal = cash.RentCollectedTotal,
            AvailableBalance = cash.AvailableBalance,
            TotalExpenses = cash.TotalExpenses,
            TotalSuppliers = total,
            ActiveSuppliers = active,
            UnpaidInvoices = unpaid,
            MonthlyExpenses = monthlyExpenses,
            ContractsExpiringSoon = expiring,
            InterventionsThisMonth = interventions,
            ActivePercent = total > 0 ? $"{active * 100.0 / total:F1}%" : "0%",
            Suppliers = items,
            Alerts = alerts,
            ExpenseByCategory = expenseByCategory,
            ExpenseTrend = trend,
            TopExpensive = top
        };
    }

    public async Task<string> CreateSupplierAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(supplier.Name))
            return "Le nom de l'entreprise est obligatoire.";

        if (string.IsNullOrWhiteSpace(supplier.Code))
            supplier.Code = $"FRN-{DateTime.Today:yyyyMM}-{(await _db.Suppliers.CountAsync(cancellationToken) + 1):D3}";

        if (await _db.Suppliers.AnyAsync(s => s.Code == supplier.Code.Trim(), cancellationToken))
            return "Ce code fournisseur existe déjà.";

        supplier.Code = supplier.Code.Trim();
        supplier.Name = supplier.Name.Trim();
        supplier.Status = string.IsNullOrWhiteSpace(supplier.Status) ? "Actif" : supplier.Status;
        supplier.Category = string.IsNullOrWhiteSpace(supplier.Category) ? "Services" : supplier.Category.Trim();
        supplier.IsSynced = false;

        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync(cancellationToken);
        return string.Empty;
    }

    public async Task<string> CreateContractAsync(SupplierContract contract, CancellationToken cancellationToken = default)
    {
        if (contract.SupplierId == Guid.Empty)
            return "Sélectionnez un fournisseur.";
        if (string.IsNullOrWhiteSpace(contract.Description))
            return "La description du contrat est obligatoire.";
        if (contract.StartDate == default || contract.EndDate == default || contract.EndDate < contract.StartDate)
            return "Les dates du contrat sont invalides.";
        if (contract.TotalValue <= 0)
            return "Le montant du contrat doit être supérieur à zéro.";

        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == contract.SupplierId, cancellationToken);
        if (supplier is null)
            return "Fournisseur introuvable.";

        if (string.IsNullOrWhiteSpace(contract.ContractNumber))
            contract.ContractNumber = $"CTR-FRN-{DateTime.Today:yyyyMM}-{(await _db.SupplierContracts.CountAsync(cancellationToken) + 1):D3}";

        contract.ContractNumber = contract.ContractNumber.Trim();
        contract.Description = contract.Description.Trim();
        contract.Status = string.IsNullOrWhiteSpace(contract.Status) ? "Actif" : contract.Status.Trim();
        contract.Building = string.IsNullOrWhiteSpace(contract.Building) ? supplier.Building : contract.Building.Trim();
        contract.IsSynced = false;

        _db.SupplierContracts.Add(contract);
        await _db.SaveChangesAsync(cancellationToken);
        return string.Empty;
    }

    public async Task<string> CreateInvoiceAsync(
        SupplierPayment payment,
        string recordedBy,
        CancellationToken cancellationToken = default)
    {
        if (payment.SupplierId == Guid.Empty)
            return "Sélectionnez un fournisseur.";
        if (payment.Amount <= 0)
            return "Le montant de la facture doit être supérieur à zéro.";

        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == payment.SupplierId, cancellationToken);
        if (supplier is null)
            return "Fournisseur introuvable.";

        payment.PaymentDate = payment.PaymentDate == default ? DateTime.Today : payment.PaymentDate;
        payment.Description = string.IsNullOrWhiteSpace(payment.Description) ? "Facture fournisseur" : payment.Description.Trim();
        payment.Category = string.IsNullOrWhiteSpace(payment.Category) ? supplier.Category : payment.Category.Trim();
        payment.InvoiceReference = string.IsNullOrWhiteSpace(payment.InvoiceReference)
            ? $"FAC-{DateTime.Today:yyyyMM}-{(await _db.SupplierPayments.CountAsync(cancellationToken) + 1):D4}"
            : payment.InvoiceReference.Trim();
        payment.IsSynced = false;

        _db.SupplierPayments.Add(payment);

        if (payment.IsPaid)
        {
            try
            {
                await _financeLedger.RecordExpensePendingPdgApprovalAsync(
                    payment.Amount,
                    payment.Category,
                    $"Facture fournisseur {payment.InvoiceReference} — {supplier.Name}",
                    FinanceConstants.SourceFinances,
                    string.IsNullOrWhiteSpace(recordedBy) ? "SBMS — Fournisseurs" : recordedBy,
                    payment.Id,
                    cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return string.Empty;
    }

    public async Task<string> PlanInterventionAsync(
        Guid supplierId,
        DateTime date,
        string description,
        decimal estimatedAmount,
        string recordedBy,
        CancellationToken cancellationToken = default)
    {
        if (supplierId == Guid.Empty)
            return "Sélectionnez un fournisseur.";
        if (date == default)
            return "La date d'intervention est obligatoire.";
        if (string.IsNullOrWhiteSpace(description))
            return "La description de l'intervention est obligatoire.";
        if (estimatedAmount <= 0)
            return "Le montant estimé doit être supérieur à zéro.";

        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId, cancellationToken);
        if (supplier is null)
            return "Fournisseur introuvable.";

        var payment = new SupplierPayment
        {
            SupplierId = supplierId,
            Amount = estimatedAmount,
            PaymentDate = date.Date,
            DueDate = date.Date,
            InvoiceReference = $"INT-{DateTime.Today:yyyyMM}-{(await _db.SupplierPayments.CountAsync(cancellationToken) + 1):D4}",
            Notes = "Intervention planifiée",
            Description = description.Trim(),
            Category = supplier.Category,
            IsPaid = false,
            IsSynced = false
        };

        _db.SupplierPayments.Add(payment);
        await _db.SaveChangesAsync(cancellationToken);
        return string.Empty;
    }

    private static SupplierListItem MapSupplier(Supplier s, DateTime today)
    {
        var contract = s.Contracts.OrderByDescending(c => c.EndDate).FirstOrDefault();
        var totalExp = s.Payments.Sum(p => p.Amount);
        var lastPay = s.Payments.OrderByDescending(p => p.PaymentDate).FirstOrDefault();
        var (status, bg, fg) = StatusStyle(s.Status);
        var initials = GetInitials(s.Name);
        var palette = GetLogoColors(s.Category);

        return new SupplierListItem
        {
            Id = s.Id,
            Code = string.IsNullOrWhiteSpace(s.Code) ? $"FRN-{s.Id.ToString()[..6].ToUpper()}" : s.Code,
            Name = s.Name,
            Initials = initials,
            LogoBackground = palette.bg,
            LogoForeground = palette.fg,
            Category = string.IsNullOrWhiteSpace(s.Category) ? "—" : s.Category,
            Phone = string.IsNullOrWhiteSpace(s.Phone) ? "—" : s.Phone,
            Email = string.IsNullOrWhiteSpace(s.Email) ? "—" : s.Email,
            ContractDisplay = contract?.ContractNumber ?? "—",
            TotalExpensesDisplay = totalExp > 0 ? Fc(totalExp) : "—",
            LastInterventionDisplay = lastPay is null ? "—" : lastPay.PaymentDate.ToString("dd/MM/yyyy"),
            StatusLabel = status,
            StatusBadgeBackground = bg,
            StatusBadgeForeground = fg,
            ServiceType = string.IsNullOrWhiteSpace(s.ServiceType) ? "—" : s.ServiceType,
            Building = string.IsNullOrWhiteSpace(s.Building) ? "—" : s.Building,
            ContactName = string.IsNullOrWhiteSpace(s.ContactName) ? "—" : s.ContactName,
            Address = s.Address ?? "—",
            TaxId = s.TaxId ?? "—",
            Notes = s.Notes ?? "—",
            ContractStatus = contract?.Status ?? "—",
            ContractEndDisplay = contract?.EndDate.ToString("dd/MM/yyyy") ?? "—",
            TotalExpenses = totalExp,
            Invoices = s.Payments.OrderByDescending(p => p.PaymentDate).Take(6).Select(p =>
            {
                var (sl, sbg, sfg) = p.IsPaid ? ("Payée", "#DCFCE7", "#166534") : ("Impayée", "#FEE2E2", "#DC2626");
                return new SupplierInvoiceItem
                {
                    Reference = p.InvoiceReference ?? "—",
                    DateDisplay = p.PaymentDate.ToString("dd/MM/yyyy"),
                    AmountDisplay = Fc(p.Amount),
                    StatusLabel = sl,
                    StatusBadgeBackground = sbg,
                    StatusBadgeForeground = sfg
                };
            }).ToList(),
            Interventions = s.Payments.OrderByDescending(p => p.PaymentDate).Take(5).Select(p => new SupplierInterventionItem
            {
                DateDisplay = p.PaymentDate.ToString("dd/MM/yyyy"),
                Description = string.IsNullOrWhiteSpace(p.Description) ? p.Notes ?? "Intervention" : p.Description,
                AmountDisplay = Fc(p.Amount)
            }).ToList()
        };
    }

    private static List<SupplierAlertItem> BuildAlerts(List<Supplier> suppliers, DateTime today, DateTime expireThreshold)
    {
        var alerts = new List<SupplierAlertItem>();

        foreach (var c in suppliers.SelectMany(s => s.Contracts)
                     .Where(c => c.EndDate >= today && c.EndDate <= expireThreshold))
        {
            alerts.Add(new SupplierAlertItem
            {
                Title = "Contrat expire bientôt",
                Message = $"{c.Supplier?.Name} — {c.ContractNumber} ({c.EndDate:dd/MM/yyyy})",
                AccentColor = "#EA580C",
                Background = "#FFEDD5"
            });
        }

        foreach (var s in suppliers)
        {
            foreach (var p in s.Payments.Where(x => !x.IsPaid).Take(2))
            {
                alerts.Add(new SupplierAlertItem
                {
                    Title = "Facture impayée",
                    Message = $"{s.Name} — {Fc(p.Amount)} ({p.InvoiceReference ?? "sans réf."})",
                AccentColor = "#DC2626",
                Background = "#FEE2E2"
                });
            }
        }

        foreach (var s in suppliers.Where(s => s.Status == "En attente").Take(2))
        {
            alerts.Add(new SupplierAlertItem
            {
                Title = "Fournisseur en attente",
                Message = $"{s.Name} — validation requise",
                AccentColor = "#2563EB",
                Background = "#DBEAFE"
            });
        }

        if (alerts.Count == 0)
            alerts.Add(new SupplierAlertItem
            {
                Title = "Aucune alerte",
                Message = "Tous les fournisseurs sont à jour",
                AccentColor = "#2D6A4F",
                Background = "#E8F5EE"
            });

        return alerts.Take(6).ToList();
    }

    private static (string Label, string Bg, string Fg) StatusStyle(string status) => status switch
    {
        "Actif" => ("Actif", "#DCFCE7", "#166534"),
        "Expiré" => ("Expiré", "#FEE2E2", "#DC2626"),
        "En attente" => ("En attente", "#FFEDD5", "#EA580C"),
        _ => ("Actif", "#DCFCE7", "#166534")
    };

    private static (string bg, string fg) GetLogoColors(string category) => category switch
    {
        var c when c.Contains("Maintenance", StringComparison.OrdinalIgnoreCase) => ("#FFEDD5", "#EA580C"),
        var c when c.Contains("Sécurité", StringComparison.OrdinalIgnoreCase) || c.Contains("Securite", StringComparison.OrdinalIgnoreCase) => ("#DBEAFE", "#1D4ED8"),
        var c when c.Contains("Nettoyage", StringComparison.OrdinalIgnoreCase) => ("#EDE9FE", "#6D28D9"),
        var c when c.Contains("Énergie", StringComparison.OrdinalIgnoreCase) || c.Contains("Energie", StringComparison.OrdinalIgnoreCase) => ("#FEF3C7", "#B45309"),
        _ => ("#E8F5EE", "#2D6A4F")
    };

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant()
            : name.Length >= 2 ? name[..2].ToUpperInvariant() : "FR";
    }

    private static string Fc(decimal amount) => MoneyFormatter.Format(amount);
}

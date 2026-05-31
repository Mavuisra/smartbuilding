using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Finance;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Domain.Entities.Personnel;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Infrastructure.Persistence;

namespace SmartBuilding.Infrastructure.Services;

/// <summary>
/// Écritures financières automatiques (Locations, Personnel).
/// </summary>
public class FinanceLedgerService
{
    private readonly SmartBuildingDbContext _db;

    public FinanceLedgerService(SmartBuildingDbContext db) => _db = db;

    public async Task RecordRentCollectionAsync(
        RentPayment payment,
        LeaseContract contract,
        decimal amountCollected,
        CancellationToken cancellationToken = default)
    {
        if (amountCollected <= 0)
            return;

        var existing = await _db.FinancialTransactions
            .Where(t => t.RelatedEntityId == payment.Id
                        && t.Type == TransactionType.Recette
                        && t.Category == FinanceConstants.CategoryRent
                        && t.DeletedAt == null)
            .OrderByDescending(t => t.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var tenant = await _db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == contract.TenantId, cancellationToken);
        var premise = contract.Premise
            ?? await _db.Premises.AsNoTracking().FirstOrDefaultAsync(p => p.Id == contract.PremiseId, cancellationToken);

        var description =
            $"Loyer {payment.Month:D2}/{payment.Year} — {tenant?.Name ?? "Locataire"} — {premise?.Name ?? contract.ContractNumber}";
        var txDate = RentPaymentLedgerDates.TransactionDate(payment);
        var paymentMethod = string.IsNullOrWhiteSpace(payment.PaymentMethod) ? "Espèces" : payment.PaymentMethod;

        if (existing is not null)
        {
            var duplicates = await _db.FinancialTransactions
                .Where(t => t.RelatedEntityId == payment.Id
                            && t.Type == TransactionType.Recette
                            && t.Category == FinanceConstants.CategoryRent
                            && t.DeletedAt == null
                            && t.Id != existing.Id)
                .ToListAsync(cancellationToken);
            foreach (var dup in duplicates)
                SoftDeleteLedgerEntry(dup);

            if (existing.Amount == amountCollected
                && existing.Description == description
                && existing.TransactionDate == txDate)
                return;

            existing.Amount = amountCollected;
            existing.Description = description;
            existing.TransactionDate = txDate;
            existing.PaymentMethod = paymentMethod;
            existing.Status = "Payé";
            existing.MarkUpdated();
            return;
        }

        var reference = await NextReferenceAsync(TransactionType.Recette, cancellationToken);
        _db.FinancialTransactions.Add(new FinancialTransaction
        {
            Type = TransactionType.Recette,
            Category = FinanceConstants.CategoryRent,
            Description = description,
            Amount = amountCollected,
            TransactionDate = txDate,
            Reference = reference,
            RelatedEntityId = payment.Id,
            Source = FinanceConstants.SourceLocations,
            PaymentMethod = paymentMethod,
            Status = "Payé",
            RecordedBy = FinanceConstants.RecordedByLocations,
            IsSynced = false
        });
    }

    /// <summary>
    /// Les cautions ne sont pas des revenus : aucune écriture de trésorerie (module Garanties uniquement).
    /// </summary>
    public Task RecordGuaranteeDepositAsync(
        LeaseGuarantee guarantee,
        LeaseContract contract,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <summary>
    /// Remboursement de caution : suivi administratif uniquement, hors trésorerie loyers.
    /// </summary>
    public Task RecordGuaranteeRefundAsync(
        LeaseGuarantee guarantee,
        LeaseContract contract,
        decimal amountRefunded,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public async Task RecordSalaryExpenseAsync(
        SalaryPayment payment,
        Employee employee,
        CancellationToken cancellationToken = default)
    {
        if (payment.NetAmount <= 0)
            return;

        var linked = await SumLinkedAmountAsync(
            payment.Id,
            FinanceConstants.CategorySalaries,
            cancellationToken);
        var missing = payment.NetAmount - linked;
        if (missing <= 0)
            return;

        var existing = await _db.FinancialTransactions
            .Where(t => t.RelatedEntityId == payment.Id
                        && t.Category == FinanceConstants.CategorySalaries
                        && t.Type == TransactionType.Depense
                        && t.DeletedAt == null)
            .OrderByDescending(t => t.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var description = $"Paie {payment.Month:D2}/{payment.Year} — {employee.FirstName} {employee.LastName}";

        if (existing is not null)
        {
            var duplicates = await _db.FinancialTransactions
                .Where(t => t.RelatedEntityId == payment.Id
                            && t.Category == FinanceConstants.CategorySalaries
                            && t.Type == TransactionType.Depense
                            && t.DeletedAt == null
                            && t.Id != existing.Id)
                .ToListAsync(cancellationToken);
            foreach (var dup in duplicates)
                SoftDeleteLedgerEntry(dup);

            if (existing.Amount == missing)
                return;

            existing.Amount = missing;
            existing.Description = description;
            existing.MarkUpdated();
            return;
        }

        var cashError = await ValidateExpenseAsync(missing, cancellationToken);
        if (cashError is not null)
            throw new InvalidOperationException(cashError);

        var reference = await NextReferenceAsync(TransactionType.Depense, cancellationToken);
        _db.FinancialTransactions.Add(new FinancialTransaction
        {
            Type = TransactionType.Depense,
            Category = FinanceConstants.CategorySalaries,
            Description = $"Paie {payment.Month:D2}/{payment.Year} — {employee.FirstName} {employee.LastName}",
            Amount = missing,
            TransactionDate = DateTime.Today,
            Reference = reference,
            RelatedEntityId = payment.Id,
            Source = FinanceConstants.SourcePersonnel,
            PaymentMethod = "Virement",
            Status = "Payé",
            RecordedBy = FinanceConstants.RecordedByPersonnel,
            IsSynced = false
        });
    }

    public async Task<int> ReconcileFromLocationsAsync(CancellationToken cancellationToken = default)
    {
        var created = 0;

        var payments = await _db.RentPayments
            .Include(p => p.LeaseContract)
            .ThenInclude(c => c!.Premise)
            .Where(p => p.AmountPaid > 0)
            .ToListAsync(cancellationToken);

        foreach (var payment in payments)
        {
            var linked = await SumLinkedAmountAsync(
                payment.Id,
                FinanceConstants.CategoryRent,
                cancellationToken);

            if (linked >= payment.AmountPaid || payment.LeaseContract is null)
                continue;

            await AlignRentLedgerWithPaymentAsync(
                payment, payment.LeaseContract, cancellationToken);
            created++;
        }

        if (created > 0)
            await _db.SaveChangesAsync(cancellationToken);

        return created;
    }

    public async Task<int> ReconcileFromPersonnelAsync(CancellationToken cancellationToken = default)
    {
        var created = 0;
        var payments = await _db.SalaryPayments
            .Include(s => s.Employee)
            .Where(s => s.Status == RhConstants.PayrollStatus.Paid ||
                        s.Status == RhConstants.PayrollStatus.Validated)
            .ToListAsync(cancellationToken);

        foreach (var payment in payments)
        {
            if (payment.Employee is null)
                continue;

            var linked = await SumLinkedAmountAsync(
                payment.Id,
                FinanceConstants.CategorySalaries,
                cancellationToken);
            if (linked >= payment.NetAmount)
                continue;

            await RecordSalaryExpenseAsync(payment, payment.Employee, cancellationToken);
            created++;
        }

        if (created > 0)
            await _db.SaveChangesAsync(cancellationToken);

        return created;
    }

    /// <summary>
    /// Dépense validée contre les loyers encaissés (seule voie pour les sorties hors modules métier).
    /// </summary>
    public async Task RecordExpenseAsync(
        decimal amount,
        string category,
        string description,
        string source,
        string recordedBy,
        Guid? relatedEntityId = null,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            return;

        var cashError = await ValidateExpenseAsync(amount, cancellationToken);
        if (cashError is not null)
            throw new InvalidOperationException(cashError);

        var exists = relatedEntityId is { } id &&
                     await _db.FinancialTransactions.AnyAsync(
                         t => t.RelatedEntityId == id && t.Category == category && t.Type == TransactionType.Depense,
                         cancellationToken);
        if (exists)
            return;

        var reference = await NextReferenceAsync(TransactionType.Depense, cancellationToken);
        _db.FinancialTransactions.Add(new FinancialTransaction
        {
            Type = TransactionType.Depense,
            Category = category.Trim(),
            Description = description.Trim(),
            Amount = amount,
            TransactionDate = DateTime.Today,
            Reference = reference,
            RelatedEntityId = relatedEntityId,
            Source = source,
            PaymentMethod = "Virement",
            Status = "Payé",
            RecordedBy = recordedBy,
            RequiresPdgApproval = false,
            ApprovedAt = DateTime.UtcNow,
            ApprovedBy = recordedBy,
            IsSynced = false
        });
    }

    /// <summary>
    /// Dépense enregistrée par le gérant en attente d'approbation finale du PDG.
    /// </summary>
    public async Task RecordExpensePendingPdgApprovalAsync(
        decimal amount,
        string category,
        string description,
        string source,
        string recordedBy,
        Guid? relatedEntityId = null,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            return;

        var cashError = await ValidateExpenseAsync(amount, cancellationToken);
        if (cashError is not null)
            throw new InvalidOperationException(cashError);

        var reference = await NextReferenceAsync(TransactionType.Depense, cancellationToken);
        _db.FinancialTransactions.Add(new FinancialTransaction
        {
            Type = TransactionType.Depense,
            Category = category.Trim(),
            Description = description.Trim(),
            Amount = amount,
            TransactionDate = DateTime.Today,
            Reference = reference,
            RelatedEntityId = relatedEntityId,
            Source = source,
            PaymentMethod = "Virement",
            Status = "En attente validation PDG",
            RecordedBy = recordedBy,
            RequiresPdgApproval = true,
            ApprovedAt = null,
            ApprovedBy = null,
            IsSynced = false
        });
    }

    public async Task<string> ApproveExpenseAsync(
        Guid transactionId,
        string approvedBy,
        CancellationToken cancellationToken = default)
    {
        var tx = await _db.FinancialTransactions
            .FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken);
        if (tx is null)
            return "Transaction introuvable.";
        if (tx.Type != TransactionType.Depense)
            return "Seules les dépenses sont approuvables.";
        if (!tx.RequiresPdgApproval || tx.Status != "En attente validation PDG")
            return "Cette transaction n'est pas en attente d'approbation PDG.";

        tx.Status = "Payé";
        tx.RequiresPdgApproval = false;
        tx.ApprovedAt = DateTime.UtcNow;
        tx.ApprovedBy = string.IsNullOrWhiteSpace(approvedBy) ? "PDG" : approvedBy.Trim();
        tx.MarkUpdated();
        await _db.SaveChangesAsync(cancellationToken);
        return string.Empty;
    }

    /// <summary>
    /// Supprime les recettes non autorisées (seuls les loyers encaissés sont des revenus).
    /// </summary>
    public async Task<int> PurgeUnauthorizedReceiptsAsync(CancellationToken cancellationToken = default)
    {
        var unauthorized = await _db.FinancialTransactions
            .Where(t => t.Type == TransactionType.Recette &&
                        t.Category != FinanceConstants.CategoryRent &&
                        t.DeletedAt == null)
            .ToListAsync(cancellationToken);

        if (unauthorized.Count == 0)
            return 0;

        foreach (var tx in unauthorized)
            SoftDeleteLedgerEntry(tx);

        await _db.SaveChangesAsync(cancellationToken);
        return unauthorized.Count;
    }

    /// <summary>
    /// Aligne le ledger loyer sur les paiements sans recréer de nouveaux UUID (évite doublons cloud).
    /// </summary>
    public async Task<int> RebuildRentLedgerAsync(CancellationToken cancellationToken = default)
    {
        var payments = await _db.RentPayments
            .Include(p => p.LeaseContract)
            .ThenInclude(c => c!.Premise)
            .Where(p => p.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var activePaymentIds = payments
            .Where(p => p.AmountPaid > 0)
            .Select(p => p.Id)
            .ToHashSet();

        var rentEntries = await _db.FinancialTransactions
            .Where(t => t.Type == TransactionType.Recette
                        && t.Category == FinanceConstants.CategoryRent
                        && t.DeletedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var tx in rentEntries)
        {
            if (tx.RelatedEntityId is null || !activePaymentIds.Contains(tx.RelatedEntityId.Value))
                SoftDeleteLedgerEntry(tx);
        }

        var aligned = 0;
        foreach (var payment in payments.Where(p => p.AmountPaid > 0 && p.LeaseContract is not null))
        {
            await AlignRentLedgerWithPaymentAsync(payment, payment.LeaseContract!, cancellationToken);
            aligned++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return aligned;
    }

    public async Task<int> PurgeGuaranteeLedgerEntriesAsync(CancellationToken cancellationToken = default)
    {
        var entries = await _db.FinancialTransactions
            .Where(t => (t.Category == FinanceConstants.CategoryGuarantee ||
                         t.Category == FinanceConstants.CategoryGuaranteeRefund)
                        && t.DeletedAt == null)
            .ToListAsync(cancellationToken);

        if (entries.Count == 0)
            return 0;

        foreach (var tx in entries)
            SoftDeleteLedgerEntry(tx);

        await _db.SaveChangesAsync(cancellationToken);
        return entries.Count;
    }

    public async Task ReconcileAllAsync(CancellationToken cancellationToken = default)
    {
        await PurgeGuaranteeLedgerEntriesAsync(cancellationToken);
        await PurgeUnauthorizedReceiptsAsync(cancellationToken);
        await RebuildRentLedgerAsync(cancellationToken);
        await ReconcileFromPersonnelAsync(cancellationToken);
    }

    /// <summary>
    /// Trésorerie réelle : loyers = somme des encaissements Locations (RentPayments), pas le ledger corrompu.
    /// </summary>
    public async Task<FinanceCashPosition> GetCashPositionAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;

        var rentPaid = await _db.RentPayments
            .Select(p => new { p.Year, p.Month, p.AmountPaid })
            .ToListAsync(cancellationToken);

        var rentTotal = rentPaid.Sum(p => p.AmountPaid);
        var rentThisMonth = rentPaid
            .Where(p => p.Year == today.Year && p.Month == today.Month)
            .Sum(p => p.AmountPaid);

        var monthStart = new DateTime(today.Year, today.Month, 1);

        var expenseAmounts = await _db.FinancialTransactions
            .Where(t => t.Type == TransactionType.Depense &&
                        t.Status != "En attente validation PDG")
            .Select(t => new { t.Amount, t.TransactionDate })
            .ToListAsync(cancellationToken);

        var expensesTotal = expenseAmounts.Sum(t => t.Amount);
        var expensesMonth = expenseAmounts
            .Where(t => t.TransactionDate >= monthStart)
            .Sum(t => t.Amount);

        return new FinanceCashPosition
        {
            RentCollectedTotal = rentTotal,
            RentCollectedThisMonth = rentThisMonth,
            TotalExpenses = expensesTotal,
            TotalExpensesThisMonth = expensesMonth
        };
    }

    /// <summary>
    /// Vérifie qu'une dépense ne dépasse pas les loyers déjà encaissés.
    /// </summary>
    public async Task<string?> ValidateExpenseAsync(decimal amount, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            return "Le montant doit être supérieur à zéro.";

        var position = await GetCashPositionAsync(cancellationToken);
        if (position.CanSpend(amount))
            return null;

        return $"Trésorerie insuffisante. Loyers encaissés : {FinanceMetrics.Fc(position.RentCollectedTotal)}, " +
               $"dépenses engagées : {FinanceMetrics.Fc(position.TotalExpenses)}, " +
               $"disponible : {FinanceMetrics.Fc(position.AvailableBalance)}.";
    }

    public async Task<int> RemoveRentLedgerEntriesForPaymentAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var entries = await _db.FinancialTransactions
            .Where(t => t.RelatedEntityId == paymentId &&
                        t.Category == FinanceConstants.CategoryRent &&
                        t.Type == TransactionType.Recette &&
                        t.DeletedAt == null)
            .ToListAsync(cancellationToken);
        if (entries.Count == 0)
            return 0;

        foreach (var entry in entries)
            SoftDeleteLedgerEntry(entry);

        return entries.Count;
    }

    /// <summary>Aligne le ledger sur le montant réellement encaissé (une seule recette par paiement).</summary>
    public async Task AlignRentLedgerWithPaymentAsync(
        RentPayment payment,
        LeaseContract contract,
        CancellationToken cancellationToken = default)
    {
        if (payment.AmountPaid <= 0)
        {
            await RemoveRentLedgerEntriesForPaymentAsync(payment.Id, cancellationToken);
            return;
        }

        await RecordRentCollectionAsync(payment, contract, payment.AmountPaid, cancellationToken);
    }

    private static void SoftDeleteLedgerEntry(FinancialTransaction tx)
    {
        if (tx.DeletedAt is not null)
            return;

        tx.DeletedAt = DateTime.UtcNow;
        tx.IsSynced = false;
        tx.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<decimal> SumLinkedAmountAsync(
        Guid relatedEntityId,
        string category,
        CancellationToken cancellationToken)
    {
        var amounts = await _db.FinancialTransactions
            .Where(t => t.RelatedEntityId == relatedEntityId && t.Category == category)
            .Select(t => t.Amount)
            .ToListAsync(cancellationToken);
        return amounts.Sum();
    }

    private async Task<string> NextReferenceAsync(TransactionType type, CancellationToken cancellationToken)
    {
        var prefix = type == TransactionType.Recette ? "REV" : "DEP";
        var month = DateTime.Today.ToString("yyyyMM");
        var pattern = $"{prefix}-{month}-";
        var existing = await _db.FinancialTransactions
            .Where(t => t.Reference != null && t.Reference.StartsWith(pattern))
            .Select(t => t.Reference!)
            .ToListAsync(cancellationToken);
        var maxSeq = 0;
        foreach (var reference in existing)
        {
            var tail = reference.Length > pattern.Length
                ? reference[pattern.Length..]
                : "";
            if (int.TryParse(tail, out var seq) && seq > maxSeq)
                maxSeq = seq;
        }

        return $"{pattern}{maxSeq + 1:D4}";
    }
}

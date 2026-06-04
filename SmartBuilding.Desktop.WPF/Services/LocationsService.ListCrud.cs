using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Infrastructure.Persistence;

namespace SmartBuilding.Desktop.WPF.Services;

public partial class LocationsService
{
    public async Task<IReadOnlyList<LocationsContractItem>> GetAllContractsAsync(CancellationToken cancellationToken = default)
    {
        var contracts = await _db.LeaseContracts
            .Include(c => c.Tenant)
            .Include(c => c.Premise)
            .OrderByDescending(c => c.StartDate)
            .ToListAsync(cancellationToken);
        return MapContracts(contracts);
    }

    public async Task<IReadOnlyList<LocationsPremiseItem>> GetAllPremisesAsync(CancellationToken cancellationToken = default)
    {
        await ReconcilePremiseOccupancyAsync(cancellationToken);

        var today = DateTime.Today;
        var buildingName = await _db.BuildingInfos
            .Select(b => b.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Bâtiment principal";

        var premises = await _db.Premises.OrderBy(p => p.Code).ToListAsync(cancellationToken);
        var contracts = await _db.LeaseContracts
            .Include(c => c.Tenant)
            .Where(c => LeaseOccupancyRules.OccupyingStatuses.Contains(c.Status))
            .ToListAsync(cancellationToken);

        var activeByPremise = contracts
            .GroupBy(c => c.PremiseId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.EndDate).First());

        return premises
            .Select(p =>
            {
                activeByPremise.TryGetValue(p.Id, out var contract);
                return MapPremise(p, contract, buildingName, today);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<LocationsPaymentItem>> GetAllPaymentsAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var payments = await _db.RentPayments
            .Include(p => p.LeaseContract)
            .ThenInclude(c => c.Premise)
            .Include(p => p.LeaseContract)
            .ThenInclude(c => c.Tenant)
            .OrderByDescending(p => p.DueDate)
            .ToListAsync(cancellationToken);
        return MapPayments(payments, today);
    }

    public async Task<IReadOnlyList<LocationsGuaranteeItem>> GetAllGuaranteesAsync(CancellationToken cancellationToken = default)
    {
        var items = await _db.LeaseGuarantees
            .Include(g => g.LeaseContract)
            .ThenInclude(c => c.Tenant)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync(cancellationToken);
        return MapGuarantees(items);
    }

    public async Task<RentPayment?> GetRentPaymentAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.RentPayments
            .Include(p => p.LeaseContract)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<LeaseGuarantee?> GetGuaranteeAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.LeaseGuarantees
            .Include(g => g.LeaseContract)
            .ThenInclude(c => c.Tenant)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    public async Task<string> UpdateContractAsync(LeaseContract updated, CancellationToken cancellationToken = default)
    {
        var contract = await _db.LeaseContracts.FirstOrDefaultAsync(c => c.Id == updated.Id, cancellationToken);
        if (contract is null)
            return "Contrat introuvable.";
        if (updated.EndDate.Date < updated.StartDate.Date)
            return "La date de fin doit être postérieure à la date de début.";

        contract.StartDate = updated.StartDate.Date;
        contract.EndDate = updated.EndDate.Date;
        contract.MonthlyRent = updated.MonthlyRent;
        contract.Deposit = updated.Deposit;
        contract.ContractType = string.IsNullOrWhiteSpace(updated.ContractType)
            ? contract.ContractType
            : updated.ContractType.Trim();
        contract.Clauses = updated.Clauses?.Trim() ?? contract.Clauses;
        contract.MarkUpdated();

        await LogTenantActivityAsync(contract.TenantId, "Contrat", "Contrat modifié",
            $"Contrat {contract.ContractNumber} mis à jour.", cancellationToken);
        return await _db.SaveChangesWithMessageAsync(cancellationToken);
    }

    public async Task<string> DeleteContractAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var contract = await _db.LeaseContracts
            .Include(c => c.Premise)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (contract is null)
            return "Contrat introuvable.";

        if (contract.Status == LeaseStatus.Actif)
            return "Impossible de supprimer un contrat actif. Résiliez-le d'abord.";

        var hasPayments = await _db.RentPayments.AnyAsync(
            p => p.LeaseContractId == id && p.AmountPaid > 0, cancellationToken);
        if (hasPayments)
            return "Impossible de supprimer : des paiements sont liés à ce contrat.";

        if (contract.Premise is not null && contract.Premise.IsOccupied)
        {
            contract.Premise.IsOccupied = false;
            contract.Premise.OccupancyStatus = LocationConstants.PremiseOccupancyStatus.Available;
            contract.Premise.MarkUpdated();
        }

        contract.SoftDelete();
        await LogTenantActivityAsync(contract.TenantId, "Contrat", "Contrat supprimé",
            $"Contrat {contract.ContractNumber} supprimé.", cancellationToken);
        return await _db.SaveChangesWithMessageAsync(cancellationToken);
    }

    public async Task<string> UpdateRentPaymentAsync(RentPayment updated, CancellationToken cancellationToken = default)
    {
        var payment = await _db.RentPayments.FirstOrDefaultAsync(p => p.Id == updated.Id, cancellationToken);
        if (payment is null)
            return "Paiement introuvable.";
        if (updated.AmountDue < 0 || updated.AmountPaid < 0)
            return "Montants invalides.";
        if (RentPaymentRules.IsFullyPaid(payment.AmountDue, payment.AmountPaid, payment.PaymentStatus) &&
            updated.AmountPaid > payment.AmountPaid)
            return "Ce mois est déjà entièrement payé. Impossible d'ajouter un second paiement.";

        if (updated.AmountPaid > updated.AmountDue)
            return "Le montant payé ne peut pas dépasser le montant dû.";

        var today = DateTime.Today;
        payment.AmountDue = updated.AmountDue;
        payment.AmountPaid = updated.AmountPaid;
        payment.DueDate = updated.DueDate.Date;
        payment.PaidDate = updated.AmountPaid > 0 ? updated.PaidDate ?? DateTime.UtcNow : null;
        payment.IsLate = payment.AmountPaid < payment.AmountDue && payment.DueDate.Date < today;
        payment.PaymentStatus = payment.AmountPaid >= payment.AmountDue
            ? LocationConstants.PaymentStatus.Paid
            : payment.AmountPaid > 0
                ? LocationConstants.PaymentStatus.Partial
                : payment.IsLate
                    ? LocationConstants.PaymentStatus.Late
                    : LocationConstants.PaymentStatus.Pending;
        payment.MarkUpdated();

        var contract = await _db.LeaseContracts.FirstOrDefaultAsync(c => c.Id == payment.LeaseContractId, cancellationToken);
        if (contract is not null)
        {
            await LogTenantActivityAsync(contract.TenantId, "Paiement", "Paiement modifié",
                $"{payment.Month:00}/{payment.Year} — {Fc(payment.AmountPaid)}", cancellationToken);
        }

        return await _db.SaveChangesWithMessageAsync(cancellationToken);
    }

    public async Task<string> DeleteRentPaymentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var payment = await _db.RentPayments.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (payment is null)
            return "Paiement introuvable.";
        if (payment.AmountPaid > 0)
            return "Impossible de supprimer un paiement déjà encaissé.";

        payment.SoftDelete();
        return await _db.SaveChangesWithMessageAsync(cancellationToken);
    }

    public async Task<string> CreateGuaranteeAsync(LeaseGuarantee guarantee, CancellationToken cancellationToken = default)
    {
        if (guarantee.Amount <= 0)
            return "Le montant de la garantie doit être supérieur à zéro.";

        var contract = await _db.LeaseContracts
            .Include(c => c.Tenant)
            .FirstOrDefaultAsync(c => c.Id == guarantee.LeaseContractId, cancellationToken);
        if (contract is null)
            return "Contrat introuvable.";

        guarantee.Status = string.IsNullOrWhiteSpace(guarantee.Status)
            ? LocationConstants.GuaranteeStatus.Active
            : guarantee.Status.Trim();
        guarantee.IsSynced = false;
        _db.LeaseGuarantees.Add(guarantee);

        await LogTenantActivityAsync(contract.TenantId, "Garantie", "Garantie enregistrée",
            $"Caution {Fc(guarantee.Amount)} — {contract.ContractNumber}", cancellationToken);
        return await _db.SaveChangesWithMessageAsync(cancellationToken);
    }

    public async Task<string> UpdateGuaranteeAsync(LeaseGuarantee updated, CancellationToken cancellationToken = default)
    {
        var guarantee = await _db.LeaseGuarantees
            .Include(g => g.LeaseContract)
            .FirstOrDefaultAsync(g => g.Id == updated.Id, cancellationToken);
        if (guarantee is null)
            return "Garantie introuvable.";
        if (updated.Amount <= 0)
            return "Montant invalide.";
        if (updated.Amount < guarantee.AmountRefunded)
            return "Le montant ne peut pas être inférieur au montant déjà remboursé.";

        guarantee.Amount = updated.Amount;
        guarantee.Status = string.IsNullOrWhiteSpace(updated.Status)
            ? guarantee.Status
            : updated.Status.Trim();
        guarantee.Notes = updated.Notes?.Trim();
        guarantee.MarkUpdated();

        await LogTenantActivityAsync(guarantee.LeaseContract.TenantId, "Garantie", "Garantie modifiée",
            $"Caution {Fc(guarantee.Amount)}", cancellationToken);
        return await _db.SaveChangesWithMessageAsync(cancellationToken);
    }

    public async Task<string> DeleteGuaranteeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var guarantee = await _db.LeaseGuarantees.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (guarantee is null)
            return "Garantie introuvable.";
        if (guarantee.AmountRefunded > 0)
            return "Impossible de supprimer une garantie partiellement ou totalement remboursée.";

        guarantee.SoftDelete();
        return await _db.SaveChangesWithMessageAsync(cancellationToken);
    }
}

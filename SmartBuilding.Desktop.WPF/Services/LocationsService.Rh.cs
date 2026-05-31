using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Infrastructure.Persistence;

namespace SmartBuilding.Desktop.WPF.Services;

public partial class LocationsService
{
    private readonly LeaseContractPdfService _contractPdf = new();
    private readonly LeaseContractSummaryPdfService _contractSummaryPdf = new();
    private readonly GuaranteeDischargePdfService _dischargePdf = new();
    private readonly RentReceiptPdfService _receiptPdf = new();

    public async Task<Tenant?> GetTenantAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.Tenants
            .Include(t => t.Dependents.Where(d => d.DeletedAt == null))
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<Premise?> GetPremiseAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.Premises.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<LeaseContract?> GetContractAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.LeaseContracts
            .Include(c => c.Premise)
            .Include(c => c.Tenant)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<string> UpdatePremiseAsync(Premise premise, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Premises.FirstOrDefaultAsync(p => p.Id == premise.Id, cancellationToken);
        if (existing is null)
            return "Local introuvable.";

        existing.Name = premise.Name.Trim();
        existing.Floor = premise.Floor.Trim();
        existing.Building = premise.Building.Trim();
        existing.PremiseType = premise.PremiseType.Trim();
        existing.AreaSqM = premise.AreaSqM;
        existing.MonthlyRent = premise.MonthlyRent;
        existing.Description = premise.Description?.Trim();
        existing.Capacity = premise.Capacity;
        existing.Equipment = premise.Equipment.Trim();
        existing.ConditionNotes = premise.ConditionNotes.Trim();
        existing.OccupancyStatus = string.IsNullOrWhiteSpace(premise.OccupancyStatus)
            ? (existing.IsOccupied ? LocationConstants.PremiseOccupancyStatus.Occupied : LocationConstants.PremiseOccupancyStatus.Available)
            : premise.OccupancyStatus;
        if (!string.IsNullOrWhiteSpace(premise.PhotoPath))
            existing.PhotoPath = premise.PhotoPath;
        existing.MarkUpdated();
        return await _db.SaveChangesWithMessageAsync(cancellationToken);
    }

    public async Task<string> DeletePremiseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var premise = await _db.Premises.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (premise is null)
            return "Local introuvable.";
        if (premise.IsOccupied)
            return "Impossible de supprimer un local occupé.";

        premise.SoftDelete();
        return await _db.SaveChangesWithMessageAsync(cancellationToken);
    }

    public async Task<string> UpdateTenantAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenant.Name))
            return "Le nom est obligatoire.";

        var existing = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenant.Id, cancellationToken);
        if (existing is null)
            return "Locataire introuvable.";

        existing.Name = tenant.Name.Trim();
        existing.Email = tenant.Email.Trim();
        existing.Phone = tenant.Phone.Trim();
        existing.Company = tenant.Company?.Trim();
        existing.Address = tenant.Address?.Trim();
        existing.TenantCategory = tenant.TenantCategory.Trim();
        existing.NationalId = tenant.NationalId?.Trim();
        existing.IdDocumentType = tenant.IdDocumentType?.Trim();
        existing.IdDocumentExpiry = tenant.IdDocumentExpiry;
        existing.SecondaryPhone = tenant.SecondaryPhone?.Trim();
        existing.Employer = tenant.Employer?.Trim();
        existing.PreviousAddress = tenant.PreviousAddress?.Trim();
        existing.DateOfBirth = tenant.DateOfBirth;
        existing.Gender = tenant.Gender.Trim();
        existing.MaritalStatus = tenant.MaritalStatus.Trim();
        existing.SpouseName = tenant.SpouseName?.Trim();
        existing.ChildrenCount = tenant.ChildrenCount;
        existing.Profession = tenant.Profession?.Trim();
        existing.EmergencyContactName = tenant.EmergencyContactName?.Trim();
        existing.EmergencyContactPhone = tenant.EmergencyContactPhone?.Trim();
        existing.Notes = tenant.Notes?.Trim();
        existing.RentalStatus = string.IsNullOrWhiteSpace(tenant.RentalStatus)
            ? LocationConstants.TenantStatus.Active
            : tenant.RentalStatus.Trim();
        existing.Nationality = tenant.Nationality?.Trim();
        existing.BusinessActivity = tenant.BusinessActivity?.Trim();
        existing.PersonCount = tenant.PersonCount > 0 ? tenant.PersonCount : 1;
        if (!string.IsNullOrWhiteSpace(tenant.ProfilePhotoPath))
            existing.ProfilePhotoPath = tenant.ProfilePhotoPath;
        existing.MarkUpdated();

        await LogTenantActivityAsync(existing.Id, "Modification", "Fiche mise à jour", "Informations locataire modifiées.", cancellationToken);
        return await _db.SaveChangesWithMessageAsync(cancellationToken);
    }

    public async Task<string> DeleteTenantAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (tenant is null)
            return "Locataire introuvable.";

        var hasActive = await _db.LeaseContracts.AnyAsync(
            c => c.TenantId == id && c.Status == LeaseStatus.Actif, cancellationToken);
        if (hasActive)
            return "Le locataire a encore un contrat actif.";

        tenant.RentalStatus = LocationConstants.TenantStatus.Archived;
        tenant.SoftDelete();
        return await _db.SaveChangesWithMessageAsync(cancellationToken);
    }

    public async Task<string> SuspendTenantAsync(Guid id, string reason, CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (tenant is null)
            return "Locataire introuvable.";

        tenant.RentalStatus = LocationConstants.TenantStatus.Suspended;
        tenant.Notes = string.IsNullOrWhiteSpace(reason) ? tenant.Notes : reason.Trim();
        tenant.MarkUpdated();
        await LogTenantActivityAsync(id, "Suspension", "Locataire suspendu", reason, cancellationToken);
        return await _db.SaveChangesWithMessageAsync(cancellationToken);
    }

    public async Task<string> GenerateNextDossierNumberAsync(CancellationToken cancellationToken = default)
    {
        var count = await _db.Tenants.CountAsync(cancellationToken);
        return $"DOS-{(count + 1):D4}";
    }

    public async Task<string> ValidateContractAsync(Guid contractId, string validatedBy, CancellationToken cancellationToken = default)
    {
        var contract = await _db.LeaseContracts
            .Include(c => c.Premise)
            .FirstOrDefaultAsync(c => c.Id == contractId, cancellationToken);
        if (contract is null)
            return "Contrat introuvable.";

        contract.Status = LeaseStatus.Actif;
        contract.ValidatedBy = validatedBy;
        contract.ValidatedAt = DateTime.UtcNow;
        contract.MarkUpdated();

        if (contract.Premise is not null)
        {
            contract.Premise.IsOccupied = true;
            contract.Premise.OccupancyStatus = LocationConstants.PremiseOccupancyStatus.Occupied;
            contract.Premise.MarkUpdated();
        }

        await LogTenantActivityAsync(contract.TenantId, "Contrat", "Contrat validé",
            $"Contrat {contract.ContractNumber} validé et activé.", cancellationToken);
        return await _db.SaveChangesWithMessageAsync(cancellationToken);
    }

    public async Task<string> TerminateContractAsync(Guid contractId, string reason, string? cancelledBy = null, CancellationToken cancellationToken = default)
    {
        var contract = await _db.LeaseContracts
            .Include(c => c.Premise)
            .FirstOrDefaultAsync(c => c.Id == contractId, cancellationToken);
        if (contract is null)
            return "Contrat introuvable.";

        contract.Status = LeaseStatus.Resilie;
        contract.CancelledBy = cancelledBy;
        contract.CancelledAt = DateTime.UtcNow;
        contract.MarkUpdated();

        if (contract.Premise is not null)
        {
            contract.Premise.IsOccupied = false;
            contract.Premise.OccupancyStatus = LocationConstants.PremiseOccupancyStatus.Available;
            contract.Premise.MarkUpdated();
        }

        await LogTenantActivityAsync(contract.TenantId, "Contrat", "Contrat résilié",
            string.IsNullOrWhiteSpace(reason) ? $"Contrat {contract.ContractNumber} résilié." : reason, cancellationToken);
        return await _db.SaveChangesWithMessageAsync(cancellationToken);
    }

    public async Task<string> CancelContractAsync(Guid contractId, string reason, string? cancelledBy = null, CancellationToken cancellationToken = default)
    {
        var contract = await _db.LeaseContracts
            .Include(c => c.Premise)
            .FirstOrDefaultAsync(c => c.Id == contractId, cancellationToken);
        if (contract is null)
            return "Contrat introuvable.";

        contract.Status = LeaseStatus.Annule;
        contract.CancelledBy = cancelledBy;
        contract.CancelledAt = DateTime.UtcNow;
        contract.MarkUpdated();

        if (contract.Premise is not null && contract.Premise.IsOccupied)
        {
            contract.Premise.IsOccupied = false;
            contract.Premise.OccupancyStatus = LocationConstants.PremiseOccupancyStatus.Available;
            contract.Premise.MarkUpdated();
        }

        await LogTenantActivityAsync(contract.TenantId, "Contrat", "Contrat annulé", reason, cancellationToken);
        return await _db.SaveChangesWithMessageAsync(cancellationToken);
    }

    public async Task<string> RenewContractAsync(
        Guid contractId,
        DateTime newEndDate,
        decimal? newRent,
        CancellationToken cancellationToken = default)
    {
        var contract = await _db.LeaseContracts.FirstOrDefaultAsync(c => c.Id == contractId, cancellationToken);
        if (contract is null)
            return "Contrat introuvable.";
        if (newEndDate.Date < contract.StartDate.Date)
            return "La nouvelle date de fin est invalide.";

        contract.EndDate = newEndDate.Date;
        if (newRent.HasValue)
            contract.MonthlyRent = newRent.Value;
        contract.Status = LeaseStatus.Actif;
        contract.MarkUpdated();

        await LogTenantActivityAsync(contract.TenantId, "Contrat", "Contrat renouvelé",
            $"Renouvellement jusqu'au {newEndDate:dd/MM/yyyy}.", cancellationToken);
        return await _db.SaveChangesWithMessageAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LeaseGuarantee>> GetGuaranteesAsync(CancellationToken cancellationToken = default) =>
        await _db.LeaseGuarantees
            .Include(g => g.LeaseContract)
            .ThenInclude(c => c.Tenant)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<string> RefundGuaranteeAsync(Guid guaranteeId, decimal amount, CancellationToken cancellationToken = default)
    {
        var g = await _db.LeaseGuarantees
            .Include(x => x.LeaseContract)
            .ThenInclude(c => c.Tenant)
            .Include(x => x.LeaseContract)
            .ThenInclude(c => c.Premise)
            .FirstOrDefaultAsync(x => x.Id == guaranteeId, cancellationToken);
        if (g is null)
            return "Garantie introuvable.";
        if (amount <= 0)
            return "Montant invalide.";

        g.AmountRefunded += amount;
        var isFullyRefunded = g.AmountRefunded >= g.Amount;
        g.Status = isFullyRefunded
            ? LocationConstants.GuaranteeStatus.Refunded
            : LocationConstants.GuaranteeStatus.Partial;
        if (isFullyRefunded)
            g.RefundedAt = DateTime.UtcNow;
        g.MarkUpdated();

        // Remboursement de garantie = fin d'occupation.
        g.LeaseContract.Status = LeaseStatus.Resilie;
        g.LeaseContract.CancelledAt = DateTime.UtcNow;
        g.LeaseContract.CancelledBy ??= "Système";
        g.LeaseContract.MarkUpdated();

        var premise = await _db.Premises.FirstOrDefaultAsync(p => p.Id == g.LeaseContract.PremiseId, cancellationToken);
        if (premise is not null)
        {
            premise.IsOccupied = false;
            premise.OccupancyStatus = LocationConstants.PremiseOccupancyStatus.Available;
            premise.MarkUpdated();
        }

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == g.LeaseContract.TenantId, cancellationToken);
        if (tenant is not null)
        {
            tenant.RentalStatus = LocationConstants.TenantStatus.Archived;
            tenant.MarkUpdated();
        }

        if (isFullyRefunded && g.LeaseContract.Tenant is not null && g.LeaseContract.Premise is not null)
        {
            var building = await _db.BuildingInfos.FirstOrDefaultAsync(cancellationToken);
            var refundDate = (g.RefundedAt ?? DateTime.UtcNow).ToLocalTime().Date;
            g.DischargePdfPath = _dischargePdf.Generate(
                g,
                g.LeaseContract,
                g.LeaseContract.Tenant,
                g.LeaseContract.Premise,
                building,
                g.AmountRefunded,
                refundDate);
        }

        await LogTenantActivityAsync(g.LeaseContract.TenantId, "Garantie", "Remboursement garantie",
            isFullyRefunded && !string.IsNullOrWhiteSpace(g.DischargePdfPath)
                ? $"{amount:N2} FC remboursés — décharge : {g.DischargePdfPath}"
                : $"{amount:N2} FC remboursés.",
            cancellationToken);
        return await _db.SaveChangesWithMessageAsync(cancellationToken);
    }

    public async Task<string?> GetGuaranteeDischargePdfPathAsync(Guid guaranteeId, CancellationToken cancellationToken = default)
    {
        var g = await _db.LeaseGuarantees
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == guaranteeId, cancellationToken);
        return string.IsNullOrWhiteSpace(g?.DischargePdfPath) ? null : g.DischargePdfPath;
    }

    public IReadOnlyList<string> GetLocationAlerts(LocationsPageData data, DateTime today)
    {
        var alerts = new List<string>();
        foreach (var p in data.Premises.Where(x => x.EndContractIsWarning))
            alerts.Add($"Contrat {p.ContractNumber} expire bientôt ({p.EndContractDisplay}).");
        if (data.LatePayments > 0)
            alerts.Add($"{data.LatePayments} paiement(s) en retard ce mois.");
        if (data.AvailablePremises > 0)
            alerts.Add($"{data.AvailablePremises} espace(s) disponible(s).");
        var pending = data.Contracts.Count(c => c.StatusLabel == "En attente validation");
        if (pending > 0)
            alerts.Add($"{pending} contrat(s) en attente de validation.");
        return alerts;
    }

    public async Task<string?> GenerateContractPdfAsync(Guid contractId, CancellationToken cancellationToken = default) =>
        await GenerateContractSummaryPdfAsync(contractId, null, null, cancellationToken);

    public async Task<string?> GenerateContractSummaryPdfAsync(
        Guid contractId,
        string? paymentFrequency = null,
        string? paymentMethod = null,
        CancellationToken cancellationToken = default)
    {
        var contract = await _db.LeaseContracts
            .Include(c => c.Premise)
            .Include(c => c.Tenant)
            .FirstOrDefaultAsync(c => c.Id == contractId, cancellationToken);
        if (contract is null)
            return null;

        var building = await _db.BuildingInfos.FirstOrDefaultAsync(cancellationToken);
        contract.ContractPdfPath = _contractSummaryPdf.Generate(contract, building, paymentFrequency, paymentMethod);
        contract.MarkUpdated();
        if (!string.IsNullOrEmpty(await _db.SaveChangesWithMessageAsync(cancellationToken)))
            return null;
        return contract.ContractPdfPath;
    }

    public async Task<IReadOnlyList<Building>> GetBuildingsAsync(CancellationToken cancellationToken = default) =>
        await _db.Buildings.OrderBy(b => b.Name).ToListAsync(cancellationToken);

    public async Task<string> UpdateBuildingAsync(Building building, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Buildings.FirstOrDefaultAsync(b => b.Id == building.Id, cancellationToken);
        if (existing is null)
            return "Bâtiment introuvable.";

        existing.Name = building.Name.Trim();
        existing.Address = building.Address.Trim();
        existing.BuildingType = building.BuildingType.Trim();
        existing.FloorCount = building.FloorCount;
        existing.PremiseCount = building.PremiseCount;
        existing.Capacity = building.Capacity;
        existing.Status = building.Status.Trim();
        existing.Equipment = building.Equipment?.Trim() ?? existing.Equipment;
        existing.Zones = building.Zones?.Trim() ?? existing.Zones;
        existing.Notes = building.Notes?.Trim();
        existing.LandlordId = building.LandlordId;
        existing.MarkUpdated();
        if (building.LandlordId.HasValue)
        {
            await LogLandlordActivityAsync(
                building.LandlordId.Value,
                "Bâtiment",
                "Bâtiment mis à jour",
                $"Lien patrimoine : {existing.Name}",
                cancellationToken);
        }
        return await _db.SaveChangesWithMessageAsync(cancellationToken);
    }

    public async Task<string> DeleteBuildingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var building = await _db.Buildings.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (building is null)
            return "Bâtiment introuvable.";

        var linked = await _db.Premises.AnyAsync(p => p.BuildingId == id, cancellationToken);
        if (linked)
            return "Des locaux sont liés à ce bâtiment.";

        building.SoftDelete();
        return await _db.SaveChangesWithMessageAsync(cancellationToken);
    }

    public async Task<string?> GenerateRentReceiptPdfAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        var payment = await _db.RentPayments
            .Include(p => p.LeaseContract)
            .ThenInclude(c => c.Premise)
            .Include(p => p.LeaseContract)
            .ThenInclude(c => c.Tenant)
            .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
        if (payment is null || payment.AmountPaid <= 0)
            return null;

        return await GenerateReceiptForPaymentAsync(
            payment,
            payment.LeaseContract,
            payment.AmountPaid,
            cancellationToken);
    }

    private async Task<string?> GenerateReceiptForPaymentAsync(
        RentPayment payment,
        LeaseContract contract,
        decimal amountThisReceipt,
        CancellationToken cancellationToken)
    {
        if (payment.AmountPaid <= 0 || contract.Tenant is null || contract.Premise is null)
            return null;

        var building = await _db.BuildingInfos.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        payment.ReceiptNumber ??=
            $"QTL-{payment.Year}-{payment.Month:D2}-{payment.Id.ToString("N")[..8].ToUpperInvariant()}";

        var history = await _db.RentPayments
            .AsNoTracking()
            .Where(p => p.LeaseContractId == payment.LeaseContractId && p.AmountPaid > 0)
            .OrderByDescending(p => p.Year)
            .ThenByDescending(p => p.Month)
            .Take(6)
            .ToListAsync(cancellationToken);

        payment.ReceiptPdfPath = _receiptPdf.Generate(
            payment,
            contract,
            building,
            amountThisReceipt,
            history);
        payment.MarkUpdated();

        await LogTenantActivityAsync(contract.TenantId, "Paiement", "Quittance générée",
            $"Quittance {payment.ReceiptNumber} — {payment.Month:00}/{payment.Year}", cancellationToken);
        if (!string.IsNullOrEmpty(await _db.SaveChangesWithMessageAsync(cancellationToken)))
            return null;

        return payment.ReceiptPdfPath;
    }

    public async Task<string> CreateBuildingAsync(Building building, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(building.Name))
            return "Le nom du bâtiment est obligatoire.";

        if (string.IsNullOrWhiteSpace(building.Code))
            building.Code = $"BAT-{(await _db.Buildings.CountAsync(cancellationToken) + 1):D3}";

        building.Name = building.Name.Trim();
        building.Address = building.Address.Trim();
        building.BuildingType = string.IsNullOrWhiteSpace(building.BuildingType)
            ? LocationConstants.BuildingTypes.Office
            : building.BuildingType.Trim();

        _db.Buildings.Add(building);
        return await _db.SaveChangesWithMessageAsync(cancellationToken);
    }

    public async Task<Guid?> GetLatestPaymentIdForContractAsync(Guid contractId, CancellationToken cancellationToken = default)
    {
        var payment = await _db.RentPayments
            .Where(p => p.LeaseContractId == contractId && p.AmountPaid > 0)
            .OrderByDescending(p => p.PaidDate)
            .ThenByDescending(p => p.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return payment?.Id;
    }

    private void TrackTenantActivity(
        Guid tenantId,
        string category,
        string title,
        string description)
    {
        _db.TenantActivities.Add(new TenantActivity
        {
            TenantId = tenantId,
            OccurredAt = DateTime.UtcNow,
            Category = category.Trim(),
            Title = title.Trim(),
            Description = description.Trim()
        });
    }

    public Task LogTenantActivityAsync(
        Guid tenantId,
        string category,
        string title,
        string description,
        CancellationToken cancellationToken = default)
    {
        TrackTenantActivity(tenantId, category, title, description);
        return Task.CompletedTask;
    }

}

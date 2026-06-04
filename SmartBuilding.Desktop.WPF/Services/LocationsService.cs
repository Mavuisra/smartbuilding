using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Services;

namespace SmartBuilding.Desktop.WPF.Services;

public partial class LocationsService
{
    private readonly SmartBuildingDbContext _db;
    private readonly FinanceLedgerService _financeLedger;

    public LocationsService(SmartBuildingDbContext db, FinanceLedgerService financeLedger)
    {
        _db = db;
        _financeLedger = financeLedger;
    }

    public async Task<LocationsPageData> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _financeLedger.ReconcileAllAsync(cancellationToken);
        await CancelOverpaidRentPaymentsAsync(cancellationToken);
        await ReconcilePremiseOccupancyAsync(cancellationToken);
        var cashPosition = await _financeLedger.GetCashPositionAsync(cancellationToken);

        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var buildingName = await _db.BuildingInfos
            .Select(b => b.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Bâtiment principal";

        var premises = await _db.Premises.OrderBy(p => p.Code).ToListAsync(cancellationToken);
        var contracts = await _db.LeaseContracts
            .Include(c => c.Tenant)
            .Include(c => c.Premise)
            .ToListAsync(cancellationToken);
        var payments = await _db.RentPayments
            .Include(p => p.LeaseContract)
            .ThenInclude(c => c.Premise)
            .Include(p => p.LeaseContract)
            .ThenInclude(c => c.Tenant)
            .ToListAsync(cancellationToken);
        var tenants = await _db.Tenants.OrderBy(t => t.Name).ToListAsync(cancellationToken);
        var buildingRows = await _db.Buildings.OrderBy(b => b.Name).ToListAsync(cancellationToken);
        var guaranteeEntities = await _db.LeaseGuarantees
            .Include(g => g.LeaseContract)
            .ThenInclude(c => c.Tenant)
            .OrderByDescending(g => g.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);
        var activeGuarantees = guaranteeEntities.Count(g => g.Status == LocationConstants.GuaranteeStatus.Active);
        var activityEntities = await _db.TenantActivities
            .Include(a => a.Tenant)
            .OrderByDescending(a => a.OccurredAt)
            .Take(80)
            .ToListAsync(cancellationToken);

        var occupyingContracts = contracts
            .Where(c => LeaseOccupancyRules.OccupiesPremise(c.Status))
            .GroupBy(c => c.PremiseId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.EndDate).First());

        var total = premises.Count;
        var t = Math.Max(total, 1);

        var monthPayments = payments.Where(p => p.Year == today.Year && p.Month == today.Month).ToList();
        var monthlyCollected = monthPayments.Sum(p => p.AmountPaid);
        var lateCount = monthPayments.Count(p => p.IsLate || (p.AmountPaid < p.AmountDue && p.DueDate.Date < today));

        var items = premises.Select(p =>
        {
            occupyingContracts.TryGetValue(p.Id, out var contract);
            return MapPremise(p, contract, buildingName, today);
        }).ToList();

        var occupied = items.Count(p => p.StatusLabel == "Occupé");
        var available = total - occupied;

        var typeDistribution = items
            .GroupBy(p => string.IsNullOrWhiteSpace(p.PremiseType) ? "Autre" : p.PremiseType)
            .OrderByDescending(g => g.Count())
            .Select(g => new LocationsTypeSlice { Type = g.Key, Count = g.Count() })
            .ToList();

        var rentTrend = new List<decimal>();
        var rentLabels = new List<string>();
        for (var i = 5; i >= 0; i--)
        {
            var d = monthStart.AddMonths(-i);
            var sum = payments
                .Where(p => p.Year == d.Year && p.Month == d.Month)
                .Sum(p => p.AmountPaid);
            rentTrend.Add(sum);
            rentLabels.Add(d.ToString("MMM"));
        }

        var rentOccupied = items.Where(p => p.StatusLabel == "Occupé").Sum(p => p.MonthlyRent);
        var rentLate = monthPayments.Where(p => p.IsLate).Sum(p => p.AmountDue - p.AmountPaid);
        var rentAvailable = items.Where(p => p.StatusLabel == "Disponible").Sum(p => p.MonthlyRent);

        return new LocationsPageData
        {
            TotalPremises = total,
            OccupiedPremises = occupied,
            AvailablePremises = available,
            OccupiedPercent = $"{occupied * 100.0 / t:F2}%",
            AvailablePercent = $"{available * 100.0 / t:F2}%",
            MonthlyRentCollected = monthlyCollected,
            RentCollectedTotal = cashPosition.RentCollectedTotal,
            AvailableBalance = cashPosition.AvailableBalance,
            TotalExpenses = cashPosition.TotalExpenses,
            LatePayments = lateCount,
            LatePercent = total > 0 ? $"{lateCount * 100.0 / t:F2}%" : "0%",
            ActiveContracts = contracts.Count(c => c.Status == LeaseStatus.Actif),
            ActiveGuarantees = activeGuarantees,
            OccupancyRate = total > 0 ? Math.Round(occupied * 100.0 / total, 2) : 0,
            Premises = items,
            Contracts = MapContracts(contracts.Where(c => c.Status != LeaseStatus.Resilie && c.Status != LeaseStatus.Annule)),
            Payments = MapPayments(monthPayments, today),
            Tenants = MapTenants(tenants, contracts),
            BuildingRows = MapBuildings(buildingRows),
            Guarantees = MapGuarantees(guaranteeEntities),
            RecentActivities = MapActivities(activityEntities),
            LatePaymentRows = MapPayments(monthPayments.Where(p => p.IsLate || p.AmountPaid < p.AmountDue), today),
            TerminatedContracts = MapContracts(contracts.Where(c => c.Status == LeaseStatus.Resilie)),
            TypeDistribution = typeDistribution,
            RentTrend = rentTrend,
            RentTrendLabels = rentLabels,
            RentOccupied = rentOccupied,
            RentLate = rentLate,
            RentAvailable = rentAvailable
        };
    }

    public async Task<string> CreatePremiseAsync(Premise premise, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(premise.Code))
            return "Le code local est obligatoire.";
        if (string.IsNullOrWhiteSpace(premise.Name))
            return "Le nom du local est obligatoire.";

        var exists = await _db.Premises.AnyAsync(p => p.Code == premise.Code.Trim(), cancellationToken);
        if (exists)
            return "Ce code local existe déjà.";

        premise.Code = premise.Code.Trim();
        premise.Name = premise.Name.Trim();
        premise.Floor = premise.Floor.Trim();
        premise.Building = premise.Building.Trim();
        premise.PremiseType = string.IsNullOrWhiteSpace(premise.PremiseType)
            ? LocationConstants.DefaultPremiseType
            : premise.PremiseType.Trim();
        premise.IsSynced = false;

        _db.Premises.Add(premise);
        return await _db.SaveChangesWithMessageAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LocationsPickItem>> GetAvailablePremisesAsync(CancellationToken cancellationToken = default)
    {
        await ReconcilePremiseOccupancyAsync(cancellationToken);

        var occupiedPremiseIds = await _db.LeaseContracts
            .Where(c => LeaseOccupancyRules.OccupyingStatuses.Contains(c.Status))
            .Select(c => c.PremiseId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var premises = await _db.Premises
            .Where(p => !occupiedPremiseIds.Contains(p.Id))
            .OrderBy(p => p.Code)
            .ToListAsync(cancellationToken);

        return premises
            .Select(p => new LocationsPickItem
            {
                Id = p.Id,
                Label = p.Code + " — " + p.Name,
                Code = p.Code,
                Name = p.Name,
                Building = p.Building,
                Floor = p.Floor,
                Type = p.PremiseType,
                StatusLabel = string.IsNullOrWhiteSpace(p.OccupancyStatus) ? "Disponible" : p.OccupancyStatus,
                AreaDisplay = p.AreaSqM > 0 ? $"{p.AreaSqM:N0} m²" : "—",
                MonthlyRent = p.MonthlyRent,
                RentDisplay = Fc(p.MonthlyRent),
                PhotoPath = p.PhotoPath ?? string.Empty
            })
            .ToList();
    }

    public async Task<IReadOnlyList<LocationsPickItem>> GetTenantsAsync(CancellationToken cancellationToken = default)
    {
        var tenants = await _db.Tenants
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return tenants
            .Select(t => new LocationsPickItem
            {
                Id = t.Id,
                TenantId = t.Id,
                Label = t.Name,
                Name = t.Name,
                Phone = t.Phone,
                Email = t.Email,
                Company = t.Company ?? "—",
                Address = t.Address ?? "—",
                Category = t.TenantCategory,
                Nationality = t.Nationality ?? "—",
                Gender = string.IsNullOrWhiteSpace(t.Gender) ? "—" : t.Gender,
                MaritalStatus = string.IsNullOrWhiteSpace(t.MaritalStatus) ? "—" : t.MaritalStatus,
                DateOfBirthDisplay = t.DateOfBirth?.ToString("dd/MM/yyyy") ?? "—",
                PersonCountDisplay = t.PersonCount <= 0 ? "—" : t.PersonCount.ToString(),
                Profession = t.Profession ?? "—",
                BusinessActivity = t.BusinessActivity ?? "—",
                EmergencyContactDisplay = string.Join(" / ", new[] { t.EmergencyContactName, t.EmergencyContactPhone }
                    .Where(v => !string.IsNullOrWhiteSpace(v))),
                ProfilePhotoPath = t.ProfilePhotoPath ?? string.Empty
            })
            .ToList();
    }

    public async Task<IReadOnlyList<LocationsPickItem>> GetActiveContractsAsync(CancellationToken cancellationToken = default) =>
        await _db.LeaseContracts
            .Include(c => c.Premise)
            .Include(c => c.Tenant)
            .Where(c => LeaseOccupancyRules.OccupyingStatuses.Contains(c.Status))
            .OrderBy(c => c.ContractNumber)
            .Select(c => new LocationsPickItem
            {
                Id = c.Id,
                TenantId = c.TenantId,
                PremiseId = c.PremiseId,
                Label = c.ContractNumber + " — " + c.Premise!.Code + " / " + c.Premise!.Name + " — " + c.Tenant!.Name +
                        (c.Status == LeaseStatus.EnAttenteValidation ? " (À valider)" : string.Empty),
                Code = c.Premise!.Code,
                Name = c.Premise!.Name,
                MonthlyRent = c.MonthlyRent,
                RentDisplay = Fc(c.MonthlyRent)
            })
            .ToListAsync(cancellationToken);

    public async Task<string?> ValidateNoDuplicateContractAsync(
        Guid tenantId,
        Guid premiseId,
        CancellationToken cancellationToken = default)
    {
        var exists = await _db.LeaseContracts.AnyAsync(
            c => c.TenantId == tenantId &&
                 c.PremiseId == premiseId &&
                 LeaseOccupancyRules.OccupyingStatuses.Contains(c.Status),
            cancellationToken);

        return exists
            ? "Un contrat existe déjà pour ce locataire sur ce local. Impossible d'en créer un second."
            : null;
    }

    public async Task<string> CreateTenantAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenant.Name))
            return "Le nom du locataire est obligatoire.";
        if (string.IsNullOrWhiteSpace(tenant.Phone))
            return "Le téléphone est obligatoire.";

        tenant.Name = tenant.Name.Trim();
        tenant.Email = tenant.Email.Trim();
        tenant.Phone = tenant.Phone.Trim();
        tenant.Company = tenant.Company?.Trim();
        tenant.Address = tenant.Address?.Trim();
        tenant.Nationality = tenant.Nationality?.Trim();
        tenant.Profession = tenant.Profession?.Trim();
        tenant.NationalId = tenant.NationalId?.Trim();
        tenant.IdDocumentType = tenant.IdDocumentType?.Trim();
        tenant.IdDocumentExpiry = tenant.IdDocumentExpiry;
        tenant.SecondaryPhone = tenant.SecondaryPhone?.Trim();
        tenant.Employer = tenant.Employer?.Trim();
        tenant.PreviousAddress = tenant.PreviousAddress?.Trim();
        tenant.EmergencyContactName = tenant.EmergencyContactName?.Trim();
        tenant.EmergencyContactPhone = tenant.EmergencyContactPhone?.Trim();
        tenant.Notes = tenant.Notes?.Trim();
        tenant.TenantCategory = string.IsNullOrWhiteSpace(tenant.TenantCategory)
            ? LocationConstants.TenantCategories.Individual
            : tenant.TenantCategory.Trim();
        tenant.DateOfBirth = tenant.DateOfBirth;
        tenant.Gender = tenant.Gender?.Trim() ?? string.Empty;
        tenant.MaritalStatus = tenant.MaritalStatus?.Trim() ?? string.Empty;
        tenant.SpouseName = tenant.SpouseName?.Trim();
        tenant.ChildrenCount = tenant.ChildrenCount;
        tenant.BusinessActivity = tenant.BusinessActivity?.Trim();
        tenant.PersonCount = tenant.PersonCount > 0 ? tenant.PersonCount : 1;
        tenant.RentalStatus = string.IsNullOrWhiteSpace(tenant.RentalStatus)
            ? LocationConstants.TenantStatus.Active
            : tenant.RentalStatus.Trim();
        if (string.IsNullOrWhiteSpace(tenant.DossierNumber))
            tenant.DossierNumber = $"DOS-{(await _db.Tenants.CountAsync(cancellationToken) + 1):D4}";
        tenant.IsSynced = false;

        _db.Tenants.Add(tenant);
        await LogTenantActivityAsync(tenant.Id, "Création", "Locataire créé", $"Dossier {tenant.DossierNumber}", cancellationToken);
        return await _db.SaveChangesWithMessageAsync(cancellationToken);
    }

    public async Task<CreateContractResult> CreateContractAsync(
        Guid premiseId,
        Guid tenantId,
        DateTime startDate,
        DateTime endDate,
        decimal monthlyRent,
        decimal deposit,
        string? contractType = null,
        string? clauses = null,
        string? paymentFrequency = null,
        string? paymentMethod = null,
        CancellationToken cancellationToken = default)
    {
        if (endDate.Date < startDate.Date)
            return new CreateContractResult("La date de fin doit être après la date de début.");

        var premise = await _db.Premises.FirstOrDefaultAsync(p => p.Id == premiseId, cancellationToken);
        if (premise is null)
            return new CreateContractResult("Local introuvable.");

        var tenant = await _db.Tenants.AnyAsync(t => t.Id == tenantId, cancellationToken);
        if (!tenant)
            return new CreateContractResult("Locataire introuvable.");

        var duplicateError = await ValidateNoDuplicateContractAsync(tenantId, premiseId, cancellationToken);
        if (duplicateError is not null)
            return new CreateContractResult(duplicateError);

        var occupyingOnPremise = await _db.LeaseContracts
            .Where(c => c.PremiseId == premiseId &&
                        LeaseOccupancyRules.OccupyingStatuses.Contains(c.Status))
            .ToListAsync(cancellationToken);
        if (occupyingOnPremise.Count > 0 &&
            occupyingOnPremise.All(c => c.TenantId != tenantId))
            return new CreateContractResult("Ce local est déjà occupé par un autre locataire.");

        var contractNumber = await GenerateNextContractNumberAsync(cancellationToken);

        var contract = new LeaseContract
        {
            PremiseId = premiseId,
            TenantId = tenantId,
            ContractNumber = contractNumber,
            StartDate = startDate.Date,
            EndDate = endDate.Date,
            MonthlyRent = monthlyRent,
            Deposit = deposit,
            ContractType = string.IsNullOrWhiteSpace(contractType) ? LocationConstants.DefaultContractType : contractType.Trim(),
            Clauses = clauses?.Trim() ?? string.Empty,
            Status = LeaseStatus.EnAttenteValidation,
            IsSynced = false
        };

        premise.MonthlyRent = monthlyRent;
        premise.IsOccupied = true;
        premise.OccupancyStatus = LocationConstants.PremiseOccupancyStatus.Occupied;
        premise.IsSynced = false;

        _db.LeaseContracts.Add(contract);

        if (deposit > 0)
        {
            _db.LeaseGuarantees.Add(new LeaseGuarantee
            {
                LeaseContractId = contract.Id,
                Amount = deposit,
                Status = LocationConstants.GuaranteeStatus.Active,
                IsSynced = false
            });
        }

        await LogTenantActivityAsync(tenantId, "Contrat", "Contrat créé",
            $"Contrat {contract.ContractNumber} en attente de validation.", cancellationToken);

        var today = DateTime.Today;
        var existingMonthPayment = await _db.RentPayments.AnyAsync(
            p => p.LeaseContractId == contract.Id && p.Year == today.Year && p.Month == today.Month,
            cancellationToken);
        if (!existingMonthPayment)
        {
            _db.RentPayments.Add(new RentPayment
            {
                LeaseContractId = contract.Id,
                Year = today.Year,
                Month = today.Month,
                AmountDue = monthlyRent,
                AmountPaid = 0,
                DueDate = new DateTime(today.Year, today.Month, Math.Min(28, DateTime.DaysInMonth(today.Year, today.Month))),
                IsLate = false,
                PaymentStatus = LocationConstants.PaymentStatus.Pending,
                IsSynced = false
            });
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var saveError = await _db.SaveChangesWithMessageAsync(cancellationToken);
        if (!string.IsNullOrEmpty(saveError))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new CreateContractResult(saveError);
        }

        await transaction.CommitAsync(cancellationToken);

        var summaryPath = await GenerateContractSummaryPdfAsync(
            contract.Id, paymentFrequency, paymentMethod, cancellationToken);

        return new CreateContractResult(string.Empty, contract.Id, summaryPath);
    }

    public async Task<PremiseOccupancyStats> GetPremiseOccupancyStatsAsync(CancellationToken cancellationToken = default)
    {
        await ReconcilePremiseOccupancyAsync(cancellationToken);

        var total = await _db.Premises.CountAsync(cancellationToken);
        var occupyingContracts = await _db.LeaseContracts
            .Where(c => LeaseOccupancyRules.OccupyingStatuses.Contains(c.Status))
            .Select(c => new { c.PremiseId, c.Status })
            .ToListAsync(cancellationToken);

        var premiseStatuses = occupyingContracts
            .GroupBy(c => c.PremiseId)
            .Select(g => g.Any(x => x.Status == LeaseStatus.Actif)
                ? LeaseStatus.Actif
                : LeaseStatus.EnAttenteValidation)
            .ToList();

        var occupied = premiseStatuses.Count(s => s == LeaseStatus.Actif);
        var pending = premiseStatuses.Count(s => s == LeaseStatus.EnAttenteValidation);
        var reserved = premiseStatuses.Count;
        var available = Math.Max(0, total - reserved);
        var rate = total > 0 ? Math.Round(reserved * 100.0 / total, 1) : 0;

        return new PremiseOccupancyStats
        {
            TotalPremises = total,
            AvailablePremises = available,
            OccupiedPremises = occupied,
            PendingPremises = pending,
            OccupancyRate = rate
        };
    }

    public Task<string> RecordRentPaymentAsync(
        Guid contractId,
        decimal amountPaid,
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        return RecordRentPaymentDetailedAsync(
            contractId,
            amountPaid,
            today.Year,
            today.Month,
            today,
            "Espèces",
            null,
            null,
            cancellationToken);
    }

    public async Task<string> RecordRentPaymentDetailedAsync(
        Guid contractId,
        decimal amountPaid,
        int year,
        int month,
        DateTime paymentDate,
        string paymentMethod,
        string? transactionReference,
        string? paymentStatusOverride,
        CancellationToken cancellationToken = default)
    {
        if (amountPaid <= 0)
            return "Le montant encaissé doit être supérieur à zéro.";
        if (month is < 1 or > 12)
            return "Mois invalide.";
        if (year < 2000 || year > 2100)
            return "Année invalide.";

        var contract = await _db.LeaseContracts
            .Include(c => c.Premise)
            .Include(c => c.Tenant)
            .FirstOrDefaultAsync(c =>
                c.Id == contractId &&
                LeaseOccupancyRules.OccupyingStatuses.Contains(c.Status),
                cancellationToken);
        if (contract is null)
            return "Contrat introuvable.";

        if (contract.Status == LeaseStatus.EnAttenteValidation)
        {
            contract.Status = LeaseStatus.Actif;
            contract.ValidatedAt = DateTime.UtcNow;
            contract.ValidatedBy = "Validation auto (paiement)";
            contract.MarkUpdated();

            if (contract.Premise is not null)
            {
                contract.Premise.IsOccupied = true;
                contract.Premise.OccupancyStatus = LocationConstants.PremiseOccupancyStatus.Occupied;
                contract.Premise.MarkUpdated();
            }
        }

        var payment = await _db.RentPayments
            .FirstOrDefaultAsync(p => p.LeaseContractId == contractId && p.Year == year && p.Month == month,
                cancellationToken);

        var amountDue = payment?.AmountDue ?? contract.MonthlyRent;
        if (amountDue <= 0)
            amountDue = contract.MonthlyRent;

        if (RentPaymentRules.IsFullyPaid(amountDue, payment?.AmountPaid ?? 0, payment?.PaymentStatus))
            return $"Le loyer de {month:00}/{year} est déjà intégralement payé. Un second paiement pour ce mois n'est pas autorisé.";

        var remaining = RentPaymentRules.RemainingDue(amountDue, payment?.AmountPaid ?? 0, payment?.PaymentStatus);
        if (amountPaid > remaining)
            return remaining <= 0
                ? $"Le loyer de {month:00}/{year} est déjà soldé."
                : $"Montant trop élevé : il reste {Fc(remaining)} à encaisser pour {month:00}/{year}.";

        if (payment is null)
        {
            payment = new RentPayment
            {
                LeaseContractId = contractId,
                Year = year,
                Month = month,
                AmountDue = amountDue,
                AmountPaid = 0,
                DueDate = new DateTime(year, month, Math.Min(28, DateTime.DaysInMonth(year, month))),
                IsSynced = false
            };
            _db.RentPayments.Add(payment);
        }

        payment.AmountPaid += amountPaid;
        payment.PaidDate = paymentDate.Date;
        payment.PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? "Virement bancaire" : paymentMethod.Trim();
        payment.TransactionReference = string.IsNullOrWhiteSpace(transactionReference) ? null : transactionReference.Trim();
        payment.IsLate = payment.AmountPaid < payment.AmountDue && payment.DueDate.Date < paymentDate.Date;
        payment.PaymentStatus = !string.IsNullOrWhiteSpace(paymentStatusOverride)
            ? paymentStatusOverride.Trim()
            : payment.AmountPaid >= payment.AmountDue
                ? LocationConstants.PaymentStatus.Paid
                : payment.AmountPaid > 0
                    ? LocationConstants.PaymentStatus.Partial
                    : payment.IsLate
                        ? LocationConstants.PaymentStatus.Late
                        : LocationConstants.PaymentStatus.Pending;
        payment.IsSynced = false;

        await _financeLedger.RecordRentCollectionAsync(payment, contract, amountPaid, cancellationToken);

        await LogTenantActivityAsync(contract.TenantId, "Paiement", "Loyer encaissé",
            $"{Fc(amountPaid)} — {payment.Month:00}/{payment.Year}", cancellationToken);
        var saveError = await _db.SaveChangesWithMessageAsync(cancellationToken);
        if (!string.IsNullOrEmpty(saveError))
            return saveError;

        if (payment.AmountPaid > 0)
            await GenerateReceiptForPaymentAsync(payment, contract, amountPaid, cancellationToken);

        return string.Empty;
    }

    public async Task<string?> GetReceiptPdfPathForPeriodAsync(
        Guid contractId,
        int year,
        int month,
        CancellationToken cancellationToken = default) =>
        await _db.RentPayments
            .Where(p => p.LeaseContractId == contractId && p.Year == year && p.Month == month)
            .Select(p => p.ReceiptPdfPath)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<RentPeriodPaymentInfo> GetRentPeriodPaymentInfoAsync(
        Guid contractId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var contract = await _db.LeaseContracts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == contractId, cancellationToken);
        if (contract is null)
            return new RentPeriodPaymentInfo { Summary = "Contrat introuvable." };

        var payment = await _db.RentPayments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LeaseContractId == contractId && p.Year == year && p.Month == month,
                cancellationToken);

        var amountDue = payment?.AmountDue ?? contract.MonthlyRent;
        var amountPaid = payment?.AmountPaid ?? 0;
        var status = payment?.PaymentStatus ?? LocationConstants.PaymentStatus.Pending;
        var remaining = RentPaymentRules.RemainingDue(amountDue, amountPaid, status);
        var fullyPaid = RentPaymentRules.IsFullyPaid(amountDue, amountPaid, status);

        var summary = fullyPaid
            ? $"Mois {month:00}/{year} : entièrement payé ({Fc(amountPaid)})."
            : remaining < amountDue
                ? $"Mois {month:00}/{year} : {Fc(amountPaid)} / {Fc(amountDue)} — reste {Fc(remaining)}."
                : $"Mois {month:00}/{year} : {Fc(amountDue)} à encaisser.";

        return new RentPeriodPaymentInfo
        {
            Exists = payment is not null,
            IsFullyPaid = fullyPaid,
            AmountDue = amountDue,
            AmountPaid = amountPaid,
            RemainingDue = remaining,
            PaymentStatus = status,
            Summary = summary
        };
    }

    /// <summary>
    /// Corrige les double-paiements : plafonne AmountPaid et annule le surplus en trésorerie.
    /// </summary>
    public async Task<int> CancelOverpaidRentPaymentsAsync(CancellationToken cancellationToken = default)
    {
        var overpaid = await _db.RentPayments
            .Include(p => p.LeaseContract)
            .ThenInclude(c => c!.Premise)
            .Where(p => p.AmountPaid > p.AmountDue && p.AmountDue > 0)
            .ToListAsync(cancellationToken);

        if (overpaid.Count == 0)
            return 0;

        foreach (var payment in overpaid)
        {
            if (payment.LeaseContract is null)
                continue;

            var excess = payment.AmountPaid - payment.AmountDue;
            payment.AmountPaid = payment.AmountDue;
            payment.PaymentStatus = LocationConstants.PaymentStatus.Paid;
            payment.IsLate = false;
            payment.MarkUpdated();

            await _financeLedger.AlignRentLedgerWithPaymentAsync(payment, payment.LeaseContract, cancellationToken);

            await LogTenantActivityAsync(
                payment.LeaseContract.TenantId,
                "Paiement",
                "Double paiement annulé",
                $"Surplus de {Fc(excess)} annulé pour {payment.Month:00}/{payment.Year} — montant ramené à {Fc(payment.AmountDue)}.",
                cancellationToken);
        }

        await _db.SaveChangesWithMessageAsync(cancellationToken);
        return overpaid.Count;
    }

    /// <summary>Prochain numéro CTR-XXX unique (y compris contrats archivés / supprimés logiquement).</summary>
    public async Task<string> GenerateNextContractNumberAsync(CancellationToken cancellationToken = default)
    {
        var numbers = await _db.LeaseContracts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(c => c.ContractNumber)
            .ToListAsync(cancellationToken);

        var max = 0;
        foreach (var num in numbers)
        {
            if (num.StartsWith("CTR-", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(num.AsSpan(4), out var n) && n > max)
                max = n;
        }

        for (var offset = 1; offset < 10_000; offset++)
        {
            var candidate = $"CTR-{(max + offset):D3}";
            var exists = await _db.LeaseContracts
                .IgnoreQueryFilters()
                .AnyAsync(c => c.ContractNumber == candidate, cancellationToken);
            if (!exists)
                return candidate;
        }

        return $"CTR-{DateTime.UtcNow:yyyyMMddHHmmss}";
    }

    public async Task<string> GenerateNextCodeAsync(CancellationToken cancellationToken = default)
    {
        var codes = await _db.Premises.Select(p => p.Code).ToListAsync(cancellationToken);
        var max = 0;
        foreach (var code in codes)
        {
            if (code.StartsWith("LOC-", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(code.AsSpan(4), out var n) && n > max)
                max = n;
        }

        return $"LOC-{(max + 1):D3}";
    }

    private static LocationsPremiseItem MapPremise(
        Premise p,
        LeaseContract? contract,
        string defaultBuilding,
        DateTime today)
    {
        var occupied = contract is not null;
        var endDate = contract?.EndDate;
        var warning = endDate.HasValue && endDate.Value.Date <= today.AddDays(30);

        return new LocationsPremiseItem
        {
            Id = p.Id,
            Code = p.Code,
            Name = p.Name,
            Building = string.IsNullOrWhiteSpace(p.Building) ? defaultBuilding : p.Building,
            Floor = string.IsNullOrWhiteSpace(p.Floor) ? "—" : p.Floor,
            PremiseType = string.IsNullOrWhiteSpace(p.PremiseType) ? LocationConstants.DefaultPremiseType : p.PremiseType,
            TenantId = contract?.TenantId ?? Guid.Empty,
            TenantName = contract?.Tenant?.Name ?? "—",
            TenantPhone = contract?.Tenant?.Phone ?? "—",
            TenantEmail = contract?.Tenant?.Email ?? "—",
            TenantCompany = contract?.Tenant?.Company ?? "—",
            MonthlyRent = contract?.MonthlyRent ?? p.MonthlyRent,
            RentDisplay = Fc(contract?.MonthlyRent ?? p.MonthlyRent),
            StatusLabel = occupied ? "Occupé" : "Disponible",
            StatusBadgeBackground = occupied ? "#DCFCE7" : "#DBEAFE",
            StatusBadgeForeground = occupied ? "#166534" : "#1D4ED8",
            EndContractDisplay = endDate?.ToString("dd/MM/yyyy") ?? "—",
            EndContractIsWarning = warning,
            AreaDisplay = p.AreaSqM > 0 ? $"{p.AreaSqM:N0} m²" : "—",
            Description = string.IsNullOrWhiteSpace(p.Description) ? "—" : p.Description,
            ContractNumber = contract?.ContractNumber ?? "—",
            ContractStart = contract?.StartDate,
            ContractEnd = contract?.EndDate,
            Deposit = contract?.Deposit ?? 0
        };
    }

    private static List<LocationsContractItem> MapContracts(IEnumerable<LeaseContract> contracts) =>
        contracts.OrderByDescending(c => c.StartDate).Select(c => new LocationsContractItem
        {
            Id = c.Id,
            TenantId = c.TenantId,
            PremiseId = c.PremiseId,
            ContractNumber = c.ContractNumber,
            ContractType = string.IsNullOrWhiteSpace(c.ContractType) ? "—" : c.ContractType,
            PremiseLabel = $"{c.Premise?.Code} — {c.Premise?.Name}",
            TenantName = c.Tenant?.Name ?? "—",
            StartDisplay = c.StartDate.ToString("dd/MM/yyyy"),
            EndDisplay = c.EndDate.ToString("dd/MM/yyyy"),
            RentDisplay = Fc(c.MonthlyRent),
            MonthlyRent = c.MonthlyRent,
            Deposit = c.Deposit,
            StatusLabel = LocationContractStatusHelper.ToLabel(c.Status),
            StatusBadgeBackground = c.Status switch
            {
                LeaseStatus.Actif => "#16A34A",
                LeaseStatus.EnAttenteValidation => "#F59E0B",
                LeaseStatus.Resilie => "#EF4444",
                LeaseStatus.Annule => "#DC2626",
                LeaseStatus.Expire => "#6B7280",
                _ => "#475569"
            },
            StatusBadgeForeground = "#FFFFFF"
        }).ToList();

    private static List<LocationsPaymentItem> MapPayments(IEnumerable<RentPayment> payments, DateTime today) =>
        payments.OrderByDescending(p => p.DueDate).Select(p =>
        {
            var paid = p.AmountPaid >= p.AmountDue;
            var late = p.IsLate || (!paid && p.DueDate.Date < today);
            return new LocationsPaymentItem
            {
                Id = p.Id,
                ContractId = p.LeaseContractId,
                PremiseLabel = $"{p.LeaseContract.Premise?.Code} — {p.LeaseContract.Premise?.Name}",
                TenantName = p.LeaseContract.Tenant?.Name ?? "—",
                PeriodDisplay = $"{p.Month:00}/{p.Year}",
                AmountDisplay = Fc(p.AmountDue),
                AmountPaidDisplay = Fc(p.AmountPaid),
                DueDisplay = p.DueDate.ToString("dd/MM/yyyy"),
                PaidDisplay = p.PaidDate?.ToString("dd/MM/yyyy") ?? "—",
                LateLabel = late ? "Oui" : "Non",
                PaymentStatus = string.IsNullOrWhiteSpace(p.PaymentStatus)
                    ? (paid ? LocationConstants.PaymentStatus.Paid : late ? LocationConstants.PaymentStatus.Late : LocationConstants.PaymentStatus.Pending)
                    : p.PaymentStatus,
                StatusLabel = paid ? "Payé" : late ? "En retard" : "En attente",
                StatusColor = paid ? "#FFFFFF" : late ? "#FFFFFF" : "#111827",
                StatusBadgeBackground = paid ? "#DCFCE7" : late ? "#FEE2E2" : "#FEF3C7",
                StatusBadgeForeground = paid ? "#166534" : late ? "#DC2626" : "#B45309"
            };
        }).ToList();

    private static List<LocationsBuildingItem> MapBuildings(IEnumerable<Building> buildings) =>
        buildings.Select(b => new LocationsBuildingItem
        {
            Id = b.Id,
            Code = b.Code,
            Name = b.Name,
            Address = string.IsNullOrWhiteSpace(b.Address) ? "—" : b.Address,
            BuildingType = b.BuildingType,
            FloorCount = b.FloorCount,
            PremiseCount = b.PremiseCount,
            Status = b.Status
        }).ToList();

    private static List<LocationsGuaranteeItem> MapGuarantees(IEnumerable<LeaseGuarantee> items) =>
        items.Select(g => new LocationsGuaranteeItem
        {
            Id = g.Id,
            ContractId = g.LeaseContractId,
            ContractNumber = g.LeaseContract?.ContractNumber ?? "—",
            TenantName = g.LeaseContract?.Tenant?.Name ?? "—",
            TypeLabel = "Caution",
            Amount = g.Amount,
            AmountDisplay = Fc(g.Amount),
            RefundedDisplay = Fc(g.AmountRefunded),
            DateDisplay = g.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy"),
            Status = g.Status,
            StatusBadgeBackground = GuaranteeBadgeBg(g.Status),
            StatusBadgeForeground = GuaranteeBadgeFg(g.Status)
        }).ToList();

    private static string GuaranteeBadgeBg(string status) => status switch
    {
        LocationConstants.GuaranteeStatus.Refunded => "#E2E8F0",
        LocationConstants.GuaranteeStatus.Partial => "#FEF3C7",
        LocationConstants.GuaranteeStatus.Suspended => "#FEE2E2",
        _ => "#DCFCE7"
    };

    private static string GuaranteeBadgeFg(string status) => status switch
    {
        LocationConstants.GuaranteeStatus.Refunded => "#475569",
        LocationConstants.GuaranteeStatus.Partial => "#B45309",
        LocationConstants.GuaranteeStatus.Suspended => "#DC2626",
        _ => "#166534"
    };

    private static List<LocationsActivityItem> MapActivities(IEnumerable<TenantActivity> items) =>
        items.Select(a => new LocationsActivityItem
        {
            DateDisplay = a.OccurredAt.ToString("dd/MM/yyyy HH:mm"),
            Category = a.Category,
            Title = a.Title,
            Description = a.Description,
            TenantName = a.Tenant?.Name ?? "—"
        }).ToList();

    private static List<LocationsTenantItem> MapTenants(IEnumerable<Tenant> tenants, IEnumerable<LeaseContract> contracts)
    {
        var byTenant = contracts.GroupBy(c => c.TenantId).ToDictionary(g => g.Key, g => g.ToList());
        return tenants.Select(t => new LocationsTenantItem
        {
            Id = t.Id,
            DossierNumber = string.IsNullOrWhiteSpace(t.DossierNumber) ? "—" : t.DossierNumber,
            RentalStatus = t.RentalStatus,
            Name = t.Name,
            Phone = t.Phone,
            Email = t.Email,
            Company = t.Company ?? "—",
            ActiveContracts = byTenant.TryGetValue(t.Id, out var list)
                ? list.Count(c => c.Status == LeaseStatus.Actif)
                : 0
        }).OrderBy(t => t.Name).ToList();
    }

    public async Task<IReadOnlyList<LocationsDirectoryRow>> GetDirectoryRowsAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var tenants = await _db.Tenants.OrderBy(t => t.Name).ToListAsync(cancellationToken);
        var contracts = await _db.LeaseContracts
            .Include(c => c.Premise)
            .Include(c => c.Tenant)
            .ToListAsync(cancellationToken);
        var payments = await _db.RentPayments
            .Where(p => p.Year == today.Year && p.Month == today.Month)
            .ToListAsync(cancellationToken);

        var occupyingPremiseIds = contracts
            .Where(c => LeaseOccupancyRules.OccupiesPremise(c.Status))
            .Select(c => c.PremiseId)
            .ToHashSet();

        var rows = new List<LocationsDirectoryRow>();
        foreach (var tenant in tenants)
        {
            var contract = contracts
                .Where(c => c.TenantId == tenant.Id)
                .OrderByDescending(c => c.Status == LeaseStatus.Actif)
                .ThenByDescending(c => c.StartDate)
                .FirstOrDefault();

            var late = contract is not null && payments.Any(p =>
                p.LeaseContractId == contract.Id &&
                (p.IsLate || (p.AmountPaid < p.AmountDue && p.DueDate.Date < today)));

            var premise = contract?.Premise;
            var availability = premise is null
                ? "—"
                : occupyingPremiseIds.Contains(premise.Id) ? "Occupé" : "Disponible";

            rows.Add(new LocationsDirectoryRow
            {
                TenantId = tenant.Id,
                ContractId = contract?.Id,
                TenantName = tenant.Name,
                RentDisplay = contract is not null ? Fc(contract.MonthlyRent) : "—",
                PremiseLabel = premise is not null ? $"{premise.Code} — {premise.Name}" : "—",
                ContractTypeOrNumber = contract is not null
                    ? $"{contract.ContractType} / {contract.ContractNumber}"
                    : "—",
                AvailabilityLabel = availability,
                LatePaymentLabel = contract is null ? "—" : late ? "Oui" : "Non",
                TerminationLabel = contract?.Status == LeaseStatus.Resilie ? "Oui" : "Non",
                StartDisplay = contract?.StartDate.ToString("dd/MM/yyyy") ?? "—",
                EndDisplay = contract?.EndDate.ToString("dd/MM/yyyy") ?? "—",
                StatusLabel = contract is null ? "Sans contrat" : LocationContractStatusHelper.ToLabel(contract.Status),
                StatusBadgeBackground = contract is null ? "#E2E8F0" : BadgeBg(contract.Status),
                StatusBadgeForeground = contract is null ? "#334155" : BadgeFg(contract.Status)
            });
        }

        return rows;
    }

    private static string BadgeBg(LeaseStatus status) => status switch
    {
        LeaseStatus.Actif => "#DCFCE7",
        LeaseStatus.EnAttenteValidation => "#FEF3C7",
        LeaseStatus.Resilie => "#FEE2E2",
        _ => "#E2E8F0"
    };

    private static string BadgeFg(LeaseStatus status) => status switch
    {
        LeaseStatus.Actif => "#166534",
        LeaseStatus.EnAttenteValidation => "#B45309",
        LeaseStatus.Resilie => "#DC2626",
        _ => "#334155"
    };

    private static string Fc(decimal amount) => MoneyFormatter.Format(amount);

    /// <summary>
    /// Aligne IsOccupied / OccupancyStatus sur les contrats actifs ou en attente de validation.
    /// </summary>
    public async Task ReconcilePremiseOccupancyAsync(CancellationToken cancellationToken = default)
    {
        var occupyingPremiseIds = await _db.LeaseContracts
            .Where(c => LeaseOccupancyRules.OccupyingStatuses.Contains(c.Status))
            .Select(c => c.PremiseId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var occupyingSet = occupyingPremiseIds.ToHashSet();
        var premises = await _db.Premises.ToListAsync(cancellationToken);
        var changed = false;

        foreach (var premise in premises)
        {
            var shouldOccupy = occupyingSet.Contains(premise.Id);
            var targetStatus = shouldOccupy
                ? LocationConstants.PremiseOccupancyStatus.Occupied
                : LocationConstants.PremiseOccupancyStatus.Available;

            if (premise.IsOccupied == shouldOccupy && premise.OccupancyStatus == targetStatus)
                continue;

            premise.IsOccupied = shouldOccupy;
            premise.OccupancyStatus = targetStatus;
            premise.MarkUpdated();
            changed = true;
        }

        if (changed)
            await _db.SaveChangesAsync(cancellationToken);
    }
}

using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;
using SmartBuilding.Infrastructure.Persistence;

namespace SmartBuilding.Desktop.WPF.Services;

public class TenantDetailService
{
    private readonly SmartBuildingDbContext _db;

    public TenantDetailService(SmartBuildingDbContext db) => _db = db;

    public async Task<TenantDetailData?> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants
            .Include(t => t.LeaseContracts)
            .ThenInclude(c => c.Premise)
            .Include(t => t.LeaseContracts)
            .ThenInclude(c => c.RentPayments)
            .Include(t => t.LeaseContracts)
            .ThenInclude(c => c.Guarantees)
            .Include(t => t.Activities)
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant is null)
            return null;

        var today = DateTime.Today;
        var contracts = tenant.LeaseContracts.OrderByDescending(c => c.StartDate).ToList();
        var active = contracts.Where(c => c.Status == LeaseStatus.Actif).ToList();

        var payments = contracts
            .SelectMany(c => c.RentPayments.Select(p => new { Contract = c, Payment = p }))
            .OrderByDescending(x => x.Payment.Year)
            .ThenByDescending(x => x.Payment.Month)
            .Take(24)
            .ToList();

        var lateCount = payments.Count(x =>
            x.Payment.IsLate || x.Payment.AmountPaid < x.Payment.AmountDue && x.Payment.DueDate.Date < today);

        var activities = tenant.Activities
            .OrderByDescending(a => a.OccurredAt)
            .Select(MapActivity)
            .ToList();

        if (activities.Count == 0)
            activities = BuildActivitiesFromContracts(contracts);

        var age = tenant.DateOfBirth.HasValue
            ? today.Year - tenant.DateOfBirth.Value.Year -
              (today.DayOfYear < tenant.DateOfBirth.Value.DayOfYear ? 1 : 0)
            : (int?)null;

        return new TenantDetailData
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Initials = GetInitials(tenant.Name),
            Email = string.IsNullOrWhiteSpace(tenant.Email) ? "—" : tenant.Email,
            Phone = string.IsNullOrWhiteSpace(tenant.Phone) ? "—" : tenant.Phone,
            Company = string.IsNullOrWhiteSpace(tenant.Company) ? "—" : tenant.Company!,
            Address = string.IsNullOrWhiteSpace(tenant.Address) ? "—" : tenant.Address!,
            Category = string.IsNullOrWhiteSpace(tenant.TenantCategory) ? "Particulier" : tenant.TenantCategory,
            DossierNumber = string.IsNullOrWhiteSpace(tenant.DossierNumber) ? "—" : tenant.DossierNumber,
            RentalStatus = tenant.RentalStatus,
            Nationality = string.IsNullOrWhiteSpace(tenant.Nationality) ? "—" : tenant.Nationality!,
            BusinessActivity = string.IsNullOrWhiteSpace(tenant.BusinessActivity) ? "—" : tenant.BusinessActivity!,
            PersonCountDisplay = tenant.PersonCount > 0 ? $"{tenant.PersonCount} personne(s)" : "—",
            NationalId = string.IsNullOrWhiteSpace(tenant.NationalId) ? "—" : tenant.NationalId!,
            DateOfBirthDisplay = tenant.DateOfBirth?.ToString("dd/MM/yyyy") ?? "—",
            AgeDisplay = age.HasValue ? $"{age} ans" : "—",
            Gender = string.IsNullOrWhiteSpace(tenant.Gender) ? "—" : tenant.Gender,
            MaritalStatus = string.IsNullOrWhiteSpace(tenant.MaritalStatus) ? "—" : tenant.MaritalStatus,
            SpouseName = string.IsNullOrWhiteSpace(tenant.SpouseName) ? "—" : tenant.SpouseName!,
            ChildrenCount = tenant.ChildrenCount,
            ChildrenDisplay = tenant.ChildrenCount > 0 ? $"{tenant.ChildrenCount} enfant(s)" : "Aucun enfant déclaré",
            Profession = string.IsNullOrWhiteSpace(tenant.Profession) ? "—" : tenant.Profession!,
            EmergencyContactName = string.IsNullOrWhiteSpace(tenant.EmergencyContactName) ? "—" : tenant.EmergencyContactName!,
            EmergencyContactPhone = string.IsNullOrWhiteSpace(tenant.EmergencyContactPhone) ? "—" : tenant.EmergencyContactPhone!,
            Notes = string.IsNullOrWhiteSpace(tenant.Notes) ? "—" : tenant.Notes!,
            SummaryLine = BuildSummary(tenant, active.Count, lateCount),
            ActiveContracts = active.Count,
            TotalContracts = contracts.Count,
            TotalRentMonthly = active.Sum(c => c.MonthlyRent),
            TotalRentDisplay = $"{MoneyFormatter.Format(active.Sum(c => c.MonthlyRent))} / mois",
            LatePaymentsCount = lateCount,
            Contracts = contracts.Select(c => new TenantContractRow
            {
                ContractNumber = c.ContractNumber,
                PremiseLabel = $"{c.Premise?.Code} — {c.Premise?.Name}",
                PeriodDisplay = $"{c.StartDate:dd/MM/yyyy} → {c.EndDate:dd/MM/yyyy}",
                RentDisplay = MoneyFormatter.Format(c.MonthlyRent),
                StatusLabel = LocationContractStatusHelper.ToLabel(c.Status),
                StatusColor = c.Status == LeaseStatus.Actif ? "#22C55E" : c.Status == LeaseStatus.Resilie ? "#94A3B8" : "#F59E0B"
            }).ToList(),
            Payments = payments.Select(x =>
            {
                var paid = x.Payment.AmountPaid >= x.Payment.AmountDue;
                var late = x.Payment.IsLate || (!paid && x.Payment.DueDate.Date < today);
                return new TenantPaymentRow
                {
                    PeriodDisplay = $"{x.Payment.Month:00}/{x.Payment.Year}",
                    PremiseLabel = $"{x.Contract.Premise?.Code} — {x.Contract.Premise?.Name}",
                    AmountDisplay = $"{MoneyFormatter.Format(x.Payment.AmountDue)} (payé {MoneyFormatter.Format(x.Payment.AmountPaid)})",
                    StatusLabel = paid ? "Payé" : late ? "En retard" : "En attente",
                    StatusColor = paid ? "#22C55E" : late ? "#EF4444" : "#F59E0B"
                };
            }).ToList(),
            Activities = activities,
            Guarantees = contracts
                .SelectMany(c => c.Guarantees.Select(g => new TenantGuaranteeRow
                {
                    ContractNumber = c.ContractNumber,
                    AmountDisplay = MoneyFormatter.Format(g.Amount),
                    RefundedDisplay = MoneyFormatter.Format(g.AmountRefunded),
                    Status = g.Status
                }))
                .ToList()
        };
    }

    public async Task<Guid?> ResolveTenantIdFromPremiseAsync(Guid premiseId, CancellationToken cancellationToken = default)
    {
        var contract = await _db.LeaseContracts
            .Where(c => c.PremiseId == premiseId && c.Status == LeaseStatus.Actif)
            .OrderByDescending(c => c.EndDate)
            .Select(c => c.TenantId)
            .FirstOrDefaultAsync(cancellationToken);

        return contract == Guid.Empty ? null : contract;
    }

    private static string BuildSummary(Domain.Entities.Location.Tenant tenant, int activeContracts, int latePayments)
    {
        var parts = new List<string> { tenant.TenantCategory };
        if (!string.IsNullOrWhiteSpace(tenant.Profession))
            parts.Add(tenant.Profession);
        if (activeContracts > 0)
            parts.Add($"{activeContracts} contrat(s) actif(s)");
        if (latePayments > 0)
            parts.Add($"{latePayments} paiement(s) en retard");
        return string.Join(" · ", parts);
    }

    private static List<TenantActivityRow> BuildActivitiesFromContracts(
        IEnumerable<Domain.Entities.Location.LeaseContract> contracts)
    {
        var rows = new List<TenantActivityRow>();
        foreach (var c in contracts.Take(8))
        {
            rows.Add(new TenantActivityRow
            {
                OccurredAt = c.StartDate,
                DateDisplay = c.StartDate.ToString("dd/MM/yyyy HH:mm"),
                Category = "Contrat",
                Title = $"Contrat {c.ContractNumber}",
                Description = $"Location {c.Premise?.Name} — loyer {MoneyFormatter.Format(c.MonthlyRent)}",
                IconKind = "FileDocumentOutline",
                Color = "#2563EB"
            });
        }

        return rows.OrderByDescending(r => r.OccurredAt).ToList();
    }

    private static TenantActivityRow MapActivity(Domain.Entities.Location.TenantActivity a) => new()
    {
        OccurredAt = a.OccurredAt,
        DateDisplay = a.OccurredAt.ToString("dd/MM/yyyy HH:mm"),
        Category = a.Category,
        Title = a.Title,
        Description = a.Description,
        IconKind = a.Category switch
        {
            "Paiement" => "CashRegister",
            "Contrat" => "FileDocumentOutline",
            "Famille" => "AccountHeart",
            _ => "History"
        },
        Color = a.Category switch
        {
            "Paiement" => "#2D6A4F",
            "Incident" => "#EF4444",
            _ => "#64748B"
        }
    };

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0][0]}{parts[^1][0]}".ToUpper() : name.Length >= 2 ? name[..2].ToUpper() : "L";
    }
}

namespace SmartBuilding.Desktop.WPF.Models;

public class TenantDetailData
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Initials { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string DossierNumber { get; init; } = "—";
    public string RentalStatus { get; init; } = "—";
    public string Nationality { get; init; } = "—";
    public string BusinessActivity { get; init; } = "—";
    public string PersonCountDisplay { get; init; } = "—";
    public string NationalId { get; init; } = string.Empty;
    public string DateOfBirthDisplay { get; init; } = "—";
    public string AgeDisplay { get; init; } = "—";
    public string Gender { get; init; } = "—";
    public string MaritalStatus { get; init; } = "—";
    public string SpouseName { get; init; } = "—";
    public int ChildrenCount { get; init; }
    public string ChildrenDisplay { get; init; } = "—";
    public string Profession { get; init; } = "—";
    public string EmergencyContactName { get; init; } = "—";
    public string EmergencyContactPhone { get; init; } = "—";
    public string Notes { get; init; } = "—";
    public string SummaryLine { get; init; } = string.Empty;
    public int ActiveContracts { get; init; }
    public int TotalContracts { get; init; }
    public decimal TotalRentMonthly { get; init; }
    public string TotalRentDisplay { get; init; } = string.Empty;
    public int LatePaymentsCount { get; init; }
    public IReadOnlyList<TenantContractRow> Contracts { get; init; } = [];
    public IReadOnlyList<TenantPaymentRow> Payments { get; init; } = [];
    public IReadOnlyList<TenantActivityRow> Activities { get; init; } = [];
    public IReadOnlyList<TenantGuaranteeRow> Guarantees { get; init; } = [];
}

public class TenantGuaranteeRow
{
    public string ContractNumber { get; init; } = string.Empty;
    public string AmountDisplay { get; init; } = string.Empty;
    public string RefundedDisplay { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

public class TenantContractRow
{
    public string ContractNumber { get; init; } = string.Empty;
    public string PremiseLabel { get; init; } = string.Empty;
    public string PeriodDisplay { get; init; } = string.Empty;
    public string RentDisplay { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string StatusColor { get; init; } = "#22C55E";
}

public class TenantPaymentRow
{
    public string PeriodDisplay { get; init; } = string.Empty;
    public string PremiseLabel { get; init; } = string.Empty;
    public string AmountDisplay { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string StatusColor { get; init; } = "#22C55E";
}

public class TenantActivityRow
{
    public DateTime OccurredAt { get; init; }
    public string DateDisplay { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string IconKind { get; init; } = "History";
    public string Color { get; init; } = "#2D6A4F";
}

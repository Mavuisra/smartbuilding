using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartBuilding.Desktop.WPF.Models;

public partial class LocationsPremiseItem : ObservableObject
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Building { get; init; } = string.Empty;
    public string Floor { get; init; } = string.Empty;
    public string PremiseType { get; init; } = string.Empty;
    public Guid TenantId { get; init; }
    public string TenantName { get; init; } = "—";
    public string TenantPhone { get; init; } = "—";
    public string RentDisplay { get; init; } = string.Empty;
    public decimal MonthlyRent { get; init; }
    public string StatusLabel { get; init; } = "Disponible";
    public string StatusBadgeBackground { get; init; } = "#DBEAFE";
    public string StatusBadgeForeground { get; init; } = "#1D4ED8";
    public string EndContractDisplay { get; init; } = "—";
    public bool EndContractIsWarning { get; init; }
    public string AreaDisplay { get; init; } = string.Empty;
    public string Description { get; init; } = "—";
    public string ContractNumber { get; init; } = "—";
    public string TenantEmail { get; init; } = "—";
    public string TenantCompany { get; init; } = "—";
    public DateTime? ContractStart { get; init; }
    public DateTime? ContractEnd { get; init; }
    public decimal Deposit { get; init; }

    [ObservableProperty] private bool _isSelected;
}

public class LocationsContractItem
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid PremiseId { get; init; }
    public string ContractNumber { get; init; } = string.Empty;
    public string ContractType { get; init; } = string.Empty;
    public string PremiseLabel { get; init; } = string.Empty;
    public string TenantName { get; init; } = string.Empty;
    public string StartDisplay { get; init; } = string.Empty;
    public string EndDisplay { get; init; } = string.Empty;
    public string RentDisplay { get; init; } = string.Empty;
    public decimal MonthlyRent { get; init; }
    public decimal Deposit { get; init; }
    public string StatusLabel { get; init; } = string.Empty;
    public string StatusBadgeBackground { get; init; } = "#E2E8F0";
    public string StatusBadgeForeground { get; init; } = "#334155";
}

public class LocationsBuildingItem
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string BuildingType { get; init; } = string.Empty;
    public int FloorCount { get; init; }
    public int PremiseCount { get; init; }
    public string Status { get; init; } = "Actif";
}

public class LocationsGuaranteeItem
{
    public Guid Id { get; init; }
    public Guid ContractId { get; init; }
    public string ContractNumber { get; init; } = string.Empty;
    public string TenantName { get; init; } = string.Empty;
    public string TypeLabel { get; init; } = "Caution";
    public string AmountDisplay { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string RefundedDisplay { get; init; } = string.Empty;
    public string DateDisplay { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string StatusBadgeBackground { get; init; } = "#DCFCE7";
    public string StatusBadgeForeground { get; init; } = "#166534";
}

public class LocationsActivityItem
{
    public string DateDisplay { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string TenantName { get; init; } = string.Empty;
}

public class RentPeriodPaymentInfo
{
    public bool Exists { get; init; }
    public bool IsFullyPaid { get; init; }
    public decimal AmountDue { get; init; }
    public decimal AmountPaid { get; init; }
    public decimal RemainingDue { get; init; }
    public string PaymentStatus { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
}

public class LocationsPaymentItem
{
    public Guid Id { get; init; }
    public Guid ContractId { get; init; }
    public string PremiseLabel { get; init; } = string.Empty;
    public string TenantName { get; init; } = string.Empty;
    public string PeriodDisplay { get; init; } = string.Empty;
    public string AmountDisplay { get; init; } = string.Empty;
    public string AmountPaidDisplay { get; init; } = string.Empty;
    public string DueDisplay { get; init; } = string.Empty;
    public string PaidDisplay { get; init; } = string.Empty;
    public string LateLabel { get; init; } = "Non";
    public string PaymentStatus { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string StatusColor { get; init; } = "#22C55E";
    public string StatusBadgeBackground { get; init; } = "#DCFCE7";
    public string StatusBadgeForeground { get; init; } = "#166534";
}

public class LocationsTenantItem
{
    public Guid Id { get; init; }
    public string DossierNumber { get; init; } = string.Empty;
    public string RentalStatus { get; init; } = "Actif";
    public string Name { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public int ActiveContracts { get; init; }
}

public class LocationsPickItem
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid PremiseId { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Building { get; init; } = string.Empty;
    public string Floor { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string AreaDisplay { get; init; } = string.Empty;
    public decimal MonthlyRent { get; init; }
    public string RentDisplay { get; init; } = "0 FC";
    public string PhotoPath { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Nationality { get; init; } = string.Empty;
    public string Gender { get; init; } = string.Empty;
    public string MaritalStatus { get; init; } = string.Empty;
    public string DateOfBirthDisplay { get; init; } = string.Empty;
    public string PersonCountDisplay { get; init; } = string.Empty;
    public string Profession { get; init; } = string.Empty;
    public string BusinessActivity { get; init; } = string.Empty;
    public string EmergencyContactDisplay { get; init; } = string.Empty;
    public string ProfilePhotoPath { get; init; } = string.Empty;
}

public class LocationsTypeSlice
{
    public string Type { get; init; } = string.Empty;
    public int Count { get; init; }
}

public class LocationsDirectoryRow
{
    public Guid TenantId { get; init; }
    public Guid? ContractId { get; init; }
    public string TenantName { get; init; } = string.Empty;
    public string RentDisplay { get; init; } = "—";
    public string PremiseLabel { get; init; } = "—";
    public string ContractTypeOrNumber { get; init; } = "—";
    public string AvailabilityLabel { get; init; } = "—";
    public string LatePaymentLabel { get; init; } = "—";
    public string TerminationLabel { get; init; } = "—";
    public string StartDisplay { get; init; } = "—";
    public string EndDisplay { get; init; } = "—";
    public string StatusLabel { get; init; } = "—";
    public string StatusBadgeBackground { get; init; } = "#E2E8F0";
    public string StatusBadgeForeground { get; init; } = "#334155";
}

public class LocationWizardStepItem
{
    public int Index { get; init; }
    public string Number { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
}

public sealed record CreateContractResult(string Error, Guid? ContractId = null, string? SummaryPdfPath = null);

public class PremiseOccupancyStats
{
    public int TotalPremises { get; init; }
    public int AvailablePremises { get; init; }
    public int OccupiedPremises { get; init; }
    public int PendingPremises { get; init; }
    public double OccupancyRate { get; init; }
    public string OccupancyRateDisplay => $"{OccupancyRate:F1} %";
}

public class LocationsPageData
{
    public int TotalPremises { get; init; }
    public int OccupiedPremises { get; init; }
    public int AvailablePremises { get; init; }
    public string OccupiedPercent { get; init; } = "0%";
    public string AvailablePercent { get; init; } = "0%";
    public decimal MonthlyRentCollected { get; init; }
    public decimal RentCollectedTotal { get; init; }
    public decimal AvailableBalance { get; init; }
    public decimal TotalExpenses { get; init; }
    public int LatePayments { get; init; }
    public string LatePercent { get; init; } = "0%";
    public int ActiveContracts { get; init; }
    public int ActiveGuarantees { get; init; }
    public double OccupancyRate { get; init; }
    public IReadOnlyList<LocationsPremiseItem> Premises { get; init; } = [];
    public IReadOnlyList<LocationsContractItem> Contracts { get; init; } = [];
    public IReadOnlyList<LocationsPaymentItem> Payments { get; init; } = [];
    public IReadOnlyList<LocationsTenantItem> Tenants { get; init; } = [];
    public IReadOnlyList<LocationsBuildingItem> BuildingRows { get; init; } = [];
    public IReadOnlyList<LocationsGuaranteeItem> Guarantees { get; init; } = [];
    public IReadOnlyList<LocationsActivityItem> RecentActivities { get; init; } = [];
    public IReadOnlyList<LocationsPaymentItem> LatePaymentRows { get; init; } = [];
    public IReadOnlyList<LocationsContractItem> TerminatedContracts { get; init; } = [];
    public IReadOnlyList<LocationsTypeSlice> TypeDistribution { get; init; } = [];
    public IReadOnlyList<decimal> RentTrend { get; init; } = [];
    public IReadOnlyList<string> RentTrendLabels { get; init; } = [];
    public decimal RentOccupied { get; init; }
    public decimal RentLate { get; init; }
    public decimal RentAvailable { get; init; }
}

using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartBuilding.Desktop.WPF.Models;

public class DocumentsPageData
{
    public int TotalCount { get; init; }
    public int RecentCount { get; init; }
    public int ActiveContractsCount { get; init; }
    public int SharedCount { get; init; }
    public int CriticalCount { get; init; }
    public double StoragePercent { get; init; }
    public long StorageUsedBytes { get; init; }
    public long StorageQuotaBytes { get; init; }
    public string TotalTrend { get; init; } = "—";
    public string RecentTrend { get; init; } = "—";
    public string ContractsTrend { get; init; } = "—";
    public string SharedTrend { get; init; } = "—";
    public string StorageTrend { get; init; } = "—";
    public string CriticalTrend { get; init; } = "—";
    public IReadOnlyList<int> TotalSparkline { get; init; } = [];
    public IReadOnlyList<int> RecentSparkline { get; init; } = [];
    public IReadOnlyList<int> ContractsSparkline { get; init; } = [];
    public IReadOnlyList<int> SharedSparkline { get; init; } = [];
    public IReadOnlyList<int> StorageSparkline { get; init; } = [];
    public IReadOnlyList<int> CriticalSparkline { get; init; } = [];
    public IReadOnlyList<DocumentListItem> Documents { get; init; } = [];
    public IReadOnlyList<DocumentCategoryItem> Categories { get; init; } = [];
    public IReadOnlyList<DocumentTagItem> PopularTags { get; init; } = [];
    public IReadOnlyList<string> TypeFilters { get; init; } = [];
    public IReadOnlyList<string> BuildingFilters { get; init; } = [];
    public string DefaultBuilding { get; init; } = "—";
}

public partial class DocumentListItem : ObservableObject
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string FileType { get; init; } = "PDF";
    public string FileTypeIcon { get; init; } = "FilePdfBox";
    public string SizeDisplay { get; init; } = "—";
    public long SizeBytes { get; init; }
    public string CategoryId { get; init; } = string.Empty;
    public string CategoryLabel { get; init; } = string.Empty;
    public string CategoryIcon { get; init; } = "FolderOutline";
    public string CategoryIconColor { get; init; } = "#64748B";
    public string DateDisplay { get; init; } = string.Empty;
    public string AddedAtDisplay { get; init; } = string.Empty;
    public string ModifiedAtDisplay { get; init; } = string.Empty;
    public string AddedBy { get; init; } = "—";
    public string Building { get; init; } = "—";
    public string? FilePath { get; init; }
    public string Status { get; init; } = "—";
    public string StatusBadgeBackground { get; init; } = "#DCFCE7";
    public string StatusBadgeForeground { get; init; } = "#166534";
    public string TypeLabel { get; init; } = "Document";
    public string PreviewTitle { get; init; } = string.Empty;
    public string PreviewBody { get; init; } = string.Empty;
    public bool IsFolder { get; init; }
    public bool IsShared { get; init; }
    public bool IsCritical { get; init; }
    public bool IsArchived { get; init; }
    public bool IsDeleted { get; init; }
    [ObservableProperty] private bool _isFavorite;
    [ObservableProperty] private bool _isSelected;
    public IReadOnlyList<DocumentTagItem> Tags { get; init; } = [];
}

public partial class DocumentCategoryItem : ObservableObject
{
    public string CategoryId { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
    public string IconKind { get; init; } = "FolderOutline";
    public string IconColor { get; init; } = "#64748B";
    [ObservableProperty] private bool _isSelected;
}

public class DocumentTagItem
{
    public string Label { get; init; } = string.Empty;
    public string Background { get; init; } = "#F1F5F9";
    public string Foreground { get; init; } = "#475569";
    public int Count { get; init; }
}

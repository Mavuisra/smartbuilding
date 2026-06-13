namespace SmartBuilding.Desktop.WPF.Models;

public sealed class SyncPageData
{
    public int SyncedCount { get; init; }
    public int PendingCount { get; init; }
    public int ConflictCount { get; init; }
    public long LocalDbSizeBytes { get; init; }
    public int TotalRecords { get; init; }
    public string LocalDbPath { get; init; } = string.Empty;
    public string LocalDatabaseLabel { get; init; } = "MySQL";
    public string DeviceLabel { get; init; } = string.Empty;
    public DateTime? LocalDbLastWrite { get; init; }
    public string CloudServerUrl { get; init; } = string.Empty;
    public DateTime? LastSyncAt { get; init; }
    public bool IsOnline { get; init; }
    public bool IsCloudReachable { get; init; }
    public int PingMs { get; init; }
    public int SyncIntervalSeconds { get; init; }
    public double GlobalProgress { get; init; }
    public string SyncStatusText { get; init; } = string.Empty;
    public string? LastSyncDuration { get; init; }
    public string? LastThroughput { get; init; }
    public int LastProcessed { get; init; }
    public int LastTotal { get; init; }
    public string? LastDataTransferred { get; init; }
    public IReadOnlyList<SyncDataTypeRow> DataTypes { get; init; } = [];
    public IReadOnlyList<SyncPendingRow> PendingItems { get; init; } = [];
    public IReadOnlyList<SyncConflictRow> Conflicts { get; init; } = [];
    public IReadOnlyList<SyncHistoryRow> History { get; init; } = [];
    public IReadOnlyList<SyncAlertRow> Alerts { get; init; } = [];
    public IReadOnlyList<int> Last7DaysCounts { get; init; } = [];
    public string? LastSyncError { get; init; }
    public bool AutoSyncEnabled { get; init; } = true;
    public string AutoSyncStatusLabel { get; init; } = "Active";
}

public sealed class SyncDataTypeRow
{
    public string Name { get; init; } = string.Empty;
    public int Synced { get; init; }
    public int Total { get; init; }
    public bool IsComplete => Total == 0 || Synced >= Total;
}

public sealed class SyncPendingRow
{
    public string TypeLabel { get; init; } = string.Empty;
    public string IconKind { get; init; } = "Database";
    public string Description { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public sealed class SyncConflictRow
{
    public string TableName { get; init; } = string.Empty;
    public string RecordLabel { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime ConflictAt { get; init; }
}

public sealed class SyncHistoryRow
{
    public DateTime StartedAt { get; init; }
    public string TypeLabel { get; init; } = string.Empty;
    public bool Success { get; init; }
    public int ItemsCount { get; init; }
    public string DataSizeLabel { get; init; } = "—";
    public string DurationLabel { get; init; } = "—";
    public string UserName { get; init; } = "Système";
    public string? Detail { get; init; }
}

public sealed class SyncAlertRow
{
    public string IconKind { get; init; } = "Information";
    public string IconColor { get; init; } = "#3B82F6";
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string TimeLabel { get; init; } = string.Empty;
}

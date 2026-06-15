namespace SmartBuilding.Infrastructure.Services;

public enum PrerequisiteKind
{
    DotNetRuntime,
    XamppMySql,
    MySqlService,
    MySqlServerReachable,
    NetworkInfo,
}

public sealed class PrerequisiteStatus
{
    public PrerequisiteKind Kind { get; init; }
    public string Title { get; init; } = "";
    public string Summary { get; init; } = "";
    public string Instructions { get; init; } = "";
    public string? DownloadLabel { get; init; }
    public string? DownloadUrl { get; init; }
    public bool IsSatisfied { get; init; }
    public bool IsOptional { get; init; }
}

public sealed class PrerequisiteCheckResult
{
    public required IReadOnlyList<PrerequisiteStatus> Items { get; init; }
    public string DeploymentModeLabel { get; init; } = "";
    public bool IsReady => Items.All(i => i.IsSatisfied || i.IsOptional);
}

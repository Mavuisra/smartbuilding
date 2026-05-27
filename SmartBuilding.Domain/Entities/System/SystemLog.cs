using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.System;

public class SystemLog : BaseEntity
{
    public string Level { get; set; } = "Info";
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public Guid? UserId { get; set; }
}

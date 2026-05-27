using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Sync;

public class SyncLog : BaseEntity
{
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool Success { get; set; }
    public int RecordsPushed { get; set; }
    public int RecordsPulled { get; set; }
    public int ConflictsResolved { get; set; }
    public string? ErrorMessage { get; set; }
    public string Direction { get; set; } = "Bidirectional";
}

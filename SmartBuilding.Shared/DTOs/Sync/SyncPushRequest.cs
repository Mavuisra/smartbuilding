namespace SmartBuilding.Shared.DTOs.Sync;

public class SyncPushRequest
{
    public string EntityType { get; set; } = string.Empty;
    public List<SyncEntityPayload> Entities { get; set; } = [];
}

public class SyncEntityPayload
{
    public Guid Id { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string JsonData { get; set; } = string.Empty;
}

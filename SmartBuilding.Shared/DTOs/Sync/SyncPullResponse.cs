namespace SmartBuilding.Shared.DTOs.Sync;

public class SyncPullResponse
{
    public DateTime ServerTimestamp { get; set; } = DateTime.UtcNow;
    public List<SyncEntityPayload> Entities { get; set; } = [];
}

using System.Text.Json;

namespace SmartBuilding.Infrastructure.Sync;

internal static class SyncJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };
}

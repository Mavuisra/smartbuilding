using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartBuilding.Infrastructure.Sync;

internal static class SyncJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

using System.Text.Json;

namespace SmartBuilding.Infrastructure.Sync;

internal static class SyncApiResponse
{
    public static bool TryParsePushResult(string? body, out int applied, out string? errorMessage)
    {
        applied = 0;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(body))
        {
            errorMessage = "Réponse API vide.";
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("success", out var ok) && !ok.GetBoolean())
            {
                errorMessage = root.TryGetProperty("message", out var msg)
                    ? msg.GetString() ?? "Échec push."
                    : "Échec push.";
                return false;
            }

            if (!root.TryGetProperty("data", out var data))
            {
                errorMessage = "Réponse push sans champ data.";
                return false;
            }

            applied = data.ValueKind switch
            {
                JsonValueKind.Number => data.GetInt32(),
                JsonValueKind.String when int.TryParse(data.GetString(), out var n) => n,
                _ => 0,
            };
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }
}

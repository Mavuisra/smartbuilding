using System.Text.Json;

namespace SmartBuilding.Infrastructure.Http;

public static class CloudApiAuth
{
    private static readonly (string Username, string Password)[] LoginFallbacks =
    [
        ("admin", "Admin@2026"),
        ("admin", "admin"),
    ];

    public static async Task<string?> LoginAsync(
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        foreach (var (username, password) in LoginFallbacks)
        {
            var token = await TryLoginAsync(baseUrl, username, password, cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(token))
                return token;
        }

        return null;
    }

    public static async Task<string?> TryLoginAsync(
        string baseUrl,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        CloudApiClient.HttpResult result;
        try
        {
            using var client = new CloudApiClient(baseUrl);
            result = await client
                .PostJsonAsync("api/auth/login/", new { username, password }, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            return null;
        }

        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.Body))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(result.Body);
            if (!doc.RootElement.TryGetProperty("success", out var ok) || !ok.GetBoolean())
                return null;
            if (!doc.RootElement.TryGetProperty("data", out var data))
                return null;
            if (!data.TryGetProperty("token", out var tokenEl))
                return null;
            var token = tokenEl.GetString();
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }
        catch
        {
            return null;
        }
    }
}

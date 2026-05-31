using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SmartBuilding.Infrastructure.Http;

namespace SmartBuilding.Desktop.WPF.Services;

/// <summary>
/// Réinitialise la base PostgreSQL sur Render via l'API web.
/// </summary>
public class CloudDatabaseResetService
{
    public const string ConfirmPhrase = "REINITIALISER SBMS";

    private readonly IConfiguration _configuration;

    public CloudDatabaseResetService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string ApiBaseUrl =>
        (_configuration["Api:BaseUrl"] ?? "https://smartbuilding-0kbk.onrender.com/").TrimEnd('/');

    public async Task<(bool Success, string Message)> ResetOnlineDatabaseAsync(
        string? bearerToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
            return (false, "Connectez-vous d'abord (sync cloud) pour obtenir un jeton API.");

        try
        {
            using var client = new CloudApiClient(ApiBaseUrl + "/", bearerToken.Trim());
            var result = await client.PostJsonAsync(
                "api/executive/admin/reset-database/",
                new { confirmPhrase = ConfirmPhrase, target = "server" },
                cancellationToken);

            if (result.StatusCode is < 200 or >= 300)
            {
                var msg = TryReadErrorMessage(result.Body) ?? $"HTTP {result.StatusCode}";
                return (false, msg);
            }

            return (true, "Base en ligne réinitialisée. Comptes admin/pdg recréés sur le serveur.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static string? TryReadErrorMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("message", out var m))
                return m.GetString();
        }
        catch
        {
            // ignore
        }

        return null;
    }
}

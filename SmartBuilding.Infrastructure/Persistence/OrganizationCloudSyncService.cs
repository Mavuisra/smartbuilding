using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartBuilding.Infrastructure.Http;
using SmartBuilding.Infrastructure.Sync;

namespace SmartBuilding.Infrastructure.Persistence;

/// <summary>Enregistre les métadonnées d'organisation sur le serveur central (Render).</summary>
public sealed class OrganizationCloudSyncService
{
    private readonly IConfiguration _configuration;
    private readonly OrganizationRegistry _registry;
    private readonly ILogger<OrganizationCloudSyncService>? _logger;

    public OrganizationCloudSyncService(
        IConfiguration configuration,
        OrganizationRegistry registry,
        ILogger<OrganizationCloudSyncService>? logger = null)
    {
        _configuration = configuration;
        _registry = registry;
        _logger = logger;
    }

    public async Task<bool> RegisterActiveOrganizationAsync(
        string? username = null,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        var org = _registry.Active;
        if (org is null)
            return false;

        if (org.SyncedToCloud)
            return true;

        var baseUrl = _configuration["Api:BaseUrl"] ?? "https://smartbuilding-0kbk.onrender.com/";
        if (!baseUrl.EndsWith('/'))
            baseUrl += "/";

        var token = SyncCloudTokenStore.Load();
        if (string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
            token = await SyncCloudTokenStore.AcquireAsync(_configuration, username, password, cancellationToken);

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger?.LogDebug("Pas de jeton cloud — enregistrement organisation reporté.");
            return false;
        }

        using var api = new CloudApiClient(baseUrl, token);
        api.SetOrganizationId(org.Id.ToString());

        var body = new
        {
            id = org.Id.ToString(),
            name = org.Name,
            slug = org.Slug,
            databaseName = org.DatabaseName,
            city = org.City,
            createdAt = org.CreatedAt.ToString("O"),
        };

        var result = await api.PostJsonAsync("api/organizations/register/", body, cancellationToken);
        if (!result.IsSuccess)
        {
            _logger?.LogWarning("Enregistrement organisation cloud échoué ({Status}): {Body}", result.StatusCode, result.Body);
            return false;
        }

        _registry.MarkSynced(org.Id);
        return true;
    }
}

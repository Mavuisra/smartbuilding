using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartBuilding.Domain.Entities.Auth;
using SmartBuilding.Infrastructure.Http;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Shared.DTOs.Sync;

namespace SmartBuilding.Infrastructure.Sync;

/// <summary>
/// Garantit que le compte connecté en local existe sur le cloud avec le même mot de passe (BCrypt).
/// </summary>
public sealed class CloudIdentityService
{
    private readonly IDbContextFactory<SmartBuildingDbContext> _contextFactory;
    private readonly IConfiguration _configuration;

    public CloudIdentityService(
        IDbContextFactory<SmartBuildingDbContext> contextFactory,
        IConfiguration configuration)
    {
        _contextFactory = contextFactory;
        _configuration = configuration;
    }

    public async Task<CloudIdentityResult> EnsureCloudLoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return CloudIdentityResult.Fail("Identifiants requis pour la connexion cloud.");

        var baseUrl = GetApiBaseUrl();

        var bootstrapToken = await CloudApiAuth.LoginAsync(baseUrl, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(bootstrapToken))
            return CloudIdentityResult.Fail("Serveur cloud injoignable — vérifiez Internet et l'URL API.");

        var (pushed, pushError) = await ForcePushUsersAsync(baseUrl, bootstrapToken, cancellationToken)
            .ConfigureAwait(false);
        if (pushed <= 0)
        {
            return CloudIdentityResult.Fail(
                pushError ?? "Échec de la publication des comptes vers le cloud.");
        }

        var userToken = await CloudApiAuth.TryLoginAsync(baseUrl, username, password, cancellationToken)
            .ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(userToken))
        {
            SyncCloudTokenStore.Persist(userToken);
            return CloudIdentityResult.Ok(
                userToken,
                $"Compte « {username} » publié ({pushed} utilisateur(s)) — mêmes identifiants en ligne.");
        }

        return CloudIdentityResult.Fail(
            $"{pushed} compte(s) publié(s) mais la connexion cloud a échoué pour « {username} ». " +
            "Vérifiez le mot de passe local.");
    }

    /// <summary>Marque les utilisateurs en attente puis les pousse vers le cloud (hash BCrypt inclus).</summary>
    public async Task<(int Pushed, string? Error)> ForcePushUsersAsync(
        string? baseUrl = null,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        baseUrl ??= GetApiBaseUrl();
        bearerToken ??= await CloudApiAuth.LoginAsync(baseUrl, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(bearerToken))
            return (0, "Jeton API cloud indisponible.");

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var adapter = SyncEntityRegistry.TryGet("Users");
        if (adapter is null)
            return (0, "Adaptateur sync Users manquant.");

        var users = await context.Users
            .IgnoreQueryFilters()
            .Where(u => u.DeletedAt == null && u.IsActive)
            .OrderBy(u => u.Username)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (users.Count == 0)
            return (0, "Aucun utilisateur actif en local.");

        foreach (var user in users)
        {
            user.IsSynced = false;
            user.MarkUpdated();
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var payloads = users.Select(u => new SyncEntityPayload
        {
            Id = u.Id,
            UpdatedAt = u.UpdatedAt,
            DeletedAt = u.DeletedAt,
            JsonData = System.Text.Json.JsonSerializer.Serialize(u, SyncJson.Options)
        }).ToList();

        using var api = new CloudApiClient(baseUrl, bearerToken);
        var pushRequest = new SyncPushRequest { EntityType = "Users", Entities = payloads };
        var pushResult = await api.PostJsonAsync("api/sync/push/", pushRequest, cancellationToken)
            .ConfigureAwait(false);

        if (!pushResult.IsSuccess)
        {
            return (0, $"HTTP {(int)pushResult.StatusCode} lors du push Users.");
        }

        if (!SyncApiResponse.IsApiSuccess(pushResult.Body, out var apiError))
            return (0, apiError ?? "Réponse API invalide lors du push Users.");

        if (!SyncApiResponse.TryParsePushResult(pushResult.Body, out var applied, out var parseError) || applied <= 0)
            return (0, parseError ?? "Aucun utilisateur appliqué côté cloud.");

        await adapter.MarkAsSyncedAsync(context, users.Select(u => u.Id).ToList(), cancellationToken)
            .ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return (applied, null);
    }

    /// <summary>Publie tous les utilisateurs actifs (hash BCrypt inclus) vers le cloud.</summary>
    public Task<(int Pushed, string? Error)> PushActiveUsersAsync(
        string? baseUrl = null,
        string? bearerToken = null,
        CancellationToken cancellationToken = default) =>
        ForcePushUsersAsync(baseUrl, bearerToken, cancellationToken);

    private string GetApiBaseUrl()
    {
        var baseUrl = _configuration["Api:BaseUrl"] ?? "https://smartbuilding-0kbk.onrender.com/";
        return baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/";
    }
}

public sealed record CloudIdentityResult(bool Success, string? Token, string Message)
{
    public static CloudIdentityResult Ok(string token, string message) => new(true, token, message);
    public static CloudIdentityResult Fail(string message) => new(false, null, message);
}

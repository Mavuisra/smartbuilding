using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Domain.Entities.Sync;
using SmartBuilding.Infrastructure.Http;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Sync;
using SmartBuilding.Shared.DTOs.Sync;

namespace SmartBuilding.Infrastructure.Services;

public class SyncService : ISyncService
{
    private static readonly SemaphoreSlim SyncGate = new(1, 1);

    private static readonly string ApiTokenPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SBMS",
        "api-token.txt");

    private readonly SmartBuildingDbContext _context;
    private readonly INetworkService _network;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SyncService> _logger;

    private DateTime? _lastSyncAt;

    public DateTime? LastSyncAt => _lastSyncAt;
    public bool IsSyncing { get; private set; }

    public SyncService(
        SmartBuildingDbContext context,
        INetworkService network,
        IConfiguration configuration,
        ILogger<SyncService> logger)
    {
        _context = context;
        _network = network;
        _configuration = configuration;
        _logger = logger;
    }

    public Task<bool> IsOnlineAsync(CancellationToken cancellationToken = default) =>
        _network.CanReachApiAsync(
            _configuration["Api:BaseUrl"] ?? "https://smartbuilding-0kbk.onrender.com",
            cancellationToken);

    public async Task EnsureMetadataLoadedAsync(CancellationToken cancellationToken = default)
    {
        _lastSyncAt ??= await SyncCoordinator.GetLastSuccessfulSyncAtAsync(_context, cancellationToken);
    }

    public async Task<SyncResult> SyncAsync(bool manual = false, CancellationToken cancellationToken = default)
    {
        if (!await SyncGate.WaitAsync(0, cancellationToken))
            return new SyncResult(false, 0, 0, 0, "Synchronisation déjà en cours.");

        try
        {
            return await SyncCoreAsync(manual, cancellationToken);
        }
        finally
        {
            SyncGate.Release();
        }
    }

    private async Task<SyncResult> SyncCoreAsync(bool manual, CancellationToken cancellationToken)
    {
        if (IsSyncing)
            return new SyncResult(false, 0, 0, 0, "Synchronisation déjà en cours.");

        if (!await IsOnlineAsync(cancellationToken))
            return new SyncResult(false, 0, 0, 0, "Hors ligne — synchronisation reportée.");

        IsSyncing = true;
        var log = new SyncLog { StartedAt = DateTime.UtcNow, Direction = manual ? "Manual" : "Auto" };
        var pushed = 0;
        var pulled = 0;
        var conflicts = 0;
        var hadFailure = false;
        var failures = new List<string>();

        try
        {
            _lastSyncAt ??= await SyncCoordinator.GetLastSuccessfulSyncAtAsync(_context, cancellationToken);
            var lastSync = _lastSyncAt ?? DateTime.MinValue;
            var baseUrl = GetApiBaseUrl();

            var token = _configuration["Api:Token"];
            if (string.IsNullOrWhiteSpace(token))
                token = LoadTokenFromLocalStore();

            if (string.IsNullOrWhiteSpace(token))
            {
                token = await CloudApiAuth.LoginAsync(baseUrl, cancellationToken);
                if (string.IsNullOrWhiteSpace(token))
                {
                    return new SyncResult(
                        false, 0, 0, 0,
                        "Connexion API refusée — vérifiez que Django tourne (port 8000) et exécutez: python manage.py seed_smartbuilding");
                }

                PersistApiToken(token);
            }

            using var api = new CloudApiClient(baseUrl, token);

            foreach (var entityType in SyncEntityRegistry.SyncableTypes)
            {
                var adapter = SyncEntityRegistry.TryGet(entityType);
                if (adapter is null)
                {
                    hadFailure = true;
                    failures.Add($"{entityType}: adaptateur desktop manquant");
                    _logger.LogWarning("Adaptateur sync manquant pour {EntityType}", entityType);
                    continue;
                }

                var localChanges = await adapter.GetLocalChangesAsync(_context, cancellationToken);
                if (manual && localChanges.Count == 0)
                {
                    localChanges = await adapter.GetChangesSinceAsync(
                        _context, DateTime.MinValue, cancellationToken);
                }

                if (localChanges.Count > 0)
                {
                    var pushRequest = new SyncPushRequest
                    {
                        EntityType = entityType,
                        Entities = localChanges.ToList(),
                    };

                    var pushResult = await PostWithAuthRetryAsync(
                        api, baseUrl, "api/sync/push", pushRequest, cancellationToken);

                    if (pushResult.IsSuccess)
                    {
                        pushed += localChanges.Count;
                        await adapter.MarkAsSyncedAsync(
                            _context, localChanges.Select(e => e.Id).ToList(), cancellationToken);
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        hadFailure = true;
                        var failure = $"{entityType} push: HTTP {pushResult.StatusCode} {Truncate(pushResult.Body)}";
                        failures.Add(failure);
                        _logger.LogWarning("Push échoué pour {EntityType}: {Detail}", entityType, failure);
                    }
                }

                var pullPath =
                    $"api/sync/pull?entityType={Uri.EscapeDataString(entityType)}&since={lastSync:O}";
                var pullResult = await GetWithAuthRetryAsync<SyncPullResponse>(
                    api, baseUrl, pullPath, cancellationToken);

                if (pullResult.Data is null && pullResult.StatusCode is 401 or 403)
                {
                    hadFailure = true;
                    failures.Add($"{entityType} pull: HTTP {pullResult.StatusCode} non autorisé");
                    continue;
                }

                if (pullResult.Data?.Entities.Count > 0)
                {
                    conflicts += await SyncCoordinator.ApplyPullAsync(
                        _context, entityType, pullResult.Data.Entities, cancellationToken);
                    pulled += pullResult.Data.Entities.Count;
                }
            }

            log.Success = !hadFailure;
            log.RecordsPushed = pushed;
            log.RecordsPulled = pulled;
            log.ConflictsResolved = conflicts;

            if (!hadFailure)
            {
                _lastSyncAt = DateTime.UtcNow;
                _logger.LogInformation(
                    "Sync OK: pushed={Pushed}, pulled={Pulled}, conflicts={Conflicts}",
                    pushed, pulled, conflicts);
                return new SyncResult(true, pushed, pulled, conflicts, null);
            }

            var message = failures.Count > 0
                ? "Synchronisation partielle — " + string.Join(" | ", failures.Take(5))
                : "Synchronisation partielle — certains types n'ont pas été synchronisés.";
            log.ErrorMessage = message;
            _logger.LogWarning(message);
            return new SyncResult(false, pushed, pulled, conflicts, message);
        }
        catch (Exception ex)
        {
            log.Success = false;
            log.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Échec synchronisation");
            return new SyncResult(false, pushed, pulled, conflicts, ex.Message);
        }
        finally
        {
            log.CompletedAt = DateTime.UtcNow;
            _context.SyncLogs.Add(log);
            await _context.SaveChangesAsync(cancellationToken);
            IsSyncing = false;

            if (log.Success)
                _lastSyncAt = log.CompletedAt;
            else
                _lastSyncAt = await SyncCoordinator.GetLastSuccessfulSyncAtAsync(_context, cancellationToken);
        }
    }

    private async Task<CloudApiClient.HttpResult> PostWithAuthRetryAsync<T>(
        CloudApiClient api,
        string baseUrl,
        string path,
        T body,
        CancellationToken cancellationToken)
    {
        var result = await api.PostJsonAsync(path, body, cancellationToken);
        if (result.StatusCode is not 401 and not 403)
            return result;

        var token = await RefreshTokenAsync(baseUrl, cancellationToken);
        if (token is null)
            return result;

        api.SetBearerToken(token);
        return await api.PostJsonAsync(path, body, cancellationToken);
    }

    private async Task<(int StatusCode, T? Data)> GetWithAuthRetryAsync<T>(
        CloudApiClient api,
        string baseUrl,
        string path,
        CancellationToken cancellationToken)
    {
        var first = await api.GetAsync(path, cancellationToken);
        if (first.StatusCode is not 401 and not 403)
            return (first.StatusCode, Deserialize<T>(first.Body));

        var token = await RefreshTokenAsync(baseUrl, cancellationToken);
        if (token is null)
            return (first.StatusCode, default);

        api.SetBearerToken(token);
        var retry = await api.GetAsync(path, cancellationToken);
        return (retry.StatusCode, Deserialize<T>(retry.Body));
    }

    private async Task<string?> RefreshTokenAsync(string baseUrl, CancellationToken cancellationToken)
    {
        ClearPersistedApiToken();
        var token = await CloudApiAuth.LoginAsync(baseUrl, cancellationToken);
        if (!string.IsNullOrWhiteSpace(token))
            PersistApiToken(token);
        return token;
    }

    private static T? Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;
        try
        {
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
        }
        catch
        {
            return default;
        }
    }

    private string GetApiBaseUrl()
    {
        var baseUrl = _configuration["Api:BaseUrl"] ?? "http://127.0.0.1:8000/";
        return baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/";
    }

    private static string Truncate(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "";
        body = body.Replace(Environment.NewLine, " ").Trim();
        return body.Length > 300 ? body[..300] + "..." : body;
    }

    private static void ClearPersistedApiToken()
    {
        try
        {
            if (File.Exists(ApiTokenPath))
                File.Delete(ApiTokenPath);
        }
        catch
        {
            // ignore
        }
    }

    private static string? LoadTokenFromLocalStore()
    {
        try
        {
            if (!File.Exists(ApiTokenPath))
                return null;
            var value = File.ReadAllText(ApiTokenPath).Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch
        {
            return null;
        }
    }

    private static void PersistApiToken(string token)
    {
        try
        {
            var folder = Path.GetDirectoryName(ApiTokenPath);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);
            File.WriteAllText(ApiTokenPath, token.Trim());
        }
        catch
        {
            // ignore
        }
    }
}

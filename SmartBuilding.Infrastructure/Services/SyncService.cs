using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Domain.Entities.Sync;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Sync;
using SmartBuilding.Shared.Constants;
using SmartBuilding.Shared.DTOs.Sync;

namespace SmartBuilding.Infrastructure.Services;

public class SyncService : ISyncService
{
    private readonly SmartBuildingDbContext _context;
    private readonly INetworkService _network;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SyncService> _logger;

    private DateTime? _lastSyncAt;

    public DateTime? LastSyncAt => _lastSyncAt;
    public bool IsSyncing { get; private set; }

    public SyncService(
        SmartBuildingDbContext context,
        INetworkService network,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<SyncService> logger)
    {
        _context = context;
        _network = network;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public Task<bool> IsOnlineAsync(CancellationToken cancellationToken = default) =>
        _network.CanReachApiAsync(_configuration["Api:BaseUrl"] ?? "https://smartbuilding-0kbk.onrender.com", cancellationToken);

    public async Task EnsureMetadataLoadedAsync(CancellationToken cancellationToken = default)
    {
        _lastSyncAt ??= await SyncCoordinator.GetLastSuccessfulSyncAtAsync(_context, cancellationToken);
    }

    public async Task<SyncResult> SyncAsync(bool manual = false, CancellationToken cancellationToken = default)
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
            var client = CreateHttpClient();

            foreach (var entityType in SyncEntityRegistry.SyncableTypes)
            {
                var adapter = SyncEntityRegistry.TryGet(entityType);
                if (adapter is null)
                {
                    hadFailure = true;
                    var failure = $"{entityType}: adaptateur desktop manquant";
                    failures.Add(failure);
                    _logger.LogWarning("Adaptateur sync manquant pour {EntityType}", entityType);
                    continue;
                }

                var localChanges = await adapter.GetLocalChangesAsync(_context, cancellationToken);
                // Bootstrap cloud: sur sync manuelle, si rien n'est marqué "local change",
                // on envoie un snapshot complet pour éviter un web vide.
                if (manual && localChanges.Count == 0)
                {
                    localChanges = await adapter.GetChangesSinceAsync(
                        _context,
                        DateTime.MinValue,
                        cancellationToken);
                }
                if (localChanges.Count > 0)
                {
                    var pushRequest = new SyncPushRequest { EntityType = entityType, Entities = localChanges.ToList() };
                    var pushResponse = await client.PostAsJsonAsync("api/sync/push", pushRequest, cancellationToken);
                    if (pushResponse.IsSuccessStatusCode)
                    {
                        pushed += localChanges.Count;
                        await adapter.MarkAsSyncedAsync(_context, localChanges.Select(e => e.Id).ToList(), cancellationToken);
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        hadFailure = true;
                        var body = await ReadErrorBodyAsync(pushResponse, cancellationToken);
                        var failure = $"{entityType} push: HTTP {(int)pushResponse.StatusCode} {pushResponse.StatusCode} {body}";
                        failures.Add(failure);
                        _logger.LogWarning("Push échoué pour {EntityType}: {Status} {Body}", entityType, pushResponse.StatusCode, body);
                    }
                }

                var pullResponse = await client.GetAsync(
                    $"api/sync/pull?entityType={Uri.EscapeDataString(entityType)}&since={lastSync:O}",
                    cancellationToken);

                if (!pullResponse.IsSuccessStatusCode)
                {
                    hadFailure = true;
                    var body = await ReadErrorBodyAsync(pullResponse, cancellationToken);
                    var failure = $"{entityType} pull: HTTP {(int)pullResponse.StatusCode} {pullResponse.StatusCode} {body}";
                    failures.Add(failure);
                    _logger.LogWarning("Pull échoué pour {EntityType}: {Status} {Body}", entityType, pullResponse.StatusCode, body);
                    continue;
                }

                var pullData = await pullResponse.Content.ReadFromJsonAsync<SyncPullResponse>(cancellationToken);
                if (pullData?.Entities.Count > 0)
                {
                    conflicts += await SyncCoordinator.ApplyPullAsync(
                        _context, entityType, pullData.Entities, cancellationToken);
                    pulled += pullData.Entities.Count;
                }
            }

            log.Success = !hadFailure;
            log.RecordsPushed = pushed;
            log.RecordsPulled = pulled;
            log.ConflictsResolved = conflicts;

            if (!hadFailure)
            {
                _lastSyncAt = DateTime.UtcNow;
                _logger.LogInformation("Sync OK: pushed={Pushed}, pulled={Pulled}, conflicts={Conflicts}", pushed, pulled, conflicts);
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

    private HttpClient CreateHttpClient()
    {
        var client = _httpClientFactory.CreateClient("SmartBuildingApi");
        var token = _configuration["Api:Token"];
        if (string.IsNullOrWhiteSpace(token))
            token = LoadTokenFromLocalStore();
        if (!string.IsNullOrEmpty(token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<string> ReadErrorBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(body))
                return "";
            body = body.Replace(Environment.NewLine, " ").Trim();
            return body.Length > 300 ? body[..300] + "..." : body;
        }
        catch
        {
            return "";
        }
    }

    private static string? LoadTokenFromLocalStore()
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SBMS",
                "api-token.txt");
            if (!File.Exists(path))
                return null;
            var value = File.ReadAllText(path).Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch
        {
            return null;
        }
    }
}

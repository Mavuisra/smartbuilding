using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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
    private const int PushBatchSize = 200;

    private static readonly SemaphoreSlim SyncGate = new(1, 1);

    private readonly IDbContextFactory<SmartBuildingDbContext> _contextFactory;
    private readonly INetworkService _network;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SyncService> _logger;
    private readonly ISyncNotifier _notifier;

    private DateTime? _lastSyncAt;

    public DateTime? LastSyncAt => _lastSyncAt;
    public bool IsSyncing { get; private set; }

    public SyncService(
        IDbContextFactory<SmartBuildingDbContext> contextFactory,
        INetworkService network,
        IConfiguration configuration,
        ILogger<SyncService> logger,
        ISyncNotifier notifier)
    {
        _contextFactory = contextFactory;
        _network = network;
        _configuration = configuration;
        _logger = logger;
        _notifier = notifier;
    }

    public Task<bool> IsOnlineAsync(CancellationToken cancellationToken = default) =>
        _network.CanReachApiAsync(
            _configuration["Api:BaseUrl"] ?? "https://smartbuilding-0kbk.onrender.com",
            cancellationToken);

    public async Task<bool> IsCloudStoreEmptyAsync(CancellationToken cancellationToken = default)
    {
        if (!await IsOnlineAsync(cancellationToken))
            return false;

        var baseUrl = GetApiBaseUrl();
        var token = SyncCloudTokenStore.Load();
        if (string.IsNullOrWhiteSpace(token))
            token = await SyncCloudTokenStore.AcquireAsync(_configuration, cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
            return false;

        using var api = new CloudApiClient(baseUrl, token);
        var (statusCode, body) = await GetRawWithAuthRetryAsync(api, baseUrl, "api/sync/status/", cancellationToken);
        if (statusCode is < 200 or >= 300 || string.IsNullOrWhiteSpace(body))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("data", out var data))
                return false;
            if (data.TryGetProperty("syncStoreTotal", out var totalEl) && totalEl.TryGetInt32(out var total))
                return total <= 0;
            if (data.TryGetProperty("hasBusinessData", out var hasBiz) && hasBiz.ValueKind == JsonValueKind.False)
                return true;
            if (data.TryGetProperty("pipelineStatus", out var statusEl)
                && statusEl.GetString() is "empty" or "sync_partial")
                return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Lecture sync/status impossible — republish local ignoré.");
        }

        return false;
    }

    public async Task MarkAllLocalDataForPushAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        foreach (var entityType in SyncEntityRegistry.SyncableTypes)
        {
            var adapter = SyncEntityRegistry.TryGet(entityType);
            if (adapter is null)
                continue;
            await adapter.MarkAllUnsyncedAsync(context, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task EnsureMetadataLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_lastSyncAt.HasValue)
            return;

        await using var readContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        _lastSyncAt = await SyncCoordinator.GetLastSuccessfulSyncAtAsync(readContext, cancellationToken);
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
        var log = new SyncLog { StartedAt = DateTime.UtcNow, Direction = manual ? "Manual" : "Auto", IsSynced = true };
        var pushed = 0;
        var pulled = 0;
        var conflicts = 0;
        var hadFailure = false;
        var failures = new List<string>();

        using (await DbContextAccessLock.AcquireAsync(cancellationToken))
        await using (var context = await _contextFactory.CreateDbContextAsync(cancellationToken))
        {
        try
        {
            await DatabaseSchemaUpgrader.UpgradeAsync(context, cancellationToken);

            _lastSyncAt ??= await SyncCoordinator.GetLastSuccessfulSyncAtAsync(context, cancellationToken);
            var lastSync = _lastSyncAt ?? DateTime.MinValue;
            var baseUrl = GetApiBaseUrl();

            var token = SyncCloudTokenStore.Load();
            if (string.IsNullOrWhiteSpace(token))
                token = await SyncCloudTokenStore.AcquireAsync(_configuration, cancellationToken: cancellationToken);

            if (string.IsNullOrWhiteSpace(token))
            {
                var noToken = new SyncResult(
                    false, 0, 0, 0,
                    "Synchronisation cloud indisponible — vérifiez l'URL API et les identifiants.");
                _notifier.Notify(noToken);
                return noToken;
            }

            using var api = new CloudApiClient(baseUrl, token);
            var deviceLabel = DesktopSyncDevice.GetDeviceLabel();
            log.Direction = manual ? $"Manual ({deviceLabel})" : $"Auto ({deviceLabel})";

            await SyncDependencyPusher.PrepareRentPaymentChainAsync(context, cancellationToken);

            // Phase 1 — Push : envoyer toutes les modifications locales (offline first).
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

                if (entityType == "RentPayments")
                {
                    await SyncDependencyPusher.PrepareRentPaymentChainAsync(context, cancellationToken);
                    foreach (var depType in SyncDependencyPusher.RentPaymentChain)
                    {
                        var depAdapter = SyncEntityRegistry.TryGet(depType);
                        if (depAdapter is null)
                            continue;

                        var depPushed = await PushAllPendingAsync(
                            api, baseUrl, context, depAdapter, depType, cancellationToken);
                        if (depPushed < 0)
                        {
                            hadFailure = true;
                            failures.Add(LastPushError ?? $"{depType}: échec envoi des dépendances loyer");
                        }
                        else
                        {
                            pushed += depPushed;
                        }
                    }
                }

                var typePushed = await PushAllPendingAsync(
                    api,
                    baseUrl,
                    context,
                    adapter,
                    entityType,
                    cancellationToken);

                if (typePushed < 0)
                {
                    hadFailure = true;
                    failures.Add(LastPushError ?? $"{entityType}: échec envoi vers le cloud");
                }
                else
                {
                    pushed += typePushed;
                }
            }

            await context.SaveChangesAsync(cancellationToken);

            // Phase 2 — Pull : récupérer les changements des autres postes via PostgreSQL.
            foreach (var entityType in SyncEntityRegistry.SyncableTypes)
            {
                var adapter = SyncEntityRegistry.TryGet(entityType);
                if (adapter is null)
                    continue;

                try
                {
                    var pullResult = await PullEntityTypeAsync(
                        api,
                        baseUrl,
                        context,
                        entityType,
                        lastSync,
                        cancellationToken,
                        failures);
                    pulled += pullResult.Pulled;
                    conflicts += pullResult.Conflicts;
                }
                catch (Exception pullEx)
                {
                    failures.Add($"{entityType} pull: {pullEx.Message}");
                    _logger.LogWarning(pullEx, "Pull ignoré pour {EntityType}", entityType);
                }
            }

            await context.SaveChangesAsync(cancellationToken);

            await DatabaseSeeder.EnsureReservedAdminAccountsAsync(context, cancellationToken);

            var remainingPending = await SyncCoordinator.CountAllUnsyncedAsync(context, cancellationToken);
            if (remainingPending > 0)
            {
                var pendingDetail = await SyncCoordinator.DescribeUnsyncedAsync(context, 8, cancellationToken);
                if (!string.IsNullOrWhiteSpace(pendingDetail))
                    failures.Add($"En attente : {pendingDetail}");
            }

            log.RecordsPushed = pushed;
            log.RecordsPulled = pulled;
            log.ConflictsResolved = conflicts;
            log.Success = !hadFailure && remainingPending == 0;

            if (log.Success)
            {
                _lastSyncAt = DateTime.UtcNow;
                _logger.LogInformation(
                    "Sync OK: pushed={Pushed}, pulled={Pulled}, pending=0",
                    pushed, pulled);
                var ok = new SyncResult(true, pushed, pulled, conflicts, null);
                _notifier.Notify(ok);
                return ok;
            }

            var message = failures.Count > 0
                ? string.Join(" | ", failures.Take(6))
                : "Synchronisation incomplète.";
            if (remainingPending > 0)
                message += $" — {remainingPending} enregistrement(s) encore en attente.";
            log.ErrorMessage = message;
            _logger.LogWarning(message);
            var partial = new SyncResult(pushed > 0 && remainingPending == 0, pushed, pulled, conflicts, message);
            _notifier.Notify(partial);
            return partial;
        }
        catch (Exception ex)
        {
            log.Success = false;
            log.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Échec synchronisation");
            var failed = new SyncResult(false, pushed, pulled, conflicts, ex.Message);
            _notifier.Notify(failed);
            return failed;
        }
        finally
        {
            log.CompletedAt = DateTime.UtcNow;
            context.SyncLogs.Add(log);
            await context.SaveChangesAsync(cancellationToken);
            IsSyncing = false;

            if (log.Success)
                _lastSyncAt = log.CompletedAt;
            else
                _lastSyncAt = await SyncCoordinator.GetLastSuccessfulSyncAtAsync(context, cancellationToken);
        }
        }
    }

    private async Task<(int Pulled, int Conflicts)> PullEntityTypeAsync(
        CloudApiClient api,
        string baseUrl,
        SmartBuildingDbContext context,
        string entityType,
        DateTime lastSync,
        CancellationToken cancellationToken,
        List<string> failures)
    {
        var pulled = 0;
        var conflicts = 0;
        var pullSince = lastSync;

        while (true)
        {
            var pullPath =
                $"api/sync/pull?entityType={Uri.EscapeDataString(entityType)}&since={pullSince:O}";
            var pullResult = await GetWithAuthRetryAsync<SyncPullResponse>(
                api, baseUrl, pullPath, cancellationToken);

            if (pullResult.StatusCode is 401 or 403)
            {
                failures.Add($"{entityType} pull: HTTP {pullResult.StatusCode}");
                break;
            }

            var entities = pullResult.Data?.Entities;
            if (entities is null || entities.Count == 0)
                break;

            conflicts += await SyncCoordinator.ApplyPullAsync(
                context, entityType, entities, cancellationToken);
            pulled += entities.Count;

            if (entities.Count < 200)
                break;

            pullSince = entities.Max(e => e.UpdatedAt);
        }

        return (pulled, conflicts);
    }

    /// <summary>
    /// Envoie tous les enregistrements locaux non synchronisés par lots jusqu'à épuisement.
    /// Retourne le nombre poussé, ou -1 en cas d'échec bloquant.
    /// </summary>
    private async Task<int> PushAllPendingAsync(
        CloudApiClient api,
        string baseUrl,
        SmartBuildingDbContext context,
        IEntitySyncAdapter adapter,
        string entityType,
        CancellationToken cancellationToken)
    {
        var totalPushed = 0;

        while (true)
        {
            var pending = await adapter.GetLocalChangesAsync(context, cancellationToken);
            if (pending.Count == 0)
                break;

            var batch = pending.Take(PushBatchSize).ToList();
            var batchPushed = await PushBatchWithSplitAsync(
                api,
                baseUrl,
                context,
                adapter,
                entityType,
                batch,
                cancellationToken);

            if (batchPushed < 0)
                return -1;

            totalPushed += batchPushed;

            if (batchPushed == 0 && batch.Count > 0)
            {
                _logger.LogWarning(
                    "Push bloqué pour {EntityType}: {Count} enregistrement(s) non appliqué(s) côté serveur",
                    entityType,
                    batch.Count);
                return -1;
            }
        }

        return totalPushed;
    }

    private async Task<int> PushBatchWithSplitAsync(
        CloudApiClient api,
        string baseUrl,
        SmartBuildingDbContext context,
        IEntitySyncAdapter adapter,
        string entityType,
        IReadOnlyList<SyncEntityPayload> batch,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
            return 0;

        var pushRequest = new SyncPushRequest
        {
            EntityType = entityType,
            Entities = batch.ToList(),
        };

        var pushResult = await PostWithAuthRetryAsync(
            api, baseUrl, "api/sync/push/", pushRequest, cancellationToken);

        if (!pushResult.IsSuccess)
        {
            var httpErr = Truncate(pushResult.Body);
            _logger.LogWarning(
                "Push HTTP {Status} pour {EntityType}: {Body}",
                pushResult.StatusCode,
                entityType,
                httpErr);
            LastPushError = $"{entityType}: HTTP {pushResult.StatusCode} {httpErr}";
            return -1;
        }

        if (!SyncApiResponse.IsApiSuccess(pushResult.Body, out var apiErr))
        {
            _logger.LogWarning("Push API {EntityType}: {Error}", entityType, apiErr);
            LastPushError = $"{entityType}: {apiErr}";
            return -1;
        }

        if (!SyncApiResponse.TryParsePushResult(pushResult.Body, out var applied, out var parseError))
        {
            _logger.LogWarning("Push {EntityType}: {Error}", entityType, parseError);
            return -1;
        }

        if (applied == batch.Count)
        {
            await adapter.MarkAsSyncedAsync(
                context, batch.Select(e => e.Id).ToList(), cancellationToken);
            return applied;
        }

        if (batch.Count == 1)
        {
            var detail = Truncate(pushResult.Body);
            LastPushError =
                $"{entityType}/{batch[0].Id}: non appliqué côté serveur (applied=0). {detail}";
            _logger.LogWarning(
                "Enregistrement {EntityType}/{Id} refusé ou ignoré par le serveur (applied=0): {Body}",
                entityType,
                batch[0].Id,
                detail);
            return -1;
        }

        var pushed = 0;
        var failedSingles = 0;
        foreach (var single in batch)
        {
            var one = await PushBatchWithSplitAsync(
                api, baseUrl, context, adapter, entityType, [single], cancellationToken);
            if (one > 0)
                pushed += one;
            else
                failedSingles++;
        }

        return pushed > 0 ? pushed : failedSingles > 0 ? -1 : 0;
    }

    internal static string? LastPushError { get; private set; }

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

        SyncCloudTokenStore.Clear();
        var token = await SyncCloudTokenStore.AcquireAsync(_configuration, cancellationToken: cancellationToken);
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

        SyncCloudTokenStore.Clear();
        var token = await SyncCloudTokenStore.AcquireAsync(_configuration, cancellationToken: cancellationToken);
        if (token is null)
            return (first.StatusCode, default);

        api.SetBearerToken(token);
        var retry = await api.GetAsync(path, cancellationToken);
        return (retry.StatusCode, Deserialize<T>(retry.Body));
    }

    private async Task<(int StatusCode, string Body)> GetRawWithAuthRetryAsync(
        CloudApiClient api,
        string baseUrl,
        string path,
        CancellationToken cancellationToken)
    {
        var first = await api.GetAsync(path, cancellationToken);
        if (first.StatusCode is not 401 and not 403)
            return (first.StatusCode, first.Body);

        SyncCloudTokenStore.Clear();
        var token = await SyncCloudTokenStore.AcquireAsync(_configuration, cancellationToken: cancellationToken);
        if (token is null)
            return (first.StatusCode, first.Body);

        api.SetBearerToken(token);
        var retry = await api.GetAsync(path, cancellationToken);
        return (retry.StatusCode, retry.Body);
    }

    private static T? Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;
        try
        {
            return JsonSerializer.Deserialize<T>(json, SyncJson.Options);
        }
        catch
        {
            return default;
        }
    }

    private string GetApiBaseUrl()
    {
        var baseUrl = _configuration["Api:BaseUrl"] ?? "https://smartbuilding-0kbk.onrender.com/";
        return baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/";
    }

    private static string Truncate(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "";
        body = body.Replace(Environment.NewLine, " ").Trim();
        return body.Length > 300 ? body[..300] + "..." : body;
    }

}

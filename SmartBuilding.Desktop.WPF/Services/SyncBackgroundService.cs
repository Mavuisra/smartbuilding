using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Sync;
using SmartBuilding.Shared.Constants;

namespace SmartBuilding.Desktop.WPF.Services;

/// <summary>
/// Synchronisation automatique offline-first : dès qu'Internet est disponible,
/// envoie les données locales vers le cloud sans action utilisateur.
/// </summary>
public class SyncBackgroundService : BackgroundService
{
    private const int PendingFastIntervalSeconds = 20;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly NetworkConnectivityWatcher _connectivity;
    private readonly ILogger<SyncBackgroundService> _logger;
    private readonly SemaphoreSlim _triggerGate = new(1, 1);
    private int _consecutiveFailures;

    public SyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        NetworkConnectivityWatcher connectivity,
        ILogger<SyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _connectivity = connectivity;
        _logger = logger;
        _connectivity.InternetRestored += OnInternetRestored;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var baseInterval = _configuration.GetValue("Sync:IntervalSeconds", SyncConstants.AutoSyncIntervalSeconds);
        var autoSync = _configuration.GetValue("Sync:EnableAutoSync", true);

        if (!autoSync)
        {
            _logger.LogInformation("Sync automatique désactivée (Sync:EnableAutoSync=false).");
            return;
        }

        _logger.LogInformation(
            "Sync automatique active — intervalle {Interval}s (accéléré à {Fast}s si données en attente).",
            baseInterval,
            PendingFastIntervalSeconds);

        await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken);
        await RunSyncCycleAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delaySeconds = await ResolveDelaySecondsAsync(baseInterval, stoppingToken);
            var delay = SyncRetryPolicy.GetDelay(delaySeconds, _consecutiveFailures);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunSyncCycleAsync(stoppingToken);
        }
    }

    private void OnInternetRestored(object? sender, EventArgs e)
    {
        _logger.LogInformation("Internet rétabli — synchronisation automatique immédiate.");
        _ = TriggerSyncAsync(CancellationToken.None);
    }

    private async Task TriggerSyncAsync(CancellationToken cancellationToken)
    {
        if (!await _triggerGate.WaitAsync(0, cancellationToken))
            return;

        try
        {
            await RunSyncCycleAsync(cancellationToken);
        }
        finally
        {
            _triggerGate.Release();
        }
    }

    private async Task RunSyncCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();
            var result = await syncService.SyncAsync(manual: false, cancellationToken);

            if (result.Success || string.IsNullOrEmpty(result.Error))
                _consecutiveFailures = 0;
            else
            {
                _consecutiveFailures++;
                _logger.LogDebug("Sync auto : {Message}", result.Error);
            }
        }
        catch (Exception ex)
        {
            _consecutiveFailures++;
            _logger.LogWarning(ex, "Sync automatique échouée (tentative {N})", _consecutiveFailures);
        }
    }

    private async Task<int> ResolveDelaySecondsAsync(int baseInterval, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SmartBuildingDbContext>();
            var pending = await SyncCoordinator.CountAllUnsyncedAsync(db, cancellationToken);
            if (pending > 0)
                return Math.Min(PendingFastIntervalSeconds, baseInterval);
        }
        catch
        {
            // ignore — garde l'intervalle de base
        }

        return baseInterval;
    }

    public override void Dispose()
    {
        _connectivity.InternetRestored -= OnInternetRestored;
        _triggerGate.Dispose();
        base.Dispose();
    }
}

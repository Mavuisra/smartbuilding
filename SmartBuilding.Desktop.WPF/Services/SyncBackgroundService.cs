using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Infrastructure.Sync;
using SmartBuilding.Shared.Constants;

namespace SmartBuilding.Desktop.WPF.Services;

/// <summary>
/// Synchronisation automatique offline-first : push local puis pull cloud avec retry progressif.
/// </summary>
public class SyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SyncBackgroundService> _logger;
    private int _consecutiveFailures;

    public SyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<SyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = _configuration.GetValue("Sync:IntervalSeconds", SyncConstants.AutoSyncIntervalSeconds);
        var autoSync = _configuration.GetValue("Sync:EnableAutoSync", true);

        if (!autoSync)
        {
            _logger.LogInformation("Sync automatique désactivée (Sync:EnableAutoSync=false).");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = SyncRetryPolicy.GetDelay(interval, _consecutiveFailures);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();
                var result = await syncService.SyncAsync(manual: false, stoppingToken);

                if (result.Success || string.IsNullOrEmpty(result.Error))
                {
                    _consecutiveFailures = 0;
                }
                else
                {
                    _consecutiveFailures++;
                    _logger.LogDebug("Sync auto reportée : {Message}", result.Error);
                }
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;
                _logger.LogWarning(ex, "Sync automatique échouée (tentative {N})", _consecutiveFailures);
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}

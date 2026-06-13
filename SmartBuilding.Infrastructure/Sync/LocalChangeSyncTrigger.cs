using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartBuilding.Application.Interfaces;

namespace SmartBuilding.Infrastructure.Sync;

/// <summary>
/// Planifie l'envoi cloud des modifications locales (debounce) après chaque CRUD.
/// </summary>
public sealed class LocalChangeSyncTrigger : ILocalChangeSyncTrigger, IDisposable
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromSeconds(3);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LocalChangeSyncTrigger> _logger;
    private readonly object _gate = new();
    private Timer? _debounceTimer;
    private int _running;

    public LocalChangeSyncTrigger(
        IServiceScopeFactory scopeFactory,
        ILogger<LocalChangeSyncTrigger> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void RequestPush()
    {
        lock (_gate)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(
                _ => _ = RunPushAsync(),
                null,
                DebounceDelay,
                Timeout.InfiniteTimeSpan);
        }
    }

    private async Task RunPushAsync()
    {
        if (Interlocked.Exchange(ref _running, 1) == 1)
            return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();

            if (await syncService.NeedsInitialCloudPullAsync().ConfigureAwait(false))
                return;

            if (!await syncService.IsOnlineAsync().ConfigureAwait(false))
                return;

            var result = await syncService.PushLocalChangesAsync().ConfigureAwait(false);
            if (result.Success && result.Pushed > 0)
            {
                _logger.LogInformation(
                    "Modifications locales envoyées au cloud : {Count} enregistrement(s).",
                    result.Pushed);
            }
            else if (!result.Success && !string.IsNullOrWhiteSpace(result.Error))
            {
                _logger.LogDebug("Push auto après CRUD : {Message}", result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Push auto après CRUD ignoré.");
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }
    }
}

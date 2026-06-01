namespace SmartBuilding.Infrastructure.Sync;

/// <summary>Délai entre deux sync auto après échecs réseau (backoff).</summary>
public static class SyncRetryPolicy
{
    public static TimeSpan GetDelay(int baseIntervalSeconds, int consecutiveFailures)
    {
        if (consecutiveFailures <= 0)
            return TimeSpan.FromSeconds(Math.Max(15, baseIntervalSeconds));

        var multiplier = Math.Pow(2, Math.Min(consecutiveFailures, 5));
        var seconds = Math.Min(baseIntervalSeconds * multiplier, 900);
        return TimeSpan.FromSeconds(seconds);
    }
}

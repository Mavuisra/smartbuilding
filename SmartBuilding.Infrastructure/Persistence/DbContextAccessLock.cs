namespace SmartBuilding.Infrastructure.Persistence;

/// <summary>
/// Sérialise les accès EF Core côté desktop (DbContext scoped partagé par le shell WPF).
/// </summary>
public static class DbContextAccessLock
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken);
        return new Releaser();
    }

    private sealed class Releaser : IDisposable
    {
        public void Dispose() => Gate.Release();
    }
}

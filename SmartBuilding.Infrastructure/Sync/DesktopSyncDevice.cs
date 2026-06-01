namespace SmartBuilding.Infrastructure.Sync;

/// <summary>Identifiant stable du poste pour la synchronisation multi-clients (offline first).</summary>
public static class DesktopSyncDevice
{
    private static readonly object Lock = new();
    private static Guid? _cachedId;

    private static string DeviceIdPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SBMS",
            "device-id.txt");

    public static Guid GetOrCreateDeviceId()
    {
        lock (Lock)
        {
            if (_cachedId is { } cached)
                return cached;

            var dir = Path.GetDirectoryName(DeviceIdPath)!;
            Directory.CreateDirectory(dir);

            if (File.Exists(DeviceIdPath)
                && Guid.TryParse(File.ReadAllText(DeviceIdPath).Trim(), out var existing))
            {
                _cachedId = existing;
                return existing;
            }

            var id = Guid.NewGuid();
            File.WriteAllText(DeviceIdPath, id.ToString("D"));
            _cachedId = id;
            return id;
        }
    }

    public static string GetDeviceLabel() =>
        $"Poste-{GetOrCreateDeviceId().ToString()[..8].ToUpperInvariant()}";
}

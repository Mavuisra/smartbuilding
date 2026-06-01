namespace SmartBuilding.Infrastructure.Persistence;

/// <summary>Dernière IP MySQL serveur joignable (poste client).</summary>
public static class DesktopClientHostCache
{
    private static string CachePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SBMS",
        "mysql-server-host.txt");

    public static string? Read()
    {
        try
        {
            if (!File.Exists(CachePath))
                return null;

            var host = File.ReadAllText(CachePath).Trim();
            return string.IsNullOrWhiteSpace(host) ? null : host;
        }
        catch
        {
            return null;
        }
    }

    public static void Write(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return;

        var folder = Path.GetDirectoryName(CachePath)!;
        Directory.CreateDirectory(folder);
        File.WriteAllText(CachePath, host.Trim());
    }
}

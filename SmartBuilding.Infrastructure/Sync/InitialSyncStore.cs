namespace SmartBuilding.Infrastructure.Sync;

/// <summary>
/// Indique qu'une synchronisation initiale complète (cloud → local) a déjà été effectuée sur ce poste.
/// </summary>
public static class InitialSyncStore
{
    private static readonly string FlagPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SBMS",
        "initial-cloud-pull-completed.flag");

    public static bool IsCompleted()
    {
        try
        {
            return File.Exists(FlagPath);
        }
        catch
        {
            return false;
        }
    }

    public static void MarkCompleted()
    {
        try
        {
            var folder = Path.GetDirectoryName(FlagPath);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);

            File.WriteAllText(FlagPath, DateTime.UtcNow.ToString("O"));
        }
        catch
        {
            // ignore
        }
    }

    public static void Clear()
    {
        try
        {
            if (File.Exists(FlagPath))
                File.Delete(FlagPath);
        }
        catch
        {
            // ignore
        }
    }
}

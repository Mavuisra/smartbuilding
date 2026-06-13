using System.Text.Json;
using SmartBuilding.Shared.DTOs.Sync;

namespace SmartBuilding.Infrastructure.Sync;

/// <summary>Conserve les conflits du dernier téléchargement cloud → local pour la page Synchronisation.</summary>
public static class SyncPullConflictStore
{
    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SBMS",
        "last-cloud-pull-conflicts.json");

    public static IReadOnlyList<SyncConflictDetail> Load()
    {
        try
        {
            if (!File.Exists(StorePath))
                return [];

            var json = File.ReadAllText(StorePath);
            return JsonSerializer.Deserialize<List<SyncConflictDetail>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void Save(IReadOnlyList<SyncConflictDetail> conflicts)
    {
        try
        {
            var folder = Path.GetDirectoryName(StorePath);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);

            File.WriteAllText(StorePath, JsonSerializer.Serialize(conflicts));
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
            if (File.Exists(StorePath))
                File.Delete(StorePath);
        }
        catch
        {
            // ignore
        }
    }
}

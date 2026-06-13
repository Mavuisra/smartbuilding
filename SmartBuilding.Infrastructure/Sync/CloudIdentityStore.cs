using System.Text.Json;

namespace SmartBuilding.Infrastructure.Sync;

/// <summary>
/// Mémorise qu'un utilisateur a déjà été lié au cloud (une seule fois au premier login).
/// </summary>
public static class CloudIdentityStore
{
    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SBMS",
        "cloud-identity.json");

    public sealed record LinkedState(string Username, string Message, DateTime LinkedAtUtc);

    public static bool TryGetForUser(string username, out LinkedState? state)
    {
        state = null;
        if (string.IsNullOrWhiteSpace(username))
            return false;

        var current = Load();
        if (current is null)
            return false;

        if (!string.Equals(current.Username, username.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        state = current;
        return true;
    }

    public static bool IsAlreadyLinkedForUser(string username) =>
        TryGetForUser(username, out _) && !string.IsNullOrWhiteSpace(SyncCloudTokenStore.Load());

    public static void MarkLinked(string username, string message)
    {
        if (string.IsNullOrWhiteSpace(username))
            return;

        Save(new LinkedState(
            username.Trim(),
            string.IsNullOrWhiteSpace(message) ? "Compte lié au cloud." : message.Trim(),
            DateTime.UtcNow));
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

    private static LinkedState? Load()
    {
        try
        {
            if (!File.Exists(StorePath))
                return null;

            var json = File.ReadAllText(StorePath);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            return JsonSerializer.Deserialize<LinkedState>(json);
        }
        catch
        {
            return null;
        }
    }

    private static void Save(LinkedState state)
    {
        try
        {
            var folder = Path.GetDirectoryName(StorePath);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);

            File.WriteAllText(StorePath, JsonSerializer.Serialize(state));
        }
        catch
        {
            // ignore
        }
    }
}

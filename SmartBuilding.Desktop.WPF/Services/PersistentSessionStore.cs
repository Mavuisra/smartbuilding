using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartBuilding.Desktop.WPF.Services;

/// <summary>
/// Session locale chiffrée (DPAPI) — évite de resaisir identifiant/mot de passe à chaque ouverture.
/// </summary>
public sealed class PersistentSessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SBMS",
        "auth-session.json");

    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(30);

    public sealed class StoredSession
    {
        public string Username { get; set; } = string.Empty;
        public Guid OrganizationId { get; set; }
        public string ProtectedPassword { get; set; } = string.Empty;
        public string SessionToken { get; set; } = string.Empty;
        public DateTime IssuedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }

        public bool IsValid(DateTime? utcNow = null)
        {
            var now = utcNow ?? DateTime.UtcNow;
            return !string.IsNullOrWhiteSpace(Username)
                   && !string.IsNullOrWhiteSpace(ProtectedPassword)
                   && ExpiresAtUtc > now;
        }
    }

    public void Save(string username, Guid organizationId, string password, TimeSpan? lifetime = null)
    {
        var trimmedUser = (username ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmedUser) || string.IsNullOrWhiteSpace(password))
            return;

        var now = DateTime.UtcNow;
        var session = new StoredSession
        {
            Username = trimmedUser,
            OrganizationId = organizationId,
            ProtectedPassword = Protect(password),
            SessionToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            IssuedAtUtc = now,
            ExpiresAtUtc = now.Add(lifetime ?? DefaultLifetime),
        };

        var folder = Path.GetDirectoryName(StorePath);
        if (!string.IsNullOrWhiteSpace(folder))
            Directory.CreateDirectory(folder);

        File.WriteAllText(StorePath, JsonSerializer.Serialize(session, JsonOptions));
    }

    public bool TryLoad(out StoredSession session)
    {
        session = new StoredSession();
        if (!File.Exists(StorePath))
            return false;

        try
        {
            var loaded = JsonSerializer.Deserialize<StoredSession>(File.ReadAllText(StorePath), JsonOptions);
            if (loaded is null || !loaded.IsValid())
            {
                Clear();
                return false;
            }

            session = loaded;
            return true;
        }
        catch
        {
            Clear();
            return false;
        }
    }

    public string? UnprotectPassword(StoredSession session)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(session.ProtectedPassword))
                return null;

            var data = Convert.FromBase64String(session.ProtectedPassword);
            var plain = ProtectedData.Unprotect(data, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return null;
        }
    }

    public void RefreshExpiry(StoredSession session, TimeSpan? lifetime = null)
    {
        if (!session.IsValid())
            return;

        session.ExpiresAtUtc = DateTime.UtcNow.Add(lifetime ?? DefaultLifetime);
        var folder = Path.GetDirectoryName(StorePath);
        if (!string.IsNullOrWhiteSpace(folder))
            Directory.CreateDirectory(folder);
        File.WriteAllText(StorePath, JsonSerializer.Serialize(session, JsonOptions));
    }

    public void Clear()
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

    private static string Protect(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var protectedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }
}

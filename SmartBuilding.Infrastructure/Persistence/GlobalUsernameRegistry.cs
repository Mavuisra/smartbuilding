using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace SmartBuilding.Infrastructure.Persistence;

public sealed class GlobalUsernameRegistryFile
{
    public Dictionary<string, Guid> Usernames { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Index global username → organisation (tous tenants).
/// Garantit l'unicité des identifiants et permet la résolution automatique au login.
/// </summary>
public sealed class GlobalUsernameRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly string IndexPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SBMS",
        "username-index.json");

    private readonly GlobalUsernameRegistryFile _file;
    private readonly string _indexPath;
    private readonly object _sync = new();

    private GlobalUsernameRegistry(GlobalUsernameRegistryFile file, string indexPath)
    {
        _file = file;
        _indexPath = indexPath;
    }

    public static GlobalUsernameRegistry Load(string? indexPath = null) =>
        new(LoadFile(indexPath ?? IndexPath), indexPath ?? IndexPath);

    public bool IsTaken(string username, Guid? exceptOrganizationId = null)
    {
        var key = Normalize(username);
        if (string.IsNullOrEmpty(key))
            return false;

        lock (_sync)
        {
            if (!_file.Usernames.TryGetValue(key, out var orgId))
                return false;
            return exceptOrganizationId is null || orgId != exceptOrganizationId.Value;
        }
    }

    public Guid? TryResolveOrganization(string username)
    {
        var key = Normalize(username);
        if (string.IsNullOrEmpty(key))
            return null;

        lock (_sync)
        {
            return _file.Usernames.TryGetValue(key, out var orgId) ? orgId : null;
        }
    }

    public void Register(string username, Guid organizationId)
    {
        var key = Normalize(username);
        if (string.IsNullOrEmpty(key))
            return;

        lock (_sync)
        {
            _file.Usernames[key] = organizationId;
            SaveUnlocked();
        }
    }

    public void Unregister(string username)
    {
        var key = Normalize(username);
        if (string.IsNullOrEmpty(key))
            return;

        lock (_sync)
        {
            _file.Usernames.Remove(key);
            SaveUnlocked();
        }
    }

    public async Task<bool> IsUsernameTakenGloballyAsync(
        string username,
        OrganizationRegistry organizationRegistry,
        OrganizationConnectionResolver connectionResolver,
        Guid? exceptOrganizationId = null,
        CancellationToken cancellationToken = default)
    {
        var key = Normalize(username);
        if (string.IsNullOrEmpty(key))
            return false;

        if (IsTaken(key, exceptOrganizationId))
            return true;

        var matches = await FindOrganizationsWithUsernameAsync(
            key,
            organizationRegistry,
            connectionResolver,
            cancellationToken);

        foreach (var org in matches)
        {
            if (exceptOrganizationId is null || org.Id != exceptOrganizationId.Value)
            {
                Register(key, org.Id);
                return true;
            }
        }

        return false;
    }

    public async Task<IReadOnlyList<OrganizationEntry>> FindOrganizationsWithUsernameAsync(
        string username,
        OrganizationRegistry organizationRegistry,
        OrganizationConnectionResolver connectionResolver,
        CancellationToken cancellationToken = default)
    {
        var key = Normalize(username);
        if (string.IsNullOrEmpty(key))
            return [];

        var matches = new List<OrganizationEntry>();
        foreach (var org in organizationRegistry.Organizations)
        {
            if (!await UsernameExistsInOrganizationAsync(
                    key, org, connectionResolver, cancellationToken))
                continue;

            matches.Add(org);
        }

        if (matches.Count == 1)
            Register(key, matches[0].Id);

        return matches;
    }

    public void RebuildFromOrganizations(
        OrganizationRegistry organizationRegistry,
        OrganizationConnectionResolver connectionResolver)
    {
        var rebuilt = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var org in organizationRegistry.Organizations)
        {
            try
            {
                foreach (var username in ListUsernamesInOrganization(org, connectionResolver))
                    rebuilt[username] = org.Id;
            }
            catch
            {
                // Base inaccessible — ignorée pour l'index.
            }
        }

        lock (_sync)
        {
            _file.Usernames = rebuilt;
            SaveUnlocked();
        }
    }

    private static IEnumerable<string> ListUsernamesInOrganization(
        OrganizationEntry org,
        OrganizationConnectionResolver connectionResolver)
    {
        using var db = CreateDbContext(connectionResolver.BuildConnectionString(org.Id));
        return db.Users
            .AsNoTracking()
            .Where(u => u.IsActive && u.DeletedAt == null)
            .Select(u => u.Username)
            .AsEnumerable()
            .Select(Normalize)
            .Where(u => !string.IsNullOrEmpty(u))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<bool> UsernameExistsInOrganizationAsync(
        string normalizedUsername,
        OrganizationEntry org,
        OrganizationConnectionResolver connectionResolver,
        CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = connectionResolver.BuildConnectionString(org.Id);
            await using var db = CreateDbContext(connectionString);
            if (!await db.Database.CanConnectAsync(cancellationToken))
                return false;

            var names = await db.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(u => u.IsActive && u.DeletedAt == null)
                .Select(u => u.Username)
                .ToListAsync(cancellationToken);

            return names.Any(
                n => string.Equals(n.Trim(), normalizedUsername, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static SmartBuildingDbContext CreateDbContext(string connectionString)
    {
        var serverVersion = ServerVersion.Parse("8.0.36-mysql");
        var options = new DbContextOptionsBuilder<SmartBuildingDbContext>()
            .UseMySql(connectionString, serverVersion, mySql => mySql.EnableStringComparisonTranslations())
            .Options;
        return new SmartBuildingDbContext(options);
    }

    private static GlobalUsernameRegistryFile LoadFile(string indexPath)
    {
        if (!File.Exists(indexPath))
            return new GlobalUsernameRegistryFile();

        try
        {
            var json = File.ReadAllText(indexPath);
            return JsonSerializer.Deserialize<GlobalUsernameRegistryFile>(json, JsonOptions)
                   ?? new GlobalUsernameRegistryFile();
        }
        catch
        {
            return new GlobalUsernameRegistryFile();
        }
    }

    private void SaveUnlocked()
    {
        var folder = Path.GetDirectoryName(_indexPath);
        if (!string.IsNullOrWhiteSpace(folder))
            Directory.CreateDirectory(folder);
        File.WriteAllText(_indexPath, JsonSerializer.Serialize(_file, JsonOptions));
    }

    private static string Normalize(string username) =>
        (username ?? "").Trim().ToLowerInvariant();
}

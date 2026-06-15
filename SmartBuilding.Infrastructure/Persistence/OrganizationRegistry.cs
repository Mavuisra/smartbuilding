using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace SmartBuilding.Infrastructure.Persistence;

/// <summary>Registre local des organisations (tenants) — une base MySQL par entrée.</summary>
public sealed class OrganizationEntry
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string DatabaseName { get; set; } = "";
    public string City { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool SyncedToCloud { get; set; }
}

public sealed class OrganizationRegistryFile
{
    public Guid? ActiveOrganizationId { get; set; }
    public List<OrganizationEntry> Organizations { get; set; } = [];
}

public sealed class OrganizationRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly string RegistryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SBMS",
        "organizations.json");

    private readonly OrganizationRegistryFile _file;

    public IReadOnlyList<OrganizationEntry> Organizations => _file.Organizations;

    public OrganizationEntry? Active =>
        _file.Organizations.FirstOrDefault(o => o.Id == _file.ActiveOrganizationId)
        ?? _file.Organizations.FirstOrDefault();

    public Guid? ActiveOrganizationId => Active?.Id;

    private OrganizationRegistry(OrganizationRegistryFile file) => _file = file;

    public static OrganizationRegistry Load(IConfiguration configuration)
    {
        OrganizationRegistryFile data;
        if (File.Exists(RegistryPath))
        {
            try
            {
                var json = File.ReadAllText(RegistryPath);
                data = JsonSerializer.Deserialize<OrganizationRegistryFile>(json, JsonOptions)
                       ?? new OrganizationRegistryFile();
            }
            catch
            {
                data = new OrganizationRegistryFile();
            }
        }
        else
        {
            data = new OrganizationRegistryFile();
        }

        if (data.Organizations.Count == 0)
            BootstrapLegacyOrganization(configuration, data);

        if (data.ActiveOrganizationId is null && data.Organizations.Count > 0)
            data.ActiveOrganizationId = data.Organizations[0].Id;

        var registry = new OrganizationRegistry(data);
        registry.Save();
        return registry;
    }

    private static void BootstrapLegacyOrganization(IConfiguration configuration, OrganizationRegistryFile data)
    {
        var section = configuration.GetSection(DesktopLocalDatabaseConfig.SectionName);
        var dbName = section.GetValue<string>("Database") ?? DesktopMySqlConnectionBuilder.DefaultDatabase;
        var entry = new OrganizationEntry
        {
            Id = Guid.NewGuid(),
            Name = "Organisation principale",
            Slug = "organisation-principale",
            DatabaseName = dbName,
            City = "",
            CreatedAt = DateTime.UtcNow,
            SyncedToCloud = false,
        };
        data.Organizations.Add(entry);
        data.ActiveOrganizationId = entry.Id;
    }

    public void SetActive(Guid organizationId)
    {
        if (_file.Organizations.All(o => o.Id != organizationId))
            throw new InvalidOperationException("Organisation introuvable.");
        _file.ActiveOrganizationId = organizationId;
        Save();
    }

    public OrganizationEntry Add(OrganizationEntry entry)
    {
        if (_file.Organizations.Any(o => o.Slug.Equals(entry.Slug, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Une organisation avec cet identifiant existe déjà.");
        _file.Organizations.Add(entry);
        _file.ActiveOrganizationId = entry.Id;
        Save();
        return entry;
    }

    public void MarkSynced(Guid organizationId)
    {
        var org = _file.Organizations.FirstOrDefault(o => o.Id == organizationId);
        if (org is null)
            return;
        org.SyncedToCloud = true;
        Save();
    }

    public void Save()
    {
        var folder = Path.GetDirectoryName(RegistryPath);
        if (!string.IsNullOrWhiteSpace(folder))
            Directory.CreateDirectory(folder);
        File.WriteAllText(RegistryPath, JsonSerializer.Serialize(_file, JsonOptions));
    }

    public static string Slugify(string name)
    {
        var slug = name.Trim().ToLowerInvariant();
        foreach (var c in Path.GetInvalidFileNameChars())
            slug = slug.Replace(c, '-');
        slug = string.Join('-', slug.Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries));
        if (slug.Length > 48)
            slug = slug[..48].TrimEnd('-');
        return string.IsNullOrWhiteSpace(slug) ? "tenant" : slug;
    }

    public static string DatabaseNameForSlug(string slug)
    {
        var db = $"sbms_{slug.Replace('-', '_')}";
        return db.Length > 64 ? db[..64] : db;
    }
}

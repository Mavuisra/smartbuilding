using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartBuilding.Domain.Entities.Auth;
using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Infrastructure.Services;

namespace SmartBuilding.Infrastructure.Persistence;

public sealed record CreateOrganizationRequest(
    string Name,
    string City,
    string AdminUsername,
    string AdminPassword,
    string? AdminFullName = null);

public sealed record CreateOrganizationResult(
    bool Success,
    OrganizationEntry? Organization,
    string Message);

/// <summary>Crée une organisation (tenant) avec base MySQL dédiée et compte admin initial.</summary>
public sealed class OrganizationProvisioningService
{
    private readonly OrganizationRegistry _registry;
    private readonly OrganizationConnectionResolver _connectionResolver;
    private readonly DesktopLocalDatabaseConfig _localDb;
    private readonly ILogger<OrganizationProvisioningService>? _logger;

    public OrganizationProvisioningService(
        OrganizationRegistry registry,
        OrganizationConnectionResolver connectionResolver,
        DesktopLocalDatabaseConfig localDb,
        ILogger<OrganizationProvisioningService>? logger = null)
    {
        _registry = registry;
        _connectionResolver = connectionResolver;
        _localDb = localDb;
        _logger = logger;
    }

    public async Task<CreateOrganizationResult> CreateOrganizationAsync(
        CreateOrganizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = (request.Name ?? "").Trim();
        if (name.Length < 2)
            return new CreateOrganizationResult(false, null, "Le nom du tenant doit contenir au moins 2 caractères.");

        var username = (request.AdminUsername ?? "").Trim();
        if (string.IsNullOrWhiteSpace(username))
            return new CreateOrganizationResult(false, null, "L'identifiant administrateur est obligatoire.");

        if ((request.AdminPassword ?? "").Length < 6)
            return new CreateOrganizationResult(false, null, "Le mot de passe doit contenir au moins 6 caractères.");

        var slug = OrganizationRegistry.Slugify(name);
        var suffix = 1;
        while (_registry.Organizations.Any(o => o.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase)))
        {
            slug = $"{OrganizationRegistry.Slugify(name)}-{suffix++}";
        }

        var databaseName = OrganizationRegistry.DatabaseNameForSlug(slug);
        var connectionString = BuildConnectionString(databaseName);

        try
        {
            DesktopLocalDatabaseBootstrap.EnsureMySqlDatabaseExists(connectionString);
        }
        catch (Exception ex)
        {
            return new CreateOrganizationResult(
                false,
                null,
                $"Impossible de créer la base MySQL « {databaseName} » : {ex.Message}");
        }

        var entry = new OrganizationEntry
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug,
            DatabaseName = databaseName,
            City = (request.City ?? "").Trim(),
            CreatedAt = DateTime.UtcNow,
            SyncedToCloud = false,
        };

        var tenantLocalDb = new DesktopLocalDatabaseConfig
        {
            Provider = DesktopLocalDatabaseProvider.MySql,
            ConnectionString = connectionString,
            DisplayLabel = $"Tenant — {name}",
            DeploymentMode = _localDb.DeploymentMode,
            ServerHost = _localDb.ServerHost,
            RunsSchemaMigrations = _localDb.RunsSchemaMigrations,
            RequiresClientDatabaseConnection = false,
        };

        try
        {
            await using var db = CreateDbContext(connectionString);
            if (tenantLocalDb.RunsSchemaMigrations)
                await DesktopDatabaseInitializer.InitializeAsync(db, tenantLocalDb, _logger, cancellationToken);
            else if (!await db.Database.CanConnectAsync(cancellationToken))
                return new CreateOrganizationResult(false, null, "Connexion à la nouvelle base impossible.");

            await DatabaseSeeder.SeedReferenceDataAsync(db);

            var admin = new User
            {
                Username = username,
                FullName = string.IsNullOrWhiteSpace(request.AdminFullName) ? name : request.AdminFullName.Trim(),
                Email = $"{username}@local.sbms",
                Role = UserRole.Administrateur,
                PasswordHash = AuthService.HashPassword(request.AdminPassword),
                IsActive = true,
                IsSynced = false,
            };
            db.Users.Add(admin);

            db.BuildingInfos.Add(new BuildingInfo
            {
                Name = name,
                BuildingDisplayName = name,
                City = entry.City,
                Country = "RDC",
                TimeZoneId = "Africa/Kinshasa",
                Currency = "USD",
                DateFormat = "dd/MM/yyyy",
                Language = "Français",
                TimeFormat = "24 heures",
            });

            await db.SaveChangesAsync(cancellationToken);
            await DatabaseSeeder.EnsureReservedAdminAccountsAsync(db, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Échec provisionnement tenant {Name}", name);
            return new CreateOrganizationResult(
                false,
                null,
                DbSaveExceptionTranslator.ToUserMessage(ex));
        }

        _registry.Add(entry);
        _registry.SetActive(entry.Id);

        return new CreateOrganizationResult(true, entry, "Tenant créé avec succès.");
    }

    private string BuildConnectionString(string databaseName)
    {
        var baseCs = _connectionResolver.BuildConnectionString();
        var builder = new MySqlConnector.MySqlConnectionStringBuilder(baseCs) { Database = databaseName };
        return builder.ConnectionString;
    }

    private static SmartBuildingDbContext CreateDbContext(string connectionString)
    {
        var serverVersion = ServerVersion.Parse("8.0.36-mysql");
        var options = new DbContextOptionsBuilder<SmartBuildingDbContext>()
            .UseMySql(connectionString, serverVersion, mySql => mySql.EnableStringComparisonTranslations())
            .Options;
        return new SmartBuildingDbContext(options);
    }
}

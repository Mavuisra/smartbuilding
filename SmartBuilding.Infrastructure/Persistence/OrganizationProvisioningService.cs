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
    private readonly GlobalUsernameRegistry _usernameRegistry;
    private readonly DesktopLocalDatabaseConfig _localDb;
    private readonly ILogger<OrganizationProvisioningService>? _logger;

    public OrganizationProvisioningService(
        OrganizationRegistry registry,
        OrganizationConnectionResolver connectionResolver,
        GlobalUsernameRegistry usernameRegistry,
        DesktopLocalDatabaseConfig localDb,
        ILogger<OrganizationProvisioningService>? logger = null)
    {
        _registry = registry;
        _connectionResolver = connectionResolver;
        _usernameRegistry = usernameRegistry;
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

        if (await _usernameRegistry.IsUsernameTakenGloballyAsync(
                username,
                _registry,
                _connectionResolver,
                cancellationToken: cancellationToken))
        {
            return new CreateOrganizationResult(
                false,
                null,
                $"L'identifiant « {username} » est déjà utilisé dans une autre organisation.\n\n" +
                "Choisissez un autre nom d'utilisateur (par ex. admin.blooom, jean.dupont).");
        }

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
            if (IsRegisteredTenantDatabase(databaseName))
            {
                return new CreateOrganizationResult(
                    false,
                    null,
                    $"Une organisation utilise déjà la base « {databaseName} ».\n\nChoisissez un autre nom de tenant.");
            }

            // Base laissée par une création interrompue → supprimer pour éviter « table already exists ».
            if (DesktopLocalDatabaseBootstrap.MySqlDatabaseExists(connectionString))
                DesktopLocalDatabaseBootstrap.DropMySqlDatabaseIfExists(connectionString);

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
            CompanyProfileCompleted = false,
        };

        // Toujours migrer le schéma sur une base tenant neuve (même en mode client LAN).
        var tenantLocalDb = new DesktopLocalDatabaseConfig
        {
            Provider = DesktopLocalDatabaseProvider.MySql,
            ConnectionString = connectionString,
            DisplayLabel = $"Tenant — {name}",
            DeploymentMode = _localDb.DeploymentMode,
            ServerHost = _localDb.ServerHost,
            RunsSchemaMigrations = true,
            RequiresClientDatabaseConnection = false,
        };

        try
        {
            await using var db = CreateDbContext(connectionString);
            await DesktopDatabaseInitializer.InitializeAsync(db, tenantLocalDb, _logger, cancellationToken);

            await DatabaseSeeder.SeedReferenceDataAsync(db);

            var admin = new User
            {
                Username = username,
                FullName = string.IsNullOrWhiteSpace(request.AdminFullName) ? name : request.AdminFullName.Trim(),
                Email = $"{username}@local.sbms",
                Role = UserRole.Administrateur,
                PasswordHash = AuthService.HashPassword(request.AdminPassword!),
                IsActive = true,
                IsSynced = false,
            };
            db.Users.Add(admin);

            var building = await db.BuildingInfos.FirstOrDefaultAsync(cancellationToken);
            if (building is null)
            {
                building = new BuildingInfo();
                db.BuildingInfos.Add(building);
            }

            building.Name = name;
            building.BuildingDisplayName = name;
            building.City = entry.City;
            building.Country = "RDC";
            building.TimeZoneId = "Africa/Kinshasa";
            building.Currency = "USD";
            building.DateFormat = "dd/MM/yyyy";
            building.Language = "Français";
            building.TimeFormat = "24 heures";
            building.MarkUpdated();

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
        _usernameRegistry.Register(username, entry.Id);

        return new CreateOrganizationResult(true, entry, "Tenant créé avec succès.");
    }

    private bool IsRegisteredTenantDatabase(string databaseName) =>
        _registry.Organizations.Any(
            o => o.DatabaseName.Equals(databaseName, StringComparison.OrdinalIgnoreCase));

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

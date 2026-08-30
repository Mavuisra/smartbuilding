using Microsoft.Extensions.Configuration;

namespace SmartBuilding.Infrastructure.Persistence;

/// <summary>Résout la chaîne MySQL de l'organisation (tenant) active.</summary>
public sealed class OrganizationConnectionResolver
{
    private readonly OrganizationRegistry _registry;
    private readonly IConfiguration _configuration;
    private readonly DesktopLocalDatabaseConfig _localDb;

    public OrganizationConnectionResolver(
        OrganizationRegistry registry,
        IConfiguration configuration,
        DesktopLocalDatabaseConfig localDb)
    {
        _registry = registry;
        _configuration = configuration;
        _localDb = localDb;
    }

    public string ConnectionString => BuildConnectionString();

    public Guid? ActiveOrganizationId => _registry.ActiveOrganizationId;

    public OrganizationEntry? ActiveOrganization => _registry.Active;

    public string BuildConnectionString(Guid? organizationId = null)
    {
        var section = _configuration.GetSection(DesktopLocalDatabaseConfig.SectionName);
        var host = _localDb.ServerHost ?? "127.0.0.1";
        if (_localDb.DeploymentMode == DesktopDatabaseDeploymentMode.Server
            || _localDb.DeploymentMode == DesktopDatabaseDeploymentMode.Standalone)
        {
            host = "127.0.0.1";
        }

        var org = organizationId is null
            ? _registry.Active
            : _registry.Organizations.FirstOrDefault(o => o.Id == organizationId);

        var database = org?.DatabaseName
                       ?? section.GetValue<string>("Database")
                       ?? DesktopMySqlConnectionBuilder.DefaultDatabase;

        return DesktopMySqlConnectionBuilder.Build(section, host, database);
    }
}

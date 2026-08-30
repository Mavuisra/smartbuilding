using Microsoft.Extensions.Configuration;
using SmartBuilding.Infrastructure.Persistence;
using Xunit;

namespace SmartBuilding.Tests.Infrastructure;

public sealed class OrganizationProvisioningTests
{
    private static IConfiguration BuildConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LocalDatabase:DeploymentMode"] = "Server",
                ["LocalDatabase:Database"] = "sbms_local",
                ["LocalDatabase:MySqlPort"] = "3306",
                ["LocalDatabase:User"] = "root",
                ["LocalDatabase:Password"] = "",
            })
            .Build();

    private static bool CanReachMySql()
    {
        try
        {
            var config = BuildConfig();
            var cs = DesktopMySqlConnectionBuilder.Build(config.GetSection("LocalDatabase"), "127.0.0.1", "sbms_local");
            return DesktopLocalDatabaseBootstrap.CanConnectToMySql(cs);
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task CreateOrganizationAsync_creates_tenant_when_mysql_available()
    {
        if (!CanReachMySql())
            return;

        var config = BuildConfig();
        var registry = OrganizationRegistry.Load(config);
        var localDb = DesktopLocalDatabaseBootstrap.Resolve(config);
        var resolver = new OrganizationConnectionResolver(registry, config, localDb);
        var usernameRegistry = GlobalUsernameRegistry.Load(
            Path.Combine(Path.GetTempPath(), $"sbms-provision-test-{Guid.NewGuid():N}.json"));
        var provisioning = new OrganizationProvisioningService(
            registry, resolver, usernameRegistry, localDb);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var tenantName = $"Test Tenant {suffix}";
        var adminUser = $"admin.test.{suffix}";

        var result = await provisioning.CreateOrganizationAsync(
            new CreateOrganizationRequest(tenantName, "", adminUser, "Test@2026"));

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Organization);
        Assert.False(result.Organization!.CompanyProfileCompleted);
        Assert.Contains(registry.Organizations, o => o.Id == result.Organization.Id);
        Assert.Equal(result.Organization.Id, usernameRegistry.TryResolveOrganization(adminUser));
    }
}

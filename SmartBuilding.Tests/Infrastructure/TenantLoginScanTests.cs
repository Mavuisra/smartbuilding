using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartBuilding.Infrastructure.Persistence;
using Xunit;

namespace SmartBuilding.Tests.Infrastructure;

/// <summary>Test d'intégration MySQL local (XAMPP) — scan multi-tenant.</summary>
public class TenantLoginScanTests
{
    private static bool CanReachMySql()
    {
        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["LocalDatabase:Database"] = "sbms_local",
                    ["LocalDatabase:DeploymentMode"] = "Server",
                })
                .Build();
            var cs = DesktopMySqlConnectionBuilder.Build(config.GetSection("LocalDatabase"), "127.0.0.1", "sbms_local");
            return DesktopLocalDatabaseBootstrap.CanConnectToMySql(cs);
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task FindOrganizations_finds_adminbloomer_in_bloomer_tenant()
    {
        if (!CanReachMySql())
            return;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LocalDatabase:Database"] = "sbms_local",
                ["LocalDatabase:DeploymentMode"] = "Server",
            })
            .Build();

        var registry = OrganizationRegistry.Load(config);
        var localDb = DesktopLocalDatabaseBootstrap.Resolve(config);
        var resolver = new OrganizationConnectionResolver(registry, config, localDb);
        var usernameRegistry = GlobalUsernameRegistry.Load(
            Path.Combine(Path.GetTempPath(), $"sbms-scan-test-{Guid.NewGuid():N}.json"));

        var matches = await usernameRegistry.FindOrganizationsWithUsernameAsync(
            "adminbloomer", registry, resolver);

        Assert.Contains(matches, o => o.DatabaseName == "sbms_bloomer");
    }

    [Fact]
    public async Task FindOrganizations_finds_admin_in_multiple_tenants()
    {
        if (!CanReachMySql())
            return;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LocalDatabase:Database"] = "sbms_local",
                ["LocalDatabase:DeploymentMode"] = "Server",
            })
            .Build();

        var registry = OrganizationRegistry.Load(config);
        var localDb = DesktopLocalDatabaseBootstrap.Resolve(config);
        var resolver = new OrganizationConnectionResolver(registry, config, localDb);
        var usernameRegistry = GlobalUsernameRegistry.Load(
            Path.Combine(Path.GetTempPath(), $"sbms-scan-test-{Guid.NewGuid():N}.json"));

        var matches = await usernameRegistry.FindOrganizationsWithUsernameAsync(
            "admin", registry, resolver);

        Assert.Contains(matches, o => o.DatabaseName == "sbms_local");
        Assert.Contains(matches, o => o.DatabaseName == "sbms_blooom");
    }
}

using Microsoft.Extensions.Configuration;
using SmartBuilding.Infrastructure.Persistence;
using Xunit;

namespace SmartBuilding.Tests.Infrastructure;

public class OrganizationRegistryTests
{
    [Fact]
    public void Slugify_normalizes_name()
    {
        var slug = OrganizationRegistry.Slugify("Résidence Kinshasa Centre");
        Assert.Contains("kinshasa", slug);
        Assert.DoesNotContain(" ", slug);
    }

    [Fact]
    public void DatabaseNameForSlug_prefixes_sbms()
    {
        var db = OrganizationRegistry.DatabaseNameForSlug("gombe");
        Assert.StartsWith("sbms_", db);
    }

    [Fact]
    public void Load_bootstraps_legacy_organization_when_empty()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LocalDatabase:Database"] = "sbms_local",
                ["LocalDatabase:DeploymentMode"] = "Server",
            })
            .Build();

        var registry = OrganizationRegistry.Load(config);
        Assert.NotEmpty(registry.Organizations);
        Assert.NotNull(registry.Active);
        Assert.Equal("sbms_local", registry.Active!.DatabaseName);
    }
}

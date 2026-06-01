using Microsoft.Extensions.Configuration;
using Xunit;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Sync;

namespace SmartBuilding.Tests.Infrastructure;

public class OfflineFirstTests
{
    [Fact]
    public void SyncRetryPolicy_Increases_Delay_On_Failures()
    {
        var first = SyncRetryPolicy.GetDelay(60, 0);
        var third = SyncRetryPolicy.GetDelay(60, 3);
        Assert.True(third > first);
        Assert.True(third.TotalSeconds <= 900);
    }

    [Fact]
    public void Build_Client_ConnectionString_Uses_ServerHost()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LocalDatabase:DeploymentMode"] = "Client",
                ["LocalDatabase:ServerHost"] = "192.168.1.50",
                ["LocalDatabase:User"] = "sbms",
                ["LocalDatabase:Password"] = "secret"
            })
            .Build();

        var cs = DesktopMySqlConnectionBuilder.Build(
            config.GetSection(DesktopLocalDatabaseConfig.SectionName),
            "192.168.1.50");

        Assert.Contains("Server=192.168.1.50", cs);
        Assert.Contains("User=sbms", cs);
    }

    [Fact]
    public void Resolve_Sqlite_When_Provider_Is_Sqlite()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LocalDatabase:DeploymentMode"] = "Standalone",
                ["LocalDatabase:Provider"] = "Sqlite"
            })
            .Build();

        var resolved = DesktopLocalDatabaseBootstrap.Resolve(config);
        Assert.True(resolved.IsSqlite);
        Assert.Contains("Data Source=", resolved.ConnectionString);
    }

    [Fact]
    public void DesktopSyncDevice_Returns_Stable_Id()
    {
        var a = DesktopSyncDevice.GetOrCreateDeviceId();
        var b = DesktopSyncDevice.GetOrCreateDeviceId();
        Assert.Equal(a, b);
    }
}

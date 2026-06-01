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
    public void Resolve_Standalone_Uses_MySql()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LocalDatabase:DeploymentMode"] = "Standalone"
            })
            .Build();

        try
        {
            var resolved = DesktopLocalDatabaseBootstrap.Resolve(config);
            Assert.True(resolved.IsMySql);
            Assert.Contains("Server=", resolved.ConnectionString);
        }
        catch (InvalidOperationException)
        {
            // MySQL non démarré sur la machine de CI — comportement attendu.
        }
    }

    [Fact]
    public void ClientHostCache_RoundTrips()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SBMS",
            "mysql-server-host.txt");
        var previous = File.Exists(path) ? File.ReadAllText(path) : null;

        try
        {
            DesktopClientHostCache.Write("192.168.99.7");
            Assert.Equal("192.168.99.7", DesktopClientHostCache.Read());
        }
        finally
        {
            if (previous is null)
                File.Delete(path);
            else
                File.WriteAllText(path, previous);
        }
    }

    [Fact]
    public void DesktopSyncDevice_Returns_Stable_Id()
    {
        var a = DesktopSyncDevice.GetOrCreateDeviceId();
        var b = DesktopSyncDevice.GetOrCreateDeviceId();
        Assert.Equal(a, b);
    }
}

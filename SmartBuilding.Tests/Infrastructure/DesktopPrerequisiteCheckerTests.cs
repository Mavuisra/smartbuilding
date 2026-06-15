using Microsoft.Extensions.Configuration;
using SmartBuilding.Infrastructure.Services;
using Xunit;

namespace SmartBuilding.Tests.Infrastructure;

public class DesktopPrerequisiteCheckerTests
{
    [Fact]
    public void Evaluate_Includes_DotNet_Status()
    {
        var config = BuildConfig("Server");
        var result = DesktopPrerequisiteChecker.Evaluate(config);

        var dotnet = Assert.Single(result.Items, i => i.Kind == PrerequisiteKind.DotNetRuntime);
        Assert.True(dotnet.IsSatisfied);
    }

    [Fact]
    public void Evaluate_Server_Mode_Requires_Local_MySql_Components()
    {
        var config = BuildConfig("Server");
        var result = DesktopPrerequisiteChecker.Evaluate(config);

        Assert.Contains(result.Items, i => i.Kind == PrerequisiteKind.XamppMySql);
        Assert.Contains(result.Items, i => i.Kind == PrerequisiteKind.MySqlService);
    }

    [Fact]
    public void Evaluate_Client_Mode_Without_ServerHost_Is_Optional_Info()
    {
        var config = BuildConfig("Client");
        var result = DesktopPrerequisiteChecker.Evaluate(config);

        var remote = Assert.Single(result.Items, i => i.Kind == PrerequisiteKind.NetworkInfo);
        Assert.True(remote.IsOptional);
        Assert.True(result.IsReady || remote.IsSatisfied);
    }

    private static IConfiguration BuildConfig(string deploymentMode)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LocalDatabase:DeploymentMode"] = deploymentMode,
                ["LocalDatabase:Database"] = "sbms_local",
                ["LocalDatabase:MySqlPort"] = "3306",
                ["LocalDatabase:User"] = "root",
                ["LocalDatabase:Password"] = "",
            })
            .Build();
    }
}

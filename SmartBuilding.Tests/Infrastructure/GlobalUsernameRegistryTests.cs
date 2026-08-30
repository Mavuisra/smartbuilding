using SmartBuilding.Infrastructure.Persistence;
using Xunit;

namespace SmartBuilding.Tests.Infrastructure;

public class GlobalUsernameRegistryTests : IDisposable
{
    private readonly string _tempIndexPath;
    private readonly GlobalUsernameRegistry _registry;

    public GlobalUsernameRegistryTests()
    {
        _tempIndexPath = Path.Combine(Path.GetTempPath(), $"sbms-username-test-{Guid.NewGuid():N}.json");
        _registry = GlobalUsernameRegistry.Load(_tempIndexPath);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempIndexPath))
                File.Delete(_tempIndexPath);
        }
        catch
        {
            // ignore
        }
    }

    [Fact]
    public void Register_and_resolve_username()
    {
        var orgId = Guid.NewGuid();

        _registry.Register("Jean.Dupont", orgId);

        Assert.True(_registry.IsTaken("jean.dupont"));
        Assert.Equal(orgId, _registry.TryResolveOrganization("JEAN.DUPONT"));
        Assert.False(_registry.IsTaken("jean.dupont", exceptOrganizationId: orgId));
    }

    [Fact]
    public void Unregister_removes_username()
    {
        var orgId = Guid.NewGuid();

        _registry.Register("temp.user", orgId);
        _registry.Unregister("temp.user");

        Assert.False(_registry.IsTaken("temp.user"));
        Assert.Null(_registry.TryResolveOrganization("temp.user"));
    }
}

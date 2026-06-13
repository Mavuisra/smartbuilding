using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Infrastructure;
using SmartBuilding.Infrastructure.Http;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Sync;
using SmartBuilding.Shared.DTOs.Sync;
using Xunit;

namespace SmartBuilding.Tests.Infrastructure;

public class CloudPullSyncTests
{
    private const string SampleTenantJson =
        "{\"Id\": \"baff5428-f3b4-4b49-b5f4-e79b64495ba9\", \"Name\": \"COCO\", \"Email\": \"\", \"Notes\": \"\", \"Phone\": \"34567890\", \"Gender\": \"\", \"Address\": \"\", \"Company\": \"\", \"Employer\": \"\", \"IsSynced\": false, \"CreatedAt\": \"2026-06-05T09:34:00.0736590Z\", \"UpdatedAt\": \"2026-06-05T12:52:00.0804410Z\", \"Activities\": [], \"Dependents\": [], \"NationalId\": \"\", \"Profession\": \"\", \"Nationality\": \"\", \"PersonCount\": 1, \"RentalStatus\": \"Actif\", \"ChildrenCount\": 0, \"DossierNumber\": \"DOS-0001\", \"MaritalStatus\": \"\", \"IdDocumentType\": \"\", \"LeaseContracts\": []}";

    [Fact]
    public void Deserialize_PullResponse_From_Cloud_Json()
    {
        var pullJson =
            """
            {"serverTimestamp":"2026-06-13T10:37:09.236090Z","entities":[{"id":"baff5428-f3b4-4b49-b5f4-e79b64495ba9","updatedAt":"2026-06-05T12:52:00.080441+00:00","deletedAt":null,"jsonData":"{\"Id\": \"baff5428-f3b4-4b49-b5f4-e79b64495ba9\", \"Name\": \"COCO\"}"}]}
            """;

        var resp = JsonSerializer.Deserialize<SyncPullResponse>(pullJson, SyncJson.Options);
        Assert.NotNull(resp);
        Assert.Single(resp!.Entities);
        Assert.Equal("COCO", JsonSerializer.Deserialize<Tenant>(resp.Entities[0].JsonData, SyncJson.Options)!.Name);
    }

    [Fact]
    public void Deserialize_Tenant_Payload_From_Cloud()
    {
        var tenant = JsonSerializer.Deserialize<Tenant>(SampleTenantJson, SyncJson.Options);
        Assert.NotNull(tenant);
        Assert.Equal("COCO", tenant!.Name);
        Assert.Equal("DOS-0001", tenant.DossierNumber);
    }

    [Fact]
    public async Task ApplyPullAsync_Persists_Tenant_InMemory()
    {
        var options = new DbContextOptionsBuilder<SmartBuildingDbContext>()
            .UseInMemoryDatabase($"sync-pull-{Guid.NewGuid():N}")
            .Options;

        await using var db = new SmartBuildingDbContext(options);
        var payload = new SyncEntityPayload
        {
            Id = Guid.Parse("baff5428-f3b4-4b49-b5f4-e79b64495ba9"),
            UpdatedAt = DateTime.Parse("2026-06-05T12:52:00.0804410Z").ToUniversalTime(),
            JsonData = SampleTenantJson
        };

        var (conflicts, applied) = await SyncCoordinator.ApplyPullAsync(db, "Tenants", [payload], CancellationToken.None);
        Assert.Equal(0, conflicts);
        Assert.Equal(1, applied);

        var saved = await db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == payload.Id);
        Assert.NotNull(saved);
        Assert.Equal("COCO", saved!.Name);
        Assert.True(saved.IsSynced);
    }

    [Fact]
    public async Task Live_Cloud_Pull_Returns_Tenants()
    {
        const string baseUrl = "https://smartbuilding-0kbk.onrender.com/";
        var token = await CloudApiAuth.LoginAsync(baseUrl);
        Assert.False(string.IsNullOrWhiteSpace(token));

        using var api = new CloudApiClient(baseUrl, token);
        var since = Uri.EscapeDataString(DateTime.MinValue.ToString("o"));
        var result = await api.GetAsync($"api/sync/pull?entityType=Tenants&since={since}");

        Assert.True(result.IsSuccess, $"HTTP {result.StatusCode}: {result.Body[..Math.Min(200, result.Body.Length)]}");
        var pull = JsonSerializer.Deserialize<SyncPullResponse>(result.Body, SyncJson.Options);
        Assert.NotNull(pull);
        Assert.NotEmpty(pull!.Entities);
    }

    [Fact]
    public async Task Live_PerformCloudToLocalPull_Writes_To_MySql_When_Available()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LocalDatabase:DeploymentMode"] = "Server",
                ["LocalDatabase:Database"] = "sbms_local",
                ["LocalDatabase:MySqlPort"] = "3306",
                ["LocalDatabase:User"] = "root",
                ["LocalDatabase:Password"] = "",
                ["Api:BaseUrl"] = "https://smartbuilding-0kbk.onrender.com/"
            })
            .Build();

        DesktopLocalDatabaseConfig localDb;
        try
        {
            localDb = DesktopLocalDatabaseBootstrap.Resolve(config);
            if (!DesktopLocalDatabaseBootstrap.CanConnectToMySql(localDb.ConnectionString))
                return; // XAMPP absent — test ignoré
        }
        catch
        {
            return;
        }

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddInfrastructure(config, isDesktop: true);
        await using var provider = services.BuildServiceProvider();

        var token = await CloudApiAuth.LoginAsync(config["Api:BaseUrl"]!);
        Assert.False(string.IsNullOrWhiteSpace(token));
        SyncCloudTokenStore.Persist(token!);
        InitialSyncStore.Clear();

        var sync = provider.GetRequiredService<ISyncService>();
        var result = await sync.PerformCloudToLocalPullAsync(fullPull: true);

        Assert.True(result.Success, result.Error ?? "pull failed");

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartBuildingDbContext>();
        var tenantCount = await db.Tenants.IgnoreQueryFilters().CountAsync();
        Assert.True(
            result.Pulled > 0 || tenantCount > 0,
            $"Expected pulled > 0 or existing tenants, got pulled={result.Pulled}, tenants={tenantCount}. Error: {result.Error}");
    }
}

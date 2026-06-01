using Microsoft.EntityFrameworkCore;
using SmartBuilding.Desktop.WPF.Services;
using SmartBuilding.Infrastructure.Persistence;
using Xunit;

namespace SmartBuilding.Tests.Visitors;

public class VisitsServiceTests
{
    [Fact]
    public async Task LoadAsync_Returns_Empty_When_No_Visitors()
    {
        await using var db = await CreateDbAsync();
        var service = new VisitsService(db);

        var data = await service.LoadAsync();

        Assert.Empty(data.Visits);
        Assert.Equal(0, data.VisitorsToday);
        Assert.False(await db.Visitors.AnyAsync());
    }

    [Fact]
    public async Task CreateVisitorAsync_And_Checkout_Work()
    {
        await using var db = await CreateDbAsync();
        var service = new VisitsService(db);

        var error = await service.CreateVisitorAsync(new Domain.Entities.Visitors.Visitor
        {
            FullName = "Test Visiteur",
            HostName = "Réception",
            Purpose = "Test",
            VisitType = "Réunion",
            Zone = "Réception"
        });
        Assert.Equal(string.Empty, error);

        var id = (await db.Visitors.SingleAsync(v => v.FullName == "Test Visiteur")).Id;
        var checkoutError = await service.CheckoutVisitorAsync(id);
        Assert.Equal(string.Empty, checkoutError);

        var visitor = await db.Visitors.FindAsync(id);
        Assert.Equal("Sorti", visitor!.AccessStatus);
        Assert.NotNull(visitor.CheckOutAt);
    }

    private static async Task<SmartBuildingDbContext> CreateDbAsync()
    {
        var options = new DbContextOptionsBuilder<SmartBuildingDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var db = new SmartBuildingDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        return db;
    }
}

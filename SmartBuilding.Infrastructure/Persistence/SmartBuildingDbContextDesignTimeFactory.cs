using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmartBuilding.Infrastructure.Persistence;

/// <summary>Factory design-time pour <c>dotnet ef migrations</c> (MySQL / XAMPP).</summary>
public sealed class SmartBuildingDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SmartBuildingDbContext>
{
    public SmartBuildingDbContext CreateDbContext(string[] args)
    {
        var connectionString = args.FirstOrDefault()
                               ?? DesktopLocalDatabaseBootstrap.DefaultMySqlConnectionString;

        DesktopLocalDatabaseBootstrap.EnsureMySqlDatabaseExists(connectionString);

        var options = new DbContextOptionsBuilder<SmartBuildingDbContext>()
            .UseMySql(connectionString, ServerVersion.Parse("8.0.36-mysql"))
            .Options;

        return new SmartBuildingDbContext(options);
    }
}

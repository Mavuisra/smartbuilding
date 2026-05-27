using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Domain.Interfaces;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Services;

namespace SmartBuilding.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDesktop = false)
    {
        string connectionString;
        if (isDesktop)
        {
            DesktopSqlitePaths.EnsureInitialized();
            connectionString = DesktopSqlitePaths.ConnectionString;
        }
        else
        {
            connectionString = configuration.GetConnectionString("PostgreSQL")
                               ?? throw new InvalidOperationException("Connection string PostgreSQL requise.");
        }

        services.AddDbContext<SmartBuildingDbContext>(options =>
        {
            if (isDesktop)
                options.UseSqlite(connectionString);
            else
                options.UseNpgsql(connectionString);
        });

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<FinanceLedgerService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ISyncService, SyncService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<LocationDataCleaner>();
        services.AddScoped<TechnicalDataCleaner>();
        services.AddSingleton<INetworkService, NetworkService>();

        services.AddHttpClient("SmartBuildingApi", client =>
        {
            client.BaseAddress = new Uri(configuration["Api:BaseUrl"] ?? "https://localhost:7001/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}

using System.Net;
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
            var baseUrl = configuration["Api:BaseUrl"] ?? "https://smartbuilding-0kbk.onrender.com/";
            if (!baseUrl.EndsWith('/'))
                baseUrl += "/";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
            // runserver Django = HTTP/1.1 ; évite pipelining/corruption sur connexion partagée
            client.DefaultRequestVersion = HttpVersion.Version11;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
        });

        return services;
    }
}

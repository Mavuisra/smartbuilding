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
        DesktopLocalDatabaseConfig? localDb = null;
        string connectionString;
        if (isDesktop)
        {
            localDb = DesktopLocalDatabaseBootstrap.Resolve(configuration);
            connectionString = localDb.ConnectionString;
            services.AddSingleton(localDb);
        }
        else
        {
            connectionString = configuration.GetConnectionString("PostgreSQL")
                               ?? throw new InvalidOperationException("Connection string PostgreSQL requise.");
        }

        void ConfigureDbContext(DbContextOptionsBuilder options)
        {
            if (isDesktop)
            {
                if (localDb!.IsMySql)
                {
                    var serverVersion = ServerVersion.Parse("8.0.36-mysql");
                    options.UseMySql(connectionString, serverVersion, mySql =>
                        mySql.EnableStringComparisonTranslations());
                }
                else
                {
                    options.UseSqlite(connectionString);
                }
            }
            else
            {
                options.UseNpgsql(connectionString);
            }
        }

        services.AddDbContext<SmartBuildingDbContext>(ConfigureDbContext);
        services.AddDbContextFactory<SmartBuildingDbContext>(ConfigureDbContext);

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

using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Domain.Interfaces;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Services;
using SmartBuilding.Infrastructure.Sync;

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
            if (!localDb.IsMySql)
            {
                throw new InvalidOperationException(
                    "SBMS desktop requiert MySQL (XAMPP). La base SQLite n'est plus prise en charge.");
            }

            services.AddSingleton(sp => OrganizationRegistry.Load(configuration));
            services.AddSingleton<OrganizationConnectionResolver>();
            services.AddSingleton<OrganizationProvisioningService>();
            services.AddSingleton<OrganizationCloudSyncService>();

            connectionString = localDb.ConnectionString;
            services.AddSingleton(localDb);
        }
        else
        {
            connectionString = configuration.GetConnectionString("PostgreSQL")
                               ?? throw new InvalidOperationException("Connection string PostgreSQL requise.");
        }

        if (isDesktop)
        {
            services.AddDbContext<SmartBuildingDbContext>((sp, options) =>
            {
                var resolver = sp.GetRequiredService<OrganizationConnectionResolver>();
                var serverVersion = ServerVersion.Parse("8.0.36-mysql");
                options.UseMySql(resolver.ConnectionString, serverVersion, mySql =>
                    mySql.EnableStringComparisonTranslations());
                options.AddInterceptors(sp.GetRequiredService<LocalChangeSyncSaveInterceptor>());
            });
            services.AddDbContextFactory<SmartBuildingDbContext>((sp, options) =>
            {
                var resolver = sp.GetRequiredService<OrganizationConnectionResolver>();
                var serverVersion = ServerVersion.Parse("8.0.36-mysql");
                options.UseMySql(resolver.ConnectionString, serverVersion, mySql =>
                    mySql.EnableStringComparisonTranslations());
                options.AddInterceptors(sp.GetRequiredService<LocalChangeSyncSaveInterceptor>());
            });
        }
        else
        {
            services.AddDbContext<SmartBuildingDbContext>((sp, options) =>
            {
                options.UseNpgsql(connectionString);
            });
            services.AddDbContextFactory<SmartBuildingDbContext>((sp, options) =>
            {
                options.UseNpgsql(connectionString);
            });
        }

        if (isDesktop)
        {
            services.AddSingleton<ILocalChangeSyncTrigger, LocalChangeSyncTrigger>();
            services.AddSingleton<LocalChangeSyncSaveInterceptor>();
        }

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<FinanceLedgerService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddSingleton<ISyncNotifier, SyncNotifier>();
        services.AddScoped<CloudIdentityService>();
        services.AddScoped<ISyncService, SyncService>();
        services.AddScoped<IDocumentCloudUploadService, CloudDocumentUploadService>();
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
            client.DefaultRequestVersion = HttpVersion.Version11;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
        });

        return services;
    }
}

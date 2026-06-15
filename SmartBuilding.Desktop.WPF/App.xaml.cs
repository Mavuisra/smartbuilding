using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Desktop.WPF.Services;
using SmartBuilding.Desktop.WPF.ViewModels;
using SmartBuilding.Desktop.WPF.Views;
using SmartBuilding.Infrastructure;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Services;

namespace SmartBuilding.Desktop.WPF;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private const int MinSplashMs = 1800;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                DbSaveExceptionTranslator.ToDetailedMessage(args.Exception),
                BuildingInfoDefaults.CompanyName,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        // Mode interne: application d’une mise à jour (l’exe est lancé depuis un dossier staging).
        if (AppAutoUpdater.TryApplyUpdateIfRequested(e.Args))
            return;

        var splash = new SplashWindow();
        splash.Show();
        await PumpUiAsync();

        try
        {
            // Sécurité démarrage: la MAJ auto au boot est désactivée par défaut
            // pour éviter les fermetures silencieuses de l'app.
            // Active-la explicitement via SMARTBUILDING_STARTUP_UPDATE=true.
            var startupUpdateEnabled = string.Equals(
                Environment.GetEnvironmentVariable("SMARTBUILDING_STARTUP_UPDATE"),
                "true",
                StringComparison.OrdinalIgnoreCase);
            if (startupUpdateEnabled)
            {
                splash.UpdateProgress(5, "Vérification des mises à jour...");
                await PumpUiAsync();

                if (await AppAutoUpdater.CheckAndApplyIfNeededAsync(
                        splash.UpdateProgress,
                        confirmUpdateAsync: async (currentVersion, latestTag) =>
                        {
                            var result = MessageBox.Show(
                                $"Une nouvelle version est disponible.\n\nVersion actuelle: {currentVersion}\nNouvelle version: {latestTag}\n\nInstaller maintenant ?",
                                "Mise à jour disponible",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Information);
                            await Task.CompletedTask;
                            return result == MessageBoxResult.Yes;
                        }))
                {
                    Shutdown(0);
                    return;
                }
            }

            var splashStarted = Environment.TickCount64;

            splash.UpdateProgress(5, "Préparation de l'application...");
            await PumpUiAsync();

            _host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration(cfg =>
                {
                    cfg.SetBasePath(AppContext.BaseDirectory);
                    cfg.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
#if DEBUG
                    cfg.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
#endif
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddInfrastructure(context.Configuration, isDesktop: true);
                    services.AddSingleton<SessionService>();
                    services.AddSingleton<NetworkConnectivityWatcher>();
                    services.AddSingleton<AppBrandingState>();
                    services.AddSingleton<AppConfigurationService>();
                    services.AddScoped<InitialSetupService>();
                    services.AddSingleton<ShellNavigationService>();
                    services.AddSingleton<NavigationService>();
                    services.AddTransient<InitialSetupViewModel>();
                    services.AddTransient<InitialSetupWindow>();
                    services.AddTransient<LoginViewModel>();
                    services.AddTransient<DashboardViewModel>();
                    services.AddTransient<PersonnelViewModel>();
                    services.AddTransient<PersonnelService>();
                    services.AddTransient<LocationsViewModel>();
                    services.AddTransient<LocationsListViewModel>();
                    services.AddTransient<LocationsTenantsViewModel>();
                    services.AddTransient<LocationsPatrimoineViewModel>();
                    services.AddTransient<LocationsService>();
                    services.AddTransient<FinancesViewModel>();
                    services.AddTransient<FinancesService>();
                    services.AddTransient<FinancesReportPdfService>();
                    services.AddTransient<RapportsViewModel>();
                    services.AddTransient<RapportsService>();
                    services.AddTransient<RapportsReportPdfService>();
                    services.AddTransient<TechnicalViewModel>();
                    services.AddTransient<TechnicalService>();
                    services.AddTransient<SuppliersViewModel>();
                    services.AddTransient<SuppliersService>();
                    services.AddTransient<InventoryViewModel>();
                    services.AddTransient<InventoryService>();
                    services.AddTransient<ConsumptionsViewModel>();
                    services.AddTransient<ConsumptionsService>();
                    services.AddTransient<IncidentsViewModel>();
                    services.AddTransient<IncidentsService>();
                    services.AddTransient<VisitsViewModel>();
                    services.AddTransient<VisitsService>();
                    services.AddTransient<EmailsViewModel>();
                    services.AddTransient<EmailsModuleService>();
                    services.AddTransient<SynchronizationViewModel>();
                    services.AddTransient<SynchronizationService>();
                    services.AddTransient<SettingsViewModel>();
                    services.AddTransient<SettingsService>();
                    services.AddTransient<CloudDatabaseResetService>();
                    services.AddTransient<PropertyStructureService>();
                    services.AddTransient<DocumentsViewModel>();
                    services.AddSingleton<DocumentsUserLibraryService>();
                    services.AddTransient<DocumentsModuleService>();
                    services.AddTransient<UsersViewModel>();
                    services.AddTransient<UsersModuleService>();
                    services.AddTransient<ActivityLogViewModel>();
                    services.AddTransient<ActivityLogModuleService>();
                    services.AddTransient<TenantDetailViewModel>();
                    services.AddTransient<TenantDetailService>();
                    services.AddTransient<LocationBuildingFormViewModel>();
                    services.AddTransient<LocationTenantFormViewModel>();
                    services.AddTransient<LocationContractFormViewModel>();
                    services.AddTransient<LocationRentFormViewModel>();
                    services.AddTransient<ModulePageViewModel>();
                    services.AddTransient<ModuleDataService>();
                    services.AddTransient<MainShellViewModel>();
                    services.AddTransient<LoginView>();
                    services.AddTransient<MainWindow>();
                    services.AddHostedService<SyncBackgroundService>();
                })
                .Build();

            splash.UpdateProgress(35, "Connexion aux services...");
            await PumpUiAsync();
            await _host.StartAsync();

            splash.UpdateProgress(60, "Initialisation de la base de données...");
            await PumpUiAsync();

            using (var scope = _host.Services.CreateScope())
            {
                var localDb = scope.ServiceProvider.GetRequiredService<DesktopLocalDatabaseConfig>();
                if (!localDb.RequiresClientDatabaseConnection)
                {
                    var db = scope.ServiceProvider.GetRequiredService<SmartBuildingDbContext>();
                    var logger = scope.ServiceProvider.GetService<ILogger<App>>();
                    await DesktopDatabaseInitializer.InitializeAsync(db, localDb, logger);
                    await DatabaseSeeder.SeedReferenceDataAsync(db);
                }
            }

            using (var setupScope = _host.Services.CreateScope())
            {
                var setupService = setupScope.ServiceProvider.GetRequiredService<InitialSetupService>();

                while (await setupService.NeedsInitialSetupAsync())
                {
                    splash.UpdateProgress(80, "Configuration initiale obligatoire...");
                    await PumpUiAsync();
                    await splash.CloseAnimatedAsync();

                    var setupWindow = setupScope.ServiceProvider.GetRequiredService<InitialSetupWindow>();
                    if (setupWindow.ShowDialog() == true)
                        break;

                    System.Windows.MessageBox.Show(
                        "La configuration initiale (administrateur, bâtiment, base de données) est obligatoire pour utiliser l'application.",
                        $"{BuildingInfoDefaults.CompanyName} — Configuration requise",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    splash = new SplashWindow();
                    splash.Show();
                    await PumpUiAsync();
                }

            }

            splash.UpdateProgress(90, "Chargement de l'interface...");
            await PumpUiAsync();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();

            splash.UpdateProgress(95, "Préparation de l'interface...");
            await PumpUiAsync();

            // Branding neutre avant login — le profil société est chargé après authentification.
            var branding = _host.Services.GetRequiredService<AppBrandingState>();
            branding.CompanyName = "Smart Building MS";
            branding.AppSubtitle = AppBrandingState.DefaultSubtitle;
            splash.ApplyBranding(branding.CompanyName, branding.AppSubtitle);
            Resources["Branding"] = branding;

            splash.UpdateProgress(100, "Prêt !");
            await PumpUiAsync();

            var elapsed = Environment.TickCount64 - splashStarted;
            if (elapsed < MinSplashMs)
                await Task.Delay((int)(MinSplashMs - elapsed));

            await splash.CloseAnimatedAsync();

            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            splash.Close();
            MessageBox.Show(
                $"Impossible de démarrer l'application.\n\n{ex.Message}",
                BuildingInfoDefaults.CompanyName,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static Task PumpUiAsync() =>
        Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render).Task;

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        base.OnExit(e);
    }
}

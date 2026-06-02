using System.IO;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Domain.Entities.Auth;
using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Services;

namespace SmartBuilding.Desktop.WPF.Services;

public sealed class InitialSetupService
{
    private static readonly string SetupFlagPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SBMS",
        "setup-completed.flag");
    private readonly AppConfigurationService _appConfiguration;
    private readonly IConfiguration _configuration;
    private readonly DesktopLocalDatabaseConfig _localDb;

    public InitialSetupService(
        AppConfigurationService appConfiguration,
        IConfiguration configuration,
        DesktopLocalDatabaseConfig localDb)
    {
        _appConfiguration = appConfiguration;
        _configuration = configuration;
        _localDb = localDb;
    }

    /// <summary>Indique si l'assistant de configuration obligatoire n'a pas encore été terminé.</summary>
    public static bool IsSetupWizardCompleted() => File.Exists(SetupFlagPath);

    public Task<bool> NeedsInitialSetupAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        if (!IsSetupWizardCompleted())
            return Task.FromResult(true);

        if (!IsDeploymentConfigured())
            return Task.FromResult(true);

        if (_localDb.RequiresClientDatabaseConnection)
            return Task.FromResult(true);

        return Task.FromResult(false);
    }

    /// <summary>Détecte automatiquement le serveur MySQL sur le réseau local (mode client).</summary>
    public Task<string?> TryDiscoverClientServerHostAsync(CancellationToken cancellationToken = default)
    {
        var section = _configuration.GetSection(DesktopLocalDatabaseConfig.SectionName);
        var preferred = section.GetValue<string>("ServerHost")?.Trim();
        return Task.Run(
            () => DesktopMySqlServerDiscovery.ResolveClientHost(section, preferred),
            cancellationToken);
    }

    private bool IsDeploymentConfigured()
    {
        var mode = _configuration["LocalDatabase:DeploymentMode"]?.Trim();
        if (string.IsNullOrWhiteSpace(mode))
            return false;

        if (mode.Equals("Client", StringComparison.OrdinalIgnoreCase))
        {
            var host = _configuration["LocalDatabase:ServerHost"]?.Trim();
            return !string.IsNullOrWhiteSpace(host);
        }

        return mode.Equals("Server", StringComparison.OrdinalIgnoreCase)
               || mode.Equals("Standalone", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<(bool Success, string Message)> TestDatabaseConnectionAsync(
        LocalDatabaseSetupSettings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isClient = string.Equals(settings.DeploymentMode, "Client", StringComparison.OrdinalIgnoreCase);
            var section = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["LocalDatabase:Database"] = settings.Database,
                    ["LocalDatabase:MySqlPort"] = settings.MySqlPort.ToString(),
                    ["LocalDatabase:User"] = settings.User,
                    ["LocalDatabase:Password"] = settings.Password
                })
                .Build()
                .GetSection("LocalDatabase");

            if (isClient)
            {
                var preferred = settings.ServerHost?.Trim();

                if (!string.IsNullOrWhiteSpace(preferred)
                    && !DesktopLocalDatabaseBootstrap.CanConnectToMySql(
                        DesktopMySqlConnectionBuilder.Build(section, preferred))
                    && IsTcpPortOpen(preferred, settings.MySqlPort > 0 ? settings.MySqlPort : 3306))
                {
                    return (false,
                        $"Serveur MySQL joignable sur {preferred}, mais authentification refusée. " +
                        "Vérifiez l'utilisateur/mot de passe MySQL (ex. sbms) et les droits réseau (sbms@%).");
                }

                var discovered = await Task.Run(
                    () => DesktopMySqlServerDiscovery.ResolveClientHost(section, preferred),
                    cancellationToken);

                if (discovered is null)
                {
                    return (false,
                        "Aucun serveur MySQL SBMS détecté sur le réseau. Vérifiez : MySQL démarré sur le serveur (XAMPP), " +
                        "même réseau Wi‑Fi/LAN, pare-feu port 3306, utilisateur MySQL autorisé (sbms@%). " +
                        "Vous pouvez aussi saisir l'IP manuellement (ipconfig sur le PC serveur).");
                }

                var autoNote = string.IsNullOrWhiteSpace(preferred)
                               || !string.Equals(preferred, discovered, StringComparison.OrdinalIgnoreCase)
                    ? $" (détecté automatiquement : {discovered})"
                    : string.Empty;

                return (true, $"Connexion MySQL réussie sur {discovered}{autoNote}.");
            }

            var host = "127.0.0.1";
            var connectionString = DesktopMySqlConnectionBuilder.Build(section, host);
            DesktopLocalDatabaseBootstrap.EnsureMySqlDatabaseExists(connectionString);

            if (!DesktopLocalDatabaseBootstrap.CanConnectToMySql(connectionString))
            {
                return (false, "Impossible de joindre MySQL sur ce PC. Démarrez MySQL dans XAMPP.");
            }

            return (true, "Connexion MySQL réussie.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<InitialSetupResult> CompleteInitialSetupAsync(InitialSetupRequest request, CancellationToken cancellationToken = default)
    {
        var dbSettings = await ResolveDatabaseSettingsForSaveAsync(request, cancellationToken);
        DesktopAppSettingsWriter.SaveLocalDatabase(dbSettings);

        var setupConfig = LoadConfigurationAfterSave();
        var targetLocalDb = DesktopLocalDatabaseBootstrap.Resolve(setupConfig);

        if (targetLocalDb.RequiresClientDatabaseConnection)
        {
            throw new InvalidOperationException(
                "Connexion MySQL au serveur impossible. À l'étape « Base de données », testez la connexion, " +
                "vérifiez XAMPP sur le serveur et le pare-feu (port 3306), puis réessayez « Terminer ».");
        }

        await using var db = CreateSetupDbContext(targetLocalDb);
        if (targetLocalDb.RunsSchemaMigrations)
            await DesktopDatabaseInitializer.InitializeAsync(db, targetLocalDb, cancellationToken: cancellationToken);
        else
            await EnsureDatabaseReadyForSetupSaveAsync(db, targetLocalDb, cancellationToken);

        await DatabaseSeeder.SeedReferenceDataAsync(db);

        var admin = await FindOrCreateSetupAdminAsync(db, request.AdminUsername, cancellationToken);
        var adminEmail = await ResolveAdminEmailAsync(db, request, admin.Id, cancellationToken);
        await PrepareUsersForSetupAdminAsync(db, request.AdminUsername, adminEmail, admin.Id, cancellationToken);

        var requiresRestart = !string.Equals(
            _configuration["LocalDatabase:DeploymentMode"],
            dbSettings.DeploymentMode,
            StringComparison.OrdinalIgnoreCase)
            || (string.Equals(dbSettings.DeploymentMode, "Client", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(
                    _configuration["LocalDatabase:ServerHost"]?.Trim(),
                    dbSettings.ServerHost?.Trim(),
                    StringComparison.OrdinalIgnoreCase));

        admin.Username = request.AdminUsername.Trim();
        admin.FullName = request.AdminFullName.Trim();
        admin.Email = adminEmail;
        admin.Role = UserRole.Administrateur;
        admin.PasswordHash = AuthService.HashPassword(request.AdminPassword);
        admin.IsActive = true;
        admin.IsSynced = false;
        admin.MarkUpdated();

        var building = await db.BuildingInfos
            .IgnoreQueryFilters()
            .OrderBy(b => b.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (building is null)
        {
            building = new BuildingInfo();
            db.BuildingInfos.Add(building);
        }
        else if (building.DeletedAt is not null)
        {
            building.DeletedAt = null;
        }

        building.Name = request.BuildingName.Trim();
        building.Address = request.BuildingAddress.Trim();
        building.City = request.BuildingCity.Trim();
        building.Country = request.BuildingCountry.Trim();
        building.Phone = request.CompanyPhone.Trim();
        building.Email = request.CompanyEmail.Trim();
        building.Website = request.CompanyWebsite.Trim();
        building.NationalId = request.CompanyNationalId.Trim();
        building.TotalFloors = request.TotalFloors;
        building.LogoPath = PersistLogo(request.LogoPath);
        building.TimeZoneId = "Africa/Kinshasa";
        building.Currency = "USD";
        building.DateFormat = "dd/MM/yyyy";
        building.Language = "Français";
        building.TimeFormat = "24 heures";
        building.MaintenanceMode = false;
        building.MarkUpdated();

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(DbSaveExceptionTranslator.ToDetailedMessage(ex), ex);
        }

        _appConfiguration.SaveAndApplyAppearance(
            request.ThemeMode,
            request.PrimaryColorHex,
            request.SidebarColorHex,
            request.SecondaryColorHex,
            compactTables: false,
            showKpiSparklines: true);

        WriteSetupCompletedFlag();

        await DatabaseSeeder.EnsureReservedAdminAccountsAsync(db, cancellationToken);

        var localDbLabel = FormatDatabaseLabel(dbSettings);
        var localPersisted = await db.Users.AnyAsync(
                                 u => u.DeletedAt == null
                                      && u.Username.ToLower() == request.AdminUsername.Trim().ToLower(),
                                 cancellationToken)
                             && await db.BuildingInfos.AnyAsync(
                                 b => b.DeletedAt == null && b.Name == request.BuildingName.Trim(),
                                 cancellationToken);
        if (!localPersisted)
            throw new InvalidOperationException("Échec de persistance locale des données de configuration.");

        var restartNote = requiresRestart
            ? " Redémarrez SBMS pour appliquer le mode serveur / poste client choisi."
            : "";

        // Sync cloud volontairement reportée : évite 30–120 s d'attente réseau au clic « Terminer ».
        return new InitialSetupResult(
            LocalPersistenceOk: true,
            LocalDbPath: localDbLabel,
            RequiresAppRestart: requiresRestart,
            CloudSyncAttempted: false,
            CloudSyncSuccess: false,
            CloudSyncMessage:
                "Configuration locale enregistrée. La synchronisation cloud se fera depuis le module Synchronisation (ou en arrière-plan)." +
                restartNote);
    }

    private static readonly string[] GhostBootstrapUsernames = ["admin", "admini", "admin2"];

    private static readonly string[] LegacyBootstrapEmails =
    [
        "admin@sbms.local",
        "admini@sbms.local",
        "admin2@sbms.local"
    ];

    private static IConfiguration LoadConfigurationAfterSave() =>
        new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

    private static SmartBuildingDbContext CreateSetupDbContext(DesktopLocalDatabaseConfig localDb)
    {
        var serverVersion = ServerVersion.Parse("8.0.36-mysql");
        var options = new DbContextOptionsBuilder<SmartBuildingDbContext>()
            .UseMySql(localDb.ConnectionString, serverVersion, mySql =>
                mySql.EnableStringComparisonTranslations())
            .Options;
        return new SmartBuildingDbContext(options);
    }

    /// <summary>Une seule passe : fantômes bootstrap + conflits d'e-mail, puis un seul SaveChanges.</summary>
    private static async Task PrepareUsersForSetupAdminAsync(
        SmartBuildingDbContext db,
        string chosenUsername,
        string targetEmail,
        Guid keepUserId,
        CancellationToken cancellationToken)
    {
        var chosen = chosenUsername.Trim().ToLowerInvariant();
        var legacyEmails = new HashSet<string>(LegacyBootstrapEmails, StringComparer.OrdinalIgnoreCase);
        var normalizedTarget = targetEmail.Trim().ToLowerInvariant();

        var users = await db.Users
            .IgnoreQueryFilters()
            .Where(u => u.Id != keepUserId)
            .ToListAsync(cancellationToken);

        var changed = false;
        foreach (var user in users)
        {
            var name = user.Username.Trim().ToLowerInvariant();
            var email = user.Email.Trim();
            var isGhostName = GhostBootstrapUsernames.Contains(name) && !string.Equals(name, chosen, StringComparison.Ordinal);
            var isGhostEmail = legacyEmails.Contains(email);
            var blocksTarget = !string.IsNullOrEmpty(normalizedTarget)
                               && email.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase);

            if (!isGhostName && !isGhostEmail && !blocksTarget)
                continue;

            user.DeletedAt ??= DateTime.UtcNow;
            user.IsActive = false;
            user.Email = MakeArchivedEmail(user);
            user.MarkUpdated();
            changed = true;
        }

        if (changed)
            await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<User> FindOrCreateSetupAdminAsync(
        SmartBuildingDbContext db,
        string adminUsername,
        CancellationToken cancellationToken)
    {
        var chosen = adminUsername.Trim().ToLowerInvariant();
        var existing = await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Username.ToLower() == chosen, cancellationToken);

        if (existing is not null)
        {
            existing.DeletedAt = null;
            existing.IsActive = true;
            return existing;
        }

        var created = new User();
        db.Users.Add(created);
        return created;
    }

    private static string MakeArchivedEmail(User user)
    {
        var name = user.Username.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(name))
            name = "user";

        return $"{name}.archive.{user.Id:N}@sbms.local";
    }

    private static async Task<string> ResolveAdminEmailAsync(
        SmartBuildingDbContext db,
        InitialSetupRequest request,
        Guid adminId,
        CancellationToken cancellationToken)
    {
        var username = request.AdminUsername.Trim().ToLowerInvariant();
        var desired = request.CompanyEmail.Trim();

        if (string.IsNullOrWhiteSpace(desired))
            desired = $"{username}@sbms.local";

        if (LegacyBootstrapEmails.Contains(desired, StringComparer.OrdinalIgnoreCase))
            desired = $"{username}@sbms.local";

        var emailTaken = await db.Users
            .IgnoreQueryFilters()
            .AnyAsync(
                u => u.Id != adminId && u.Email.ToLower() == desired.ToLower(),
                cancellationToken);

        if (!emailTaken)
            return desired;

        var fallback = $"{username}@sbms.local";
        var fallbackTaken = await db.Users
            .IgnoreQueryFilters()
            .AnyAsync(
                u => u.Id != adminId && u.Email.ToLower() == fallback.ToLower(),
                cancellationToken);

        if (!fallbackTaken)
            return fallback;

        return $"{username}.{Guid.NewGuid():N}@sbms.local";
    }

    private static async Task EnsureDatabaseReadyForSetupSaveAsync(
        SmartBuildingDbContext db,
        DesktopLocalDatabaseConfig localDb,
        CancellationToken cancellationToken)
    {
        if (!DesktopDatabaseInitializer.IsMySqlProvider(db))
            return;

        try
        {
            if (!await db.Database.CanConnectAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    localDb.IsLanClient
                        ? $"Impossible d'accéder à MySQL sur le serveur {localDb.ServerHost}."
                        : "Impossible d'accéder à MySQL sur ce PC. Démarrez MySQL dans XAMPP.");
            }

            await db.Users.IgnoreQueryFilters().AnyAsync(cancellationToken);
        }
        catch (Exception ex) when (localDb.IsLanClient && !localDb.RunsSchemaMigrations)
        {
            throw new InvalidOperationException(
                "La base MySQL du serveur n'est pas encore prête pour ce poste client.\n\n" +
                "Sur le PC serveur (XAMPP) : lancez SBMS en mode « Serveur », terminez la configuration une première fois, puis réessayez ici.\n\n" +
                DbSaveExceptionTranslator.ToDetailedMessage(ex),
                ex);
        }
    }

    private static Task<LocalDatabaseSetupSettings> ResolveDatabaseSettingsForSaveAsync(
        InitialSetupRequest request,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var settings = request.ToLocalDatabaseSettings();
        if (!string.Equals(settings.DeploymentMode, "Client", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(settings);

        var section = BuildLocalDatabaseSection(settings);
        var host = settings.ServerHost?.Trim();

        if (!string.IsNullOrWhiteSpace(host) && CanConnectMySqlHost(section, host))
            return Task.FromResult(settings);

        var cached = DesktopClientHostCache.Read();
        if (!string.IsNullOrWhiteSpace(cached)
            && !string.Equals(cached, host, StringComparison.OrdinalIgnoreCase)
            && CanConnectMySqlHost(section, cached))
        {
            request.ServerHost = cached;
            return Task.FromResult(request.ToLocalDatabaseSettings());
        }

        throw new InvalidOperationException(
            "Impossible de joindre le serveur MySQL. À l'étape « Base de données », saisissez l'IP du serveur " +
            "et cliquez sur « Tester la connexion » avant « Terminer ».");
    }

    private static bool CanConnectMySqlHost(IConfigurationSection section, string host)
    {
        var connectionString = DesktopMySqlConnectionBuilder.Build(section, host);
        return DesktopLocalDatabaseBootstrap.CanConnectToMySql(connectionString);
    }

    private static bool IsTcpPortOpen(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            var connect = client.ConnectAsync(host, port);
            if (!connect.Wait(350))
                return false;
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static IConfigurationSection BuildLocalDatabaseSection(LocalDatabaseSetupSettings settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LocalDatabase:Database"] = settings.Database,
                ["LocalDatabase:MySqlPort"] = settings.MySqlPort.ToString(),
                ["LocalDatabase:User"] = settings.User,
                ["LocalDatabase:Password"] = settings.Password
            })
            .Build()
            .GetSection("LocalDatabase");

    private static string FormatDatabaseLabel(LocalDatabaseSetupSettings settings) =>
        string.Equals(settings.DeploymentMode, "Client", StringComparison.OrdinalIgnoreCase)
            ? $"MySQL client → {settings.ServerHost}/{settings.Database}"
            : $"MySQL serveur local ({settings.Database})";

    private static string? PersistLogo(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return null;

        var logosDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SBMS",
            "logos");
        Directory.CreateDirectory(logosDir);

        var extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".png";

        var destination = Path.Combine(logosDir, $"company-logo{extension}");
        File.Copy(sourcePath, destination, overwrite: true);
        return destination;
    }

    public void EnsureSetupCompletedFlag()
    {
        if (!File.Exists(SetupFlagPath))
            WriteSetupCompletedFlag();
    }

    private static void WriteSetupCompletedFlag()
    {
        var folder = Path.GetDirectoryName(SetupFlagPath)!;
        Directory.CreateDirectory(folder);
        File.WriteAllText(SetupFlagPath, DateTime.UtcNow.ToString("O"));
    }
}

public sealed record InitialSetupResult(
    bool LocalPersistenceOk,
    string LocalDbPath,
    bool RequiresAppRestart,
    bool CloudSyncAttempted,
    bool CloudSyncSuccess,
    string CloudSyncMessage);

public sealed class InitialSetupRequest
{
    public string AdminFullName { get; set; } = string.Empty;
    public string AdminUsername { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;

    public string BuildingName { get; set; } = string.Empty;
    public string BuildingAddress { get; set; } = string.Empty;
    public string BuildingCity { get; set; } = string.Empty;
    public string BuildingCountry { get; set; } = "RDC";
    public int TotalFloors { get; set; } = 1;
    public string? LogoPath { get; set; }

    public string CompanyPhone { get; set; } = string.Empty;
    public string CompanyEmail { get; set; } = string.Empty;
    public string CompanyWebsite { get; set; } = string.Empty;
    public string CompanyNationalId { get; set; } = string.Empty;

    public AppThemeMode ThemeMode { get; set; } = AppThemeMode.Light;
    public string PrimaryColorHex { get; set; } = "#2D6A4F";
    public string SidebarColorHex { get; set; } = "#1B3D3B";
    public string SecondaryColorHex { get; set; } = "#0D9488";

    /// <summary>Server = base unique sur ce PC ; Client = poste distant.</summary>
    public string DeploymentMode { get; set; } = "Server";
    public string? ServerHost { get; set; }
    public string DatabaseName { get; set; } = "sbms_local";
    public int MySqlPort { get; set; } = 3306;
    public string MySqlUser { get; set; } = "root";
    public string MySqlPassword { get; set; } = "";

    public LocalDatabaseSetupSettings ToLocalDatabaseSettings() => new()
    {
        DeploymentMode = DeploymentMode,
        ServerHost = ServerHost,
        Database = DatabaseName,
        MySqlPort = MySqlPort,
        User = MySqlUser,
        Password = MySqlPassword
    };
}

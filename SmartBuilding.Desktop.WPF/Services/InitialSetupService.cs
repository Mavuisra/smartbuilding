using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using SmartBuilding.Application.Interfaces;
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
    private static readonly string ApiTokenPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SBMS",
        "api-token.txt");

    private readonly SmartBuildingDbContext _db;
    private readonly AppConfigurationService _appConfiguration;
    private readonly ISyncService _syncService;
    private readonly IConfiguration _configuration;
    private readonly DesktopLocalDatabaseConfig _localDb;

    public InitialSetupService(
        SmartBuildingDbContext db,
        AppConfigurationService appConfiguration,
        ISyncService syncService,
        IConfiguration configuration,
        DesktopLocalDatabaseConfig localDb)
    {
        _db = db;
        _appConfiguration = appConfiguration;
        _syncService = syncService;
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

        if (_localDb.RequiresClientDatabaseConnection)
        {
            throw new InvalidOperationException(
                "Connexion MySQL au serveur impossible. À l'étape « Base de données », vérifiez XAMPP sur le serveur, " +
                "testez la connexion, puis redémarrez SBMS avant de cliquer sur « Terminer ».");
        }

        await EnsureDatabaseReadyForSetupSaveAsync(cancellationToken);
        await RemoveGhostBootstrapAccountsAsync(request.AdminUsername, cancellationToken);

        var admin = await FindOrCreateSetupAdminAsync(request.AdminUsername, cancellationToken);
        var adminEmail = await ResolveAdminEmailAsync(request, admin.Id, cancellationToken);
        await ReleaseUniqueEmailAsync(adminEmail, admin.Id, cancellationToken);

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

        var building = await _db.BuildingInfos
            .IgnoreQueryFilters()
            .OrderBy(b => b.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (building is null)
        {
            building = new BuildingInfo();
            _db.BuildingInfos.Add(building);
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
            await _db.SaveChangesAsync(cancellationToken);
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

        await DatabaseSeeder.EnsureReservedAdminAccountsAsync(_db, cancellationToken);

        var localDbLabel = FormatDatabaseLabel(dbSettings);
        var localPersisted = await _db.Users.AnyAsync(
                                 u => u.DeletedAt == null
                                      && u.Username.ToLower() == request.AdminUsername.Trim().ToLower(),
                                 cancellationToken)
                             && await _db.BuildingInfos.AnyAsync(
                                 b => b.DeletedAt == null && b.Name == request.BuildingName.Trim(),
                                 cancellationToken);
        if (!localPersisted)
            throw new InvalidOperationException("Échec de persistance locale des données de configuration.");

        var restartNote = requiresRestart
            ? " Redémarrez SBMS pour appliquer le mode serveur / poste client choisi."
            : "";

        var online = await _syncService.IsOnlineAsync(cancellationToken);
        if (!online)
        {
            return new InitialSetupResult(
                LocalPersistenceOk: true,
                LocalDbPath: localDbLabel,
                RequiresAppRestart: requiresRestart,
                CloudSyncAttempted: false,
                CloudSyncSuccess: false,
                CloudSyncMessage: "Configuration locale enregistrée. Synchronisation cloud reportée (hors ligne)." + restartNote);
        }

        var auth = await AuthenticateCloudWithFallbackAsync(
            request.AdminUsername.Trim(),
            request.AdminPassword,
            cancellationToken);
        if (!auth.Success)
        {
            return new InitialSetupResult(
                LocalPersistenceOk: true,
                LocalDbPath: localDbLabel,
                RequiresAppRestart: requiresRestart,
                CloudSyncAttempted: true,
                CloudSyncSuccess: false,
                CloudSyncMessage: $"Configuration locale OK, mais authentification cloud échouée: {auth.Message}" + restartNote);
        }

        var sync = await _syncService.SyncAsync(manual: true, cancellationToken);
        return new InitialSetupResult(
            LocalPersistenceOk: true,
            LocalDbPath: localDbLabel,
            RequiresAppRestart: requiresRestart,
            CloudSyncAttempted: true,
            CloudSyncSuccess: sync.Success,
            CloudSyncMessage: (sync.Success
                ? $"Synchronisation cloud réussie ({sync.Pushed} envoyés, {sync.Pulled} reçus)."
                : $"Configuration locale OK, mais sync cloud échouée: {sync.Error ?? "erreur inconnue"}") + restartNote);
    }

    private static readonly string[] GhostBootstrapUsernames = ["admin", "admini", "admin2"];

    /// <summary>Retire les comptes techniques (admin, admini, admin2) sauf le compte choisi — libère leurs e-mails pour l'index unique.</summary>
    private async Task RemoveGhostBootstrapAccountsAsync(string chosenUsername, CancellationToken cancellationToken)
    {
        var chosen = chosenUsername.Trim().ToLowerInvariant();
        var ghosts = await _db.Users
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);

        var changed = false;
        foreach (var user in ghosts)
        {
            var name = user.Username.Trim().ToLowerInvariant();
            if (!GhostBootstrapUsernames.Contains(name) || name == chosen)
                continue;

            user.DeletedAt ??= DateTime.UtcNow;
            user.IsActive = false;
            user.Email = MakeArchivedEmail(user);
            user.MarkUpdated();
            changed = true;
        }

        if (changed)
            await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<User> FindOrCreateSetupAdminAsync(string adminUsername, CancellationToken cancellationToken)
    {
        var chosen = adminUsername.Trim().ToLowerInvariant();
        var existing = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Username.ToLower() == chosen, cancellationToken);

        if (existing is not null)
        {
            existing.DeletedAt = null;
            existing.IsActive = true;
            return existing;
        }

        var created = new User();
        _db.Users.Add(created);
        return created;
    }

    /// <summary>Réattribue l'e-mail des autres lignes (y compris supprimées) pour respecter IX_Users_Email.</summary>
    private async Task ReleaseUniqueEmailAsync(
        string email,
        Guid keepUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
            return;

        var normalized = email.Trim().ToLowerInvariant();
        var conflicts = await _db.Users
            .IgnoreQueryFilters()
            .Where(u => u.Id != keepUserId && u.Email.ToLower() == normalized)
            .ToListAsync(cancellationToken);

        if (conflicts.Count == 0)
            return;

        foreach (var user in conflicts)
        {
            user.Email = MakeArchivedEmail(user);
            if (GhostBootstrapUsernames.Contains(user.Username.Trim().ToLowerInvariant()))
            {
                user.DeletedAt ??= DateTime.UtcNow;
                user.IsActive = false;
            }

            user.MarkUpdated();
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string MakeArchivedEmail(User user)
    {
        var name = user.Username.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(name))
            name = "user";

        return $"{name}.archive.{user.Id:N}@sbms.local";
    }

    private async Task<string> ResolveAdminEmailAsync(
        InitialSetupRequest request,
        Guid adminId,
        CancellationToken cancellationToken)
    {
        var username = request.AdminUsername.Trim().ToLowerInvariant();
        var desired = request.CompanyEmail.Trim();

        if (string.IsNullOrWhiteSpace(desired))
            desired = $"{username}@sbms.local";

        var emailTaken = await _db.Users
            .IgnoreQueryFilters()
            .AnyAsync(
                u => u.Id != adminId && u.Email.ToLower() == desired.ToLower(),
                cancellationToken);

        if (!emailTaken)
            return desired;

        var fallback = $"{username}@sbms.local";
        if (fallback.Equals(desired, StringComparison.OrdinalIgnoreCase))
            return desired;

        var fallbackTaken = await _db.Users
            .IgnoreQueryFilters()
            .AnyAsync(
                u => u.Id != adminId && u.Email.ToLower() == fallback.ToLower(),
                cancellationToken);

        return fallbackTaken ? $"{username}.{Guid.NewGuid():N}@sbms.local" : fallback;
    }

    private async Task EnsureDatabaseReadyForSetupSaveAsync(CancellationToken cancellationToken)
    {
        if (!DesktopDatabaseInitializer.IsMySqlProvider(_db))
            return;

        try
        {
            if (!await _db.Database.CanConnectAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    _localDb.IsLanClient
                        ? $"Impossible d'accéder à MySQL sur le serveur {_localDb.ServerHost}."
                        : "Impossible d'accéder à MySQL sur ce PC. Démarrez MySQL dans XAMPP.");
            }

            await _db.Users.IgnoreQueryFilters().AnyAsync(cancellationToken);
        }
        catch (Exception ex) when (_localDb.IsLanClient && !_localDb.RunsSchemaMigrations)
        {
            throw new InvalidOperationException(
                "La base MySQL du serveur n'est pas encore prête pour ce poste client.\n\n" +
                "Sur le PC serveur (XAMPP) : lancez SBMS en mode « Serveur », terminez la configuration une première fois, puis réessayez ici.\n\n" +
                DbSaveExceptionTranslator.ToDetailedMessage(ex),
                ex);
        }
    }

    private static async Task<LocalDatabaseSetupSettings> ResolveDatabaseSettingsForSaveAsync(
        InitialSetupRequest request,
        CancellationToken cancellationToken)
    {
        var settings = request.ToLocalDatabaseSettings();
        if (!string.Equals(settings.DeploymentMode, "Client", StringComparison.OrdinalIgnoreCase))
            return settings;

        var section = BuildLocalDatabaseSection(settings);
        var discovered = await Task.Run(
            () => DesktopMySqlServerDiscovery.ResolveClientHost(section, settings.ServerHost?.Trim()),
            cancellationToken);

        if (discovered is null)
        {
            throw new InvalidOperationException(
                "Impossible de joindre le serveur MySQL. Vérifiez XAMPP sur le serveur, le réseau et le pare-feu (port 3306), " +
                "ou saisissez l'IP manuellement puis « Tester la connexion ».");
        }

        if (!string.Equals(settings.ServerHost, discovered, StringComparison.OrdinalIgnoreCase))
        {
            request.ServerHost = discovered;
            settings = request.ToLocalDatabaseSettings();
        }

        return settings;
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


    private async Task<(bool Success, string Message)> AuthenticateCloudAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        try
        {
            var baseUrl = (_configuration["Api:BaseUrl"] ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(baseUrl))
                return (false, "URL API cloud non configurée.");

            var token = await SmartBuilding.Infrastructure.Http.CloudApiAuth.TryLoginAsync(
                baseUrl, username, password, cancellationToken);
            if (string.IsNullOrWhiteSpace(token))
                return (false, "Identifiants cloud refusés (vérifiez admin / Admin@2026).");

            PersistApiToken(token);
            return (true, "Authentification cloud OK.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task<(bool Success, string Message)> AuthenticateCloudWithFallbackAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        var primary = await AuthenticateCloudAsync(username, password, cancellationToken);
        if (primary.Success)
            return primary;

        // Le portail cloud possède un compte bootstrap admin/admin.
        // Il sert uniquement à obtenir le JWT de synchronisation initiale.
        var bootstrap = await AuthenticateCloudAsync("admin", "admin", cancellationToken);
        if (bootstrap.Success)
            return (true, "Authentification cloud OK via compte bootstrap admin.");

        return (
            false,
            $"Compte local refusé ({primary.Message}); bootstrap admin/admin refusé ({bootstrap.Message}).");
    }

    private static void PersistApiToken(string token)
    {
        var folder = Path.GetDirectoryName(ApiTokenPath)!;
        Directory.CreateDirectory(folder);
        File.WriteAllText(ApiTokenPath, token.Trim());
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

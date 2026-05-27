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

    public InitialSetupService(
        SmartBuildingDbContext db,
        AppConfigurationService appConfiguration,
        ISyncService syncService,
        IConfiguration configuration)
    {
        _db = db;
        _appConfiguration = appConfiguration;
        _syncService = syncService;
        _configuration = configuration;
    }

    public async Task<bool> NeedsInitialSetupAsync(CancellationToken cancellationToken = default)
    {
        var users = await _db.Users
            .IgnoreQueryFilters()
            .Where(u => u.DeletedAt == null)
            .ToListAsync(cancellationToken);

        if (users.Count == 0)
            return true;

        // Cas legacy: ancien seed "admin/admin@smartbuilding.local" => on force l'onboarding pro.
        if (users.Count == 1)
        {
            var u = users[0];
            if (string.Equals(u.Username, "admin", StringComparison.OrdinalIgnoreCase)
                && string.Equals(u.Email, "admin@smartbuilding.local", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<InitialSetupResult> CompleteInitialSetupAsync(InitialSetupRequest request, CancellationToken cancellationToken = default)
    {
        var admin = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                u => u.DeletedAt == null
                     && u.Username.ToLower() == request.AdminUsername.Trim().ToLower(),
                cancellationToken);

        if (admin is null)
        {
            admin = new User();
            _db.Users.Add(admin);
        }

        admin.Username = request.AdminUsername.Trim();
        admin.FullName = request.AdminFullName.Trim();
        admin.Email = request.CompanyEmail.Trim();
        admin.Role = UserRole.Administrateur;
        admin.PasswordHash = AuthService.HashPassword(request.AdminPassword);
        admin.IsActive = true;
        admin.IsSynced = false;
        admin.MarkUpdated();

        var building = await _db.BuildingInfos.FirstOrDefaultAsync(cancellationToken) ?? new BuildingInfo();
        if (building.Id == Guid.Empty)
            _db.BuildingInfos.Add(building);

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
        building.Currency = "CDF";
        building.DateFormat = "dd/MM/yyyy";
        building.Language = "Français";
        building.TimeFormat = "24 heures";
        building.MaintenanceMode = false;
        building.MarkUpdated();

        await _db.SaveChangesAsync(cancellationToken);

        _appConfiguration.SaveAndApplyAppearance(
            request.ThemeMode,
            request.PrimaryColorHex,
            request.SidebarColorHex,
            request.SecondaryColorHex,
            compactTables: false,
            showKpiSparklines: true);

        WriteSetupCompletedFlag();

        var localDbPath = DesktopSqlitePaths.GetDatabaseFilePath();
        var localPersisted = await _db.Users.AnyAsync(
                                 u => u.DeletedAt == null
                                      && u.Username.ToLower() == request.AdminUsername.Trim().ToLower(),
                                 cancellationToken)
                             && await _db.BuildingInfos.AnyAsync(
                                 b => b.DeletedAt == null && b.Name == request.BuildingName.Trim(),
                                 cancellationToken)
                             && File.Exists(localDbPath);
        if (!localPersisted)
            throw new InvalidOperationException("Échec de persistance locale des données de configuration.");

        var online = await _syncService.IsOnlineAsync(cancellationToken);
        if (!online)
        {
            return new InitialSetupResult(
                LocalPersistenceOk: true,
                LocalDbPath: localDbPath,
                CloudSyncAttempted: false,
                CloudSyncSuccess: false,
                CloudSyncMessage: "Configuration locale enregistrée. Synchronisation cloud reportée (hors ligne).");
        }

        var auth = await AuthenticateCloudWithFallbackAsync(
            request.AdminUsername.Trim(),
            request.AdminPassword,
            cancellationToken);
        if (!auth.Success)
        {
            return new InitialSetupResult(
                LocalPersistenceOk: true,
                LocalDbPath: localDbPath,
                CloudSyncAttempted: true,
                CloudSyncSuccess: false,
                CloudSyncMessage: $"Configuration locale OK, mais authentification cloud échouée: {auth.Message}");
        }

        var sync = await _syncService.SyncAsync(manual: true, cancellationToken);
        return new InitialSetupResult(
            LocalPersistenceOk: true,
            LocalDbPath: localDbPath,
            CloudSyncAttempted: true,
            CloudSyncSuccess: sync.Success,
            CloudSyncMessage: sync.Success
                ? $"Synchronisation cloud réussie ({sync.Pushed} envoyés, {sync.Pulled} reçus)."
                : $"Configuration locale OK, mais sync cloud échouée: {sync.Error ?? "erreur inconnue"}");
    }

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
            if (!baseUrl.EndsWith("/"))
                baseUrl += "/";

            using var http = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(20)
            };
            var response = await http.PostAsJsonAsync(
                "api/auth/login/",
                new { username, password },
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                return (false, $"HTTP {(int)response.StatusCode}");

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!doc.RootElement.TryGetProperty("success", out var successEl) || !successEl.GetBoolean())
                return (false, "Réponse login invalide.");

            if (!doc.RootElement.TryGetProperty("data", out var dataEl)
                || !dataEl.TryGetProperty("token", out var tokenEl))
                return (false, "Token JWT absent dans la réponse.");

            var token = tokenEl.GetString();
            if (string.IsNullOrWhiteSpace(token))
                return (false, "Token JWT vide.");

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
}

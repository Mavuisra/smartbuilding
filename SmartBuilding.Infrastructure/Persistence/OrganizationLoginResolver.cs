using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Infrastructure.Services;
using SmartBuilding.Shared.DTOs.Auth;

namespace SmartBuilding.Infrastructure.Persistence;

public sealed record OrganizationLoginResult(
    bool Success,
    OrganizationEntry? Organization,
    LoginResponse? User,
    string Message)
{
    public static OrganizationLoginResult Ok(OrganizationEntry org, LoginResponse user) =>
        new(true, org, user, "");

    public static OrganizationLoginResult Fail(string message) =>
        new(false, null, null, message);
}

/// <summary>Résout l'organisation à partir du username et authentifie l'utilisateur.</summary>
public sealed class OrganizationLoginResolver
{
    private readonly GlobalUsernameRegistry _usernameRegistry;
    private readonly OrganizationRegistry _organizationRegistry;
    private readonly OrganizationConnectionResolver _connectionResolver;
    private readonly IConfiguration _configuration;

    public OrganizationLoginResolver(
        GlobalUsernameRegistry usernameRegistry,
        OrganizationRegistry organizationRegistry,
        OrganizationConnectionResolver connectionResolver,
        IConfiguration configuration)
    {
        _usernameRegistry = usernameRegistry;
        _organizationRegistry = organizationRegistry;
        _connectionResolver = connectionResolver;
        _configuration = configuration;
    }

    public async Task<OrganizationLoginResult> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var trimmed = (username ?? "").Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return OrganizationLoginResult.Fail("Veuillez saisir votre nom d'utilisateur.");

        if (string.IsNullOrWhiteSpace(password))
            return OrganizationLoginResult.Fail("Veuillez saisir votre mot de passe.");

        _organizationRegistry.ReloadFromDisk();

        var matches = await FindTenantMatchesAsync(trimmed, cancellationToken);
        if (matches.Count == 0)
        {
            _usernameRegistry.RebuildFromOrganizations(_organizationRegistry, _connectionResolver);
            matches = await FindTenantMatchesAsync(trimmed, cancellationToken);
        }

        if (matches.Count == 0)
        {
            return OrganizationLoginResult.Fail(
                BuildUnknownUserMessage(trimmed));
        }

        OrganizationEntry? authenticatedOrg = null;
        LoginResponse? authenticatedUser = null;

        foreach (var org in matches)
        {
            var result = await TryAuthenticateOnOrganizationAsync(
                org, trimmed, password, cancellationToken);

            if (result is null)
                continue;

            if (authenticatedOrg is not null)
            {
                return OrganizationLoginResult.Fail(
                    "Cet identifiant existe dans plusieurs organisations avec le même mot de passe.\n\n" +
                    "Contactez l'administrateur pour corriger ce conflit.");
            }

            authenticatedOrg = org;
            authenticatedUser = result;
        }

        if (authenticatedOrg is null || authenticatedUser is null)
        {
            var orgNames = string.Join(", ", matches.Select(o => o.Name));
            return OrganizationLoginResult.Fail(
                $"Mot de passe incorrect pour « {trimmed} ».\n\n" +
                $"Organisation(s) concernée(s) : {orgNames}.\n" +
                "Vérifiez le mot de passe du tenant où ce compte a été créé.");
        }

        _organizationRegistry.SetActive(authenticatedOrg.Id);
        _usernameRegistry.Register(trimmed, authenticatedOrg.Id);
        return OrganizationLoginResult.Ok(authenticatedOrg, authenticatedUser);
    }

    private async Task<IReadOnlyList<OrganizationEntry>> FindTenantMatchesAsync(
        string username,
        CancellationToken cancellationToken)
    {
        return await _usernameRegistry.FindOrganizationsWithUsernameAsync(
            username,
            _organizationRegistry,
            _connectionResolver,
            cancellationToken);
    }

    private string BuildUnknownUserMessage(string username)
    {
        var tenants = _organizationRegistry.Organizations;
        if (tenants.Count == 0)
        {
            return "Aucun tenant configuré sur ce poste.\n\n" +
                   "Créez un tenant ou vérifiez la configuration MySQL (XAMPP).";
        }

        var tenantList = string.Join("\n", tenants.Select(o => $"• {o.Name} ({o.DatabaseName})"));
        return "Aucun compte trouvé pour cet identifiant.\n\n" +
               $"Identifiant saisi : « {username} »\n\n" +
               "Tenants enregistrés sur ce poste :\n" +
               $"{tenantList}\n\n" +
               "Utilisez l'identifiant exact créé dans le tenant concerné " +
               "(ex. adminbloomer pour bloomer, admin12 pour l'organisation principale).";
    }

    private async Task<LoginResponse?> TryAuthenticateOnOrganizationAsync(
        OrganizationEntry org,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        var connectionString = _connectionResolver.BuildConnectionString(org.Id);
        await using var db = CreateDbContext(connectionString);
        IAuthService auth = new AuthService(db, _configuration);
        return await auth.LoginAsync(
            new LoginRequest { Username = username, Password = password },
            cancellationToken);
    }

    private static SmartBuildingDbContext CreateDbContext(string connectionString)
    {
        var serverVersion = ServerVersion.Parse("8.0.36-mysql");
        var options = new DbContextOptionsBuilder<SmartBuildingDbContext>()
            .UseMySql(connectionString, serverVersion, mySql => mySql.EnableStringComparisonTranslations())
            .Options;
        return new SmartBuildingDbContext(options);
    }
}

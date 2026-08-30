using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using SmartBuilding.Infrastructure.Persistence;

namespace SmartBuilding.Infrastructure.Services;

/// <summary>
/// Vérifie les composants requis avant le premier lancement (nouvelle machine).
/// </summary>
public static class DesktopPrerequisiteChecker
{
    public const string DotNetDownloadUrl = "https://dotnet.microsoft.com/fr-fr/download/dotnet/8.0";
    public const string XamppDownloadUrl = "https://www.apachefriends.org/fr/download.html";
    public const string XamppControlPanelHint = "XAMPP Control Panel → bouton « Start » sur la ligne MySQL.";

    private static readonly string[] XamppCandidateRoots =
    [
        @"C:\xampp",
        @"D:\xampp",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "xampp"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "xampp"),
    ];

    public static PrerequisiteCheckResult Evaluate(IConfiguration configuration)
    {
        var section = configuration.GetSection(DesktopLocalDatabaseConfig.SectionName);
        var modeRaw = section.GetValue<string>("DeploymentMode") ?? nameof(DesktopDatabaseDeploymentMode.Server);
        if (!Enum.TryParse<DesktopDatabaseDeploymentMode>(modeRaw, ignoreCase: true, out var deploymentMode))
            deploymentMode = DesktopDatabaseDeploymentMode.Server;

        var modeLabel = deploymentMode switch
        {
            DesktopDatabaseDeploymentMode.Server => "Serveur (base MySQL sur ce PC)",
            DesktopDatabaseDeploymentMode.Client => "Poste client (MySQL sur un autre PC)",
            _ => "Autonome (MySQL local via XAMPP)",
        };

        var items = new List<PrerequisiteStatus> { BuildDotNetStatus() };

        switch (deploymentMode)
        {
            case DesktopDatabaseDeploymentMode.Client:
                items.Add(BuildClientServerStatus(section));
                break;
            default:
                items.AddRange(BuildLocalMySqlStatuses(section));
                break;
        }

        return new PrerequisiteCheckResult
        {
            Items = items,
            DeploymentModeLabel = modeLabel,
        };
    }

    private static PrerequisiteStatus BuildDotNetStatus()
    {
        var version = Environment.Version;
        var ok = version.Major >= 8;
        return new PrerequisiteStatus
        {
            Kind = PrerequisiteKind.DotNetRuntime,
            Title = ".NET 8 Desktop Runtime",
            Summary = ok
                ? $"Installé (version {version.Major}.{version.Minor}.{version.Build})."
                : $"Version détectée : {version} — .NET 8 ou supérieur requis.",
            Instructions = ok
                ? "Aucune action requise."
                : "Téléchargez « .NET Desktop Runtime 8.x » pour Windows x64, installez-le, puis relancez Smart Building.",
            DownloadLabel = ok ? null : "Télécharger .NET 8 Desktop Runtime",
            DownloadUrl = ok ? null : DotNetDownloadUrl,
            IsSatisfied = ok,
        };
    }

    private static IEnumerable<PrerequisiteStatus> BuildLocalMySqlStatuses(IConfigurationSection section)
    {
        var port = section.GetValue<int?>("MySqlPort") ?? 3306;
        var xamppRoot = FindXamppRoot();
        var portOpen = IsTcpPortOpen("127.0.0.1", port);
        var connectionString = DesktopMySqlConnectionBuilder.Build(section, "127.0.0.1");
        var canConnect = portOpen && DesktopLocalDatabaseBootstrap.CanConnectToMySql(connectionString);

        yield return new PrerequisiteStatus
        {
            Kind = PrerequisiteKind.XamppMySql,
            Title = "XAMPP (MySQL / MariaDB)",
            Summary = xamppRoot is not null
                ? $"Installation détectée : {xamppRoot}"
                : "XAMPP non détecté sur ce PC.",
            Instructions = xamppRoot is not null
                ? "XAMPP est installé. Assurez-vous que le service MySQL est démarré (voir étape suivante)."
                : "Installez XAMPP pour Windows (PHP n'est pas utilisé par SBMS — seul MySQL est nécessaire). "
                  + "Après installation, ouvrez le XAMPP Control Panel et démarrez MySQL.",
            DownloadLabel = xamppRoot is null ? "Télécharger XAMPP pour Windows" : null,
            DownloadUrl = xamppRoot is null ? XamppDownloadUrl : null,
            IsSatisfied = xamppRoot is not null,
        };

        yield return new PrerequisiteStatus
        {
            Kind = PrerequisiteKind.MySqlService,
            Title = "Service MySQL démarré",
            Summary = canConnect
                ? $"Connexion MySQL OK sur 127.0.0.1:{port}."
                : portOpen
                    ? $"Le port {port} répond, mais la connexion à la base a échoué (identifiants ou base)."
                    : $"MySQL ne répond pas sur 127.0.0.1:{port}.",
            Instructions = canConnect
                ? "Aucune action requise."
                : portOpen
                    ? "Vérifiez dans appsettings.json : User, Password et Database (sbms_local). "
                      + "Par défaut XAMPP utilise l'utilisateur root sans mot de passe."
                    : $"Ouvrez le XAMPP Control Panel sur ce PC.\n{XamppControlPanelHint}\n"
                      + "Attendez que la ligne MySQL affiche « Running », puis cliquez sur « Vérifier à nouveau ».",
            DownloadLabel = null,
            DownloadUrl = null,
            IsSatisfied = canConnect,
        };
    }

    private static PrerequisiteStatus BuildClientServerStatus(IConfigurationSection section)
    {
        var configuredHost = section.GetValue<string>("ServerHost")?.Trim();
        var port = section.GetValue<int?>("MySqlPort") ?? 3306;

        if (string.IsNullOrWhiteSpace(configuredHost))
        {
            return new PrerequisiteStatus
            {
                Kind = PrerequisiteKind.NetworkInfo,
                Title = "Serveur MySQL distant",
                Summary = "Aucune adresse serveur configurée pour l'instant.",
                Instructions = "En mode client, la base MySQL est sur le PC serveur (XAMPP). "
                               + "Vous pourrez saisir l'adresse IP du serveur lors de la configuration initiale. "
                               + "Assurez-vous que le PC serveur a MySQL démarré et que le port 3306 est autorisé sur le réseau.",
                IsSatisfied = true,
                IsOptional = true,
            };
        }

        var portOpen = IsTcpPortOpen(configuredHost, port);
        var connectionString = DesktopMySqlConnectionBuilder.Build(section, configuredHost);
        var canConnect = portOpen && DesktopLocalDatabaseBootstrap.CanConnectToMySql(connectionString);

        return new PrerequisiteStatus
        {
            Kind = PrerequisiteKind.MySqlServerReachable,
            Title = $"Serveur MySQL ({configuredHost})",
            Summary = canConnect
                ? $"Connexion réussie vers {configuredHost}:{port}."
                : portOpen
                    ? $"Le serveur répond sur le port {port}, mais la connexion MySQL a échoué."
                    : $"Impossible de joindre {configuredHost}:{port}.",
            Instructions = canConnect
                ? "Aucune action requise."
                : "Sur le PC serveur : démarrez MySQL dans XAMPP et terminez la configuration SBMS en mode « Serveur ». "
                  + "Vérifiez l'adresse IP, le pare-feu Windows (port 3306) et les identifiants dans appsettings.json.",
            DownloadLabel = canConnect ? null : "Guide XAMPP (PC serveur)",
            DownloadUrl = canConnect ? null : XamppDownloadUrl,
            IsSatisfied = canConnect,
        };
    }

    public static string? FindXamppRoot()
    {
        var fromEnv = Environment.GetEnvironmentVariable("XAMPP_HOME");
        if (!string.IsNullOrWhiteSpace(fromEnv) && Directory.Exists(fromEnv))
            return fromEnv.Trim();

        foreach (var root in XamppCandidateRoots)
        {
            if (!Directory.Exists(root))
                continue;
            if (File.Exists(Path.Combine(root, "mysql", "bin", "mysqld.exe"))
                || File.Exists(Path.Combine(root, "xampp-control.exe")))
                return root;
        }

        return null;
    }

    public static bool IsTcpPortOpen(string host, int port, int timeoutMs = 2500)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            if (!connectTask.Wait(timeoutMs))
                return false;
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }
}

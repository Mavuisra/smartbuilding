using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Windows;

namespace SmartBuilding.Desktop.WPF.Services;

public static class AppAutoUpdater
{
    // GitHub repo source des mises à jour.
    // Si tu changes d’orga/repo, mets ici les nouvelles valeurs.
    private const string GitHubOwner = "Mavuisra";
    private const string GitHubRepo = "smartbuilding";

    private const string ApplyFlag = "--apply-update";
    private const string VersionPrefixTrim = "v";

    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(20);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static bool TryApplyUpdateIfRequested(string[] args)
    {
        var idx = Array.IndexOf(args, ApplyFlag);
        if (idx < 0)
            return false;

        // Format attendu:
        // --apply-update "<installDir>" "<stagingDir>" "<exeName>"
        if (args.Length <= idx + 3)
            return false;

        var installDir = args[idx + 1];
        var stagingDir = args[idx + 2];
        var exeName = args[idx + 3];

        try
        {
            if (!Directory.Exists(installDir))
                throw new DirectoryNotFoundException(installDir);
            if (!Directory.Exists(stagingDir))
                throw new DirectoryNotFoundException(stagingDir);

            // Le zip peut contenir un dossier enveloppe. On cherche le vrai dossier racine contenant l’EXE.
            var exePath = FindExeInDirectory(stagingDir, exeName);
            var stagingRoot = exePath is null ? stagingDir : Path.GetDirectoryName(exePath) ?? stagingDir;

            // Copie sans supprimer ce qui n’existe pas dans le package (ex: smartbuilding.db).
            foreach (var sourceFile in Directory.EnumerateFiles(stagingRoot, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(stagingRoot, sourceFile);
                var destFile = Path.Combine(installDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                CopyFileWithRetry(sourceFile, destFile, overwrite: true, retries: 8, delayMs: 200);
            }

            // Redémarrage sur la version installée.
            var targetExe = Path.Combine(installDir, exeName);
            if (File.Exists(targetExe))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetExe,
                    WorkingDirectory = installDir,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            TryWriteErrorLog(ex);
        }
        finally
        {
            // On coupe immédiatement pour éviter que l’applier continue son démarrage WPF.
            System.Windows.Application.Current?.Dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown());
            Environment.Exit(0);
        }

        return true;
    }

    private static void CopyFileWithRetry(string sourceFile, string destFile, bool overwrite, int retries, int delayMs)
    {
        for (var attempt = 0; attempt <= retries; attempt++)
        {
            try
            {
                File.Copy(sourceFile, destFile, overwrite);
                return;
            }
            catch (IOException) when (attempt < retries)
            {
                Thread.Sleep(delayMs);
            }
            catch (UnauthorizedAccessException) when (attempt < retries)
            {
                Thread.Sleep(delayMs);
            }
        }

        // Dernier essai sans protection
        File.Copy(sourceFile, destFile, overwrite);
    }

    public static async Task<bool> CheckAndApplyIfNeededAsync(
        Action<double, string> reportProgress,
        Func<string, string, Task<bool>> confirmUpdateAsync,
        CancellationToken ct = default)
    {
        try
        {
            // Anti-spam: limite les checks à ~1 fois par heure par machine.
            if (!ShouldCheckNow())
                return false;

            MarkLastCheckNow();

            var localVersion = GetLocalNormalizedVersion();
            var installDir = AppContext.BaseDirectory;
            var exeName = GetEntryExeName();

            using var http = new HttpClient { Timeout = HttpTimeout };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("SmartBuilding-AutoUpdater");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            reportProgress(10, "Recherche des mises à jour...");

            var latest = await GetLatestReleaseAsync(http, ct);
            if (latest is null || string.IsNullOrWhiteSpace(latest.TagName))
                return false;

            var remoteVersion = NormalizeVersion(latest.TagName);
            if (string.Equals(remoteVersion, localVersion, StringComparison.OrdinalIgnoreCase))
                return false;

            reportProgress(25, $"Mise à jour disponible: {latest.TagName}");
            var shouldApply = await confirmUpdateAsync(localVersion, latest.TagName);
            if (!shouldApply)
                return false;

            // Sélection de l’asset zip correspondant au desktop win-x64.
            var asset = SelectUpdateZip(latest);
            if (asset is null || string.IsNullOrWhiteSpace(asset.DownloadUrl))
                throw new InvalidOperationException("Aucun asset zip de mise à jour trouvé dans la release GitHub.");

            var updateRoot = Path.Combine(Path.GetTempPath(), "SmartBuilding", "updates", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(updateRoot);

            var zipPath = Path.Combine(updateRoot, "update.zip");
            reportProgress(40, "Téléchargement de la mise à jour...");

            await DownloadFileAsync(http, asset.DownloadUrl, zipPath, ct);

            var stagingDir = Path.Combine(updateRoot, "staging");
            reportProgress(65, "Extraction de la mise à jour...");
            ZipFile.ExtractToDirectory(zipPath, stagingDir);

            // L’applier tourne depuis le dossier staging (donc pas verrouillé par l’app courante).
            var applierExe = FindExeInDirectory(stagingDir, exeName)
                              ?? throw new FileNotFoundException("EXE d’appliquer introuvable dans le staging.", exeName);

            reportProgress(80, "Application: redémarrage...");

            Process.Start(new ProcessStartInfo
            {
                FileName = applierExe,
                Arguments = $"{ApplyFlag} \"{installDir}\" \"{stagingDir}\" \"{exeName}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            // L’applier redémarre le programme, on coupe l’instance courante.
            return true;
        }
        catch (Exception ex)
        {
            TryWriteErrorLog(ex);
            return false;
        }
    }

    private static async Task<GithubRelease?> GetLatestReleaseAsync(HttpClient http, CancellationToken ct)
    {
        var url = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
        var json = await http.GetStringAsync(url, ct);
        return JsonSerializer.Deserialize<GithubRelease>(json, JsonOptions);
    }

    private static GithubAsset? SelectUpdateZip(GithubRelease latest)
    {
        // Contrat côté build workflow: zip de mise à jour contient "smartbuilding-desktop-win-x64" et se termine par .zip.
        var candidates = latest.Assets?
            .Where(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .ToList() ?? [];

        var match = candidates.FirstOrDefault(a =>
            a.Name.Contains("smartbuilding-desktop-win-x64", StringComparison.OrdinalIgnoreCase));

        return match ?? candidates.FirstOrDefault();
    }

    private static async Task DownloadFileAsync(HttpClient http, string url, string destPath, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = File.Create(destPath);

        await contentStream.CopyToAsync(fileStream, ct);
    }

    private static string GetLocalNormalizedVersion()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version
            ?? Assembly.GetExecutingAssembly().GetName().Version;

        return NormalizeVersion(version?.ToString() ?? "");
    }

    private static string NormalizeVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return "";

        var v = version.Trim();
        if (v.StartsWith(VersionPrefixTrim, StringComparison.OrdinalIgnoreCase))
            v = v[VersionPrefixTrim.Length..];

        // Local assembly version peut être au format "1.0.123.0"
        // Remote tag peut être "v1.0.123". On normalise en retirant le ".0" final répétitif.
        while (v.EndsWith(".0", StringComparison.OrdinalIgnoreCase))
        {
            var parts = v.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 3)
                break;

            v = v[..^2]; // retire ".0"
        }

        return v;
    }

    private static bool ShouldCheckNow()
    {
        try
        {
            var stateDir = GetStateDir();
            var stampPath = Path.Combine(stateDir, "last-check.txt");

            if (!File.Exists(stampPath))
                return true;

            var lastTicks = long.Parse(File.ReadAllText(stampPath));
            var lastUtc = DateTimeOffset.FromUnixTimeMilliseconds(lastTicks).UtcDateTime;
            return (DateTime.UtcNow - lastUtc) > TimeSpan.FromHours(1);
        }
        catch
        {
            return true;
        }
    }

    private static void MarkLastCheckNow()
    {
        try
        {
            var stateDir = GetStateDir();
            Directory.CreateDirectory(stateDir);
            var stampPath = Path.Combine(stateDir, "last-check.txt");
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            File.WriteAllText(stampPath, nowMs.ToString());
        }
        catch
        {
            // ignore
        }
    }

    private static void TryWriteErrorLog(Exception ex)
    {
        try
        {
            var stateDir = GetStateDir();
            Directory.CreateDirectory(stateDir);
            File.WriteAllText(Path.Combine(stateDir, "update-error.log"), ex.ToString());
        }
        catch
        {
            // ignore
        }
    }

    private static string GetStateDir()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "SmartBuilding");
    }

    private static string GetEntryExeName()
    {
        var location = Assembly.GetEntryAssembly()?.Location;
        if (!string.IsNullOrWhiteSpace(location))
            return Path.GetFileName(location);

        // fallback raisonnable
        return "SmartBuilding.Desktop.WPF.exe";
    }

    private static string? FindExeInDirectory(string dir, string exeName)
    {
        // La plupart du temps l’exe est à la racine, mais on tolère un dossier enveloppe.
        var direct = Path.Combine(dir, exeName);
        if (File.Exists(direct))
            return direct;

        foreach (var file in Directory.EnumerateFiles(dir, exeName, SearchOption.AllDirectories))
            return file;

        return null;
    }

    private record GithubRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("assets")] List<GithubAsset>? Assets);

    private record GithubAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string DownloadUrl);
}


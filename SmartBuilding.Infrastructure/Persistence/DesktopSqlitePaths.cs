namespace SmartBuilding.Infrastructure.Persistence;

/// <summary>
/// Emplacement durable de la base SQLite desktop (hors dossier d'installation / MAJ).
/// </summary>
public static class DesktopSqlitePaths
{
    private static readonly object InitLock = new();
    private static bool _initialized;

    public static string DataDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SBMS",
            "data");

    public static string DatabaseFilePath => Path.Combine(DataDirectory, "smartbuilding.db");

    public static string ConnectionString => $"Data Source={DatabaseFilePath}";

    public static void EnsureInitialized()
    {
        lock (InitLock)
        {
            if (_initialized)
                return;

            Directory.CreateDirectory(DataDirectory);
            MigrateLegacyDatabaseIfNeeded();
            _initialized = true;
        }
    }

    public static string GetDatabaseFilePath()
    {
        EnsureInitialized();
        return DatabaseFilePath;
    }

    /// <summary>Réinitialise le flag interne après suppression du fichier .db (nouveau EnsureInitialized).</summary>
    public static void ResetInitializationState()
    {
        lock (InitLock)
        {
            _initialized = false;
        }
    }

    private static void MigrateLegacyDatabaseIfNeeded()
    {
        if (File.Exists(DatabaseFilePath))
            return;

        foreach (var legacy in GetLegacyCandidates())
        {
            if (!File.Exists(legacy))
                continue;

            TryMigrateFrom(legacy);
            if (File.Exists(DatabaseFilePath))
                return;
        }
    }

    private static IEnumerable<string> GetLegacyCandidates()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "smartbuilding.db");
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SBMS",
            "smartbuilding.db");
    }

    private static void TryMigrateFrom(string legacyPath)
    {
        try
        {
            MigrateDatabaseSet(legacyPath, DatabaseFilePath);
            WriteMigrationLog($"Base migrée : {legacyPath} → {DatabaseFilePath}");
        }
        catch (Exception ex)
        {
            WriteMigrationLog($"Échec migration depuis {legacyPath} : {ex.Message}");
        }
    }

    private static void MigrateDatabaseSet(string sourceDb, string destDb)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destDb)!);
        MigrateSidecarFile(sourceDb, destDb);
        MigrateSidecarFile(sourceDb + "-wal", destDb + "-wal");
        MigrateSidecarFile(sourceDb + "-shm", destDb + "-shm");
    }

    private static void MigrateSidecarFile(string source, string destination)
    {
        if (!File.Exists(source) || File.Exists(destination))
            return;

        try
        {
            File.Move(source, destination);
        }
        catch (IOException)
        {
            File.Copy(source, destination, overwrite: false);
        }
    }

    private static void WriteMigrationLog(string message)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SBMS");
            Directory.CreateDirectory(logDir);
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}";
            File.AppendAllText(Path.Combine(logDir, "database-migration.log"), line);
        }
        catch
        {
            // Ne pas bloquer le démarrage si le journal échoue.
        }
    }
}

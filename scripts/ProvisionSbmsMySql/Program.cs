using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Services;

static string? FindAppsettingsPath()
{
    var dir = AppContext.BaseDirectory;
    for (var i = 0; i < 10; i++)
    {
        var candidate = Path.Combine(dir, "SmartBuilding.Desktop.WPF", "appsettings.json");
        if (File.Exists(candidate))
            return candidate;
        var parent = Directory.GetParent(dir);
        if (parent is null)
            break;
        dir = parent.FullName;
    }

    return null;
}

var appsettings = FindAppsettingsPath()
                  ?? throw new FileNotFoundException("appsettings.json introuvable (SmartBuilding.Desktop.WPF).");

var config = new ConfigurationBuilder()
    .AddJsonFile(appsettings, optional: false)
    .Build();

var localDb = DesktopLocalDatabaseBootstrap.Resolve(config);
if (!localDb.IsMySql)
{
    Console.WriteLine("ERREUR : Provider actuel = SQLite. Mettez LocalDatabase:Provider à MySql dans appsettings.json");
    return 1;
}

Console.WriteLine($"Connexion : {localDb.DisplayLabel}");
DesktopLocalDatabaseBootstrap.EnsureMySqlDatabaseExists(localDb.ConnectionString);
Console.WriteLine("Base sbms_local créée ou déjà présente.");

var options = new DbContextOptionsBuilder<SmartBuildingDbContext>()
    .UseMySql(localDb.ConnectionString, ServerVersion.Parse("8.0.36-mysql"))
    .Options;

await using var db = new SmartBuildingDbContext(options);
await DesktopDatabaseInitializer.InitializeAsync(db, localDb);
await DatabaseSeeder.SeedAsync(db);

var connection = db.Database.GetDbConnection();
await connection.OpenAsync();
await using var countCmd = connection.CreateCommand();
countCmd.CommandText =
    "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE()";
var tables = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

Console.WriteLine($"OK — migrations appliquées. Tables dans sbms_local : {tables}");
Console.WriteLine("Actualisez phpMyAdmin (F5) : la base « sbms_local » doit apparaître.");
return 0;

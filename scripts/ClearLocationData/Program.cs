using Microsoft.Data.Sqlite;

var purgeAll = args.Any(a => a.Equals("--purge-all", StringComparison.OrdinalIgnoreCase) ||
                            a.Equals("--all", StringComparison.OrdinalIgnoreCase));
var clearDocuments = args.Any(a => a.Equals("--documents", StringComparison.OrdinalIgnoreCase));
var clearExpenses = args.Any(a => a.Equals("--expenses", StringComparison.OrdinalIgnoreCase));
var clearTechnique = args.Any(a => a.Equals("--technique", StringComparison.OrdinalIgnoreCase));
var fixRentLedger = args.Any(a => a.Equals("--fix-rent-ledger", StringComparison.OrdinalIgnoreCase));
var analyze = args.Any(a => a.Equals("--analyze", StringComparison.OrdinalIgnoreCase));
var dbPath = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.OrdinalIgnoreCase));
if (string.IsNullOrWhiteSpace(dbPath))
{
    var candidates = new[]
    {
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SmartBuilding.Desktop.WPF", "bin", "Debug", "net8.0-windows", "smartbuilding.db")),
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "smartbuilding.db")),
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "SmartBuilding.Desktop.WPF", "bin", "Debug", "net8.0-windows", "smartbuilding.db")),
        Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SBMS", "smartbuilding.db"))
    };

    dbPath = candidates.FirstOrDefault(File.Exists);
}

if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
{
    Console.WriteLine("Base SQLite introuvable.");
    Console.WriteLine("Fermez SBMS, puis exécutez :");
    Console.WriteLine("  dotnet run --project scripts/ClearLocationData -- \"C:\\chemin\\vers\\smartbuilding.db\"");
    return 1;
}

Console.WriteLine($"Base : {dbPath}");

if (purgeAll)
{
  var totalPurged = await PurgeAllTablesAsync(dbPath);
  Console.WriteLine();
  Console.WriteLine($"Purge complète terminée : {totalPurged} enregistrement(s) supprimé(s).");
  Console.WriteLine("Fermez SBMS avant la purge. Au prochain démarrage, un compte admin par défaut sera recréé (admin / Admin@2026) si aucun utilisateur n'existe.");
  return 0;
}

if (analyze || clearTechnique)
{
    await using var connDiag = new SqliteConnection($"Data Source={dbPath}");
    await connDiag.OpenAsync();

    static async Task<decimal> SumAsync(SqliteConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var o = await cmd.ExecuteScalarAsync();
        return o is null or DBNull ? 0 : Convert.ToDecimal(o);
    }

    static async Task<long> CountAsync(SqliteConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    var maintCost = await SumAsync(connDiag, "SELECT COALESCE(SUM(Cost),0) FROM MaintenanceRecords");
    var maintCount = await CountAsync(connDiag, "SELECT COUNT(*) FROM MaintenanceRecords");
    var purchaseTotal = await SumAsync(connDiag, "SELECT COALESCE(SUM(PurchaseValue),0) FROM Equipment");
    var rentLedger = await SumAsync(connDiag, "SELECT COALESCE(SUM(Amount),0) FROM FinancialTransactions WHERE Type=1 AND Category='Loyers'");
    var rentPayments = await SumAsync(connDiag, "SELECT COALESCE(SUM(AmountPaid),0) FROM RentPayments");
    var rentMonth = await SumAsync(connDiag,
        $"SELECT COALESCE(SUM(AmountPaid),0) FROM RentPayments WHERE Year={DateTime.Today.Year} AND Month={DateTime.Today.Month}");
    var techDep = await SumAsync(connDiag,
        "SELECT COALESCE(SUM(Amount),0) FROM FinancialTransactions WHERE Type=2 AND (Source LIKE '%Technique%' OR Category LIKE '%Maintenance%')");
    var allDep = await SumAsync(connDiag, "SELECT COALESCE(SUM(Amount),0) FROM FinancialTransactions WHERE Type=2");

    Console.WriteLine("--- Diagnostic financier ---");
    Console.WriteLine($"  MaintenanceRecords : {maintCount} fiche(s), coût total {maintCost:N0} FC");
    Console.WriteLine($"  Valeur d'achat équipements (démo, pas trésorerie) : {purchaseTotal:N0} FC");
    Console.WriteLine($"  Loyers en ledger (peut être erroné) : {rentLedger:N0} FC");
    Console.WriteLine($"  Loyers réels (RentPayments) : {rentPayments:N0} FC");
    Console.WriteLine($"  Loyers ce mois : {rentMonth:N0} FC");
    Console.WriteLine($"  Dépenses Technique (ledger) : {techDep:N0} FC");
    Console.WriteLine($"  Toutes dépenses (ledger) : {allDep:N0} FC");
    Console.WriteLine($"  Disponible théorique (loyers - dépenses) : {rentLedger - allDep:N0} FC");

    if (!clearTechnique)
        return 0;
}

if (clearTechnique)
{
    await using var connTech = new SqliteConnection($"Data Source={dbPath}");
    await connTech.OpenAsync();

    await using (var fkOff = connTech.CreateCommand())
    {
        fkOff.CommandText = "PRAGMA foreign_keys=OFF;";
        await fkOff.ExecuteNonQueryAsync();
    }

    await using var cmdMaint = connTech.CreateCommand();
    cmdMaint.CommandText = "DELETE FROM \"MaintenanceRecords\"";
    var nMaint = await cmdMaint.ExecuteNonQueryAsync();
    Console.WriteLine($"  MaintenanceRecords : {nMaint} ligne(s)");

    await using var cmdDep = connTech.CreateCommand();
    cmdDep.CommandText =
        "DELETE FROM \"FinancialTransactions\" WHERE \"Type\" = 2 AND (\"Source\" LIKE '%Technique%' OR \"Category\" LIKE '%Maintenance%')";
    var nDep = await cmdDep.ExecuteNonQueryAsync();
    Console.WriteLine($"  FinancialTransactions (dépenses technique) : {nDep} ligne(s)");

    await using (var fkOn = connTech.CreateCommand())
    {
        fkOn.CommandText = "PRAGMA foreign_keys=ON;";
        await fkOn.ExecuteNonQueryAsync();
    }

    Console.WriteLine($"Total supprimé (technique) : {nMaint + nDep} enregistrement(s).");
    return 0;
}

if (fixRentLedger)
{
    await using var connFix = new SqliteConnection($"Data Source={dbPath}");
    await connFix.OpenAsync();
    await using (var fkOff = connFix.CreateCommand())
    {
        fkOff.CommandText = "PRAGMA foreign_keys=OFF;";
        await fkOff.ExecuteNonQueryAsync();
    }

    await using var cmdDel = connFix.CreateCommand();
    cmdDel.CommandText = "DELETE FROM \"FinancialTransactions\" WHERE \"Type\" = 1 AND \"Category\" = 'Loyers'";
    var nDel = await cmdDel.ExecuteNonQueryAsync();
    Console.WriteLine($"  Écritures Loyers supprimées : {nDel}");
    Console.WriteLine("  Relancez SBMS : le ledger sera recréé depuis Locations (RentPayments).");
    await using (var fkOn = connFix.CreateCommand())
    {
        fkOn.CommandText = "PRAGMA foreign_keys=ON;";
        await fkOn.ExecuteNonQueryAsync();
    }

    return 0;
}

if (clearExpenses)
{
    await using var connExp = new SqliteConnection($"Data Source={dbPath}");
    await connExp.OpenAsync();

    await using (var fkOff = connExp.CreateCommand())
    {
        fkOff.CommandText = "PRAGMA foreign_keys=OFF;";
        await fkOff.ExecuteNonQueryAsync();
    }

    await using var cmdExp = connExp.CreateCommand();
    // TransactionType.Depense = 2
    cmdExp.CommandText = "DELETE FROM \"FinancialTransactions\" WHERE \"Type\" = 2";
    var nExp = await cmdExp.ExecuteNonQueryAsync();
    Console.WriteLine($"  FinancialTransactions (dépenses) : {nExp} ligne(s)");
    Console.WriteLine($"Total supprimé : {nExp} enregistrement(s).");

    await using (var fkOn = connExp.CreateCommand())
    {
        fkOn.CommandText = "PRAGMA foreign_keys=ON;";
        await fkOn.ExecuteNonQueryAsync();
    }

    return 0;
}

var tables = clearDocuments
    ? new[]
    {
        "RentPayments",
        "LeaseGuarantees",
        "LeaseContracts",
        "TenantActivities",
        "SupplierPayments",
        "SupplierContracts",
        "FinancialTransactions",
        "SalaryPayments",
        "DisciplinaryNotes",
        "Attendances",
        "Employees",
        "IncidentInterventions",
        "Incidents",
        "RepairRecords",
        "MaintenanceRecords",
        "Equipment",
        "InventoryMaintenanceRecords",
        "InventoryItems",
        "ConsumptionRecords",
        "CachedEmails"
    }
    : new[]
    {
        "RentPayments",
        "LeaseGuarantees",
        "LeaseContracts",
        "TenantActivities",
        "Premises",
        "Tenants",
        "Buildings"
    };

await using var conn = new SqliteConnection($"Data Source={dbPath}");
await conn.OpenAsync();

await using (var fkOff = conn.CreateCommand())
{
    fkOff.CommandText = "PRAGMA foreign_keys=OFF;";
    await fkOff.ExecuteNonQueryAsync();
}

var total = 0;
foreach (var table in tables)
{
    await using var check = conn.CreateCommand();
    check.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name";
    check.Parameters.AddWithValue("$name", table);
    var exists = Convert.ToInt32(await check.ExecuteScalarAsync()) > 0;
    if (!exists)
    {
        Console.WriteLine($"  {table} : (table absente)");
        continue;
    }

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = $"DELETE FROM \"{table}\"";
    var n = await cmd.ExecuteNonQueryAsync();
    total += n;
    Console.WriteLine($"  {table} : {n} ligne(s)");
}

await using (var fkOn = conn.CreateCommand())
{
    fkOn.CommandText = "PRAGMA foreign_keys=ON;";
    await fkOn.ExecuteNonQueryAsync();
}

Console.WriteLine($"Total supprimé : {total} enregistrement(s).");
return 0;

static async Task<int> PurgeAllTablesAsync(string dbPath)
{
  await using var conn = new SqliteConnection($"Data Source={dbPath}");
  await conn.OpenAsync();

  await using (var fkOff = conn.CreateCommand())
  {
    fkOff.CommandText = "PRAGMA foreign_keys=OFF;";
    await fkOff.ExecuteNonQueryAsync();
  }

  var tables = new List<string>();
  await using (var listCmd = conn.CreateCommand())
  {
    listCmd.CommandText =
      "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name <> '__EFMigrationsHistory' ORDER BY name";
    await using var reader = await listCmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
      tables.Add(reader.GetString(0));
  }

  var total = 0;
  foreach (var table in tables)
  {
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = $"DELETE FROM \"{table}\"";
    var n = await cmd.ExecuteNonQueryAsync();
    total += n;
    if (n > 0)
      Console.WriteLine($"  {table} : {n} ligne(s)");
  }

  await using (var fkOn = conn.CreateCommand())
  {
    fkOn.CommandText = "PRAGMA foreign_keys=ON;";
    await fkOn.ExecuteNonQueryAsync();
  }

  return total;
}

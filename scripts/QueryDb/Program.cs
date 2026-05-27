using Microsoft.Data.Sqlite;

var dbPath = args.FirstOrDefault(a => !a.StartsWith("--"))
    ?? Path.Combine(Directory.GetCurrentDirectory(), "smartbuilding.db");
if (!File.Exists(dbPath))
    dbPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "smartbuilding.db");

if (!File.Exists(dbPath))
{
    Console.WriteLine($"Base introuvable : {dbPath}");
    return 1;
}

await using var conn = new SqliteConnection($"Data Source={dbPath}");
await conn.OpenAsync();

Console.WriteLine($"Base : {Path.GetFullPath(dbPath)}\n");

await using var cmd = conn.CreateCommand();
cmd.CommandText = """
    SELECT p.Code, p.Name, p.IsOccupied, p.OccupancyStatus,
           c.ContractNumber, c.Status, t.Name
    FROM Premises p
    LEFT JOIN LeaseContracts c ON c.PremiseId = p.Id
    LEFT JOIN Tenants t ON t.Id = c.TenantId
    ORDER BY p.Code, c.ContractNumber;
    """;

await using var reader = await cmd.ExecuteReaderAsync();
while (await reader.ReadAsync())
{
    var occupied = reader.GetInt32(2) == 1;
    var status = reader.IsDBNull(5) ? "—" : reader.GetInt32(5).ToString();
    var contract = reader.IsDBNull(4) ? "—" : reader.GetString(4);
    var tenant = reader.IsDBNull(6) ? "—" : reader.GetString(6);
    Console.WriteLine($"{reader.GetString(0)} | {reader.GetString(1)} | DB occupé={occupied} | Contrat {contract} statut={status} | {tenant}");
}

return 0;

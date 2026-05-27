using Microsoft.Data.Sqlite;

var dbPath = args.FirstOrDefault() ?? "c:\\Users\\PC\\Music\\SmartBuilding\\smartbuilding.db";
Console.WriteLine($"DB: {dbPath}");
if (!File.Exists(dbPath))
{
    Console.WriteLine("Base introuvable");
    return 1;
}

await using var conn = new SqliteConnection($"Data Source={dbPath}");
await conn.OpenAsync();

var sql = @"
SELECT c.ContractNumber,
       c.Status AS ContractStatus,
       t.Name AS Tenant,
       p.Code AS PremiseCode,
       p.IsOccupied,
       p.OccupancyStatus,
       rp.Year,
       rp.Month,
       rp.AmountDue,
       rp.AmountPaid,
       rp.PaymentStatus,
       rp.PaidDate
FROM LeaseContracts c
LEFT JOIN Tenants t ON t.Id = c.TenantId
LEFT JOIN Premises p ON p.Id = c.PremiseId
LEFT JOIN RentPayments rp ON rp.LeaseContractId = c.Id
ORDER BY rp.Year DESC, rp.Month DESC, c.ContractNumber;
";

await using var cmd = conn.CreateCommand();
cmd.CommandText = sql;
await using var reader = await cmd.ExecuteReaderAsync();

Console.WriteLine("Contract | CStatus | Tenant | Local | Occ? | OccStatus | Periode | Due | Paid | PStatus | PaidDate");
while (await reader.ReadAsync())
{
    string Get(int i) => reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString() ?? "";
    var period = $"{Get(6)}/{Get(7)}";
    Console.WriteLine($"{Get(0)} | {Get(1)} | {Get(2)} | {Get(3)} | {Get(4)} | {Get(5)} | {period} | {Get(8)} | {Get(9)} | {Get(10)} | {Get(11)}");
}

return 0;

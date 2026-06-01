using System.Diagnostics;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Domain.Common;
using SmartBuilding.Domain.Entities.Auth;
using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Domain.Entities.Consumption;
using SmartBuilding.Domain.Entities.Finance;
using SmartBuilding.Domain.Entities.Incidents;
using SmartBuilding.Domain.Entities.Inventory;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Domain.Entities.Personnel;
using SmartBuilding.Domain.Entities.Sync;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Services;
using SmartBuilding.Infrastructure.Sync;
using SmartBuilding.Domain.Entities.Technical;
using SmartBuilding.Domain.Entities.Visitors;
using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public class SynchronizationService
{
    private readonly SmartBuildingDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly INetworkService _network;
    private readonly DesktopLocalDatabaseConfig _localDb;

    public SynchronizationService(
        SmartBuildingDbContext db,
        IConfiguration configuration,
        INetworkService network,
        DesktopLocalDatabaseConfig localDb)
    {
        _db = db;
        _configuration = configuration;
        _network = network;
        _localDb = localDb;
    }

    public async Task<SyncPageData> LoadAsync(DateTime? lastSyncAt, CancellationToken cancellationToken = default)
    {
        await DatabaseSchemaUpgrader.UpgradeAsync(_db, cancellationToken);

        var apiUrl = _configuration["Api:BaseUrl"] ?? "https://localhost:7001/";
        var interval = _configuration.GetValue("Sync:IntervalSeconds", 60);
        var dbPath = _localDb.IsMySql
            ? MaskConnectionString(_localDb.ConnectionString)
            : DesktopSqlitePaths.GetDatabaseFilePath();
        (long Size, DateTime? LastWrite) dbInfo = _localDb.IsMySql
            ? (0L, null)
            : GetDbFileInfo(dbPath);

        var pingMs = 0;
        var isOnline = _network.IsConnected();
        var cloudOk = false;
        if (isOnline)
        {
            var sw = Stopwatch.StartNew();
            cloudOk = await _network.CanReachApiAsync(apiUrl, cancellationToken);
            sw.Stop();
            pingMs = cloudOk ? (int)sw.ElapsedMilliseconds : 0;
        }

        var dataTypes = await BuildDataTypesAsync(cancellationToken);
        var totalRecords = dataTypes.Sum(d => d.Total);
        var syncedCount = dataTypes.Sum(d => d.Synced);
        var pendingCount = await SyncCoordinator.CountAllUnsyncedAsync(_db, cancellationToken);

        var pendingItems = await BuildPendingItemsAsync(cancellationToken);
        var history = await BuildHistoryAsync(cancellationToken);
        var lastLog = history.FirstOrDefault();
        var lastSyncError = await _db.SyncLogs.IgnoreQueryFilters()
            .Where(l => !l.Success && l.ErrorMessage != null && l.ErrorMessage != "")
            .OrderByDescending(l => l.StartedAt)
            .Select(l => l.ErrorMessage)
            .FirstOrDefaultAsync(cancellationToken);
        var conflicts = await BuildConflictsAsync(cancellationToken);
        var alerts = BuildAlerts(pendingCount, conflicts.Count, lastSyncAt, lastLog, cloudOk, isOnline);
        var last7 = await BuildLast7DaysAsync(cancellationToken);

        var progress = totalRecords == 0 ? 100.0 : Math.Round(syncedCount * 100.0 / totalRecords, 0);
        var statusText = pendingCount == 0 && cloudOk
            ? "Terminée avec succès"
            : pendingCount > 0
                ? "En attente de synchronisation"
                : !cloudOk
                    ? "Hors ligne"
                    : "À jour";

        return new SyncPageData
        {
            SyncedCount = syncedCount,
            PendingCount = pendingCount,
            ConflictCount = conflicts.Count,
            LocalDbSizeBytes = dbInfo.Size,
            TotalRecords = totalRecords,
            LocalDbPath = dbPath,
            LocalDatabaseLabel = _localDb.DisplayLabel,
            DeviceLabel = DesktopSyncDevice.GetDeviceLabel(),
            LocalDbLastWrite = dbInfo.LastWrite,
            CloudServerUrl = apiUrl.TrimEnd('/'),
            LastSyncAt = lastSyncAt ?? lastLog?.StartedAt,
            IsOnline = isOnline,
            IsCloudReachable = cloudOk,
            PingMs = pingMs,
            SyncIntervalSeconds = interval,
            GlobalProgress = progress,
            SyncStatusText = statusText,
            LastSyncDuration = lastLog?.DurationLabel,
            LastThroughput = lastLog is not null ? FormatThroughput(lastLog.ItemsCount, lastLog.DurationLabel) : null,
            LastProcessed = syncedCount,
            LastTotal = totalRecords,
            LastDataTransferred = lastLog?.DataSizeLabel,
            DataTypes = dataTypes,
            PendingItems = pendingItems,
            Conflicts = conflicts,
            History = history,
            Alerts = alerts,
            Last7DaysCounts = last7,
            LastSyncError = lastSyncError
        };
    }

    private async Task<IReadOnlyList<SyncDataTypeRow>> BuildDataTypesAsync(CancellationToken ct)
    {
        return
        [
            await RowAsync("Utilisateurs", _db.Users, ct),
            await RowAsync("Bâtiments", _db.BuildingInfos, ct),
            await RowAsync("Bailleurs", _db.Landlords, ct),
            await RowAsync("Locaux", _db.Premises, ct),
            await RowAsync("Locataires", _db.Tenants, ct),
            await RowAsync("Contrats", _db.LeaseContracts, ct),
            await RowAsync("Loyers", _db.RentPayments, ct),
            await RowAsync("Équipements", _db.Equipment, ct),
            await RowAsync("Transactions", _db.FinancialTransactions, ct),
            await RowAsync("Incidents", _db.Incidents, ct),
            await RowAsync("Fournisseurs", _db.Suppliers, ct),
            await RowAsync("Personnel", _db.Employees, ct),
            await RowAsync("Inventaire", _db.InventoryItems, ct),
            await RowAsync("Consommations", _db.ConsumptionRecords, ct),
            await RowAsync("Visiteurs", _db.Visitors, ct)
        ];
    }

    private static async Task<SyncDataTypeRow> RowAsync<T>(string name, DbSet<T> set, CancellationToken ct)
        where T : BaseEntity
    {
        var total = await set.IgnoreQueryFilters().CountAsync(e => e.DeletedAt == null, ct);
        var synced = await set.IgnoreQueryFilters().CountAsync(e => e.DeletedAt == null && e.IsSynced, ct);
        return new SyncDataTypeRow { Name = name, Total = total, Synced = synced };
    }

    private async Task<IReadOnlyList<SyncPendingRow>> BuildPendingItemsAsync(CancellationToken ct)
    {
        var rows = new List<SyncPendingRow>();

        var users = await _db.Users.IgnoreQueryFilters()
            .Where(x => !x.IsSynced && x.DeletedAt == null).Take(5).ToListAsync(ct);
        rows.AddRange(users.Select(u => new SyncPendingRow
        {
            TypeLabel = "Utilisateur",
            IconKind = "Account",
            Description = u.FullName,
            CreatedAt = u.CreatedAt
        }));

        var incidents = await _db.Incidents.IgnoreQueryFilters()
            .Where(x => !x.IsSynced && x.DeletedAt == null).Take(5).ToListAsync(ct);
        rows.AddRange(incidents.Select(i => new SyncPendingRow
        {
            TypeLabel = "Incident",
            IconKind = "AlertCircle",
            Description = i.Title,
            CreatedAt = i.CreatedAt
        }));

        var employees = await _db.Employees.IgnoreQueryFilters()
            .Where(x => !x.IsSynced && x.DeletedAt == null).Take(5).ToListAsync(ct);
        rows.AddRange(employees.Select(e => new SyncPendingRow
        {
            TypeLabel = "Employé",
            IconKind = "AccountGroup",
            Description = $"{e.FirstName} {e.LastName}",
            CreatedAt = e.CreatedAt
        }));

        var transactions = await _db.FinancialTransactions.IgnoreQueryFilters()
            .Where(x => !x.IsSynced && x.DeletedAt == null).Take(5).ToListAsync(ct);
        rows.AddRange(transactions.Select(t => new SyncPendingRow
        {
            TypeLabel = "Transaction",
            IconKind = "Cash",
            Description = t.Description ?? t.Reference ?? t.Id.ToString()[..8],
            CreatedAt = t.CreatedAt
        }));

        var tenants = await _db.Tenants.IgnoreQueryFilters()
            .Where(x => !x.IsSynced && x.DeletedAt == null).Take(5).ToListAsync(ct);
        rows.AddRange(tenants.Select(t => new SyncPendingRow
        {
            TypeLabel = "Locataire",
            IconKind = "HomeAccount",
            Description = t.Name,
            CreatedAt = t.CreatedAt
        }));

        var premises = await _db.Premises.IgnoreQueryFilters()
            .Where(x => !x.IsSynced && x.DeletedAt == null).Take(5).ToListAsync(ct);
        rows.AddRange(premises.Select(p => new SyncPendingRow
        {
            TypeLabel = "Local",
            IconKind = "OfficeBuilding",
            Description = p.Name ?? p.Code,
            CreatedAt = p.CreatedAt
        }));

        return rows.OrderByDescending(r => r.CreatedAt).Take(28).ToList();
    }

    private async Task<IReadOnlyList<SyncConflictRow>> BuildConflictsAsync(CancellationToken ct)
    {
        var logs = await _db.SyncLogs.IgnoreQueryFilters()
            .Where(l => l.ConflictsResolved > 0)
            .OrderByDescending(l => l.StartedAt)
            .Take(10)
            .ToListAsync(ct);

        return logs.Select(l => new SyncConflictRow
        {
            TableName = "Synchronisation",
            RecordLabel = l.Direction,
            Description = $"{l.ConflictsResolved} conflit(s) résolu(s) — {l.ErrorMessage ?? "Last Write Wins"}",
            ConflictAt = l.StartedAt
        }).ToList();
    }

    private async Task<IReadOnlyList<SyncHistoryRow>> BuildHistoryAsync(CancellationToken ct)
    {
        var logs = await _db.SyncLogs.IgnoreQueryFilters()
            .OrderByDescending(l => l.StartedAt)
            .Take(20)
            .ToListAsync(ct);

        return logs.Select(MapHistory).ToList();
    }

    private static SyncHistoryRow MapHistory(SyncLog log)
    {
        var items = log.RecordsPushed + log.RecordsPulled;
        var duration = log.CompletedAt.HasValue
            ? log.CompletedAt.Value - log.StartedAt
            : TimeSpan.Zero;

        return new SyncHistoryRow
        {
            StartedAt = log.StartedAt.ToLocalTime(),
            TypeLabel = log.Direction is "Manual" ? "Manuelle" : "Automatique",
            Success = log.Success,
            ItemsCount = items,
            DataSizeLabel = items > 0 ? $"~{items * 2} KB" : "—",
            DurationLabel = duration.TotalSeconds > 0
                ? $"{(int)duration.TotalMinutes:00}:{duration.Seconds:00}"
                : "—",
            UserName = log.Direction is "Manual" ? "Admin" : "Système",
            Detail = log.Success ? null : log.ErrorMessage
        };
    }

    private async Task<IReadOnlyList<int>> BuildLast7DaysAsync(CancellationToken ct)
    {
        var start = DateTime.UtcNow.Date.AddDays(-6);
        var logs = await _db.SyncLogs.IgnoreQueryFilters()
            .Where(l => l.StartedAt >= start)
            .ToListAsync(ct);

        var result = new List<int>();
        for (var i = 0; i < 7; i++)
        {
            var day = start.AddDays(i).Date;
            var count = logs.Count(l => l.StartedAt.Date == day);
            result.Add(count);
        }
        return result;
    }

    private static IReadOnlyList<SyncAlertRow> BuildAlerts(
        int pending,
        int conflicts,
        DateTime? lastSyncAt,
        SyncHistoryRow? lastLog,
        bool cloudOk,
        bool isOnline)
    {
        var alerts = new List<SyncAlertRow>();

        if (pending > 0)
        {
            alerts.Add(new SyncAlertRow
            {
                IconKind = "Alert",
                IconColor = "#F59E0B",
                Title = "Éléments en attente",
                Message = $"{pending} élément(s) en attente de synchronisation",
                TimeLabel = "Maintenant"
            });
        }

        if (lastLog is { Success: false } && !string.IsNullOrWhiteSpace(lastLog.Detail))
        {
            alerts.Insert(0, new SyncAlertRow
            {
                IconKind = "CloseCircle",
                IconColor = "#DC2626",
                Title = "Dernière synchronisation échouée",
                Message = lastLog.Detail.Length > 220 ? lastLog.Detail[..220] + "…" : lastLog.Detail,
                TimeLabel = FormatTimeAgo(lastLog.StartedAt)
            });
        }

        if (conflicts > 0)
        {
            alerts.Add(new SyncAlertRow
            {
                IconKind = "AlertOctagon",
                IconColor = "#F59E0B",
                Title = "Conflits détectés",
                Message = $"{conflicts} conflit(s) nécessitent une attention",
                TimeLabel = "Récent"
            });
        }

        if (!isOnline)
        {
            alerts.Add(new SyncAlertRow
            {
                IconKind = "WifiOff",
                IconColor = "#EF4444",
                Title = "Hors ligne",
                Message = "Connexion Internet indisponible",
                TimeLabel = "Maintenant"
            });
        }
        else if (!cloudOk)
        {
            alerts.Add(new SyncAlertRow
            {
                IconKind = "CloudOffOutline",
                IconColor = "#F59E0B",
                Title = "Serveur cloud",
                Message = "Impossible de joindre le serveur cloud",
                TimeLabel = "Maintenant"
            });
        }

        if (lastLog?.Success == true)
        {
            alerts.Add(new SyncAlertRow
            {
                IconKind = "CheckCircle",
                IconColor = "#2D6A4F",
                Title = "Synchronisation terminée",
                Message = $"{lastLog.ItemsCount} élément(s) synchronisé(s)",
                TimeLabel = FormatTimeAgo(lastSyncAt)
            });
        }
        else if (lastLog is { Success: false })
        {
            alerts.Add(new SyncAlertRow
            {
                IconKind = "CloseCircle",
                IconColor = "#EF4444",
                Title = "Échec de synchronisation",
                Message = "La dernière synchronisation a échoué",
                TimeLabel = FormatTimeAgo(lastLog.StartedAt)
            });
        }

        if (alerts.Count == 0)
        {
            alerts.Add(new SyncAlertRow
            {
                IconKind = "Information",
                IconColor = "#3B82F6",
                Title = "Aucune alerte",
                Message = "Toutes les données sont à jour",
                TimeLabel = "—"
            });
        }

        return alerts;
    }

    private static string FormatTimeAgo(DateTime? dt)
    {
        if (!dt.HasValue) return "—";
        var span = DateTime.Now - dt.Value.ToLocalTime();
        if (span.TotalMinutes < 1) return "À l'instant";
        if (span.TotalHours < 1) return $"Il y a {(int)span.TotalMinutes} min";
        if (span.TotalDays < 1) return $"Il y a {(int)span.TotalHours} h";
        return $"Il y a {(int)span.TotalDays} j";
    }

    private static string? FormatThroughput(int items, string durationLabel)
    {
        if (items <= 0 || durationLabel == "—" || !durationLabel.Contains(':'))
            return null;
        var parts = durationLabel.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var min) || !int.TryParse(parts[1], out var sec))
            return null;
        var totalSec = min * 60 + sec;
        if (totalSec <= 0) return null;
        var kbPerSec = items * 2.0 / totalSec;
        return kbPerSec >= 1024
            ? $"{kbPerSec / 1024:F1} MB/s"
            : $"{kbPerSec:F1} KB/s";
    }


    private static (long Size, DateTime? LastWrite) GetDbFileInfo(string path)
    {
        if (!File.Exists(path))
            return (0, null);
        var info = new FileInfo(path);
        return (info.Length, info.LastWriteTime);
    }

    private static string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return "—";

        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].StartsWith("Password=", StringComparison.OrdinalIgnoreCase))
                parts[i] = "Password=***";
        }

        return string.Join(';', parts);
    }
}

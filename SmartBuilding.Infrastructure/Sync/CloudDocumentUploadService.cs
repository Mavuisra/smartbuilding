using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Infrastructure.Http;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Shared.DTOs.Sync;

namespace SmartBuilding.Infrastructure.Sync;

/// <summary>
/// Pousse les PDF/fichiers locaux vers Render — contenu binaire inchangé (100 % même format).
/// </summary>
public sealed class CloudDocumentUploadService : IDocumentCloudUploadService
{
    private const int MaxFileBytes = 20 * 1024 * 1024;

    private static readonly string ManifestPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SBMS",
        "document-upload-manifest.json");

    private readonly IDbContextFactory<SmartBuildingDbContext> _contextFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CloudDocumentUploadService> _logger;

    public CloudDocumentUploadService(
        IDbContextFactory<SmartBuildingDbContext> contextFactory,
        IConfiguration configuration,
        ILogger<CloudDocumentUploadService> logger)
    {
        _contextFactory = contextFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> TryUploadFileAsync(
        string localPath,
        string entityType,
        Guid entityId,
        string category,
        string? addedBy = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
            return false;

        try
        {
            var info = new FileInfo(localPath);
            if (info.Length <= 0 || info.Length > MaxFileBytes)
                return false;

            var bytes = await File.ReadAllBytesAsync(localPath, cancellationToken).ConfigureAwait(false);
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var manifest = await LoadManifestAsync(cancellationToken).ConfigureAwait(false);
            var key = $"{entityType}:{entityId:N}:{localPath}";
            if (manifest.TryGetValue(key, out var prev) &&
                prev.Sha256 == hash &&
                prev.FileSize == info.Length)
                return true;

            var baseUrl = GetApiBaseUrl();
            var token = await SyncCloudTokenStore.AcquireAsync(_configuration, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(token))
                return false;

            var request = new DocumentUploadRequest
            {
                EntityId = entityId,
                EntityType = entityType,
                Category = category,
                FileName = Path.GetFileName(localPath),
                MimeType = MimeFromPath(localPath),
                ContentBase64 = Convert.ToBase64String(bytes),
                AddedBy = addedBy ?? "SBMS Desktop",
                ContentSha256 = hash,
                FileSizeBytes = info.Length,
            };

            using var api = new CloudApiClient(baseUrl, token);
            var result = await api.PostJsonAsync("api/sync/documents/upload/", request, cancellationToken)
                .ConfigureAwait(false);

            if (!result.IsSuccess || !SyncApiResponse.IsApiSuccess(result.Body, out _))
                return false;

            manifest[key] = new ManifestEntry(hash, info.Length, DateTime.UtcNow);
            await SaveManifestAsync(manifest, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Upload document échoué pour {Path}", localPath);
            return false;
        }
    }

    public async Task<int> UploadAllPendingAsync(CancellationToken cancellationToken = default)
    {
        var uploaded = 0;
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var jobs = new List<(string Path, string EntityType, Guid Id, string Category)>();

        var contracts = await context.LeaseContracts.IgnoreQueryFilters()
            .Where(c => c.ContractPdfPath != null && c.ContractPdfPath != "")
            .Select(c => new { c.Id, c.ContractPdfPath })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        jobs.AddRange(contracts.Select(c => (c.ContractPdfPath!, "LeaseContracts", c.Id, "contrats")));

        var receipts = await context.RentPayments.IgnoreQueryFilters()
            .Where(p => p.ReceiptPdfPath != null && p.ReceiptPdfPath != "")
            .Select(p => new { p.Id, p.ReceiptPdfPath })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        jobs.AddRange(receipts.Select(p => (p.ReceiptPdfPath!, "RentPayments", p.Id, "factures")));

        var payslips = await context.SalaryPayments.IgnoreQueryFilters()
            .Where(s => s.PaySlipPdfPath != null && s.PaySlipPdfPath != "")
            .Select(s => new { s.Id, s.PaySlipPdfPath })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        jobs.AddRange(payslips.Select(s => (s.PaySlipPdfPath!, "SalaryPayments", s.Id, "personnel")));

        var employees = await context.Employees.IgnoreQueryFilters()
            .Where(e => e.ContractPdfPath != null && e.ContractPdfPath != "")
            .Select(e => new { e.Id, e.ContractPdfPath })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        jobs.AddRange(employees.Select(e => (e.ContractPdfPath!, "Employees", e.Id, "personnel")));

        var guarantees = await context.LeaseGuarantees.IgnoreQueryFilters()
            .Where(g => g.DischargePdfPath != null && g.DischargePdfPath != "")
            .Select(g => new { g.Id, g.DischargePdfPath })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        jobs.AddRange(guarantees.Select(g => (g.DischargePdfPath!, "LeaseGuarantees", g.Id, "contrats")));

        jobs.AddRange(ScanSbmsFolders());

        foreach (var job in jobs)
        {
            if (await TryUploadFileAsync(job.Path, job.EntityType, job.Id, job.Category, cancellationToken: cancellationToken)
                .ConfigureAwait(false))
                uploaded++;
        }

        return uploaded;
    }

    private static IEnumerable<(string Path, string EntityType, Guid Id, string Category)> ScanSbmsFolders()
    {
        var roots = new (string Root, string Category, string EntityType)[]
        {
            (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SBMS", "Contracts"), "contrats", "LeaseContracts"),
            (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SBMS", "Receipts"), "factures", "RentPayments"),
            (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SBMS", "Rapports"), "rapports", "Reports"),
            (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SBMS", "Finances"), "rapports", "FinancialTransactions"),
            (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SBMS", "Incidents"), "securite", "Incidents"),
            (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SBMS", "Documents", "files"), "archives", "Documents"),
        };

        foreach (var (root, category, entityType) in roots)
        {
            if (!Directory.Exists(root))
                continue;

            foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext is not (".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".csv"))
                    continue;

                var id = DeterministicGuid(file);
                yield return (file, entityType, id, category);
            }
        }
    }

    private static Guid DeterministicGuid(string input)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input.ToLowerInvariant()));
        var bytes = new byte[16];
        Array.Copy(hash, bytes, 16);
        return new Guid(bytes);
    }

    private static string MimeFromPath(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".csv" => "text/csv",
            _ => "application/octet-stream",
        };

    private string GetApiBaseUrl()
    {
        var baseUrl = _configuration["Api:BaseUrl"] ?? "https://smartbuilding-0kbk.onrender.com/";
        return baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/";
    }

    private sealed record ManifestEntry(string Sha256, long FileSize, DateTime UploadedAtUtc);

    private static async Task<Dictionary<string, ManifestEntry>> LoadManifestAsync(CancellationToken ct)
    {
        try
        {
            if (!File.Exists(ManifestPath))
                return new Dictionary<string, ManifestEntry>(StringComparer.OrdinalIgnoreCase);
            var json = await File.ReadAllTextAsync(ManifestPath, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<Dictionary<string, ManifestEntry>>(json)
                   ?? new Dictionary<string, ManifestEntry>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, ManifestEntry>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static async Task SaveManifestAsync(Dictionary<string, ManifestEntry> manifest, CancellationToken ct)
    {
        try
        {
            var dir = Path.GetDirectoryName(ManifestPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(ManifestPath, JsonSerializer.Serialize(manifest), ct).ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }
    }
}

using System.Globalization;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartBuilding.Domain.Entities.Email;
using SmartBuilding.Domain.Entities.Finance;
using SmartBuilding.Domain.Entities.Incidents;
using SmartBuilding.Domain.Entities.Inventory;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Domain.Entities.Personnel;
using SmartBuilding.Domain.Entities.Suppliers;
using SmartBuilding.Domain.Entities.Technical;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Infrastructure.Persistence;

namespace SmartBuilding.Desktop.WPF.Services;

public class DocumentsModuleService
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    private const long DefaultQuotaBytes = 20L * 1024 * 1024 * 1024;

    private readonly SmartBuildingDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly DocumentsUserLibraryService _userLibrary;

    public DocumentsModuleService(
        SmartBuildingDbContext db,
        IConfiguration configuration,
        DocumentsUserLibraryService userLibrary)
    {
        _db = db;
        _configuration = configuration;
        _userLibrary = userLibrary;
    }

    public async Task<DocumentsPageData> LoadAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var prevMonthStart = monthStart.AddMonths(-1);
        var weekStart = today.AddDays(-7);

        var buildingName = await _db.BuildingInfos
            .Select(b => b.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Immeuble";

        var docs = new List<RawDocument>();
        docs.AddRange(await LoadLeaseContractsAsync(buildingName, cancellationToken));
        docs.AddRange(await LoadSupplierContractsAsync(cancellationToken));
        docs.AddRange(await LoadFinancialDocumentsAsync(cancellationToken));
        docs.AddRange(await LoadRentPaymentsAsync(cancellationToken));
        docs.AddRange(await LoadSalaryDocumentsAsync(cancellationToken));
        docs.AddRange(await LoadEmployeeDocumentsAsync(cancellationToken));
        docs.AddRange(await LoadIncidentDocumentsAsync(cancellationToken));
        docs.AddRange(await LoadTechnicalDocumentsAsync(cancellationToken));
        docs.AddRange(await LoadInventoryDocumentsAsync(cancellationToken));
        docs.AddRange(await LoadEmailAttachmentsAsync(cancellationToken));
        docs.AddRange(await LoadConsumptionReportsAsync(cancellationToken));
        docs.AddRange(await LoadDeletedDocumentsAsync(cancellationToken));
        docs.AddRange(await LoadUserLibraryAsync(cancellationToken));

        var active = docs.Where(d => !d.IsDeleted).ToList();
        var contentBytes = active.Sum(d => d.SizeBytes);
        var usedBytes = contentBytes;
        var quota = DefaultQuotaBytes;
        var storagePercent = quota == 0 ? 0 : Math.Min(100, Math.Round(usedBytes * 100.0 / quota, 1));

        var recent = active.Count(d => d.CreatedAt >= weekStart);
        var activeContracts = active.Count(d =>
            d.CategoryId is "contrats" or "fournisseurs" &&
            d.Status.Contains("Actif", StringComparison.OrdinalIgnoreCase));
        var shared = active.Count(d => d.IsShared);
        var critical = active.Count(d => d.IsCritical);

        var thisMonth = active.Count(d => d.CreatedAt >= monthStart);
        var prevMonth = active.Count(d => d.CreatedAt >= prevMonthStart && d.CreatedAt < monthStart);
        var recentPrev = active.Count(d => d.CreatedAt >= weekStart.AddDays(-7) && d.CreatedAt < weekStart);
        var contractsPrev = active.Count(d =>
            d.CategoryId is "contrats" or "fournisseurs" &&
            d.Status.Contains("Actif", StringComparison.OrdinalIgnoreCase) &&
            d.CreatedAt < monthStart);

        var spark = BuildMonthlySparkline(active, today);

        var categories = BuildCategories(active);
        var tags = BuildPopularTags(active);
        var items = active.OrderByDescending(d => d.UpdatedAt).Select(MapItem).ToList();

        var buildings = active.Select(d => d.Building)
            .Where(b => !string.IsNullOrWhiteSpace(b) && b != "—")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(b => b)
            .ToList();

        return new DocumentsPageData
        {
            TotalCount = active.Count,
            RecentCount = recent,
            ActiveContractsCount = activeContracts,
            SharedCount = shared,
            CriticalCount = critical,
            StoragePercent = storagePercent,
            StorageUsedBytes = usedBytes,
            StorageQuotaBytes = quota,
            TotalTrend = FormatTrend(thisMonth, prevMonth, "Ce mois"),
            RecentTrend = FormatTrend(recent, recentPrev, "Cette semaine"),
            ContractsTrend = FormatTrend(activeContracts, contractsPrev, "Actifs"),
            SharedTrend = FormatTrend(shared, Math.Max(0, shared - 1), "Ce mois"),
            StorageTrend = $"{storagePercent:0.#}% utilisé",
            CriticalTrend = FormatDelta(critical, active.Count(d => d.IsCritical && d.CreatedAt < weekStart), "À traiter"),
            TotalSparkline = spark,
            RecentSparkline = BuildWeeklySparkline(active, today),
            ContractsSparkline = [activeContracts],
            SharedSparkline = [shared],
            StorageSparkline = [(int)storagePercent],
            CriticalSparkline = [critical],
            Documents = items,
            Categories = categories,
            PopularTags = tags,
            TypeFilters = ["Tous types", "PDF", "DOC", "XLS", "Dossier"],
            BuildingFilters = buildings.Count == 0
                ? ["Tous bâtiments", buildingName]
                : ["Tous bâtiments", ..buildings],
            DefaultBuilding = buildingName
        };
    }

    public Task<DocumentListItem> CreateUserFolderAsync(
        string folderName,
        string categoryId,
        string building,
        string addedBy,
        CancellationToken cancellationToken = default) =>
        _userLibrary.CreateFolderAsync(folderName, categoryId, building, addedBy, cancellationToken);

    public Task<IReadOnlyList<DocumentListItem>> UploadUserFilesAsync(
        IEnumerable<string> sourcePaths,
        string categoryId,
        string building,
        string addedBy,
        CancellationToken cancellationToken = default) =>
        _userLibrary.UploadFilesAsync(sourcePaths, categoryId, building, addedBy, cancellationToken);

    public string? ResolveDocumentFilePath(DocumentListItem item) =>
        _userLibrary.ResolveFilePath(item) ?? item.FilePath;

    public bool IsUserLibraryDocument(Guid id) =>
        _userLibrary.IsUserLibraryItem(id);

    private async Task<List<RawDocument>> LoadUserLibraryAsync(CancellationToken ct)
    {
        var items = await _userLibrary.LoadRawDocumentsAsync(ct);
        return items.Select(d => new RawDocument
        {
            Id = d.Id,
            FileName = d.FileName,
            SourcePath = d.SourcePath,
            FileType = d.FileType,
            CategoryId = d.CategoryId,
            CategoryLabel = CategoryLabel(d.CategoryId),
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt,
            AddedBy = d.AddedBy,
            Building = d.Building,
            Status = d.IsFolder ? "Dossier" : "Importé",
            SizeBytes = d.SizeBytes,
            IsFolder = d.IsFolder,
            PreviewTitle = d.IsFolder
                ? d.FileName.ToUpperInvariant()
                : Path.GetFileNameWithoutExtension(d.FileName).ToUpperInvariant(),
            PreviewBody = d.IsFolder
                ? "Dossier créé dans la bibliothèque SBMS."
                : $"Fichier importé — {d.FileName}",
            Tags = d.IsFolder ? [] : ["IMPORT"]
        }).ToList();
    }

    private async Task<List<RawDocument>> LoadLeaseContractsAsync(string defaultBuilding, CancellationToken ct)
    {
        var items = await _db.LeaseContracts
            .Include(c => c.Premise)
            .Include(c => c.Tenant)
            .ToListAsync(ct);

        return items.Select(c =>
        {
            var archived = c.Status != LeaseStatus.Actif;
            var building = string.IsNullOrWhiteSpace(c.Premise?.Building) ? defaultBuilding : c.Premise.Building;
            var name = string.IsNullOrWhiteSpace(c.Tenant?.Name) ? c.ContractNumber : c.Tenant.Name;
            var body = $"Contrat de location — {name} — loyer {c.MonthlyRent:N0} {GetCurrency()}";
            return new RawDocument
            {
                Id = c.Id,
                FileName = $"Contrat_{SanitizeFileName(c.ContractNumber)}.pdf",
                SourcePath = c.ContractPdfPath,
                FileType = "PDF",
                CategoryId = "contrats",
                CategoryLabel = "Contrats",
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                AddedBy = "SBMS",
                Building = building,
                Status = c.Status.ToString(),
                SizeBytes = EstimateSize(body, c.ContractNumber),
                IsShared = c.IsSynced,
                IsCritical = c.EndDate <= DateTime.UtcNow.AddDays(30) && c.Status == LeaseStatus.Actif,
                IsArchived = archived,
                IsDeleted = c.DeletedAt.HasValue,
                Tags = BuildTags("CONTRAT", c.Status == LeaseStatus.Actif ? null : "ARCHIVE"),
                PreviewTitle = "CONTRAT DE LOCATION",
                PreviewBody = body
            };
        }).ToList();
    }

    private async Task<List<RawDocument>> LoadSupplierContractsAsync(CancellationToken ct)
    {
        var items = await _db.SupplierContracts.Include(c => c.Supplier).ToListAsync(ct);
        return items.Select(c =>
        {
            var body = c.Description;
            var active = c.Status.Contains("Actif", StringComparison.OrdinalIgnoreCase);
            return new RawDocument
            {
                Id = c.Id,
                FileName = $"Contrat_{SanitizeFileName(c.ContractNumber)}.pdf",
                FileType = "PDF",
                CategoryId = active ? "fournisseurs" : "contrats",
                CategoryLabel = active ? "Fournisseurs" : "Contrats",
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                AddedBy = c.Supplier?.Name ?? "Fournisseur",
                Building = string.IsNullOrWhiteSpace(c.Building) ? "—" : c.Building,
                Status = c.Status,
                SizeBytes = EstimateSize(body, c.ContractNumber),
                IsShared = c.IsSynced,
                IsCritical = c.EndDate <= DateTime.UtcNow.AddDays(30) && active,
                IsArchived = !active,
                IsDeleted = c.DeletedAt.HasValue,
                Tags = BuildTags("CONTRAT", "FOURNISSEUR"),
                PreviewTitle = "CONTRAT FOURNISSEUR",
                PreviewBody = body
            };
        }).ToList();
    }

    private async Task<List<RawDocument>> LoadFinancialDocumentsAsync(CancellationToken ct)
    {
        var items = await _db.FinancialTransactions.ToListAsync(ct);
        return items.Select(t =>
        {
            var isInvoice = t.Category.Contains("Facture", StringComparison.OrdinalIgnoreCase)
                            || t.Description.Contains("Facture", StringComparison.OrdinalIgnoreCase);
            var catId = isInvoice ? "factures" : MapFinanceCategory(t.Category);
            var refPart = string.IsNullOrWhiteSpace(t.Reference) ? t.Id.ToString()[..8] : t.Reference;
            return new RawDocument
            {
                Id = t.Id,
                FileName = isInvoice
                    ? $"Facture_{SanitizeFileName(refPart)}.pdf"
                    : $"Document_{SanitizeFileName(refPart)}.pdf",
                FileType = "PDF",
                CategoryId = catId,
                CategoryLabel = CategoryLabel(catId),
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                AddedBy = string.IsNullOrWhiteSpace(t.RecordedBy) ? "Comptabilité" : t.RecordedBy,
                Building = string.IsNullOrWhiteSpace(t.Source) ? "—" : t.Source,
                Status = t.Status,
                SizeBytes = EstimateSize(t.Description, t.Amount.ToString("F0")),
                IsShared = t.IsSynced,
                IsCritical = t.Status.Contains("attente", StringComparison.OrdinalIgnoreCase),
                IsArchived = t.Status.Contains("Archiv", StringComparison.OrdinalIgnoreCase),
                IsDeleted = t.DeletedAt.HasValue,
                Tags = BuildTags(isInvoice ? "FACTURE" : null, t.Status.Contains("attente", StringComparison.OrdinalIgnoreCase) ? "URGENT" : null),
                PreviewTitle = isInvoice ? "FACTURE" : t.Category.ToUpperInvariant(),
                PreviewBody = $"{t.Description}\nMontant : {t.Amount:N0} {GetCurrency()}"
            };
        }).ToList();
    }

    private async Task<List<RawDocument>> LoadRentPaymentsAsync(CancellationToken ct)
    {
        var items = await _db.RentPayments.Include(p => p.LeaseContract).ThenInclude(l => l!.Premise).ToListAsync(ct);
        return items.Select(p =>
        {
            var refNo = string.IsNullOrWhiteSpace(p.ReceiptNumber) ? $"{p.Year}{p.Month:D2}" : p.ReceiptNumber;
            var body = $"Quittance loyer {p.Month:D2}/{p.Year} — {p.AmountPaid:N0} {GetCurrency()}";
            return new RawDocument
            {
                Id = p.Id,
                FileName = $"Quittance_{SanitizeFileName(refNo)}.pdf",
                SourcePath = p.ReceiptPdfPath,
                FileType = "PDF",
                CategoryId = "factures",
                CategoryLabel = "Factures",
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                AddedBy = "Locations",
                Building = p.LeaseContract?.Premise?.Building ?? "—",
                Status = p.PaidDate.HasValue ? "Payé" : p.IsLate ? "En retard" : "En attente",
                SizeBytes = EstimateSize(body, refNo),
                IsShared = p.IsSynced,
                IsCritical = p.IsLate,
                IsArchived = p.PaidDate.HasValue && p.PaidDate < DateTime.UtcNow.AddYears(-1),
                IsDeleted = p.DeletedAt.HasValue,
                Tags = BuildTags("FACTURE", p.IsLate ? "URGENT" : null),
                PreviewTitle = "QUITTANCE DE LOYER",
                PreviewBody = body
            };
        }).ToList();
    }

    private async Task<List<RawDocument>> LoadSalaryDocumentsAsync(CancellationToken ct)
    {
        var items = await _db.SalaryPayments.Include(s => s.Employee).ToListAsync(ct);
        return items.Select(s =>
        {
            var name = s.Employee is null ? "Employé" : $"{s.Employee.FirstName} {s.Employee.LastName}";
            var body = $"Bulletin de paie — {name} — {s.Month:D2}/{s.Year}";
            return new RawDocument
            {
                Id = s.Id,
                FileName = $"Paie_{s.Year}{s.Month:D2}_{SanitizeFileName(name)}.pdf",
                SourcePath = s.PaySlipPdfPath,
                FileType = "PDF",
                CategoryId = "personnel",
                CategoryLabel = "Personnel",
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
                AddedBy = "RH",
                Building = "—",
                Status = s.PaymentDate <= DateTime.UtcNow ? "Payé" : "En attente",
                SizeBytes = EstimateSize(body, s.Amount.ToString("F0")),
                IsShared = s.IsSynced,
                IsCritical = s.PaymentDate > DateTime.UtcNow,
                IsDeleted = s.DeletedAt.HasValue,
                Tags = BuildTags("CONFIDENTIEL"),
                PreviewTitle = "BULLETIN DE PAIE",
                PreviewBody = body
            };
        }).ToList();
    }

    private async Task<List<RawDocument>> LoadEmployeeDocumentsAsync(CancellationToken ct)
    {
        var items = await _db.Employees.ToListAsync(ct);
        return items.Select(e =>
        {
            var name = $"{e.FirstName} {e.LastName}";
            var body = $"Dossier personnel — {name} — {e.Position} / {e.Department}";
            return new RawDocument
            {
                Id = e.Id,
                FileName = $"Dossier_{SanitizeFileName(e.Matricule)}.pdf",
                SourcePath = e.ContractPdfPath,
                FileType = "PDF",
                CategoryId = "personnel",
                CategoryLabel = "Personnel",
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt,
                AddedBy = "RH",
                Building = "—",
                Status = e.IsActive ? "Actif" : "Inactif",
                SizeBytes = EstimateSize(body, e.Matricule),
                IsShared = e.IsSynced,
                IsDeleted = e.DeletedAt.HasValue,
                Tags = BuildTags("CONFIDENTIEL"),
                PreviewTitle = "DOSSIER PERSONNEL",
                PreviewBody = body
            };
        }).ToList();
    }

    private async Task<List<RawDocument>> LoadIncidentDocumentsAsync(CancellationToken ct)
    {
        var items = await _db.Incidents.ToListAsync(ct);
        return items.Select(i =>
        {
            var archived = i.Status is IncidentStatus.Resolu or IncidentStatus.Cloture;
            var body = $"{i.Title}\n{i.Description}";
            return new RawDocument
            {
                Id = i.Id,
                FileName = $"Incident_{SanitizeFileName(i.Code)}.pdf",
                FileType = "PDF",
                CategoryId = i.HasPhoto ? "rapports" : "securite",
                CategoryLabel = i.HasPhoto ? "Rapports" : "Sécurité",
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt,
                AddedBy = string.IsNullOrWhiteSpace(i.Responsible) ? "Sécurité" : i.Responsible,
                Building = string.IsNullOrWhiteSpace(i.Building) ? i.Location : i.Building,
                Status = i.Status.ToString(),
                SizeBytes = EstimateSize(body, i.Code),
                IsShared = i.IsSynced,
                IsCritical = i.Severity is IncidentSeverity.Critique or IncidentSeverity.Elevee,
                IsArchived = archived,
                IsDeleted = i.DeletedAt.HasValue,
                Tags = BuildTags(
                    i.Severity == IncidentSeverity.Critique ? "URGENT" : null,
                    i.HasPhoto ? "INSPECTION" : "MAINTENANCE"),
                PreviewTitle = "RAPPORT D'INCIDENT",
                PreviewBody = body
            };
        }).ToList();
    }

    private async Task<List<RawDocument>> LoadTechnicalDocumentsAsync(CancellationToken ct)
    {
        var list = new List<RawDocument>();

        var maintenance = await _db.MaintenanceRecords.Include(m => m.Equipment).ToListAsync(ct);
        list.AddRange(maintenance.Select(m =>
        {
            var body = m.Description;
            return new RawDocument
            {
                Id = m.Id,
                FileName = $"Maintenance_{(m.CompletedDate ?? m.ScheduledDate):yyyyMMdd}.pdf",
                FileType = "PDF",
                CategoryId = "technique",
                CategoryLabel = "Technique",
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt,
                AddedBy = string.IsNullOrWhiteSpace(m.Technician) ? "Technique" : m.Technician,
                Building = m.Equipment?.Location ?? "—",
                Status = "Archivé",
                SizeBytes = EstimateSize(body, m.Cost.ToString("F0")),
                IsShared = m.IsSynced,
                Tags = BuildTags("MAINTENANCE"),
                PreviewTitle = "FICHE MAINTENANCE",
                PreviewBody = body
            };
        }));

        var repairs = await _db.RepairRecords.Include(r => r.Equipment).ToListAsync(ct);
        list.AddRange(repairs.Select(r =>
        {
            var body = string.IsNullOrWhiteSpace(r.Resolution) ? r.Issue : $"{r.Issue}\n{r.Resolution}";
            var repairedAt = r.ResolvedDate ?? r.ReportedDate;
            return new RawDocument
            {
                Id = r.Id,
                FileName = $"Reparation_{repairedAt:yyyyMMdd}.pdf",
                FileType = "PDF",
                CategoryId = "technique",
                CategoryLabel = "Technique",
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                AddedBy = "Technique",
                Building = r.Equipment?.Location ?? "—",
                Status = r.ResolvedDate.HasValue ? "Résolu" : "En cours",
                SizeBytes = EstimateSize(body, r.Cost.ToString("F0")),
                IsShared = r.IsSynced,
                IsCritical = !r.ResolvedDate.HasValue,
                Tags = BuildTags("MAINTENANCE", "URGENT"),
                PreviewTitle = "FICHE RÉPARATION",
                PreviewBody = body
            };
        }));

        var equipment = await _db.Equipment.ToListAsync(ct);
        list.AddRange(equipment.Select(e =>
        {
            var body = $"{e.Name} — {e.Brand} {e.Model}\n{e.Location}";
            return new RawDocument
            {
                Id = e.Id,
                FileName = $"Fiche_{SanitizeFileName(e.Code)}.docx",
                FileType = "DOC",
                CategoryId = "technique",
                CategoryLabel = "Technique",
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt,
                AddedBy = "Technique",
                Building = e.Location,
                Status = e.Status.ToString(),
                SizeBytes = EstimateSize(body, e.Code),
                IsShared = e.IsSynced,
                Tags = BuildTags("MAINTENANCE"),
                PreviewTitle = "FICHE ÉQUIPEMENT",
                PreviewBody = body
            };
        }));

        return list;
    }

    private async Task<List<RawDocument>> LoadInventoryDocumentsAsync(CancellationToken ct)
    {
        var items = await _db.InventoryItems.ToListAsync(ct);
        return items.Select(i =>
        {
            var cat = i.Category.ToLowerInvariant();
            var categoryId = cat.Contains("doc", StringComparison.Ordinal) || cat.Contains("fichier", StringComparison.Ordinal)
                ? "archives"
                : "inventaire";
            var ext = cat.Contains("xls", StringComparison.Ordinal) || cat.Contains("tableur", StringComparison.Ordinal)
                ? "XLS"
                : cat.Contains("doc", StringComparison.Ordinal) ? "DOC" : "PDF";
            var body = $"{i.Name} — {i.Brand} {i.Model}\n{i.Notes}";
            return new RawDocument
            {
                Id = i.Id,
                FileName = $"{SanitizeFileName(i.Code)}_{SanitizeFileName(i.Name)}.{ext.ToLowerInvariant()}",
                FileType = ext,
                CategoryId = categoryId,
                CategoryLabel = CategoryLabel(categoryId),
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt,
                AddedBy = string.IsNullOrWhiteSpace(i.Responsible) ? "Inventaire" : i.Responsible,
                Building = string.IsNullOrWhiteSpace(i.Building) ? i.Location : i.Building,
                Status = i.Status,
                SizeBytes = EstimateSize(body, i.SerialNumber),
                IsShared = i.IsSynced,
                IsCritical = i.Status.Contains("critique", StringComparison.OrdinalIgnoreCase),
                IsArchived = categoryId == "archives",
                IsDeleted = i.DeletedAt.HasValue,
                Tags = BuildTags("INVENTAIRE"),
                PreviewTitle = i.Name.ToUpperInvariant(),
                PreviewBody = body
            };
        }).ToList();
    }

    private async Task<List<RawDocument>> LoadEmailAttachmentsAsync(CancellationToken ct)
    {
        var emails = await _db.CachedEmails.Where(e => e.HasAttachments).ToListAsync(ct);
        return emails.SelectMany(e => ParseEmailAttachments(e)).ToList();
    }

    private IEnumerable<RawDocument> ParseEmailAttachments(CachedEmail e)
    {
        if (string.IsNullOrWhiteSpace(e.AttachmentPaths))
        {
            yield return MapEmailAsDocument(e, e.Subject, "PDF");
            yield break;
        }

        var paths = e.AttachmentPaths.Split([';', ',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var index = 0;
        foreach (var path in paths)
        {
            index++;
            var fileName = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = $"Piece_jointe_{index}.pdf";
            var ext = Path.GetExtension(fileName).TrimStart('.').ToUpperInvariant();
            var type = ext switch
            {
                "DOC" or "DOCX" => "DOC",
                "XLS" or "XLSX" => "XLS",
                "PDF" => "PDF",
                _ => "PDF"
            };
            yield return MapEmailAsDocument(e, fileName, type, Guid.NewGuid(), path);
        }
    }

    private static RawDocument MapEmailAsDocument(CachedEmail e, string fileName, string fileType, Guid? id = null, string? sourcePath = null)
    {
        var body = e.BodyText ?? e.BodyHtml ?? e.BodyPreview;
        return new RawDocument
        {
            Id = id ?? e.Id,
            FileName = fileName,
            SourcePath = sourcePath,
            FileType = fileType,
            CategoryId = "emails",
            CategoryLabel = "Emails",
            CreatedAt = e.ReceivedAt,
            UpdatedAt = e.UpdatedAt,
            AddedBy = ExtractName(e.FromAddress),
            Building = "—",
            Status = e.IsRead ? "Lu" : "Non lu",
            SizeBytes = EstimateSize(body, fileName),
            IsShared = e.IsSynced,
            IsCritical = e.Priority == "Urgent",
            IsArchived = e.IsArchived,
            IsDeleted = e.IsSpam,
            Tags = BuildTags(e.Priority == "Urgent" ? "URGENT" : null, e.Category.ToUpperInvariant()),
            PreviewTitle = e.Subject,
            PreviewBody = body
        };
    }

    private async Task<List<RawDocument>> LoadConsumptionReportsAsync(CancellationToken ct)
    {
        var items = await _db.ConsumptionRecords.ToListAsync(ct);
        return items.Select(c =>
        {
            var body = $"Consommation {c.Type} — {c.PeriodStart:MMMM yyyy}\nMontant: {MoneyFormatter.Format(c.Cost)}";
            return new RawDocument
            {
                Id = c.Id,
                FileName = $"Rapport_{c.Type}_{c.PeriodStart:yyyyMM}.xlsx",
                FileType = "XLS",
                CategoryId = "rapports",
                CategoryLabel = "Rapports",
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                AddedBy = "Énergie",
                Building = string.IsNullOrWhiteSpace(c.Building) ? "—" : c.Building,
                Status = "Validé",
                SizeBytes = EstimateSize(body, c.Cost.ToString("F2")),
                IsShared = c.IsSynced,
                Tags = BuildTags("RAPPORT"),
                PreviewTitle = "RAPPORT DE CONSOMMATION",
                PreviewBody = body
            };
        }).ToList();
    }

    private async Task<List<RawDocument>> LoadDeletedDocumentsAsync(CancellationToken ct)
    {
        var list = new List<RawDocument>();

        var leases = await _db.LeaseContracts.IgnoreQueryFilters().Where(x => x.DeletedAt != null).ToListAsync(ct);
        list.AddRange(leases.Select(c => new RawDocument
        {
            Id = c.Id,
            FileName = $"Contrat_{SanitizeFileName(c.ContractNumber)}.pdf",
            FileType = "PDF",
            CategoryId = "corbeille",
            CategoryLabel = "Corbeille",
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
            AddedBy = "SBMS",
            Building = "—",
            Status = "Supprimé",
            SizeBytes = 512_000,
            IsDeleted = true,
            PreviewTitle = "CONTRAT SUPPRIMÉ",
            PreviewBody = c.ContractNumber
        }));

        return list;
    }

    private static List<DocumentCategoryItem> BuildCategories(IReadOnlyList<RawDocument> active)
    {
        var visible = active.Where(d => !d.IsDeleted).ToList();
        var defs = new (string Id, string Label, string Icon, string Color)[]
        {
            ("all", "Tous les documents", "FileDocumentMultiple", "#2D6A4F"),
            ("contrats", "Contrats", "FileSign", "#7C3AED"),
            ("factures", "Factures", "Receipt", "#2563EB"),
            ("personnel", "Personnel", "AccountGroup", "#0EA5E9"),
            ("technique", "Technique", "Wrench", "#EA580C"),
            ("securite", "Sécurité", "ShieldAlert", "#DC2626"),
            ("fournisseurs", "Fournisseurs", "TruckDelivery", "#D97706"),
            ("emails", "Emails", "Email", "#64748B"),
            ("rapports", "Rapports", "ChartBar", "#6D28D9"),
            ("inventaire", "Inventaire", "PackageVariant", "#166534"),
            ("archives", "Archives", "Archive", "#94A3B8"),
            ("corbeille", "Corbeille", "Delete", "#475569")
        };

        return defs.Select(d => new DocumentCategoryItem
        {
            CategoryId = d.Id,
            Label = d.Label,
            IconKind = d.Icon,
            IconColor = d.Color,
            Count = d.Id switch
            {
                "all" => visible.Count(d => !d.IsArchived),
                "archives" => visible.Count(x => x.IsArchived),
                "corbeille" => active.Count(x => x.IsDeleted),
                _ => visible.Count(x => x.CategoryId == d.Id && !x.IsArchived && !x.IsDeleted)
            },
            IsSelected = d.Id == "all"
        }).ToList();
    }

    private static List<DocumentTagItem> BuildPopularTags(IReadOnlyList<RawDocument> active)
    {
        var freq = active.Where(d => !d.IsDeleted && !d.IsArchived)
            .SelectMany(d => d.Tags)
            .GroupBy(t => t)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .ToList();

        var palette = new Dictionary<string, (string Bg, string Fg)>(StringComparer.OrdinalIgnoreCase)
        {
            ["URGENT"] = ("#FEE2E2", "#DC2626"),
            ["CONTRAT"] = ("#FFEDD5", "#EA580C"),
            ["FACTURE"] = ("#DCFCE7", "#166534"),
            ["CONFIDENTIEL"] = ("#EDE9FE", "#6D28D9"),
            ["MAINTENANCE"] = ("#DBEAFE", "#2563EB"),
            ["FOURNISSEUR"] = ("#FEF3C7", "#D97706"),
            ["INSPECTION"] = ("#E0F2FE", "#0369A1"),
            ["INVENTAIRE"] = ("#F1F5F9", "#475569"),
            ["RAPPORT"] = ("#F3E8FF", "#7C3AED")
        };

        return freq.Select(f =>
        {
            palette.TryGetValue(f.Key, out var colors);
            colors = colors == default ? ("#F1F5F9", "#475569") : colors;
            return new DocumentTagItem
            {
                Label = f.Key,
                Background = colors.Bg,
                Foreground = colors.Fg,
                Count = f.Count()
            };
        }).ToList();
    }

    private static DocumentListItem MapItem(RawDocument d) => new()
    {
        Id = d.Id,
        FileName = d.FileName,
        FileType = d.FileType,
        FileTypeIcon = FileTypeIcon(d.FileType),
        SizeDisplay = FormatSize(d.SizeBytes),
        SizeBytes = d.SizeBytes,
        CategoryId = d.CategoryId,
        CategoryLabel = d.CategoryLabel,
        CategoryIcon = CategoryIcon(d.CategoryId),
        CategoryIconColor = CategoryColor(d.CategoryId),
        DateDisplay = d.UpdatedAt.ToLocalTime().ToString("dd MMM yyyy", Fr),
        AddedAtDisplay = d.CreatedAt.ToLocalTime().ToString("dd MMMM yyyy 'à' HH:mm", Fr),
        ModifiedAtDisplay = d.UpdatedAt.ToLocalTime().ToString("dd MMMM yyyy 'à' HH:mm", Fr),
        AddedBy = d.AddedBy,
        Building = d.Building,
        FilePath = d.SourcePath,
        Status = d.Status,
        StatusBadgeBackground = StatusBackground(d.Status),
        StatusBadgeForeground = StatusForeground(d.Status),
        TypeLabel = $"{d.FileType} Document",
        PreviewTitle = d.PreviewTitle,
        PreviewBody = d.PreviewBody,
        IsFolder = d.IsFolder,
        IsShared = d.IsShared,
        IsCritical = d.IsCritical,
        IsArchived = d.IsArchived,
        IsDeleted = d.IsDeleted,
        IsFavorite = d.IsFavorite,
        Tags = d.Tags.Select(t => new DocumentTagItem
        {
            Label = t,
            Background = TagColor(t).Bg,
            Foreground = TagColor(t).Fg
        }).ToList()
    };

    private static long EstimateSize(string content, string seed)
    {
        var len = (content?.Length ?? 0) + (seed?.Length ?? 0);
        return Math.Clamp(256L * 1024 + len * 128L, 256 * 1024, 8 * 1024 * 1024);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} o";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
        return $"{bytes / (1024.0 * 1024):0.#} MB";
    }

    private static string FormatTrend(int current, int previous, string suffix)
    {
        if (previous == 0)
            return current == 0 ? $"0% {suffix}" : $"+100% {suffix}";
        var pct = (current - previous) * 100.0 / previous;
        return $"{(pct >= 0 ? "+" : "")}{pct:0.#}% {suffix}";
    }

    private static string FormatDelta(int current, int previous, string suffix)
    {
        var delta = current - previous;
        return delta == 0 ? $"0 {suffix}" : $"{(delta > 0 ? "+" : "")}{delta} {suffix}";
    }

    private static List<int> BuildMonthlySparkline(IReadOnlyList<RawDocument> docs, DateTime today)
    {
        var result = new List<int>();
        for (var i = 5; i >= 0; i--)
        {
            var start = new DateTime(today.Year, today.Month, 1).AddMonths(-i);
            var end = start.AddMonths(1);
            result.Add(docs.Count(d => d.CreatedAt >= start && d.CreatedAt < end && !d.IsDeleted));
        }
        return result;
    }

    private static List<int> BuildWeeklySparkline(IReadOnlyList<RawDocument> docs, DateTime today)
    {
        var result = new List<int>();
        for (var i = 6; i >= 0; i--)
        {
            var d = today.AddDays(-i);
            result.Add(docs.Count(x => x.CreatedAt.Date == d && !x.IsDeleted));
        }
        return result;
    }

    private static string ExtractName(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return "Inconnu";
        var idx = address.IndexOf('<');
        if (idx > 0) return address[..idx].Trim().Trim('"');
        return address.Contains('@') ? address.Split('@')[0] : address;
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "document";
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(c => invalid.Contains(c) ? '_' : c)).Replace(' ', '_');
    }

    private static IReadOnlyList<string> BuildTags(params string?[] tags) =>
        tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t!).Distinct().ToList();

    private static string MapFinanceCategory(string category)
    {
        var c = category.ToLowerInvariant();
        if (c.Contains("fournisseur")) return "fournisseurs";
        if (c.Contains("sécur") || c.Contains("secur")) return "securite";
        if (c.Contains("personnel") || c.Contains("salaire")) return "personnel";
        return "rapports";
    }

    private static string CategoryLabel(string id) => id switch
    {
        "contrats" => "Contrats",
        "factures" => "Factures",
        "personnel" => "Personnel",
        "technique" => "Technique",
        "securite" => "Sécurité",
        "fournisseurs" => "Fournisseurs",
        "emails" => "Emails",
        "rapports" => "Rapports",
        "inventaire" => "Inventaire",
        "archives" => "Archives",
        "corbeille" => "Corbeille",
        _ => "Document"
    };

    private static string CategoryIcon(string id) => id switch
    {
        "contrats" => "FileSign",
        "factures" => "Receipt",
        "personnel" => "AccountGroup",
        "technique" => "Wrench",
        "securite" => "ShieldAlert",
        "fournisseurs" => "TruckDelivery",
        "emails" => "Email",
        "rapports" => "ChartBar",
        "inventaire" => "PackageVariant",
        "archives" => "Archive",
        _ => "FileDocumentOutline"
    };

    private static string CategoryColor(string id) => id switch
    {
        "contrats" => "#7C3AED",
        "factures" => "#2563EB",
        "personnel" => "#0EA5E9",
        "technique" => "#EA580C",
        "securite" => "#DC2626",
        "fournisseurs" => "#D97706",
        "emails" => "#64748B",
        "rapports" => "#6D28D9",
        "inventaire" => "#166534",
        _ => "#64748B"
    };

    private static string FileTypeIcon(string type) => type switch
    {
        "DOC" => "FileWord",
        "XLS" => "FileExcel",
        "Dossier" => "Folder",
        _ => "FilePdfBox"
    };

    private static (string Bg, string Fg) TagColor(string tag) => tag.ToUpperInvariant() switch
    {
        "URGENT" => ("#FEE2E2", "#DC2626"),
        "CONTRAT" => ("#FFEDD5", "#EA580C"),
        "FACTURE" => ("#DCFCE7", "#166534"),
        "CONFIDENTIEL" => ("#EDE9FE", "#6D28D9"),
        "MAINTENANCE" => ("#DBEAFE", "#2563EB"),
        "INSPECTION" => ("#E0F2FE", "#0369A1"),
        _ => ("#F1F5F9", "#475569")
    };

    private static string StatusBackground(string status)
    {
        if (status.Contains("Actif", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("Payé", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("Validé", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("Lu", StringComparison.OrdinalIgnoreCase))
            return "#16A34A";
        if (status.Contains("attente", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("retard", StringComparison.OrdinalIgnoreCase))
            return "#F59E0B";
        if (status.Contains("Supprim", StringComparison.OrdinalIgnoreCase))
            return "#DC2626";
        return "#475569";
    }

    private static string StatusForeground(string status)
    {
        if (status.Contains("Actif", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("Payé", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("Validé", StringComparison.OrdinalIgnoreCase))
            return "#FFFFFF";
        if (status.Contains("attente", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("retard", StringComparison.OrdinalIgnoreCase))
            return "#111827";
        if (status.Contains("Supprim", StringComparison.OrdinalIgnoreCase))
            return "#FFFFFF";
        return "#FFFFFF";
    }

    private string GetCurrency() =>
        _configuration["Building:Currency"] ?? "USD";

    public async Task<int> PurgeAllDocumentsDataAsync(CancellationToken cancellationToken = default)
    {
        var deleted = 0;

        // Purge des enregistrements qui alimentent la page Documents.
        deleted += await _db.RentPayments.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        deleted += await _db.LeaseGuarantees.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        deleted += await _db.LeaseContracts.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        deleted += await _db.TenantActivities.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);

        deleted += await _db.SupplierPayments.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        deleted += await _db.SupplierContracts.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        deleted += await _db.FinancialTransactions.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);

        deleted += await _db.SalaryPayments.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        deleted += await _db.DisciplinaryNotes.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        deleted += await _db.Attendances.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        deleted += await _db.Employees.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);

        deleted += await _db.IncidentInterventions.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        deleted += await _db.Incidents.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        deleted += await _db.RepairRecords.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        deleted += await _db.MaintenanceRecords.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        deleted += await _db.Equipment.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);

        deleted += await _db.InventoryMaintenanceRecords.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        deleted += await _db.InventoryItems.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);

        deleted += await _db.ConsumptionRecords.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        deleted += await _db.CachedEmails.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);

        return deleted;
    }

    private sealed class RawDocument
    {
        public Guid Id { get; init; }
        public string FileName { get; init; } = string.Empty;
        public string FileType { get; init; } = "PDF";
        public string CategoryId { get; init; } = string.Empty;
        public string CategoryLabel { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
        public string AddedBy { get; init; } = "—";
        public string Building { get; init; } = "—";
        public string? SourcePath { get; init; }
        public string Status { get; init; } = "—";
        public long SizeBytes { get; init; }
        public bool IsShared { get; init; }
        public bool IsCritical { get; init; }
        public bool IsFavorite { get; init; }
        public bool IsFolder { get; init; }
        public bool IsArchived { get; init; }
        public bool IsDeleted { get; init; }
        public IReadOnlyList<string> Tags { get; init; } = [];
        public string PreviewTitle { get; init; } = string.Empty;
        public string PreviewBody { get; init; } = string.Empty;
    }
}

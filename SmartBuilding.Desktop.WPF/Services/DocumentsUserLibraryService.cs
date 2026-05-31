using System.Globalization;
using System.IO;
using System.Text.Json;
using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

/// <summary>
/// Bibliothèque utilisateur (dossiers et fichiers importés) persistée sur disque.
/// </summary>
public class DocumentsUserLibraryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _root;
    private readonly string _filesDir;
    private readonly string _indexPath;

    public DocumentsUserLibraryService()
    {
        _root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SBMS",
            "Documents");
        _filesDir = Path.Combine(_root, "files");
        _indexPath = Path.Combine(_root, "library.json");
        Directory.CreateDirectory(_filesDir);
    }

    public async Task<IReadOnlyList<UserLibraryRawDocument>> LoadRawDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var index = await ReadIndexAsync(cancellationToken);
        var items = new List<UserLibraryRawDocument>();

        foreach (var entry in index.Entries.OrderByDescending(e => e.UpdatedAt))
        {
            if (entry.IsFolder)
            {
                items.Add(MapToRaw(entry, null));
                continue;
            }

            var path = GetAbsolutePath(entry.StoredRelativePath);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                continue;

            items.Add(MapToRaw(entry, path));
        }

        return items;
    }

    public async Task<IReadOnlyList<DocumentListItem>> LoadItemsAsync(CancellationToken cancellationToken = default)
    {
        var raws = await LoadRawDocumentsAsync(cancellationToken);
        return raws.Select(r => r.IsFolder
            ? MapFolder(ToEntry(r))
            : MapFile(ToEntry(r), r.SourcePath!)).ToList();
    }

    public async Task<DocumentListItem> CreateFolderAsync(
        string folderName,
        string categoryId,
        string building,
        string addedBy,
        CancellationToken cancellationToken = default)
    {
        var name = folderName.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Le nom du dossier est obligatoire.");

        var now = DateTime.UtcNow;
        var entry = new LibraryEntry
        {
            Id = Guid.NewGuid(),
            FileName = name,
            IsFolder = true,
            CategoryId = NormalizeCategoryId(categoryId),
            Building = string.IsNullOrWhiteSpace(building) ? "—" : building.Trim(),
            AddedBy = string.IsNullOrWhiteSpace(addedBy) ? "Utilisateur" : addedBy.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            SizeBytes = 0
        };

        var index = await ReadIndexAsync(cancellationToken);
        index.Entries.Add(entry);
        await WriteIndexAsync(index, cancellationToken);
        return MapFolder(entry);
    }

    public async Task<IReadOnlyList<DocumentListItem>> UploadFilesAsync(
        IEnumerable<string> sourcePaths,
        string categoryId,
        string building,
        string addedBy,
        CancellationToken cancellationToken = default)
    {
        var uploaded = new List<DocumentListItem>();
        var index = await ReadIndexAsync(cancellationToken);
        var cat = NormalizeCategoryId(categoryId);
        var bld = string.IsNullOrWhiteSpace(building) ? "—" : building.Trim();
        var user = string.IsNullOrWhiteSpace(addedBy) ? "Utilisateur" : addedBy.Trim();

        foreach (var source in sourcePaths)
        {
            if (!File.Exists(source))
                continue;

            var id = Guid.NewGuid();
            var fileName = Path.GetFileName(source);
            var ext = Path.GetExtension(fileName);
            var relative = Path.Combine(cat, $"{id:N}{ext}");
            var dest = GetAbsolutePath(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(source, dest, overwrite: true);

            var info = new FileInfo(dest);
            var now = DateTime.UtcNow;
            var entry = new LibraryEntry
            {
                Id = id,
                FileName = fileName,
                IsFolder = false,
                StoredRelativePath = relative.Replace('\\', '/'),
                CategoryId = cat,
                Building = bld,
                AddedBy = user,
                CreatedAt = now,
                UpdatedAt = now,
                SizeBytes = info.Length
            };

            index.Entries.Add(entry);
            uploaded.Add(MapFile(entry, dest));
        }

        if (uploaded.Count > 0)
            await WriteIndexAsync(index, cancellationToken);

        return uploaded;
    }

    public string? ResolveFilePath(DocumentListItem item)
    {
        if (item.IsFolder || string.IsNullOrWhiteSpace(item.FilePath))
            return null;
        return File.Exists(item.FilePath) ? item.FilePath : null;
    }

    public bool IsUserLibraryItem(Guid id) =>
        ReadIndexAsync().GetAwaiter().GetResult().Entries.Any(e => e.Id == id);

    private async Task<LibraryIndex> ReadIndexAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_indexPath))
            return new LibraryIndex();

        await using var stream = File.OpenRead(_indexPath);
        return await JsonSerializer.DeserializeAsync<LibraryIndex>(stream, JsonOptions, cancellationToken)
               ?? new LibraryIndex();
    }

    private async Task WriteIndexAsync(LibraryIndex index, CancellationToken cancellationToken = default)
    {
        await using var stream = File.Create(_indexPath);
        await JsonSerializer.SerializeAsync(stream, index, JsonOptions, cancellationToken);
    }

    private string GetAbsolutePath(string? relative) =>
        string.IsNullOrWhiteSpace(relative)
            ? string.Empty
            : Path.Combine(_filesDir, relative.Replace('/', Path.DirectorySeparatorChar));

    private static string NormalizeCategoryId(string categoryId) =>
        string.IsNullOrWhiteSpace(categoryId) || categoryId is "all" or "corbeille"
            ? "archives"
            : categoryId.Trim();

    private static LibraryEntry ToEntry(UserLibraryRawDocument r) => new()
    {
        Id = r.Id,
        FileName = r.FileName,
        IsFolder = r.IsFolder,
        CategoryId = r.CategoryId,
        Building = r.Building,
        AddedBy = r.AddedBy,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
        SizeBytes = r.SizeBytes
    };

    private static UserLibraryRawDocument MapToRaw(LibraryEntry e, string? path)
    {
        var ext = Path.GetExtension(e.FileName).TrimStart('.').ToUpperInvariant();
        var type = e.IsFolder ? "Dossier" : ext switch
        {
            "DOC" or "DOCX" => "DOC",
            "XLS" or "XLSX" or "CSV" => "XLS",
            _ => string.IsNullOrWhiteSpace(ext) ? "PDF" : ext
        };
        return new UserLibraryRawDocument(
            e.Id, e.FileName, e.IsFolder, path, type,
            e.CategoryId, e.Building, e.AddedBy, e.CreatedAt, e.UpdatedAt, e.SizeBytes);
    }

    private static DocumentListItem MapFolder(LibraryEntry e)
    {
        var fr = CultureInfo.GetCultureInfo("fr-FR");
        return new DocumentListItem
        {
            Id = e.Id,
            FileName = e.FileName,
            FileType = "Dossier",
            FileTypeIcon = "Folder",
            SizeDisplay = "—",
            SizeBytes = 0,
            CategoryId = e.CategoryId,
            CategoryLabel = CategoryLabel(e.CategoryId),
            CategoryIcon = "Folder",
            CategoryIconColor = "#D97706",
            DateDisplay = e.UpdatedAt.ToLocalTime().ToString("dd MMM yyyy", fr),
            AddedAtDisplay = e.CreatedAt.ToLocalTime().ToString("dd MMMM yyyy 'à' HH:mm", fr),
            ModifiedAtDisplay = e.UpdatedAt.ToLocalTime().ToString("dd MMMM yyyy 'à' HH:mm", fr),
            AddedBy = e.AddedBy,
            Building = e.Building,
            Status = "Dossier",
            StatusBadgeBackground = "#FEF3C7",
            StatusBadgeForeground = "#D97706",
            TypeLabel = "Dossier",
            PreviewTitle = e.FileName.ToUpperInvariant(),
            PreviewBody = "Dossier créé dans la bibliothèque SBMS.",
            IsFolder = true,
            IsShared = false,
            Tags = []
        };
    }

    private static DocumentListItem MapFile(LibraryEntry e, string absolutePath)
    {
        var fr = CultureInfo.GetCultureInfo("fr-FR");
        var ext = Path.GetExtension(e.FileName).TrimStart('.').ToUpperInvariant();
        var type = ext switch
        {
            "DOC" or "DOCX" => "DOC",
            "XLS" or "XLSX" or "CSV" => "XLS",
            "PDF" => "PDF",
            _ => string.IsNullOrWhiteSpace(ext) ? "PDF" : ext
        };

        return new DocumentListItem
        {
            Id = e.Id,
            FileName = e.FileName,
            FileType = type,
            FileTypeIcon = FileTypeIcon(type),
            SizeDisplay = FormatSize(e.SizeBytes),
            SizeBytes = e.SizeBytes,
            CategoryId = e.CategoryId,
            CategoryLabel = CategoryLabel(e.CategoryId),
            CategoryIcon = CategoryIcon(e.CategoryId),
            CategoryIconColor = CategoryColor(e.CategoryId),
            DateDisplay = e.UpdatedAt.ToLocalTime().ToString("dd MMM yyyy", fr),
            AddedAtDisplay = e.CreatedAt.ToLocalTime().ToString("dd MMMM yyyy 'à' HH:mm", fr),
            ModifiedAtDisplay = e.UpdatedAt.ToLocalTime().ToString("dd MMMM yyyy 'à' HH:mm", fr),
            AddedBy = e.AddedBy,
            Building = e.Building,
            FilePath = absolutePath,
            Status = "Importé",
            StatusBadgeBackground = "#DCFCE7",
            StatusBadgeForeground = "#166534",
            TypeLabel = $"{type} Document",
            PreviewTitle = Path.GetFileNameWithoutExtension(e.FileName).ToUpperInvariant(),
            PreviewBody = $"Fichier importé — {e.FileName}",
            IsFolder = false,
            IsShared = false,
            Tags = [new DocumentTagItem { Label = "IMPORT", Background = "#E0F2FE", Foreground = "#0369A1" }]
        };
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} o";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
        return $"{bytes / (1024.0 * 1024):0.#} MB";
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
        _ => "Archives"
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
        _ => "Archive"
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
        _ => "#94A3B8"
    };

    private static string FileTypeIcon(string type) => type switch
    {
        "DOC" => "FileWord",
        "XLS" => "FileExcel",
        "Dossier" => "Folder",
        _ => "FilePdfBox"
    };

    private sealed class LibraryIndex
    {
        public List<LibraryEntry> Entries { get; set; } = [];
    }

    private sealed class LibraryEntry
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public bool IsFolder { get; set; }
        public string? StoredRelativePath { get; set; }
        public string CategoryId { get; set; } = "archives";
        public string Building { get; set; } = "—";
        public string AddedBy { get; set; } = "Utilisateur";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public long SizeBytes { get; set; }
    }

    public sealed record UserLibraryRawDocument(
        Guid Id,
        string FileName,
        bool IsFolder,
        string? SourcePath,
        string FileType,
        string CategoryId,
        string Building,
        string AddedBy,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        long SizeBytes);
}

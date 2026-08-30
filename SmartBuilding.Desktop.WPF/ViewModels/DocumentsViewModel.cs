using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SmartBuilding.Desktop.WPF.Helpers;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class DocumentsViewModel : BaseViewModel
{
    private const int PageSize = 12;
    private readonly DocumentsModuleService _documentsService;
    private List<DocumentListItem> _allDocuments = [];

    public const string AllTypes = "Tous types";
    public const string AllBuildings = "Tous bâtiments";
    public const string AllCategories = "Toutes catégories";

    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _userRole = string.Empty;
    [ObservableProperty] private string _userInitials = "AD";
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _filterType = AllTypes;
    [ObservableProperty] private string _filterCategory = AllCategories;
    [ObservableProperty] private string _filterDate = "Toute date";
    [ObservableProperty] private string _filterBuilding = AllBuildings;
    [ObservableProperty] private string _selectedSort = "Plus récents";
    [ObservableProperty] private string _selectedCategoryId = "all";
    [ObservableProperty] private DocumentListItem? _selectedDocument;
    [ObservableProperty] private bool _isGridView = true;
    [ObservableProperty] private bool _isTagsExpanded;
    [ObservableProperty] private bool _isAdvancedFiltersOpen;
    [ObservableProperty] private bool _isNotificationsOpen;
    [ObservableProperty] private bool _filterFavoritesOnly;
    [ObservableProperty] private bool _filterSharedOnly;
    [ObservableProperty] private bool _filterCriticalOnly;
    [ObservableProperty] private int _notificationCount;

    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _recentCount;
    [ObservableProperty] private int _activeContractsCount;
    [ObservableProperty] private int _sharedCount;
    [ObservableProperty] private int _criticalCount;
    [ObservableProperty] private double _storagePercent;
    [ObservableProperty] private string _storageDisplay = "—";
    [ObservableProperty] private string _totalTrend = "—";
    [ObservableProperty] private string _recentTrend = "—";
    [ObservableProperty] private string _contractsTrend = "—";
    [ObservableProperty] private string _sharedTrend = "—";
    [ObservableProperty] private string _storageTrend = "—";
    [ObservableProperty] private string _criticalTrend = "—";

    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private int _filteredTotal;
    [ObservableProperty] private string _paginationDisplay = string.Empty;

    [ObservableProperty] private ISeries[] _totalSparkline = [];
    [ObservableProperty] private ISeries[] _recentSparkline = [];
    [ObservableProperty] private ISeries[] _contractsSparkline = [];
    [ObservableProperty] private ISeries[] _sharedSparkline = [];
    [ObservableProperty] private ISeries[] _storageSparkline = [];
    [ObservableProperty] private ISeries[] _criticalSparkline = [];

    public ObservableCollection<DocumentListItem> PagedDocuments { get; } = [];
    public ObservableCollection<DocumentCategoryItem> Categories { get; } = [];
    public ObservableCollection<DocumentTagItem> PopularTags { get; } = [];
    public ObservableCollection<DocumentTagItem> AllTags { get; } = [];
    public ObservableCollection<DocumentListItem> CriticalNotifications { get; } = [];

    public string TagsToggleLabel => IsTagsExpanded ? "− Moins" : "+ Plus";

    public bool HasCriticalNotifications => CriticalNotifications.Count > 0;
    public ObservableCollection<int> PageNumbers { get; } = [];
    public ObservableCollection<string> SortOptions { get; } = ["Plus récents", "Plus anciens", "Par nom"];
    public ObservableCollection<string> TypeFilters { get; } = [AllTypes];
    public ObservableCollection<string> CategoryFilters { get; } = [AllCategories];
    public ObservableCollection<string> DateFilters { get; } = ["Toute date", "Aujourd'hui", "Cette semaine", "Ce mois"];
    public ObservableCollection<string> BuildingFilters { get; } = [AllBuildings];

    public DocumentsViewModel(DocumentsModuleService documentsService, SessionService session)
    {
        _documentsService = documentsService;
        UserName = session.CurrentUser?.FullName ?? "Admin SBMS";
        UserRole = session.CurrentUser?.Role ?? "Administrateur";
        UserInitials = GetInitials(UserName);
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var data = await _documentsService.LoadAsync();
            _allDocuments = data.Documents.ToList();

            TotalCount = data.TotalCount;
            RecentCount = data.RecentCount;
            ActiveContractsCount = data.ActiveContractsCount;
            SharedCount = data.SharedCount;
            CriticalCount = data.CriticalCount;
            StoragePercent = data.StoragePercent;
            StorageDisplay = FormatStorage(data.StorageUsedBytes, data.StorageQuotaBytes);
            TotalTrend = data.TotalTrend;
            RecentTrend = data.RecentTrend;
            ContractsTrend = data.ContractsTrend;
            SharedTrend = data.SharedTrend;
            StorageTrend = data.StorageTrend;
            CriticalTrend = data.CriticalTrend;
            NotificationCount = data.CriticalCount;

            Categories.Clear();
            foreach (var c in data.Categories)
            {
                c.IsSelected = c.CategoryId == SelectedCategoryId;
                Categories.Add(c);
            }

            PopularTags.Clear();
            foreach (var t in data.PopularTags) PopularTags.Add(t);

            AllTags.Clear();
            foreach (var t in BuildAllTags(_allDocuments)) AllTags.Add(t);

            RefreshCriticalNotifications();

            TypeFilters.Clear();
            foreach (var t in data.TypeFilters) TypeFilters.Add(t);

            CategoryFilters.Clear();
            CategoryFilters.Add(AllCategories);
            foreach (var c in data.Categories.Where(x => x.CategoryId != "all" && x.CategoryId != "corbeille"))
                CategoryFilters.Add(c.Label);

            BuildingFilters.Clear();
            foreach (var b in data.BuildingFilters) BuildingFilters.Add(b);

            FilterType = PageFilterHelper.RestoreSelection(FilterType, TypeFilters, AllTypes);
            FilterCategory = PageFilterHelper.RestoreSelection(FilterCategory, CategoryFilters, AllCategories);
            FilterBuilding = PageFilterHelper.RestoreSelection(FilterBuilding, BuildingFilters, AllBuildings);
            FilterDate = PageFilterHelper.RestoreSelection(FilterDate, DateFilters, "Toute date");

            BuildSparklines(data);
            ApplyFilters();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void SelectCategory(string? categoryId)
    {
        SelectedCategoryId = string.IsNullOrWhiteSpace(categoryId) ? "all" : categoryId;
        foreach (var c in Categories)
            c.IsSelected = c.CategoryId == SelectedCategoryId;
        CurrentPage = 1;
        ApplyFilters();
    }

    [RelayCommand]
    private void SelectDocument(DocumentListItem? doc)
    {
        foreach (var d in _allDocuments) d.IsSelected = false;
        if (doc is not null) doc.IsSelected = true;
        SelectedDocument = doc;
        if (IsNotificationsOpen)
            IsNotificationsOpen = false;
    }

    [RelayCommand]
    private void CloseDetail() => SelectedDocument = null;

    [RelayCommand]
    private void SetGridView() => IsGridView = true;

    [RelayCommand]
    private void SetTableView() => IsGridView = false;

    [RelayCommand]
    private async Task ToggleFavoriteAsync(DocumentListItem? doc)
    {
        if (doc is null) return;

        var newValue = !doc.IsFavorite;
        doc.IsFavorite = newValue;
        try
        {
            await _documentsService.SetFavoriteAsync(doc.Id, newValue);
            StatusMessage = newValue
                ? $"« {doc.FileName} » ajouté aux favoris."
                : $"« {doc.FileName} » retiré des favoris.";
        }
        catch (Exception ex)
        {
            doc.IsFavorite = !newValue;
            SbmsDialogService.ShowError("Favoris", ex.Message);
        }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private void ToggleNotifications()
    {
        RefreshCriticalNotifications();
        IsNotificationsOpen = !IsNotificationsOpen;
    }

    [RelayCommand]
    private void CloseNotifications() => IsNotificationsOpen = false;

    [RelayCommand]
    private async Task CreateFolderAsync()
    {
        var name = SbmsDialogService.PromptText(
            "Nouveau dossier",
            "Nom du dossier à créer :",
            "Nouveau dossier");
        if (string.IsNullOrWhiteSpace(name))
            return;

        IsBusy = true;
        try
        {
            var folder = await _documentsService.CreateUserFolderAsync(
                name,
                GetUploadCategoryId(),
                GetUploadBuilding(),
                UserName);
            StatusMessage = $"Dossier « {folder.FileName} » créé.";
            var folderId = folder.Id;
            await LoadAsync();
            SelectDocument(_allDocuments.FirstOrDefault(d => d.Id == folderId));
        }
        catch (Exception ex)
        {
            SbmsDialogService.ShowError("Dossier", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task UploadDocumentAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Importer des documents",
            Filter = "Documents|*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.csv;*.png;*.jpg;*.jpeg;*.webp|Tous les fichiers|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0)
            return;

        IsBusy = true;
        try
        {
            var uploaded = await _documentsService.UploadUserFilesAsync(
                dialog.FileNames,
                GetUploadCategoryId(),
                GetUploadBuilding(),
                UserName);
            if (uploaded.Count == 0)
            {
                StatusMessage = "Aucun fichier importé.";
                return;
            }

            StatusMessage = $"{uploaded.Count} fichier(s) importé(s).";
            var firstId = uploaded[0].Id;
            await LoadAsync();
            SelectDocument(_allDocuments.FirstOrDefault(d => d.Id == firstId));
        }
        catch (Exception ex)
        {
            SbmsDialogService.ShowError("Import", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ShowMoreTags()
    {
        IsTagsExpanded = !IsTagsExpanded;
        OnPropertyChanged(nameof(TagsToggleLabel));
    }

    [RelayCommand]
    private void OpenAdvancedFilters() => IsAdvancedFiltersOpen = !IsAdvancedFiltersOpen;

    [RelayCommand]
    private async Task OpenDocumentAsync(DocumentListItem? doc)
    {
        doc ??= SelectedDocument;
        if (doc is null) return;

        var path = await EnsureOpenablePathAsync(doc);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        StatusMessage = $"Ouverture: {doc.FileName}";
    }

    [RelayCommand]
    private async Task DownloadDocumentAsync(DocumentListItem? doc)
    {
        doc ??= SelectedDocument;
        if (doc is null)
            return;

        if (doc.IsFolder)
        {
            SbmsDialogService.ShowInfo("Télécharger", "Un dossier ne peut pas être téléchargé. Sélectionnez un fichier.");
            return;
        }

        var source = await EnsureOpenablePathAsync(doc);
        if (!File.Exists(source))
        {
            SbmsDialogService.ShowWarning("Télécharger", "Fichier introuvable sur le disque.");
            return;
        }

        var saveDialog = new SaveFileDialog
        {
            Title = "Enregistrer le document",
            FileName = Path.GetFileName(source),
            Filter = BuildSaveFilter(Path.GetExtension(source))
        };
        if (saveDialog.ShowDialog() != true)
            return;

        File.Copy(source, saveDialog.FileName, overwrite: true);
        StatusMessage = $"Document enregistré : {saveDialog.FileName}";

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.GetDirectoryName(saveDialog.FileName)!,
                UseShellExecute = true
            });
        }
        catch
        {
            // Ouverture du dossier optionnelle
        }
    }

    [RelayCommand]
    private async Task ShowDocumentActionsAsync(DocumentListItem? doc)
    {
        doc ??= SelectedDocument;
        if (doc is null) return;

        var actions = new List<string> { "Ouvrir", "Télécharger", doc.IsFavorite ? "Retirer des favoris" : "Ajouter aux favoris" };
        if (_documentsService.IsUserLibraryDocument(doc.Id))
            actions.Add("Supprimer");

        var choice = SbmsDialogService.ShowActionMenu($"Actions — {doc.FileName}", actions);
        if (choice is null) return;

        switch (choice)
        {
            case "Ouvrir":
                await OpenDocumentAsync(doc);
                break;
            case "Télécharger":
                await DownloadDocumentAsync(doc);
                break;
            case "Ajouter aux favoris":
            case "Retirer des favoris":
                await ToggleFavoriteAsync(doc);
                break;
            case "Supprimer":
                await DeleteDocumentAsync(doc);
                break;
        }
    }

    [RelayCommand]
    private async Task DeleteDocumentAsync(DocumentListItem? doc)
    {
        doc ??= SelectedDocument;
        if (doc is null) return;

        if (!_documentsService.IsUserLibraryDocument(doc.Id))
        {
            SbmsDialogService.ShowInfo("Supprimer", "Seuls les documents importés peuvent être supprimés.");
            return;
        }

        if (!SbmsDialogService.Confirm("Supprimer", $"Supprimer « {doc.FileName} » ? Cette action est irréversible."))
            return;

        IsBusy = true;
        try
        {
            var deleted = await _documentsService.DeleteUserDocumentAsync(doc.Id);
            if (!deleted)
            {
                SbmsDialogService.ShowWarning("Supprimer", "Document introuvable dans la bibliothèque.");
                return;
            }

            StatusMessage = $"« {doc.FileName} » supprimé.";
            SelectedDocument = null;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            SbmsDialogService.ShowError("Supprimer", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PurgeDocumentsDatabaseAsync()
    {
        if (!SbmsDialogService.Confirm(
                "Confirmation",
                "Supprimer TOUTES les données documentaires de la base ? Cette action est irréversible."))
            return;

        IsBusy = true;
        try
        {
            var deleted = await _documentsService.PurgeAllDocumentsDataAsync();
            StatusMessage = $"{deleted} enregistrements documentaires supprimés.";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            ApplyFilters(skipResetPage: true);
        }
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            ApplyFilters(skipResetPage: true);
        }
    }

    [RelayCommand]
    private void GoToPage(int page)
    {
        if (page < 1 || page > TotalPages) return;
        CurrentPage = page;
        ApplyFilters(skipResetPage: true);
    }

    [RelayCommand]
    private void FilterByTag(DocumentTagItem? tag)
    {
        if (tag is null) return;
        SearchQuery = tag.Label;
        CurrentPage = 1;
        ApplyFilters();
    }

    partial void OnSearchQueryChanged(string value) { CurrentPage = 1; ApplyFilters(); }
    partial void OnFilterTypeChanged(string value) { CurrentPage = 1; ApplyFilters(); }
    partial void OnFilterCategoryChanged(string value) { CurrentPage = 1; ApplyFilters(); }
    partial void OnFilterDateChanged(string value) { CurrentPage = 1; ApplyFilters(); }
    partial void OnFilterBuildingChanged(string value) { CurrentPage = 1; ApplyFilters(); }
    partial void OnSelectedSortChanged(string value) { CurrentPage = 1; ApplyFilters(); }
    partial void OnFilterFavoritesOnlyChanged(bool value) { CurrentPage = 1; ApplyFilters(); }
    partial void OnFilterSharedOnlyChanged(bool value) { CurrentPage = 1; ApplyFilters(); }
    partial void OnFilterCriticalOnlyChanged(bool value) { CurrentPage = 1; ApplyFilters(); }

    private void ApplyFilters(bool skipResetPage = false)
    {
        var today = DateTime.Today;
        var weekStart = today.AddDays(-7);
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var query = SearchQuery.Trim().ToLowerInvariant();

        IEnumerable<DocumentListItem> filtered = _allDocuments;

        filtered = SelectedCategoryId switch
        {
            "all" => filtered.Where(d => !d.IsArchived && !d.IsDeleted),
            "archives" => filtered.Where(d => d.IsArchived && !d.IsDeleted),
            "corbeille" => filtered.Where(d => d.IsDeleted),
            _ => filtered.Where(d => d.CategoryId == SelectedCategoryId && !d.IsArchived && !d.IsDeleted)
        };

        if (!PageFilterHelper.IsAll(FilterType, AllTypes))
            filtered = filtered.Where(d => d.FileType.Equals(FilterType, StringComparison.OrdinalIgnoreCase));

        if (!PageFilterHelper.IsAll(FilterCategory, AllCategories))
            filtered = filtered.Where(d => d.CategoryLabel.Equals(FilterCategory, StringComparison.OrdinalIgnoreCase));

        if (!PageFilterHelper.IsAll(FilterBuilding, AllBuildings))
            filtered = filtered.Where(d => d.Building.Equals(FilterBuilding, StringComparison.OrdinalIgnoreCase));

        if (FilterDate == "Aujourd'hui")
            filtered = filtered.Where(d => d.DateDisplay.Contains(today.ToString("dd MMM", System.Globalization.CultureInfo.GetCultureInfo("fr-FR")), StringComparison.OrdinalIgnoreCase));
        else if (FilterDate == "Cette semaine")
            filtered = filtered.Where(d => ParseDocDate(d) >= weekStart);
        else if (FilterDate == "Ce mois")
            filtered = filtered.Where(d => ParseDocDate(d) >= monthStart);

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(d =>
                d.FileName.ToLowerInvariant().Contains(query) ||
                d.CategoryLabel.ToLowerInvariant().Contains(query) ||
                d.Tags.Any(t => t.Label.ToLowerInvariant().Contains(query)) ||
                d.PreviewBody.ToLowerInvariant().Contains(query));
        }

        if (FilterFavoritesOnly)
            filtered = filtered.Where(d => d.IsFavorite);

        if (FilterSharedOnly)
            filtered = filtered.Where(d => d.IsShared);

        if (FilterCriticalOnly)
            filtered = filtered.Where(d => d.IsCritical);

        var list = SelectedSort switch
        {
            "Plus anciens" => filtered.OrderBy(ParseDocDate).ToList(),
            "Par nom" => filtered.OrderBy(d => d.FileName).ToList(),
            _ => filtered.OrderByDescending(ParseDocDate).ToList()
        };

        FilteredTotal = list.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling(FilteredTotal / (double)PageSize));
        if (!skipResetPage && CurrentPage > TotalPages)
            CurrentPage = 1;

        var pageItems = list.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
        PagedDocuments.Clear();
        foreach (var d in pageItems) PagedDocuments.Add(d);

        var from = FilteredTotal == 0 ? 0 : (CurrentPage - 1) * PageSize + 1;
        var to = Math.Min(CurrentPage * PageSize, FilteredTotal);
        PaginationDisplay = FilteredTotal == 0
            ? "Aucun document"
            : $"Affichage {from} à {to} sur {FilteredTotal:N0} documents";

        PageNumbers.Clear();
        var maxButtons = Math.Min(TotalPages, 7);
        for (var i = 1; i <= maxButtons; i++)
            PageNumbers.Add(i);

        if (SelectedDocument is not null && !list.Any(x => x.Id == SelectedDocument.Id))
            SelectedDocument = PagedDocuments.FirstOrDefault();
    }

    private static DateTime ParseDocDate(DocumentListItem d)
    {
        if (DateTime.TryParse(d.DateDisplay, System.Globalization.CultureInfo.GetCultureInfo("fr-FR"), out var dt))
            return dt;
        return DateTime.MinValue;
    }

    private string GetUploadCategoryId() =>
        SelectedCategoryId is "all" or "corbeille" ? "archives" : SelectedCategoryId;

    private string GetUploadBuilding()
    {
        if (!PageFilterHelper.IsAll(FilterBuilding, AllBuildings))
            return FilterBuilding;
        return BuildingFilters.Count > 1 ? BuildingFilters[1] : "—";
    }

    private static string BuildSaveFilter(string ext) => ext.ToLowerInvariant() switch
    {
        ".pdf" => "PDF (*.pdf)|*.pdf",
        ".doc" or ".docx" => "Word|*.doc;*.docx",
        ".xls" or ".xlsx" => "Excel|*.xls;*.xlsx",
        ".csv" => "CSV|*.csv",
        ".png" or ".jpg" or ".jpeg" or ".webp" => "Images|*.png;*.jpg;*.jpeg;*.webp",
        _ => "Tous les fichiers|*.*"
    };

    private async Task<string> EnsureOpenablePathAsync(DocumentListItem doc)
    {
        var resolved = _documentsService.ResolveDocumentFilePath(doc);
        if (!string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved))
            return resolved;

        if (!string.IsNullOrWhiteSpace(doc.FilePath) && File.Exists(doc.FilePath))
            return doc.FilePath;

        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SBMS",
            "DocumentPreview");
        Directory.CreateDirectory(folder);

        var ext = doc.FileType.ToUpperInvariant() switch
        {
            "DOC" => ".txt",
            "XLS" => ".csv",
            _ => ".html"
        };
        var fileName = SanitizeFileNameWithoutExtension(doc.FileName);
        var path = Path.Combine(folder, $"{fileName}_{doc.Id:N}{ext}");

        if (ext == ".html")
        {
            var html = $$$"""
                <!doctype html>
                <html lang="fr">
                <head>
                  <meta charset="utf-8" />
                  <meta name="viewport" content="width=device-width, initial-scale=1" />
                  <title>{{{doc.FileName}}}</title>
                  <style>
                    body {{ font-family: Segoe UI, Arial, sans-serif; background:#f4f7f6; margin:0; padding:24px; color:#1f2937; }}
                    .card {{ max-width:900px; margin:auto; background:#fff; border:1px solid #e5e7eb; border-radius:14px; padding:24px; box-shadow:0 6px 20px rgba(0,0,0,0.06); }}
                    .k {{ color:#64748b; font-size:12px; margin-top:10px; }}
                    .v {{ font-size:14px; font-weight:600; }}
                    h1 {{ margin:0 0 8px 0; color:#0f766e; }}
                    .body {{ white-space:pre-wrap; margin-top:16px; line-height:1.6; }}
                  </style>
                </head>
                <body>
                  <div class="card">
                    <h1>{{{doc.PreviewTitle}}}</h1>
                    <div class="k">Fichier</div><div class="v">{{{doc.FileName}}}</div>
                    <div class="k">Type</div><div class="v">{{{doc.TypeLabel}}}</div>
                    <div class="k">Catégorie</div><div class="v">{{{doc.CategoryLabel}}}</div>
                    <div class="k">Bâtiment</div><div class="v">{{{doc.Building}}}</div>
                    <div class="k">Ajouté par</div><div class="v">{{{doc.AddedBy}}}</div>
                    <div class="k">Aperçu</div>
                    <div class="body">{{{doc.PreviewBody}}}</div>
                  </div>
                </body>
                </html>
                """;
            await File.WriteAllTextAsync(path, html);
        }
        else
        {
            var payload = $"{doc.FileName}{Environment.NewLine}{doc.PreviewTitle}{Environment.NewLine}{Environment.NewLine}{doc.PreviewBody}";
            await File.WriteAllTextAsync(path, payload);
        }

        return path;
    }

    private static string SanitizeFileNameWithoutExtension(string value)
    {
        var name = Path.GetFileNameWithoutExtension(value);
        if (string.IsNullOrWhiteSpace(name))
            name = "document";
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Replace(' ', '_');
    }

    private void BuildSparklines(DocumentsPageData data)
    {
        TotalSparkline = BuildSparkline(data.TotalSparkline, "#2563EB");
        RecentSparkline = BuildSparkline(data.RecentSparkline, "#2D6A4F");
        ContractsSparkline = BuildSparkline(data.ContractsSparkline, "#EA580C");
        SharedSparkline = BuildSparkline(data.SharedSparkline, "#6D28D9");
        StorageSparkline = BuildSparkline(data.StorageSparkline, "#0EA5E9");
        CriticalSparkline = BuildSparkline(data.CriticalSparkline, "#DC2626");
    }

    private static ISeries[] BuildSparkline(IReadOnlyList<int> values, string color) =>
    [
        new LineSeries<int>
        {
            Values = values.Count == 0 ? [0] : values.ToArray(),
            Stroke = new SolidColorPaint(SKColor.Parse(color)) { StrokeThickness = 2 },
            Fill = new SolidColorPaint(SKColor.Parse(color).WithAlpha(40)),
            GeometrySize = 0,
            LineSmoothness = 0.6
        }
    ];

    private static string FormatStorage(long used, long quota)
    {
        static string F(long b)
        {
            if (b < 1024L * 1024 * 1024) return $"{b / (1024.0 * 1024):0.#} Mo";
            return $"{b / (1024.0 * 1024 * 1024):0.#} Go";
        }
        return $"{F(used)} / {F(quota)}";
    }

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
            : name.Length >= 2 ? name[..2].ToUpperInvariant() : "AD";
    }

    private void RefreshCriticalNotifications()
    {
        CriticalNotifications.Clear();
        foreach (var doc in _allDocuments
                     .Where(d => d.IsCritical && !d.IsDeleted && !d.IsArchived)
                     .OrderByDescending(ParseDocDate))
            CriticalNotifications.Add(doc);
        OnPropertyChanged(nameof(HasCriticalNotifications));
    }

    private static List<DocumentTagItem> BuildAllTags(IEnumerable<DocumentListItem> documents)
    {
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
            ["RAPPORT"] = ("#F3E8FF", "#7C3AED"),
            ["IMPORT"] = ("#E0F2FE", "#0369A1"),
            ["ARCHIVE"] = ("#F1F5F9", "#64748B")
        };

        return documents
            .Where(d => !d.IsDeleted && !d.IsArchived)
            .SelectMany(d => d.Tags)
            .GroupBy(t => t.Label, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                palette.TryGetValue(g.Key, out var colors);
                colors = colors == default ? ("#F1F5F9", "#475569") : colors;
                return new DocumentTagItem
                {
                    Label = g.Key,
                    Background = colors.Bg,
                    Foreground = colors.Fg,
                    Count = g.Count()
                };
            })
            .ToList();
    }
}

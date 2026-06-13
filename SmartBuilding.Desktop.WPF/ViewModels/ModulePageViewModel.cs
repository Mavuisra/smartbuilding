using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class ModulePageViewModel : BaseViewModel
{
    private readonly ModuleDataService _dataService;
    private List<ModuleListRow> _allRows = [];

    [ObservableProperty] private string _moduleId = string.Empty;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _subtitle = string.Empty;
    [ObservableProperty] private string _iconKind = "FolderOutline";
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _companyName = BuildingInfoDefaults.CompanyName;

    public ObservableCollection<string> ColumnHeaders { get; } = [];
    public ObservableCollection<ModuleListRow> Items { get; } = [];

    public ModulePageViewModel(ModuleDataService dataService, AppConfigurationService appConfiguration)
    {
        _dataService = dataService;
        CompanyName = appConfiguration.Current.CompanyName;
        appConfiguration.ConfigurationChanged += (_, _) =>
            CompanyName = appConfiguration.Current.CompanyName;
    }

    public void Initialize(string moduleId)
    {
        var def = ModuleRegistry.Get(moduleId);
        ModuleId = def.Id;
        Title = def.Title;
        Subtitle = def.Subtitle;
        IconKind = def.IconKind;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var result = await _dataService.LoadAsync(ModuleId);
            ColumnHeaders.Clear();
            foreach (var h in result.Headers)
                ColumnHeaders.Add(h);

            _allRows = result.Rows.ToList();
            TotalCount = result.TotalCount;
            ApplyFilter();
            StatusMessage = $"{Items.Count} élément(s) affiché(s)";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur de chargement : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Refresh() => LoadCommand.Execute(null);

    partial void OnSearchQueryChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        Items.Clear();
        var query = SearchQuery.Trim();
        var rows = string.IsNullOrEmpty(query)
            ? _allRows
            : _allRows.Where(r =>
                r.Col0.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                r.Col1.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                r.Col2.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                r.Col3.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                r.Col4.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                r.Col5.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var row in rows)
            Items.Add(row);
    }
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;
using SmartBuilding.Domain.Entities.Building;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class LocationsPatrimoineViewModel
{
    private List<PatrimoineUnitRow> _allGestionUnits = [];

    [ObservableProperty] private string _gestionSearchQuery = string.Empty;
    [ObservableProperty] private string _gestionFilterType = "Tous";
    [ObservableProperty] private string _gestionFilterStatus = "Tous";
    [ObservableProperty] private PatrimoineUnitRow? _selectedGestionUnit;
    [ObservableProperty] private int _gestionDisplayedCount;

    [ObservableProperty] private int _gestionKpiTotalUnits;
    [ObservableProperty] private int _gestionKpiAvailable;
    [ObservableProperty] private int _gestionKpiOccupied;
    [ObservableProperty] private int _gestionKpiFloors;
    [ObservableProperty] private string _gestionKpiTotalRentDisplay = "0 USD";
    [ObservableProperty] private string _gestionKpiTotalAreaDisplay = "0 m²";

    public bool HasSelectedGestionUnit => SelectedGestionUnit is not null;

    public ObservableCollection<PatrimoineUnitRow> GestionUnits { get; } = [];

    public ObservableCollection<string> GestionTypeFilters { get; } =
    [
        "Tous",
        PropertyStructureConstants.UnitTypes.Apartment,
        PropertyStructureConstants.UnitTypes.Commercial
    ];

    public ObservableCollection<string> GestionStatusFilters { get; } = ["Tous", "Libre", "Occupé", "Réservé", "Maintenance"];

    partial void OnSelectedGestionUnitChanged(PatrimoineUnitRow? value)
    {
        OnPropertyChanged(nameof(HasSelectedGestionUnit));
        OnPropertyChanged(nameof(DetailUnitTitle));
        OnPropertyChanged(nameof(DetailUnitSubtitle));
    }

    public string DetailUnitTitle => SelectedGestionUnit is null
        ? "Sélectionnez une unité"
        : $"{SelectedGestionUnit.Code} — {SelectedGestionUnit.Name}";

    public string DetailUnitSubtitle => SelectedGestionUnit is null
        ? "Double-cliquez sur une ligne ou utilisez l'icône œil."
        : $"{SelectedGestionUnit.BuildingName} · {SelectedGestionUnit.FloorDisplay} · {SelectedGestionUnit.UnitType}";

    partial void OnGestionSearchQueryChanged(string value) => ApplyGestionFilter();

    partial void OnGestionFilterTypeChanged(string value) => ApplyGestionFilter();

    partial void OnGestionFilterStatusChanged(string value) => ApplyGestionFilter();

    private async Task LoadGestionAsync()
    {
        _allGestionUnits = (await _propertyStructureService.GetManagementUnitsAsync()).ToList();
        RefreshGestionKpis();
        ApplyGestionFilter();
        if (SelectedGestionUnit is not null
            && !_allGestionUnits.Any(u => u.ApartmentId == SelectedGestionUnit.ApartmentId))
            SelectedGestionUnit = null;
    }

    private void RefreshGestionKpis()
    {
        GestionKpiTotalUnits = _allGestionUnits.Count;
        GestionKpiAvailable = _allGestionUnits.Count(u => !u.IsOccupied);
        GestionKpiOccupied = _allGestionUnits.Count(u => u.IsOccupied);
        GestionKpiFloors = _allGestionUnits.Select(u => u.FloorId).Distinct().Count();
        var totalRent = _allGestionUnits.Sum(u => u.MonthlyRent);
        var totalArea = _allGestionUnits.Sum(u => u.AreaSqM);
        GestionKpiTotalRentDisplay = totalRent > 0 ? $"{totalRent:0} USD" : "0 USD";
        GestionKpiTotalAreaDisplay = totalArea > 0 ? $"{totalArea:0.##} m²" : "0 m²";
    }

    private void ApplyGestionFilter()
    {
        var q = GestionSearchQuery.Trim();
        IEnumerable<PatrimoineUnitRow> items = _allGestionUnits;

        if (!string.IsNullOrWhiteSpace(q))
        {
            items = items.Where(u =>
                u.Code.Contains(q, StringComparison.OrdinalIgnoreCase)
                || u.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || u.FloorLabel.Contains(q, StringComparison.OrdinalIgnoreCase)
                || u.UnitType.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(GestionFilterType, "Tous", StringComparison.OrdinalIgnoreCase))
            items = items.Where(u => u.UnitType.Contains(GestionFilterType, StringComparison.OrdinalIgnoreCase));

        if (!string.Equals(GestionFilterStatus, "Tous", StringComparison.OrdinalIgnoreCase))
            items = items.Where(u => string.Equals(u.OccupancyLabel, GestionFilterStatus, StringComparison.OrdinalIgnoreCase));

        GestionUnits.Clear();
        foreach (var row in items)
            GestionUnits.Add(row);

        GestionDisplayedCount = GestionUnits.Count;
    }

    [RelayCommand]
    private void OpenGestionUnitDetail(PatrimoineUnitRow? row)
    {
        if (row is not null)
            SelectedGestionUnit = row;
    }

    [RelayCommand]
    private void CloseGestionUnitDetail() => SelectedGestionUnit = null;

    [RelayCommand]
    private void EditGestionUnitInStructure(PatrimoineUnitRow? row)
    {
        if (row is null)
            return;

        SelectedGestionUnit = row;
        SelectedPatrimoineTab = 2;
        ExpandUnitInStructureEditor(row.ApartmentId);
    }

    [RelayCommand]
    private void AddGestionUnitInStructure()
    {
        SelectedPatrimoineTab = 2;
        if (PropertyFloors.Count == 0)
            AddDefaultPropertyFloor();
        var floor = PropertyFloors.Last();
        floor.IsExpanded = true;
        AddPropertyApartment(floor);
    }

    [RelayCommand]
    private async Task DeleteGestionUnitAsync(PatrimoineUnitRow? row)
    {
        if (!CanManage || row is null)
            return;

        if (!SbmsDialogService.Confirm(
                "Supprimer l'unité",
                $"Supprimer « {row.Code} — {row.Name} » ? Cette action est irréversible."))
            return;

        IsBusy = true;
        FormError = null;
        try
        {
            var error = await _propertyStructureService.DeleteUnitAsync(row.ApartmentId);
            if (!string.IsNullOrEmpty(error))
            {
                FormError = error;
                return;
            }

            StatusMessage = "Unité supprimée.";
            SelectedGestionUnit = null;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            FormError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshGestionAsync() => await LoadAsync();

    private void ExpandUnitInStructureEditor(Guid apartmentId)
    {
        foreach (var floor in PropertyFloors)
        {
            var apt = floor.Apartments.FirstOrDefault(a => a.EntityId == apartmentId);
            if (apt is null)
                continue;

            floor.IsExpanded = true;
            apt.IsExpanded = true;
            return;
        }
    }
}

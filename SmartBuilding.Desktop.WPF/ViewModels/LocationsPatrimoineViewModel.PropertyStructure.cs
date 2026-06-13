using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Shared.Constants;
using SmartBuilding.Desktop.WPF.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class LocationsPatrimoineViewModel
{
    [ObservableProperty] private string? _propertyStructureError;
    [ObservableProperty] private string _structureSummaryLine = "Aucune structure définie";

    public ObservableCollection<PropertyFloorEditRow> PropertyFloors { get; } = [];

    public ObservableCollection<string> UnitTypeChoices { get; } =
    [
        PropertyStructureConstants.UnitTypes.Apartment,
        PropertyStructureConstants.UnitTypes.Commercial
    ];

    public ObservableCollection<string> RoomTypeChoices { get; } =
    [
        PropertyStructureConstants.RoomTypes.Bedroom,
        PropertyStructureConstants.RoomTypes.LivingRoom,
        PropertyStructureConstants.RoomTypes.Kitchen,
        PropertyStructureConstants.RoomTypes.Bathroom,
        PropertyStructureConstants.RoomTypes.Toilet,
        PropertyStructureConstants.RoomTypes.Office,
        PropertyStructureConstants.RoomTypes.Storage,
        PropertyStructureConstants.RoomTypes.Balcony,
        PropertyStructureConstants.RoomTypes.Other
    ];

    private async Task LoadPropertyStructureAsync()
    {
        var drafts = await _propertyStructureService.LoadAsync();
        PropertyFloors.Clear();
        foreach (var f in drafts)
            PropertyFloors.Add(PropertyFloorEditRow.FromDraft(f));

        if (PropertyFloors.Count == 0)
            AddDefaultPropertyFloor();

        RefreshStructureSummary();
    }

    private void AddDefaultPropertyFloor()
    {
        PropertyFloors.Add(new PropertyFloorEditRow
        {
            LevelNumberText = "0",
            Label = "RDC",
            IsExpanded = true
        });
    }

    private void RefreshStructureSummary()
    {
        var drafts = PropertyFloors.Select((f, i) => f.ToDraft(i)).ToList();
        var summary = PropertyStructureService.ComputeSummary(drafts);

        BuildingFloors = summary.FloorCount;
        ApartmentCount = summary.ApartmentCount;
        CommercialUnitCount = summary.CommercialCount;
        TotalPremisesConfig = summary.ApartmentCount + summary.CommercialCount;
        if (summary.TotalAreaSqM > 0)
            BuildingAreaSqMValue = summary.TotalAreaSqM;

        StructureSummaryLine =
            $"{BrandConstants.AppName} · {summary.FloorCount} étage(s) · {summary.ApartmentCount + summary.CommercialCount} local(aux)";
    }

    [RelayCommand]
    private void AddPropertyFloor()
    {
        var nextLevel = PropertyFloors.Count == 0
            ? 0
            : PropertyFloors.Max(f => int.TryParse(f.LevelNumberText, out var n) ? n : 0) + 1;

        PropertyFloors.Add(new PropertyFloorEditRow
        {
            LevelNumberText = nextLevel.ToString(),
            Label = nextLevel == 0 ? "RDC" : $"{nextLevel}e",
            IsExpanded = true
        });
        RefreshStructureSummary();
    }

    [RelayCommand]
    private void RemovePropertyFloor(PropertyFloorEditRow? row)
    {
        if (row is null)
            return;
        PropertyFloors.Remove(row);
        if (PropertyFloors.Count == 0)
            AddDefaultPropertyFloor();
        RefreshStructureSummary();
    }

    [RelayCommand]
    private void AddPropertyApartment(PropertyFloorEditRow? floor)
    {
        if (floor is null)
            return;

        var index = floor.Apartments.Count + 1;
        var apt = new PropertyApartmentEditRow
        {
            Code = $"L{floor.LevelNumberText}-{index:D2}",
            Name = $"Local {floor.Label}-{index}",
            UnitType = PropertyStructureConstants.UnitTypes.Apartment,
            IsExpanded = true
        };
        floor.Apartments.Add(apt);
        floor.IsExpanded = true;
        RefreshStructureSummary();
    }

    [RelayCommand]
    private void RemovePropertyApartment(PropertyApartmentEditRow? apartment)
    {
        if (apartment is null)
            return;
        foreach (var floor in PropertyFloors)
        {
            if (floor.Apartments.Remove(apartment))
                break;
        }
        RefreshStructureSummary();
    }

    [RelayCommand]
    private void AddPropertyRoom(PropertyApartmentEditRow? apartment)
    {
        if (apartment is null)
            return;

        apartment.Rooms.Add(new PropertyRoomEditRow
        {
            Name = $"Chambre {apartment.Rooms.Count + 1}",
            RoomType = PropertyStructureConstants.RoomTypes.Bedroom
        });
        apartment.IsExpanded = true;
        RefreshStructureSummary();
    }

    [RelayCommand]
    private void RemovePropertyRoom(PropertyRoomEditRow? room)
    {
        if (room is null)
            return;
        foreach (var apt in PropertyFloors.SelectMany(f => f.Apartments))
        {
            if (apt.Rooms.Remove(room))
                break;
        }
        RefreshStructureSummary();
    }

    private async Task<string?> SavePropertyStructureAsync()
    {
        PropertyStructureError = null;
        var drafts = PropertyFloors.Select((f, i) => f.ToDraft(i)).ToList();
        return await _propertyStructureService.SaveAsync(drafts, BrandConstants.AppName);
    }
}

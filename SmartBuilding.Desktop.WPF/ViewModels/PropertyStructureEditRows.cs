using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class PropertyRoomEditRow : ObservableObject
{
    public Guid RowId { get; } = Guid.NewGuid();
    public Guid? EntityId { get; set; }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _roomType = PropertyStructureConstants.RoomTypes.Bedroom;
    [ObservableProperty] private string _areaSqMText = string.Empty;

    public PropertyRoomDraft ToDraft(int sortOrder) => new()
    {
        Id = EntityId,
        Name = Name,
        RoomType = RoomType,
        AreaSqM = ParseDecimal(AreaSqMText),
        SortOrder = sortOrder
    };

    public static PropertyRoomEditRow FromDraft(PropertyRoomDraft d) => new()
    {
        EntityId = d.Id,
        Name = d.Name,
        RoomType = d.RoomType,
        AreaSqMText = d.AreaSqM > 0 ? d.AreaSqM.ToString("0.##") : string.Empty
    };

    private static decimal ParseDecimal(string text) =>
        decimal.TryParse(text.Replace(',', '.'), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
}

public partial class PropertyApartmentEditRow : ObservableObject
{
    public Guid RowId { get; } = Guid.NewGuid();
    public Guid? EntityId { get; set; }

    [ObservableProperty] private string _code = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _unitType = PropertyStructureConstants.UnitTypes.Apartment;
    [ObservableProperty] private string _areaSqMText = string.Empty;
    [ObservableProperty] private string _monthlyRentText = string.Empty;
    [ObservableProperty] private bool _isExpanded = true;

    public ObservableCollection<PropertyRoomEditRow> Rooms { get; } = [];

    public bool IsResidential =>
        UnitType.Contains("Appartement", StringComparison.OrdinalIgnoreCase);

    partial void OnUnitTypeChanged(string value) => OnPropertyChanged(nameof(IsResidential));

    public PropertyApartmentDraft ToDraft(int sortOrder) => new()
    {
        Id = EntityId,
        Code = Code,
        Name = Name,
        UnitType = UnitType,
        AreaSqM = ParseDecimal(AreaSqMText),
        MonthlyRent = ParseDecimal(MonthlyRentText),
        SortOrder = sortOrder,
        Rooms = Rooms.Select((r, i) => r.ToDraft(i)).ToList()
    };

    public static PropertyApartmentEditRow FromDraft(PropertyApartmentDraft d)
    {
        var row = new PropertyApartmentEditRow
        {
            EntityId = d.Id,
            Code = d.Code,
            Name = d.Name,
            UnitType = d.UnitType,
            AreaSqMText = d.AreaSqM > 0 ? d.AreaSqM.ToString("0.##") : string.Empty,
            MonthlyRentText = d.MonthlyRent > 0 ? d.MonthlyRent.ToString("0.##") : string.Empty
        };
        foreach (var room in d.Rooms)
            row.Rooms.Add(PropertyRoomEditRow.FromDraft(room));
        return row;
    }

    private static decimal ParseDecimal(string text) =>
        decimal.TryParse(text.Replace(',', '.'), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
}

public partial class PropertyFloorEditRow : ObservableObject
{
    public Guid RowId { get; } = Guid.NewGuid();
    public Guid? EntityId { get; set; }

    [ObservableProperty] private string _levelNumberText = "0";
    [ObservableProperty] private string _label = string.Empty;
    [ObservableProperty] private bool _isExpanded = true;

    public ObservableCollection<PropertyApartmentEditRow> Apartments { get; } = [];

    public PropertyFloorDraft ToDraft(int sortOrder) => new()
    {
        Id = EntityId,
        LevelNumber = int.TryParse(LevelNumberText, out var n) ? n : 0,
        Label = Label,
        SortOrder = sortOrder,
        Apartments = Apartments.Select((a, i) => a.ToDraft(i)).ToList()
    };

    public static PropertyFloorEditRow FromDraft(PropertyFloorDraft d)
    {
        var row = new PropertyFloorEditRow
        {
            EntityId = d.Id,
            LevelNumberText = d.LevelNumber.ToString(),
            Label = d.Label
        };
        foreach (var apt in d.Apartments)
            row.Apartments.Add(PropertyApartmentEditRow.FromDraft(apt));
        return row;
    }
}

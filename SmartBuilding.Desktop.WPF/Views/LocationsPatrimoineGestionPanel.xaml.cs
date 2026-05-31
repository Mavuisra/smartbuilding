using System.Windows.Controls;
using System.Windows.Input;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.ViewModels;

namespace SmartBuilding.Desktop.WPF.Views;

public partial class LocationsPatrimoineGestionPanel
{
    public LocationsPatrimoineGestionPanel() => InitializeComponent();

    private void GestionUnitsGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not LocationsPatrimoineViewModel vm)
            return;
        if (GestionUnitsGrid.SelectedItem is PatrimoineUnitRow row)
            vm.OpenGestionUnitDetailCommand.Execute(row);
    }
}

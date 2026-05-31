using System.Windows.Controls;
using System.Windows.Input;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.ViewModels;

namespace SmartBuilding.Desktop.WPF.Views;

public partial class LocationsTenantsView : UserControl
{
    public LocationsTenantsView() => InitializeComponent();

    private async void TenantsGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not LocationsTenantsViewModel vm)
            return;
        if (sender is not DataGrid grid || grid.SelectedItem is not LocationsTenantItem item)
            return;
        if (vm.OpenDetailCommand.CanExecute(item))
            await vm.OpenDetailCommand.ExecuteAsync(item);
    }
}

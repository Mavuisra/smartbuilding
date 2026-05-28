using System.Windows.Controls;
using System.Windows.Input;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.ViewModels;

namespace SmartBuilding.Desktop.WPF.Views;

public partial class IncidentsView
{
    public IncidentsView() => InitializeComponent();

    private void IncidentRow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridRow row || row.DataContext is not IncidentListItem item)
            return;
        if (DataContext is IncidentsViewModel vm)
            vm.OpenIncidentDetailCommand.Execute(item);
    }
}

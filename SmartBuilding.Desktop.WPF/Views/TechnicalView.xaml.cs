using System.Windows.Controls;
using System.Windows.Input;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.ViewModels;

namespace SmartBuilding.Desktop.WPF.Views;

public partial class TechnicalView
{
    public TechnicalView() => InitializeComponent();

    private void EquipmentRow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridRow row || row.DataContext is not TechnicalEquipmentItem item)
            return;
        if (DataContext is TechnicalViewModel vm)
            vm.OpenEquipmentDetailCommand.Execute(item);
    }
}

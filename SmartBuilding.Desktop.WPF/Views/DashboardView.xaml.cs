using System.Windows.Controls;
using System.Windows.Input;

namespace SmartBuilding.Desktop.WPF.Views;

public partial class DashboardView : UserControl
{
    public DashboardView() => InitializeComponent();

    private void NotificationsOverlay_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.DashboardViewModel vm)
            vm.CloseNotificationsCommand.Execute(null);
    }
}

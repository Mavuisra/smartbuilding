using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.ViewModels;

namespace SmartBuilding.Desktop.WPF.Views;

public partial class DocumentsView
{
    public DocumentsView() => InitializeComponent();

    private void DocumentCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not DocumentListItem doc)
            return;
        if (DataContext is DocumentsViewModel vm)
            vm.SelectDocumentCommand.Execute(doc);
    }

    private void NotificationsOverlay_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is DocumentsViewModel vm)
            vm.CloseNotificationsCommand.Execute(null);
    }
}

using System.Windows;
using System.Windows.Input;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.ViewModels;

namespace SmartBuilding.Desktop.WPF.Views;

public partial class UsersView
{
    public UsersView() => InitializeComponent();

    private void UserCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: UserListItem user })
            return;
        if (DataContext is UsersViewModel vm)
            vm.SelectUserCommand.Execute(user);
    }
}

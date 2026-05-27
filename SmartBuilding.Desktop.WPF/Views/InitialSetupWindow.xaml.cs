using System.Windows;
using SmartBuilding.Desktop.WPF.ViewModels;

namespace SmartBuilding.Desktop.WPF.Views;

public partial class InitialSetupWindow : Window
{
    private readonly InitialSetupViewModel _vm;

    public InitialSetupWindow(InitialSetupViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
        _vm.CloseRequested += OnCloseRequested;
    }

    private void OnCloseRequested(bool success)
    {
        DialogResult = success;
        Close();
    }

    private void PasswordBoxMain_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        _vm.AdminPassword = PasswordBoxMain.Password;
    }

    private void PasswordBoxConfirm_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        _vm.AdminPasswordConfirm = PasswordBoxConfirm.Password;
    }
}

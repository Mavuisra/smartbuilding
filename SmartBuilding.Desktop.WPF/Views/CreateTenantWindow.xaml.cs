using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SmartBuilding.Desktop.WPF.ViewModels;

namespace SmartBuilding.Desktop.WPF.Views;

public partial class CreateTenantWindow : Window
{
    private readonly CreateTenantViewModel _vm;

    public CreateTenantWindow(IServiceProvider services)
    {
        InitializeComponent();
        _vm = ActivatorUtilities.CreateInstance<CreateTenantViewModel>(services);
        DataContext = _vm;
    }

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.CreateCommand.CanExecute(null))
            await _vm.CreateCommand.ExecuteAsync(null);

        if (_vm.Succeeded)
        {
            DialogResult = true;
            Close();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void AdminPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.PasswordBox box)
            _vm.AdminPassword = box.Password;
    }
}

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
        _vm.TenantCreated += OnTenantCreated;
        Loaded += (_, _) => AdminPasswordBox.Focus();
    }

    public string? CreatedAdminUsername => _vm.Succeeded ? _vm.AdminUsername.Trim() : null;
    public string? CreatedAdminPassword => _vm.Succeeded ? _vm.AdminPassword : null;

    private void OnTenantCreated()
    {
        DialogResult = true;
        Close();
    }

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        await _vm.CreateCommand.ExecuteAsync(null);
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

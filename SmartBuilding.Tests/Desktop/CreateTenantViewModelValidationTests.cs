using SmartBuilding.Desktop.WPF.ViewModels;
using Xunit;

namespace SmartBuilding.Tests.Desktop;

public sealed class CreateTenantViewModelValidationTests
{
    [Fact]
    public void ValidateInputs_rejects_short_password_with_message()
    {
        var vm = new CreateTenantViewModel(null!, null!, null!);
        vm.TenantName = "Ma Société";
        vm.AdminUsername = "admin.test";
        vm.AdminPassword = "12345";

        var task = vm.CreateCommand.ExecuteAsync(null);
        task.Wait(TimeSpan.FromSeconds(5));

        Assert.False(vm.Succeeded);
        Assert.Contains("6 caractères", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateInputs_rejects_empty_tenant_name()
    {
        var vm = new CreateTenantViewModel(null!, null!, null!);
        vm.TenantName = "A";
        vm.AdminUsername = "admin.test";
        vm.AdminPassword = "Test@2026";

        var task = vm.CreateCommand.ExecuteAsync(null);
        task.Wait(TimeSpan.FromSeconds(5));

        Assert.False(vm.Succeeded);
        Assert.Contains("2 caractères", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}

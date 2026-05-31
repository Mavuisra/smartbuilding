using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Desktop.WPF.Services;
using SmartBuilding.Infrastructure.Sync;
using SmartBuilding.Shared.DTOs.Auth;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly SessionService _session;
    private readonly IConfiguration _configuration;
    private readonly Action _onLoginSuccess;

    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _rememberMe = true;
    [ObservableProperty] private bool _isDarkMode;

    public LoginViewModel(
        IAuthService authService,
        SessionService session,
        IConfiguration configuration,
        Action onLoginSuccess)
    {
        _authService = authService;
        _session = session;
        _configuration = configuration;
        _onLoginSuccess = onLoginSuccess;
        Username = LoadRememberedUsername() ?? "admin";
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var result = await _authService.LoginAsync(new LoginRequest
            {
                Username = Username.Trim(),
                Password = Password
            });

            if (result is null)
            {
                ErrorMessage = "Nom d'utilisateur ou mot de passe incorrect.";
                return;
            }

            if (RememberMe)
                SaveRememberedUsername(Username.Trim());

            _session.SetUser(result);

            await SyncCloudTokenStore.AcquireAsync(
                _configuration,
                Username.Trim(),
                Password,
                cancellationToken: default);

            var invoke = System.Windows.Application.Current?.Dispatcher;
            if (invoke is not null && !invoke.CheckAccess())
                invoke.Invoke(_onLoginSuccess);
            else
                _onLoginSuccess();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void UseAlternateAccount()
    {
        Username = string.Empty;
        Password = string.Empty;
        ErrorMessage = null;
    }

    [RelayCommand]
    private void ToggleDarkMode() => IsDarkMode = !IsDarkMode;

    private static string? LoadRememberedUsername()
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SmartBuilding", "remembered_user.txt");
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch { return null; }
    }

    private static void SaveRememberedUsername(string username)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmartBuilding");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "remembered_user.txt"), username);
        }
        catch { /* ignore */ }
    }
}

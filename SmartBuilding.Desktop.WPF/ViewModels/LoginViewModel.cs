using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Desktop.WPF.Services;
using SmartBuilding.Infrastructure.Services;
using SmartBuilding.Infrastructure.Sync;
using SmartBuilding.Shared.DTOs.Auth;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly ISyncService _syncService;
    private readonly CloudIdentityService _cloudIdentity;
    private readonly SessionService _session;
    private readonly Action _onLoginSuccess;

    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _rememberMe = true;
    [ObservableProperty] private bool _isDarkMode;
    [ObservableProperty] private bool _isErrorDialogVisible;
    [ObservableProperty] private string? _errorDialogMessage;
    [ObservableProperty] private string _loginProgressText = "Connexion en cours…";

    public AppBrandingState Branding { get; }

    public LoginViewModel(
        IAuthService authService,
        ISyncService syncService,
        CloudIdentityService cloudIdentity,
        SessionService session,
        AppBrandingState branding,
        Action onLoginSuccess)
    {
        _authService = authService;
        _syncService = syncService;
        _cloudIdentity = cloudIdentity;
        _session = session;
        Branding = branding;
        _onLoginSuccess = onLoginSuccess;
        Username = LoadRememberedUsername() ?? "admin";
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        DismissErrorDialog();
        IsBusy = true;
        LoginProgressText = "Vérification des identifiants…";

        try
        {
            if (string.IsNullOrWhiteSpace(Username))
            {
                ShowLoginError("Veuillez saisir votre nom d'utilisateur.");
                return;
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                ShowLoginError("Veuillez saisir votre mot de passe.");
                return;
            }

            var result = await _authService.LoginAsync(new LoginRequest
            {
                Username = Username.Trim(),
                Password = Password
            });

            if (result is null)
            {
                ShowLoginError(
                    "Nom d'utilisateur ou mot de passe incorrect.\n\n" +
                    "Utilisez le compte créé lors de la configuration initiale, ou admin / Admin@2026 si c'est une ancienne base.");
                return;
            }

            if (RememberMe)
                SaveRememberedUsername(Username.Trim());

            _session.SetUser(result);

            await PublishAndSyncToCloudAsync(Username.Trim(), Password);

            LoginProgressText = "Ouverture de l'application…";

            var invoke = System.Windows.Application.Current?.Dispatcher;
            if (invoke is not null && !invoke.CheckAccess())
                invoke.Invoke(_onLoginSuccess);
            else
                _onLoginSuccess();
        }
        catch (Exception ex)
        {
            ShowLoginError(DbSaveExceptionTranslator.ToUserMessage(ex));
        }
        finally
        {
            IsBusy = false;
            LoginProgressText = "Connexion en cours…";
        }
    }

    [RelayCommand]
    private void DismissErrorDialog()
    {
        IsErrorDialogVisible = false;
        ErrorDialogMessage = null;
        ErrorMessage = null;
    }

    private void ShowLoginError(string message)
    {
        ErrorMessage = message;
        ErrorDialogMessage = message;
        IsErrorDialogVisible = true;
    }

    [RelayCommand]
    private void UseAlternateAccount()
    {
        Username = string.Empty;
        Password = string.Empty;
        DismissErrorDialog();
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

    private async Task PublishAndSyncToCloudAsync(string username, string password)
    {
        try
        {
            if (CloudIdentityStore.IsAlreadyLinkedForUser(username))
            {
                var linked = CloudIdentityStore.TryGetForUser(username, out var state) ? state : null;
                _session.SetCloudIdentityStatus(
                    true,
                    linked?.Message ?? "Compte cloud déjà configuré — synchronisation en arrière-plan.");
                return;
            }

            LoginProgressText = "Connexion au cloud (première fois)…";
            var identity = await _cloudIdentity.EnsureCloudLoginAsync(username, password);
            _session.SetCloudIdentityStatus(identity.Success, identity.Message);

            if (!identity.Success)
                return;

            CloudIdentityStore.MarkLinked(username, identity.Message);

            LoginProgressText = "Synchronisation initiale des données…";
            await _syncService.SyncAsync(manual: false);

            if (await _syncService.IsCloudStoreEmptyAsync())
            {
                LoginProgressText = "Publication complète des données locales…";
                await _syncService.MarkAllLocalDataForPushAsync();
                await _syncService.SyncAsync(manual: false);
            }
        }
        catch (Exception ex)
        {
            _session.SetCloudIdentityStatus(false, ex.Message);
        }
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

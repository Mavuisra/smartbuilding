using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Desktop.WPF.Services;
using SmartBuilding.Desktop.WPF.Views;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Services;
using SmartBuilding.Infrastructure.Sync;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IServiceProvider _rootServices;
    private readonly CloudIdentityService _cloudIdentity;
    private readonly OrganizationCloudSyncService _organizationCloudSync;
    private readonly OrganizationLoginResolver _loginResolver;
    private readonly SessionService _session;
    private readonly PersistentSessionStore _persistentSession;
    private readonly CompanyProfileCompletionService _companyProfileCompletion;
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
        IServiceScopeFactory scopeFactory,
        IServiceProvider rootServices,
        CloudIdentityService cloudIdentity,
        OrganizationCloudSyncService organizationCloudSync,
        OrganizationLoginResolver loginResolver,
        SessionService session,
        PersistentSessionStore persistentSession,
        CompanyProfileCompletionService companyProfileCompletion,
        AppBrandingState branding,
        Action onLoginSuccess)
    {
        _scopeFactory = scopeFactory;
        _rootServices = rootServices;
        _cloudIdentity = cloudIdentity;
        _organizationCloudSync = organizationCloudSync;
        _loginResolver = loginResolver;
        _session = session;
        _persistentSession = persistentSession;
        _companyProfileCompletion = companyProfileCompletion;
        Branding = branding;
        _onLoginSuccess = onLoginSuccess;

        if (_persistentSession.TryLoad(out var stored))
        {
            Username = stored.Username;
            RememberMe = true;
        }
        else
        {
            Username = LoadRememberedUsername() ?? string.Empty;
        }

        Branding.CompanyName = "Smart Building MS";
        Branding.AppSubtitle = AppBrandingState.DefaultSubtitle;
    }

    public void ClearPassword() => Password = string.Empty;

    public async Task<bool> TryRestoreSessionAsync()
    {
        if (!_persistentSession.TryLoad(out var stored) || !stored.IsValid())
            return false;

        var password = _persistentSession.UnprotectPassword(stored);
        if (string.IsNullOrWhiteSpace(password))
        {
            _persistentSession.Clear();
            return false;
        }

        Username = stored.Username;
        Password = password;
        RememberMe = true;
        await LoginAsync();
        return _session.IsAuthenticated;
    }

    [RelayCommand]
    private async Task CreateTenantAsync()
    {
        var window = new CreateTenantWindow(_rootServices)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (window.ShowDialog() != true)
            return;

        Username = window.CreatedAdminUsername ?? string.Empty;
        Password = window.CreatedAdminPassword ?? string.Empty;
        RememberMe = true;

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            return;

        await LoginAsync();
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        DismissErrorDialog();
        IsBusy = true;
        LoginProgressText = "Recherche de votre organisation…";

        try
        {
            LoginProgressText = "Vérification des identifiants…";
            var loginResult = await _loginResolver.LoginAsync(Username, Password);

            if (!loginResult.Success || loginResult.Organization is null || loginResult.User is null)
            {
                ShowLoginError(loginResult.Message);
                return;
            }

            _session.SetOrganization(loginResult.Organization);
            _session.SetUser(loginResult.User);

            var needsProfile = await _companyProfileCompletion.NeedsSetupAsync();
            _session.SetPendingCompanyProfileSetup(needsProfile);

            if (RememberMe)
            {
                SaveRememberedUsername(Username.Trim());
                _persistentSession.Save(
                    Username.Trim(),
                    loginResult.Organization.Id,
                    Password);
            }
            else
            {
                _persistentSession.Clear();
            }

            await _organizationCloudSync.RegisterActiveOrganizationAsync(Username.Trim(), Password);

            using var syncScope = _scopeFactory.CreateScope();
            var syncService = syncScope.ServiceProvider.GetRequiredService<ISyncService>();
            await PublishAndSyncToCloudAsync(syncService, Username.Trim(), Password);

            if (RememberMe && _persistentSession.TryLoad(out var stored))
                _persistentSession.RefreshExpiry(stored);

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
        RememberMe = false;
        _persistentSession.Clear();
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

    private async Task PublishAndSyncToCloudAsync(ISyncService syncService, string username, string password)
    {
        try
        {
            if (await syncService.NeedsInitialCloudPullAsync())
            {
                await EnsureCloudSessionAsync(username, password);
                if (!_session.IsCloudIdentityLinked)
                    return;

                await RunInitialCloudPullAsync(syncService);
                return;
            }

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

            if (await syncService.IsCloudStoreEmptyAsync())
            {
                LoginProgressText = "Publication complète des données locales vers le cloud…";
                await syncService.MarkAllLocalDataForPushAsync();
                var pushResult = await syncService.SyncAsync(manual: false);
                if (pushResult.Success)
                    InitialSyncStore.MarkCompleted();
            }
            else
            {
                await syncService.SyncAsync(manual: false);
                InitialSyncStore.MarkCompleted();
            }
        }
        catch (Exception ex)
        {
            _session.SetCloudIdentityStatus(false, ex.Message);
        }
    }

    private async Task EnsureCloudSessionAsync(string username, string password)
    {
        if (CloudIdentityStore.IsAlreadyLinkedForUser(username))
            return;

        LoginProgressText = "Connexion au cloud (première fois)…";
        var identity = await _cloudIdentity.EnsureCloudLoginAsync(username, password);
        _session.SetCloudIdentityStatus(identity.Success, identity.Message);

        if (identity.Success)
            CloudIdentityStore.MarkLinked(username, identity.Message);
    }

    private async Task RunInitialCloudPullAsync(ISyncService syncService)
    {
        LoginProgressText = "Synchronisation initiale — téléchargement depuis le cloud…";
        var pullResult = await syncService.PerformInitialCloudPullAsync();

        if (pullResult.Success)
        {
            InitialSyncStore.MarkCompleted();
            _session.SetCloudIdentityStatus(
                true,
                $"Données cloud téléchargées ({pullResult.Pulled} enregistrement(s)). Travail local prêt.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(pullResult.Error))
            _session.SetCloudIdentityStatus(false, pullResult.Error);
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

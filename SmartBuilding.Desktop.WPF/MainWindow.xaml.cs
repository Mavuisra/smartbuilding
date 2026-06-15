using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using SmartBuilding.Desktop.WPF.Services;
using SmartBuilding.Domain.Entities.Building;
using SmartBuilding.Desktop.WPF.ViewModels;
using SmartBuilding.Infrastructure.Services;

namespace SmartBuilding.Desktop.WPF;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly IServiceScopeFactory _scopeFactory;
    private IServiceScope? _loginScope;
    private IServiceScope? _shellScope;

    public MainWindow(IServiceProvider services, IServiceScopeFactory scopeFactory)
    {
        _services = services;
        _scopeFactory = scopeFactory;
        InitializeComponent();
        ShowLogin();
    }

    protected override void OnClosed(EventArgs e)
    {
        _shellScope?.Dispose();
        _loginScope?.Dispose();
        base.OnClosed(e);
    }

    private void ShowLogin()
    {
        _shellScope?.Dispose();
        _shellScope = null;
        _loginScope?.Dispose();
        _loginScope = _scopeFactory.CreateScope();

        var session = _services.GetRequiredService<SessionService>();
        session.Clear();

        var branding = _services.GetRequiredService<AppBrandingState>();
        branding.CompanyName = "Smart Building MS";
        branding.AppSubtitle = AppBrandingState.DefaultSubtitle;

        ApplyLoginWindowLayout();

        DataContext = null;
        ShellPanel.DataContext = null;
        ShellPanel.Visibility = Visibility.Collapsed;
        LoginBackdrop.Visibility = Visibility.Visible;
        LoginChrome.Visibility = Visibility.Visible;
        LoginPanel.Visibility = Visibility.Visible;
        LoginPanel.DataContext = ActivatorUtilities.CreateInstance<LoginViewModel>(
            _loginScope!.ServiceProvider, (Action)ShowShell);
    }

    private async void ShowShell()
    {
        _loginScope?.Dispose();
        _loginScope = null;

        try
        {
            var appConfig = _services.GetRequiredService<AppConfigurationService>();
            await appConfig.LoadAndApplyAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Connexion réussie, mais le profil société n'a pas pu être chargé.\n\n{ex.Message}",
                "Smart Building MS",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        ApplyShellWindowLayout();

        LoginBackdrop.Visibility = Visibility.Collapsed;
        LoginChrome.Visibility = Visibility.Collapsed;
        LoginPanel.Visibility = Visibility.Collapsed;
        ShellPanel.Visibility = Visibility.Visible;

        _shellScope?.Dispose();
        _shellScope = _scopeFactory.CreateScope();

        var shellVm = ActivatorUtilities.CreateInstance<MainShellViewModel>(
            _shellScope.ServiceProvider, (Action)ShowLogin);
        DataContext = shellVm;
        ShellPanel.DataContext = shellVm;

        try
        {
            await shellVm.NavigateToDefaultModuleAsync();
        }
        catch (Exception ex)
        {
            var dialogTitle = AppConfigurationService.Instance?.Current.CompanyName
                              ?? BuildingInfoDefaults.CompanyName;
            MessageBox.Show(
                $"Impossible de charger le tableau de bord.\n\n{DbSaveExceptionTranslator.ToDetailedMessage(ex)}",
                dialogTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Fenêtre login : 75% de la zone utile, centrée, sans barre titre.
    /// WindowState doit repasser à Normal avant Width/Height (sinon reste plein écran après déconnexion).
    /// </summary>
    private void ApplyLoginWindowLayout()
    {
        WindowState = WindowState.Normal;

        var workArea = SystemParameters.WorkArea;

        const double widthRatio = 0.75;
        const double heightRatio = 0.82;

        var loginWidth = workArea.Width * widthRatio;
        var loginHeight = workArea.Height * heightRatio;

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xEF, 0xED));

        Width = loginWidth;
        Height = loginHeight;
        Left = workArea.Left + (workArea.Width - loginWidth) / 2;
        Top = workArea.Top + (workArea.Height - loginHeight) / 2;

        LoginChrome.Width = loginWidth;
        LoginChrome.Height = loginHeight;
        LoginChrome.HorizontalAlignment = HorizontalAlignment.Center;
        LoginChrome.VerticalAlignment = VerticalAlignment.Center;
    }

    /// <summary>
    /// Après connexion : fenêtre plein écran avec barre système standard.
    /// Ne pas modifier AllowsTransparency (interdit après Show).
    /// </summary>
    private void ApplyShellWindowLayout()
    {
        WindowState = WindowState.Normal;
        Background = ThemeResourceHelper.GetBrush("SbmsPageBackgroundBrush") ?? Brushes.White;
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.CanResize;

        var screenW = SystemParameters.PrimaryScreenWidth;
        var screenH = SystemParameters.PrimaryScreenHeight;
        Left = 0;
        Top = 0;
        Width = screenW;
        Height = screenH;
        WindowState = WindowState.Maximized;

        LoginChrome.ClearValue(FrameworkElement.WidthProperty);
        LoginChrome.ClearValue(FrameworkElement.HeightProperty);
    }
}

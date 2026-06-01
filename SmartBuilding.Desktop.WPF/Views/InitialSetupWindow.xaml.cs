using System.Windows;
using SmartBuilding.Desktop.WPF.ViewModels;

namespace SmartBuilding.Desktop.WPF.Views;

public partial class InitialSetupWindow : Window
{
    private readonly InitialSetupViewModel _vm;
    private bool _forceExit;

    public InitialSetupWindow(InitialSetupViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
        _vm.CloseRequested += OnCloseRequested;
        _vm.RequestApplicationExit += OnRequestApplicationExit;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_forceExit || DialogResult == true)
            return;

        e.Cancel = true;
        if (!ConfirmQuitWithoutSetup())
            return;

        _forceExit = true;
        e.Cancel = false;
        System.Windows.Application.Current.Shutdown();
    }

    private void OnRequestApplicationExit()
    {
        if (!ConfirmQuitWithoutSetup())
            return;

        _forceExit = true;
        System.Windows.Application.Current.Shutdown();
    }

    private static bool ConfirmQuitWithoutSetup()
    {
        var quit = MessageBox.Show(
            "La configuration n'est pas terminée. Voulez-vous quitter SBMS ?\n\nAu prochain lancement, l'assistant s'affichera à nouveau.",
            "Configuration obligatoire",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        return quit == MessageBoxResult.Yes;
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

    private void MySqlPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        _vm.MySqlPassword = MySqlPasswordBox.Password;
    }
}

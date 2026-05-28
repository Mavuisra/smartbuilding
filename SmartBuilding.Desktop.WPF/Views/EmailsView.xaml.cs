namespace SmartBuilding.Desktop.WPF.Views;

public partial class EmailsView
{
    private readonly System.Windows.Threading.DispatcherTimer _autoSyncTimer;

    public EmailsView()
    {
        InitializeComponent();
        _autoSyncTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _autoSyncTimer.Tick += AutoSyncTimer_Tick;
    }

    private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (!_autoSyncTimer.IsEnabled)
            _autoSyncTimer.Start();
    }

    private void UserControl_Unloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_autoSyncTimer.IsEnabled)
            _autoSyncTimer.Stop();
    }

    private void AutoSyncTimer_Tick(object? sender, EventArgs e)
    {
        if (DataContext is not SmartBuilding.Desktop.WPF.ViewModels.EmailsViewModel vm)
            return;

        if (!vm.IsBusy && vm.SyncEmailsCommand.CanExecute(null))
            vm.SyncEmailsCommand.Execute(null);
    }
}

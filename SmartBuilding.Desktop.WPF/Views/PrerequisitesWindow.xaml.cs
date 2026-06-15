using System.Windows;
using Microsoft.Extensions.Configuration;
using SmartBuilding.Desktop.WPF.ViewModels;

namespace SmartBuilding.Desktop.WPF.Views;

public partial class PrerequisitesWindow : Window
{
    private readonly PrerequisitesViewModel _viewModel;

    public PrerequisitesWindow(IConfiguration configuration)
    {
        InitializeComponent();
        _viewModel = new PrerequisitesViewModel(configuration);
        _viewModel.Ready += OnPrerequisitesReady;
        DataContext = _viewModel;
    }

    private void OnPrerequisitesReady(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Ready -= OnPrerequisitesReady;
        base.OnClosed(e);
    }
}

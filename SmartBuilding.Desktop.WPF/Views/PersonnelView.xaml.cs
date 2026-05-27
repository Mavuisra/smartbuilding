using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SmartBuilding.Desktop.WPF.Helpers;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.ViewModels;

namespace SmartBuilding.Desktop.WPF.Views;

public partial class PersonnelView
{
    private PersonnelViewModel? _viewModel;

    public PersonnelView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachViewModel();
        _viewModel = e.NewValue as PersonnelViewModel;
        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplyTableColumnVisibility();
        SyncDetailPageVisibility();
    }

    private void DetachViewModel()
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateResponsiveLayout();
        if (EmployeesGrid is not null)
        {
            DataGridScrollHelper.SetIsEnabled(EmployeesGrid, true);
            DataGridScrollHelper.Refresh(EmployeesGrid);
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateResponsiveLayout();

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PersonnelViewModel.ShowColumnMatricule)
            or nameof(PersonnelViewModel.ShowColumnPhone)
            or nameof(PersonnelViewModel.ShowColumnHireDate)
            or nameof(PersonnelViewModel.ShowColumnPresence)
            or nameof(PersonnelViewModel.ShowColumnDepartment))
        {
            ApplyTableColumnVisibility();
        }

        if (e.PropertyName == nameof(PersonnelViewModel.IsEmployeeDetailPageOpen))
            SyncDetailPageVisibility();
    }

    private void SyncDetailPageVisibility()
    {
        if (EmployeeDetailPageGrid is null || _viewModel is null)
            return;

        EmployeeDetailPageGrid.Visibility = _viewModel.IsEmployeeDetailPageOpen
            ? Visibility.Visible
            : Visibility.Collapsed;
        Panel.SetZIndex(EmployeeDetailPageGrid, _viewModel.IsEmployeeDetailPageOpen ? 20 : 0);
    }

    private void UpdateResponsiveLayout()
    {
        if (DataContext is PersonnelViewModel vm)
            vm.UpdateViewWidth(ActualWidth);
        SyncEmployeesGridMinWidth();
    }

    private void SyncEmployeesGridMinWidth()
    {
        if (EmployeesGrid is null)
            return;

        var total = EmployeesGrid.Columns
            .Where(c => c.Visibility == Visibility.Visible)
            .Sum(c => c.Width.IsAbsolute ? c.Width.Value : c.MinWidth);

        EmployeesGrid.MinWidth = Math.Max(total + 24, 1320);
        DataGridScrollHelper.Refresh(EmployeesGrid);
    }

    private void ApplyTableColumnVisibility()
    {
        if (_viewModel is null || EmployeesGrid is null)
            return;

        SetColumnVisible(EmployeesGrid, "ID EMPLOYÉ", _viewModel.ShowColumnMatricule);
        SetColumnVisible(EmployeesGrid, "TÉLÉPHONE", _viewModel.ShowColumnPhone);
        SetColumnVisible(EmployeesGrid, "DATE EMBAUCHE", _viewModel.ShowColumnHireDate);
        SetColumnVisible(EmployeesGrid, "PRÉSENCE", _viewModel.ShowColumnPresence);
        SetColumnVisible(EmployeesGrid, "DÉPARTEMENT", _viewModel.ShowColumnDepartment);
        DataGridScrollHelper.SetIsEnabled(EmployeesGrid, true);
        DataGridScrollHelper.Refresh(EmployeesGrid);
        SyncEmployeesGridMinWidth();
    }

    private static void SetColumnVisible(DataGrid grid, string header, bool visible)
    {
        foreach (var column in grid.Columns)
        {
            if (column.Header?.ToString() != header)
                continue;
            column.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            break;
        }
    }

    private async void OnViewEmployeeClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (GetEmployeeFromSender(sender) is not { } employee)
            return;

        if (DataContext is PersonnelViewModel vm)
            await vm.OpenEmployeeDetailPageAsync(employee);
    }

    private async void OnEditEmployeeClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (GetEmployeeFromSender(sender) is not { } employee)
            return;

        if (DataContext is PersonnelViewModel vm)
            await vm.OpenEmployeeDetailPageAsync(employee);
    }

    private static PersonnelEmployeeItem? GetEmployeeFromSender(object sender)
    {
        if (sender is FrameworkElement { DataContext: PersonnelEmployeeItem employee })
            return employee;

        if (sender is FrameworkElement fe && fe.DataContext is PersonnelEmployeeItem item)
            return item;

        return null;
    }
}

using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SmartBuilding.Desktop.WPF.Helpers;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.ViewModels;

namespace SmartBuilding.Desktop.WPF.Views;

public partial class VisitsView
{
    public VisitsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => RefreshGridScroll();

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is VisitsViewModel oldVm)
            oldVm.Visits.CollectionChanged -= OnVisitsCollectionChanged;
        if (e.NewValue is VisitsViewModel newVm)
            newVm.Visits.CollectionChanged += OnVisitsCollectionChanged;
    }

    private void OnVisitsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Dispatcher.BeginInvoke(RefreshGridScroll);

    private void RefreshGridScroll()
    {
        if (VisitsGrid is not null)
            DataGridScrollHelper.Refresh(VisitsGrid);
    }

    private void VisitsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid || grid.SelectedItem is not VisitListItem visit)
            return;
        if (DataContext is VisitsViewModel vm)
            vm.SelectVisitCommand.Execute(visit);
    }
}

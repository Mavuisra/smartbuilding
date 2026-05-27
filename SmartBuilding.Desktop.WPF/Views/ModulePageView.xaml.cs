using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SmartBuilding.Desktop.WPF.Helpers;
using SmartBuilding.Desktop.WPF.ViewModels;

namespace SmartBuilding.Desktop.WPF.Views;

public partial class ModulePageView : UserControl
{
    private ModulePageViewModel? _viewModel;

    public ModulePageView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.ColumnHeaders.CollectionChanged -= OnColumnHeadersChanged;

        if (e.NewValue is ModulePageViewModel vm)
        {
            _viewModel = vm;
            vm.ColumnHeaders.CollectionChanged += OnColumnHeadersChanged;
            BuildColumns(vm.ColumnHeaders);
        }
    }

    private void OnColumnHeadersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_viewModel is not null)
            BuildColumns(_viewModel.ColumnHeaders);
    }

    private void BuildColumns(IReadOnlyList<string> headers)
    {
        ModuleGrid.Columns.Clear();
        var bindings = new[] { "Col0", "Col1", "Col2", "Col3", "Col4", "Col5" };

        for (var i = 0; i < headers.Count && i < bindings.Length; i++)
        {
            ModuleGrid.Columns.Add(new DataGridTextColumn
            {
                Header = headers[i],
                Binding = new Binding(bindings[i]),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 120
            });
        }

        DataGridScrollHelper.Refresh(ModuleGrid);
    }
}

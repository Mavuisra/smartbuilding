using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartBuilding.Domain.Entities.Technical;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class TechnicalViewModel
{
    private readonly TechnicalReportPdfService _technicalPdf = new();

    [ObservableProperty] private bool _isScheduleMaintenanceOpen;
    [ObservableProperty] private bool _isInterventionsHistoryOpen;
    [ObservableProperty] private bool _isEditEquipmentOpen;
    [ObservableProperty] private Guid _scheduleEquipmentId;
    [ObservableProperty] private DateTime _scheduleDate = DateTime.Today.AddDays(7);
    [ObservableProperty] private string _scheduleMaintenanceType = "Maintenance préventive";
    [ObservableProperty] private string _scheduleDescription = string.Empty;
    [ObservableProperty] private string _scheduleTechnician = string.Empty;
    [ObservableProperty] private string? _scheduleError;
    [ObservableProperty] private string? _workflowError;
    [ObservableProperty] private string _completeCostText = "0";
    [ObservableProperty] private TechnicalInterventionHistoryRow? _selectedHistoryRow;

    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string _editCategory = string.Empty;
    [ObservableProperty] private string _editLocation = string.Empty;
    [ObservableProperty] private string _editBrand = string.Empty;
    [ObservableProperty] private string? _editError;

    public ObservableCollection<TechnicalEquipmentItem> ScheduleEquipmentChoices { get; } = [];
    public ObservableCollection<TechnicalInterventionHistoryRow> InterventionsHistory { get; } = [];
    public ObservableCollection<string> MaintenanceTypes { get; } =
    [
        "Maintenance préventive",
        "Maintenance corrective",
        "Contrôle réglementaire",
        "Inspection sécurité",
        "Révision générale"
    ];

    [RelayCommand]
    private void OpenScheduleMaintenance()
    {
        ScheduleEquipmentChoices.Clear();
        foreach (var e in _allEquipment.OrderBy(x => x.Name))
            ScheduleEquipmentChoices.Add(e);

        ScheduleEquipmentId = SelectedEquipment?.Id ?? ScheduleEquipmentChoices.FirstOrDefault()?.Id ?? Guid.Empty;
        ScheduleDate = DateTime.Today.AddDays(7);
        ScheduleMaintenanceType = "Maintenance préventive";
        ScheduleDescription = SelectedEquipment is not null
            ? $"Intervention planifiée — {SelectedEquipment.Name}"
            : string.Empty;
        ScheduleTechnician = UserName;
        ScheduleError = null;
        IsScheduleMaintenanceOpen = true;
    }

    [RelayCommand]
    private void CloseScheduleMaintenance() => IsScheduleMaintenanceOpen = false;

    [RelayCommand]
    private async Task SaveScheduleMaintenanceAsync()
    {
        ScheduleError = null;
        if (ScheduleEquipmentId == Guid.Empty)
        {
            ScheduleError = "Sélectionnez un équipement.";
            return;
        }

        IsBusy = true;
        try
        {
            var error = await _technicalService.ScheduleMaintenanceAsync(
                ScheduleEquipmentId,
                ScheduleDate,
                ScheduleMaintenanceType,
                ScheduleDescription,
                ScheduleTechnician);

            if (!string.IsNullOrEmpty(error))
            {
                ScheduleError = error;
                return;
            }

            IsScheduleMaintenanceOpen = false;
            StatusMessage = "Maintenance planifiée.";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenInterventionsHistoryAsync()
    {
        WorkflowError = null;
        IsBusy = true;
        try
        {
            var rows = await _technicalService.GetInterventionsHistoryAsync();
            InterventionsHistory.Clear();
            foreach (var r in rows)
                InterventionsHistory.Add(r);
            IsInterventionsHistoryOpen = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CloseInterventionsHistory() => IsInterventionsHistoryOpen = false;

    [RelayCommand]
    private async Task CompleteSelectedInterventionAsync()
    {
        if (SelectedHistoryRow is null || !SelectedHistoryRow.IsPlanned)
        {
            WorkflowError = "Sélectionnez une intervention planifiée.";
            return;
        }

        if (!decimal.TryParse(CompleteCostText.Replace(" ", "").Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var cost))
        {
            WorkflowError = "Coût réel invalide.";
            return;
        }

        WorkflowError = null;
        IsBusy = true;
        try
        {
            var error = await _technicalService.CompleteMaintenanceAsync(
                SelectedHistoryRow.MaintenanceId,
                cost,
                UserName);

            if (!string.IsNullOrEmpty(error))
            {
                WorkflowError = error;
                return;
            }

            StatusMessage = "Intervention clôturée.";
            await OpenInterventionsHistoryAsync();
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectHistoryRow(TechnicalInterventionHistoryRow? row) => SelectedHistoryRow = row;

    [RelayCommand]
    private void OpenEditEquipment()
    {
        if (SelectedEquipment is null)
        {
            StatusMessage = "Sélectionnez un équipement dans la liste.";
            return;
        }

        EditName = SelectedEquipment.Name;
        EditCategory = SelectedEquipment.Category;
        EditLocation = SelectedEquipment.Location == "—" ? string.Empty : SelectedEquipment.Location;
        EditBrand = SelectedEquipment.Brand == "—" ? string.Empty : SelectedEquipment.Brand;
        EditError = null;
        IsEditEquipmentOpen = true;
    }

    [RelayCommand]
    private void CloseEditEquipment() => IsEditEquipmentOpen = false;

    [RelayCommand]
    private async Task SaveEditEquipmentAsync()
    {
        if (SelectedEquipment is null)
            return;

        EditError = null;
        IsBusy = true;
        try
        {
            var error = await _technicalService.UpdateEquipmentAsync(new Equipment
            {
                Id = SelectedEquipment.Id,
                Name = EditName,
                Category = EditCategory,
                Location = EditLocation,
                Brand = EditBrand
            });

            if (!string.IsNullOrEmpty(error))
            {
                EditError = error;
                return;
            }

            IsEditEquipmentOpen = false;
            StatusMessage = "Fiche équipement mise à jour.";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ExportCsv()
    {
        if (TechnicalExportService.ExportCsv(_allEquipment))
            StatusMessage = "Export Excel enregistré.";
        else
            StatusMessage = "Export annulé.";
    }

    [RelayCommand]
    private void ExportPdf()
    {
        var path = _technicalPdf.ExportEquipmentList(_allEquipment, "Inventaire technique — équipements");
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        StatusMessage = $"PDF : {path}";
    }

    [RelayCommand]
    private void PrintEquipment()
    {
        if (TechnicalExportService.PrintEquipmentList(_allEquipment, "SBMS — Liste des équipements"))
            StatusMessage = "Impression envoyée.";
        else
            StatusMessage = "Impression annulée.";
    }

    [RelayCommand]
    private void OpenEquipmentDetail(TechnicalEquipmentItem? item)
    {
        if (item is null)
            return;
        SelectedEquipment = item;
        SelectedDetailTab = 0;
        IsDetailPanelOpen = true;
    }
}

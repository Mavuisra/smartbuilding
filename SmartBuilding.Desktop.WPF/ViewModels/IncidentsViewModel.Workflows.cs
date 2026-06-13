using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class IncidentsViewModel
{
    private readonly IncidentsReportPdfService _incidentsPdf = new();

    [ObservableProperty] private bool _isInterventionFormOpen;
    [ObservableProperty] private bool _isSecurityAlertFormOpen;
    [ObservableProperty] private bool _isHistoryOpen;
    [ObservableProperty] private bool _isResolveFormOpen;
    [ObservableProperty] private bool _isEditIncidentOpen;

    [ObservableProperty] private Guid _interventionIncidentId;
    [ObservableProperty] private string _interventionType = "Intervention corrective";
    [ObservableProperty] private Guid _interventionTechnicianId;
    [ObservableProperty] private DateTime _interventionScheduledAt = DateTime.Today.AddHours(2);
    [ObservableProperty] private string _interventionNotes = string.Empty;
    [ObservableProperty] private string? _interventionError;
    [ObservableProperty] private string _quickTechnicianName = string.Empty;

    [ObservableProperty] private string _alertTitle = string.Empty;
    [ObservableProperty] private string _alertMessage = string.Empty;
    [ObservableProperty] private string _alertLocation = "Hall principal";
    [ObservableProperty] private string _alertSeverity = "Élevé";
    [ObservableProperty] private string? _alertError;
    [ObservableProperty] private Guid _alertEquipmentId;
    [ObservableProperty] private string _quickEquipmentName = string.Empty;
    [ObservableProperty] private string _quickEquipmentCategory = "Sécurité incendie";
    [ObservableProperty] private string _quickEquipmentLocation = "Zone sécurité";

    [ObservableProperty] private string _resolveNotes = string.Empty;
    [ObservableProperty] private string _resolveCostText = "0";
    [ObservableProperty] private string? _resolveError;

    [ObservableProperty] private string _editTitle = string.Empty;
    [ObservableProperty] private string _editDescription = string.Empty;
    [ObservableProperty] private string _editSeverity = "Moyen";
    [ObservableProperty] private string? _editError;

    [ObservableProperty] private string? _workflowError;

    public ObservableCollection<IncidentListItem> IncidentPickerItems { get; } = [];
    public ObservableCollection<IncidentListItem> HistoryIncidents { get; } = [];

    public ObservableCollection<string> InterventionTypes { get; } =
    [
        "Intervention corrective",
        "Inspection sécurité",
        "Réparation urgente",
        "Remplacement pièce",
        "Test équipement",
        "Nettoyage / remise en état"
    ];

    [RelayCommand]
    private void OpenInterventionForm()
    {
        IncidentPickerItems.Clear();
        foreach (var i in _allIncidents
                     .Where(x => x.StatusLabel is not "Résolu")
                     .OrderByDescending(x => x.DateDisplay))
            IncidentPickerItems.Add(i);

        InterventionIncidentId = SelectedIncident?.Id ?? IncidentPickerItems.FirstOrDefault()?.Id ?? Guid.Empty;
        InterventionType = "Intervention corrective";
        InterventionTechnicianId = TechnicianOptions.FirstOrDefault()?.Id ?? Guid.Empty;
        InterventionScheduledAt = DateTime.Today.AddHours(2);
        InterventionNotes = SelectedIncident is not null
            ? $"Intervention liée à {SelectedIncident.Code}"
            : string.Empty;
        InterventionError = null;
        IsInterventionFormOpen = true;
    }

    [RelayCommand]
    private void CloseInterventionForm() => IsInterventionFormOpen = false;

    [RelayCommand]
    private async Task SaveInterventionAsync()
    {
        InterventionError = null;
        if (InterventionIncidentId == Guid.Empty)
        {
            InterventionError = "Sélectionnez un incident.";
            return;
        }

        IsBusy = true;
        try
        {
            var error = await _incidentsService.CreateInterventionAsync(
                InterventionIncidentId,
                InterventionType,
                TechnicianOptions.FirstOrDefault(t => t.Id == InterventionTechnicianId)?.FullName ?? string.Empty,
                InterventionScheduledAt,
                InterventionNotes);

            if (!string.IsNullOrEmpty(error))
            {
                InterventionError = error;
                return;
            }

            IsInterventionFormOpen = false;
            StatusMessage = "Intervention enregistrée.";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenSecurityAlertForm()
    {
        AlertTitle = "Alerte sécurité — vérification requise";
        AlertMessage = string.Empty;
        AlertLocation = "Hall principal";
        AlertSeverity = "Élevé";
        AlertEquipmentId = EquipmentOptions.FirstOrDefault()?.Id ?? Guid.Empty;
        AlertError = null;
        IsSecurityAlertFormOpen = true;
    }

    [RelayCommand]
    private void CloseSecurityAlertForm() => IsSecurityAlertFormOpen = false;

    [RelayCommand]
    private async Task SaveSecurityAlertAsync()
    {
        AlertError = null;
        IsBusy = true;
        try
        {
            var error = await _incidentsService.CreateSecurityAlertAsync(
                AlertTitle,
                BuildSecurityAlertMessage(),
                ResolveAlertLocation(),
                AlertSeverity,
                UserName);

            if (!string.IsNullOrEmpty(error))
            {
                AlertError = error;
                return;
            }

            IsSecurityAlertFormOpen = false;
            StatusMessage = "Alerte sécurité enregistrée.";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenHistoryAsync()
    {
        WorkflowError = null;
        IsBusy = true;
        try
        {
            var rows = await _incidentsService.GetAllIncidentsHistoryAsync();
            HistoryIncidents.Clear();
            foreach (var r in rows)
                HistoryIncidents.Add(r);
            IsHistoryOpen = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CloseHistory() => IsHistoryOpen = false;

    [RelayCommand]
    private void OpenResolveForm()
    {
        if (SelectedIncident is null)
        {
            StatusMessage = "Sélectionnez un incident dans la liste.";
            return;
        }

        ResolveNotes = $"Incident {SelectedIncident.Code} résolu.";
        ResolveCostText = "0";
        ResolveError = null;
        IsResolveFormOpen = true;
    }

    [RelayCommand]
    private void CloseResolveForm() => IsResolveFormOpen = false;

    [RelayCommand]
    private async Task SaveResolveAsync()
    {
        if (SelectedIncident is null)
            return;

        ResolveError = null;
        if (!decimal.TryParse(ResolveCostText.Replace(" ", "").Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var cost))
        {
            ResolveError = "Coût de réparation invalide.";
            return;
        }

        IsBusy = true;
        try
        {
            var error = await _incidentsService.ResolveIncidentAsync(
                SelectedIncident.Id,
                ResolveNotes,
                cost);

            if (!string.IsNullOrEmpty(error))
            {
                ResolveError = error;
                return;
            }

            IsResolveFormOpen = false;
            StatusMessage = "Incident résolu.";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenEditIncident()
    {
        if (SelectedIncident is null)
        {
            StatusMessage = "Sélectionnez un incident.";
            return;
        }

        EditTitle = SelectedIncident.Title;
        EditDescription = SelectedIncident.Description;
        EditSeverity = SelectedIncident.SeverityLabel;
        EditError = null;
        IsEditIncidentOpen = true;
    }

    [RelayCommand]
    private void CloseEditIncident() => IsEditIncidentOpen = false;

    [RelayCommand]
    private async Task SaveEditIncidentAsync()
    {
        if (SelectedIncident is null)
            return;

        EditError = null;
        IsBusy = true;
        try
        {
            var error = await _incidentsService.UpdateIncidentAsync(
                SelectedIncident.Id,
                EditTitle,
                EditDescription,
                EditSeverity);

            if (!string.IsNullOrEmpty(error))
            {
                EditError = error;
                return;
            }

            IsEditIncidentOpen = false;
            StatusMessage = "Incident mis à jour.";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenIncidentDetail(IncidentListItem? item)
    {
        if (item is null)
            return;
        SelectedIncident = item;
        SelectedDetailTab = 0;
        IsDetailPanelOpen = true;
    }

    [RelayCommand]
    private void ExportCsv()
    {
        if (IncidentsExportService.ExportCsv(_allIncidents))
            StatusMessage = "Export PDF enregistré.";
        else
            StatusMessage = "Export annulé.";
    }

    [RelayCommand]
    private void ExportPdf()
    {
        var path = _incidentsPdf.ExportIncidentsList(_allIncidents, "Rapport incidents & sécurité");
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        StatusMessage = $"PDF : {path}";
    }

    [RelayCommand]
    private void PrintIncidents()
    {
        if (IncidentsExportService.PrintIncidentsList(_allIncidents, "SBMS — Incidents & sécurité"))
            StatusMessage = "Impression envoyée.";
        else
            StatusMessage = "Impression annulée.";
    }

    [RelayCommand]
    private async Task QuickAddTechnicianAsync()
    {
        if (string.IsNullOrWhiteSpace(QuickTechnicianName))
        {
            InterventionError = "Entrez le nom du technicien.";
            return;
        }

        var tech = await _incidentsService.QuickCreateTechnicianAsync(QuickTechnicianName, "Technique");
        TechnicianOptions.Add(tech);
        InterventionTechnicianId = tech.Id;
        QuickTechnicianName = string.Empty;
        InterventionError = null;
    }

    [RelayCommand]
    private async Task QuickAddEquipmentAsync()
    {
        var eq = await _incidentsService.QuickCreateEquipmentAsync(
            QuickEquipmentName,
            QuickEquipmentCategory,
            QuickEquipmentLocation);
        EquipmentOptions.Add(eq);
        AlertEquipmentId = eq.Id;
        if (!IncidentLocations.Contains(eq.Location))
            IncidentLocations.Add(eq.Location);
        AlertLocation = eq.Location;
        QuickEquipmentName = string.Empty;
        AlertError = null;
    }

    private string ResolveAlertLocation()
    {
        var equipmentLocation = EquipmentOptions.FirstOrDefault(e => e.Id == AlertEquipmentId)?.Location;
        return string.IsNullOrWhiteSpace(equipmentLocation) ? AlertLocation : equipmentLocation;
    }

    private string BuildSecurityAlertMessage()
    {
        var equipment = EquipmentOptions.FirstOrDefault(e => e.Id == AlertEquipmentId);
        if (equipment is null)
            return AlertMessage;

        var prefix = $"Équipement concerné : {equipment.Name} ({equipment.Category})";
        return string.IsNullOrWhiteSpace(AlertMessage)
            ? prefix
            : $"{prefix}. {AlertMessage.Trim()}";
    }
}

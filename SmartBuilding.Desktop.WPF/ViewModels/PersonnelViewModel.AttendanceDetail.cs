using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartBuilding.Domain.Entities.Personnel;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class PersonnelViewModel
{
    [ObservableProperty] private string _detailAttendancePeriod = PersonnelAttendancePeriods.Month;
    [ObservableProperty] private string _detailAttendanceHistoryCaption = "Historique — ce mois";
    [ObservableProperty] private bool _isAttendanceEditOpen;
    [ObservableProperty] private string _editAttendanceDateDisplay = string.Empty;
    [ObservableProperty] private string _editCheckInText = string.Empty;
    [ObservableProperty] private string _editCheckOutText = string.Empty;
    [ObservableProperty] private string _editAttendanceStatus = "Automatique";
    [ObservableProperty] private string _editAttendanceNotes = string.Empty;
    [ObservableProperty] private string? _attendanceEditError;

    private Guid _editingAttendanceId;

    public ObservableCollection<string> DetailAttendancePeriodChoices { get; } =
    [
        PersonnelAttendancePeriods.Day,
        PersonnelAttendancePeriods.Week,
        PersonnelAttendancePeriods.Month,
        PersonnelAttendancePeriods.Year
    ];

    public ObservableCollection<string> AttendanceEditStatusChoices { get; } =
    [
        "Automatique",
        "Absent",
        "En congé"
    ];

    partial void OnDetailAttendancePeriodChanged(string value) => _ = LoadDetailAttendancesAsync();

    private async Task LoadDetailAttendancesAsync()
    {
        if (EditingEmployeeId == Guid.Empty)
            return;

        var (_, _, suffix) = PersonnelService.ResolveAttendancePeriod(DetailAttendancePeriod);
        DetailAttendanceHistoryCaption = $"Historique — {suffix}";

        var rows = await _personnelService.GetEmployeeAttendancesAsync(EditingEmployeeId, DetailAttendancePeriod);
        DetailAttendances.Clear();
        foreach (var r in rows)
            DetailAttendances.Add(r);
    }

    [RelayCommand]
    private void OpenEditAttendance(PersonnelAttendanceRow? row)
    {
        if (!CanManagePersonnel || row is null)
            return;

        _editingAttendanceId = row.Id;
        EditAttendanceDateDisplay = row.DateDisplay;
        EditCheckInText = row.CheckInDisplay == "—" ? string.Empty : row.CheckInDisplay;
        EditCheckOutText = row.CheckOutDisplay == "—" ? string.Empty : row.CheckOutDisplay;
        EditAttendanceNotes = row.Notes ?? string.Empty;
        EditAttendanceStatus = row.StatusLabel switch
        {
            "Absent" => "Absent",
            "En congé" => "En congé",
            _ => "Automatique"
        };
        AttendanceEditError = null;
        IsAttendanceEditOpen = true;
    }

    [RelayCommand]
    private void CloseAttendanceEdit()
    {
        IsAttendanceEditOpen = false;
        _editingAttendanceId = Guid.Empty;
        AttendanceEditError = null;
    }

    [RelayCommand]
    private async Task SaveAttendanceEditAsync()
    {
        if (!CanManagePersonnel || _editingAttendanceId == Guid.Empty)
            return;

        if (!SbmsDialogService.Confirm("Modifier le pointage", "Confirmer la modification de ce pointage ?"))
            return;

        AttendanceEditError = null;
        IsBusy = true;
        try
        {
            var statusOverride = MapAttendanceStatusOverride(EditAttendanceStatus);
            var error = await _personnelService.UpdateAttendanceAsync(
                _editingAttendanceId,
                EditCheckInText,
                EditCheckOutText,
                statusOverride,
                EditAttendanceNotes);

            if (!string.IsNullOrEmpty(error))
            {
                AttendanceEditError = error;
                return;
            }

            CloseAttendanceEdit();
            if (EditingEmployeeId != Guid.Empty)
                await LoadEmployeeDetailAsync(EditingEmployeeId);
            else
                await LoadDetailAttendancesAsync();

            StatusMessage = "Pointage mis à jour.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string? MapAttendanceStatusOverride(string selected) =>
        selected switch
        {
            "Absent" => RhConstants.PresenceStatus.Absent,
            "En congé" => RhConstants.PresenceStatus.Leave,
            _ => null
        };
}

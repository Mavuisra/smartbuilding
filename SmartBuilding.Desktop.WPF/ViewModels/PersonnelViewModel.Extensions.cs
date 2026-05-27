using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SmartBuilding.Domain.Entities.Personnel;
using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class PersonnelViewModel
{
    public const string AllAttendanceEmployees = "Tous les employés";
    public const string AllAttendanceStatuses = "Tous statuts";

    [ObservableProperty] private bool _isAttendanceDashboardOpen;
    [ObservableProperty] private DateTime? _attendanceFilterFrom = DateTime.Today.AddDays(-30);
    [ObservableProperty] private DateTime? _attendanceFilterTo = DateTime.Today;
    [ObservableProperty] private string _attendanceFilterEmployee = AllAttendanceEmployees;
    [ObservableProperty] private string _attendanceFilterStatus = AllAttendanceStatuses;
    [ObservableProperty] private string _attendanceStatsSummary = string.Empty;
    [ObservableProperty] private string? _detailProfilePhotoPath;
    [ObservableProperty] private bool _hasDetailProfilePhoto;

    public ObservableCollection<PersonnelAttendanceHistoryRow> AttendanceHistoryRows { get; } = [];
    public ObservableCollection<string> AttendanceEmployeeFilters { get; } = [AllAttendanceEmployees];
    public ObservableCollection<string> AttendanceStatusFilters { get; } =
    [
        AllAttendanceStatuses,
        RhConstants.PresenceStatus.Present,
        RhConstants.PresenceStatus.Late,
        RhConstants.PresenceStatus.Absent,
        RhConstants.PresenceStatus.Leave,
        RhConstants.PresenceStatus.EarlyLeave
    ];

    [RelayCommand]
    private async Task OpenAttendanceDashboardAsync()
    {
        IsAttendanceDashboardOpen = true;
        RefreshAttendanceEmployeeFilters();
        await LoadAttendanceDashboardAsync();
    }

    [RelayCommand]
    private void CloseAttendanceDashboard() => IsAttendanceDashboardOpen = false;

    [RelayCommand]
    private async Task LoadAttendanceDashboardAsync()
    {
        IsBusy = true;
        try
        {
            Guid? employeeId = null;
            if (AttendanceFilterEmployee != AllAttendanceEmployees)
            {
                var emp = _allEmployees.FirstOrDefault(e =>
                    $"{e.FullName} ({e.Matricule})" == AttendanceFilterEmployee);
                employeeId = emp?.Id;
            }

            var status = AttendanceFilterStatus == AllAttendanceStatuses ? null : AttendanceFilterStatus;
            var rows = await _personnelService.GetAttendanceHistoryDetailedAsync(
                employeeId,
                AttendanceFilterFrom,
                AttendanceFilterTo,
                status);

            AttendanceHistoryRows.Clear();
            foreach (var r in rows)
                AttendanceHistoryRows.Add(r);

            var stats = await _personnelService.GetAttendanceStatsAsync(AttendanceFilterFrom, AttendanceFilterTo);
            AttendanceStatsSummary =
                $"{stats.TotalRecords} pointages · {stats.PresentCount} présents · {stats.LateCount} retards · " +
                $"{stats.AbsentCount} absents · {stats.TotalWorkedHours:N1} h · {stats.TotalOvertimeHours:N1} h sup.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshAttendanceEmployeeFilters()
    {
        AttendanceEmployeeFilters.Clear();
        AttendanceEmployeeFilters.Add(AllAttendanceEmployees);
        foreach (var e in _allEmployees.OrderBy(x => x.FullName))
            AttendanceEmployeeFilters.Add($"{e.FullName} ({e.Matricule})");
    }

    [RelayCommand]
    private async Task ExportPayrollExcelAsync()
    {
        IsBusy = true;
        try
        {
            var path = await _personnelService.ExportPayrollExcelAsync();
            StatusMessage = "Export Excel paies terminé.";
            if (MessageBox.Show(
                    "Fichier Excel généré. Ouvrir maintenant ?",
                    "Export paies",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) == MessageBoxResult.Yes)
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportAttendanceExcelAsync()
    {
        IsBusy = true;
        try
        {
            Guid? employeeId = null;
            if (AttendanceFilterEmployee != AllAttendanceEmployees)
            {
                var emp = _allEmployees.FirstOrDefault(e =>
                    $"{e.FullName} ({e.Matricule})" == AttendanceFilterEmployee);
                employeeId = emp?.Id;
            }

            var status = AttendanceFilterStatus == AllAttendanceStatuses ? null : AttendanceFilterStatus;
            var path = await _personnelService.ExportAttendanceExcelAsync(
                AttendanceFilterFrom,
                AttendanceFilterTo,
                employeeId,
                status);

            StatusMessage = "Export Excel pointages terminé.";
            if (MessageBox.Show(
                    "Fichier Excel généré. Ouvrir maintenant ?",
                    "Export pointages",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) == MessageBoxResult.Yes)
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AttachProfilePhotoAsync()
    {
        if (!CanManagePersonnel || EditingEmployeeId == Guid.Empty)
            return;

        var dialog = new OpenFileDialog
        {
            Filter = "Images|*.jpg;*.jpeg;*.png;*.webp",
            Title = "Photo de profil"
        };
        if (dialog.ShowDialog() != true)
            return;

        var employee = await _personnelService.GetEmployeeAsync(EditingEmployeeId);
        if (employee is null)
            return;

        var destDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SBMS", "Photos");
        Directory.CreateDirectory(destDir);
        var ext = Path.GetExtension(dialog.FileName);
        var dest = Path.Combine(destDir, $"{employee.Matricule}{ext}");
        File.Copy(dialog.FileName, dest, overwrite: true);

        employee.ProfilePhotoPath = dest;
        employee.MarkUpdated();
        await _personnelService.UpdateEmployeeAsync(employee);

        DetailProfilePhotoPath = dest;
        HasDetailProfilePhoto = true;
        StatusMessage = "Photo de profil enregistrée.";
        await LoadAsync();
        if (SelectedEmployee?.Id == EditingEmployeeId)
            SelectedEmployee = _allEmployees.FirstOrDefault(e => e.Id == EditingEmployeeId);
    }

    [RelayCommand]
    private async Task RemoveProfilePhotoAsync()
    {
        if (!CanManagePersonnel || EditingEmployeeId == Guid.Empty)
            return;

        var employee = await _personnelService.GetEmployeeAsync(EditingEmployeeId);
        if (employee is null)
            return;

        employee.ProfilePhotoPath = null;
        employee.MarkUpdated();
        await _personnelService.UpdateEmployeeAsync(employee);
        DetailProfilePhotoPath = null;
        HasDetailProfilePhoto = false;
        StatusMessage = "Photo supprimée.";
        await LoadAsync();
    }

    private void ApplyProfilePhotoFromDetail(string? path)
    {
        DetailProfilePhotoPath = path;
        HasDetailProfilePhoto = !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }
}

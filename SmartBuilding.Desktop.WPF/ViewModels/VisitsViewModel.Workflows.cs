using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartBuilding.Domain.Entities.Visitors;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class VisitsViewModel
{
    [ObservableProperty] private bool _isAppointmentFormOpen;
    [ObservableProperty] private bool _isBadgeScannerOpen;
    [ObservableProperty] private bool _isHistoryPanelOpen;
    [ObservableProperty] private string _formApptVisitorName = string.Empty;
    [ObservableProperty] private string _formApptHost = string.Empty;
    [ObservableProperty] private string _formApptPurpose = string.Empty;
    [ObservableProperty] private string _formApptRoom = "Réception";
    [ObservableProperty] private DateTime _formApptDate = DateTime.Today;
    [ObservableProperty] private string _formApptTime = "14:00";
    [ObservableProperty] private string _formApptDuration = "60";
    [ObservableProperty] private string _formBadgeQuery = string.Empty;
    [ObservableProperty] private string? _badgeScanResult;

    public ObservableCollection<VisitListItem> HistoryVisits { get; } = [];

    [RelayCommand]
    private void OpenAppointmentForm()
    {
        FormApptVisitorName = string.Empty;
        FormApptHost = string.Empty;
        FormApptPurpose = string.Empty;
        FormApptRoom = "Réception";
        FormApptDate = DateTime.Today;
        FormApptTime = DateTime.Now.AddHours(1).ToString("HH:mm");
        FormApptDuration = "60";
        FormError = null;
        IsAppointmentFormOpen = true;
    }

    [RelayCommand] private void CloseAppointmentForm() => IsAppointmentFormOpen = false;

    [RelayCommand]
    private async Task SaveAppointmentAsync()
    {
        FormError = null;
        if (!TimeSpan.TryParse(FormApptTime, out var time))
        {
            FormError = "Horaire invalide (format HH:mm).";
            return;
        }

        if (!int.TryParse(FormApptDuration, out var durationMinutes) || durationMinutes < 5)
        {
            FormError = "Durée invalide (minutes, min. 5).";
            return;
        }

        var scheduled = FormApptDate.Date + time;
        IsBusy = true;
        try
        {
            var error = await _visitsService.CreateAppointmentAsync(new VisitorAppointment
            {
                VisitorName = FormApptVisitorName,
                HostName = FormApptHost,
                Purpose = FormApptPurpose,
                Room = FormApptRoom,
                ScheduledAt = scheduled,
                DurationMinutes = durationMinutes,
                Status = "Confirmé"
            });
            if (!string.IsNullOrEmpty(error)) { FormError = error; return; }

            IsAppointmentFormOpen = false;
            StatusMessage = "Rendez-vous enregistré.";
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void OpenBadgeScanner()
    {
        FormBadgeQuery = SelectedVisit?.BadgeNumber ?? string.Empty;
        BadgeScanResult = null;
        FormError = null;
        IsBadgeScannerOpen = true;
    }

    [RelayCommand] private void CloseBadgeScanner() => IsBadgeScannerOpen = false;

    [RelayCommand]
    private async Task ScanBadgeAsync()
    {
        FormError = null;
        BadgeScanResult = null;
        if (string.IsNullOrWhiteSpace(FormBadgeQuery))
        {
            FormError = "Saisissez ou scannez un numéro de badge ou code visite.";
            return;
        }

        IsBusy = true;
        try
        {
            var visitor = await _visitsService.FindByBadgeOrCodeAsync(FormBadgeQuery);
            if (visitor is null)
            {
                FormError = "Visiteur introuvable.";
                return;
            }

            var item = _allVisits.FirstOrDefault(v => v.Id == visitor.Id);
            if (item is not null)
            {
                SelectedVisit = item;
                IsDetailPanelOpen = true;
            }

            BadgeScanResult = $"{visitor.FullName} — {visitor.AccessStatus} — {visitor.Zone}";
            StatusMessage = $"Badge reconnu : {visitor.FullName}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CheckInBadgeAsync()
    {
        FormError = null;
        IsBusy = true;
        try
        {
            var error = await _visitsService.CheckInFromBadgeAsync(FormBadgeQuery);
            if (!string.IsNullOrEmpty(error)) { FormError = error; return; }
            IsBadgeScannerOpen = false;
            StatusMessage = "Entrée enregistrée.";
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task OpenHistoryAsync()
    {
        IsBusy = true;
        try
        {
            var history = await _visitsService.GetAccessHistoryAsync();
            HistoryVisits.Clear();
            foreach (var v in history) HistoryVisits.Add(v);
            IsHistoryPanelOpen = true;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand] private void CloseHistory() => IsHistoryPanelOpen = false;

    [RelayCommand]
    private void ExportPdf()
    {
        if (_allVisits.Count == 0)
        {
            MessageBox.Show("Aucune visite à exporter.", "SBMS — Réception", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (VisitsExportService.PrintPdfReport(_allVisits, "Rapport Visites & Accès"))
            StatusMessage = "Export PDF envoyé à l'imprimante (ou « Microsoft Print to PDF »).";
    }

    [RelayCommand]
    private void ExportExcel()
    {
        try
        {
            var path = VisitsExportService.ExportCsv(_allVisits);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            StatusMessage = $"Export PDF : {path}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export impossible.\n{ex.Message}", "SBMS — Réception", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void SelectVisit(VisitListItem? visit)
    {
        if (visit is null) return;
        SelectedVisit = visit;
    }

    [RelayCommand]
    private async Task GrantAccessAsync()
    {
        if (SelectedVisit is null) return;
        IsBusy = true;
        try
        {
            var error = await _visitsService.SetAccessStatusAsync(SelectedVisit.Id, "Actif");
            if (!string.IsNullOrEmpty(error)) { StatusMessage = error; return; }
            StatusMessage = "Accès accordé.";
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DenyAccessAsync()
    {
        if (SelectedVisit is null) return;
        IsBusy = true;
        try
        {
            var error = await _visitsService.SetAccessStatusAsync(SelectedVisit.Id, "Refusé");
            if (!string.IsNullOrEmpty(error)) { StatusMessage = error; return; }
            StatusMessage = "Accès refusé.";
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void ShowAlerts()
    {
        FilterStatus = "Refusé";
        CurrentPage = 1;
        ApplyFilters();
        if (FilteredTotal == 0)
        {
            FilterStatus = AllStatuses;
            ApplyFilters();
            MessageBox.Show(
                "Aucune alerte critique.\nRéception sous contrôle.",
                "SBMS — Alertes accès",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}

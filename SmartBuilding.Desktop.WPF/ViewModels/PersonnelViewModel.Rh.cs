using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SmartBuilding.Domain.Entities.Personnel;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;
using SmartBuilding.Shared.Constants;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class PersonnelViewModel
{
    [ObservableProperty] private bool _canManagePersonnel;
    [ObservableProperty] private string _detailSeniorityDisplay = "—";
    [ObservableProperty] private string _detailPresenceStatsLine = string.Empty;
    [ObservableProperty] private string _detailContractPdfPath = string.Empty;

    [ObservableProperty] private bool _isSuspendDialogOpen;
    [ObservableProperty] private string _suspendReason = string.Empty;
    [ObservableProperty] private DateTime _suspendUntil = DateTime.Today.AddDays(7);
    [ObservableProperty] private string? _suspendError;

    [ObservableProperty] private bool _isDismissDialogOpen;
    [ObservableProperty] private string _dismissReason = string.Empty;
    [ObservableProperty] private string? _dismissError;

    [ObservableProperty] private bool _isPayrollDialogOpen;
    [ObservableProperty] private int _payrollYear = DateTime.Today.Year;
    [ObservableProperty] private int _payrollMonth = DateTime.Today.Month;
    [ObservableProperty] private string _payrollBonusesText = "0";
    [ObservableProperty] private string _payrollPenaltiesText = "0";
    [ObservableProperty] private string _payrollAdvancesText = "0";
    [ObservableProperty] private string _payrollDeductionsText = "0";
    [ObservableProperty] private string _payrollNetPreview = "0";
    [ObservableProperty] private string? _payrollError;

    [ObservableProperty] private bool _isDisciplinaryDialogOpen;
    [ObservableProperty] private string _disciplinaryCategory = RhConstants.DisciplinaryCategory.Remark;
    [ObservableProperty] private string _disciplinaryTitle = string.Empty;
    [ObservableProperty] private string _disciplinaryDescription = string.Empty;
    [ObservableProperty] private string? _disciplinaryError;

    [ObservableProperty] private string _formRhStatus = RhConstants.EmployeeStatus.Active;

    public ObservableCollection<PersonnelDisciplinaryRow> DetailDisciplinaryNotes { get; } = [];
    public ObservableCollection<string> RhStatusOptions { get; } =
    [
        RhConstants.EmployeeStatus.Active,
        RhConstants.EmployeeStatus.Suspended,
        RhConstants.EmployeeStatus.OnLeave,
        RhConstants.EmployeeStatus.Pending,
        RhConstants.EmployeeStatus.Dismissed
    ];
    public ObservableCollection<string> DisciplinaryCategories { get; } =
    [
        RhConstants.DisciplinaryCategory.Warning,
        RhConstants.DisciplinaryCategory.Remark,
        RhConstants.DisciplinaryCategory.Incident,
        RhConstants.DisciplinaryCategory.Behavior,
        RhConstants.DisciplinaryCategory.Performance
    ];

    [RelayCommand]
    private async Task DeleteEmployeeAsync(PersonnelEmployeeItem? employee)
    {
        if (!CanManagePersonnel || employee is null)
            return;
        if (MessageBox.Show(
                $"Supprimer l'employé {employee.FullName} ?",
                "Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        IsBusy = true;
        try
        {
            var error = await _personnelService.DeleteEmployeeAsync(employee.Id);
            if (!string.IsNullOrEmpty(error))
            {
                ErrorMessage = error;
                return;
            }
            IsEmployeeDetailPageOpen = false;
            StatusMessage = "Employé archivé.";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenSuspendDialog()
    {
        if (!CanManagePersonnel || EditingEmployeeId == Guid.Empty)
            return;
        SuspendReason = string.Empty;
        SuspendUntil = DateTime.Today.AddDays(7);
        SuspendError = null;
        IsSuspendDialogOpen = true;
    }

    [RelayCommand]
    private void CloseSuspendDialog() => IsSuspendDialogOpen = false;

    [RelayCommand]
    private async Task ConfirmSuspendAsync()
    {
        SuspendError = null;
        IsBusy = true;
        try
        {
            var error = await _personnelService.SuspendEmployeeAsync(EditingEmployeeId, SuspendReason, SuspendUntil);
            if (!string.IsNullOrEmpty(error))
            {
                SuspendError = error;
                return;
            }
            IsSuspendDialogOpen = false;
            StatusMessage = "Employé suspendu.";
            await LoadEmployeeDetailAsync(EditingEmployeeId);
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenDismissDialog()
    {
        if (!CanManagePersonnel || EditingEmployeeId == Guid.Empty)
            return;
        DismissReason = string.Empty;
        DismissError = null;
        IsDismissDialogOpen = true;
    }

    [RelayCommand]
    private void CloseDismissDialog() => IsDismissDialogOpen = false;

    [RelayCommand]
    private async Task ConfirmDismissAsync()
    {
        DismissError = null;
        IsBusy = true;
        try
        {
            var error = await _personnelService.DismissEmployeeAsync(EditingEmployeeId, DismissReason);
            if (!string.IsNullOrEmpty(error))
            {
                DismissError = error;
                return;
            }
            IsDismissDialogOpen = false;
            StatusMessage = "Employé renvoyé.";
            await LoadEmployeeDetailAsync(EditingEmployeeId);
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenPayrollDialogAsync()
    {
        if (!CanManagePersonnel || EditingEmployeeId == Guid.Empty)
            return;
        PayrollYear = DateTime.Today.Year;
        PayrollMonth = DateTime.Today.Month;
        PayrollBonusesText = "0";
        PayrollPenaltiesText = "0";
        PayrollAdvancesText = "0";
        PayrollDeductionsText = "0";
        PayrollError = null;
        IsPayrollDialogOpen = true;
        await RefreshPayrollPreviewAsync();
    }

    [RelayCommand]
    private void ClosePayrollDialog() => IsPayrollDialogOpen = false;

    [RelayCommand]
    private async Task RefreshPayrollPreviewAsync()
    {
        if (EditingEmployeeId == Guid.Empty)
            return;
        try
        {
            var calc = await _personnelService.CalculatePayrollAsync(
                EditingEmployeeId,
                PayrollYear,
                PayrollMonth,
                ParseDecimal(PayrollBonusesText),
                ParseDecimal(PayrollPenaltiesText),
                ParseDecimal(PayrollAdvancesText),
                ParseDecimal(PayrollDeductionsText));
            PayrollNetPreview = MoneyFormatter.Format(calc.NetAmount);

            var treasuryError = await _personnelService.ValidatePayrollAgainstTreasuryAsync(calc.NetAmount);
            PayrollError = treasuryError;
        }
        catch
        {
            PayrollNetPreview = "—";
            PayrollError = null;
        }
    }

    [RelayCommand]
    private async Task CreatePayrollAsync()
    {
        PayrollError = null;
        IsBusy = true;
        try
        {
            var calc = await _personnelService.CalculatePayrollAsync(
                EditingEmployeeId,
                PayrollYear,
                PayrollMonth,
                ParseDecimal(PayrollBonusesText),
                ParseDecimal(PayrollPenaltiesText),
                ParseDecimal(PayrollAdvancesText),
                ParseDecimal(PayrollDeductionsText));

            var treasuryError = await _personnelService.ValidatePayrollAgainstTreasuryAsync(calc.NetAmount);
            if (!string.IsNullOrEmpty(treasuryError))
            {
                PayrollError = treasuryError;
                return;
            }

            var (error, payment) = await _personnelService.CreateSalaryPaymentAsync(
                EditingEmployeeId, PayrollYear, PayrollMonth, calc, validate: false);

            if (!string.IsNullOrEmpty(error))
            {
                PayrollError = error;
                return;
            }

            IsPayrollDialogOpen = false;
            StatusMessage = "Fiche de paie créée.";
            await LoadEmployeeDetailAsync(EditingEmployeeId);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenPaySlipPdfAsync(PersonnelSalaryRow? row)
    {
        if (row is null || row.Id == Guid.Empty)
            return;
        var path = row.PaySlipPdfPath ?? await _personnelService.GeneratePaySlipPdfAsync(row.Id);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            ErrorMessage = "Fiche de paie PDF introuvable.";
            return;
        }
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenContractPdf()
    {
        if (string.IsNullOrWhiteSpace(DetailContractPdfPath) || !File.Exists(DetailContractPdfPath))
        {
            ErrorMessage = "Aucun contrat PDF associé à cet employé.";
            return;
        }
        Process.Start(new ProcessStartInfo(DetailContractPdfPath) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task AttachContractPdfAsync()
    {
        if (!CanManagePersonnel || EditingEmployeeId == Guid.Empty)
            return;

        var dialog = new OpenFileDialog
        {
            Filter = "PDF (*.pdf)|*.pdf",
            Title = "Sélectionner le contrat PDF"
        };
        if (dialog.ShowDialog() != true)
            return;

        var employee = await _personnelService.GetEmployeeAsync(EditingEmployeeId);
        if (employee is null)
            return;

        var destDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SBMS", "Contracts");
        Directory.CreateDirectory(destDir);
        var dest = Path.Combine(destDir, $"{employee.Matricule}_contrat.pdf");
        File.Copy(dialog.FileName, dest, overwrite: true);
        employee.ContractPdfPath = dest;
        employee.MarkUpdated();
        await _personnelService.UpdateEmployeeAsync(employee);
        DetailContractPdfPath = dest;
        StatusMessage = "Contrat PDF enregistré.";
    }

    [RelayCommand]
    private void OpenDisciplinaryDialog()
    {
        if (!CanManagePersonnel || EditingEmployeeId == Guid.Empty)
            return;
        DisciplinaryTitle = string.Empty;
        DisciplinaryDescription = string.Empty;
        DisciplinaryCategory = RhConstants.DisciplinaryCategory.Remark;
        DisciplinaryError = null;
        IsDisciplinaryDialogOpen = true;
    }

    [RelayCommand]
    private void CloseDisciplinaryDialog() => IsDisciplinaryDialogOpen = false;

    [RelayCommand]
    private async Task SaveDisciplinaryNoteAsync()
    {
        DisciplinaryError = null;
        IsBusy = true;
        try
        {
            var note = new DisciplinaryNote
            {
                EmployeeId = EditingEmployeeId,
                Category = DisciplinaryCategory,
                Title = DisciplinaryTitle,
                Description = DisciplinaryDescription,
                OccurredAt = DateTime.UtcNow,
                IssuedBy = UserName
            };
            var error = await _personnelService.AddDisciplinaryNoteAsync(note);
            if (!string.IsNullOrEmpty(error))
            {
                DisciplinaryError = error;
                return;
            }
            IsDisciplinaryDialogOpen = false;
            StatusMessage = "Remarque enregistrée.";
            await LoadEmployeeDetailAsync(EditingEmployeeId);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static decimal ParseDecimal(string text) =>
        decimal.TryParse(text.Replace(',', '.'), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
}

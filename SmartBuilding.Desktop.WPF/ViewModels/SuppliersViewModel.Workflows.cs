using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartBuilding.Domain.Entities.Suppliers;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class SuppliersViewModel
{
    private readonly SuppliersContractPdfService _suppliersPdf = new();

    [ObservableProperty] private bool _isContractFormOpen;
    [ObservableProperty] private bool _isInvoiceFormOpen;
    [ObservableProperty] private bool _isInterventionFormOpen;
    [ObservableProperty] private bool _isContractDetailsOpen;

    [ObservableProperty] private Guid _contractSupplierId;
    [ObservableProperty] private string _contractNumber = string.Empty;
    [ObservableProperty] private DateTime _contractStartDate = DateTime.Today;
    [ObservableProperty] private DateTime _contractEndDate = DateTime.Today.AddMonths(12);
    [ObservableProperty] private string _contractDescription = string.Empty;
    [ObservableProperty] private string _contractBuilding = "Tour SBMS";
    [ObservableProperty] private string _contractStatus = "Actif";
    [ObservableProperty] private string _contractAmountText = "0";
    [ObservableProperty] private string? _contractError;

    [ObservableProperty] private Guid _invoiceSupplierId;
    [ObservableProperty] private string _invoiceReference = string.Empty;
    [ObservableProperty] private DateTime _invoiceDate = DateTime.Today;
    [ObservableProperty] private DateTime _invoiceDueDate = DateTime.Today.AddDays(30);
    [ObservableProperty] private string _invoiceDescription = "Facture prestation";
    [ObservableProperty] private string _invoiceCategory = "Maintenance";
    [ObservableProperty] private string _invoiceNewCategory = string.Empty;
    [ObservableProperty] private string _invoiceAmountText = "0";
    [ObservableProperty] private bool _invoiceIsPaid = true;
    [ObservableProperty] private string? _invoiceError;

    [ObservableProperty] private Guid _interventionSupplierId;
    [ObservableProperty] private DateTime _interventionDate = DateTime.Today.AddDays(2);
    [ObservableProperty] private string _interventionDescription = string.Empty;
    [ObservableProperty] private string _interventionAmountText = "0";
    [ObservableProperty] private string? _interventionError;

    public ObservableCollection<SupplierListItem> SupplierChoices { get; } = [];
    public ObservableCollection<string> ContractStatuses { get; } = ["Actif", "En attente", "Expiré"];
    public ObservableCollection<string> InvoiceCategories { get; } =
    [
        "Maintenance", "Énergie", "Sécurité", "Internet", "Fournitures", "Service", "Autre"
    ];

    [RelayCommand]
    private void OpenContractForm()
    {
        RebuildSupplierChoices();
        ContractSupplierId = SupplierChoices.FirstOrDefault()?.Id ?? Guid.Empty;
        ContractNumber = string.Empty;
        ContractStartDate = DateTime.Today;
        ContractEndDate = DateTime.Today.AddMonths(12);
        ContractDescription = "Contrat de maintenance annuelle";
        ContractBuilding = "Tour SBMS";
        ContractStatus = "Actif";
        ContractAmountText = "0";
        ContractError = null;
        IsContractFormOpen = true;
    }

    [RelayCommand]
    private void CloseContractForm() => IsContractFormOpen = false;

    [RelayCommand]
    private async Task SaveContractAsync()
    {
        ContractError = null;
        var amount = ParseAmount(ContractAmountText);
        IsBusy = true;
        try
        {
            var error = await _suppliersService.CreateContractAsync(new SupplierContract
            {
                SupplierId = ContractSupplierId,
                ContractNumber = ContractNumber,
                StartDate = ContractStartDate,
                EndDate = ContractEndDate,
                Description = ContractDescription,
                TotalValue = amount,
                Status = ContractStatus,
                Building = ContractBuilding
            });

            if (!string.IsNullOrWhiteSpace(error))
            {
                ContractError = error;
                return;
            }

            IsContractFormOpen = false;
            StatusMessage = "Contrat fournisseur enregistré.";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenInvoiceForm()
    {
        RebuildSupplierChoices();
        InvoiceSupplierId = SupplierChoices.FirstOrDefault()?.Id ?? Guid.Empty;
        InvoiceReference = string.Empty;
        InvoiceDate = DateTime.Today;
        InvoiceDueDate = DateTime.Today.AddDays(30);
        InvoiceDescription = "Facture prestation";
        InvoiceCategory = "Maintenance";
        InvoiceNewCategory = string.Empty;
        InvoiceAmountText = "0";
        InvoiceIsPaid = true;
        InvoiceError = null;
        IsInvoiceFormOpen = true;
    }

    [RelayCommand]
    private void CloseInvoiceForm() => IsInvoiceFormOpen = false;

    [RelayCommand]
    private async Task SaveInvoiceAsync()
    {
        InvoiceError = null;
        var normalizedNewCategory = InvoiceNewCategory?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(normalizedNewCategory))
        {
            if (!InvoiceCategories.Any(c => string.Equals(c, normalizedNewCategory, StringComparison.OrdinalIgnoreCase)))
                InvoiceCategories.Add(normalizedNewCategory);
            InvoiceCategory = InvoiceCategories.First(c => string.Equals(c, normalizedNewCategory, StringComparison.OrdinalIgnoreCase));
            InvoiceNewCategory = string.Empty;
        }

        var amount = ParseAmount(InvoiceAmountText);
        IsBusy = true;
        try
        {
            var error = await _suppliersService.CreateInvoiceAsync(new SupplierPayment
            {
                SupplierId = InvoiceSupplierId,
                Amount = amount,
                PaymentDate = InvoiceDate,
                DueDate = InvoiceDueDate,
                InvoiceReference = InvoiceReference,
                Description = InvoiceDescription,
                Category = InvoiceCategory,
                IsPaid = InvoiceIsPaid
            }, UserName);

            if (!string.IsNullOrWhiteSpace(error))
            {
                InvoiceError = error;
                return;
            }

            IsInvoiceFormOpen = false;
            StatusMessage = InvoiceIsPaid
                ? "Facture enregistrée (en attente validation PDG)."
                : "Facture enregistrée (impayée).";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenInterventionForm()
    {
        RebuildSupplierChoices();
        InterventionSupplierId = SupplierChoices.FirstOrDefault()?.Id ?? Guid.Empty;
        InterventionDate = DateTime.Today.AddDays(2);
        InterventionDescription = "Intervention technique planifiée";
        InterventionAmountText = "0";
        InterventionError = null;
        IsInterventionFormOpen = true;
    }

    [RelayCommand]
    private void CloseInterventionForm() => IsInterventionFormOpen = false;

    [RelayCommand]
    private void OpenSupplierDetails(SupplierListItem? supplier)
    {
        if (supplier is null)
            return;
        SelectedSupplier = supplier;
        IsDetailPanelOpen = true;
    }

    [RelayCommand]
    private void ViewContractDetails(SupplierListItem? supplier)
    {
        var target = supplier ?? SelectedSupplier;
        if (target is null)
        {
            ErrorMessage = "Sélectionnez un fournisseur pour voir le contrat.";
            return;
        }

        if (target.ContractDisplay == "—")
        {
            ErrorMessage = "Aucun contrat disponible pour ce fournisseur.";
            return;
        }

        SelectedSupplier = target;
        IsContractDetailsOpen = true;
        ErrorMessage = null;
    }

    [RelayCommand]
    private void CloseContractDetails() => IsContractDetailsOpen = false;

    [RelayCommand]
    private void ExportContractPdf()
    {
        var target = SelectedSupplier;
        if (target is null)
        {
            ErrorMessage = "Sélectionnez un fournisseur pour exporter le contrat.";
            return;
        }

        if (target.ContractDisplay == "—")
        {
            ErrorMessage = "Aucun contrat à exporter pour ce fournisseur.";
            return;
        }

        var path = _suppliersPdf.ExportContractDetails(target);
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
        StatusMessage = $"Contrat exporté : {path}";
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task SaveInterventionAsync()
    {
        InterventionError = null;
        var amount = ParseAmount(InterventionAmountText);
        IsBusy = true;
        try
        {
            var error = await _suppliersService.PlanInterventionAsync(
                InterventionSupplierId,
                InterventionDate,
                InterventionDescription,
                amount,
                UserName);

            if (!string.IsNullOrWhiteSpace(error))
            {
                InterventionError = error;
                return;
            }

            IsInterventionFormOpen = false;
            StatusMessage = "Intervention fournisseur planifiée.";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RebuildSupplierChoices()
    {
        SupplierChoices.Clear();
        foreach (var item in _allSuppliers.OrderBy(s => s.Name))
            SupplierChoices.Add(item);
    }

    private static decimal ParseAmount(string text)
    {
        if (decimal.TryParse(text.Replace(" ", "").Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var value))
            return value;

        return 0;
    }
}

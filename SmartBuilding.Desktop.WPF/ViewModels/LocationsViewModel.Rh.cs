using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartBuilding.Domain.Entities.Location;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Desktop.WPF.Services;

namespace SmartBuilding.Desktop.WPF.ViewModels;

public partial class LocationsViewModel
{
    [ObservableProperty] private bool _isEditPremiseFormOpen;
    [ObservableProperty] private Guid _editPremiseId;
    [ObservableProperty] private int _activeGuarantees;
    [ObservableProperty] private LocationsContractItem? _selectedContract;

    [RelayCommand]
    private async Task AddBuilding()
    {
        if (!CanManageLocations) { StatusMessage = "Permission refusée."; return; }
        await _shellNavigation.OpenBuildingFormAsync(null);
    }

    [RelayCommand]
    private async Task EditBuilding(LocationsBuildingItem? item)
    {
        if (!CanManageLocations || item is null) return;
        await _shellNavigation.OpenBuildingFormAsync(item.Id);
    }

    [RelayCommand]
    private async Task DeleteBuildingAsync(LocationsBuildingItem? item)
    {
        if (!CanManageLocations || item is null) return;
        if (!SbmsDialogService.Confirm(
                "Confirmation",
                $"Supprimer le bâtiment \"{item.Name}\" ? Cette action est irréversible."))
            return;

        IsBusy = true;
        try
        {
            var error = await _locationsService.DeleteBuildingAsync(item.Id);
            StatusMessage = string.IsNullOrEmpty(error) ? "Bâtiment supprimé." : error;
            if (string.IsNullOrEmpty(error)) await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RefundGuaranteeAsync(LocationsGuaranteeItem? item)
    {
        if (!CanManageLocations || item is null) return;
        if (!SbmsDialogService.Confirm(
                "Confirmer le remboursement",
                "Confirmer le remboursement de cette garantie ?\n\nLe contrat sera clôturé, le local libéré et le locataire archivé."))
            return;

        IsBusy = true;
        try
        {
            var guarantees = await _locationsService.GetGuaranteesAsync();
            var g = guarantees.FirstOrDefault(x => x.Id == item.Id);
            if (g is null) { StatusMessage = "Garantie introuvable."; return; }
            var remaining = g.Amount - g.AmountRefunded;
            var error = await _locationsService.RefundGuaranteeAsync(item.Id, remaining);
            if (!string.IsNullOrEmpty(error))
            {
                StatusMessage = error;
                return;
            }

            var dischargePath = await _locationsService.GetGuaranteeDischargePdfPathAsync(item.Id);
            if (!string.IsNullOrWhiteSpace(dischargePath) && File.Exists(dischargePath))
            {
                Process.Start(new ProcessStartInfo(dischargePath) { UseShellExecute = true });
                StatusMessage = "Garantie remboursée — décharge générée.";
            }
            else
                StatusMessage = "Garantie remboursée.";

            await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task GenerateReceiptPdfAsync(LocationsPaymentItem? payment)
    {
        if (payment is null || payment.Id == Guid.Empty) return;
        if (!SbmsDialogService.Confirm(
                "Générer une quittance",
                "Générer la quittance PDF de ce paiement ?"))
            return;

        IsBusy = true;
        try
        {
            var path = await _locationsService.GenerateRentReceiptPdfAsync(payment.Id);
            StatusMessage = path is null ? "Paiement introuvable ou montant nul." : $"Quittance : {path}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void ToggleNotifications() => IsNotificationsOpen = !IsNotificationsOpen;

    [RelayCommand]
    private void EditPremise(LocationsPremiseItem? premise)
    {
        if (!CanManageLocations || premise is null)
            return;

        CloseAllForms();
        EditPremiseId = premise.Id;
        FormCode = premise.Code;
        FormName = premise.Name;
        FormBuilding = premise.Building;
        FormFloor = premise.Floor == "—" ? string.Empty : premise.Floor;
        FormType = premise.PremiseType;
        FormRentText = premise.MonthlyRent.ToString("F2");
        FormError = null;
        IsEditPremiseFormOpen = true;
    }

    [RelayCommand]
    private async Task SaveEditPremiseAsync()
    {
        if (!CanManageLocations) { FormError = "Permission refusée."; return; }
        if (!SbmsDialogService.Confirm(
                "Confirmer la modification",
                "Enregistrer les modifications de ce local ?"))
            return;

        FormError = null;
        if (EditPremiseId == Guid.Empty)
        {
            FormError = "Local non sélectionné.";
            return;
        }

        IsBusy = true;
        try
        {
            var premise = await _locationsService.GetPremiseAsync(EditPremiseId);
            if (premise is null)
            {
                FormError = "Local introuvable.";
                return;
            }

            if (!decimal.TryParse(FormRentText.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var rent))
                rent = premise.MonthlyRent;

            premise.Name = FormName;
            premise.Building = FormBuilding;
            premise.Floor = FormFloor;
            premise.PremiseType = FormType;
            premise.MonthlyRent = rent;

            var error = await _locationsService.UpdatePremiseAsync(premise);
            if (!string.IsNullOrEmpty(error))
            {
                FormError = error;
                return;
            }

            IsEditPremiseFormOpen = false;
            StatusMessage = "Local mis à jour.";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task EditTenant(LocationsTenantItem? tenant)
    {
        if (!CanManageLocations || tenant is null) return;
        await _shellNavigation.OpenTenantFormAsync(tenant.Id);
    }

    [RelayCommand]
    private async Task SuspendTenantAsync(LocationsTenantItem? tenant)
    {
        if (!CanManageLocations || tenant is null) return;
        if (!SbmsDialogService.Confirm(
                "Confirmer la suspension",
                $"Suspendre le locataire \"{tenant.Name}\" ?"))
            return;

        IsBusy = true;
        try
        {
            var error = await _locationsService.SuspendTenantAsync(tenant.Id, "Suspension administrative");
            StatusMessage = string.IsNullOrEmpty(error) ? "Locataire suspendu." : error;
            if (string.IsNullOrEmpty(error))
                await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ValidateContractAsync(LocationsContractItem? contract)
    {
        if (!CanManageLocations || contract is null || contract.Id == Guid.Empty)
            return;
        if (!SbmsDialogService.Confirm(
                "Validation du contrat",
                $"Valider le contrat {contract.ContractNumber} et occuper le local ?"))
            return;

        IsBusy = true;
        try
        {
            var error = await _locationsService.ValidateContractAsync(contract.Id, UserName);
            StatusMessage = string.IsNullOrEmpty(error) ? "Contrat validé et activé." : error;
            if (string.IsNullOrEmpty(error))
                await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task TerminateContractAsync(LocationsContractItem? contract)
    {
        if (!CanManageLocations || contract is null || contract.Id == Guid.Empty)
            return;
        if (!SbmsDialogService.Confirm(
                "Résilier le contrat",
                $"Résilier le contrat {contract.ContractNumber} ? Le local sera remis disponible."))
            return;

        IsBusy = true;
        try
        {
            var error = await _locationsService.TerminateContractAsync(contract.Id, "Résiliation demandée", UserName);
            StatusMessage = string.IsNullOrEmpty(error) ? "Contrat résilié." : error;
            if (string.IsNullOrEmpty(error))
                await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GenerateContractPdfAsync(LocationsContractItem? contract)
    {
        if (contract is null || contract.Id == Guid.Empty)
            return;

        IsBusy = true;
        try
        {
            var path = await _locationsService.GenerateContractPdfAsync(contract.Id);
            StatusMessage = path is null ? "Contrat introuvable." : $"PDF généré : {path}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyLocationAlerts(LocationsPageData data)
    {
        ActiveGuarantees = data.ActiveGuarantees;
        var alerts = _locationsService.GetLocationAlerts(data, DateTime.Today);
        NotificationMessages.Clear();
        foreach (var a in alerts) NotificationMessages.Add(a);
        if (NotificationMessages.Count == 0)
            NotificationMessages.Add("Aucune alerte pour le moment.");
        NotificationCount = NotificationMessages.Count;
    }
}

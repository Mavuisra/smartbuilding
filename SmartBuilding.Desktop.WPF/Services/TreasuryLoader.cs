using SmartBuilding.Domain.Entities.Finance;
using SmartBuilding.Infrastructure.Services;

namespace SmartBuilding.Desktop.WPF.Services;

/// <summary>Charge la position de trésorerie (loyers encaissés − dépenses).</summary>
internal static class TreasuryLoader
{
    public static async Task<FinanceCashPosition> LoadAsync(
        FinanceLedgerService ledger,
        CancellationToken cancellationToken = default)
    {
        await ledger.ReconcileAllAsync(cancellationToken);
        return await ledger.GetCashPositionAsync(cancellationToken);
    }
}

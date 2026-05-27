using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Finance;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Infrastructure.Persistence;

namespace SmartBuilding.Infrastructure.Services;

/// <summary>
/// Supprime les dépenses / maintenances de démonstration du module Technique.
/// </summary>
public class TechnicalDataCleaner
{
    private readonly SmartBuildingDbContext _db;

    public TechnicalDataCleaner(SmartBuildingDbContext db) => _db = db;

    public sealed class TechnicalFinanceDiagnostics
    {
        public decimal MaintenanceRecordsCostTotal { get; init; }
        public int MaintenanceRecordsCount { get; init; }
        public decimal EquipmentPurchaseValueTotal { get; init; }
        public decimal TechniqueExpensesInLedger { get; init; }
        public int TechniqueExpensesCount { get; init; }
        public decimal RentCollectedInLedger { get; init; }
    }

    public async Task<TechnicalFinanceDiagnostics> GetDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        var maintenance = await _db.MaintenanceRecords
            .Select(m => m.Cost)
            .ToListAsync(cancellationToken);

        var purchaseTotal = (await _db.Equipment.Select(e => e.PurchaseValue).ToListAsync(cancellationToken)).Sum();

        var techExpenses = await _db.FinancialTransactions
            .Where(t => t.Type == TransactionType.Depense &&
                        (t.Source == FinanceConstants.SourceTechnique ||
                         t.Category.Contains("Maintenance") ||
                         t.Category == FinanceConstants.CategoryMaintenance))
            .Select(t => t.Amount)
            .ToListAsync(cancellationToken);

        var rentCollected = (await _db.FinancialTransactions
            .Where(t => t.Type == TransactionType.Recette && t.Category == FinanceConstants.CategoryRent)
            .Select(t => t.Amount)
            .ToListAsync(cancellationToken)).Sum();

        return new TechnicalFinanceDiagnostics
        {
            MaintenanceRecordsCostTotal = maintenance.Sum(),
            MaintenanceRecordsCount = maintenance.Count,
            EquipmentPurchaseValueTotal = purchaseTotal,
            TechniqueExpensesInLedger = techExpenses.Sum(),
            TechniqueExpensesCount = techExpenses.Count,
            RentCollectedInLedger = rentCollected
        };
    }

    /// <summary>
    /// Supprime toutes les fiches maintenance et les écritures de dépenses liées au module Technique.
    /// </summary>
    public async Task<int> ClearFictitiousMaintenanceAsync(CancellationToken cancellationToken = default)
    {
        var deleted = 0;

        deleted += await _db.MaintenanceRecords
            .Where(m => m.IsSynced)
            .ExecuteDeleteAsync(cancellationToken);

        var techExpenses = await _db.FinancialTransactions
            .Where(t => t.Type == TransactionType.Depense &&
                        t.IsSynced &&
                        (t.Source == FinanceConstants.SourceTechnique ||
                         t.Category.Contains("Maintenance") ||
                         t.Category == FinanceConstants.CategoryMaintenance))
            .ExecuteDeleteAsync(cancellationToken);
        deleted += techExpenses;

        return deleted;
    }
}

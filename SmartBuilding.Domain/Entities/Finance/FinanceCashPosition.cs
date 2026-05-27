namespace SmartBuilding.Domain.Entities.Finance;

/// <summary>
/// Position de trésorerie : les dépenses ne peuvent pas dépasser les loyers encaissés.
/// </summary>
public sealed class FinanceCashPosition
{
    /// <summary>Total des loyers encaissés (source : paiements Locations).</summary>
    public decimal RentCollectedTotal { get; init; }

    /// <summary>Loyers encaissés pour le mois en cours.</summary>
    public decimal RentCollectedThisMonth { get; init; }

    /// <summary>Total de toutes les dépenses enregistrées.</summary>
    public decimal TotalExpenses { get; init; }

    /// <summary>Montant encore disponible pour de nouvelles dépenses (total).</summary>
    public decimal AvailableBalance => RentCollectedTotal - TotalExpenses;

    /// <summary>Dépenses du mois en cours.</summary>
    public decimal TotalExpensesThisMonth { get; init; }

    /// <summary>Disponible sur le mois : loyers du mois − dépenses du mois.</summary>
    public decimal AvailableThisMonth => RentCollectedThisMonth - TotalExpensesThisMonth;

    public bool CanSpend(decimal amount) => amount > 0 && amount <= AvailableBalance;
}

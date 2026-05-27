using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace SmartBuilding.Infrastructure.Services;

/// <summary>
/// Convertit les erreurs EF / SQLite en messages compréhensibles pour l'utilisateur.
/// </summary>
public static class DbSaveExceptionTranslator
{
    public static string ToUserMessage(Exception exception)
    {
        foreach (var ex in EnumerateExceptions(exception))
        {
            if (ex is SqliteException sqlite)
            {
                var message = FromSqlite(sqlite);
                if (message is not null)
                    return message;
            }
        }

        if (exception is DbUpdateException)
            return "Impossible d'enregistrer les données. Vérifiez les informations saisies (doublon, champ obligatoire ou lien manquant).";

        return exception.Message;
    }

    public static string ToDetailedMessage(Exception exception)
    {
        var user = ToUserMessage(exception);
        var technical = exception.InnerException?.Message;
        return string.IsNullOrWhiteSpace(technical) || technical == user
            ? user
            : $"{user}\n\nDétail technique : {technical}";
    }

    private static IEnumerable<Exception> EnumerateExceptions(Exception exception)
    {
        for (var ex = exception; ex is not null; ex = ex.InnerException)
            yield return ex;
    }

    private static string? FromSqlite(SqliteException ex)
    {
        if (ex.SqliteErrorCode != 19)
            return null;

        var raw = ex.Message;

        if (raw.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
        {
            if (raw.Contains("ContractNumber", StringComparison.OrdinalIgnoreCase) ||
                raw.Contains("LeaseContracts.ContractNumber", StringComparison.OrdinalIgnoreCase))
                return "Ce numéro de contrat existe déjà. Choisissez un autre numéro.";

            if (raw.Contains("Tenants", StringComparison.OrdinalIgnoreCase))
                return "Ce locataire existe déjà (doublon détecté).";

            if (raw.Contains("Premises", StringComparison.OrdinalIgnoreCase) ||
                raw.Contains("Code", StringComparison.OrdinalIgnoreCase))
                return "Ce code de local existe déjà.";

            return "Cette valeur existe déjà dans la base (contrainte d'unicité).";
        }

        if (raw.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase))
            return "Référence invalide : le locataire, le local ou le contrat lié n'existe plus.";

        if (raw.Contains("NOT NULL", StringComparison.OrdinalIgnoreCase))
            return "Un champ obligatoire est manquant.";

        return "Donnée refusée par la base (contrainte non respectée).";
    }
}

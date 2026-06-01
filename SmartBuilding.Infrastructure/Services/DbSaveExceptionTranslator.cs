using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace SmartBuilding.Infrastructure.Services;

/// <summary>
/// Convertit les erreurs EF / SQLite / MySQL en messages compréhensibles pour l'utilisateur.
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

            if (ex is MySqlException mysql)
            {
                var message = FromMySql(mysql);
                if (message is not null)
                    return message;
            }
        }

        if (exception is DbUpdateException)
            return "Impossible d'enregistrer les données. Vérifiez les informations saisies (doublon, champ obligatoire ou lien manquant).";

        var generic = exception.Message;
        if (generic.Contains("See the inner exception", StringComparison.OrdinalIgnoreCase)
            && exception.InnerException is not null)
        {
            return ToUserMessage(exception.InnerException);
        }

        return generic;
    }

    public static string ToDetailedMessage(Exception exception)
    {
        var lines = new List<string>();
        AddUniqueLine(lines, ToUserMessage(exception));

        foreach (var ex in EnumerateExceptions(exception))
        {
            if (ex is MySqlException mysql)
                AddUniqueLine(lines, FromMySql(mysql) ?? mysql.Message);

            if (ex is SqliteException sqlite)
                AddUniqueLine(lines, FromSqlite(sqlite) ?? sqlite.Message);

            if (!string.IsNullOrWhiteSpace(ex.Message)
                && !ex.Message.Contains("See the inner exception", StringComparison.OrdinalIgnoreCase))
            {
                AddUniqueLine(lines, ex.Message);
            }
        }

        return lines.Count == 0 ? "Erreur inconnue." : string.Join(Environment.NewLine + Environment.NewLine, lines);
    }

    private static void AddUniqueLine(List<string> lines, string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        if (lines.Any(existing => existing.Contains(line, StringComparison.OrdinalIgnoreCase)
                                  || line.Contains(existing, StringComparison.OrdinalIgnoreCase)))
            return;

        lines.Add(line.Trim());
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

    private static string? FromMySql(MySqlException ex)
    {
        return ex.Number switch
        {
            1146 => "Table MySQL introuvable : la base du serveur n'a pas été initialisée. Lancez SBMS une première fois en mode « Serveur » sur le PC avec XAMPP.",
            1049 => "Base de données introuvable sur le serveur. Vérifiez le nom (ex. sbms_local) ou initialisez le PC serveur.",
            1045 or 1044 => "Accès MySQL refusé : vérifiez l'utilisateur et le mot de passe (ex. sbms + script deploy/mysql-utilisateur-reseau.sql).",
            1062 when ex.Message.Contains("IX_Users_Email", StringComparison.OrdinalIgnoreCase)
                => "Cet e-mail est déjà utilisé par un autre compte. Utilisez un autre e-mail entreprise ou laissez le champ vide (SBMS utilisera votre nom d'utilisateur).",
            1062 => "Cette valeur existe déjà dans la base (doublon, ex. nom d'utilisateur).",
            2003 or 2002 => "Impossible de joindre le serveur MySQL. Vérifiez l'IP, XAMPP démarré et le pare-feu (port 3306).",
            0 when ex.Message.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase)
                => "Table ou base MySQL manquante. Initialisez d'abord le PC serveur en mode « Serveur ».",
            _ when ex.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)
                => "Doublon détecté dans la base MySQL.",
            _ when ex.Message.Contains("foreign key", StringComparison.OrdinalIgnoreCase)
                => "Référence invalide dans la base (lien manquant).",
            _ => null
        };
    }
}

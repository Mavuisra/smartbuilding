using Microsoft.Data.Sqlite;
using SmartBuilding.Infrastructure.Services;
using Xunit;

namespace SmartBuilding.Tests.Infrastructure;

public class DbSaveExceptionTranslatorTests
{
    [Fact]
    public void ToUserMessage_UniqueContractNumber_ReturnsFrenchHint()
    {
        var ex = new SqliteException(
            "SQLite Error 19: 'UNIQUE constraint failed: LeaseContracts.ContractNumber'.",
            19);

        var message = DbSaveExceptionTranslator.ToUserMessage(ex);

        Assert.Contains("numéro de contrat", message, StringComparison.OrdinalIgnoreCase);
    }
}

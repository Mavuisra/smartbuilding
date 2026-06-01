using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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

    [Fact]
    public void ToDetailedMessage_DbUpdate_UnwrapsInnerMessage()
    {
        var ex = new DbUpdateException(
            "An error occurred while saving the entity changes.",
            new Exception("Duplicate entry 'admin' for key 'Users.Username'"));

        var message = DbSaveExceptionTranslator.ToDetailedMessage(ex);

        Assert.Contains("Duplicate", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("See the inner exception", message, StringComparison.OrdinalIgnoreCase);
    }
}

using Microsoft.EntityFrameworkCore;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Desktop.WPF.Services;
using SmartBuilding.Domain.Entities.Personnel;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Services;
using Xunit;

namespace SmartBuilding.Tests.Personnel;

public class PersonnelServiceCreateTests
{
    [Fact]
    public async Task CreateEmployeeAsync_Allows_Create_After_Existing_Employee_Is_Tracked()
    {
        await using var db = await CreateDbAsync();
        var existing = new Employee
        {
            Matricule = "EMP-0001",
            FirstName = "Alice",
            LastName = "Martin",
            Position = "RH",
            Department = "RH",
            HireDate = DateTime.Today,
        };
        db.Employees.Add(existing);
        await db.SaveChangesAsync();

        _ = await db.Employees.FirstAsync(e => e.Id == existing.Id);

        var service = CreateService(db);
        var duplicateId = new Employee
        {
            Id = existing.Id,
            Matricule = "EMP-0002",
            FirstName = "Bob",
            LastName = "Dupont",
            Position = "Technique",
            Department = "Technique",
            HireDate = DateTime.Today,
        };

        var error = await service.CreateEmployeeAsync(duplicateId);

        Assert.Equal(string.Empty, error);
        Assert.Equal(2, await db.Employees.CountAsync());
        Assert.NotEqual(existing.Id, duplicateId.Id);
    }

    private static PersonnelService CreateService(SmartBuildingDbContext db) =>
        new(db, new FinanceLedgerService(db), new NoopDocumentUpload());

    private static async Task<SmartBuildingDbContext> CreateDbAsync()
    {
        var options = new DbContextOptionsBuilder<SmartBuildingDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var db = new SmartBuildingDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private sealed class NoopDocumentUpload : IDocumentCloudUploadService
    {
        public Task<bool> TryUploadFileAsync(
            string localPath,
            string entityType,
            Guid entityId,
            string category,
            string? addedBy = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<int> UploadAllPendingAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }
}

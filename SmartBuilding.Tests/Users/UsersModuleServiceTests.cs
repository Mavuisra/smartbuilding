using Microsoft.EntityFrameworkCore;
using SmartBuilding.Domain.Entities.Auth;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Desktop.WPF.Services;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Services;
using Xunit;

namespace SmartBuilding.Tests.Users;

public class UsersModuleServiceTests
{
    [Fact]
    public async Task CreateUserAsync_Creates_Receptionniste_WithPermissions()
    {
        await using var db = await CreateDbAsync();
        await SeedPermissionsAsync(db);
        var service = new UsersModuleService(db);

        var (ok, error) = await service.CreateUserAsync(
            "reception1", "Marie Accueil", "marie@test.local", "Test@123", UserRole.Receptionniste);

        Assert.True(ok, error);
        var user = await db.Users.SingleAsync(u => u.Username == "reception1");
        Assert.Equal(UserRole.Receptionniste, user.Role);
        Assert.True(user.IsActive);

        var perms = await service.LoadPermissionsAsync(user.Id);
        Assert.Single(perms);
        Assert.Equal("visitors.manage", perms[0].Code);
    }

    [Fact]
    public async Task UpdateUserAsync_Changes_Role_And_Email()
    {
        await using var db = await CreateDbAsync();
        var service = new UsersModuleService(db);
        await service.CreateUserAsync("user1", "User One", "a@b.c", "pass12", UserRole.Gestionnaire);

        var id = (await db.Users.SingleAsync()).Id;
        var (ok, error) = await service.UpdateUserAsync(id, "User Un", "new@b.c", UserRole.Comptable, null);

        Assert.True(ok, error);
        var updated = await db.Users.SingleAsync();
        Assert.Equal("new@b.c", updated.Email);
        Assert.Equal(UserRole.Comptable, updated.Role);
    }

    [Fact]
    public async Task SetUserActiveAsync_Blocks_Self_Suspend()
    {
        await using var db = await CreateDbAsync();
        var adminId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = adminId,
            Username = "admin",
            FullName = "Admin",
            Email = "a@a.c",
            PasswordHash = AuthService.HashPassword("pass12"),
            Role = UserRole.Administrateur,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var service = new UsersModuleService(db);
        var (ok, error) = await service.SetUserActiveAsync(adminId, false, adminId);

        Assert.False(ok);
        Assert.Contains("propre compte", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Admin_User_With_Wrong_Role_Should_Be_Fixed_By_Seeder()
    {
        await using var db = await CreateDbAsync();
        db.Users.Add(new User
        {
            Username = "admin",
            FullName = "admin",
            Email = "admin@test.local",
            PasswordHash = AuthService.HashPassword("Admin@2026"),
            Role = UserRole.Gestionnaire,
            IsActive = true
        });
        await db.SaveChangesAsync();

        await DatabaseSeeder.SeedAsync(db);

        var user = await db.Users.SingleAsync(u => u.Username == "admin");
        Assert.Equal(UserRole.Administrateur, user.Role);
        Assert.Equal("Administrateur", user.FullName);
    }

    [Fact]
    public async Task SeedAsync_Creates_Admin2_When_Missing()
    {
        await using var db = await CreateDbAsync();
        db.Users.Add(new User
        {
            Username = "admin",
            FullName = "Administrateur",
            Email = "admin@test.local",
            PasswordHash = AuthService.HashPassword(DatabaseSeeder.BootstrapAdminPassword),
            Role = UserRole.Administrateur,
            IsActive = true
        });
        await db.SaveChangesAsync();

        await DatabaseSeeder.SeedAsync(db);

        var admin2 = await db.Users.SingleOrDefaultAsync(u => u.Username == "admin2");
        Assert.NotNull(admin2);
        Assert.Equal(UserRole.Administrateur, admin2!.Role);
        Assert.True(BCrypt.Net.BCrypt.Verify(DatabaseSeeder.BootstrapAdminPassword, admin2.PasswordHash));
    }

    [Fact]
    public async Task Admini_User_With_Receptionniste_Role_Should_Be_Fixed_By_Seeder()
    {
        await using var db = await CreateDbAsync();
        db.Users.Add(new User
        {
            Username = "admini",
            FullName = "admini",
            Email = "admini@test.local",
            PasswordHash = AuthService.HashPassword("Admin@2026"),
            Role = UserRole.Receptionniste,
            IsActive = true
        });
        await db.SaveChangesAsync();

        await DatabaseSeeder.EnsureReservedAdminAccountsAsync(db);

        var user = await db.Users.SingleAsync(u => u.Username == "admini");
        Assert.Equal(UserRole.Administrateur, user.Role);
        Assert.Equal("Administrateur", user.FullName);
        Assert.False(user.IsSynced);
    }

    [Fact]
    public async Task ResetPasswordAsync_Updates_Hash()
    {
        await using var db = await CreateDbAsync();
        var service = new UsersModuleService(db);
        await service.CreateUserAsync("u1", "U1", "", "oldpass", UserRole.Gestionnaire);
        var id = (await db.Users.SingleAsync()).Id;
        var oldHash = (await db.Users.SingleAsync()).PasswordHash;

        var (ok, error) = await service.ResetPasswordAsync(id, "newpass99");
        Assert.True(ok, error);

        var user = await db.Users.SingleAsync();
        Assert.NotEqual(oldHash, user.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("newpass99", user.PasswordHash));
    }

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

    private static async Task SeedPermissionsAsync(SmartBuildingDbContext db)
    {
        db.Permissions.Add(new Permission
        {
            Code = "visitors.manage",
            Name = "Gestion des visites",
            Module = "Réception"
        });
        await db.SaveChangesAsync();
    }
}

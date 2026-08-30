using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SmartBuilding.Application.Interfaces;
using SmartBuilding.Domain.Entities.Auth;
using SmartBuilding.Domain.Enums;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Shared.Constants;
using SmartBuilding.Shared.DTOs.Auth;

namespace SmartBuilding.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly SmartBuildingDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthService(SmartBuildingDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var loginName = request.Username.Trim();
        var candidates = await _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Permissions)
            .ThenInclude(up => up.Permission)
            .Where(u => u.IsActive && u.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var user = candidates.FirstOrDefault(u =>
            string.Equals(u.Username.Trim(), loginName, StringComparison.OrdinalIgnoreCase));

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return null;

        if (DatabaseSeeder.IsReservedAdminUsername(user.Username))
            await DatabaseSeeder.EnsureReservedAdminAccountsAsync(_context, cancellationToken);

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var permissions = GetUserPermissions(user);
        var token = GenerateJwtToken(user, permissions);
        var expiryMinutes = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "480");

        return new LoginResponse
        {
            Token = token,
            UserId = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Role = UserRoleCatalog.ToLabel(user.Role),
            Permissions = permissions,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes)
        };
    }

    public async Task<bool> RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null) return true;

        user.PasswordResetToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        user.PasswordResetExpires = DateTime.UtcNow.AddHours(24);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(
            u => u.PasswordResetToken == token && u.PasswordResetExpires > DateTime.UtcNow,
            cancellationToken);
        if (user is null) return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.PasswordResetToken = null;
        user.PasswordResetExpires = null;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public bool HasPermission(string role, string permissionCode)
    {
        if (!PermissionCodes.RolePermissions.TryGetValue(role, out var perms))
            return false;
        return perms.Contains("*") || perms.Contains(permissionCode);
    }

    private static List<string> GetUserPermissions(User user)
    {
        var roleLabel = UserRoleCatalog.ToLabel(user.Role);
        var rolePerms = PermissionCodes.RolePermissions.GetValueOrDefault(roleLabel, []);
        if (rolePerms.Length == 0)
            rolePerms = PermissionCodes.RolePermissions.GetValueOrDefault(user.Role.ToString(), []);
        if (rolePerms.Contains("*"))
            return PermissionCodes.RolePermissions.Values.SelectMany(x => x).Distinct().ToList();

        var custom = user.Permissions.Select(p => p.Permission.Code).ToList();
        return rolePerms.Concat(custom).Distinct().ToList();
    }

    private string GenerateJwtToken(User user, IReadOnlyList<string> permissions)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("fullName", user.FullName)
        };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "480")),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password);
}

using SmartBuilding.Domain.Common;
using SmartBuilding.Domain.Enums;

namespace SmartBuilding.Domain.Entities.Auth;

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetExpires { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public ICollection<UserPermission> Permissions { get; set; } = [];
}

namespace SmartBuilding.Shared.DTOs.Auth;

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public IReadOnlyList<string> Permissions { get; set; } = [];
    public DateTime ExpiresAt { get; set; }
}

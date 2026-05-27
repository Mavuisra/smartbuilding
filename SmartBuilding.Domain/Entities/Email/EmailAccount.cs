using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Email;

public class EmailAccount : BaseEntity
{
    public Guid UserId { get; set; }
    public string Provider { get; set; } = "Gmail";
    public string EmailAddress { get; set; } = string.Empty;
    public string ImapHost { get; set; } = string.Empty;
    public int ImapPort { get; set; } = 993;
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string EncryptedPassword { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = true;
    public string? FilterKeywords { get; set; }
}

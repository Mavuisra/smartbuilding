using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Email;

public class CachedEmail : BaseEntity
{
    public Guid? AccountId { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string ToAddresses { get; set; } = string.Empty;
    public string? CcAddresses { get; set; }
    public string BodyPreview { get; set; } = string.Empty;
    public string? BodyHtml { get; set; }
    public string? BodyText { get; set; }
    public DateTime ReceivedAt { get; set; }
    public bool IsRead { get; set; }
    public bool IsImportant { get; set; }
    public bool IsArchived { get; set; }
    public bool IsDraft { get; set; }
    public bool IsSpam { get; set; }
    public bool AwaitingReply { get; set; }
    public bool HasAttachments { get; set; }
    public string? AttachmentPaths { get; set; }
    public string Folder { get; set; } = "INBOX";
    public string Category { get; set; } = "Administration";
    public string Priority { get; set; } = "Normal";
    public string AssignedTo { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
}

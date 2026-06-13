namespace SmartBuilding.Shared.DTOs.Sync;

public sealed class DocumentUploadRequest
{
    public Guid EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = "application/pdf";
    public string ContentBase64 { get; set; } = string.Empty;
    public string? AddedBy { get; set; }
    public string? ContentSha256 { get; set; }
    public long FileSizeBytes { get; set; }
}

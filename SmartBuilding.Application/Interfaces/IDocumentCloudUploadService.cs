namespace SmartBuilding.Application.Interfaces;

/// <summary>
/// Envoie les fichiers PDF/documents générés localement vers le cloud (octets identiques).
/// </summary>
public interface IDocumentCloudUploadService
{
    Task<bool> TryUploadFileAsync(
        string localPath,
        string entityType,
        Guid entityId,
        string category,
        string? addedBy = null,
        CancellationToken cancellationToken = default);

    Task<int> UploadAllPendingAsync(CancellationToken cancellationToken = default);
}

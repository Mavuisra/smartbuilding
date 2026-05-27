namespace SmartBuilding.Domain.Common;

/// <summary>
/// Entité de base avec audit et support synchronisation offline-first.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsSynced { get; set; }
    public DateTime? DeletedAt { get; set; }

    public bool IsDeleted => DeletedAt.HasValue;

    public void MarkUpdated()
    {
        UpdatedAt = DateTime.UtcNow;
        IsSynced = false;
    }

    public void SoftDelete()
    {
        DeletedAt = DateTime.UtcNow;
        MarkUpdated();
    }
}

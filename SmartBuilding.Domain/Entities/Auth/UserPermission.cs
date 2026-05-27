using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Auth;

public class UserPermission : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid PermissionId { get; set; }

    public User User { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}

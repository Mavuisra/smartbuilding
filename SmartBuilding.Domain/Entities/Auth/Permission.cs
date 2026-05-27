using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Auth;

public class Permission : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public ICollection<UserPermission> UserPermissions { get; set; } = [];
}

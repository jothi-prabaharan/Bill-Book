using Shared.Kernel.Entities;

namespace Identity.Entity.TableEntities;

public class RolePermission : AuditableEntity
{
    public long RolePermissionId { get; set; }

    public int RoleId { get; set; }

    public int PermissionId { get; set; }
}

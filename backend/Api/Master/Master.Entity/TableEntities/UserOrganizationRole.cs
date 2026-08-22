using Shared.Kernel.Entities;

namespace Master.Entity.TableEntities;

/// <summary>
/// The pivot that makes multi-org access work: one login, a different role per
/// organization. Revoke by setting <see cref="IsActive"/> false — never delete.
/// </summary>
public class UserOrganizationRole : AuditableEntity
{
    public long UserOrganizationRoleId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>No FK — Organizations are owned by the Platform service.</summary>
    public Guid OrgId { get; set; }

    public int RoleId { get; set; }

    public bool IsActive { get; set; } = true;
}

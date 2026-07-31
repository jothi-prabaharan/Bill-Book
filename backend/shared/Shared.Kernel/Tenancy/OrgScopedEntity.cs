using Shared.Kernel.Entities;

namespace Shared.Kernel.Tenancy;

/// <summary>
/// Base for every table in a per-customer schema. Carrying OrgId on a marker
/// type lets the DbContext apply the global query filter by reflection, so a new
/// table cannot be added without one — a missing filter leaks data between
/// organizations, which is the highest-consequence mistake available here.
/// </summary>
public abstract class OrgScopedEntity : AuditableEntity
{
    public Guid OrgId { get; set; }
}

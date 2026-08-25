using Shared.Kernel.Entities;

namespace Shared.Kernel.Tenancy;

/// <summary>
/// Base for every table in the shared tenant database. Carrying CustomerId and
/// OrgId on a marker type lets the DbContext apply the global query filter by
/// reflection, so a new table cannot be added without both — a missing filter
/// leaks data between customers or organizations, which is the
/// highest-consequence mistake available here.
///
/// CustomerId is redundant with OrgId in principle — OrgId is already a
/// globally-unique id allocated from one shared table in <c>mst</c>, so it alone
/// would isolate customers correctly. It is carried as a real column anyway: as
/// an explicit second check on the tenant boundary rather than resting on OrgId's
/// uniqueness alone, and because a physical column is what customer-scoped bulk
/// operations (export, delete, future partitioning) need to work against.
/// </summary>
public abstract class OrgScopedEntity : AuditableEntity
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }
}

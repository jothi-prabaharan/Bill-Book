using Shared.Kernel.Interfaces;

namespace CostingEngine.Worker.Consumers;

/// <summary>
/// The worker acts as no person. Every id is null, which is exactly what the
/// audit columns reserve null for — <c>CreatedBy IS NULL</c> already means
/// "written by no user", so a costing row is indistinguishable from seed data
/// in the way that matters: nobody typed it.
///
/// The tenant still comes from <see cref="Shared.Kernel.Tenancy.ITenantContext"/>,
/// which the worker sets per organization. This only answers "which user", and
/// the honest answer is none.
/// </summary>
public sealed class SystemUser : ICurrentUser
{
    public Guid? UserId => null;

    public Guid? CustomerId => null;

    public Guid? OrgId => null;

    /// <summary>
    /// No role. The worker is not a person, so no period lock applies to it —
    /// costing settles movements whose dates are long past by design.
    /// </summary>
    public int? RoleId => null;
}

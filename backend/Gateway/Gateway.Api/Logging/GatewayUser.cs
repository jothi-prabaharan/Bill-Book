using Shared.Kernel.Interfaces;

namespace Gateway.Api.Logging;

/// <summary>
/// The gateway acts as no person, and could not name one if it wanted to.
///
/// Request logs are written by a background writer draining a queue, long after
/// the request that produced them has been answered — there is no HttpContext to
/// read a token from by then, and the gateway does not validate tokens anyway.
/// Null is the honest answer, and it is exactly what the audit columns reserve
/// null for.
/// </summary>
public sealed class GatewayUser : ICurrentUser
{
    public Guid? UserId => null;

    public Guid? CustomerId => null;

    public Guid? OrgId => null;

    /// <summary>
    /// No role, and none to be had: the gateway does not validate tokens, so it
    /// could not read a role claim even while the request was in flight. A
    /// period-lock check reads null as the branch's strictest lock, which is the
    /// safe end to be at for something that only ever writes its own logs.
    /// </summary>
    public int? RoleId => null;
}

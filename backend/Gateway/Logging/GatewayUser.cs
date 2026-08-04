using Shared.Kernel.Interfaces;

namespace Gateway.Logging;

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
}

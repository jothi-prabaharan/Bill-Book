namespace Shared.Kernel.Interfaces;

/// <summary>
/// Ambient accessor for the authenticated caller, resolved per request from the
/// JWT. Returns null ids for unauthenticated or system contexts (seeding,
/// background workers) — which is exactly why the audit columns are nullable.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    Guid? CustomerId { get; }

    Guid? OrgId { get; }

    /// <summary>
    /// The single role the caller holds in this organization. Null on a token
    /// minted before the claim existed, and on system contexts — a period-lock
    /// check treats that as "apply the branch's strictest lock" rather than as
    /// no lock at all.
    /// </summary>
    int? RoleId { get; }
}

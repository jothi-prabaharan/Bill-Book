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
}

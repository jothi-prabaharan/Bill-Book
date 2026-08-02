namespace Identity.Api.Services;

/// <summary>
/// The seam to the Platform service. Identity must not reference Platform's
/// DbContext (cross-service boundary), so org → customer resolution and licence
/// status come through this abstraction, backed by a Platform API call.
///
/// This replaces the old AuthController.ResolveCustomerIdAsync that returned null.
/// </summary>
public interface IPlatformDirectory
{
    Task<OrgContext?> ResolveOrgAsync(Guid orgId, CancellationToken cancellationToken = default);
}

public sealed class OrgContext
{
    public required Guid OrgId { get; init; }

    public required Guid CustomerId { get; init; }

    public required string OrgName { get; init; }

    /// <summary>False until the customer's database has finished provisioning — login into it is blocked.</summary>
    public required bool DatabaseReady { get; init; }

    /// <summary>Active / Trial / Expired / Suspended.</summary>
    public required string LicenseStatus { get; init; }

    /// <summary>
    /// When access to this branch ends: the earlier of the customer's licence
    /// expiry and the branch's own. Platform resolves which one governs; this
    /// side only reports it.
    /// </summary>
    public DateOnly? LicenseExpiry { get; init; }

    /// <summary>
    /// True when it is the branch's date rather than the licence's. Carried into
    /// the token so a page can say "this branch has closed" instead of telling a
    /// customer with a valid licence to renew it.
    /// </summary>
    public bool ExpiryIsBranchLevel { get; init; }

    /// <summary>Licence seat cap — invites are refused at this number.</summary>
    public int MaxUsers { get; init; }
}

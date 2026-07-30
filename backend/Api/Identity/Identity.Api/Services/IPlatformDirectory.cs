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

    /// <summary>Active / Trial / Expired.</summary>
    public required string LicenseStatus { get; init; }

    public DateOnly? LicenseExpiry { get; init; }
}

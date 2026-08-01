using System.ComponentModel.DataAnnotations;

namespace Shared.Kernel.Internal;

/// <summary>
/// Asks a service to write its master data for a newly created organization.
///
/// Both ids are on the request rather than read from a JWT: the caller is
/// Platform's provisioning worker, which has no user token. CustomerId picks the
/// physical database and OrgId scopes the rows inside it, so the receiving
/// service sets its own tenant context from these before touching a DbContext.
///
/// Every implementation must be idempotent. Provisioning is retried by hand
/// after a failure, and seeding twice must be harmless.
/// </summary>
public sealed class SeedOrganizationRequest
{
    [Required(ErrorMessage = "Customer id is required.")]
    public Guid CustomerId { get; set; }

    [Required(ErrorMessage = "Organization id is required.")]
    public Guid OrgId { get; set; }
}

/// <summary>What a service wrote, so a failed fan-out can be told from a no-op.</summary>
public sealed class SeedOrganizationResponse
{
    /// <summary>Rows written per master, keyed by name. Empty when already seeded.</summary>
    public Dictionary<string, int> Seeded { get; set; } = [];
}

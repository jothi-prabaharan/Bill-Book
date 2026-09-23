namespace Shared.Kernel.Tenancy;

/// <summary>
/// The tenant a request belongs to, resolved once from the JWT and read by the
/// per-customer DbContext. Both ids are required for any per-customer query:
/// CustomerId picks the physical database, OrgId scopes the rows inside it.
/// </summary>
public interface ITenantContext
{
    Guid? CustomerId { get; }

    Guid? OrgId { get; }

    /// <summary>
    /// The customer's human-readable code, from the <c>customer_code</c> claim.
    ///
    /// Not an isolation key — <see cref="CustomerId"/> is — and never used in a
    /// query filter. It exists for places a person reads, where a GUID is
    /// useless: the first folder of every stored file is this code, so an
    /// operator browsing the storage account sees whose files they are.
    ///
    /// Null on a token minted before the claim existed, and on portal tokens.
    /// </summary>
    string? CustomerCode { get; }

    IReadOnlySet<string> Permissions { get; }

    /// <summary>Throws when either id is missing, so a query can never silently run unscoped.</summary>
    (Guid CustomerId, Guid OrgId) Require();
}

public sealed class TenantContext : ITenantContext
{
    public Guid? CustomerId { get; set; }

    public Guid? OrgId { get; set; }

    public string? CustomerCode { get; set; }

    public IReadOnlySet<string> Permissions { get; set; } = new HashSet<string>();

    public (Guid CustomerId, Guid OrgId) Require()
    {
        if (CustomerId is not Guid customerId || OrgId is not Guid orgId)
        {
            throw new InvalidOperationException(
                "No tenant context on this request. A per-customer query requires both " +
                "customer_id and org_id claims — check the request is authenticated.");
        }

        return (customerId, orgId);
    }
}

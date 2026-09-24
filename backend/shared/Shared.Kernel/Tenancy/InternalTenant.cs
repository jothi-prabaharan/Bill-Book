namespace Shared.Kernel.Tenancy;

/// <summary>What <see cref="InternalTenant.Apply"/> made of a request.</summary>
public enum InternalTenantOutcome
{
    /// <summary>The tenant is set, from the request or from the caller's token.</summary>
    Applied = 0,

    /// <summary>Neither the request nor a token named a branch.</summary>
    Missing = 1,

    /// <summary>The request named a different branch from the token it arrived with.</summary>
    Mismatch = 2,
}

/// <summary>
/// Sets the tenant on an internal route that may be called either way: with the
/// internal key alone, naming the branch in the request, or with the user's own
/// token forwarded beside the key.
///
/// <b>Call it before anything resolves a DbContext.</b> The context takes its
/// connection and its query filter from the tenant when it is built, so a
/// controller that injects one directly has bound it to no tenant before this
/// runs. Resolve the context from <see cref="IServiceProvider"/> afterwards.
///
/// <b>A token wins only by agreeing.</b> When both are present and name
/// different branches the request is refused rather than one quietly chosen:
/// a caller that says one branch while signed in to another has a bug, and
/// answering either would hide it.
/// </summary>
public static class InternalTenant
{
    public static InternalTenantOutcome Apply(TenantContext tenant, Guid customerId, Guid orgId)
    {
        bool named = customerId != Guid.Empty && orgId != Guid.Empty;
        bool fromToken = tenant.CustomerId is not null && tenant.OrgId is not null;

        if (!named)
        {
            return fromToken ? InternalTenantOutcome.Applied : InternalTenantOutcome.Missing;
        }

        if (fromToken && (tenant.CustomerId != customerId || tenant.OrgId != orgId))
        {
            return InternalTenantOutcome.Mismatch;
        }

        tenant.CustomerId = customerId;
        tenant.OrgId = orgId;
        return InternalTenantOutcome.Applied;
    }
}

using Master.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Approvals;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Master.Api.Controllers;

/// <summary>
/// Resolves approval chains for the services that store steps (D-26, TK-99).
/// Called with the internal key, the branch named in the body, and optionally
/// the user's token beside it, which must agree.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/approval-chains")]
public sealed class InternalApprovalChainsController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalApprovalChainsController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost("resolve")]
    public async Task<IActionResult> Resolve([FromBody] ResolveChainRequest request, CancellationToken ct)
    {
        if (InternalTenant.Apply(_tenant, request.CustomerId, request.OrgId) != InternalTenantOutcome.Applied)
        {
            return Forbid();
        }

        // Resolved after the tenant is set, so the context binds to this branch.
        var resolver = _services.GetRequiredService<ApprovalChainResolver>();
        return Ok(await resolver.ResolveAsync(request, ct));
    }

    /// <summary>Whether one user is standing in for another today (delegation).</summary>
    [HttpPost("delegate-check")]
    public async Task<IActionResult> DelegateCheck([FromBody] DelegateCheckRequest request, CancellationToken ct)
    {
        if (InternalTenant.Apply(_tenant, request.CustomerId, request.OrgId) != InternalTenantOutcome.Applied)
        {
            return Forbid();
        }

        var resolver = _services.GetRequiredService<ApprovalChainResolver>();
        return Ok(new { isDelegate = await resolver.IsDelegateAsync(request.ApproverUserId, request.ActorUserId, request.OnDate, ct) });
    }
}

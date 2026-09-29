using Accounting.Api.Services;
using Accounting.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Projects;
using Shared.Kernel.Tenancy;

namespace Accounting.Api.Controllers;

/// <summary>
/// Sales claims a posting invoice's project items here, and releases them on
/// its void (TK-106). A lost claim answers 409, so the request's transaction
/// takes back whatever part of it was written.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/projects/billing")]
public sealed class InternalProjectBillingController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalProjectBillingController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost("claim")]
    public async Task<IActionResult> Claim([FromBody] ClaimProjectItemsRequest request, CancellationToken ct)
    {
        if (InternalTenant.Apply(_tenant, request.CustomerId, request.OrgId) != InternalTenantOutcome.Applied)
        {
            return BadRequest(new ClaimProjectItemsResponse { Detail = "A customer and an organization are required." });
        }

        ClaimProjectItemsResponse answer = await _services.GetRequiredService<ProjectBillingRepository>()
            .ClaimAsync(request.InvoiceId, request.Items, ct);

        return answer.Claimed ? Ok(answer) : Conflict(answer);
    }

    [HttpPost("release")]
    public async Task<IActionResult> Release([FromBody] ReleaseProjectItemsRequest request, CancellationToken ct)
    {
        if (InternalTenant.Apply(_tenant, request.CustomerId, request.OrgId) != InternalTenantOutcome.Applied)
        {
            return BadRequest();
        }

        int released = await _services.GetRequiredService<ProjectBillingRepository>().ReleaseAsync(request.InvoiceId, ct);
        return Ok(new { released });
    }
}

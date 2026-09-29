using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Purchase.Repository.Clients;
using Purchase.Repository.Repositories;
using Shared.Kernel.Internal;
using Shared.Kernel.Projects;
using Shared.Kernel.Tenancy;

namespace Purchase.Api.Controllers;

/// <summary>
/// Sales claims a posting invoice's re-billed bill lines here, and releases
/// them on its void (TK-106). A lost claim answers 409, so the request's
/// transaction takes back whatever part of it was written.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/bills/billing")]
public sealed class InternalBillBillingController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalBillBillingController(TenantContext tenant, IServiceProvider services)
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

        ClaimProjectItemsResponse answer = await _services.GetRequiredService<BillBillingRepository>()
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

        return Ok(new { released = await _services.GetRequiredService<BillBillingRepository>().ReleaseAsync(request.InvoiceId, ct) });
    }
}

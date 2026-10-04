using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Purchase.Entity.Models;
using Purchase.Repository.Repositories;
using Shared.Kernel.Internal;
using Shared.Kernel.Persistence;
using Shared.Kernel.Tenancy;

namespace Purchase.Api.Controllers;

/// <summary>
/// Where the worker's <c>RecurringBillingWorker</c> raises a branch's due
/// recurring bills (TK-139). The tenant comes from the body, set before the
/// repository is resolved because its <c>DbContext</c> is built from the tenant.
///
/// <c>[NoTransaction]</c>: each occurrence is its own transaction, so one refused
/// bill does not roll back the others raised in the same call.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[NoTransaction]
[Route("internal/recurring-bills")]
public sealed class InternalRecurringBillsController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalRecurringBillsController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost("run")]
    public async Task<IActionResult> Run([FromBody] RunRecurringRequest request, CancellationToken ct)
    {
        if (request.CustomerId == Guid.Empty || request.OrgId == Guid.Empty)
        {
            return BadRequest(new MessageResponse { Message = "A customer and an organization are required." });
        }

        _tenant.CustomerId = request.CustomerId;
        _tenant.OrgId = request.OrgId;

        var schedules = _services.GetRequiredService<RecurringBillingRepository>();
        return Ok(await schedules.RunDueAsync(request.AsOf, ct));
    }
}

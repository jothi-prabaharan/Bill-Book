using Accounting.Api.Services;
using Accounting.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Accounting.Api.Controllers;

/// <summary>
/// Payment terms, for the services that raise documents against them.
///
/// <b>Why this exists beside the public controller.</b> A bill's due date is
/// derived from its payment term, and the rule for deriving it — net days
/// against day-of-month, and what to do when that day has passed — belongs to
/// Accounting, which owns the term. Purchase asking for the answer keeps one
/// implementation of that rule; Purchase reading <c>DueDays</c> and doing the
/// arithmetic itself would make two, and they would disagree the first time a
/// day-of-month term was used.
///
/// The public route is behind a user token, and the services calling this hold
/// the shared internal key instead — so the tenant comes from the query rather
/// than from claims, like every other internal door.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/payment-terms")]
public sealed class InternalPaymentTermsController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalPaymentTermsController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpGet("{paymentTermId:long}/due-date")]
    public async Task<IActionResult> DueDate(
        long paymentTermId,
        [FromQuery] Guid customerId,
        [FromQuery] Guid orgId,
        [FromQuery] DateOnly documentDate,
        CancellationToken ct)
    {
        if (customerId == Guid.Empty || orgId == Guid.Empty)
        {
            return BadRequest(new MessageResponse
            {
                Message = "A customer and an organization are required.",
            });
        }

        // Set before anything resolves a DbContext: the context is built from
        // the tenant, so resolving the service first would bind it to no tenant.
        _tenant.CustomerId = customerId;
        _tenant.OrgId = orgId;

        var terms = _services.GetRequiredService<PaymentTermService>();

        DueDateResult? result = await terms.ResolveDueDateAsync(paymentTermId, documentDate, ct);

        return result is null ? NotFound() : Ok(result);
    }
}

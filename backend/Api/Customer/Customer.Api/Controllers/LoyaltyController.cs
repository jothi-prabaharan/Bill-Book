using Customer.Entity.Models;
using Customer.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Customer.Api.Controllers;

/// <summary>
/// A contact's loyalty points (TK-166): the balance and history the restaurant
/// bill page shows. Under the <c>restaurant</c> module, because points are
/// earned on restaurant bills and read at the restaurant's till.
/// </summary>
[ApiController]
[Authorize]
[RequireModulePermission("restaurant")]
[RequireBusinessType(BusinessType.Restaurant)]
[Route("api/loyalty")]
public sealed class LoyaltyController : ControllerBase
{
    private readonly LoyaltyRepository _loyalty;

    public LoyaltyController(LoyaltyRepository loyalty) => _loyalty = loyalty;

    /// <summary>A contact who never earned has a zero balance and no history, not a 404.</summary>
    [HttpGet("contacts/{contactId:long}")]
    public async Task<IActionResult> ForContact(long contactId, CancellationToken ct) =>
        Ok(await _loyalty.GetAsync(contactId, ct));
}

/// <summary>
/// Sales recording what a posted bill earned, and taking it back on a void
/// (TK-166). The tenant comes from the body, set before the repository is
/// resolved. Both calls are safe to repeat.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/loyalty")]
public sealed class InternalLoyaltyController : ControllerBase
{
    private readonly Shared.Kernel.Tenancy.TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalLoyaltyController(Shared.Kernel.Tenancy.TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost("earn")]
    public async Task<IActionResult> Earn([FromBody] EarnLoyaltyRequest request, CancellationToken ct)
    {
        if (!SetTenant(request.CustomerId, request.OrgId))
        {
            return BadRequest(new { message = "A customer and an organization are required." });
        }

        return Ok(await _services.GetRequiredService<LoyaltyRepository>().EarnAsync(request, ct));
    }

    /// <summary>204 when the invoice had earned nothing to take back.</summary>
    [HttpPost("reverse")]
    public async Task<IActionResult> Reverse([FromBody] ReverseLoyaltyRequest request, CancellationToken ct)
    {
        if (!SetTenant(request.CustomerId, request.OrgId))
        {
            return BadRequest(new { message = "A customer and an organization are required." });
        }

        LoyaltyPostingResult? result = await _services.GetRequiredService<LoyaltyRepository>().ReverseAsync(request.InvoiceId, ct);
        return result is null ? NoContent() : Ok(result);
    }

    private bool SetTenant(Guid customerId, Guid orgId)
    {
        if (customerId == Guid.Empty || orgId == Guid.Empty)
        {
            return false;
        }

        _tenant.CustomerId = customerId;
        _tenant.OrgId = orgId;
        return true;
    }
}

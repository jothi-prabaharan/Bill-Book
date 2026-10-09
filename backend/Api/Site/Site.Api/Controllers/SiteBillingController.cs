using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;
using Shared.Kernel.Security;
using Site.Repository.Repositories;

namespace Site.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("site")]
[Route("api/site/billing")]
[RequireBusinessType(BusinessType.Service)]
[RequireAddOn(AddOn.SiteServices)]
public sealed class SiteBillingController : ControllerBase
{
    private readonly SiteBillingRepository _repo;

    public SiteBillingController(SiteBillingRepository repo)
    {
        _repo = repo;
    }

    [HttpGet("worksheet")]
    public async Task<IActionResult> GetWorksheet(
        [FromQuery] long siteId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] long? tradeId,
        CancellationToken ct)
    {
        if (siteId <= 0)
        {
            return BadRequest(new { Message = "Site ID is required." });
        }

        // Privacy rule: cost and margin fields visible only if user has site.cost.view
        bool canViewCost = User.Claims.Any(c => (c.Type == "permission" || c.Type == "permissions") && c.Value == "site.cost.view");

        try
        {
            var worksheet = await _repo.GetWorksheetAsync(siteId, fromDate, toDate, tradeId, canViewCost, ct);
            return Ok(worksheet);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    [HttpPost("create-bill")]
    public async Task<IActionResult> CreateBill([FromBody] CreateSiteBillRequest request, CancellationToken ct)
    {
        if (request is null || request.SiteId <= 0)
        {
            return BadRequest(new { Message = "Valid site and billing details are required." });
        }

        try
        {
            var result = await _repo.CreateAndPostBillAsync(request, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { Message = ex.Message });
        }
    }
}

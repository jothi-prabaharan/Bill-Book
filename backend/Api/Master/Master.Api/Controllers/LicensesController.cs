using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Master.Api.Services;
using Master.Repository.Repositories;
using Master.Repository.Clients;
using Master.Entity.Models;
using Shared.Kernel.Internal;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Apps;

namespace Master.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/customers/{customerId:guid}/licenses")]
[RequireBusinessType(AllowStandalone = true)]
public sealed class LicensesController : ControllerBase
{
    private readonly LicenseRepository _service;

    public LicensesController(LicenseRepository service)
    {
        _service = service;
    }

    [CustomerRouteMustMatchToken]
    [RequirePermission("settings.view")]
    [HttpGet]
    public async Task<IActionResult> Get(Guid customerId, CancellationToken ct)
    {
        // The customer's one licence (D-36).
        LicenseDto? dto = await _service.GetAsync(customerId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>
    /// Operator only. Renews the license for a customer and updates branch tracking.
    /// </summary>
    [RequirePermission("platform.edit")]
    [HttpPut("renew")]
    public async Task<IActionResult> Renew(
        Guid customerId, [FromBody] RenewLicenseRequest request, CancellationToken ct)
    {
        bool ok = await _service.RenewAsync(customerId, request.NewExpiryDate, ct);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>
    /// Upgrades a trial branch by clearing IsTrial and bumping the license entitlement.
    /// In a real system, this would be tied to a payment gateway webhook.
    /// </summary>
    [CustomerRouteMustMatchToken]
    [RequirePermission("settings.edit")]
    [HttpPost("branches/{orgId:guid}/upgrade")]
    public async Task<IActionResult> UpgradeBranch(Guid customerId, Guid orgId, CancellationToken ct)
    {
        bool ok = await _service.ClearBranchTrialAsync(customerId, orgId, ct);
        if (!ok)
        {
            return BadRequest(new MessageResponse { Message = "Branch is not on trial, or license not found." });
        }
        
        return Ok(new MessageResponse { Message = "Branch trial cleared and license extended." });
    }
}

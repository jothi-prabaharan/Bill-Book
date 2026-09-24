using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Master.Api.Services;
using Master.Entity.Models;
using Shared.Kernel.Internal;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Apps;

namespace Master.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/customers/{customerId:guid}/licenses")]
[RequireApp(App.All)]
public sealed class LicensesController : ControllerBase
{
    private readonly LicenseService _service;

    public LicensesController(LicenseService service)
    {
        _service = service;
    }

    [CustomerRouteMustMatchToken]
    [RequirePermission("settings.view")]
    [HttpGet]
    public async Task<IActionResult> Get(Guid customerId, [FromQuery] string? app, CancellationToken ct)
    {
        // The licence of the app named, or of the app the caller is signed in to (TK-43).
        App forApp = app is null ? CallerApp() : AuthService.AppOrDefault(app);
        LicenseDto? dto = await _service.GetAsync(customerId, ct, forApp);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Every licence the customer holds, one per app (TK-43).</summary>
    [CustomerRouteMustMatchToken]
    [RequirePermission("settings.view")]
    [HttpGet("apps")]
    public async Task<IActionResult> ListApps(Guid customerId, CancellationToken ct) =>
        Ok(await _service.ListAsync(customerId, ct));

    private App CallerApp()
    {
        App app = RequireAppAttribute.AppOf(User);
        return app == App.None ? App.RetailErp : app;
    }

    /// <summary>
    /// Operator only. Renews the license for a customer and updates branch tracking.
    /// </summary>
    [RequirePermission("platform.edit")]
    [HttpPut("renew")]
    public async Task<IActionResult> Renew(
        Guid customerId, [FromBody] RenewLicenseRequest request, CancellationToken ct)
    {
        bool ok = await _service.RenewAsync(customerId, request.NewExpiryDate, ct, AuthService.AppOrDefault(request.App));
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
        bool ok = await _service.ClearBranchTrialAsync(customerId, orgId, ct, CallerApp());
        if (!ok)
        {
            return BadRequest(new MessageResponse { Message = "Branch is not on trial, or license not found." });
        }
        
        return Ok(new MessageResponse { Message = "Branch trial cleared and license extended." });
    }
}

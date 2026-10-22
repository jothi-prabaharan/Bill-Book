using Master.Api.Services;
using Master.Entity.Models;
using Master.Repository.Clients;
using Master.Repository.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;

namespace Master.Api.Controllers;

/// <summary>
/// The signed-in customer's plan and asking for a higher one (D-68, D-70,
/// TK-209). Both read the customer and the branch from the token: there is no
/// id in the route to point at somebody else's.
/// </summary>
[ApiController]
[Authorize]
[Route("api/plan")]
[RequireBusinessType(AllowStandalone = true)]
public sealed class PlanController : ControllerBase
{
    private readonly PlanRepository _plans;
    private readonly PlanPriceRepository _prices;
    private readonly ICurrentUser _currentUser;

    public PlanController(PlanRepository plans, PlanPriceRepository prices, ICurrentUser currentUser)
    {
        _plans = plans;
        _prices = prices;
        _currentUser = currentUser;
    }

    /// <summary>The plan, its limits, and the user places the branch has taken.</summary>
    [RequirePermission("settings.view")]
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (_currentUser.CustomerId is not Guid customerId || _currentUser.OrgId is not Guid orgId)
        {
            return Forbid();
        }

        PlanSummaryResponse? summary = await _plans.GetSummaryAsync(customerId, orgId, ct);
        return summary is null ? NotFound() : Ok(summary);
    }

    /// <summary>
    /// What each paid plan costs the signed-in branch's trade for a quarter and for
    /// half a year (D-71). No cycles for a branch with no trade, whose price is agreed
    /// with the platform's team.
    /// </summary>
    [RequirePermission("settings.view")]
    [HttpGet("prices")]
    public async Task<IActionResult> Prices(CancellationToken ct)
    {
        if (_currentUser.OrgId is not Guid orgId)
        {
            return Forbid();
        }

        return Ok(await _prices.GetForBranchAsync(orgId, ct));
    }

    /// <summary>Asks for a higher plan. There is no payment here; an operator makes the change.</summary>
    [RequirePermission("settings.edit")]
    [HttpPost("upgrade-requests")]
    public async Task<IActionResult> RequestUpgrade([FromBody] UpgradeRequestInput input, CancellationToken ct)
    {
        if (_currentUser.CustomerId is not Guid customerId
            || _currentUser.OrgId is not Guid orgId
            || _currentUser.UserId is not Guid userId)
        {
            return Forbid();
        }

        UpgradeRequestResult result = await _plans.RequestUpgradeAsync(customerId, orgId, userId, input, ct);
        return result.Outcome switch
        {
            UpgradeRequestOutcome.Recorded => Ok(new MessageResponse
            {
                Message = "Your request has been sent. We will contact you to complete the upgrade.",
            }),
            UpgradeRequestOutcome.AlreadyRequested => Ok(new MessageResponse
            {
                Message = "You have already asked to upgrade. We will contact you to complete it.",
            }),
            UpgradeRequestOutcome.NotAnUpgrade => Conflict(new MessageResponse
            {
                Message = "Your plan cannot be moved to that one.",
            }),
            UpgradeRequestOutcome.NoLicence => NotFound(),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }
}

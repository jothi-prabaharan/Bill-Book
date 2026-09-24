using Master.Api.Services;
using Master.Entity.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;

namespace Master.Api.Controllers;

/// <summary>
/// Settings › Applications (H0.3, TK-44): the caller's customer's licence for
/// each of the four apps. The customer is the token's, never the URL's, so the
/// page needs no internal id.
/// </summary>
[ApiController]
[Authorize]
[RequireApp(App.All)]
[Route("api/applications")]
public sealed class ApplicationsController : ControllerBase
{
    private readonly LicenseService _licences;
    private readonly ApplicationService _applications;
    private readonly ICurrentUser _currentUser;

    public ApplicationsController(LicenseService licences, ApplicationService applications, ICurrentUser currentUser)
    {
        _licences = licences;
        _applications = applications;
        _currentUser = currentUser;
    }

    /// <summary>One row per app, licensed or not, in the order the apps are numbered.</summary>
    [HttpGet]
    [RequirePermission("settings.view")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (_currentUser.CustomerId is not Guid customerId)
        {
            return Forbid();
        }

        IReadOnlyList<LicenseDto> held = await _licences.ListAsync(customerId, ct);

        return Ok(new[] { App.RetailErp, App.School, App.Hrms, App.Payroll }
            .Select(app =>
            {
                LicenseDto? licence = held.FirstOrDefault(l => l.App == app.ToString());
                return new ApplicationRow
                {
                    App = app.ToString(),
                    Licensed = licence is not null,
                    LicenseType = licence?.LicenseType,
                    ExpiryDate = licence?.ExpiryDate,
                    IsActive = licence?.IsActive ?? false,
                };
            })
            .ToList());
    }

    /// <summary>
    /// Starts a 14-day trial of another app (TK-45): its licence, its Owner role
    /// for the caller in every branch, and its master data in every branch.
    /// </summary>
    [HttpPost("{app}/trial")]
    [RequirePermission("settings.edit")]
    public async Task<IActionResult> StartTrial(string app, CancellationToken ct)
    {
        if (_currentUser.CustomerId is not Guid customerId || _currentUser.UserId is not Guid userId)
        {
            return Forbid();
        }

        if (!AppRules.TryParseSingle(app, out App parsed))
        {
            return BadRequest(new MessageResponse { Message = "Choose one app: RetailErp, School, Hrms or Payroll." });
        }

        StartTrialResult result = await _applications.StartTrialAsync(customerId, userId, parsed, ct);

        return result.Outcome switch
        {
            StartTrialOutcome.Ok => Ok(new MessageResponse { Message = "Trial started." }),
            StartTrialOutcome.AlreadyLicensed => Conflict(new MessageResponse
            {
                Message = "This app is already licensed for your account.",
            }),
            StartTrialOutcome.NotFound => NotFound(),
            StartTrialOutcome.SeedFailed => StatusCode(StatusCodes.Status503ServiceUnavailable, new MessageResponse
            {
                Message = "The app could not be set up in every branch yet. Nothing was started; try again shortly.",
            }),
            _ => BadRequest(new MessageResponse { Message = "Choose one app: RetailErp, School, Hrms or Payroll." }),
        };
    }
}

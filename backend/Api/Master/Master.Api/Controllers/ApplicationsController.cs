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
    private readonly ICurrentUser _currentUser;

    public ApplicationsController(LicenseService licences, ICurrentUser currentUser)
    {
        _licences = licences;
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
}

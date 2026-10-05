using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reporting.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

/// <summary>
/// Restaurant › Reports (TK-150): the restaurant reports this caller may open.
/// Each one opens on the report screen and runs through <c>api/reports</c>,
/// which applies the same trade and switch checks.
/// </summary>
[ApiController]
[Authorize]
[Route("api/restaurant/reports")]
[RequireBusinessType(BusinessType.Restaurant)]
[RequireModulePermission("restaurant")]
public sealed class RestaurantReportsController : ControllerBase
{
    private readonly RestaurantReportRepository _reports;

    public RestaurantReportsController(RestaurantReportRepository reports) => _reports = reports;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _reports.ListAsync(
            User.FindAll("permission").Select(c => c.Value).ToHashSet(StringComparer.Ordinal),
            ScopeClaims.AddOnsOf(User),
            ct));
}

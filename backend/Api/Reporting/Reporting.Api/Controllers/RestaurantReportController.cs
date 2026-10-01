using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Reporting.Repository.Repositories;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Reporting.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequireBusinessType(BusinessType.Restaurant)]
[RequireModulePermission("reporting")]
public class RestaurantReportController : ControllerBase
{
    private readonly RestaurantReportRepository _repo;

    public RestaurantReportController(RestaurantReportRepository repo)
    {
        _repo = repo;
    }

    [HttpGet("day-end")]
    [PermissionAction("read")]
    public async Task<IActionResult> GetDayEndReport([FromQuery] DateOnly date)
    {
        var result = await _repo.GetDayEndReportAsync(date);
        return Ok(result);
    }
}

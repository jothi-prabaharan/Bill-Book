using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Performance.Api.Services;
using Performance.Entity.Models;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Performance.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("performance")]
[RequireApp(App.Hrms)]
[Route("api/prf/rating-scales")]
public sealed class RatingScalesController : ControllerBase
{
    private readonly PerformanceService _service;

    public RatingScalesController(PerformanceService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _service.GetRatingScalesAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var scale = await _service.GetRatingScaleByIdAsync(id, ct);
        return scale is null ? NotFound() : Ok(scale);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveRatingScaleRequest req, CancellationToken ct)
    {
        var scale = await _service.SaveRatingScaleAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = scale.RatingScaleId }, scale);
    }
}

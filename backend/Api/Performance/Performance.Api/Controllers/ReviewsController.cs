using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Performance.Api.Services;
using Performance.Entity.Enums;
using Performance.Entity.Models;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Performance.Api.Controllers;

[ApiController]
[Authorize]
[RequireModulePermission("performance")]
[RequireApp(App.Hrms)]
[Route("api/prf/reviews")]
public sealed class ReviewsController : ControllerBase
{
    private readonly PerformanceService _service;
    private readonly IEmployeeClient _hrm;
    private readonly ITenantContext _tenant;
    private readonly ICallerPermissions _caller;

    public ReviewsController(
        PerformanceService service,
        IEmployeeClient hrm,
        ITenantContext tenant,
        ICallerPermissions caller)
    {
        _service = service;
        _hrm = hrm;
        _tenant = tenant;
        _caller = caller;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] long? cycleId,
        [FromQuery] long? employeeId,
        [FromQuery] ReviewStatus? status,
        CancellationToken ct) =>
        Ok(await _service.GetReviewsAsync(cycleId, employeeId, status, ct));

    [HttpGet("{id:long}/self-evaluation")]
    public async Task<IActionResult> GetSelfEvaluation(long id, CancellationToken ct)
    {
        var se = await _service.GetSelfEvaluationAsync(id, ct);
        return se is null ? NotFound() : Ok(se);
    }

    [HttpPost("{id:long}/reopen")]
    public async Task<IActionResult> Reopen(long id, CancellationToken ct)
    {
        bool reopened = await _service.ReopenSelfEvaluationAsync(id, ct);
        return reopened ? NoContent() : NotFound();
    }

    [HttpGet("{id:long}/levels")]
    public async Task<IActionResult> GetLevels(long id, CancellationToken ct) =>
        Ok(await _service.GetLevelReviewsAsync(id, ct));

    [HttpPost("{id:long}/act")]
    public async Task<IActionResult> Act(long id, [FromBody] ActLevelReviewRequest req, CancellationToken ct)
    {
        long reviewerEmployeeId = 0;
        if (_caller.UserId.HasValue)
        {
            var emp = await _hrm.FindByUserIdAsync(_tenant.CustomerId ?? Guid.Empty, _tenant.OrgId ?? Guid.Empty, _caller.UserId.Value, ct);
            if (emp is not null) reviewerEmployeeId = emp.EmployeeId;
        }

        bool success = await _service.ActLevelReviewAsync(id, req, reviewerEmployeeId, ct);
        return success ? NoContent() : BadRequest();
    }

    [HttpPost("release")]
    public async Task<IActionResult> Release([FromQuery] long cycleId, [FromQuery] long? departmentId, CancellationToken ct)
    {
        int count = await _service.ReleaseReviewsAsync(cycleId, departmentId, ct);
        return Ok(new { releasedCount = count });
    }

    [HttpPost("{id:long}/acknowledge")]
    public async Task<IActionResult> Acknowledge(long id, [FromBody] AcknowledgeReviewRequest req, CancellationToken ct)
    {
        bool success = await _service.AcknowledgeReviewAsync(id, req, ct);
        return success ? NoContent() : NotFound();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Performance.Api.Services;
using Performance.Entity.Models;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;

namespace Performance.Api.Controllers;

[ApiController]
[Authorize]
[RequireApp(App.Hrms)]
[Route("api/me/appraisals")]
public sealed class MeAppraisalsController : ControllerBase
{
    private readonly PerformanceService _service;
    private readonly IEmployeeClient _hrm;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUser _user;

    public MeAppraisalsController(
        PerformanceService service,
        IEmployeeClient hrm,
        ITenantContext tenant,
        ICurrentUser user)
    {
        _service = service;
        _hrm = hrm;
        _tenant = tenant;
        _user = user;
    }

    private async Task<long?> ResolveEmployeeIdAsync(CancellationToken ct)
    {
        if (!_user.UserId.HasValue) return null;
        var emp = await _hrm.FindByUserIdAsync(_tenant.CustomerId ?? Guid.Empty, _tenant.OrgId ?? Guid.Empty, _user.UserId.Value, ct);
        return emp?.EmployeeId;
    }

    [HttpGet]
    public async Task<IActionResult> ListMyReviews(CancellationToken ct)
    {
        long? employeeId = await ResolveEmployeeIdAsync(ct);
        if (!employeeId.HasValue) return Ok(new List<PerformanceReviewView>());

        return Ok(await _service.GetReviewsAsync(null, employeeId.Value, null, ct));
    }

    [HttpGet("{reviewId:long}/self-evaluation")]
    public async Task<IActionResult> GetSelfEvaluation(long reviewId, CancellationToken ct)
    {
        long? employeeId = await ResolveEmployeeIdAsync(ct);
        if (!employeeId.HasValue) return Forbid();

        var se = await _service.GetSelfEvaluationAsync(reviewId, ct);
        return se is null ? NotFound() : Ok(se);
    }

    [HttpPost("{reviewId:long}/self-evaluation")]
    public async Task<IActionResult> SaveSelfEvaluation(long reviewId, [FromBody] SaveSelfEvaluationRequest req, CancellationToken ct)
    {
        long? employeeId = await ResolveEmployeeIdAsync(ct);
        if (!employeeId.HasValue) return Forbid();

        var se = await _service.SaveSelfEvaluationAsync(reviewId, req, ct);
        return Ok(se);
    }

    [HttpPost("{reviewId:long}/acknowledge")]
    public async Task<IActionResult> Acknowledge(long reviewId, [FromBody] AcknowledgeReviewRequest req, CancellationToken ct)
    {
        long? employeeId = await ResolveEmployeeIdAsync(ct);
        if (!employeeId.HasValue) return Forbid();

        bool ok = await _service.AcknowledgeReviewAsync(reviewId, req, ct);
        return ok ? NoContent() : NotFound();
    }
}

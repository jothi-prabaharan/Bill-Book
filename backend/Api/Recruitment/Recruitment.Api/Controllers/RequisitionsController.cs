using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recruitment.Api.Services;
using Recruitment.Entity.Models;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace Recruitment.Api.Controllers;

[ApiController]
[Authorize]
[RequireApp(App.Hrms)]
[RequireModulePermission("recruitment")]
[Route("api/rec/requisitions")]
public sealed class RequisitionsController : ControllerBase
{
    private readonly RecruitmentService _recruitment;

    public RequisitionsController(RecruitmentService recruitment) => _recruitment = recruitment;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await _recruitment.ListRequisitionsAsync(page, pageSize, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var req = await _recruitment.GetRequisitionByIdAsync(id, ct);
        return req is null ? NotFound() : Ok(req);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateJobRequisitionRequest request, CancellationToken ct)
    {
        var req = await _recruitment.CreateRequisitionAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = req.JobRequisitionId }, req);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateJobRequisitionRequest request, CancellationToken ct)
    {
        var req = await _recruitment.UpdateRequisitionAsync(id, request, ct);
        return req is null ? NotFound() : Ok(req);
    }

    [HttpPost("{id:long}/submit")]
    public async Task<IActionResult> Submit(long id, CancellationToken ct) =>
        await _recruitment.SubmitRequisitionAsync(id, ct) ? Ok(new { success = true }) : NotFound();

    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, CancellationToken ct) =>
        await _recruitment.ApproveRequisitionAsync(id, ct) ? Ok(new { success = true }) : NotFound();

    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, CancellationToken ct) =>
        await _recruitment.RejectRequisitionAsync(id, ct) ? Ok(new { success = true }) : NotFound();
}

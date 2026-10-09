using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Site.Entity.Enums;
using Site.Entity.TableEntities;
using Site.Repository.Repositories;

namespace Site.Api.Controllers;

public sealed class SaveAssignmentRequest
{
    public long SiteId { get; set; }
    public long LaborerId { get; set; }
    public long TradeId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal? CostRatePerDay { get; set; }
    public decimal? SellRatePerDay { get; set; }
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Active;
}

public sealed class SiteAssignmentDto
{
    public long SiteAssignmentId { get; set; }
    public long SiteId { get; set; }
    public long LaborerId { get; set; }
    public long TradeId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal? CostRatePerDay { get; set; }
    public decimal? SellRatePerDay { get; set; }
    public decimal ApprovedDays { get; set; }
    public decimal ApprovedOtHours { get; set; }
    public decimal BilledDays { get; set; }
    public decimal BilledOtHours { get; set; }
    public AssignmentStatus Status { get; set; }
}

[ApiController]
[Authorize]
[RequireModulePermission("site")]
[Route("api/site/assignments")]
[RequireBusinessType(BusinessType.Service)]
[RequireAddOn(AddOn.SiteServices)]
public sealed class SiteAssignmentsController : ControllerBase
{
    private readonly SiteRepository _repo;

    public SiteAssignmentsController(SiteRepository repo) => _repo = repo;

    private bool CanViewCost =>
        User.FindAll("permission").Any(c => string.Equals(c.Value, "site.cost.view", StringComparison.OrdinalIgnoreCase));

    private SiteAssignmentDto ToDto(SiteAssignment a) => new()
    {
        SiteAssignmentId = a.SiteAssignmentId,
        SiteId = a.SiteId,
        LaborerId = a.LaborerId,
        TradeId = a.TradeId,
        StartDate = a.StartDate,
        EndDate = a.EndDate,
        CostRatePerDay = CanViewCost ? a.CostRatePerDay : null,
        SellRatePerDay = a.SellRatePerDay,
        ApprovedDays = a.ApprovedDays,
        ApprovedOtHours = a.ApprovedOtHours,
        BilledDays = a.BilledDays,
        BilledOtHours = a.BilledOtHours,
        Status = a.Status,
    };

    [HttpGet]
    [RequirePermission("site.view")]
    public async Task<IActionResult> List(
        [FromQuery] long? siteId,
        [FromQuery] long? laborerId,
        CancellationToken ct)
    {
        var list = await _repo.ListAssignmentsAsync(siteId, laborerId, ct);
        return Ok(list.Select(ToDto));
    }

    [HttpGet("{assignmentId:long}")]
    [RequirePermission("site.view")]
    public async Task<IActionResult> Get(long assignmentId, CancellationToken ct)
    {
        SiteAssignment? assignment = await _repo.GetAssignmentAsync(assignmentId, ct);
        return assignment is null ? NotFound() : Ok(ToDto(assignment));
    }

    [HttpPost]
    [RequirePermission("site.edit")]
    public async Task<IActionResult> Create([FromBody] SaveAssignmentRequest req, CancellationToken ct)
    {
        if (req.SiteId <= 0 || req.LaborerId <= 0 || req.TradeId <= 0)
        {
            return BadRequest(new { Message = "SiteId, LaborerId, and TradeId are required." });
        }

        var assignment = new SiteAssignment
        {
            SiteId = req.SiteId,
            LaborerId = req.LaborerId,
            TradeId = req.TradeId,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            CostRatePerDay = req.CostRatePerDay,
            SellRatePerDay = req.SellRatePerDay,
            Status = req.Status,
        };

        await _repo.CreateAssignmentAsync(assignment, ct);
        return Ok(ToDto(assignment));
    }

    [HttpPut("{assignmentId:long}")]
    [RequirePermission("site.edit")]
    public async Task<IActionResult> Update(long assignmentId, [FromBody] SaveAssignmentRequest req, CancellationToken ct)
    {
        var assignment = new SiteAssignment
        {
            SiteAssignmentId = assignmentId,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            CostRatePerDay = req.CostRatePerDay,
            SellRatePerDay = req.SellRatePerDay,
            Status = req.Status,
        };

        var updated = await _repo.UpdateAssignmentAsync(assignment, ct);
        return updated is null ? NotFound() : Ok(ToDto(updated));
    }
}

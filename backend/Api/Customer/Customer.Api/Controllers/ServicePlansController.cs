using Customer.Entity.TableEntities;
using Customer.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;

namespace Customer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServicePlansController : ControllerBase
{
    private readonly CustomerDbContext _db;

    public ServicePlansController(CustomerDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ServicePlanStatus? status)
    {
        var query = _db.ServicePlans.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(s => s.Status == status.Value);
        }

        var plans = await query
            .OrderBy(s => s.NextDueDate)
            .ToListAsync();

        return Ok(plans);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(long id)
    {
        var plan = await _db.ServicePlans.FindAsync(id);
        if (plan == null) return NotFound();

        return Ok(plan);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ServicePlan request)
    {
        _db.ServicePlans.Add(request);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = request.ServicePlanId }, request);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, [FromBody] ServicePlan request)
    {
        var plan = await _db.ServicePlans.FindAsync(id);
        if (plan == null) return NotFound();

        plan.ContactId = request.ContactId;
        plan.ItemId = request.ItemId;
        plan.AssetId = request.AssetId;
        plan.Interval = request.Interval;
        plan.StartDate = request.StartDate;
        plan.EndDate = request.EndDate;
        plan.LeadDays = request.LeadDays;
        plan.NextDueDate = request.NextDueDate;
        plan.Status = request.Status;

        await _db.SaveChangesAsync();
        return Ok(plan);
    }
}

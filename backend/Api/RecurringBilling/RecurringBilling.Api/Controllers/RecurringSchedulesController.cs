using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecurringBilling.Entity.Enums;
using RecurringBilling.Entity.TableEntities;
using RecurringBilling.Repository;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace RecurringBilling.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequireAddOn(AddOn.RecurringBilling)]
public class RecurringSchedulesController : ControllerBase
{
    private readonly RecurringBillingDbContext _context;

    public RecurringSchedulesController(RecurringBillingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var schedules = await _context.RecurringSchedules
            .OrderByDescending(s => s.RecurringScheduleId)
            .Select(s => new
            {
                s.RecurringScheduleId,
                s.DocumentType,
                s.Interval,
                s.NextDueDate,
                s.Status
            })
            .ToListAsync();

        return Ok(schedules);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(long id)
    {
        var schedule = await _context.RecurringSchedules.FindAsync(id);
        if (schedule == null) return NotFound();
        return Ok(schedule);
    }

    [HttpPost]
    public async Task<IActionResult> Create(RecurringSchedule request)
    {
        request.Status = ScheduleStatus.Active;
        request.RunStatus = ScheduleRunStatus.Pending;
        
        _context.RecurringSchedules.Add(request);
        await _context.SaveChangesAsync();
        
        return CreatedAtAction(nameof(Get), new { id = request.RecurringScheduleId }, request);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(long id, RecurringSchedule request)
    {
        var schedule = await _context.RecurringSchedules.FindAsync(id);
        if (schedule == null) return NotFound();

        schedule.Interval = request.Interval;
        schedule.NextDueDate = request.NextDueDate;
        schedule.EndDate = request.EndDate;
        schedule.Status = request.Status;

        await _context.SaveChangesAsync();
        return NoContent();
    }
}

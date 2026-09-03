using Customer.Entity.TableEntities;
using Customer.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Customer;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;
using System.ComponentModel.DataAnnotations;

namespace Customer.Api.Controllers;

[ApiController]
[Authorize]
// "support", not "customer" — see LeadsController. A ticket is the support
// half of this service, and support.* is what the catalogue actually seeds.
[RequireModulePermission("support")]
[Route("api/tickets")]
public sealed class TicketsController : ControllerBase
{
    private readonly CustomerDbContext _db;
    private readonly ITenantContext _tenant;

    public TicketsController(CustomerDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var tickets = await _db.Tickets
            .OrderByDescending(t => t.TicketId)
            .Select(t => new
            {
                t.TicketId,
                t.OrgId,
                t.ContactId,
                t.Subject,
                t.Status,
                t.Priority,
                t.SlaDueAt,
                t.AssignedToUserId,
                t.ResolvedAt,
                t.ClosedAt
            })
            .ToListAsync(ct);

        return Ok(tickets);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.TicketId == id, ct);
            
        if (ticket == null) return NotFound();

        if (ticket.OrgId != _tenant.OrgId) return Forbid();

        return Ok(ticket);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveTicketRequest request, CancellationToken ct)
    {
        var ticket = new Ticket
        {
            ContactId = request.ContactId,
            Subject = request.Subject,
            Description = request.Description,
            Priority = request.Priority,
            Status = TicketStatus.Open,
            SlaDueAt = request.Priority switch
            {
                TicketPriority.Urgent => DateTimeOffset.UtcNow.AddHours(2),
                TicketPriority.High => DateTimeOffset.UtcNow.AddHours(8),
                TicketPriority.Medium => DateTimeOffset.UtcNow.AddDays(2),
                TicketPriority.Low => DateTimeOffset.UtcNow.AddDays(7),
                _ => DateTimeOffset.UtcNow.AddDays(2)
            }
        };

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = ticket.TicketId }, ticket);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveTicketRequest request, CancellationToken ct)
    {
        var ticket = await _db.Tickets.FindAsync(new object[] { id }, ct);
        if (ticket == null) return NotFound();

        if (ticket.OrgId != _tenant.OrgId) return Forbid();

        ticket.Subject = request.Subject;
        ticket.Description = request.Description;
        ticket.Priority = request.Priority;

        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpPut("{id:long}/status")]
    public async Task<IActionResult> UpdateStatus(long id, [FromBody] UpdateTicketStatusRequest request, CancellationToken ct)
    {
        var ticket = await _db.Tickets.FindAsync(new object[] { id }, ct);
        if (ticket == null) return NotFound();

        if (ticket.OrgId != _tenant.OrgId) return Forbid();

        ticket.Status = request.Status;
        if (request.Status == TicketStatus.Resolved) ticket.ResolvedAt = DateTimeOffset.UtcNow;
        if (request.Status == TicketStatus.Closed) ticket.ClosedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpPost("{id:long}/messages")]
    public async Task<IActionResult> AddMessage(long id, [FromBody] SaveTicketMessageRequest request, CancellationToken ct)
    {
        var ticket = await _db.Tickets.FindAsync(new object[] { id }, ct);
        if (ticket == null) return NotFound();

        if (ticket.OrgId != _tenant.OrgId) return Forbid();

        var message = new TicketMessage
        {
            TicketId = id,
            AuthorType = request.AuthorType,
            AuthorUserId = request.AuthorType == TicketAuthorType.User ? request.AuthorUserId : null,
            Body = request.Body
        };

        _db.TicketMessages.Add(message);
        await _db.SaveChangesAsync(ct);

        return Ok(message);
    }
}

public class SaveTicketRequest
{
    [Required(ErrorMessage = "ContactId is required.")]
    public long ContactId { get; set; }

    [Required(ErrorMessage = "Subject is required.")]
    [MaxLength(200, ErrorMessage = "Subject cannot exceed 200 characters.")]
    public string Subject { get; set; } = null!;

    public string? Description { get; set; }

    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
}

public class UpdateTicketStatusRequest
{
    [Required(ErrorMessage = "Status is required.")]
    public TicketStatus Status { get; set; }
}

public class SaveTicketMessageRequest
{
    public TicketAuthorType AuthorType { get; set; }

    public Guid? AuthorUserId { get; set; }

    [Required(ErrorMessage = "Body is required.")]
    public string Body { get; set; } = null!;
}

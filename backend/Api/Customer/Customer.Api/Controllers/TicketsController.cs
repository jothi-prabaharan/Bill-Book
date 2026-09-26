using Customer.Api.Services;
using Customer.Entity.TableEntities;
using Customer.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Customer;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;
using Shared.Kernel.Tenancy;
using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Apps;

namespace Customer.Api.Controllers;

[ApiController]
[Authorize]
// "support", not "customer" — see LeadsController. A ticket is the support
// half of this service, and support.* is what the catalogue actually seeds.
[RequireModulePermission("support")]
[Route("api/tickets")]
[RequireApp(App.RetailErp)]
public sealed class TicketsController : ControllerBase
{
    private readonly CustomerDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IContactsClient _contacts;
    private readonly SlaPolicyService _sla;
    private readonly TimeProvider _clock;

    public TicketsController(
        CustomerDbContext db,
        ITenantContext tenant,
        IContactsClient contacts,
        SlaPolicyService sla,
        TimeProvider clock)
    {
        _db = db;
        _tenant = tenant;
        _contacts = contacts;
        _sla = sla;
        _clock = clock;
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

    /// <summary>
    /// Raises a ticket against a contact.
    ///
    /// <b><c>ContactId</c> is checked against the caller's branch first.</b> The
    /// column holds a plain id into another service's database, so the number
    /// alone says nothing about whose books the contact belongs to — a ticket
    /// created against another branch's contact would leak that contact's
    /// existence and put a support thread on the wrong customer's account.
    /// Contacts is asked with the caller's own token forwarded, so the answer
    /// comes back through that service's query filter and RLS policy.
    ///
    /// <c>Forbid()</c> rather than <c>NotFound()</c>, per CLAUDE.md, and the same
    /// answer for "no such contact" as for "not yours" — distinguishing them is
    /// the information id-probing is after.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveTicketRequest request, CancellationToken ct)
    {
        if (!await _contacts.ExistsInCallerOrgAsync(request.ContactId, ct))
        {
            return Forbid();
        }

        var ticket = new Ticket
        {
            ContactId = request.ContactId,
            Subject = request.Subject,
            Description = request.Description,
            Priority = request.Priority,
            Status = TicketStatus.Open,

            // The branch's own SLA for the priority (D-18, TK-18), not a table
            // hard-coded here for every branch alike.
            SlaDueAt = await _sla.DueAtAsync(request.Priority, _clock.GetUtcNow(), ct),
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

    /// <summary>The ticket's whole thread, internal notes included — this is the staff side.</summary>
    [HttpGet("{id:long}/messages")]
    public async Task<IActionResult> Messages(long id, CancellationToken ct)
    {
        if (!await _db.Tickets.AnyAsync(t => t.TicketId == id, ct)) return NotFound();

        return Ok(await _db.TicketMessages
            .Where(m => m.TicketId == id)
            .OrderBy(m => m.TicketMessageId)
            .Select(m => new
            {
                id = m.TicketMessageId,
                ticketId = m.TicketId,
                body = m.Body,
                authorType = m.AuthorType.ToString(),
                authorUserId = m.AuthorUserId,
                isInternal = m.IsInternal,
                createdAt = m.CreatedAt,
            })
            .ToListAsync(ct));
    }

    /// <summary>
    /// A staff reply, or an internal note when <c>IsInternal</c> is set (TK-97).
    /// Always written as the signed-in user: the author used to be taken from
    /// the request, which recorded the screen's replies as the customer's.
    /// </summary>
    [HttpPost("{id:long}/messages")]
    public async Task<IActionResult> AddMessage(
        long id, [FromBody] SaveTicketMessageRequest request, [FromServices] ICurrentUser user, CancellationToken ct)
    {
        var ticket = await _db.Tickets.FindAsync(new object[] { id }, ct);
        if (ticket == null) return NotFound();

        if (ticket.OrgId != _tenant.OrgId) return Forbid();

        var message = new TicketMessage
        {
            TicketId = id,
            AuthorType = TicketAuthorType.User,
            AuthorUserId = user.UserId,
            Body = request.Body,
            IsInternal = request.IsInternal,
        };

        _db.TicketMessages.Add(message);
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            id = message.TicketMessageId,
            ticketId = message.TicketId,
            body = message.Body,
            authorType = message.AuthorType.ToString(),
            authorUserId = message.AuthorUserId,
            isInternal = message.IsInternal,
            createdAt = message.CreatedAt,
        });
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
    [Required(ErrorMessage = "Body is required.")]
    [MaxLength(4000, ErrorMessage = "A message cannot exceed 4000 characters.")]
    public string Body { get; set; } = null!;

    /// <summary>A note for colleagues only; the customer never sees it (TK-97).</summary>
    public bool IsInternal { get; set; }
}

using Customer.Entity.TableEntities;
using Customer.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Customer;

namespace Customer.Api.Services;

public sealed class PortalTicketItem
{
    public long TicketId { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset? RaisedAt { get; set; }

    public DateTimeOffset? LastMessageAt { get; set; }
}

public sealed class PortalTicketMessage
{
    public long MessageId { get; set; }

    /// <summary>"You" for the contact's own messages, "Support" for the business's.</summary>
    public string From { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DateTimeOffset? SentAt { get; set; }
}

public sealed class PortalTicketDetail
{
    public PortalTicketItem Ticket { get; set; } = new();

    public string? Description { get; set; }

    public List<PortalTicketMessage> Messages { get; set; } = [];
}

public enum PortalTicketOutcome
{
    Ok = 0,
    NotFound = 1,
    Closed = 2,
}

/// <summary>
/// A contact's support tickets in the client portal (TK-97, design "Client
/// portal" → Tickets). Only the token's contact's tickets, and never an
/// internal note: those are staff talking to staff. A ticket raised here gets
/// its SLA from the branch's policies like any other, at Medium priority.
/// </summary>
public sealed class PortalTicketService
{
    private readonly CustomerDbContext _db;
    private readonly SlaPolicyService _sla;
    private readonly TimeProvider _clock;

    public PortalTicketService(CustomerDbContext db, SlaPolicyService sla, TimeProvider clock)
    {
        _db = db;
        _sla = sla;
        _clock = clock;
    }

    public async Task<List<PortalTicketItem>> ListAsync(long contactId, CancellationToken ct) =>
        await _db.Tickets
            .AsNoTracking()
            .Where(t => t.ContactId == contactId)
            .OrderByDescending(t => t.TicketId)
            .Select(t => new PortalTicketItem
            {
                TicketId = t.TicketId,
                Subject = t.Subject,
                Status = t.Status.ToString(),
                RaisedAt = t.CreatedAt,
                LastMessageAt = t.Messages.Where(m => !m.IsInternal).Max(m => m.CreatedAt),
            })
            .ToListAsync(ct);

    /// <summary>The ticket and its thread without internal notes, or null when it is not the contact's.</summary>
    public async Task<PortalTicketDetail?> GetAsync(long contactId, long ticketId, CancellationToken ct)
    {
        Ticket? ticket = await _db.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TicketId == ticketId && t.ContactId == contactId, ct);

        if (ticket is null)
        {
            return null;
        }

        List<PortalTicketMessage> messages = await _db.TicketMessages
            .AsNoTracking()
            .Where(m => m.TicketId == ticketId && !m.IsInternal)
            .OrderBy(m => m.TicketMessageId)
            .Select(m => new PortalTicketMessage
            {
                MessageId = m.TicketMessageId,
                From = m.AuthorType == TicketAuthorType.Contact ? "You" : "Support",
                Body = m.Body,
                SentAt = m.CreatedAt,
            })
            .ToListAsync(ct);

        return new PortalTicketDetail
        {
            Ticket = new PortalTicketItem
            {
                TicketId = ticket.TicketId,
                Subject = ticket.Subject,
                Status = ticket.Status.ToString(),
                RaisedAt = ticket.CreatedAt,
                LastMessageAt = messages.Count == 0 ? null : messages[^1].SentAt,
            },
            Description = ticket.Description,
            Messages = messages,
        };
    }

    /// <summary>Raises a ticket for the contact, at Medium priority with the branch's SLA for it.</summary>
    public async Task<long> RaiseAsync(long contactId, string subject, string? description, CancellationToken ct)
    {
        var ticket = new Ticket
        {
            ContactId = contactId,
            Subject = subject.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Priority = TicketPriority.Medium,
            Status = TicketStatus.Open,
            SlaDueAt = await _sla.DueAtAsync(TicketPriority.Medium, _clock.GetUtcNow(), ct),
        };

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync(ct);
        return ticket.TicketId;
    }

    /// <summary>
    /// The contact's reply. A resolved ticket reopens, because the customer is
    /// saying it is not resolved; a closed one is refused — raise a new ticket.
    /// </summary>
    public async Task<PortalTicketOutcome> ReplyAsync(long contactId, long ticketId, string body, CancellationToken ct)
    {
        Ticket? ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.TicketId == ticketId && t.ContactId == contactId, ct);

        if (ticket is null)
        {
            return PortalTicketOutcome.NotFound;
        }

        if (ticket.Status == TicketStatus.Closed)
        {
            return PortalTicketOutcome.Closed;
        }

        if (ticket.Status == TicketStatus.Resolved)
        {
            ticket.Status = TicketStatus.Open;
            ticket.ResolvedAt = null;
        }

        _db.TicketMessages.Add(new TicketMessage
        {
            TicketId = ticketId,
            AuthorType = TicketAuthorType.Contact,
            Body = body.Trim(),
            IsInternal = false,
        });

        await _db.SaveChangesAsync(ct);
        return PortalTicketOutcome.Ok;
    }
}

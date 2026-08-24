using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Customer;
using Shared.Kernel.Tenancy;

namespace Customer.Entity.TableEntities;

public class Ticket : OrgScopedEntity
{
    public long TicketId { get; set; }

    public long ContactId { get; set; }

    [Required(ErrorMessage = "Subject is required.")]
    [MaxLength(200, ErrorMessage = "Subject cannot exceed 200 characters.")]
    public string Subject { get; set; } = null!;

    public string? Description { get; set; }

    public TicketStatus Status { get; set; } = TicketStatus.Open;

    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    public DateTimeOffset? SlaDueAt { get; set; }

    public Guid? AssignedToUserId { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
}

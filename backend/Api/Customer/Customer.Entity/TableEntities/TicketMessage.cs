using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Customer;
using Shared.Kernel.Tenancy;

namespace Customer.Entity.TableEntities;

public class TicketMessage : OrgScopedEntity
{
    public long TicketMessageId { get; set; }

    public long TicketId { get; set; }

    public TicketAuthorType AuthorType { get; set; } = TicketAuthorType.User;

    public Guid? AuthorUserId { get; set; }

    [Required(ErrorMessage = "Body is required.")]
    public string Body { get; set; } = null!;
}

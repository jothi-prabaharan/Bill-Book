using Shared.Kernel.Projects;
using Shared.Kernel.Tenancy;

namespace Sales.Entity.TableEntities;

/// <summary>
/// A project item an invoice bills (TK-106): an hour, a re-billed expense or
/// bill line, or a milestone. Saved with the draft and claimed from its owner
/// when the invoice posts; a void releases it. The item lives in Accounting or
/// Purchase, so <see cref="ItemId"/> is an unenforced id.
/// </summary>
public class InvoiceProjectItem : OrgScopedEntity
{
    public long InvoiceProjectItemId { get; set; }

    public long InvoiceId { get; set; }

    public ProjectItemKind Kind { get; set; }

    public long ItemId { get; set; }

    /// <summary>The project the item belongs to, for reading the invoice back.</summary>
    public long ProjectId { get; set; }
}

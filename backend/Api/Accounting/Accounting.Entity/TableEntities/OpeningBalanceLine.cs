using System.ComponentModel.DataAnnotations;
using Accounting.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>
/// One thing being brought across: an account balance, what a contact owed on a
/// particular invoice, what was owed to them on a bill, or stock on the shelf.
///
/// <b>A receivable line is one document, not one contact.</b> The plan says AR
/// and AP come across per contact rather than as a lump sum, and the reason is
/// aging — but per contact is only half of it. A contact owing ₹300,000 across
/// three invoices of thirty, ninety and a hundred and eighty days ages nothing
/// like one ₹300,000 balance, and an aging report is the first thing anybody
/// opens after a migration. So the line carries the original document's number
/// and date, and a contact has as many lines as they have unpaid documents.
///
/// <b>Item lines carry a quantity and a cost, and no amount at all.</b> Their
/// value is quantity × unit cost, and Inventory is what computes and posts it —
/// see <see cref="OpeningBalanceLineType.Item"/>. A value keyed here as well
/// would be a second figure free to disagree with the stock the item is actually
/// carrying.
/// </summary>
public class OpeningBalanceLine : OrgScopedEntity
{
    public long OpeningBalanceLineId { get; set; }

    public long OpeningBalanceId { get; set; }

    /// <summary>Position on the document. Unique within it, and what the screen orders by.</summary>
    public int LineNumber { get; set; }

    public OpeningBalanceLineType LineType { get; set; }

    /// <summary>
    /// The account, on a <see cref="OpeningBalanceLineType.GlAccount"/> line and
    /// only there. The other three kinds resolve their control account from the
    /// kind, because a receivable that could name any account is a receivable
    /// that can be filed under Sales Revenue.
    /// </summary>
    public long? AccountId { get; set; }

    /// <summary>Set on the two contact kinds. No FK — Contacts owns it, in another schema.</summary>
    public long? ContactId { get; set; }

    /// <summary>Set on an item line. No FK — Inventory owns it.</summary>
    public long? ItemId { get; set; }

    /// <summary>Stock on hand at go-live, in the item's own inventory unit.</summary>
    public decimal? Quantity { get; set; }

    /// <summary>
    /// What that stock cost. <b>This is what seeds the weighted average</b>, so
    /// it is the single most consequential figure on the screen: every cost of
    /// sale until the next receipt is computed from it.
    /// </summary>
    public decimal? UnitCost { get; set; }

    /// <summary>A debit <b>xor</b> a credit, never both, never negative.</summary>
    public decimal DebitAmount { get; set; }

    public decimal CreditAmount { get; set; }

    /// <summary>
    /// The old system's number for the invoice or bill this line is the unpaid
    /// remainder of. Not a link — that document does not exist in this product —
    /// but it is what someone matches against when the contact queries it.
    /// </summary>
    [MaxLength(50, ErrorMessage = "Document reference cannot exceed 50 characters.")]
    public string? DocumentReference { get; set; }

    /// <summary>When the original document was raised. What an aging report ages from.</summary>
    public DateOnly? DocumentDate { get; set; }

    public DateOnly? DueDate { get; set; }

    [MaxLength(300, ErrorMessage = "Line memo cannot exceed 300 characters.")]
    public string? LineMemo { get; set; }
}

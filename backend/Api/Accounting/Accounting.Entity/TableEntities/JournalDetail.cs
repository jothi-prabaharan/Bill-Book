using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>
/// One line of a manual journal. A debit <b>xor</b> a credit, never both, never
/// negative — a signed amount would let a reversal be written as a negative
/// debit, and then no column could be summed without knowing which convention
/// its writer used.
///
/// <b>Carries OrgId, unlike the SPEC entry for this table.</b> SPEC says the
/// line is scoped through its parent journal. Every other detail table in the
/// product carries its own — contact addresses, item profiles — and CLAUDE.md
/// requires OrgId plus a query filter on every per-customer table without
/// exception. Scoping through the parent means no EF query filter at all and an
/// RLS policy that has to subquery the header, which is strictly weaker than the
/// two lines every other table gets for free.
/// </summary>
public class JournalDetail : OrgScopedEntity
{
    public long JournalDetailId { get; set; }

    public long JournalId { get; set; }

    /// <summary>Position on the entry. Unique within the journal, and what the screen orders by.</summary>
    public int LineNumber { get; set; }

    public long AccountId { get; set; }

    /// <summary>
    /// The sub-dimension beneath a control account: which contact, which item,
    /// which tax rate. Null for lines that have none — a bank line and an equity
    /// line are the account itself and nothing finer.
    /// </summary>
    public long? SubAccountId { get; set; }

    /// <summary>Transaction currency.</summary>
    public decimal DebitAmount { get; set; }

    /// <summary>Transaction currency.</summary>
    public decimal CreditAmount { get; set; }

    /// <summary>Base currency. What the balance trigger checks and what reports sum.</summary>
    public decimal DebitAmountBase { get; set; }

    /// <summary>Base currency.</summary>
    public decimal CreditAmountBase { get; set; }

    [MaxLength(300, ErrorMessage = "Line memo cannot exceed 300 characters.")]
    public string? LineMemo { get; set; }

    /// <summary>
    /// Set on the <b>reversing</b> line: the original line this one offsets.
    ///
    /// The pairing is per line and not only per document, and that is the whole
    /// reason this column exists. A reversal that omitted a line would still
    /// balance and still look complete at the header — only the line pairs show
    /// that one original was left standing.
    /// </summary>
    public long? ReversesJournalDetailId { get; set; }

    /// <summary>Set on the <b>reversed</b> line: the line that offset it.</summary>
    public long? ReversedByJournalDetailId { get; set; }
}

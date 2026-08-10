using System.ComponentModel.DataAnnotations;
using Accounting.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>
/// Where a branch's books begin: everything it owned, owed and was owed at the
/// moment it went live, brought across from whatever it kept the accounts in
/// before.
///
/// <b>One per branch, ever.</b> A branch has one moment it started; a second
/// opening balance is a second starting position, and every balance in the
/// product is measured from the first. The unique index on <c>OrgId</c> is what
/// says so — and it is why this document has no void and no reversal. An error
/// found after go-live is corrected the way any error in a closed period is,
/// with a journal entry that leaves a trail.
///
/// <b>Draft is where the work happens.</b> A migration takes weeks: balances
/// arrive from an accountant, stock from a physical count, receivables from a
/// pile of unpaid invoices. So a draft may be as unbalanced as it likes, holds no
/// number and touches no ledger. Finalize is the single act that posts it,
/// numbers it and freezes it.
///
/// <b>Opening Balance Equity is the plug, and it netting to zero is the whole
/// validation.</b> Every line posts against it, so the ledger balances at every
/// step and the accumulated OBE position is exactly what has not been accounted
/// for yet. When the last piece goes in — capital, retained earnings — it
/// reaches zero. A migration that finalizes with OBE non-zero has silently
/// invented equity out of a figure somebody forgot.
/// </summary>
public class OpeningBalance : OrgScopedEntity
{
    public long OpeningBalanceId { get; set; }

    /// <summary>
    /// From the OPB series, allocated at finalize rather than at draft — the
    /// same rule every other document follows, and here it barely matters
    /// because there is only ever one.
    /// </summary>
    [MaxLength(30, ErrorMessage = "Document number cannot exceed 30 characters.")]
    public string? TransactionNo { get; set; }

    /// <summary>
    /// Go-live. Every line posts on this date, and every balance in the branch is
    /// measured from it — so a transaction dated before it is trading that
    /// happened under the old system and is already inside these figures.
    /// </summary>
    public DateOnly AsOfDate { get; set; }

    /// <summary>
    /// The branch's own currency, resolved when the document is created. Opening
    /// balances are stated in it and nothing else: a figure carried across from
    /// an old system has no exchange rate to snapshot, because the conversion
    /// already happened in whatever produced it.
    /// </summary>
    [Required(ErrorMessage = "Currency code is required.")]
    [MaxLength(3, ErrorMessage = "Currency code must be a 3-letter code.")]
    public string CurrencyCode { get; set; } = null!;

    public string? Memo { get; set; }

    public OpeningBalanceStatus Status { get; set; } = OpeningBalanceStatus.Draft;

    public DateTimeOffset? FinalizedAt { get; set; }

    public Guid? FinalizedBy { get; set; }
}

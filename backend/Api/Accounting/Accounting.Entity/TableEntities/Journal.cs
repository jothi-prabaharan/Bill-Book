using System.ComponentModel.DataAnnotations;
using Accounting.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Accounting.Entity.TableEntities;

/// <summary>
/// A manual journal — the document a person writes by hand when no other
/// document fits: an accrual, a correction, a transfer between two accounts, an
/// asset built out of stock.
///
/// <b>Only manual journals live here.</b> An invoice, a bill or a payment posts
/// straight to <see cref="JournalLedger"/> under its own document type and id,
/// with no header in this table shadowing it. A second header per document is a
/// second thing to keep in step with the first, and the ledger rows already
/// carry the document type and the id.
///
/// <b>Reversal is paired, not deleted.</b> A posted journal is offset by a
/// reversing journal, and the two point at each other — here at the header
/// level, and in <see cref="JournalDetail"/> line by line, which is what tells a
/// partially reversed journal from a fully reversed one. Both balance, so
/// nothing else would catch the difference.
/// </summary>
public class Journal : OrgScopedEntity
{
    public long JournalId { get; set; }

    /// <summary>
    /// The document number, allocated from the JRN series <b>at post, not at
    /// draft</b> — so it is null while the journal is being written. A number
    /// taken when the form opens is a number lost every time someone changes
    /// their mind, and this series has to run without gaps.
    /// </summary>
    [MaxLength(30, ErrorMessage = "Journal number cannot exceed 30 characters.")]
    public string? JournalNo { get; set; }

    /// <summary>The date the entry posts on. What every report ages and periods on.</summary>
    public DateOnly JournalDate { get; set; }

    [Required(ErrorMessage = "Currency code is required.")]
    [MaxLength(3, ErrorMessage = "Currency code must be a 3-letter code.")]
    public string CurrencyCode { get; set; } = null!;

    /// <summary>
    /// Snapshot at <see cref="JournalDate"/>. <b>Never looked up live</b> — an
    /// entry opened a year later would silently reprice itself.
    /// </summary>
    public decimal ExchangeRate { get; set; } = 1m;

    /// <summary>The user's own reference — a voucher number, a bank advice.</summary>
    [MaxLength(200, ErrorMessage = "Reference cannot exceed 200 characters.")]
    public string? Reference { get; set; }

    /// <summary>Why the entry was made. Unbounded, because the reason is the point.</summary>
    public string? Memo { get; set; }

    /// <summary>
    /// From <c>mst.TransactionTypes</c>, stored as its code with no FK because
    /// the master lives in another database. <c>JRN</c> for everything written
    /// here today; the column exists because a future document that does want a
    /// header of its own has somewhere to say so.
    /// </summary>
    [Required(ErrorMessage = "Transaction type code is required.")]
    [MaxLength(3, ErrorMessage = "Transaction type code must be a 3-letter code.")]
    public string TransactionTypeCode { get; set; } = "JRN";

    /// <summary>The source document header, when one produced this. Polymorphic, no FK.</summary>
    public long? SourceId { get; set; }

    public JournalStatus Status { get; set; } = JournalStatus.Draft;

    public DateTimeOffset? PostedAt { get; set; }

    /// <summary>Who posted it. A plain Guid — users live in the master database.</summary>
    public Guid? PostedBy { get; set; }

    /// <summary>Set on the <b>reversing</b> journal: the entry it offsets.</summary>
    public long? ReversesJournalId { get; set; }

    /// <summary>Set on the <b>reversed</b> journal: the entry that offset it.</summary>
    public long? ReversedByJournalId { get; set; }
}

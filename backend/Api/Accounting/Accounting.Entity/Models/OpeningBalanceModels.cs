using System.ComponentModel.DataAnnotations;
using Accounting.Entity.Enums;

namespace Accounting.Entity.Models;

/// <summary>
/// The opening balance as the screen sends it: the whole document, header and
/// every line, in one request.
///
/// <b>Replaced wholesale on every save, not patched.</b> A migration is edited by
/// moving figures between lines, deleting the ones that turned out to be the same
/// invoice twice, and re-keying a stock count — reconciling that against line ids
/// would be guesswork, and the document is a draft with nothing pointing at it
/// until the day it is finalized.
/// </summary>
public class SaveOpeningBalanceRequest
{
    /// <summary>
    /// Go-live. Every line posts on it, and everything dated before it is trading
    /// that happened under the old system and is already inside these figures.
    /// </summary>
    public DateOnly AsOfDate { get; set; }

    public string? Memo { get; set; }

    public List<SaveOpeningBalanceLineRequest> Lines { get; set; } = [];
}

/// <summary>One balance being brought across.</summary>
public class SaveOpeningBalanceLineRequest
{
    public OpeningBalanceLineType LineType { get; set; }

    /// <summary>Required on a <see cref="OpeningBalanceLineType.GlAccount"/> line, refused on any other.</summary>
    public long? AccountId { get; set; }

    /// <summary>Required on the two contact kinds, refused on any other.</summary>
    public long? ContactId { get; set; }

    /// <summary>Required on an item line, refused on any other.</summary>
    public long? ItemId { get; set; }

    [Range(typeof(decimal), "0.000001", "999999999999.999999",
        ErrorMessage = "Quantity must be greater than zero.")]
    public decimal? Quantity { get; set; }

    /// <summary>What the stock cost. This is what seeds the weighted average.</summary>
    [Range(typeof(decimal), "0", "999999999999.999999",
        ErrorMessage = "Unit cost cannot be negative.")]
    public decimal? UnitCost { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335",
        ErrorMessage = "Debit amount cannot be negative.")]
    public decimal DebitAmount { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335",
        ErrorMessage = "Credit amount cannot be negative.")]
    public decimal CreditAmount { get; set; }

    [MaxLength(50, ErrorMessage = "Document reference cannot exceed 50 characters.")]
    public string? DocumentReference { get; set; }

    /// <summary>When the original invoice or bill was raised. What an aging report ages from.</summary>
    public DateOnly? DocumentDate { get; set; }

    public DateOnly? DueDate { get; set; }

    [MaxLength(300, ErrorMessage = "Line memo cannot exceed 300 characters.")]
    public string? LineMemo { get; set; }
}

public class OpeningBalanceView
{
    public long OpeningBalanceId { get; set; }

    public string? TransactionNo { get; set; }

    public DateOnly AsOfDate { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public string? Memo { get; set; }

    public string Status { get; set; } = null!;

    public DateTimeOffset? FinalizedAt { get; set; }

    public List<OpeningBalanceLineView> Lines { get; set; } = [];
}

public class OpeningBalanceLineView
{
    public long OpeningBalanceLineId { get; set; }

    public int LineNumber { get; set; }

    public string LineType { get; set; } = null!;

    public long? AccountId { get; set; }

    /// <summary>Filled for a GL line, and for the control account behind a contact line.</summary>
    public string? AccountCode { get; set; }

    public string? AccountName { get; set; }

    public long? ContactId { get; set; }

    public long? ItemId { get; set; }

    public decimal? Quantity { get; set; }

    public decimal? UnitCost { get; set; }

    public decimal DebitAmount { get; set; }

    public decimal CreditAmount { get; set; }

    public string? DocumentReference { get; set; }

    public DateOnly? DocumentDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public string? LineMemo { get; set; }
}

/// <summary>
/// Whether the document may be finalized, and if not, precisely what is stopping
/// it.
///
/// <b>Shown continuously, not only when Finalize is pressed.</b> A migration is
/// keyed over weeks; discovering at the end that the whole thing is out by ₹4,300
/// means auditing weeks of work instead of the line that was just typed.
/// </summary>
public class OpeningBalanceReadinessView
{
    public decimal TotalDebit { get; set; }

    public decimal TotalCredit { get; set; }

    /// <summary>
    /// What Opening Balance Equity would be left holding. <b>Zero is the whole
    /// validation</b> — every line posts against OBE, so this is exactly the part
    /// of the branch's position nobody has accounted for yet.
    /// </summary>
    public decimal OpeningBalanceEquity { get; set; }

    /// <summary>Stock value the item lines come to: quantity × unit cost, summed.</summary>
    public decimal InventoryValue { get; set; }

    public int ContactReceivableLines { get; set; }

    public int ContactPayableLines { get; set; }

    public int ItemLines { get; set; }

    /// <summary>Every reason finalize is refused. Empty means it may go ahead.</summary>
    public List<string> Blockers { get; set; } = [];

    public bool CanFinalize => Blockers.Count == 0;
}

/// <summary>Why an opening balance was refused. Every value is something a user can act on.</summary>
public enum OpeningBalanceOutcome
{
    Ok = 0,
    NotFound = 1,

    /// <summary>An edit, a delete or a finalize aimed at a document already finalized.</summary>
    AlreadyFinalized = 2,

    /// <summary>A second opening balance for a branch that already has one.</summary>
    AlreadyExists = 3,

    /// <summary>A line names the wrong thing for its kind — a receivable with no contact, say.</summary>
    LineShapeInvalid = 4,

    /// <summary>A GL line names an account this branch does not have, or one frozen for posting.</summary>
    AccountUnusable = 5,

    /// <summary>Finalize was refused. <c>Detail</c> says what did not tie.</summary>
    NotReady = 6,

    /// <summary>The chart of accounts is missing a control account, or a contact has no sub-account.</summary>
    SubLedgerMissing = 7,

    /// <summary>The ledger refused the posting, or could not be reached.</summary>
    PostingRefused = 8,

    /// <summary>Inventory would not record the opening stock. Transient — nothing was posted.</summary>
    InventoryUnavailable = 9,

    /// <summary>The branch's base currency could not be read. Transient.</summary>
    BaseCurrencyUnavailable = 10,
}

public sealed record OpeningBalanceResult(
    OpeningBalanceOutcome Outcome, long OpeningBalanceId = 0, string? Detail = null);

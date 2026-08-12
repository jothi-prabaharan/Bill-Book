using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Documents;

/// <summary>
/// What every sales and purchase document header carries, whichever of the nine
/// types it is.
///
/// <b>Inherited, not copied.</b> Nine tables share these columns, and nine copies
/// of them is eight that fall out of step the first time one is corrected. The
/// audit columns and <c>OrgId</c> arrive through <see cref="OrgScopedEntity"/>
/// above this, so nothing here repeats them either.
///
/// <b>Why a table per document type at all, when the columns are shared.</b>
/// Because the conversion links become real foreign keys — an invoice's
/// <c>SalesOrderId</c> points at a table that only holds sales orders — and
/// because the type-specific columns sit <c>NOT NULL</c> where they belong
/// rather than nullable everywhere. One discriminated table gets the columns for
/// free and gives up both.
///
/// See <c>SALES.md</c> §3 and <c>PURCHASE.md</c> for the column list this
/// implements and the decisions behind it.
/// </summary>
public abstract class DocumentHeaderBase : OrgScopedEntity
{
    /// <summary>
    /// From <c>mst.TransactionTypes</c>, stored as its code with no FK because
    /// the master lives in another database.
    ///
    /// Fixed per table — a row in <c>sal.Quotes</c> is always <c>QTE</c> — with
    /// one exception: <c>Invoices</c> holds <c>INV</c> or <c>POS</c>, because a
    /// POS sale is the same document rung up on a different screen. Two tables
    /// for one document would mean two places to fix a GST bug.
    /// </summary>
    [Required(ErrorMessage = "Transaction type code is required.")]
    [MaxLength(3, ErrorMessage = "Transaction type code must be a 3-letter code.")]
    public string TransactionTypeCode { get; set; } = null!;

    /// <summary>
    /// The document number, allocated <b>when the document is created</b>, not
    /// when it posts.
    ///
    /// A draft can be sent to a customer — that is what a quote is for — so it
    /// needs a number somebody can quote back. The consequence is carried
    /// deliberately and is the reason there is no delete anywhere in these
    /// tables: <b>no document row is ever removed</b>. Abandoning one voids it,
    /// so the number stays accounted for and the series stays explicable.
    /// </summary>
    [Required(ErrorMessage = "Document number is required.")]
    [MaxLength(30, ErrorMessage = "Document number cannot exceed 30 characters.")]
    public string DocumentNo { get; set; } = null!;

    /// <summary>
    /// The date the document is dated, and <b>the date every snapshot on it is
    /// taken at</b> — the exchange rate, the tax rates, the addresses. Never
    /// today's date at the time of reading.
    /// </summary>
    public DateOnly DocumentDate { get; set; }

    /// <summary>
    /// The customer or supplier. No FK — Contacts is another service — so this is
    /// an unenforced id validated in C#.
    ///
    /// <b>There is no <c>ContactName</c> beside it.</b> The name is read from
    /// Contacts when the document is read, so correcting a misspelling shows
    /// everywhere including on documents already raised. The cost is a batched
    /// cross-service lookup on every list screen, which is a real cost and is
    /// accepted rather than overlooked.
    /// </summary>
    [Range(1, long.MaxValue, ErrorMessage = "Choose the contact.")]
    public long ContactId { get; set; }

    /// <summary>
    /// The contact's GSTIN as it stood when the document was raised.
    ///
    /// <b>A snapshot, not a label.</b> A document filed under one registration
    /// cannot later claim another — that is a different return, and the figures
    /// have already gone to the first.
    /// </summary>
    [MaxLength(15, ErrorMessage = "GSTIN must be 15 characters.")]
    public string? ContactGstin { get; set; }

    /// <summary>Where the invoice goes. A snapshot; a contact who moves does not rewrite it.</summary>
    public string? BillingAddress { get; set; }

    /// <summary>Where the goods actually went, which is not always where the invoice goes.</summary>
    public string? ShippingAddress { get; set; }

    /// <summary>
    /// The state the supply is made in, from <c>mst.States</c>. A snapshot, and
    /// an unenforced id — the master is in another database.
    /// </summary>
    public int PlaceOfSupplyStateId { get; set; }

    /// <summary>
    /// Whether this is an inter-state supply.
    ///
    /// <b>Stored, never re-derived on read.</b> It decides whether the lines
    /// carry CGST + SGST or IGST, and it was decided against the branch's state
    /// and the place of supply <i>on the document date</i>. Recomputing it later
    /// against whatever those are now would silently restate a filed return.
    /// </summary>
    public bool IsInterState { get; set; }

    [Required(ErrorMessage = "Currency code is required.")]
    [MaxLength(3, ErrorMessage = "Currency code must be a 3-letter code.")]
    public string CurrencyCode { get; set; } = null!;

    /// <summary>
    /// Snapshot at <see cref="DocumentDate"/>. <b>Never looked up live</b> — the
    /// difference between this rate and a settlement's is exactly what realized
    /// exchange gain and loss is computed from, so a document that repriced
    /// itself would erase the gain it caused.
    /// </summary>
    public decimal ExchangeRate { get; set; } = 1m;

    // ---- Totals -----------------------------------------------------------
    // Sums of the lines and of the tax rows, written by the server. Stored
    // rather than computed on read because a document is a statement of figures
    // that were communicated: recomputing them under whatever the rounding rules
    // have since become would change what the customer was told.

    /// <summary>The lines before discount.</summary>
    public decimal SubTotal { get; set; }

    public decimal DiscountAmount { get; set; }

    /// <summary>What tax is charged on.</summary>
    public decimal TaxableAmount { get; set; }

    /// <summary>Sum of the <c>Cgst</c> tax rows beneath this document.</summary>
    public decimal CgstAmount { get; set; }

    public decimal SgstAmount { get; set; }

    public decimal IgstAmount { get; set; }

    public decimal CessAmount { get; set; }

    /// <summary>
    /// What rounding the total to the rupee moved. <b>Signed — the only amount
    /// on a document allowed to be negative</b>, because rounding runs both ways
    /// and it is a correction rather than an amount.
    /// </summary>
    public decimal RoundOffAmount { get; set; }

    /// <summary>What is payable, in <see cref="CurrencyCode"/>.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary><see cref="TotalAmount"/> at <see cref="ExchangeRate"/>, in the branch's base currency.</summary>
    public decimal TotalAmountBase { get; set; }

    // ---- Lifecycle --------------------------------------------------------

    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    /// <summary>
    /// Set if and only if the document ever reached the books. It is also what
    /// tells a void draft from a void posting, which is why there is no separate
    /// <c>Cancelled</c> status.
    /// </summary>
    public DateTimeOffset? PostedAt { get; set; }

    /// <summary>Who posted it. A plain Guid — users live in the master database.</summary>
    public Guid? PostedBy { get; set; }

    public DateTimeOffset? VoidedAt { get; set; }

    public Guid? VoidedBy { get; set; }

    /// <summary>
    /// Why it was withdrawn. <b>Required when voided</b>, and set in the same
    /// breath: a void with no reason is the row somebody has to reconstruct from
    /// memory a year later.
    /// </summary>
    [MaxLength(300, ErrorMessage = "Void reason cannot exceed 300 characters.")]
    public string? VoidReason { get; set; }

    /// <summary>Whatever the contact should read. Printed on the document.</summary>
    public string? Notes { get; set; }

    /// <summary>Printed beneath the totals.</summary>
    public string? TermsAndConditions { get; set; }
}

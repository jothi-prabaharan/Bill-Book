namespace Shared.Kernel.Numbering;

/// <summary>
/// What a series numbers. The distinction is not cosmetic: a document series
/// must run consecutively within its financial year because GST says so, which
/// is why manual override is refused on one and allowed on the other.
/// </summary>
public enum SeriesFor
{
    /// <summary>Master records — contacts, items, warehouses. A gap bothers nobody.</summary>
    Master = 0,

    /// <summary>
    /// Transactions — invoices, credit notes, receipts. <c>SeriesCode</c> is the
    /// three-letter <c>mst.TransactionTypes</c> code, so the document type and
    /// its series resolve by the same key.
    /// </summary>
    Document = 1,
}

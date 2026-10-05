using Microsoft.EntityFrameworkCore;
using Sales.Entity.Models;

namespace Sales.Repository.Repositories;

/// <summary>
/// Finds the sales document a printed number names (TK-162), for scan-to-open:
/// TK-157 prints each document's number as a barcode and a QR code, and reading
/// one back should open that document.
///
/// A number is unique within one document type in a branch, not across types,
/// so the types are asked in a fixed order and the first match wins — the
/// invoice first, as the document most often in a customer's hand. The query
/// filter keeps it to the caller's branch, so another branch's number finds
/// nothing.
/// </summary>
public sealed class DocumentNumberRepository
{
    /// <summary>The longest a document number can be (<c>DocumentHeaderBase.DocumentNo</c>).</summary>
    public const int MaxLength = 30;

    private readonly SalesDbContext _db;

    public DocumentNumberRepository(SalesDbContext db) => _db = db;

    public async Task<DocumentNumberMatch?> FindAsync(string? documentNo, CancellationToken ct)
    {
        string no = (documentNo ?? string.Empty).Trim();
        if (no.Length is 0 or > MaxLength)
        {
            return null;
        }

        long id = await _db.Invoices.AsNoTracking().Where(d => d.DocumentNo == no).Select(d => d.InvoiceId).FirstOrDefaultAsync(ct);
        if (id > 0)
        {
            return Match("Invoice", id);
        }

        id = await _db.CreditNotes.AsNoTracking().Where(d => d.DocumentNo == no).Select(d => d.CreditNoteId).FirstOrDefaultAsync(ct);
        if (id > 0)
        {
            return Match("CreditNote", id);
        }

        id = await _db.DeliveryChallans.AsNoTracking().Where(d => d.DocumentNo == no).Select(d => d.DeliveryChallanId).FirstOrDefaultAsync(ct);
        if (id > 0)
        {
            return Match("DeliveryChallan", id);
        }

        id = await _db.SalesOrders.AsNoTracking().Where(d => d.DocumentNo == no).Select(d => d.SalesOrderId).FirstOrDefaultAsync(ct);
        if (id > 0)
        {
            return Match("SalesOrder", id);
        }

        id = await _db.Quotes.AsNoTracking().Where(d => d.DocumentNo == no).Select(d => d.QuoteId).FirstOrDefaultAsync(ct);
        return id > 0 ? Match("Quote", id) : null;
    }

    private static DocumentNumberMatch Match(string type, long id) => new() { TransactionType = type, TransactionId = id };
}

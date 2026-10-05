using Microsoft.EntityFrameworkCore;
using Purchase.Entity.Models;

namespace Purchase.Repository.Repositories;

/// <summary>
/// Finds the purchase document a printed number names (TK-162), for
/// scan-to-open: TK-157 prints each document's number as a barcode and a QR
/// code, and reading one back should open that document.
///
/// A number is unique within one document type in a branch, not across types,
/// so the types are asked in a fixed order and the first match wins. The query
/// filter keeps it to the caller's branch.
/// </summary>
public sealed class DocumentNumberRepository
{
    /// <summary>The longest a document number can be (<c>DocumentHeaderBase.DocumentNo</c>).</summary>
    public const int MaxLength = 30;

    private readonly PurchaseDbContext _db;

    public DocumentNumberRepository(PurchaseDbContext db) => _db = db;

    public async Task<DocumentNumberMatch?> FindAsync(string? documentNo, CancellationToken ct)
    {
        string no = (documentNo ?? string.Empty).Trim();
        if (no.Length is 0 or > MaxLength)
        {
            return null;
        }

        long id = await _db.Bills.AsNoTracking().Where(d => d.DocumentNo == no).Select(d => d.BillId).FirstOrDefaultAsync(ct);
        if (id > 0)
        {
            return Match("Bill", id);
        }

        id = await _db.GoodsReceipts.AsNoTracking().Where(d => d.DocumentNo == no).Select(d => d.GoodsReceiptId).FirstOrDefaultAsync(ct);
        if (id > 0)
        {
            return Match("GoodsReceipt", id);
        }

        id = await _db.PurchaseOrders.AsNoTracking().Where(d => d.DocumentNo == no).Select(d => d.PurchaseOrderId).FirstOrDefaultAsync(ct);
        if (id > 0)
        {
            return Match("PurchaseOrder", id);
        }

        id = await _db.DebitNotes.AsNoTracking().Where(d => d.DocumentNo == no).Select(d => d.DebitNoteId).FirstOrDefaultAsync(ct);
        return id > 0 ? Match("DebitNote", id) : null;
    }

    private static DocumentNumberMatch Match(string type, long id) => new() { TransactionType = type, TransactionId = id };
}

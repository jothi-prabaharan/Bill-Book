using Microsoft.EntityFrameworkCore;
using Purchase.Entity.Enums;
using Purchase.Entity.TableEntities;
using Purchase.Repository.Clients;
using Shared.Kernel.CustomFields;
using Shared.Kernel.Documents;
using Shared.Kernel.Printing;
using Shared.Kernel.Tenancy;

namespace Purchase.Repository.Repositories;

/// <summary>
/// Prints a purchase order, goods receipt, bill or debit note through its template: reads the document from
/// Purchase's own tables, builds its payload, and has Printing lay it out.
///
/// Purchase never reads a template and Printing never reads a bill, the boundary the Printing design was
/// argued on. Each method answers null when the document is not in this branch, which the caller answers
/// as not found; row-level security means another branch's id reads as none rather than as forbidden.
/// </summary>
public sealed class PurchasePrintRepository
{
    private readonly PurchaseDbContext _db;
    private readonly IOrgIdentityProvider _orgIdentity;
    private readonly IContactNameLookup _contactNames;
    private readonly IItemNameLookup _itemNames;
    private readonly IPrintingClient _printing;
    private readonly ICustomFieldDefinitions? _customFields;

    public PurchasePrintRepository(
        PurchaseDbContext db,
        IOrgIdentityProvider orgIdentity,
        IContactNameLookup contactNames,
        IItemNameLookup itemNames,
        IPrintingClient printing,
        ICustomFieldDefinitions? customFields = null)
    {
        _db = db;
        _orgIdentity = orgIdentity;
        _contactNames = contactNames;
        _itemNames = itemNames;
        _printing = printing;
        _customFields = customFields;
    }

    public async Task<PrintedDocument?> PrintOrderAsync(long purchaseOrderId, CancellationToken ct)
    {
        PurchaseOrder? order = await _db.PurchaseOrders.AsNoTracking()
            .Include(o => o.Lines).ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(o => o.PurchaseOrderId == purchaseOrderId, ct);

        if (order is null)
        {
            return null;
        }

        return await RenderAsync(
            order,
            [.. order.Lines.Select(l => new PurchasePrintLine(l, [.. l.Taxes]))],
            "POR",
            CustomFieldEntityKind.PurchaseOrder,
            singles => singles["Document.DeliveryDate"] = order.ExpectedDate,
            ct);
    }

    public async Task<PrintedDocument?> PrintReceiptAsync(long goodsReceiptId, CancellationToken ct)
    {
        GoodsReceipt? receipt = await _db.GoodsReceipts.AsNoTracking()
            .Include(r => r.Lines).ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(r => r.GoodsReceiptId == goodsReceiptId, ct);

        if (receipt is null)
        {
            return null;
        }

        string? orderNo = receipt.PurchaseOrderId is long orderId
            ? await _db.PurchaseOrders.AsNoTracking().Where(o => o.PurchaseOrderId == orderId).Select(o => o.DocumentNo).FirstOrDefaultAsync(ct)
            : null;

        return await RenderAsync(
            receipt,
            [.. receipt.Lines.Select(l => new PurchasePrintLine(l, [.. l.Taxes], RejectionNote(l)))],
            "GRN",
            CustomFieldEntityKind.GoodsReceipt,
            singles =>
            {
                singles["Document.Reference"] = receipt.VendorDeliveryNoteNo;
                singles["Document.DeliveryDate"] = receipt.VendorDeliveryNoteDate;
                singles["Document.OriginalDocumentNo"] = orderNo;
            },
            ct);
    }

    public async Task<PrintedDocument?> PrintBillAsync(long billId, CancellationToken ct)
    {
        Bill? bill = await _db.Bills.AsNoTracking()
            .Include(b => b.Lines).ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(b => b.BillId == billId, ct);

        if (bill is null)
        {
            return null;
        }

        // What the bill was raised against: the receipt it bills, else the order.
        string? sourceNo = bill.GoodsReceiptId is long receiptId
            ? await _db.GoodsReceipts.AsNoTracking().Where(r => r.GoodsReceiptId == receiptId).Select(r => r.DocumentNo).FirstOrDefaultAsync(ct)
            : bill.PurchaseOrderId is long orderId
                ? await _db.PurchaseOrders.AsNoTracking().Where(o => o.PurchaseOrderId == orderId).Select(o => o.DocumentNo).FirstOrDefaultAsync(ct)
                : null;

        return await RenderAsync(
            bill,
            [.. bill.Lines.Select(l => new PurchasePrintLine(l, [.. l.Taxes]))],
            "BIL",
            CustomFieldEntityKind.Bill,
            singles =>
            {
                singles["Document.DueDate"] = bill.DueDate;
                singles["Document.Reference"] = bill.VendorBillNo;
                singles["Document.OriginalDocumentNo"] = sourceNo;
            },
            ct);
    }

    public async Task<PrintedDocument?> PrintDebitNoteAsync(long debitNoteId, CancellationToken ct)
    {
        DebitNote? note = await _db.DebitNotes.AsNoTracking()
            .Include(n => n.Lines).ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(n => n.DebitNoteId == debitNoteId, ct);

        if (note is null)
        {
            return null;
        }

        var bill = await _db.Bills.AsNoTracking()
            .Where(b => b.BillId == note.BillId)
            .Select(b => new { b.DocumentNo, b.VendorBillNo })
            .FirstOrDefaultAsync(ct);

        return await RenderAsync(
            note,
            [.. note.Lines.Select(l => new PurchasePrintLine(l, [.. l.Taxes]))],
            "DBN",
            CustomFieldEntityKind.DebitNote,
            singles =>
            {
                singles["Document.Reason"] = ReasonLabel(note.ReasonCode);
                singles["Document.OriginalDocumentNo"] = bill?.DocumentNo;
                singles["Document.Reference"] = bill?.VendorBillNo;
            },
            ct);
    }

    private async Task<PrintedDocument> RenderAsync(
        DocumentHeaderBase document,
        IReadOnlyList<PurchasePrintLine> lines,
        string fallbackTypeCode,
        CustomFieldEntityKind kind,
        Action<Dictionary<string, object?>> addTags,
        CancellationToken ct)
    {
        // The buyer's GSTIN is on every purchase document a vendor may be sent, so a branch that cannot be
        // identified stops the print rather than printing a placeholder name, as posting is stopped.
        OrgIdentity buyer = await _orgIdentity.GetIdentityAsync(ct)
            ?? throw new InvalidOperationException(
                "The branch issuing this document could not be identified, so it cannot be printed.");

        var contacts = await _contactNames.ResolveAsync([document.ContactId], ct);
        string vendorName = contacts.TryGetValue(document.ContactId, out var contact) ? contact.Name : string.Empty;

        long[] itemIds = [.. lines
            .Where(l => l.Line.ItemId.HasValue)
            .Select(l => l.Line.ItemId!.Value)
            .Distinct()];

        Dictionary<long, string> itemNames = (await _itemNames.ResolveAsync(itemIds, ct))
            .ToDictionary(pair => pair.Key, pair => pair.Value.Name);

        PrintPayload payload = PurchasePrintPayloadRepository.Build(document, lines, buyer, vendorName, itemNames);
        addTags(payload.Singles);

        // The branch's own fields marked to print, as {{custom.key}}.
        CustomFieldPrint.Add(payload, document.CustomFields,
            _customFields is null ? null : await _customFields.GetAsync(kind, ct));

        string typeCode = document.TransactionTypeCode?.Trim() is { Length: 3 } code && DocumentTypeCatalog.IsPrintable(code)
            ? code
            : fallbackTypeCode;

        return await _printing.RenderAsync(
            typeCode,
            document.PrintTemplateId,
            payload,
            PurchasePrintPayloadRepository.Watermark(document.Status),
            ct);
    }

    /// <summary>
    /// What a goods receipt says about a line that was not taken in whole, or null when all of it was.
    /// The line's quantity is what was delivered, so rate times quantity is the amount; this says how much
    /// of it was kept. Pure.
    /// </summary>
    public static string? RejectionNote(GoodsReceiptDetail line)
    {
        if (line.RejectedQuantity <= 0)
        {
            return null;
        }

        string note = $"Accepted {line.AcceptedQuantity:0.######}, rejected {line.RejectedQuantity:0.######}";
        return string.IsNullOrWhiteSpace(line.RejectionReason) ? note : $"{note} ({line.RejectionReason.Trim()})";
    }

    /// <summary>The reason a debit note was raised, as the vendor reads it. Pure.</summary>
    public static string ReasonLabel(DebitNoteReason reason) => reason switch
    {
        DebitNoteReason.PurchaseReturn => "Goods returned",
        DebitNoteReason.PriceCorrection => "Price correction",
        DebitNoteReason.PostPurchaseDiscount => "Post-purchase discount",
        DebitNoteReason.Deficiency => "Short delivery or damage",
        DebitNoteReason.Cancellation => "Bill cancelled",
        _ => reason.ToString(),
    };
}

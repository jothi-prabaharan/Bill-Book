using Microsoft.EntityFrameworkCore;
using Sales.Entity.Enums;
using Sales.Entity.TableEntities;
using Sales.Repository.Clients;
using Shared.Kernel.CustomFields;
using Shared.Kernel.Documents;
using Shared.Kernel.Printing;
using Shared.Kernel.Tenancy;

namespace Sales.Repository.Repositories;

/// <summary>
/// Lays out a quote, sales order, delivery challan or credit note through its print template: reads the
/// document from Sales' own tables, builds its payload, and has Printing render it. The invoice has its own
/// repository (<see cref="InvoicePrintRepository"/>) because it also carries the UPI QR and the e-way bill.
///
/// Sales never reads a template and Printing never reads a quote, the boundary the Printing design was argued
/// on. Each method answers null when the document is not in this branch, which the caller answers as not
/// found; row-level security means another branch's id reads as none rather than as forbidden.
/// </summary>
public sealed class SalesPrintRepository
{
    private readonly SalesDbContext _db;
    private readonly IOrgIdentityProvider _orgIdentity;
    private readonly IContactNameLookup _contactNames;
    private readonly IItemNameLookup _itemNames;
    private readonly IPrintingClient _printing;
    private readonly ICustomFieldDefinitions? _customFields;

    public SalesPrintRepository(
        SalesDbContext db,
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

    public async Task<PrintedDocument?> PrintQuoteAsync(long quoteId, CancellationToken ct)
    {
        Quote? quote = await _db.Quotes.AsNoTracking()
            .Include(q => q.Lines).ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(q => q.QuoteId == quoteId, ct);

        if (quote is null)
        {
            return null;
        }

        return await RenderAsync(
            quote,
            [.. quote.Lines.Select(l => new DocumentPrintLine(l, [.. l.Taxes]))],
            "QTE",
            CustomFieldEntityKind.Quote,
            _ => { },
            DocumentPrintPayload.Watermark(quote.Status),
            ct);
    }

    public async Task<PrintedDocument?> PrintOrderAsync(long salesOrderId, CancellationToken ct)
    {
        SalesOrder? order = await _db.SalesOrders.AsNoTracking()
            .Include(o => o.Lines).ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(o => o.SalesOrderId == salesOrderId, ct);

        if (order is null)
        {
            return null;
        }

        string? quoteNo = order.QuoteId is long quoteId
            ? await _db.Quotes.AsNoTracking().Where(q => q.QuoteId == quoteId).Select(q => q.DocumentNo).FirstOrDefaultAsync(ct)
            : null;

        return await RenderAsync(
            order,
            [.. order.Lines.Select(l => new DocumentPrintLine(l, [.. l.Taxes]))],
            "SOR",
            CustomFieldEntityKind.SalesOrder,
            payload =>
            {
                payload.Singles["Document.DeliveryDate"] = order.DeliveryDate;
                payload.Singles["Document.OriginalDocumentNo"] = quoteNo;
            },
            DocumentPrintPayload.Watermark(order.Status),
            ct);
    }

    public async Task<PrintedDocument?> PrintChallanAsync(long deliveryChallanId, CancellationToken ct)
    {
        DeliveryChallan? challan = await _db.DeliveryChallans.AsNoTracking()
            .Include(c => c.Lines).ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(c => c.DeliveryChallanId == deliveryChallanId, ct);

        if (challan is null)
        {
            return null;
        }

        string? orderNo = challan.SalesOrderId is long orderId
            ? await _db.SalesOrders.AsNoTracking().Where(o => o.SalesOrderId == orderId).Select(o => o.DocumentNo).FirstOrDefaultAsync(ct)
            : null;

        return await RenderAsync(
            challan,
            [.. challan.Lines.Select(l => new DocumentPrintLine(l, [.. l.Taxes]))],
            "DLC",
            CustomFieldEntityKind.DeliveryChallan,
            payload =>
            {
                payload.Singles["Document.DeliveryDate"] = challan.DispatchDate;
                payload.Singles["Document.VehicleNo"] = challan.VehicleNo;
                payload.Singles["Document.TransporterName"] = challan.TransporterName;
                payload.Singles["Document.EwayBillNo"] = challan.EwayBillNo;
                payload.Singles["Document.EwayBillDate"] = challan.EwayBillDate;
                payload.Singles["Document.OriginalDocumentNo"] = orderNo;
            },
            DocumentPrintPayload.Watermark(challan.Status),
            ct);
    }

    public async Task<PrintedDocument?> PrintCreditNoteAsync(long creditNoteId, CancellationToken ct)
    {
        CreditNote? note = await _db.CreditNotes.AsNoTracking()
            .Include(n => n.Lines).ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(n => n.CreditNoteId == creditNoteId, ct);

        if (note is null)
        {
            return null;
        }

        string? invoiceNo = await _db.Invoices.AsNoTracking()
            .Where(i => i.InvoiceId == note.InvoiceId)
            .Select(i => i.DocumentNo)
            .FirstOrDefaultAsync(ct);

        // The IRP's answer, when the credit note was registered (TK-92): Printing puts the IRN and the QR
        // code on it even where the template does not.
        EInvoice? eInvoice = await _db.EInvoices.AsNoTracking()
            .FirstOrDefaultAsync(e => e.SourceType == EInvoiceSource.CreditNote && e.SourceId == creditNoteId, ct);

        return await RenderAsync(
            note,
            [.. note.Lines.Select(l => new DocumentPrintLine(l, [.. l.Taxes]))],
            "CRN",
            CustomFieldEntityKind.CreditNote,
            payload =>
            {
                payload.Singles["Document.Reason"] = ReasonLabel(note.ReasonCode);
                payload.Singles["Document.OriginalDocumentNo"] = invoiceNo;
                InvoicePrintRepository.AddEInvoice(payload, eInvoice);
            },
            CreditNoteWatermark(note.Status, eInvoice?.Status),
            ct);
    }

    private async Task<PrintedDocument> RenderAsync(
        DocumentHeaderBase document,
        IReadOnlyList<DocumentPrintLine> lines,
        string fallbackTypeCode,
        CustomFieldEntityKind kind,
        Action<PrintPayload> addTags,
        string? watermark,
        CancellationToken ct)
    {
        // The issuer's GSTIN is on every sales document a customer may be handed, so a branch that cannot be
        // identified stops the print rather than printing a placeholder name, as posting is stopped.
        OrgIdentity seller = await _orgIdentity.GetIdentityAsync(ct)
            ?? throw new InvalidOperationException(
                "The branch issuing this document could not be identified, so it cannot be printed.");

        var contacts = await _contactNames.ResolveAsync([document.ContactId], ct);
        string customerName = contacts.TryGetValue(document.ContactId, out var contact) ? contact.Name : string.Empty;

        long[] itemIds = [.. lines
            .Where(l => l.Line.ItemId.HasValue)
            .Select(l => l.Line.ItemId!.Value)
            .Distinct()];

        Dictionary<long, string> itemNames = (await _itemNames.ResolveAsync(itemIds, ct))
            .ToDictionary(pair => pair.Key, pair => pair.Value.Name);

        PrintPayload payload = DocumentPrintPayload.Build(document, lines, seller, customerName, itemNames);

        // A sale's place of supply is the customer's state, from their GSTIN, else the branch's own, as on the
        // invoice: Sales stores no state id on a document.
        payload.Singles["Document.PlaceOfSupply"] =
            InvoicePrintPayloadRepository.PlaceOfSupply(document.ContactGstin, seller.StateCode);

        addTags(payload);

        // The branch's own fields marked to print, as {{custom.key}}.
        CustomFieldPrint.Add(payload, document.CustomFields,
            _customFields is null ? null : await _customFields.GetAsync(kind, ct));

        string typeCode = document.TransactionTypeCode?.Trim() is { Length: 3 } code && DocumentTypeCatalog.IsPrintable(code)
            ? code
            : fallbackTypeCode;

        PrintedDocument printed = await _printing.RenderAsync(typeCode, document.PrintTemplateId, payload, watermark, ct);

        // Which document this is, for the page that shows it (the view page's heading).
        printed.DocumentNo = document.DocumentNo;
        printed.ChosenPrintTemplateId = document.PrintTemplateId;
        printed.DocumentTypeCode = typeCode;
        printed.Status = document.Status.ToString();
        return printed;
    }

    /// <summary>
    /// A posted credit note that needs an IRN and has none yet prints stamped IRN PENDING, as the invoice
    /// does (TK-92): without its IRN it is not a valid credit note. Otherwise it is stamped by its status.
    /// </summary>
    public static string? CreditNoteWatermark(DocumentStatus status, EInvoiceStatus? eInvoice) =>
        status == DocumentStatus.Posted && eInvoice is EInvoiceStatus.Pending or EInvoiceStatus.Failed
            ? "IRN PENDING"
            : DocumentPrintPayload.Watermark(status);

    /// <summary>The reason a credit note was raised, as the customer reads it. Pure.</summary>
    public static string ReasonLabel(CreditNoteReason reason) => reason switch
    {
        CreditNoteReason.SalesReturn => "Goods returned",
        CreditNoteReason.PriceCorrection => "Price correction",
        CreditNoteReason.PostSaleDiscount => "Post-sale discount",
        CreditNoteReason.Deficiency => "Short delivery or damage",
        CreditNoteReason.Cancellation => "Invoice cancelled",
        _ => reason.ToString(),
    };
}

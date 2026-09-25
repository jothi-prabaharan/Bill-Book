using Microsoft.EntityFrameworkCore;
using Sales.Entity.Enums;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Printing;
using Shared.Kernel.Tenancy;

namespace Sales.Api.Services.Printing;

/// <summary>
/// Prints an invoice through its template: reads the invoice from Sales' own
/// tables, builds its payload, and has Printing lay it out.
///
/// Sales never reads a template and Printing never reads an invoice, which is
/// the boundary stage P was argued on.
/// </summary>
public sealed class InvoicePrintService
{
    private readonly SalesDbContext _db;
    private readonly IOrgIdentityProvider _orgIdentity;
    private readonly IContactNameLookup _contactNames;
    private readonly IItemNameLookup _itemNames;
    private readonly IPrintingClient _printing;

    public InvoicePrintService(
        SalesDbContext db,
        IOrgIdentityProvider orgIdentity,
        IContactNameLookup contactNames,
        IItemNameLookup itemNames,
        IPrintingClient printing)
    {
        _db = db;
        _orgIdentity = orgIdentity;
        _contactNames = contactNames;
        _itemNames = itemNames;
        _printing = printing;
    }

    /// <summary>Null when the invoice is not in this branch — which the caller answers as not found.</summary>
    public async Task<PrintedDocument?> PrintAsync(long invoiceId, CancellationToken ct)
    {
        Invoice? invoice = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Lines)
            .ThenInclude(l => l.Taxes)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, ct);

        if (invoice is null)
        {
            return null;
        }

        // A tax invoice names the supplier whose GSTIN the buyer claims credit
        // against, so a branch that cannot be identified stops the print rather
        // than printing a placeholder seller, exactly as it stops posting.
        OrgIdentity seller = await _orgIdentity.GetIdentityAsync(ct)
            ?? throw new InvalidOperationException(
                "The branch issuing this invoice could not be identified, so it cannot be printed.");

        var contacts = await _contactNames.ResolveAsync([invoice.ContactId], ct);
        string customerName = contacts.TryGetValue(invoice.ContactId, out var contact)
            ? contact.Name
            : string.Empty;

        long[] itemIds = [.. invoice.Lines
            .Where(l => l.ItemId.HasValue)
            .Select(l => l.ItemId!.Value)
            .Distinct()];

        Dictionary<long, string> itemNames = (await _itemNames.ResolveAsync(itemIds, ct))
            .ToDictionary(pair => pair.Key, pair => pair.Value.Name);

        PrintPayload payload = InvoicePrintPayload.Build(invoice, seller, customerName, itemNames);

        // The IRP's answer, when the invoice was registered (TK-92). Printing
        // puts the IRN and the QR code on it even where the template does not.
        EInvoice? eInvoice = await _db.EInvoices.AsNoTracking()
            .FirstOrDefaultAsync(e => e.SourceType == EInvoiceSource.Invoice && e.SourceId == invoiceId, ct);
        AddEInvoice(payload, eInvoice);
        AddEwayBill(payload, await _db.EwayBills.AsNoTracking()
            .Where(e => e.SourceType == EwayBillSource.Invoice && e.SourceId == invoiceId && e.Status == EwayBillStatus.Generated)
            .OrderByDescending(e => e.EwayBillId)
            .FirstOrDefaultAsync(ct));

        return await _printing.RenderAsync(
            invoice.TransactionTypeCode?.Trim() is { Length: 3 } code && DocumentTypeCatalog.IsPrintable(code)
                ? code
                : "INV",
            invoice.PrintTemplateId,
            payload,
            Watermark(invoice.Status, eInvoice?.Status),
            ct);
    }

    /// <summary>The IRN, acknowledgement and signed QR payload, on a registered e-invoice only.</summary>
    public static void AddEInvoice(PrintPayload payload, EInvoice? eInvoice)
    {
        if (eInvoice is not { Status: EInvoiceStatus.Registered })
        {
            return;
        }

        payload.Singles["EInvoice.Irn"] = eInvoice.Irn;
        payload.Singles["EInvoice.AckNo"] = eInvoice.AckNo;
        payload.Singles["EInvoice.AckDate"] = eInvoice.AckDate is DateTimeOffset ack
            ? DateOnly.FromDateTime(ack.ToOffset(TimeSpan.FromHours(5.5)).DateTime)
            : null;
        payload.Singles["EInvoice.QrImage"] = eInvoice.SignedQrCode;
    }

    /// <summary>The live e-way bill's number and dates, when the invoice has one (TK-93).</summary>
    public static void AddEwayBill(PrintPayload payload, EwayBill? eway)
    {
        if (eway?.EwbNo is not { Length: > 0 } number)
        {
            return;
        }

        static DateOnly? Day(DateTimeOffset? at) =>
            at is DateTimeOffset value ? DateOnly.FromDateTime(value.ToOffset(TimeSpan.FromHours(5.5)).DateTime) : null;

        payload.Singles["EInvoice.EwbNo"] = number;
        payload.Singles["EInvoice.EwbDate"] = Day(eway.EwbDate);
        payload.Singles["EInvoice.EwbValidUntil"] = Day(eway.ValidUntil);
    }

    /// <summary>
    /// A posted invoice that needs an IRN and has none yet prints stamped
    /// IRN PENDING (TK-92, design decision 4): without its IRN it is not a
    /// valid tax invoice, and a buyer must not be handed it as one.
    /// </summary>
    public static string? Watermark(DocumentStatus status, EInvoiceStatus? eInvoice) =>
        status == DocumentStatus.Posted && eInvoice is EInvoiceStatus.Pending or EInvoiceStatus.Failed
            ? "IRN PENDING"
            : Watermark(status);

    /// <summary>
    /// Only a posted invoice is a tax invoice. A draft still prints — somebody
    /// proofreads it before posting — but as a proforma, stamped so, because a
    /// draft handed to a customer as a tax invoice is one they may claim input
    /// credit on. A voided one says it is void.
    /// </summary>
    public static string? Watermark(DocumentStatus status) => status switch
    {
        DocumentStatus.Posted => null,
        DocumentStatus.Void => "VOID",
        _ => "PROFORMA",
    };
}

using Sales.Repository.Clients;
using Shared.Kernel.CustomFields;
using Microsoft.EntityFrameworkCore;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Payments;
using Shared.Kernel.Printing;
using Shared.Kernel.Tenancy;

namespace Sales.Repository.Repositories;

/// <summary>
/// Prints an invoice through its template: reads the invoice from Sales' own
/// tables, builds its payload, and has Printing lay it out.
///
/// Sales never reads a template and Printing never reads an invoice, which is
/// the boundary stage P was argued on.
/// </summary>
public sealed class InvoicePrintRepository
{
    private readonly SalesDbContext _db;
    private readonly IOrgIdentityProvider _orgIdentity;
    private readonly IContactNameLookup _contactNames;
    private readonly IItemNameLookup _itemNames;
    private readonly IPrintingClient _printing;

    private readonly ICustomFieldDefinitions? _customFields;
    private readonly IUpiIdProvider? _upi;
    private readonly ILedgerClient? _ledger;
    private readonly ITenantContext? _tenant;

    public InvoicePrintRepository(
        SalesDbContext db,
        IOrgIdentityProvider orgIdentity,
        IContactNameLookup contactNames,
        IItemNameLookup itemNames,
        IPrintingClient printing,
        ICustomFieldDefinitions? customFields = null,
        IUpiIdProvider? upi = null,
        ILedgerClient? ledger = null,
        ITenantContext? tenant = null)
    {
        _customFields = customFields;
        _upi = upi;
        _ledger = ledger;
        _tenant = tenant;
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

        PrintPayload payload = InvoicePrintPayloadRepository.Build(invoice, seller, customerName, itemNames);

        // The branch's own fields marked to print, as {{custom.key}} (TK-112).
        CustomFieldPrint.Add(payload, invoice.CustomFields,
            _customFields is null ? null : await _customFields.GetAsync(CustomFieldEntityKind.Invoice, ct));

        // The IRP's answer, when the invoice was registered (TK-92). Printing
        // puts the IRN and the QR code on it even where the template does not.
        EInvoice? eInvoice = await _db.EInvoices.AsNoTracking()
            .FirstOrDefaultAsync(e => e.SourceType == EInvoiceSource.Invoice && e.SourceId == invoiceId, ct);
        AddEInvoice(payload, eInvoice);
        AddEwayBill(payload, await _db.EwayBills.AsNoTracking()
            .Where(e => e.SourceType == EwayBillSource.Invoice && e.SourceId == invoiceId && e.Status == EwayBillStatus.Generated)
            .OrderByDescending(e => e.EwayBillId)
            .FirstOrDefaultAsync(ct));

        // A UPI QR for what is still owed (TK-158), when the branch has a UPI id.
        payload.Singles[UpiQrTag] = await UpiQrTextAsync(invoice, seller.Name, ct) ?? string.Empty;

        return await _printing.RenderAsync(
            invoice.TransactionTypeCode?.Trim() is { Length: 3 } code && DocumentTypeCatalog.IsPrintable(code)
                ? code
                : "INV",
            invoice.PrintTemplateId,
            payload,
            Watermark(invoice.Status, eInvoice?.Status),
            ct);
    }

    /// <summary>
    /// The UPI link a till receipt prints as a QR (TK-164): the same rule and
    /// the same amount as the printed invoice's (TK-158). Not found when the
    /// invoice is not in this branch; found with no link when nothing is owed,
    /// the branch has no UPI id, or the branch cannot be identified.
    /// </summary>
    public async Task<InvoicePaymentLink?> PaymentLinkAsync(long invoiceId, CancellationToken ct)
    {
        Invoice? invoice = await _db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, ct);
        if (invoice is null)
        {
            return null;
        }

        OrgIdentity? seller = await _orgIdentity.GetIdentityAsync(ct);
        return new InvoicePaymentLink
        {
            PayUri = seller is null ? null : await UpiQrTextAsync(invoice, seller.Name, ct),
        };
    }

    /// <summary>The placeholder the payment QR fills (TK-158).</summary>
    public const string UpiQrTag = PlaceholderCatalog.UpiQrTag;

    /// <summary>
    /// What a payment QR should ask for, or null for none. Pure. A draft or a
    /// document ready to post asks for its whole total (it prints PROFORMA); a
    /// posted invoice asks for what the ledger says is still outstanding, and
    /// nothing when that could not be read, because asking a customer who has
    /// paid to pay again is worse than printing no QR; a voided one asks for
    /// nothing.
    /// </summary>
    public static decimal? AmountDue(DocumentStatus status, decimal totalAmount, Settlement? settlement) => status switch
    {
        DocumentStatus.Draft or DocumentStatus.ReadyToPost => totalAmount,
        DocumentStatus.Posted => settlement?.OutstandingAmount,
        _ => null,
    };

    private async Task<string?> UpiQrTextAsync(Invoice invoice, string payeeName, CancellationToken ct)
    {
        if (_upi is null || await _upi.GetUpiIdAsync(ct) is not string upiId)
        {
            return null;
        }

        Settlement? settlement = null;
        if (invoice.Status == DocumentStatus.Posted
            && _ledger is not null
            && _tenant is { CustomerId: Guid customerId, OrgId: Guid orgId })
        {
            IReadOnlyDictionary<long, Settlement> answer = await _ledger.GetSettlementsAsync(new SettlementQueryRequest
            {
                CustomerId = customerId,
                OrgId = orgId,
                TransactionTypeCode = invoice.TransactionTypeCode?.Trim() is { Length: > 0 } code ? code : "INV",
                TransactionIds = [invoice.InvoiceId],
            }, ct);
            answer.TryGetValue(invoice.InvoiceId, out settlement);
        }

        return AmountDue(invoice.Status, invoice.TotalAmount, settlement) is decimal due
            ? UpiPayment.PayUri(upiId, payeeName, due, invoice.DocumentNo, invoice.CurrencyCode)
            : null;
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

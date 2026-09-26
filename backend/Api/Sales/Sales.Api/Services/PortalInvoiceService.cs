using Microsoft.EntityFrameworkCore;
using Sales.Api.Services.Pdf;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Sales.Api.Services;

/// <summary>One invoice as a contact sees it in the portal (TK-95).</summary>
public sealed class PortalInvoiceItem
{
    public long InvoiceId { get; set; }

    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }

    /// <summary>What is still owed, in base currency. Null when Accounting could not be asked.</summary>
    public decimal? OutstandingAmount { get; set; }

    /// <summary>Open, PartPaid, Paid, Overdue or Void.</summary>
    public string Status { get; set; } = string.Empty;
}

public sealed class PortalInvoiceDetail
{
    public PortalInvoiceItem Invoice { get; set; } = new();

    public decimal SubTotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal RoundOffAmount { get; set; }

    public List<PortalInvoiceLine> Lines { get; set; } = [];
}

public sealed class PortalInvoiceLine
{
    public int LineNumber { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? HsnSacCode { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal LineTotal { get; set; }
}

/// <summary>
/// A contact's own invoices, for the client portal (TK-95).
///
/// Only invoices that were posted — standing or since voided — and only the
/// contact's: a draft is not a bill yet, and another contact's invoice answers
/// exactly as an invoice that does not exist, so the portal never confirms that
/// a number belongs to someone else (design, decision 3). The contact is the
/// portal token's; the query filter and RLS keep it to the token's branch.
/// </summary>
public sealed class PortalInvoiceService
{
    private const decimal Tolerance = 0.01m;

    private readonly SalesDbContext _db;
    private readonly ILedgerClient _ledger;
    private readonly ITenantContext _tenant;
    private readonly IItemNameLookup _items;
    private readonly SalesDocumentArchive _archive;
    private readonly TimeProvider _clock;

    public PortalInvoiceService(
        SalesDbContext db,
        ILedgerClient ledger,
        ITenantContext tenant,
        IItemNameLookup items,
        SalesDocumentArchive archive,
        TimeProvider clock)
    {
        _db = db;
        _ledger = ledger;
        _tenant = tenant;
        _items = items;
        _archive = archive;
        _clock = clock;
    }

    public async Task<List<PortalInvoiceItem>> ListAsync(long contactId, CancellationToken ct)
    {
        List<Invoice> invoices = await Visible(contactId)
            .OrderByDescending(i => i.DocumentDate)
            .ThenByDescending(i => i.InvoiceId)
            .ToListAsync(ct);

        return await ItemsAsync(invoices, ct);
    }

    /// <summary>The invoice with its lines, or null when it is not one of the contact's posted invoices.</summary>
    public async Task<PortalInvoiceDetail?> GetAsync(long contactId, long invoiceId, CancellationToken ct)
    {
        Invoice? invoice = await Visible(contactId)
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, ct);

        if (invoice is null)
        {
            return null;
        }

        IReadOnlyDictionary<long, NamedRef> names = await _items.ResolveAsync(
            [.. invoice.Lines.Select(l => l.ItemId).OfType<long>().Distinct()], ct);

        return new PortalInvoiceDetail
        {
            Invoice = (await ItemsAsync([invoice], ct))[0],
            SubTotal = invoice.SubTotal,
            DiscountAmount = invoice.DiscountAmount,
            TaxAmount = invoice.CgstAmount + invoice.SgstAmount + invoice.IgstAmount + invoice.CessAmount,
            RoundOffAmount = invoice.RoundOffAmount,
            Lines = [.. invoice.Lines.OrderBy(l => l.LineNumber).Select(l => new PortalInvoiceLine
            {
                LineNumber = l.LineNumber,
                Description = l.Description
                    ?? (l.ItemId is long id && names.TryGetValue(id, out NamedRef? name) ? name.Name : string.Empty),
                HsnSacCode = l.HsnSacCode,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                TaxAmount = l.TaxAmount,
                LineTotal = l.LineTotal,
            })],
        };
    }

    /// <summary>The archived PDF, the same file staff download, or null when the invoice is not the contact's or has none.</summary>
    public async Task<ArchivedPdf?> PdfAsync(long contactId, long invoiceId, CancellationToken ct) =>
        await Visible(contactId).AnyAsync(i => i.InvoiceId == invoiceId, ct)
            ? await _archive.OpenAsync(ArchivedSalesDocument.Invoice, invoiceId, ct)
            : null;

    /// <summary>
    /// Posted, or voided after posting — never a draft — and the contact's own.
    /// </summary>
    private IQueryable<Invoice> Visible(long contactId) =>
        _db.Invoices.AsNoTracking().Where(i => i.ContactId == contactId && i.PostedAt != null
            && (i.Status == DocumentStatus.Posted || i.Status == DocumentStatus.Void));

    private async Task<List<PortalInvoiceItem>> ItemsAsync(List<Invoice> invoices, CancellationToken ct)
    {
        (Guid customerId, Guid orgId) = _tenant.Require();
        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

        // One call per document type: a till sale files under POS, an invoice under INV.
        Dictionary<(string Code, long Id), Settlement> settled = [];
        foreach (IGrouping<string, Invoice> byCode in invoices
            .Where(i => i.Status == DocumentStatus.Posted)
            .GroupBy(i => i.TransactionTypeCode))
        {
            IReadOnlyDictionary<long, Settlement> answer = await _ledger.GetSettlementsAsync(
                new SettlementQueryRequest
                {
                    CustomerId = customerId,
                    OrgId = orgId,
                    TransactionTypeCode = byCode.Key,
                    TransactionIds = [.. byCode.Select(i => i.InvoiceId)],
                },
                ct);

            foreach ((long id, Settlement settlement) in answer)
            {
                settled[(byCode.Key, id)] = settlement;
            }
        }

        return [.. invoices.Select(i =>
        {
            Settlement? settlement = settled.GetValueOrDefault((i.TransactionTypeCode, i.InvoiceId));
            return new PortalInvoiceItem
            {
                InvoiceId = i.InvoiceId,
                DocumentNo = i.DocumentNo,
                DocumentDate = i.DocumentDate,
                DueDate = i.DueDate,
                CurrencyCode = i.CurrencyCode,
                TotalAmount = i.TotalAmount,
                OutstandingAmount = i.Status == DocumentStatus.Void ? 0m : settlement?.OutstandingAmount,
                Status = StatusOf(i, settlement, today),
            };
        })];
    }

    /// <summary>
    /// Void; Paid when nothing is owed to within a paisa; Overdue when money is
    /// owed past the due date; PartPaid when some has come in; otherwise Open.
    /// Without Accounting's answer the invoice reads Open, or Overdue by its
    /// date, rather than claiming to be paid.
    /// </summary>
    public static string StatusOf(Invoice invoice, Settlement? settlement, DateOnly today)
    {
        if (invoice.Status == DocumentStatus.Void)
        {
            return "Void";
        }

        if (settlement is not null && Math.Abs(settlement.OutstandingAmount) <= Tolerance)
        {
            return "Paid";
        }

        if (invoice.DueDate is DateOnly due && due < today)
        {
            return "Overdue";
        }

        return settlement is not null && settlement.PaidAmount > Tolerance ? "PartPaid" : "Open";
    }
}

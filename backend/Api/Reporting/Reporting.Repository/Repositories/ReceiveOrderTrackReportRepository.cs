using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Receive Order Track Report (TK-242): each posted goods receipt dated in the period, newest first
/// and a page at a time, with what its accepted goods are worth, how much of that posted bills have
/// billed, what is still to bill, and its status: not billed, partly billed, billed, or nothing to bill
/// when every line was rejected. A draft received nothing, and a voided receipt was never posted, so
/// neither is listed.
///
/// <b>A receipt's value is what it posted</b> (<c>GoodsReceiptRepository.PostAsync</c>): each line's
/// taxable value over what was <i>delivered</i> times what was <i>accepted</i>, rounded to two places, taken
/// to base at the receipt's rate. <b>Billed is the share of it that posted bills name</b>: a bill's line
/// names the receipt line it bills, and Purchase refuses a bill line that is dearer or cheaper than the
/// receipt's cost, so a billed quantity at the receipt's cost is what the bill actually charged. Billed
/// value is therefore worked out the same way as value, from the billed quantity, and the value still to
/// bill is value less billed, so it adds up exactly on every receipt.
///
/// <b>The status is worked out in the database</b> from every line, as Purchase bills it: every line with
/// goods accepted billed in full makes it billed. So the tabs' counts, the totals and the page are each one
/// query over every receipt the filters match, and only the page's vendors and orders are read after it.
/// </summary>
public sealed class ReceiveOrderTrackReportRepository
{
    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public ReceiveOrderTrackReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>The refusals a request needs beyond both dates being given, with the field each belongs to. Pure.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(ReceiveOrderTrackReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from)
        {
            refusals.Add((nameof(ReceiveOrderTrackReportRequest.ToDate), "To date cannot be before the from date."));
        }

        if (request.Status is GoodsReceiptBillingStatus status && !Enum.IsDefined(status))
        {
            refusals.Add((nameof(ReceiveOrderTrackReportRequest.Status), "Status must be one of the options offered."));
        }

        return refusals;
    }

    /// <summary>Whether a vendor is on this branch; the query filter decides, so another branch's reads as none.</summary>
    public Task<bool> ContactExistsAsync(long contactId, CancellationToken ct) =>
        _db.Contacts.AnyAsync(c => c.ContactId == contactId, ct);

    /// <summary>The vendors the filter offers, by name: the Aged Payables list.</summary>
    public Task<List<AgedPayablesVendorChoiceDto>> VendorsAsync(CancellationToken ct) =>
        AgedPayablesSummaryReportRepository.VendorChoices(_db.Contacts).ToListAsync(ct);

    /// <summary>The report for a request the controller has already validated.</summary>
    public async Task<ReceiveOrderTrackReportResultDto> RunAsync(ReceiveOrderTrackReportRequest request, CancellationToken ct)
    {
        DateOnly start = request.FromDate!.Value;
        DateOnly end = request.ToDate!.Value;
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

        IQueryable<GoodsReceiptRead> receipts = Receipts(_db.GoodsReceipts, request, start, end);
        IQueryable<ReceiveOrderBilledSum> billedSums = BilledSums(_db.Bills, _db.BillDetails);
        IQueryable<ReceiveOrderLineFigure> figures = Figures(receipts, _db.GoodsReceiptDetails);
        IQueryable<ReceiveOrderBilledFigure> billedFigures = BilledFigures(receipts, _db.GoodsReceiptDetails, billedSums);
        IQueryable<ReceiveOrderTrackEntry> rows = Rows(receipts, figures, billedFigures);

        // The tabs count every status the other filters let through, so a tab never hides a receipt behind the one chosen.
        IQueryable<ReceiveOrderTrackEntry> counted = NotYetBilledOnly(rows, request.NotYetBilledOnly);
        List<ReceiveOrderStatusCount> counts = await counted
            .GroupBy(r => r.Status)
            .Select(g => new ReceiveOrderStatusCount { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        IQueryable<ReceiveOrderTrackEntry> matching = WithStatus(counted, request.Status);

        ReceiveOrderTrackEntry totals = await matching
            .GroupBy(r => 1)
            .Select(g => new ReceiveOrderTrackEntry { Value = g.Sum(r => r.Value), BilledValue = g.Sum(r => r.BilledValue) })
            .FirstOrDefaultAsync(ct) ?? new ReceiveOrderTrackEntry();

        int matchCount = await matching.CountAsync(ct);

        List<ReceiveOrderTrackEntry> page = await matching
            .OrderByDescending(r => r.DocumentDate)
            .ThenByDescending(r => r.GoodsReceiptId)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        long[] orderIds = [.. page.Where(r => r.PurchaseOrderId != null).Select(r => r.PurchaseOrderId!.Value).Distinct()];
        List<ReceiptOrderRef> orders = orderIds.Length == 0
            ? []
            : await _db.PurchaseOrders
                .Where(o => orderIds.Contains(o.PurchaseOrderId))
                .Select(o => new ReceiptOrderRef { PurchaseOrderId = o.PurchaseOrderId, DocumentNo = o.DocumentNo })
                .ToListAsync(ct);

        long[] contactIds = [.. page.Select(r => r.ContactId).Distinct()];
        List<VendorRef> vendors = contactIds.Length == 0
            ? []
            : await _db.Contacts
                .Where(c => contactIds.Contains(c.ContactId))
                .Select(c => new VendorRef { ContactId = c.ContactId, Code = c.ContactCode, Name = c.DisplayName, IsMsme = c.IsMsme })
                .ToListAsync(ct);

        return Compose(request, page, matchCount, orders, vendors, counts, totals, baseCurrency);
    }

    // --- builders: LINQ over the read models, so a test runs them over lists -------------

    /// <summary>The posted receipts dated in the period, narrowed by vendor. A draft received nothing, and a voided receipt was never posted. LINQ only.</summary>
    public static IQueryable<GoodsReceiptRead> Receipts(
        IQueryable<GoodsReceiptRead> receipts, ReceiveOrderTrackReportRequest request, DateOnly start, DateOnly end)
    {
        IQueryable<GoodsReceiptRead> matching = receipts.Where(r =>
            r.Status == DocumentStatus.Posted && r.DocumentDate >= start && r.DocumentDate <= end);

        return request.ContactId is long contact ? matching.Where(r => r.ContactId == contact) : matching;
    }

    /// <summary>
    /// How much each receipt line has been billed for: what the lines of posted bills naming it add up to.
    /// A draft or voided bill bills nothing. LINQ only.
    /// </summary>
    public static IQueryable<ReceiveOrderBilledSum> BilledSums(IQueryable<BillRead> bills, IQueryable<BillDetailRead> billDetails) =>
        (from bd in billDetails
         join b in bills on bd.BillId equals b.BillId
         where b.Status == DocumentStatus.Posted && bd.GoodsReceiptDetailId != null
         group bd by bd.GoodsReceiptDetailId!.Value into g
         select new ReceiveOrderBilledSum { GoodsReceiptDetailId = g.Key, Quantity = g.Sum(x => x.Quantity) });

    /// <summary>
    /// One row for each line of those receipts: what was accepted, and what the accepted goods are worth,
    /// the line's taxable value over what was delivered times what was accepted, rounded to two places, in
    /// the receipt's own currency and taken to base at its rate. LINQ only.
    /// </summary>
    public static IQueryable<ReceiveOrderLineFigure> Figures(IQueryable<GoodsReceiptRead> receipts, IQueryable<GoodsReceiptDetailRead> details) =>
        from d in details
        join r in receipts on d.GoodsReceiptId equals r.GoodsReceiptId
        select new ReceiveOrderLineFigure
        {
            GoodsReceiptId = r.GoodsReceiptId,
            Accepted = d.AcceptedQuantity,
            ValueInCurrency = d.Quantity == 0m ? 0m : Math.Round(d.TaxableAmount * d.AcceptedQuantity / d.Quantity, 2),
            Value = d.Quantity == 0m ? 0m : Math.Round(Math.Round(d.TaxableAmount * d.AcceptedQuantity / d.Quantity, 2) * r.ExchangeRate, 2),
        };

    /// <summary>
    /// One row for each billed line of those receipts, with how much of it is billed and the billed share
    /// of its value at the receipt's own cost, rounded the way the value is; and, so that every receipt has
    /// a row to join even when nothing is billed, one row of nothing for every line. The row of nothing
    /// carries a billed quantity no bill can have, so it never reads as billed. LINQ only.
    /// </summary>
    public static IQueryable<ReceiveOrderBilledFigure> BilledFigures(
        IQueryable<GoodsReceiptRead> receipts,
        IQueryable<GoodsReceiptDetailRead> details,
        IQueryable<ReceiveOrderBilledSum> billedSums)
    {
        IQueryable<ReceiveOrderBilledFigure> billed =
            from d in details
            join r in receipts on d.GoodsReceiptId equals r.GoodsReceiptId
            join s in billedSums on d.GoodsReceiptDetailId equals s.GoodsReceiptDetailId
            select new ReceiveOrderBilledFigure
            {
                GoodsReceiptId = r.GoodsReceiptId,
                Accepted = d.AcceptedQuantity,
                Billed = s.Quantity,
                BilledValue = d.Quantity == 0m ? 0m : Math.Round(Math.Round(d.TaxableAmount * s.Quantity / d.Quantity, 2) * r.ExchangeRate, 2),
            };

        IQueryable<ReceiveOrderBilledFigure> none =
            from d in details
            join r in receipts on d.GoodsReceiptId equals r.GoodsReceiptId
            select new ReceiveOrderBilledFigure { GoodsReceiptId = r.GoodsReceiptId, Accepted = d.AcceptedQuantity, Billed = -1m, BilledValue = 0m };

        return billed.Concat(none);
    }

    /// <summary>
    /// One row for each receipt with its lines' values added up and its status: nothing to bill when no
    /// line had goods accepted; else billed when every such line is billed in full; else not billed when
    /// nothing is billed on any line; else partly billed.
    /// </summary>
    public static IQueryable<ReceiveOrderTrackEntry> Rows(
        IQueryable<GoodsReceiptRead> receipts,
        IQueryable<ReceiveOrderLineFigure> figures,
        IQueryable<ReceiveOrderBilledFigure> billedFigures)
    {
        IQueryable<ReceiveOrderSum> sums = figures
            .GroupBy(f => f.GoodsReceiptId)
            .Select(g => new ReceiveOrderSum
            {
                GoodsReceiptId = g.Key,
                AcceptedLines = g.Count(f => f.Accepted > 0m),
                ValueInCurrency = g.Sum(f => f.ValueInCurrency),
                Value = g.Sum(f => f.Value),
            });

        IQueryable<ReceiveOrderBilledLines> billed = billedFigures
            .GroupBy(f => f.GoodsReceiptId)
            .Select(g => new ReceiveOrderBilledLines
            {
                GoodsReceiptId = g.Key,
                FullyBilledLines = g.Count(f => f.Accepted > 0m && f.Billed >= f.Accepted),
                BilledLines = g.Count(f => f.Billed > 0m),
                BilledValue = g.Sum(f => f.BilledValue),
            });

        return from r in receipts
               join s in sums on r.GoodsReceiptId equals s.GoodsReceiptId
               join b in billed on r.GoodsReceiptId equals b.GoodsReceiptId
               select new ReceiveOrderTrackEntry
               {
                   GoodsReceiptId = r.GoodsReceiptId,
                   DocumentNo = r.DocumentNo,
                   DocumentDate = r.DocumentDate,
                   ContactId = r.ContactId,
                   CurrencyCode = r.CurrencyCode,
                   PurchaseOrderId = r.PurchaseOrderId,
                   VendorDeliveryNoteNo = r.VendorDeliveryNoteNo,
                   ValueInCurrency = s.ValueInCurrency,
                   Value = s.Value,
                   BilledValue = b.BilledValue,
                   Status = s.AcceptedLines == 0
                       ? GoodsReceiptBillingStatus.NothingToBill
                       : b.FullyBilledLines == s.AcceptedLines
                           ? GoodsReceiptBillingStatus.Billed
                           : b.BilledLines == 0
                               ? GoodsReceiptBillingStatus.NotBilled
                               : GoodsReceiptBillingStatus.PartlyBilled,
               };
    }

    /// <summary>Only the receipts with something still to bill when asked: not billed, or partly billed. LINQ only.</summary>
    public static IQueryable<ReceiveOrderTrackEntry> NotYetBilledOnly(IQueryable<ReceiveOrderTrackEntry> rows, bool notYetBilledOnly) =>
        notYetBilledOnly
            ? rows.Where(r => r.Status == GoodsReceiptBillingStatus.NotBilled || r.Status == GoodsReceiptBillingStatus.PartlyBilled)
            : rows;

    /// <summary>The rows with the status asked for; with none asked for, every row. LINQ only.</summary>
    public static IQueryable<ReceiveOrderTrackEntry> WithStatus(IQueryable<ReceiveOrderTrackEntry> rows, GoodsReceiptBillingStatus? status) =>
        status is GoodsReceiptBillingStatus chosen ? rows.Where(r => r.Status == chosen) : rows;

    // --- compose: pure, so a test needs no database --------------------------------------

    /// <summary>
    /// The page's receipts with their vendors and orders, how many receipts have each status, and the
    /// totals. Every status has a count, a nought included. What is still to bill is value less billed, on
    /// each receipt and in total. Pure.
    /// </summary>
    public static ReceiveOrderTrackReportResultDto Compose(
        ReceiveOrderTrackReportRequest request,
        IReadOnlyList<ReceiveOrderTrackEntry> page,
        int totalReceipts,
        IReadOnlyList<ReceiptOrderRef> orders,
        IReadOnlyList<VendorRef> vendors,
        IReadOnlyList<ReceiveOrderStatusCount> counts,
        ReceiveOrderTrackEntry totals,
        string baseCurrency)
    {
        Dictionary<long, VendorRef> names = vendors.GroupBy(c => c.ContactId).ToDictionary(g => g.Key, g => g.First());
        Dictionary<long, string> orderNos = orders.GroupBy(o => o.PurchaseOrderId).ToDictionary(g => g.Key, g => g.First().DocumentNo);

        Dictionary<GoodsReceiptBillingStatus, int> statusCounts = Enum.GetValues<GoodsReceiptBillingStatus>().ToDictionary(s => s, _ => 0);
        foreach (ReceiveOrderStatusCount count in counts)
        {
            statusCounts[count.Status] = count.Count;
        }

        return new ReceiveOrderTrackReportResultDto
        {
            FromDate = request.FromDate!.Value,
            ToDate = request.ToDate!.Value,
            BaseCurrencyCode = baseCurrency,
            Receipts =
            [
                .. page.Select(r =>
                {
                    VendorRef? vendor = names.GetValueOrDefault(r.ContactId);
                    return new ReceiveOrderTrackReceiptDto
                    {
                        GoodsReceiptId = r.GoodsReceiptId,
                        DocumentNo = r.DocumentNo,
                        DocumentDate = r.DocumentDate,
                        ContactId = r.ContactId,
                        ContactCode = vendor?.Code ?? string.Empty,
                        ContactName = vendor?.Name ?? $"Vendor {r.ContactId}",
                        CurrencyCode = r.CurrencyCode,
                        PurchaseOrderId = r.PurchaseOrderId,
                        PurchaseOrderNo = r.PurchaseOrderId is long orderId ? orderNos.GetValueOrDefault(orderId) : null,
                        VendorDeliveryNoteNo = r.VendorDeliveryNoteNo,
                        Status = r.Status,
                        ValueInCurrency = r.ValueInCurrency,
                        Value = r.Value,
                        BilledValue = r.BilledValue,
                        ToBillValue = r.Value - r.BilledValue,
                    };
                }),
            ],
            TotalReceipts = totalReceipts,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            StatusCounts = statusCounts,
            Totals = new ReceiveOrderTrackTotalsDto
            {
                Value = totals.Value,
                BilledValue = totals.BilledValue,
                ToBillValue = totals.Value - totals.BilledValue,
            },
        };
    }
}

/// <summary>What posted bills have billed against one receipt line.</summary>
public sealed class ReceiveOrderBilledSum
{
    public long GoodsReceiptDetailId { get; set; }

    public decimal Quantity { get; set; }
}

/// <summary>A line of a receipt with what was accepted and what that is worth.</summary>
public sealed class ReceiveOrderLineFigure
{
    public long GoodsReceiptId { get; set; }

    public decimal Accepted { get; set; }

    public decimal ValueInCurrency { get; set; }

    public decimal Value { get; set; }
}

/// <summary>A billed line of a receipt: what was accepted, how much of it is billed and the billed share of its value in base currency.</summary>
public sealed class ReceiveOrderBilledFigure
{
    public long GoodsReceiptId { get; set; }

    public decimal Accepted { get; set; }

    public decimal Billed { get; set; }

    public decimal BilledValue { get; set; }
}

/// <summary>A receipt's lines counted and its values added up.</summary>
public sealed class ReceiveOrderSum
{
    public long GoodsReceiptId { get; set; }

    /// <summary>Lines with goods accepted.</summary>
    public int AcceptedLines { get; set; }

    public decimal ValueInCurrency { get; set; }

    public decimal Value { get; set; }
}

/// <summary>A receipt's billed lines counted and their values added up.</summary>
public sealed class ReceiveOrderBilledLines
{
    public long GoodsReceiptId { get; set; }

    /// <summary>Lines with goods accepted that are billed in full.</summary>
    public int FullyBilledLines { get; set; }

    /// <summary>Lines with something billed.</summary>
    public int BilledLines { get; set; }

    public decimal BilledValue { get; set; }
}

/// <summary>A receipt with its values and its status.</summary>
public sealed class ReceiveOrderTrackEntry
{
    public long GoodsReceiptId { get; set; }

    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    public long ContactId { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public long? PurchaseOrderId { get; set; }

    public string? VendorDeliveryNoteNo { get; set; }

    public decimal ValueInCurrency { get; set; }

    public decimal Value { get; set; }

    public decimal BilledValue { get; set; }

    public GoodsReceiptBillingStatus Status { get; set; }
}

/// <summary>How many receipts have a status.</summary>
public sealed class ReceiveOrderStatusCount
{
    public GoodsReceiptBillingStatus Status { get; set; }

    public int Count { get; set; }
}

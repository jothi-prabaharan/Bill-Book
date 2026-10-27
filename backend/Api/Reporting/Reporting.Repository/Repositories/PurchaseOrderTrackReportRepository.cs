using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Purchase Order Track Report (TK-240): each purchase order's progress, a page of orders at a
/// time, with their lines: what was ordered, received and billed of each, and what is still to receive
/// or to bill. An order is a posted purchase order dated in the period (a voided one is listed as
/// cancelled, and a draft is not an order yet).
///
/// <b>Received is the line's own running figure</b>, <c>ReceivedQuantity</c>, which a posted goods
/// receipt against the order advances by what it accepted. <b>Billed is added up from the bills</b>:
/// Purchase keeps no running billed quantity, so a posted bill's line counts against an order line
/// when it names that line, or names a receipt line that does. So ordered less received is what is
/// still to receive and ordered less billed what is still to bill, on every line. A value is the line's
/// net value before tax in base currency, shared out at the order's own price per unit; the outstanding
/// values are ordered less received or billed, so they add up exactly. What the bills' lines actually
/// came to is read apart (it can differ if the vendor's price differed) and shown beside them.
///
/// <b>The progress is worked out in the database</b>, from every line, as Purchase does: every line
/// received in full makes it received, every line billed in full billed. So the tabs' counts, the
/// totals and the page are each one query over every order the filters match, and only the lines of
/// the page's own orders are read after it.
/// </summary>
public sealed class PurchaseOrderTrackReportRepository
{
    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public PurchaseOrderTrackReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>The refusals a request needs beyond both dates being given, with the field each belongs to. Pure.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(PurchaseOrderTrackReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from)
        {
            refusals.Add((nameof(PurchaseOrderTrackReportRequest.ToDate), "To date cannot be before the from date."));
        }

        if (request.Progress is PurchaseOrderProgress progress && !Enum.IsDefined(progress))
        {
            refusals.Add((nameof(PurchaseOrderTrackReportRequest.Progress), "Progress must be one of the options offered."));
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
    public async Task<PurchaseOrderTrackReportResultDto> RunAsync(PurchaseOrderTrackReportRequest request, CancellationToken ct)
    {
        DateOnly start = request.FromDate!.Value;
        DateOnly end = request.ToDate!.Value;
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

        IQueryable<PurchaseOrderRead> orders = Orders(_db.PurchaseOrders, request, start, end);
        IQueryable<PurchaseBilledLine> billedLines = BilledLines(_db.Bills, _db.BillDetails, _db.GoodsReceiptDetails);
        IQueryable<PurchaseBilledSum> billedSums = BilledSums(billedLines);
        IQueryable<PurchaseOrderLineFigure> figures = Figures(orders, _db.PurchaseOrderDetails);
        IQueryable<PurchaseBilledFigure> billedFigures = BilledFigures(orders, _db.PurchaseOrderDetails, billedSums);
        IQueryable<PurchaseOrderTrackEntry> rows = Rows(orders, figures, billedFigures);

        // The tabs count every progress the other filters let through, so a tab never hides an order behind the one chosen.
        IQueryable<PurchaseOrderTrackEntry> counted = OutstandingOnly(rows, request.OutstandingOnly);
        List<PurchaseOrderProgressCount> counts = await counted
            .GroupBy(r => r.Progress)
            .Select(g => new PurchaseOrderProgressCount { Progress = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        IQueryable<PurchaseOrderTrackEntry> matching = WithProgress(counted, request.Progress);

        PurchaseOrderTrackEntry totals = await matching
            .GroupBy(r => 1)
            .Select(g => new PurchaseOrderTrackEntry
            {
                OrderedValue = g.Sum(r => r.OrderedValue),
                ReceivedValue = g.Sum(r => r.ReceivedValue),
                BilledValue = g.Sum(r => r.BilledValue),
            })
            .FirstOrDefaultAsync(ct) ?? new PurchaseOrderTrackEntry();

        int matches = await matching.CountAsync(ct);

        List<PurchaseOrderTrackEntry> page = await matching
            .OrderByDescending(r => r.DocumentDate)
            .ThenByDescending(r => r.PurchaseOrderId)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        long[] orderIds = [.. page.Select(r => r.PurchaseOrderId)];
        List<PurchaseOrderLineRow> lines = orderIds.Length == 0
            ? []
            : await Lines(_db.PurchaseOrders, _db.PurchaseOrderDetails, _db.Items, _db.UnitsOfMeasure, orderIds).ToListAsync(ct);
        List<PurchaseBilledFigure> billedLinesOfPage = orderIds.Length == 0
            ? []
            : await BilledLinesOf(_db.PurchaseOrders, _db.PurchaseOrderDetails, billedSums, orderIds).ToListAsync(ct);
        List<PurchaseOrderBillsRaised> raised = orderIds.Length == 0
            ? []
            : await Raised(_db.PurchaseOrderDetails, billedLines, orderIds).ToListAsync(ct);

        long[] contactIds = [.. page.Select(r => r.ContactId).Distinct()];
        List<VendorRef> vendors = contactIds.Length == 0
            ? []
            : await _db.Contacts
                .Where(c => contactIds.Contains(c.ContactId))
                .Select(c => new VendorRef { ContactId = c.ContactId, Code = c.ContactCode, Name = c.DisplayName, IsMsme = c.IsMsme })
                .ToListAsync(ct);

        return Compose(request, page, matches, lines, billedLinesOfPage, raised, vendors, counts, totals, baseCurrency);
    }

    // --- builders: LINQ over the read models, so a test runs them over lists -------------

    /// <summary>The posted and voided purchase orders dated in the period, narrowed by vendor. A draft is not an order yet.</summary>
    public static IQueryable<PurchaseOrderRead> Orders(IQueryable<PurchaseOrderRead> orders, PurchaseOrderTrackReportRequest request, DateOnly start, DateOnly end)
    {
        IQueryable<PurchaseOrderRead> matching = orders.Where(o =>
            (o.Status == DocumentStatus.Posted || o.Status == DocumentStatus.Void) && o.DocumentDate >= start && o.DocumentDate <= end);

        return request.ContactId is long contact ? matching.Where(o => o.ContactId == contact) : matching;
    }

    /// <summary>
    /// One row for each line of a posted bill that bills an order line, the line named by the bill's own
    /// line or, when it names none, by the receipt line it bills: the bill, how much it billed and what
    /// that came to before tax in base at the bill's rate. A bill that was voided, or never posted,
    /// billed nothing.
    /// </summary>
    public static IQueryable<PurchaseBilledLine> BilledLines(
        IQueryable<BillRead> bills,
        IQueryable<BillDetailRead> billDetails,
        IQueryable<GoodsReceiptDetailRead> receiptDetails) =>
        from bd in billDetails
        join b in bills on bd.BillId equals b.BillId
        join rd in receiptDetails on bd.GoodsReceiptDetailId equals (long?)rd.GoodsReceiptDetailId into received
        from r in received.DefaultIfEmpty()
        let orderLine = bd.PurchaseOrderDetailId ?? (r == null ? (long?)null : r.PurchaseOrderDetailId)
        where b.Status == DocumentStatus.Posted && orderLine != null
        select new PurchaseBilledLine
        {
            PurchaseOrderDetailId = orderLine!.Value,
            BillId = b.BillId,
            Quantity = bd.Quantity,
            Value = Math.Round(bd.TaxableAmount * b.ExchangeRate, 2),
        };

    /// <summary>What has been billed against each order line, in its unit and in base currency before tax.</summary>
    public static IQueryable<PurchaseBilledSum> BilledSums(IQueryable<PurchaseBilledLine> billed) =>
        billed
            .GroupBy(l => l.PurchaseOrderDetailId)
            .Select(g => new PurchaseBilledSum
            {
                PurchaseOrderDetailId = g.Key,
                Quantity = g.Sum(l => l.Quantity),
                Value = g.Sum(l => l.Value),
            });

    /// <summary>
    /// One row for each line of those orders: its ordered and received quantities, and its value before
    /// tax in base at the order's rate, with the received share of it at the order's own price per unit:
    /// the received share is rounded, and what is outstanding is the ordered value less it, so it adds
    /// up exactly.
    /// </summary>
    public static IQueryable<PurchaseOrderLineFigure> Figures(IQueryable<PurchaseOrderRead> orders, IQueryable<PurchaseOrderDetailRead> details) =>
        from d in details
        join o in orders on d.PurchaseOrderId equals o.PurchaseOrderId
        select new PurchaseOrderLineFigure
        {
            PurchaseOrderId = o.PurchaseOrderId,
            Ordered = d.Quantity,
            Received = d.ReceivedQuantity,
            OrderedValue = Math.Round(d.TaxableAmount * o.ExchangeRate, 2),
            ReceivedValue = d.Quantity == 0m ? 0m : Math.Round(d.TaxableAmount * o.ExchangeRate * d.ReceivedQuantity / d.Quantity, 2),
        };

    /// <summary>
    /// One row for each line of those orders that has been billed, with how much and its billed share of
    /// the line's value, at the order's own price per unit, rounded like the received share; and, so that
    /// every order has a row to join even when nothing is billed, one row of nothing for every line. A
    /// line is billed in full when one of its rows says so, which the row of nothing never does.
    /// </summary>
    public static IQueryable<PurchaseBilledFigure> BilledFigures(
        IQueryable<PurchaseOrderRead> orders,
        IQueryable<PurchaseOrderDetailRead> details,
        IQueryable<PurchaseBilledSum> billedSums)
    {
        IQueryable<PurchaseBilledFigure> billed = BilledLineFigures(orders, details, billedSums);

        // A billed quantity no bill can have, so this row never reads as billed in full, whatever the line ordered.
        IQueryable<PurchaseBilledFigure> none =
            from d in details
            join o in orders on d.PurchaseOrderId equals o.PurchaseOrderId
            select new PurchaseBilledFigure { PurchaseOrderDetailId = d.PurchaseOrderDetailId, PurchaseOrderId = o.PurchaseOrderId, Ordered = d.Quantity, Billed = -1m, BilledValue = 0m };

        return billed.Concat(none);
    }

    /// <summary>One row for each billed line of those orders: how much of it is billed and the billed share of its value, at the order's price.</summary>
    private static IQueryable<PurchaseBilledFigure> BilledLineFigures(
        IQueryable<PurchaseOrderRead> orders,
        IQueryable<PurchaseOrderDetailRead> details,
        IQueryable<PurchaseBilledSum> billedSums) =>
        from d in details
        join o in orders on d.PurchaseOrderId equals o.PurchaseOrderId
        join s in billedSums on d.PurchaseOrderDetailId equals s.PurchaseOrderDetailId
        select new PurchaseBilledFigure
        {
            PurchaseOrderDetailId = d.PurchaseOrderDetailId,
            PurchaseOrderId = o.PurchaseOrderId,
            Ordered = d.Quantity,
            Billed = s.Quantity,
            BilledValue = d.Quantity == 0m ? 0m : Math.Round(d.TaxableAmount * o.ExchangeRate * s.Quantity / d.Quantity, 2),
        };

    /// <summary>The billed lines of the given orders only, for the lines the page shows: what each is billed for, worked out the way <see cref="BilledFigures"/> does.</summary>
    public static IQueryable<PurchaseBilledFigure> BilledLinesOf(
        IQueryable<PurchaseOrderRead> orders,
        IQueryable<PurchaseOrderDetailRead> details,
        IQueryable<PurchaseBilledSum> billedSums,
        IReadOnlyCollection<long> orderIds) =>
        BilledLineFigures(orders, details, billedSums).Where(f => orderIds.Contains(f.PurchaseOrderId));

    /// <summary>
    /// One row for each order with its lines' values added up and its progress: cancelled when voided
    /// or cancelled; else billed when every line is billed in full; else closed short when it was
    /// closed with a line not received; else received when every line is received in full; else
    /// partly received when anything was; else open.
    /// </summary>
    public static IQueryable<PurchaseOrderTrackEntry> Rows(
        IQueryable<PurchaseOrderRead> orders,
        IQueryable<PurchaseOrderLineFigure> figures,
        IQueryable<PurchaseBilledFigure> billedFigures)
    {
        IQueryable<PurchaseOrderSum> sums = figures
            .GroupBy(f => f.PurchaseOrderId)
            .Select(g => new PurchaseOrderSum
            {
                PurchaseOrderId = g.Key,
                Lines = g.Count(),
                UnreceivedLines = g.Count(f => f.Received < f.Ordered),
                ReceivedLines = g.Count(f => f.Received > 0m),
                OrderedValue = g.Sum(f => f.OrderedValue),
                ReceivedValue = g.Sum(f => f.ReceivedValue),
            });

        IQueryable<PurchaseOrderBilledSum> billed = billedFigures
            .GroupBy(f => f.PurchaseOrderId)
            .Select(g => new PurchaseOrderBilledSum
            {
                PurchaseOrderId = g.Key,
                FullyBilledLines = g.Count(f => f.Billed >= f.Ordered),
                BilledValue = g.Sum(f => f.BilledValue),
            });

        return from o in orders
               join s in sums on o.PurchaseOrderId equals s.PurchaseOrderId
               join b in billed on o.PurchaseOrderId equals b.PurchaseOrderId
               select new PurchaseOrderTrackEntry
               {
                   PurchaseOrderId = o.PurchaseOrderId,
                   DocumentNo = o.DocumentNo,
                   DocumentDate = o.DocumentDate,
                   ExpectedDate = o.ExpectedDate,
                   ContactId = o.ContactId,
                   CurrencyCode = o.CurrencyCode,
                   OrderedValue = s.OrderedValue,
                   ReceivedValue = s.ReceivedValue,
                   BilledValue = b.BilledValue,
                   Progress = o.Status == DocumentStatus.Void || o.FulfilmentStatus == PurchaseFulfilmentStatus.Cancelled
                       ? PurchaseOrderProgress.Cancelled
                       : b.FullyBilledLines == s.Lines
                           ? PurchaseOrderProgress.Billed
                           : o.FulfilmentStatus == PurchaseFulfilmentStatus.Closed && s.UnreceivedLines > 0
                               ? PurchaseOrderProgress.ClosedShort
                               : s.UnreceivedLines == 0
                                   ? PurchaseOrderProgress.Received
                                   : s.ReceivedLines > 0
                                       ? PurchaseOrderProgress.PartlyReceived
                                       : PurchaseOrderProgress.Open,
               };
    }

    /// <summary>Only the orders with something still to receive or bill when asked: open, partly received or received and not billed. LINQ only.</summary>
    public static IQueryable<PurchaseOrderTrackEntry> OutstandingOnly(IQueryable<PurchaseOrderTrackEntry> rows, bool outstandingOnly) =>
        outstandingOnly
            ? rows.Where(r => r.Progress == PurchaseOrderProgress.Open || r.Progress == PurchaseOrderProgress.PartlyReceived || r.Progress == PurchaseOrderProgress.Received)
            : rows;

    /// <summary>The rows at the progress asked for; with none asked for, every row but the cancelled ones. LINQ only.</summary>
    public static IQueryable<PurchaseOrderTrackEntry> WithProgress(IQueryable<PurchaseOrderTrackEntry> rows, PurchaseOrderProgress? progress) =>
        progress is PurchaseOrderProgress chosen
            ? rows.Where(r => r.Progress == chosen)
            : rows.Where(r => r.Progress != PurchaseOrderProgress.Cancelled);

    /// <summary>
    /// The lines of the given orders with their items and units, each with its ordered and received
    /// figures worked out the way <see cref="Figures"/> does, so a line's values add up to its order's.
    /// What each is billed for is read apart (<see cref="BilledLinesOf"/>) and joined in by
    /// <see cref="Compose"/>.
    /// </summary>
    public static IQueryable<PurchaseOrderLineRow> Lines(
        IQueryable<PurchaseOrderRead> orders,
        IQueryable<PurchaseOrderDetailRead> details,
        IQueryable<ItemRead> items,
        IQueryable<UnitOfMeasureRead> units,
        IReadOnlyCollection<long> orderIds) =>
        from d in details
        join o in orders on d.PurchaseOrderId equals o.PurchaseOrderId
        join iv in items on d.ItemId equals (long?)iv.ItemId into itemed
        from it in itemed.DefaultIfEmpty()
        join uv in units on d.UomId equals (long?)uv.UomId into united
        from un in united.DefaultIfEmpty()
        where orderIds.Contains(o.PurchaseOrderId)
        select new PurchaseOrderLineRow
        {
            PurchaseOrderDetailId = d.PurchaseOrderDetailId,
            PurchaseOrderId = o.PurchaseOrderId,
            LineNumber = d.LineNumber,
            ItemId = d.ItemId,
            ItemCode = it == null ? null : it.ItemCode,
            ItemName = it == null ? null : it.ItemName,
            Description = d.Description,
            UnitCode = un == null ? null : un.UomCode,
            Ordered = d.Quantity,
            Received = d.ReceivedQuantity,
            OrderedValue = Math.Round(d.TaxableAmount * o.ExchangeRate, 2),
            ReceivedValue = d.Quantity == 0m ? 0m : Math.Round(d.TaxableAmount * o.ExchangeRate * d.ReceivedQuantity / d.Quantity, 2),
        };

    /// <summary>
    /// How many posted bills have billed against each of the given orders' lines, and what their lines
    /// came to before tax in base: the actual billing, at the prices the bills were raised at.
    /// </summary>
    public static IQueryable<PurchaseOrderBillsRaised> Raised(
        IQueryable<PurchaseOrderDetailRead> details,
        IQueryable<PurchaseBilledLine> billedLines,
        IReadOnlyCollection<long> orderIds) =>
        from l in billedLines
        join d in details on l.PurchaseOrderDetailId equals d.PurchaseOrderDetailId
        where orderIds.Contains(d.PurchaseOrderId)
        group l by d.PurchaseOrderId into g
        select new PurchaseOrderBillsRaised
        {
            PurchaseOrderId = g.Key,
            Count = g.Select(l => l.BillId).Distinct().Count(),
            Billed = g.Sum(l => l.Value),
        };

    // --- compose: pure, so a test needs no database --------------------------------------

    /// <summary>
    /// The page's orders with their vendors, lines and what was billed against them, how many orders
    /// are at each progress, and the totals. Every progress has a count, a nought included. What is
    /// outstanding is ordered less received or billed, on each line and each order. Pure.
    /// </summary>
    public static PurchaseOrderTrackReportResultDto Compose(
        PurchaseOrderTrackReportRequest request,
        IReadOnlyList<PurchaseOrderTrackEntry> page,
        int totalOrders,
        IReadOnlyList<PurchaseOrderLineRow> lines,
        IReadOnlyList<PurchaseBilledFigure> billedLines,
        IReadOnlyList<PurchaseOrderBillsRaised> raised,
        IReadOnlyList<VendorRef> vendors,
        IReadOnlyList<PurchaseOrderProgressCount> counts,
        PurchaseOrderTrackEntry totals,
        string baseCurrency)
    {
        Dictionary<long, VendorRef> names = vendors.GroupBy(c => c.ContactId).ToDictionary(g => g.Key, g => g.First());
        Dictionary<long, PurchaseOrderBillsRaised> billed = raised.GroupBy(r => r.PurchaseOrderId).ToDictionary(g => g.Key, g => g.First());
        ILookup<long, PurchaseOrderLineRow> byOrder = lines.ToLookup(l => l.PurchaseOrderId);
        Dictionary<long, PurchaseBilledFigure> billedBy = billedLines.GroupBy(f => f.PurchaseOrderDetailId).ToDictionary(g => g.Key, g => g.First());

        Dictionary<PurchaseOrderProgress, int> progressCounts = Enum.GetValues<PurchaseOrderProgress>().ToDictionary(p => p, _ => 0);
        foreach (PurchaseOrderProgressCount count in counts)
        {
            progressCounts[count.Progress] = count.Count;
        }

        return new PurchaseOrderTrackReportResultDto
        {
            FromDate = request.FromDate!.Value,
            ToDate = request.ToDate!.Value,
            BaseCurrencyCode = baseCurrency,
            Orders =
            [
                .. page.Select(r =>
                {
                    VendorRef? vendor = names.GetValueOrDefault(r.ContactId);
                    PurchaseOrderBillsRaised? bills = billed.GetValueOrDefault(r.PurchaseOrderId);
                    return new PurchaseOrderTrackOrderDto
                    {
                        PurchaseOrderId = r.PurchaseOrderId,
                        DocumentNo = r.DocumentNo,
                        DocumentDate = r.DocumentDate,
                        ExpectedDate = r.ExpectedDate,
                        ContactId = r.ContactId,
                        ContactCode = vendor?.Code ?? string.Empty,
                        ContactName = vendor?.Name ?? $"Vendor {r.ContactId}",
                        CurrencyCode = r.CurrencyCode,
                        Progress = r.Progress,
                        Lines = [.. byOrder[r.PurchaseOrderId].OrderBy(l => l.LineNumber).Select(l => LineOf(l, billedBy.GetValueOrDefault(l.PurchaseOrderDetailId)))],
                        OrderedValue = r.OrderedValue,
                        ReceivedValue = r.ReceivedValue,
                        BilledValue = r.BilledValue,
                        ToReceiveValue = r.OrderedValue - r.ReceivedValue,
                        ToBillValue = r.OrderedValue - r.BilledValue,
                        BillsRaised = bills?.Count ?? 0,
                        BillTotal = bills?.Billed ?? 0m,
                    };
                }),
            ],
            TotalOrders = totalOrders,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            ProgressCounts = progressCounts,
            Totals = new PurchaseOrderTrackTotalsDto
            {
                OrderedValue = totals.OrderedValue,
                ReceivedValue = totals.ReceivedValue,
                BilledValue = totals.BilledValue,
                ToReceiveValue = totals.OrderedValue - totals.ReceivedValue,
                ToBillValue = totals.OrderedValue - totals.BilledValue,
            },
        };
    }

    private static PurchaseOrderTrackLineDto LineOf(PurchaseOrderLineRow l, PurchaseBilledFigure? billed) =>
        new()
        {
            LineNumber = l.LineNumber,
            ItemId = l.ItemId,
            ItemCode = l.ItemCode,
            ItemName = l.ItemName ?? l.Description ?? string.Empty,
            UnitCode = l.UnitCode,
            Ordered = l.Ordered,
            Received = l.Received,
            Billed = billed?.Billed ?? 0m,
            ToReceive = l.Ordered - l.Received,
            ToBill = l.Ordered - (billed?.Billed ?? 0m),
            OrderedValue = l.OrderedValue,
            ReceivedValue = l.ReceivedValue,
            BilledValue = billed?.BilledValue ?? 0m,
            ToReceiveValue = l.OrderedValue - l.ReceivedValue,
            ToBillValue = l.OrderedValue - (billed?.BilledValue ?? 0m),
        };
}

/// <summary>A line of a posted bill that bills an order line: the bill, how much it billed and what that came to in base before tax.</summary>
public sealed class PurchaseBilledLine
{
    public long PurchaseOrderDetailId { get; set; }

    public long BillId { get; set; }

    public decimal Quantity { get; set; }

    public decimal Value { get; set; }
}

/// <summary>What has been billed against one order line.</summary>
public sealed class PurchaseBilledSum
{
    public long PurchaseOrderDetailId { get; set; }

    public decimal Quantity { get; set; }

    public decimal Value { get; set; }
}

/// <summary>A line of an order with its quantities and its values in base currency.</summary>
public sealed class PurchaseOrderLineFigure
{
    public long PurchaseOrderId { get; set; }

    public decimal Ordered { get; set; }

    public decimal Received { get; set; }

    public decimal OrderedValue { get; set; }

    public decimal ReceivedValue { get; set; }
}

/// <summary>A billed line of an order: how much of it is billed and the billed share of its value in base currency.</summary>
public sealed class PurchaseBilledFigure
{
    public long PurchaseOrderDetailId { get; set; }

    public long PurchaseOrderId { get; set; }

    public decimal Ordered { get; set; }

    public decimal Billed { get; set; }

    public decimal BilledValue { get; set; }
}

/// <summary>An order's billed lines counted and their values added up.</summary>
public sealed class PurchaseOrderBilledSum
{
    public long PurchaseOrderId { get; set; }

    /// <summary>Lines billed in full.</summary>
    public int FullyBilledLines { get; set; }

    public decimal BilledValue { get; set; }
}

/// <summary>An order's lines counted and its values added up.</summary>
public sealed class PurchaseOrderSum
{
    public long PurchaseOrderId { get; set; }

    /// <summary>How many lines the order has.</summary>
    public int Lines { get; set; }

    /// <summary>Lines with something left to receive.</summary>
    public int UnreceivedLines { get; set; }

    /// <summary>Lines with something received.</summary>
    public int ReceivedLines { get; set; }

    public decimal OrderedValue { get; set; }

    public decimal ReceivedValue { get; set; }
}

/// <summary>An order with its values and its progress.</summary>
public sealed class PurchaseOrderTrackEntry
{
    public long PurchaseOrderId { get; set; }

    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    public DateOnly? ExpectedDate { get; set; }

    public long ContactId { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal OrderedValue { get; set; }

    public decimal ReceivedValue { get; set; }

    public decimal BilledValue { get; set; }

    public PurchaseOrderProgress Progress { get; set; }
}

/// <summary>A line of an order with its item, unit, quantities and values.</summary>
public sealed class PurchaseOrderLineRow
{
    public long PurchaseOrderDetailId { get; set; }

    public long PurchaseOrderId { get; set; }

    public int LineNumber { get; set; }

    public long? ItemId { get; set; }

    public string? ItemCode { get; set; }

    public string? ItemName { get; set; }

    public string? Description { get; set; }

    public string? UnitCode { get; set; }

    public decimal Ordered { get; set; }

    public decimal Received { get; set; }

    public decimal OrderedValue { get; set; }

    public decimal ReceivedValue { get; set; }
}

/// <summary>How many posted bills billed against an order, and what their lines came to.</summary>
public sealed class PurchaseOrderBillsRaised
{
    public long PurchaseOrderId { get; set; }

    public int Count { get; set; }

    public decimal Billed { get; set; }
}

/// <summary>How many orders are at a progress.</summary>
public sealed class PurchaseOrderProgressCount
{
    public PurchaseOrderProgress Progress { get; set; }

    public int Count { get; set; }
}

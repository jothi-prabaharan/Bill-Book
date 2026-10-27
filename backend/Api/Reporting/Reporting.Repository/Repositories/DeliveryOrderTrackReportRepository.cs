using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Delivery Order Track Report (TK-232): each delivery challan's billing, a page of challans at a
/// time, with their lines: what was delivered, what invoices raised from the challan have billed of each,
/// and what is still to bill. A challan is a posted delivery challan dated in the period (a voided one is
/// listed as void, and a draft has delivered nothing).
///
/// <b>Invoiced is the line's own running figure</b>, <c>InvoicedQuantity</c>, which an invoice raised from
/// the challan advances (and voiding it takes back), so delivered less invoiced is what is still to bill on
/// every line. An invoice raised from the sales order and not naming the challan bills the order's lines
/// and clears the goods-delivered-not-invoiced account, but does not advance the challan's figure, so the
/// challan keeps reading as unbilled; the page says so. A value is the line's net value before tax in base
/// currency, shared out at the challan's own price per unit; the invoiced and to-bill values are shares of
/// it, so they add up exactly.
///
/// <b>The status is worked out in the database</b>, from every line: every line billed in full makes it
/// invoiced, none billed at all not invoiced, anything between partly invoiced. So the tabs' counts, the
/// totals and the page are each one query over every challan the filters match, and only the lines, the
/// source orders and the invoices of the page's own challans are read after it.
/// </summary>
public sealed class DeliveryOrderTrackReportRepository
{
    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public DeliveryOrderTrackReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>The refusals a request needs beyond both dates being given, with the field each belongs to. Pure.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(DeliveryOrderTrackReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from)
        {
            refusals.Add((nameof(DeliveryOrderTrackReportRequest.ToDate), "To date cannot be before the from date."));
        }

        if (request.Progress is ChallanProgress progress && !Enum.IsDefined(progress))
        {
            refusals.Add((nameof(DeliveryOrderTrackReportRequest.Progress), "Status must be one of the options offered."));
        }

        if (request.ChallanType is ChallanType type && !Enum.IsDefined(type))
        {
            refusals.Add((nameof(DeliveryOrderTrackReportRequest.ChallanType), "Challan type must be one of the options offered."));
        }

        return refusals;
    }

    /// <summary>Whether a customer is on this branch; the query filter decides, so another branch's reads as none.</summary>
    public Task<bool> ContactExistsAsync(long contactId, CancellationToken ct) =>
        _db.Contacts.AnyAsync(c => c.ContactId == contactId, ct);

    /// <summary>The customers the filter offers, by name: the Aged Receivables list.</summary>
    public Task<List<AgedReceivablesCustomerChoiceDto>> CustomersAsync(CancellationToken ct) =>
        AgedReceivablesSummaryReportRepository.CustomerChoices(_db.Contacts).ToListAsync(ct);

    /// <summary>The report for a request the controller has already validated.</summary>
    public async Task<DeliveryOrderTrackReportResultDto> RunAsync(DeliveryOrderTrackReportRequest request, CancellationToken ct)
    {
        DateOnly start = request.FromDate!.Value;
        DateOnly end = request.ToDate!.Value;
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

        IQueryable<DeliveryChallanRead> challans = Challans(_db.DeliveryChallans, request, start, end);
        IQueryable<ChallanLineFigure> figures = Figures(challans, _db.DeliveryChallanDetails);
        IQueryable<ChallanTrackRow> rows = Rows(challans, figures);

        // The tabs count every status the other filters let through, so a tab never hides a challan behind the one chosen.
        IQueryable<ChallanTrackRow> counted = NotInvoicedOnly(rows, request.NotInvoicedOnly);
        List<ChallanProgressCount> counts = await counted
            .GroupBy(r => r.Progress)
            .Select(g => new ChallanProgressCount { Progress = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        IQueryable<ChallanTrackRow> matching = WithProgress(counted, request.Progress);

        ChallanTrackRow totals = await matching
            .GroupBy(r => 1)
            .Select(g => new ChallanTrackRow
            {
                DeliveredValue = g.Sum(r => r.DeliveredValue),
                InvoicedValue = g.Sum(r => r.InvoicedValue),
            })
            .FirstOrDefaultAsync(ct) ?? new ChallanTrackRow();

        int matches = await matching.CountAsync(ct);

        List<ChallanTrackRow> page = await matching
            .OrderByDescending(r => r.DocumentDate)
            .ThenByDescending(r => r.DeliveryChallanId)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        long[] challanIds = [.. page.Select(r => r.DeliveryChallanId)];
        List<ChallanLineRow> lines = challanIds.Length == 0
            ? []
            : await Lines(_db.DeliveryChallans, _db.DeliveryChallanDetails, _db.Items, _db.UnitsOfMeasure, challanIds).ToListAsync(ct);
        List<ChallanInvoiceRow> invoices = challanIds.Length == 0 ? [] : await Raised(_db.Invoices, challanIds).ToListAsync(ct);

        long[] orderIds = [.. page.Where(r => r.SalesOrderId != null).Select(r => r.SalesOrderId!.Value).Distinct()];
        List<SourceOrderRow> orders = orderIds.Length == 0 ? [] : await SourceOrders(_db.SalesOrders, orderIds).ToListAsync(ct);

        long[] contactIds = [.. page.Select(r => r.ContactId).Distinct()];
        List<CustomerRef> customers = contactIds.Length == 0
            ? []
            : await _db.Contacts
                .Where(c => contactIds.Contains(c.ContactId))
                .Select(c => new CustomerRef { ContactId = c.ContactId, Code = c.ContactCode, Name = c.DisplayName })
                .ToListAsync(ct);

        return Compose(request, page, matches, lines, invoices, orders, customers, counts, totals, baseCurrency);
    }

    // --- builders: LINQ over the read models, so a test runs them over lists -------------

    /// <summary>
    /// The posted and voided delivery challans dated in the period, narrowed by customer and by type.
    /// A draft has delivered nothing.
    /// </summary>
    public static IQueryable<DeliveryChallanRead> Challans(
        IQueryable<DeliveryChallanRead> challans, DeliveryOrderTrackReportRequest request, DateOnly start, DateOnly end)
    {
        IQueryable<DeliveryChallanRead> matching = challans.Where(c =>
            (c.Status == DocumentStatus.Posted || c.Status == DocumentStatus.Void) && c.DocumentDate >= start && c.DocumentDate <= end);

        if (request.ContactId is long contact)
        {
            matching = matching.Where(c => c.ContactId == contact);
        }

        return request.ChallanType is ChallanType type ? matching.Where(c => c.ChallanType == type) : matching;
    }

    /// <summary>
    /// One row for each line of those challans: its delivered and invoiced quantities, and its value
    /// before tax in base at the challan's rate, shared out at the challan's own price per unit: the
    /// invoiced share is rounded, and what is still to bill is the delivered value less it, so each adds
    /// up exactly.
    /// </summary>
    public static IQueryable<ChallanLineFigure> Figures(IQueryable<DeliveryChallanRead> challans, IQueryable<DeliveryChallanDetailRead> details) =>
        from d in details
        join c in challans on d.DeliveryChallanId equals c.DeliveryChallanId
        select new ChallanLineFigure
        {
            DeliveryChallanId = c.DeliveryChallanId,
            Delivered = d.Quantity,
            Invoiced = d.InvoicedQuantity,
            DeliveredValue = Math.Round(d.TaxableAmount * c.ExchangeRate, 2),
            InvoicedValue = d.Quantity == 0m ? 0m : Math.Round(d.TaxableAmount * c.ExchangeRate * d.InvoicedQuantity / d.Quantity, 2),
        };

    /// <summary>
    /// One row for each challan with its lines' values added up and its status: void when voided; else
    /// invoiced when every line is billed in full; else not invoiced when no line has been billed at all;
    /// else partly invoiced.
    /// </summary>
    public static IQueryable<ChallanTrackRow> Rows(IQueryable<DeliveryChallanRead> challans, IQueryable<ChallanLineFigure> figures)
    {
        IQueryable<ChallanSum> sums = figures
            .GroupBy(f => f.DeliveryChallanId)
            .Select(g => new ChallanSum
            {
                DeliveryChallanId = g.Key,
                UnbilledLines = g.Count(f => f.Invoiced < f.Delivered),
                BilledLines = g.Count(f => f.Invoiced > 0m),
                DeliveredValue = g.Sum(f => f.DeliveredValue),
                InvoicedValue = g.Sum(f => f.InvoicedValue),
            });

        return from c in challans
               join s in sums on c.DeliveryChallanId equals s.DeliveryChallanId
               select new ChallanTrackRow
               {
                   DeliveryChallanId = c.DeliveryChallanId,
                   DocumentNo = c.DocumentNo,
                   DocumentDate = c.DocumentDate,
                   DispatchDate = c.DispatchDate,
                   ContactId = c.ContactId,
                   CurrencyCode = c.CurrencyCode,
                   ChallanType = c.ChallanType,
                   SalesOrderId = c.SalesOrderId,
                   DeliveredValue = s.DeliveredValue,
                   InvoicedValue = s.InvoicedValue,
                   Progress = c.Status == DocumentStatus.Void
                       ? ChallanProgress.Void
                       : s.UnbilledLines == 0
                           ? ChallanProgress.Invoiced
                           : s.BilledLines == 0
                               ? ChallanProgress.NotInvoiced
                               : ChallanProgress.PartlyInvoiced,
               };
    }

    /// <summary>Only the challans with something still to bill when asked: not invoiced, or partly invoiced. LINQ only.</summary>
    public static IQueryable<ChallanTrackRow> NotInvoicedOnly(IQueryable<ChallanTrackRow> rows, bool notInvoicedOnly) =>
        notInvoicedOnly
            ? rows.Where(r => r.Progress == ChallanProgress.NotInvoiced || r.Progress == ChallanProgress.PartlyInvoiced)
            : rows;

    /// <summary>The rows at the status asked for; with none asked for, every row but the void ones. LINQ only.</summary>
    public static IQueryable<ChallanTrackRow> WithProgress(IQueryable<ChallanTrackRow> rows, ChallanProgress? progress) =>
        progress is ChallanProgress chosen
            ? rows.Where(r => r.Progress == chosen)
            : rows.Where(r => r.Progress != ChallanProgress.Void);

    /// <summary>
    /// The lines of the given challans with their items and units, each with its figures worked out the
    /// way <see cref="Figures"/> does, so a line's values add up to its challan's.
    /// </summary>
    public static IQueryable<ChallanLineRow> Lines(
        IQueryable<DeliveryChallanRead> challans,
        IQueryable<DeliveryChallanDetailRead> details,
        IQueryable<ItemRead> items,
        IQueryable<UnitOfMeasureRead> units,
        IReadOnlyCollection<long> challanIds) =>
        from d in details
        join c in challans on d.DeliveryChallanId equals c.DeliveryChallanId
        join iv in items on d.ItemId equals (long?)iv.ItemId into itemed
        from it in itemed.DefaultIfEmpty()
        join uv in units on d.UomId equals (long?)uv.UomId into united
        from un in united.DefaultIfEmpty()
        where challanIds.Contains(c.DeliveryChallanId)
        select new ChallanLineRow
        {
            DeliveryChallanId = c.DeliveryChallanId,
            LineNumber = d.LineNumber,
            ItemId = d.ItemId,
            ItemCode = it == null ? null : it.ItemCode,
            ItemName = it == null ? null : it.ItemName,
            Description = d.Description,
            UnitCode = un == null ? null : un.UomCode,
            Delivered = d.Quantity,
            Invoiced = d.InvoicedQuantity,
            DeliveredValue = Math.Round(d.TaxableAmount * c.ExchangeRate, 2),
            InvoicedValue = d.Quantity == 0m ? 0m : Math.Round(d.TaxableAmount * c.ExchangeRate * d.InvoicedQuantity / d.Quantity, 2),
        };

    /// <summary>The posted invoices raised from each of the given challans, one row each.</summary>
    public static IQueryable<ChallanInvoiceRow> Raised(IQueryable<InvoiceRead> invoices, IReadOnlyCollection<long> challanIds) =>
        invoices
            .Where(i => i.DeliveryChallanId != null && challanIds.Contains(i.DeliveryChallanId.Value) && i.Status == DocumentStatus.Posted)
            .Select(i => new ChallanInvoiceRow
            {
                DeliveryChallanId = i.DeliveryChallanId!.Value,
                TypeCode = i.TransactionTypeCode,
                DocumentId = i.InvoiceId,
                DocumentNo = i.DocumentNo,
                DocumentDate = i.DocumentDate,
            });

    /// <summary>The numbers of the sales orders the given challans were delivered against.</summary>
    public static IQueryable<SourceOrderRow> SourceOrders(IQueryable<SalesOrderRead> orders, IReadOnlyCollection<long> orderIds) =>
        orders
            .Where(o => orderIds.Contains(o.SalesOrderId))
            .Select(o => new SourceOrderRow { SalesOrderId = o.SalesOrderId, DocumentNo = o.DocumentNo });

    // --- compose: pure, so a test needs no database --------------------------------------

    /// <summary>
    /// The page's challans with their customers, lines, source orders and invoices, how many challans
    /// are at each status, and the totals. Every status has a count, a nought included. What is still to
    /// bill is delivered less invoiced, on each line and each challan. Pure.
    /// </summary>
    public static DeliveryOrderTrackReportResultDto Compose(
        DeliveryOrderTrackReportRequest request,
        IReadOnlyList<ChallanTrackRow> page,
        int totalChallans,
        IReadOnlyList<ChallanLineRow> lines,
        IReadOnlyList<ChallanInvoiceRow> invoices,
        IReadOnlyList<SourceOrderRow> orders,
        IReadOnlyList<CustomerRef> customers,
        IReadOnlyList<ChallanProgressCount> counts,
        ChallanTrackRow totals,
        string baseCurrency)
    {
        Dictionary<long, CustomerRef> names = customers.GroupBy(c => c.ContactId).ToDictionary(g => g.Key, g => g.First());
        Dictionary<long, SourceOrderRow> sources = orders.GroupBy(o => o.SalesOrderId).ToDictionary(g => g.Key, g => g.First());
        ILookup<long, ChallanLineRow> byChallan = lines.ToLookup(l => l.DeliveryChallanId);
        ILookup<long, ChallanInvoiceRow> billedOn = invoices.ToLookup(i => i.DeliveryChallanId);

        Dictionary<ChallanProgress, int> progressCounts = Enum.GetValues<ChallanProgress>().ToDictionary(p => p, _ => 0);
        foreach (ChallanProgressCount count in counts)
        {
            progressCounts[count.Progress] = count.Count;
        }

        return new DeliveryOrderTrackReportResultDto
        {
            FromDate = request.FromDate!.Value,
            ToDate = request.ToDate!.Value,
            BaseCurrencyCode = baseCurrency,
            Challans =
            [
                .. page.Select(r =>
                {
                    CustomerRef? customer = names.GetValueOrDefault(r.ContactId);
                    SourceOrderRow? source = r.SalesOrderId is long orderId ? sources.GetValueOrDefault(orderId) : null;
                    return new DeliveryOrderTrackChallanDto
                    {
                        DeliveryChallanId = r.DeliveryChallanId,
                        DocumentNo = r.DocumentNo,
                        DocumentDate = r.DocumentDate,
                        DispatchDate = r.DispatchDate,
                        ContactId = r.ContactId,
                        ContactCode = customer?.Code ?? string.Empty,
                        ContactName = customer?.Name ?? $"Customer {r.ContactId}",
                        CurrencyCode = r.CurrencyCode,
                        ChallanType = r.ChallanType,
                        Progress = r.Progress,
                        SalesOrderId = r.SalesOrderId,
                        SalesOrderNo = source?.DocumentNo,
                        Lines = [.. byChallan[r.DeliveryChallanId].OrderBy(l => l.LineNumber).Select(LineOf)],
                        DeliveredValue = r.DeliveredValue,
                        InvoicedValue = r.InvoicedValue,
                        ToBillValue = r.DeliveredValue - r.InvoicedValue,
                        Invoices =
                        [
                            .. billedOn[r.DeliveryChallanId]
                                .OrderBy(i => i.DocumentDate)
                                .ThenBy(i => i.DocumentId)
                                .Select(i => new DeliveryOrderTrackInvoiceDto
                                {
                                    TypeCode = i.TypeCode,
                                    DocumentId = i.DocumentId,
                                    DocumentNo = i.DocumentNo,
                                    DocumentDate = i.DocumentDate,
                                }),
                        ],
                    };
                }),
            ],
            TotalChallans = totalChallans,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            ProgressCounts = progressCounts,
            Totals = new DeliveryOrderTrackTotalsDto
            {
                DeliveredValue = totals.DeliveredValue,
                InvoicedValue = totals.InvoicedValue,
                ToBillValue = totals.DeliveredValue - totals.InvoicedValue,
            },
        };
    }

    private static DeliveryOrderTrackLineDto LineOf(ChallanLineRow l) =>
        new()
        {
            LineNumber = l.LineNumber,
            ItemId = l.ItemId,
            ItemCode = l.ItemCode,
            ItemName = l.ItemName ?? l.Description ?? string.Empty,
            UnitCode = l.UnitCode,
            Delivered = l.Delivered,
            Invoiced = l.Invoiced,
            ToBill = l.Delivered - l.Invoiced,
            DeliveredValue = l.DeliveredValue,
            InvoicedValue = l.InvoicedValue,
            ToBillValue = l.DeliveredValue - l.InvoicedValue,
        };
}

/// <summary>A line of a challan with its quantities and its values in base currency.</summary>
public sealed class ChallanLineFigure
{
    public long DeliveryChallanId { get; set; }

    public decimal Delivered { get; set; }

    public decimal Invoiced { get; set; }

    public decimal DeliveredValue { get; set; }

    public decimal InvoicedValue { get; set; }
}

/// <summary>A challan's lines counted and its values added up.</summary>
public sealed class ChallanSum
{
    public long DeliveryChallanId { get; set; }

    /// <summary>Lines with something left to bill.</summary>
    public int UnbilledLines { get; set; }

    /// <summary>Lines with something billed.</summary>
    public int BilledLines { get; set; }

    public decimal DeliveredValue { get; set; }

    public decimal InvoicedValue { get; set; }
}

/// <summary>A challan with its values and its status.</summary>
public sealed class ChallanTrackRow
{
    public long DeliveryChallanId { get; set; }

    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    public DateOnly DispatchDate { get; set; }

    public long ContactId { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public ChallanType ChallanType { get; set; }

    public long? SalesOrderId { get; set; }

    public decimal DeliveredValue { get; set; }

    public decimal InvoicedValue { get; set; }

    public ChallanProgress Progress { get; set; }
}

/// <summary>A line of a challan with its item, unit, quantities and values.</summary>
public sealed class ChallanLineRow
{
    public long DeliveryChallanId { get; set; }

    public int LineNumber { get; set; }

    public long? ItemId { get; set; }

    public string? ItemCode { get; set; }

    public string? ItemName { get; set; }

    public string? Description { get; set; }

    public string? UnitCode { get; set; }

    public decimal Delivered { get; set; }

    public decimal Invoiced { get; set; }

    public decimal DeliveredValue { get; set; }

    public decimal InvoicedValue { get; set; }
}

/// <summary>An invoice raised from a challan.</summary>
public sealed class ChallanInvoiceRow
{
    public long DeliveryChallanId { get; set; }

    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }
}

/// <summary>The number of the sales order a challan was delivered against.</summary>
public sealed class SourceOrderRow
{
    public long SalesOrderId { get; set; }

    public string DocumentNo { get; set; } = string.Empty;
}

/// <summary>How many challans are at a status.</summary>
public sealed class ChallanProgressCount
{
    public ChallanProgress Progress { get; set; }

    public int Count { get; set; }
}

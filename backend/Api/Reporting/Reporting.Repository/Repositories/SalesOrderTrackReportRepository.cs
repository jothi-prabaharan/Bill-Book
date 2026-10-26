using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Sales Order Track Report (TK-230): each sales order's progress, a page of orders at a time,
/// with their lines: what was ordered, delivered and invoiced of each, and what is still to deliver
/// or to bill. An order is a posted sales order dated in the period (a voided one is listed as
/// cancelled, and a draft is not an order yet).
///
/// <b>Delivered and invoiced are the lines' own running figures</b>, <c>DeliveredQuantity</c> and
/// <c>InvoicedQuantity</c>, which a delivery challan and an invoice raised from the order advance, so
/// ordered less delivered is what is still to deliver and ordered less invoiced what is still to bill,
/// on every line. A value is the line's net value before tax in base currency, shared out at the
/// order's own price per unit; the outstanding values are ordered less delivered or invoiced, so they
/// add up exactly. What the invoices raised from the order actually came to is read apart (it can
/// differ if a price was changed at billing) and shown beside them.
///
/// <b>The progress is worked out in the database</b>, from every line, as Sales does: every line
/// delivered in full makes it delivered, every line billed in full invoiced. So the tabs' counts, the
/// totals and the page are each one query over every order the filters match, and only the lines of
/// the page's own orders are read after it.
/// </summary>
public sealed class SalesOrderTrackReportRepository
{
    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public SalesOrderTrackReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>The refusals a request needs beyond both dates being given, with the field each belongs to. Pure.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(SalesOrderTrackReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from)
        {
            refusals.Add((nameof(SalesOrderTrackReportRequest.ToDate), "To date cannot be before the from date."));
        }

        if (request.Progress is SalesOrderProgress progress && !Enum.IsDefined(progress))
        {
            refusals.Add((nameof(SalesOrderTrackReportRequest.Progress), "Progress must be one of the options offered."));
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
    public async Task<SalesOrderTrackReportResultDto> RunAsync(SalesOrderTrackReportRequest request, CancellationToken ct)
    {
        DateOnly start = request.FromDate!.Value;
        DateOnly end = request.ToDate!.Value;
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

        IQueryable<SalesOrderRead> orders = Orders(_db.SalesOrders, request, start, end);
        IQueryable<OrderLineFigure> figures = Figures(orders, _db.SalesOrderDetails);
        IQueryable<OrderTrackRow> rows = Rows(orders, figures);

        // The tabs count every progress the other filters let through, so a tab never hides an order behind the one chosen.
        IQueryable<OrderTrackRow> counted = OutstandingOnly(rows, request.OutstandingOnly);
        List<ProgressCount> counts = await counted
            .GroupBy(r => r.Progress)
            .Select(g => new ProgressCount { Progress = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        IQueryable<OrderTrackRow> matching = WithProgress(counted, request.Progress);

        OrderTrackRow totals = await matching
            .GroupBy(r => 1)
            .Select(g => new OrderTrackRow
            {
                OrderedValue = g.Sum(r => r.OrderedValue),
                DeliveredValue = g.Sum(r => r.DeliveredValue),
                InvoicedValue = g.Sum(r => r.InvoicedValue),
            })
            .FirstOrDefaultAsync(ct) ?? new OrderTrackRow();

        int matches = await matching.CountAsync(ct);

        List<OrderTrackRow> page = await matching
            .OrderByDescending(r => r.DocumentDate)
            .ThenByDescending(r => r.SalesOrderId)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        long[] orderIds = [.. page.Select(r => r.SalesOrderId)];
        List<LineRow> lines = orderIds.Length == 0 ? [] : await Lines(_db.SalesOrders, _db.SalesOrderDetails, _db.Items, _db.UnitsOfMeasure, orderIds).ToListAsync(ct);
        List<RaisedInvoices> raised = orderIds.Length == 0 ? [] : await Raised(_db.Invoices, orderIds).ToListAsync(ct);

        long[] contactIds = [.. page.Select(r => r.ContactId).Distinct()];
        List<CustomerRef> customers = contactIds.Length == 0
            ? []
            : await _db.Contacts
                .Where(c => contactIds.Contains(c.ContactId))
                .Select(c => new CustomerRef { ContactId = c.ContactId, Code = c.ContactCode, Name = c.DisplayName })
                .ToListAsync(ct);

        return Compose(request, page, matches, lines, raised, customers, counts, totals, baseCurrency);
    }

    // --- builders: LINQ over the read models, so a test runs them over lists -------------

    /// <summary>The posted and voided sales orders dated in the period, narrowed by customer. A draft is not an order yet.</summary>
    public static IQueryable<SalesOrderRead> Orders(IQueryable<SalesOrderRead> orders, SalesOrderTrackReportRequest request, DateOnly start, DateOnly end)
    {
        IQueryable<SalesOrderRead> matching = orders.Where(o =>
            (o.Status == DocumentStatus.Posted || o.Status == DocumentStatus.Void) && o.DocumentDate >= start && o.DocumentDate <= end);

        return request.ContactId is long contact ? matching.Where(o => o.ContactId == contact) : matching;
    }

    /// <summary>
    /// One row for each line of those orders: its ordered, delivered and invoiced quantities, and its
    /// value before tax in base at the order's rate, shared out at the order's own price per unit: the
    /// delivered and invoiced shares are rounded, and what is outstanding is the ordered value less
    /// them, so each adds up exactly.
    /// </summary>
    public static IQueryable<OrderLineFigure> Figures(IQueryable<SalesOrderRead> orders, IQueryable<SalesOrderDetailRead> details) =>
        from d in details
        join o in orders on d.SalesOrderId equals o.SalesOrderId
        select new OrderLineFigure
        {
            SalesOrderId = o.SalesOrderId,
            Ordered = d.Quantity,
            Delivered = d.DeliveredQuantity,
            Invoiced = d.InvoicedQuantity,
            OrderedValue = Math.Round(d.TaxableAmount * o.ExchangeRate, 2),
            DeliveredValue = d.Quantity == 0m ? 0m : Math.Round(d.TaxableAmount * o.ExchangeRate * d.DeliveredQuantity / d.Quantity, 2),
            InvoicedValue = d.Quantity == 0m ? 0m : Math.Round(d.TaxableAmount * o.ExchangeRate * d.InvoicedQuantity / d.Quantity, 2),
        };

    /// <summary>
    /// One row for each order with its lines' values added up and its progress: cancelled when voided
    /// or cancelled; else invoiced when every line is billed in full; else closed short when it was
    /// closed with a line undelivered; else delivered when every line is delivered in full; else
    /// partly delivered when anything was; else open.
    /// </summary>
    public static IQueryable<OrderTrackRow> Rows(IQueryable<SalesOrderRead> orders, IQueryable<OrderLineFigure> figures)
    {
        IQueryable<OrderSum> sums = figures
            .GroupBy(f => f.SalesOrderId)
            .Select(g => new OrderSum
            {
                SalesOrderId = g.Key,
                UndeliveredLines = g.Count(f => f.Delivered < f.Ordered),
                DeliveredLines = g.Count(f => f.Delivered > 0m),
                UnbilledLines = g.Count(f => f.Invoiced < f.Ordered),
                OrderedValue = g.Sum(f => f.OrderedValue),
                DeliveredValue = g.Sum(f => f.DeliveredValue),
                InvoicedValue = g.Sum(f => f.InvoicedValue),
            });

        return from o in orders
               join s in sums on o.SalesOrderId equals s.SalesOrderId
               select new OrderTrackRow
               {
                   SalesOrderId = o.SalesOrderId,
                   DocumentNo = o.DocumentNo,
                   DocumentDate = o.DocumentDate,
                   DeliveryDate = o.DeliveryDate,
                   ContactId = o.ContactId,
                   CurrencyCode = o.CurrencyCode,
                   OrderedValue = s.OrderedValue,
                   DeliveredValue = s.DeliveredValue,
                   InvoicedValue = s.InvoicedValue,
                   Progress = o.Status == DocumentStatus.Void || o.FulfilmentStatus == FulfilmentStatus.Cancelled
                       ? SalesOrderProgress.Cancelled
                       : s.UnbilledLines == 0
                           ? SalesOrderProgress.Invoiced
                           : o.FulfilmentStatus == FulfilmentStatus.Closed && s.UndeliveredLines > 0
                               ? SalesOrderProgress.ClosedShort
                               : s.UndeliveredLines == 0
                                   ? SalesOrderProgress.Delivered
                                   : s.DeliveredLines > 0
                                       ? SalesOrderProgress.PartlyDelivered
                                       : SalesOrderProgress.Open,
               };
    }

    /// <summary>Only the orders with something still to deliver or bill when asked: open, partly delivered or delivered and not invoiced. LINQ only.</summary>
    public static IQueryable<OrderTrackRow> OutstandingOnly(IQueryable<OrderTrackRow> rows, bool outstandingOnly) =>
        outstandingOnly
            ? rows.Where(r => r.Progress == SalesOrderProgress.Open || r.Progress == SalesOrderProgress.PartlyDelivered || r.Progress == SalesOrderProgress.Delivered)
            : rows;

    /// <summary>The rows at the progress asked for; with none asked for, every row but the cancelled ones. LINQ only.</summary>
    public static IQueryable<OrderTrackRow> WithProgress(IQueryable<OrderTrackRow> rows, SalesOrderProgress? progress) =>
        progress is SalesOrderProgress chosen
            ? rows.Where(r => r.Progress == chosen)
            : rows.Where(r => r.Progress != SalesOrderProgress.Cancelled);

    /// <summary>
    /// The lines of the given orders with their items and units, each with its figures worked out the
    /// way <see cref="Figures"/> does, so a line's values add up to its order's.
    /// </summary>
    public static IQueryable<LineRow> Lines(
        IQueryable<SalesOrderRead> orders,
        IQueryable<SalesOrderDetailRead> details,
        IQueryable<ItemRead> items,
        IQueryable<UnitOfMeasureRead> units,
        IReadOnlyCollection<long> orderIds) =>
        from d in details
        join o in orders on d.SalesOrderId equals o.SalesOrderId
        join iv in items on d.ItemId equals (long?)iv.ItemId into itemed
        from it in itemed.DefaultIfEmpty()
        join uv in units on d.UomId equals (long?)uv.UomId into united
        from un in united.DefaultIfEmpty()
        where orderIds.Contains(o.SalesOrderId)
        select new LineRow
        {
            SalesOrderId = o.SalesOrderId,
            LineNumber = d.LineNumber,
            ItemId = d.ItemId,
            ItemCode = it == null ? null : it.ItemCode,
            ItemName = it == null ? null : it.ItemName,
            Description = d.Description,
            UnitCode = un == null ? null : un.UomCode,
            Ordered = d.Quantity,
            Delivered = d.DeliveredQuantity,
            Invoiced = d.InvoicedQuantity,
            OrderedValue = Math.Round(d.TaxableAmount * o.ExchangeRate, 2),
            DeliveredValue = d.Quantity == 0m ? 0m : Math.Round(d.TaxableAmount * o.ExchangeRate * d.DeliveredQuantity / d.Quantity, 2),
            InvoicedValue = d.Quantity == 0m ? 0m : Math.Round(d.TaxableAmount * o.ExchangeRate * d.InvoicedQuantity / d.Quantity, 2),
        };

    /// <summary>
    /// What the posted invoices raised from each of the given orders came to before tax in base, and
    /// how many there are: the actual billing, at the prices the invoices were raised at.
    /// </summary>
    public static IQueryable<RaisedInvoices> Raised(IQueryable<InvoiceRead> invoices, IReadOnlyCollection<long> orderIds) =>
        invoices
            .Where(i => i.SalesOrderId != null && orderIds.Contains(i.SalesOrderId.Value) && i.Status == DocumentStatus.Posted)
            .GroupBy(i => i.SalesOrderId!.Value)
            .Select(g => new RaisedInvoices
            {
                SalesOrderId = g.Key,
                Count = g.Count(),
                Billed = g.Sum(i => Math.Round(i.TaxableAmount * i.ExchangeRate, 2)),
            });

    // --- compose: pure, so a test needs no database --------------------------------------

    /// <summary>
    /// The page's orders with their customers, lines and what was raised against them, how many orders
    /// are at each progress, and the totals. Every progress has a count, a nought included. What is
    /// outstanding is ordered less delivered or invoiced, on each line and each order. Pure.
    /// </summary>
    public static SalesOrderTrackReportResultDto Compose(
        SalesOrderTrackReportRequest request,
        IReadOnlyList<OrderTrackRow> page,
        int totalOrders,
        IReadOnlyList<LineRow> lines,
        IReadOnlyList<RaisedInvoices> raised,
        IReadOnlyList<CustomerRef> customers,
        IReadOnlyList<ProgressCount> counts,
        OrderTrackRow totals,
        string baseCurrency)
    {
        Dictionary<long, CustomerRef> names = customers.GroupBy(c => c.ContactId).ToDictionary(g => g.Key, g => g.First());
        Dictionary<long, RaisedInvoices> billed = raised.GroupBy(r => r.SalesOrderId).ToDictionary(g => g.Key, g => g.First());
        ILookup<long, LineRow> byOrder = lines.ToLookup(l => l.SalesOrderId);

        Dictionary<SalesOrderProgress, int> progressCounts = Enum.GetValues<SalesOrderProgress>().ToDictionary(p => p, _ => 0);
        foreach (ProgressCount count in counts)
        {
            progressCounts[count.Progress] = count.Count;
        }

        return new SalesOrderTrackReportResultDto
        {
            FromDate = request.FromDate!.Value,
            ToDate = request.ToDate!.Value,
            BaseCurrencyCode = baseCurrency,
            Orders =
            [
                .. page.Select(r =>
                {
                    CustomerRef? customer = names.GetValueOrDefault(r.ContactId);
                    RaisedInvoices? invoices = billed.GetValueOrDefault(r.SalesOrderId);
                    return new SalesOrderTrackOrderDto
                    {
                        SalesOrderId = r.SalesOrderId,
                        DocumentNo = r.DocumentNo,
                        DocumentDate = r.DocumentDate,
                        DeliveryDate = r.DeliveryDate,
                        ContactId = r.ContactId,
                        ContactCode = customer?.Code ?? string.Empty,
                        ContactName = customer?.Name ?? $"Customer {r.ContactId}",
                        CurrencyCode = r.CurrencyCode,
                        Progress = r.Progress,
                        Lines = [.. byOrder[r.SalesOrderId].OrderBy(l => l.LineNumber).Select(LineOf)],
                        OrderedValue = r.OrderedValue,
                        DeliveredValue = r.DeliveredValue,
                        InvoicedValue = r.InvoicedValue,
                        ToDeliverValue = r.OrderedValue - r.DeliveredValue,
                        ToBillValue = r.OrderedValue - r.InvoicedValue,
                        InvoicesRaised = invoices?.Count ?? 0,
                        Billed = invoices?.Billed ?? 0m,
                    };
                }),
            ],
            TotalOrders = totalOrders,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            ProgressCounts = progressCounts,
            Totals = new SalesOrderTrackTotalsDto
            {
                OrderedValue = totals.OrderedValue,
                DeliveredValue = totals.DeliveredValue,
                InvoicedValue = totals.InvoicedValue,
                ToDeliverValue = totals.OrderedValue - totals.DeliveredValue,
                ToBillValue = totals.OrderedValue - totals.InvoicedValue,
            },
        };
    }

    private static SalesOrderTrackLineDto LineOf(LineRow l) =>
        new()
        {
            LineNumber = l.LineNumber,
            ItemId = l.ItemId,
            ItemCode = l.ItemCode,
            ItemName = l.ItemName ?? l.Description ?? string.Empty,
            UnitCode = l.UnitCode,
            Ordered = l.Ordered,
            Delivered = l.Delivered,
            Invoiced = l.Invoiced,
            ToDeliver = l.Ordered - l.Delivered,
            ToBill = l.Ordered - l.Invoiced,
            OrderedValue = l.OrderedValue,
            DeliveredValue = l.DeliveredValue,
            InvoicedValue = l.InvoicedValue,
            ToDeliverValue = l.OrderedValue - l.DeliveredValue,
            ToBillValue = l.OrderedValue - l.InvoicedValue,
        };
}

/// <summary>A line of an order with its quantities and its values in base currency.</summary>
public sealed class OrderLineFigure
{
    public long SalesOrderId { get; set; }

    public decimal Ordered { get; set; }

    public decimal Delivered { get; set; }

    public decimal Invoiced { get; set; }

    public decimal OrderedValue { get; set; }

    public decimal DeliveredValue { get; set; }

    public decimal InvoicedValue { get; set; }
}

/// <summary>An order's lines counted and its values added up.</summary>
public sealed class OrderSum
{
    public long SalesOrderId { get; set; }

    /// <summary>Lines with something left to deliver.</summary>
    public int UndeliveredLines { get; set; }

    /// <summary>Lines with something delivered.</summary>
    public int DeliveredLines { get; set; }

    /// <summary>Lines with something left to bill.</summary>
    public int UnbilledLines { get; set; }

    public decimal OrderedValue { get; set; }

    public decimal DeliveredValue { get; set; }

    public decimal InvoicedValue { get; set; }
}

/// <summary>An order with its values and its progress.</summary>
public sealed class OrderTrackRow
{
    public long SalesOrderId { get; set; }

    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    public DateOnly? DeliveryDate { get; set; }

    public long ContactId { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal OrderedValue { get; set; }

    public decimal DeliveredValue { get; set; }

    public decimal InvoicedValue { get; set; }

    public SalesOrderProgress Progress { get; set; }
}

/// <summary>A line of an order with its item, unit, quantities and values.</summary>
public sealed class LineRow
{
    public long SalesOrderId { get; set; }

    public int LineNumber { get; set; }

    public long? ItemId { get; set; }

    public string? ItemCode { get; set; }

    public string? ItemName { get; set; }

    public string? Description { get; set; }

    public string? UnitCode { get; set; }

    public decimal Ordered { get; set; }

    public decimal Delivered { get; set; }

    public decimal Invoiced { get; set; }

    public decimal OrderedValue { get; set; }

    public decimal DeliveredValue { get; set; }

    public decimal InvoicedValue { get; set; }
}

/// <summary>What the posted invoices raised from an order came to.</summary>
public sealed class RaisedInvoices
{
    public long SalesOrderId { get; set; }

    public int Count { get; set; }

    public decimal Billed { get; set; }
}

/// <summary>How many orders are at a progress.</summary>
public sealed class ProgressCount
{
    public SalesOrderProgress Progress { get; set; }

    public int Count { get; set; }
}

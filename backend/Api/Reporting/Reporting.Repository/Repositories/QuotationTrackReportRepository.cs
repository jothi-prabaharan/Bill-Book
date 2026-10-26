using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Quotation Track Report (TK-231): each quote's outcome, a page of quotes at a time, and the
/// conversion rate over the period. A quote is one posted (or voided) quote dated in the period; a
/// draft is not an offer yet.
///
/// <b>The outcome is read from what exists, never from a flag Sales keeps for it</b>: a quote with a
/// posted invoice raised from it is converted to an invoice (an invoice raised from a sales order that
/// came from the quote carries the quote too, so the whole chain counts); else one with a posted sales
/// order is converted to an order; else the customer's answer on the portal, declined or accepted;
/// else expired when it is past its valid-until date; else open. A voided quote is void whatever else
/// is true of it. The document a conversion names is a posted one, the invoice rather than the order
/// when both exist, and the earliest of its kind, so every converted quote names a document that exists.
///
/// <b>Outcomes are as they stand today</b>: a conversion that happened after the period still counts,
/// and a quote becomes expired the day after it lapses, whatever period is asked about. The conversion
/// rate is converted over raised, by count and by value in base currency, over the quotes the customer
/// filter leaves, with void ones counted in neither.
///
/// <b>Everything but the page's own conversion documents is worked out in the database</b>: one grouped
/// query gives every outcome's count and value (the tabs, the summary and the paging total), and the
/// page is one more.
/// </summary>
public sealed class QuotationTrackReportRepository
{
    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;
    private readonly TimeProvider _clock;

    public QuotationTrackReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency, TimeProvider clock)
    {
        _db = db;
        _currency = currency;
        _clock = clock;
    }

    /// <summary>The refusals a request needs beyond both dates being given, with the field each belongs to. Pure.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(QuotationTrackReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from)
        {
            refusals.Add((nameof(QuotationTrackReportRequest.ToDate), "To date cannot be before the from date."));
        }

        if (request.Outcome is QuoteOutcome outcome && !Enum.IsDefined(outcome))
        {
            refusals.Add((nameof(QuotationTrackReportRequest.Outcome), "Status must be one of the options offered."));
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
    public async Task<QuotationTrackReportResultDto> RunAsync(QuotationTrackReportRequest request, CancellationToken ct)
    {
        DateOnly start = request.FromDate!.Value;
        DateOnly end = request.ToDate!.Value;
        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

        IQueryable<QuoteRead> quotes = Quotes(_db.Quotes, request, start, end);
        IQueryable<QuoteTrackRow> rows = Rows(quotes, _db.Invoices, _db.SalesOrders, today);

        // The tabs count every outcome the customer filter lets through, so a tab never hides a quote behind the one chosen.
        List<OutcomeFigure> figures = await Figures(rows).ToListAsync(ct);

        List<QuoteTrackRow> page = await WithOutcome(rows, request.Outcome)
            .OrderByDescending(r => r.DocumentDate)
            .ThenByDescending(r => r.QuoteId)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        long[] quoteIds = [.. page.Select(r => r.QuoteId)];
        List<QuoteConversionRow> conversions = quoteIds.Length == 0
            ? []
            : await Conversions(_db.Invoices, _db.SalesOrders, quoteIds).ToListAsync(ct);

        long[] contactIds = [.. page.Select(r => r.ContactId).Distinct()];
        List<CustomerRef> customers = contactIds.Length == 0
            ? []
            : await _db.Contacts
                .Where(c => contactIds.Contains(c.ContactId))
                .Select(c => new CustomerRef { ContactId = c.ContactId, Code = c.ContactCode, Name = c.DisplayName })
                .ToListAsync(ct);

        return Compose(request, page, conversions, customers, figures, baseCurrency);
    }

    // --- builders: LINQ over the read models, so a test runs them over lists -------------

    /// <summary>The posted and voided quotes dated in the period, narrowed by customer. A draft is not an offer yet.</summary>
    public static IQueryable<QuoteRead> Quotes(IQueryable<QuoteRead> quotes, QuotationTrackReportRequest request, DateOnly start, DateOnly end)
    {
        IQueryable<QuoteRead> matching = quotes.Where(q =>
            (q.Status == DocumentStatus.Posted || q.Status == DocumentStatus.Void) && q.DocumentDate >= start && q.DocumentDate <= end);

        return request.ContactId is long contact ? matching.Where(q => q.ContactId == contact) : matching;
    }

    /// <summary>
    /// One row for each of those quotes with its outcome: void when voided; else converted to an
    /// invoice when a posted one was raised from it; else converted to an order when a posted sales
    /// order was; else declined or accepted as the customer answered; else expired when its valid-until
    /// date is before <paramref name="today"/>; else open. Its value is the total in base currency.
    /// </summary>
    public static IQueryable<QuoteTrackRow> Rows(
        IQueryable<QuoteRead> quotes,
        IQueryable<InvoiceRead> invoices,
        IQueryable<SalesOrderRead> orders,
        DateOnly today) =>
        from q in quotes
        select new QuoteTrackRow
        {
            QuoteId = q.QuoteId,
            DocumentNo = q.DocumentNo,
            DocumentDate = q.DocumentDate,
            ValidUntil = q.ValidUntil,
            ContactId = q.ContactId,
            CurrencyCode = q.CurrencyCode,
            TotalInCurrency = q.TotalAmount,
            Total = Math.Round(q.TotalAmount * q.ExchangeRate, 2),
            Outcome = q.Status == DocumentStatus.Void
                ? QuoteOutcome.Void
                : invoices.Any(i => i.QuoteId == q.QuoteId && i.Status == DocumentStatus.Posted)
                    ? QuoteOutcome.ConvertedToInvoice
                    : orders.Any(o => o.QuoteId == q.QuoteId && o.Status == DocumentStatus.Posted)
                        ? QuoteOutcome.ConvertedToOrder
                        : q.CustomerResponse == QuoteResponse.Rejected
                            ? QuoteOutcome.Declined
                            : q.CustomerResponse == QuoteResponse.Accepted
                                ? QuoteOutcome.Accepted
                                : q.ValidUntil < today
                                    ? QuoteOutcome.Expired
                                    : QuoteOutcome.Open,
        };

    /// <summary>How many quotes, and what they add up to in base currency, there are at each outcome. LINQ only.</summary>
    public static IQueryable<OutcomeFigure> Figures(IQueryable<QuoteTrackRow> rows) =>
        rows
            .GroupBy(r => r.Outcome)
            .Select(g => new OutcomeFigure { Outcome = g.Key, Count = g.Count(), Value = g.Sum(r => r.Total) });

    /// <summary>The rows at the outcome asked for; with none asked for, every row but the void ones. LINQ only.</summary>
    public static IQueryable<QuoteTrackRow> WithOutcome(IQueryable<QuoteTrackRow> rows, QuoteOutcome? outcome) =>
        outcome is QuoteOutcome chosen
            ? rows.Where(r => r.Outcome == chosen)
            : rows.Where(r => r.Outcome != QuoteOutcome.Void);

    /// <summary>
    /// The posted invoices and sales orders raised from each of the given quotes, one row each, for the
    /// page to name the document a quote became. Posted ones only, as the outcome counts them.
    /// </summary>
    public static IQueryable<QuoteConversionRow> Conversions(
        IQueryable<InvoiceRead> invoices,
        IQueryable<SalesOrderRead> orders,
        IReadOnlyCollection<long> quoteIds) =>
        invoices
            .Where(i => i.QuoteId != null && quoteIds.Contains(i.QuoteId.Value) && i.Status == DocumentStatus.Posted)
            .Select(i => new QuoteConversionRow
            {
                QuoteId = i.QuoteId!.Value,
                TypeCode = i.TransactionTypeCode,
                DocumentId = i.InvoiceId,
                DocumentNo = i.DocumentNo,
                DocumentDate = i.DocumentDate,
            })
            .Concat(orders
                .Where(o => o.QuoteId != null && quoteIds.Contains(o.QuoteId.Value) && o.Status == DocumentStatus.Posted)
                .Select(o => new QuoteConversionRow
                {
                    QuoteId = o.QuoteId!.Value,
                    TypeCode = o.TransactionTypeCode,
                    DocumentId = o.SalesOrderId,
                    DocumentNo = o.DocumentNo,
                    DocumentDate = o.DocumentDate,
                }));

    // --- compose: pure, so a test needs no database --------------------------------------

    /// <summary>
    /// The page's quotes with their customers and what they became, how many quotes are at each outcome,
    /// and the summary. Every outcome has a count, a nought included. A quote is named with the invoice
    /// raised from it, else the earliest sales order, and the days it took are counted to that document.
    /// Raised is every outcome but void; converted is the two converted outcomes. Pure.
    /// </summary>
    public static QuotationTrackReportResultDto Compose(
        QuotationTrackReportRequest request,
        IReadOnlyList<QuoteTrackRow> page,
        IReadOnlyList<QuoteConversionRow> conversions,
        IReadOnlyList<CustomerRef> customers,
        IReadOnlyList<OutcomeFigure> figures,
        string baseCurrency)
    {
        Dictionary<long, CustomerRef> names = customers.GroupBy(c => c.ContactId).ToDictionary(g => g.Key, g => g.First());

        // The invoice before the order, then the earliest, then the lowest id so the same one is named every time.
        Dictionary<long, QuoteConversionRow> became = conversions
            .GroupBy(c => c.QuoteId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(c => c.TypeCode == "SOR" ? 1 : 0).ThenBy(c => c.DocumentDate).ThenBy(c => c.DocumentId).First());

        Dictionary<QuoteOutcome, int> counts = Enum.GetValues<QuoteOutcome>().ToDictionary(o => o, _ => 0);
        Dictionary<QuoteOutcome, decimal> values = Enum.GetValues<QuoteOutcome>().ToDictionary(o => o, _ => 0m);
        foreach (OutcomeFigure figure in figures)
        {
            counts[figure.Outcome] = figure.Count;
            values[figure.Outcome] = figure.Value;
        }

        QuoteOutcome[] raised = [.. Enum.GetValues<QuoteOutcome>().Where(o => o != QuoteOutcome.Void)];
        QuoteOutcome[] converted = [QuoteOutcome.ConvertedToOrder, QuoteOutcome.ConvertedToInvoice];
        QuoteOutcome[] listed = request.Outcome is QuoteOutcome chosen ? [chosen] : raised;

        int raisedCount = raised.Sum(o => counts[o]);
        decimal raisedValue = raised.Sum(o => values[o]);
        int convertedCount = converted.Sum(o => counts[o]);
        decimal convertedValue = converted.Sum(o => values[o]);

        return new QuotationTrackReportResultDto
        {
            FromDate = request.FromDate!.Value,
            ToDate = request.ToDate!.Value,
            BaseCurrencyCode = baseCurrency,
            Quotes =
            [
                .. page.Select(r =>
                {
                    CustomerRef? customer = names.GetValueOrDefault(r.ContactId);
                    QuoteConversionRow? document = became.GetValueOrDefault(r.QuoteId);
                    bool isConverted = r.Outcome is QuoteOutcome.ConvertedToOrder or QuoteOutcome.ConvertedToInvoice;
                    return new QuotationTrackRowDto
                    {
                        QuoteId = r.QuoteId,
                        DocumentNo = r.DocumentNo,
                        DocumentDate = r.DocumentDate,
                        ValidUntil = r.ValidUntil,
                        ContactId = r.ContactId,
                        ContactCode = customer?.Code ?? string.Empty,
                        ContactName = customer?.Name ?? $"Customer {r.ContactId}",
                        CurrencyCode = r.CurrencyCode,
                        TotalInCurrency = r.TotalInCurrency,
                        Total = r.Total,
                        Outcome = r.Outcome,
                        ConvertedTo = isConverted && document is not null
                            ? new QuoteConversionDto
                            {
                                TypeCode = document.TypeCode,
                                DocumentId = document.DocumentId,
                                DocumentNo = document.DocumentNo,
                                DocumentDate = document.DocumentDate,
                            }
                            : null,
                        DaysToConvert = isConverted && document is not null
                            ? Math.Max(0, document.DocumentDate.DayNumber - r.DocumentDate.DayNumber)
                            : null,
                    };
                }),
            ],
            TotalQuotes = listed.Sum(o => counts[o]),
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            OutcomeCounts = counts,
            TotalValue = listed.Sum(o => values[o]),
            Summary = new QuotationTrackSummaryDto
            {
                Raised = raisedCount,
                RaisedValue = raisedValue,
                Converted = convertedCount,
                ConvertedValue = convertedValue,
                ConversionRateByCount = raisedCount == 0 ? null : Rate(convertedCount, raisedCount),
                ConversionRateByValue = raisedValue == 0m ? null : Rate(convertedValue, raisedValue),
            },
        };
    }

    /// <summary>A part as a percentage of its whole, to one decimal place.</summary>
    private static decimal Rate(decimal part, decimal whole) =>
        Math.Round(part * 100m / whole, 1, MidpointRounding.AwayFromZero);
}

/// <summary>A quote with its value and its outcome.</summary>
public sealed class QuoteTrackRow
{
    public long QuoteId { get; set; }

    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    public DateOnly ValidUntil { get; set; }

    public long ContactId { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal TotalInCurrency { get; set; }

    public decimal Total { get; set; }

    public QuoteOutcome Outcome { get; set; }
}

/// <summary>An invoice or a sales order raised from a quote.</summary>
public sealed class QuoteConversionRow
{
    public long QuoteId { get; set; }

    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }
}

/// <summary>How many quotes are at an outcome, and what they add up to in base currency.</summary>
public sealed class OutcomeFigure
{
    public QuoteOutcome Outcome { get; set; }

    public int Count { get; set; }

    public decimal Value { get; set; }
}

using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Invoice Track Report (TK-226): where each sales invoice is in its life, a page at a time.
/// It is the Receivable Invoice Summary's invoices (posted and voided, with what was received and
/// what is due as at the To date, from that report's own builders) with the drafts and the
/// ready-to-post invoices of the period beside them, and for each the dates of its steps: when it
/// was posted, its due date, and the last settlement of it.
///
/// <b>A draft is not in the books.</b> It owes nothing and has been paid nothing, so its received
/// and due are nought, and the totals add up posted invoices that still stand (not a draft, not a
/// voided one); otherwise the total less the received would not be the due. The tab counts and the totals cover every invoice the filters
/// match, worked out in the database; only the posted-on and last-payment dates of the page's own
/// invoices are read after it, since they decide neither a status nor a total.
/// </summary>
public sealed class InvoiceTrackReportRepository
{
    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public InvoiceTrackReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>The refusals a request needs beyond both dates being given, with the field each belongs to. Pure.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(InvoiceTrackReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from)
        {
            refusals.Add((nameof(InvoiceTrackReportRequest.ToDate), "To date cannot be before the from date."));
        }

        if (request.Status is InvoiceTrackStatus status && !Enum.IsDefined(status))
        {
            refusals.Add((nameof(InvoiceTrackReportRequest.Status), "Status must be one of the statuses offered."));
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
    public async Task<InvoiceTrackReportResultDto> RunAsync(InvoiceTrackReportRequest request, CancellationToken ct)
    {
        DateOnly start = request.FromDate!.Value;
        DateOnly end = request.ToDate!.Value;
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

        IQueryable<InvoiceTrackEntry> rows = Entries(_db.Invoices, _db.CreditNotes, _db.TransactionRatios, request, start, end);

        // The tabs count every status the other filters let through, so a tab never hides an invoice behind the one chosen.
        List<TrackStatusCount> counts = await rows
            .GroupBy(r => r.Status)
            .Select(g => new TrackStatusCount { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        IQueryable<InvoiceTrackEntry> matching = WithStatus(rows, request.Status);

        InvoiceTrackTotalsDto totals = await Posted(matching)
            .GroupBy(r => 1)
            .Select(g => new InvoiceTrackTotalsDto { Total = g.Sum(r => r.Total), Received = g.Sum(r => r.Received), Due = g.Sum(r => r.Due) })
            .FirstOrDefaultAsync(ct) ?? new InvoiceTrackTotalsDto();

        int matches = await matching.CountAsync(ct);

        List<InvoiceTrackEntry> page = await matching
            .OrderByDescending(r => r.DocumentDate)
            .ThenByDescending(r => r.DocumentId)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        List<long> contactIds = [.. page.Select(r => r.ContactId).Distinct()];
        List<CustomerRef> customers = contactIds.Count == 0
            ? []
            : await _db.Contacts
                .Where(c => contactIds.Contains(c.ContactId))
                .Select(c => new CustomerRef { ContactId = c.ContactId, Code = c.ContactCode, Name = c.DisplayName })
                .ToListAsync(ct);

        List<long> invoiceIds = [.. page.Select(r => r.DocumentId)];
        List<PostedStamp> stamps = invoiceIds.Count == 0
            ? []
            : await PostedStamps(_db.Invoices, invoiceIds).ToListAsync(ct);
        List<LastPayment> payments = invoiceIds.Count == 0
            ? []
            : await LastPayments(_db.TransactionRatios, invoiceIds, end).ToListAsync(ct);

        return Compose(request, page, matches, customers, stamps, payments, counts, totals, baseCurrency);
    }

    // --- builders: LINQ over the read models, so a test runs them over lists -------------

    /// <summary>
    /// Every invoice dated in the period for the filters: the posted and voided ones with what was
    /// received and what is due as at the date, from the Receivable Invoice Summary's builders (no
    /// credit notes), and the drafts and ready-to-post ones, which owe nothing. The customer filter
    /// applies to both.
    /// </summary>
    public static IQueryable<InvoiceTrackEntry> Entries(
        IQueryable<InvoiceRead> invoices,
        IQueryable<CreditNoteRead> creditNotes,
        IQueryable<TransactionRatioRead> ratios,
        InvoiceTrackReportRequest request,
        DateOnly start,
        DateOnly end)
    {
        var summary = new ReceivableInvoiceSummaryReportRequest { FromDate = start, ToDate = end, ContactId = request.ContactId };
        IQueryable<ReceivableInvoiceFigure> figures = ReceivableInvoiceSummaryReportRepository.Figures(invoices, creditNotes, summary, start, end);
        IQueryable<ReceivableInvoiceRow> booked = ReceivableInvoiceSummaryReportRepository.Rows(
            figures, ReceivableInvoiceSummaryReportRepository.ReceivedAmounts(ratios, figures, end), end);

        return Drafts(invoices, request.ContactId, start, end).Concat(Booked(booked));
    }

    /// <summary>The posted and voided rows as track entries, the summary's status read as this report's.</summary>
    public static IQueryable<InvoiceTrackEntry> Booked(IQueryable<ReceivableInvoiceRow> rows) =>
        from r in rows
        select new InvoiceTrackEntry
        {
            TypeCode = r.TypeCode,
            DocumentId = r.DocumentId,
            DocumentNo = r.DocumentNo,
            DocumentDate = r.DocumentDate,
            DueDate = r.DueDate,
            ContactId = r.ContactId,
            CurrencyCode = r.CurrencyCode,
            TotalSource = r.TotalSource,
            Total = r.Total,
            Received = r.Received,
            Due = r.Due,
            Status = r.Status == ReceivableInvoiceStatus.Void
                ? InvoiceTrackStatus.Void
                : r.Status == ReceivableInvoiceStatus.Paid
                    ? InvoiceTrackStatus.Paid
                    : r.Status == ReceivableInvoiceStatus.Overdue
                        ? InvoiceTrackStatus.Overdue
                        : r.Status == ReceivableInvoiceStatus.PartlyPaid
                            ? InvoiceTrackStatus.PartlyPaid
                            : InvoiceTrackStatus.AwaitingPayment,
        };

    /// <summary>The invoices dated in the period that are not in the books yet: drafts and ready to post.</summary>
    public static IQueryable<InvoiceTrackEntry> Drafts(IQueryable<InvoiceRead> invoices, long? contactId, DateOnly start, DateOnly end)
    {
        IQueryable<InvoiceTrackEntry> drafts =
            from i in invoices
            where (i.Status == DocumentStatus.Draft || i.Status == DocumentStatus.ReadyToPost)
                && i.DocumentDate >= start && i.DocumentDate <= end
            select new InvoiceTrackEntry
            {
                TypeCode = i.TransactionTypeCode,
                DocumentId = i.InvoiceId,
                DocumentNo = i.DocumentNo,
                DocumentDate = i.DocumentDate,
                DueDate = i.DueDate,
                ContactId = i.ContactId,
                CurrencyCode = i.CurrencyCode,
                TotalSource = i.TotalAmount,
                Total = i.TotalAmountBase,
                Received = 0m,
                Due = 0m,
                Status = i.Status == DocumentStatus.Draft ? InvoiceTrackStatus.Draft : InvoiceTrackStatus.ReadyToPost,
            };

        return contactId is long contact ? drafts.Where(d => d.ContactId == contact) : drafts;
    }

    /// <summary>The entries with the status asked for; with none asked for, every entry but the void ones. LINQ only.</summary>
    public static IQueryable<InvoiceTrackEntry> WithStatus(IQueryable<InvoiceTrackEntry> rows, InvoiceTrackStatus? status) =>
        status is InvoiceTrackStatus chosen
            ? rows.Where(r => r.Status == chosen)
            : rows.Where(r => r.Status != InvoiceTrackStatus.Void);

    /// <summary>
    /// The entries the totals add up: posted invoices that still stand. A draft or one ready to post
    /// is not in the books yet and a voided one has been withdrawn.
    /// </summary>
    public static IQueryable<InvoiceTrackEntry> Posted(IQueryable<InvoiceTrackEntry> rows) =>
        rows.Where(r => r.Status != InvoiceTrackStatus.Draft && r.Status != InvoiceTrackStatus.ReadyToPost && r.Status != InvoiceTrackStatus.Void);

    /// <summary>When each of the given invoices was posted. The invoice id is unique across invoices and POS sales.</summary>
    public static IQueryable<PostedStamp> PostedStamps(IQueryable<InvoiceRead> invoices, IReadOnlyCollection<long> invoiceIds) =>
        invoices
            .Where(i => invoiceIds.Contains(i.InvoiceId))
            .Select(i => new PostedStamp { DocumentId = i.InvoiceId, PostedAt = i.PostedAt });

    /// <summary>
    /// The latest settlement of each of the given invoices dated on or before the date and not
    /// voided: any row of <c>acc.TransactionRatios</c> that names the invoice at its target end, a
    /// receipt or a credit note or an advance applied to it.
    /// </summary>
    public static IQueryable<LastPayment> LastPayments(IQueryable<TransactionRatioRead> ratios, IReadOnlyCollection<long> invoiceIds, DateOnly asAt) =>
        ratios
            .Where(t => !t.IsVoided && t.AllocationDate <= asAt
                && (t.TargetTransactionTypeCode == "INV" || t.TargetTransactionTypeCode == "POS")
                && invoiceIds.Contains(t.TargetTransactionId))
            .GroupBy(t => new { t.TargetTransactionTypeCode, t.TargetTransactionId })
            .Select(g => new LastPayment
            {
                TypeCode = g.Key.TargetTransactionTypeCode,
                DocumentId = g.Key.TargetTransactionId,
                Date = g.Max(t => t.AllocationDate),
            });

    // --- compose: pure, so a test needs no database --------------------------------------

    /// <summary>
    /// The page's rows with their customers' names, posted-on and last-payment dates, how many
    /// invoices have each status and the totals. Every status has a count, a nought included. Days
    /// overdue are given only to an overdue invoice, counted from its due date to the To date. Pure.
    /// </summary>
    public static InvoiceTrackReportResultDto Compose(
        InvoiceTrackReportRequest request,
        IReadOnlyList<InvoiceTrackEntry> page,
        int totalInvoices,
        IReadOnlyList<CustomerRef> customers,
        IReadOnlyList<PostedStamp> stamps,
        IReadOnlyList<LastPayment> payments,
        IReadOnlyList<TrackStatusCount> counts,
        InvoiceTrackTotalsDto totals,
        string baseCurrency)
    {
        DateOnly end = request.ToDate!.Value;
        Dictionary<long, CustomerRef> names = customers.GroupBy(c => c.ContactId).ToDictionary(g => g.Key, g => g.First());
        Dictionary<long, DateTimeOffset?> posted = stamps.GroupBy(s => s.DocumentId).ToDictionary(g => g.Key, g => g.First().PostedAt);
        Dictionary<(string, long), DateOnly> last = payments.GroupBy(p => (p.TypeCode, p.DocumentId)).ToDictionary(g => g.Key, g => g.Max(p => p.Date));

        Dictionary<InvoiceTrackStatus, int> statusCounts = Enum.GetValues<InvoiceTrackStatus>().ToDictionary(s => s, _ => 0);
        foreach (TrackStatusCount count in counts)
        {
            statusCounts[count.Status] = count.Count;
        }

        return new InvoiceTrackReportResultDto
        {
            FromDate = request.FromDate!.Value,
            ToDate = end,
            BaseCurrencyCode = baseCurrency,
            Invoices =
            [
                .. page.Select(r =>
                {
                    CustomerRef? customer = names.GetValueOrDefault(r.ContactId);
                    return new InvoiceTrackRowDto
                    {
                        TypeCode = r.TypeCode,
                        DocumentId = r.DocumentId,
                        DocumentNo = r.DocumentNo,
                        DocumentDate = r.DocumentDate,
                        PostedAt = posted.GetValueOrDefault(r.DocumentId),
                        DueDate = r.DueDate,
                        LastPaymentDate = last.TryGetValue((r.TypeCode, r.DocumentId), out DateOnly paid) ? paid : null,
                        DaysOverdue = r.Status == InvoiceTrackStatus.Overdue && r.DueDate is DateOnly due ? end.DayNumber - due.DayNumber : null,
                        ContactId = r.ContactId,
                        ContactCode = customer?.Code ?? string.Empty,
                        ContactName = customer?.Name ?? $"Customer {r.ContactId}",
                        CurrencyCode = r.CurrencyCode,
                        TotalInCurrency = r.TotalSource,
                        Total = r.Total,
                        Received = r.Received,
                        Due = r.Due,
                        Status = r.Status,
                    };
                }),
            ],
            TotalInvoices = totalInvoices,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            StatusCounts = statusCounts,
            Totals = totals,
        };
    }
}

/// <summary>An invoice with what was received, what is due and its status in the track report.</summary>
public sealed class InvoiceTrackEntry
{
    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public long ContactId { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal TotalSource { get; set; }

    public decimal Total { get; set; }

    public decimal Received { get; set; }

    public decimal Due { get; set; }

    public InvoiceTrackStatus Status { get; set; }
}

/// <summary>When an invoice was posted; null while it is not in the books.</summary>
public sealed class PostedStamp
{
    public long DocumentId { get; set; }

    public DateTimeOffset? PostedAt { get; set; }
}

/// <summary>The latest settlement date of an invoice.</summary>
public sealed class LastPayment
{
    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    public DateOnly Date { get; set; }
}

/// <summary>How many invoices have a status.</summary>
public sealed class TrackStatusCount
{
    public InvoiceTrackStatus Status { get; set; }

    public int Count { get; set; }
}

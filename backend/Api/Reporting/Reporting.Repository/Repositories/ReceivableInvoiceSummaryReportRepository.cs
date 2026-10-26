using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Receivable Invoice Summary (TK-224): every posted sales invoice dated in a period with
/// its taxable value, tax and total, what has been received and what is still due, a page at a
/// time, with how many have each status.
///
/// <b>Received and due are as at the To date</b>, not as at today, so a report for a past period
/// reads the same when it is run again. What was received is read from <c>acc.TransactionRatios</c>
/// that are not voided and were effective on or before that date, the way the Settle documents
/// workspace and the Aged Receivables reports read it: a receipt, or a credit note or advance
/// applied with Allocate, taken off the invoice it settled. <b>It is one figure in base
/// currency</b>: a money document's line is in the document's own currency, so it is taken to
/// base at the invoice's own rate (its base total over its total); any other allocation is in
/// base already.
///
/// <b>Credit notes are not invoices.</b> They are listed only when asked for, as negatives, so
/// the total less received is the due on every row: for a credit note, received is what has been
/// applied to invoices and due is what has not. A voided document settles nothing and is listed
/// only when its status is asked for; drafts never are.
///
/// <b>The status is worked out in the database</b> (<see cref="Rows"/>), from the document's total,
/// what was received and its due date, so the tabs' counts and the totals cover every invoice the
/// filters match and not only the page in hand. Money is in base currency: the document's own
/// currency amounts are taken to base at the rate it was booked at, and the total is the one the
/// document stored.
/// </summary>
public sealed class ReceivableInvoiceSummaryReportRepository
{
    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public ReceivableInvoiceSummaryReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>The refusals a request needs beyond both dates being given, with the field each belongs to. Pure.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(ReceivableInvoiceSummaryReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from)
        {
            refusals.Add((nameof(ReceivableInvoiceSummaryReportRequest.ToDate), "To date cannot be before the from date."));
        }

        if (!string.IsNullOrWhiteSpace(request.CurrencyCode) && request.CurrencyCode.Trim().Length != 3)
        {
            refusals.Add((nameof(ReceivableInvoiceSummaryReportRequest.CurrencyCode), "Currency must be a 3-letter code."));
        }

        if (request.Status is ReceivableInvoiceStatus status && !Enum.IsDefined(status))
        {
            refusals.Add((nameof(ReceivableInvoiceSummaryReportRequest.Status), "Status must be one of the statuses offered."));
        }

        return refusals;
    }

    /// <summary>Whether a customer is on this branch; the query filter decides, so another branch's reads as none.</summary>
    public Task<bool> ContactExistsAsync(long contactId, CancellationToken ct) =>
        _db.Contacts.AnyAsync(c => c.ContactId == contactId, ct);

    /// <summary>The customers the filter offers, by name: the Aged Receivables list.</summary>
    public Task<List<AgedReceivablesCustomerChoiceDto>> CustomersAsync(CancellationToken ct) =>
        AgedReceivablesSummaryReportRepository.CustomerChoices(_db.Contacts).ToListAsync(ct);

    /// <summary>The currencies the branch has invoiced in, for the currency filter.</summary>
    public async Task<List<ReceivableInvoiceCurrencyChoiceDto>> CurrenciesAsync(CancellationToken ct)
    {
        List<string> invoices = await _db.Invoices
            .Where(i => i.Status == DocumentStatus.Posted || i.Status == DocumentStatus.Void)
            .Select(i => i.CurrencyCode)
            .Distinct()
            .ToListAsync(ct);

        List<string> credits = await _db.CreditNotes
            .Where(c => c.Status == DocumentStatus.Posted || c.Status == DocumentStatus.Void)
            .Select(c => c.CurrencyCode)
            .Distinct()
            .ToListAsync(ct);

        return
        [
            .. invoices.Concat(credits)
                .Select(c => c.Trim().ToUpperInvariant())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(c => c, StringComparer.Ordinal)
                .Select(c => new ReceivableInvoiceCurrencyChoiceDto { CurrencyCode = c }),
        ];
    }

    /// <summary>The report for a request the controller has already validated.</summary>
    public async Task<ReceivableInvoiceSummaryReportResultDto> RunAsync(ReceivableInvoiceSummaryReportRequest request, CancellationToken ct)
    {
        DateOnly start = request.FromDate!.Value;
        DateOnly end = request.ToDate!.Value;
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

        IQueryable<ReceivableInvoiceFigure> figures = Figures(_db.Invoices, _db.CreditNotes, request, start, end);
        IQueryable<ReceivableInvoiceRow> rows = Rows(figures, ReceivedAmounts(_db.TransactionRatios, figures, end), end);

        // The tabs count every status the other filters let through, so a tab never hides a document behind the one chosen.
        List<StatusCount> counts = await rows
            .GroupBy(r => r.Status)
            .Select(g => new StatusCount { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        IQueryable<ReceivableInvoiceRow> matching = WithStatus(rows, request.Status);

        ReceivableInvoiceSummaryTotalsDto totals = await matching
            .GroupBy(r => 1)
            .Select(g => new ReceivableInvoiceSummaryTotalsDto
            {
                Taxable = g.Sum(r => r.Taxable),
                Tax = g.Sum(r => r.Cgst + r.Sgst + r.Igst + r.Cess),
                Total = g.Sum(r => r.Total),
                Received = g.Sum(r => r.Received),
                Due = g.Sum(r => r.Due),
            })
            .FirstOrDefaultAsync(ct) ?? new ReceivableInvoiceSummaryTotalsDto();

        int matches = await matching.CountAsync(ct);

        List<ReceivableInvoiceRow> page = await matching
            .OrderByDescending(r => r.DocumentDate)
            .ThenByDescending(r => r.TypeCode)
            .ThenByDescending(r => r.DocumentId)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        List<long> ids = [.. page.Select(r => r.ContactId).Distinct()];
        List<CustomerRef> customers = ids.Count == 0
            ? []
            : await _db.Contacts
                .Where(c => ids.Contains(c.ContactId))
                .Select(c => new CustomerRef { ContactId = c.ContactId, Code = c.ContactCode, Name = c.DisplayName })
                .ToListAsync(ct);

        return Compose(request, page, matches, customers, counts, totals, baseCurrency);
    }

    // --- builders: LINQ over the read models, so a test runs them over lists -------------

    /// <summary>
    /// The posted and voided invoices dated in the period, and the credit notes too when asked,
    /// narrowed by customer and currency. Each is one row in base currency, signed: an invoice is
    /// positive and a credit note negative. A tax figure is taken to base at the rate the document
    /// was booked at; the total is the document's own base total.
    /// </summary>
    public static IQueryable<ReceivableInvoiceFigure> Figures(
        IQueryable<InvoiceRead> invoices,
        IQueryable<CreditNoteRead> creditNotes,
        ReceivableInvoiceSummaryReportRequest request,
        DateOnly start,
        DateOnly end)
    {
        IQueryable<ReceivableInvoiceFigure> figures =
            from i in invoices
            where (i.Status == DocumentStatus.Posted || i.Status == DocumentStatus.Void)
                && i.DocumentDate >= start && i.DocumentDate <= end
            select new ReceivableInvoiceFigure
            {
                TypeCode = i.TransactionTypeCode,
                DocumentId = i.InvoiceId,
                DocumentNo = i.DocumentNo,
                DocumentDate = i.DocumentDate,
                DueDate = i.DueDate,
                ContactId = i.ContactId,
                CurrencyCode = i.CurrencyCode,
                Sign = 1,
                IsVoid = i.Status == DocumentStatus.Void,
                Taxable = Math.Round(i.TaxableAmount * i.ExchangeRate, 2),
                Cgst = Math.Round(i.CgstAmount * i.ExchangeRate, 2),
                Sgst = Math.Round(i.SgstAmount * i.ExchangeRate, 2),
                Igst = Math.Round(i.IgstAmount * i.ExchangeRate, 2),
                Cess = Math.Round(i.CessAmount * i.ExchangeRate, 2),
                Total = i.TotalAmountBase,
                TotalSource = i.TotalAmount,
            };

        if (request.IncludeCreditNotes)
        {
            IQueryable<ReceivableInvoiceFigure> credits =
                from c in creditNotes
                where (c.Status == DocumentStatus.Posted || c.Status == DocumentStatus.Void)
                    && c.DocumentDate >= start && c.DocumentDate <= end
                select new ReceivableInvoiceFigure
                {
                    TypeCode = c.TransactionTypeCode,
                    DocumentId = c.CreditNoteId,
                    DocumentNo = c.DocumentNo,
                    DocumentDate = c.DocumentDate,
                    DueDate = null,
                    ContactId = c.ContactId,
                    CurrencyCode = c.CurrencyCode,
                    Sign = -1,
                    IsVoid = c.Status == DocumentStatus.Void,
                    Taxable = -Math.Round(c.TaxableAmount * c.ExchangeRate, 2),
                    Cgst = -Math.Round(c.CgstAmount * c.ExchangeRate, 2),
                    Sgst = -Math.Round(c.SgstAmount * c.ExchangeRate, 2),
                    Igst = -Math.Round(c.IgstAmount * c.ExchangeRate, 2),
                    Cess = -Math.Round(c.CessAmount * c.ExchangeRate, 2),
                    Total = -c.TotalAmountBase,
                    TotalSource = -c.TotalAmount,
                };

            figures = figures.Concat(credits);
        }

        if (request.ContactId is long contact)
        {
            figures = figures.Where(f => f.ContactId == contact);
        }

        if (!string.IsNullOrWhiteSpace(request.CurrencyCode))
        {
            string code = request.CurrencyCode.Trim().ToUpperInvariant();
            figures = figures.Where(f => f.CurrencyCode == code);
        }

        return figures;
    }

    /// <summary>
    /// What was applied to each document, in base currency, by settlements effective on or before
    /// the date that have not been voided: an invoice at the target end of a row of
    /// <c>acc.TransactionRatios</c>, a credit note at the source end. A row where either end is a
    /// money document is in that document's currency and is taken to base at the measured document's
    /// own rate (its base total over its total); any other is in base already. Rounded to two places.
    /// Signed as the document is, so a credit note's is negative.
    /// </summary>
    public static IQueryable<ReceivedAmount> ReceivedAmounts(
        IQueryable<TransactionRatioRead> ratios, IQueryable<ReceivableInvoiceFigure> figures, DateOnly asAt)
    {
        IQueryable<TransactionRatioRead> live = ratios.Where(t => !t.IsVoided && t.AllocationDate <= asAt);

        IQueryable<ReceivedAmount> onInvoices =
            from t in live
            join f in figures
                on new { Code = t.TargetTransactionTypeCode, Id = t.TargetTransactionId }
                equals new { Code = f.TypeCode, Id = f.DocumentId }
            where f.Sign > 0 && !f.IsVoid
            select new ReceivedAmount
            {
                TypeCode = f.TypeCode,
                DocumentId = f.DocumentId,
                Amount = Math.Round(
                    (t.SourceTransactionTypeCode == "RCM" || t.SourceTransactionTypeCode == "SPM"
                        || t.TargetTransactionTypeCode == "RCM" || t.TargetTransactionTypeCode == "SPM")
                    && f.TotalSource != 0m
                        ? t.Amount * Math.Abs(f.Total) / Math.Abs(f.TotalSource)
                        : t.Amount,
                    2),
            };

        IQueryable<ReceivedAmount> onCreditNotes =
            from t in live
            join f in figures
                on new { Code = t.SourceTransactionTypeCode, Id = t.SourceTransactionId }
                equals new { Code = f.TypeCode, Id = f.DocumentId }
            where f.Sign < 0 && !f.IsVoid
            select new ReceivedAmount
            {
                TypeCode = f.TypeCode,
                DocumentId = f.DocumentId,
                Amount = -Math.Round(
                    (t.SourceTransactionTypeCode == "RCM" || t.SourceTransactionTypeCode == "SPM"
                        || t.TargetTransactionTypeCode == "RCM" || t.TargetTransactionTypeCode == "SPM")
                    && f.TotalSource != 0m
                        ? t.Amount * Math.Abs(f.Total) / Math.Abs(f.TotalSource)
                        : t.Amount,
                    2),
            };

        return onInvoices.Concat(onCreditNotes);
    }

    /// <summary>
    /// One row for each document with what was received, what is due and its status as at the date.
    /// The total and what was received are added up together in one pass (the document's own total,
    /// and each settlement), so no document is joined to a possibly empty sub-query. The status:
    /// void, else paid when nothing is left, else overdue when it has a due date before the date,
    /// else partly paid when something was received, else awaiting payment.
    /// </summary>
    public static IQueryable<ReceivableInvoiceRow> Rows(
        IQueryable<ReceivableInvoiceFigure> figures, IQueryable<ReceivedAmount> received, DateOnly asAt)
    {
        IQueryable<ReceivableSum> sums = figures
            .Select(f => new ReceivableSum { TypeCode = f.TypeCode, DocumentId = f.DocumentId, Total = f.Total, Received = 0m })
            .Concat(received.Select(r => new ReceivableSum { TypeCode = r.TypeCode, DocumentId = r.DocumentId, Total = 0m, Received = r.Amount }))
            .GroupBy(x => new { x.TypeCode, x.DocumentId })
            .Select(g => new ReceivableSum
            {
                TypeCode = g.Key.TypeCode,
                DocumentId = g.Key.DocumentId,
                Total = g.Sum(x => x.Total),
                Received = g.Sum(x => x.Received),
            });

        return from f in figures
               join s in sums
                   on new { Code = f.TypeCode, Id = f.DocumentId }
                   equals new { Code = s.TypeCode, Id = s.DocumentId }
               select new ReceivableInvoiceRow
               {
                   TypeCode = f.TypeCode,
                   DocumentId = f.DocumentId,
                   DocumentNo = f.DocumentNo,
                   DocumentDate = f.DocumentDate,
                   DueDate = f.DueDate,
                   ContactId = f.ContactId,
                   CurrencyCode = f.CurrencyCode,
                   TotalSource = f.TotalSource,
                   Taxable = f.Taxable,
                   Cgst = f.Cgst,
                   Sgst = f.Sgst,
                   Igst = f.Igst,
                   Cess = f.Cess,
                   Total = f.Total,
                   Received = f.IsVoid ? 0m : s.Received,
                   Due = f.IsVoid ? 0m : s.Total - s.Received,
                   Status = f.IsVoid
                       ? ReceivableInvoiceStatus.Void
                       : (s.Total - s.Received) * f.Sign <= 0m
                           ? ReceivableInvoiceStatus.Paid
                           : f.Sign > 0 && f.DueDate != null && f.DueDate < asAt
                               ? ReceivableInvoiceStatus.Overdue
                               : s.Received != 0m
                                   ? ReceivableInvoiceStatus.PartlyPaid
                                   : ReceivableInvoiceStatus.AwaitingPayment,
               };
    }

    /// <summary>The rows with the status asked for; with none asked for, every row but the void ones. LINQ only.</summary>
    public static IQueryable<ReceivableInvoiceRow> WithStatus(IQueryable<ReceivableInvoiceRow> rows, ReceivableInvoiceStatus? status) =>
        status is ReceivableInvoiceStatus chosen
            ? rows.Where(r => r.Status == chosen)
            : rows.Where(r => r.Status != ReceivableInvoiceStatus.Void);

    // --- compose: pure, so a test needs no database --------------------------------------

    /// <summary>
    /// The page's rows with their customers' names, how many documents have each status and the
    /// totals. Every status has a count, a nought included; the counts do not include a status that
    /// the other filters leave none of. Days overdue are given only to an overdue invoice, counted
    /// from its due date to the To date. Pure.
    /// </summary>
    public static ReceivableInvoiceSummaryReportResultDto Compose(
        ReceivableInvoiceSummaryReportRequest request,
        IReadOnlyList<ReceivableInvoiceRow> page,
        int totalInvoices,
        IReadOnlyList<CustomerRef> customers,
        IReadOnlyList<StatusCount> counts,
        ReceivableInvoiceSummaryTotalsDto totals,
        string baseCurrency)
    {
        DateOnly end = request.ToDate!.Value;
        Dictionary<long, CustomerRef> names = customers.GroupBy(c => c.ContactId).ToDictionary(g => g.Key, g => g.First());

        Dictionary<ReceivableInvoiceStatus, int> statusCounts = Enum.GetValues<ReceivableInvoiceStatus>().ToDictionary(s => s, _ => 0);
        foreach (StatusCount count in counts)
        {
            statusCounts[count.Status] = count.Count;
        }

        return new ReceivableInvoiceSummaryReportResultDto
        {
            FromDate = request.FromDate!.Value,
            ToDate = end,
            BaseCurrencyCode = baseCurrency,
            Invoices =
            [
                .. page.Select(r =>
                {
                    CustomerRef? customer = names.GetValueOrDefault(r.ContactId);
                    return new ReceivableInvoiceSummaryRowDto
                    {
                        TypeCode = r.TypeCode,
                        DocumentId = r.DocumentId,
                        DocumentNo = r.DocumentNo,
                        DocumentDate = r.DocumentDate,
                        DueDate = r.DueDate,
                        DaysOverdue = r.Status == ReceivableInvoiceStatus.Overdue && r.DueDate is DateOnly due ? end.DayNumber - due.DayNumber : null,
                        ContactId = r.ContactId,
                        ContactCode = customer?.Code ?? string.Empty,
                        ContactName = customer?.Name ?? $"Customer {r.ContactId}",
                        CurrencyCode = r.CurrencyCode,
                        TotalInCurrency = r.TotalSource,
                        Taxable = r.Taxable,
                        Cgst = r.Cgst,
                        Sgst = r.Sgst,
                        Igst = r.Igst,
                        Cess = r.Cess,
                        Tax = r.Cgst + r.Sgst + r.Igst + r.Cess,
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

/// <summary>One posted or voided document in the period, in base currency, signed (an invoice positive, a credit note negative).</summary>
public sealed class ReceivableInvoiceFigure
{
    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public long ContactId { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>1 for an invoice, −1 for a credit note.</summary>
    public int Sign { get; set; }

    public bool IsVoid { get; set; }

    public decimal Taxable { get; set; }

    public decimal Cgst { get; set; }

    public decimal Sgst { get; set; }

    public decimal Igst { get; set; }

    public decimal Cess { get; set; }

    /// <summary>The document's total in base currency.</summary>
    public decimal Total { get; set; }

    /// <summary>The same total in the document's own currency.</summary>
    public decimal TotalSource { get; set; }
}

/// <summary>What one settlement applied to a document, in base currency, signed as the document is.</summary>
public sealed class ReceivedAmount
{
    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    public decimal Amount { get; set; }
}

/// <summary>A document's total and what was received, added up.</summary>
public sealed class ReceivableSum
{
    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    public decimal Total { get; set; }

    public decimal Received { get; set; }
}

/// <summary>A document with what was received, what is due and its status.</summary>
public sealed class ReceivableInvoiceRow
{
    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public long ContactId { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal TotalSource { get; set; }

    public decimal Taxable { get; set; }

    public decimal Cgst { get; set; }

    public decimal Sgst { get; set; }

    public decimal Igst { get; set; }

    public decimal Cess { get; set; }

    public decimal Total { get; set; }

    public decimal Received { get; set; }

    public decimal Due { get; set; }

    public ReceivableInvoiceStatus Status { get; set; }
}

/// <summary>How many documents have a status.</summary>
public sealed class StatusCount
{
    public ReceivableInvoiceStatus Status { get; set; }

    public int Count { get; set; }
}

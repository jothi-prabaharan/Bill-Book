using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Payable Invoice Summary (TK-235): every posted vendor bill dated in a period with its taxable value, tax and
/// total, what has been paid and what is still due, a page at a time, with how many have each status. It is the
/// Receivable Invoice Summary (TK-224) the other way round.
///
/// <b>Paid and due are as at the To date</b>, not as at today, so a report for a past period reads the same when it is
/// run again. What was paid is read from <c>acc.TransactionRatios</c> that are not voided and were effective on or before
/// that date, the way the Aged Payables reports read it: a payment, or a debit note or advance applied with Allocate,
/// taken off the bill it settled. Accounting writes the bill, the credit side, as the <b>source</b> of such a row, and
/// the payment or debit note as its target. <b>It is one figure in base currency</b>: a money document's line is in the
/// document's own currency, so it is taken to base at the bill's own rate (its base total over its total); any other
/// allocation is in base already.
///
/// <b>What the vendor is owed is not always the total.</b> TDS is withheld from the vendor and credited to TDS Payable,
/// and on a reverse-charge bill the tax is the branch's own to pay, so the payable control leg Purchase posts is the total
/// less both, and that is what payments settle. <see cref="PayableInvoiceFigure.Payable"/> is that figure, so
/// <i>payable less paid is due</i> on every row; the total is shown as the vendor billed it.
///
/// <b>Debit notes are not bills.</b> They are listed only when asked for, as negatives: for a debit note, paid is what
/// has been applied to bills and due is what has not. A voided document settles nothing and is listed only when its status
/// is asked for; drafts never are.
///
/// <b>The status is worked out in the database</b> (<see cref="Rows"/>), so the tabs' counts and the totals cover every
/// bill the filters match and not only the page in hand.
/// </summary>
public sealed class PayableInvoiceSummaryReportRepository
{
    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public PayableInvoiceSummaryReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>The refusals a request needs beyond both dates being given, with the field each belongs to. Pure.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(PayableInvoiceSummaryReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from)
        {
            refusals.Add((nameof(PayableInvoiceSummaryReportRequest.ToDate), "To date cannot be before the from date."));
        }

        if (!string.IsNullOrWhiteSpace(request.CurrencyCode) && request.CurrencyCode.Trim().Length != 3)
        {
            refusals.Add((nameof(PayableInvoiceSummaryReportRequest.CurrencyCode), "Currency must be a 3-letter code."));
        }

        if (request.Status is PayableInvoiceStatus status && !Enum.IsDefined(status))
        {
            refusals.Add((nameof(PayableInvoiceSummaryReportRequest.Status), "Status must be one of the statuses offered."));
        }

        return refusals;
    }

    /// <summary>Whether a vendor is on this branch; the query filter decides, so another branch's reads as none.</summary>
    public Task<bool> ContactExistsAsync(long contactId, CancellationToken ct) =>
        _db.Contacts.AnyAsync(c => c.ContactId == contactId, ct);

    /// <summary>The vendors the filter offers, by name: the Aged Payables list.</summary>
    public Task<List<AgedPayablesVendorChoiceDto>> VendorsAsync(CancellationToken ct) =>
        AgedPayablesSummaryReportRepository.VendorChoices(_db.Contacts).ToListAsync(ct);

    /// <summary>The currencies the branch has been billed in, for the currency filter.</summary>
    public async Task<List<PayableInvoiceCurrencyChoiceDto>> CurrenciesAsync(CancellationToken ct)
    {
        List<string> bills = await _db.Bills
            .Where(b => b.Status == DocumentStatus.Posted || b.Status == DocumentStatus.Void)
            .Select(b => b.CurrencyCode)
            .Distinct()
            .ToListAsync(ct);

        List<string> debits = await _db.DebitNotes
            .Where(d => d.Status == DocumentStatus.Posted || d.Status == DocumentStatus.Void)
            .Select(d => d.CurrencyCode)
            .Distinct()
            .ToListAsync(ct);

        return
        [
            .. bills.Concat(debits)
                .Select(c => c.Trim().ToUpperInvariant())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(c => c, StringComparer.Ordinal)
                .Select(c => new PayableInvoiceCurrencyChoiceDto { CurrencyCode = c }),
        ];
    }

    /// <summary>The report for a request the controller has already validated.</summary>
    public async Task<PayableInvoiceSummaryReportResultDto> RunAsync(PayableInvoiceSummaryReportRequest request, CancellationToken ct)
    {
        DateOnly start = request.FromDate!.Value;
        DateOnly end = request.ToDate!.Value;
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

        IQueryable<PayableInvoiceFigure> figures = Figures(_db.Bills, _db.DebitNotes, request, start, end);
        IQueryable<PayableInvoiceRow> rows = Rows(figures, PaidAmounts(_db.TransactionRatios, figures, end), end);

        // The tabs count every status the other filters let through, so a tab never hides a document behind the one chosen.
        List<PayableStatusCount> counts = await rows
            .GroupBy(r => r.Status)
            .Select(g => new PayableStatusCount { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        IQueryable<PayableInvoiceRow> matching = WithStatus(rows, request.Status);

        PayableInvoiceSummaryTotalsDto totals = await matching
            .GroupBy(r => 1)
            .Select(g => new PayableInvoiceSummaryTotalsDto
            {
                Taxable = g.Sum(r => r.Taxable),
                Tax = g.Sum(r => r.Cgst + r.Sgst + r.Igst + r.Cess),
                Tds = g.Sum(r => r.Tds),
                Total = g.Sum(r => r.Total),
                Payable = g.Sum(r => r.Payable),
                Paid = g.Sum(r => r.Paid),
                Due = g.Sum(r => r.Due),
            })
            .FirstOrDefaultAsync(ct) ?? new PayableInvoiceSummaryTotalsDto();

        int matches = await matching.CountAsync(ct);

        List<PayableInvoiceRow> page = await matching
            .OrderByDescending(r => r.DocumentDate)
            .ThenByDescending(r => r.TypeCode)
            .ThenByDescending(r => r.DocumentId)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        List<long> ids = [.. page.Select(r => r.ContactId).Distinct()];
        List<VendorRef> vendors = ids.Count == 0
            ? []
            : await _db.Contacts
                .Where(c => ids.Contains(c.ContactId))
                .Select(c => new VendorRef { ContactId = c.ContactId, Code = c.ContactCode, Name = c.DisplayName, IsMsme = c.IsMsme })
                .ToListAsync(ct);

        return Compose(request, page, matches, vendors, counts, totals, baseCurrency);
    }

    // --- builders: LINQ over the read models, so a test runs them over lists -------------

    /// <summary>
    /// The posted and voided bills dated in the period, and the debit notes too when asked, narrowed by vendor and
    /// currency. Each is one row in base currency, signed: a bill is positive and a debit note negative. A tax figure is
    /// taken to base at the rate the document was booked at (a purchase document's rate is base per unit of the foreign
    /// currency); the total is the document's own base total; what the vendor is owed is the total less TDS and, on a
    /// reverse-charge bill, the tax.
    /// </summary>
    public static IQueryable<PayableInvoiceFigure> Figures(
        IQueryable<BillRead> bills,
        IQueryable<DebitNoteRead> debitNotes,
        PayableInvoiceSummaryReportRequest request,
        DateOnly start,
        DateOnly end)
    {
        IQueryable<PayableInvoiceFigure> figures =
            from b in bills
            where (b.Status == DocumentStatus.Posted || b.Status == DocumentStatus.Void)
                && b.DocumentDate >= start && b.DocumentDate <= end
            select new PayableInvoiceFigure
            {
                TypeCode = b.TransactionTypeCode,
                DocumentId = b.BillId,
                DocumentNo = b.DocumentNo,
                VendorBillNo = b.VendorBillNo,
                DocumentDate = b.DocumentDate,
                DueDate = b.DueDate,
                ContactId = b.ContactId,
                CurrencyCode = b.CurrencyCode,
                Sign = 1,
                ExchangeRate = b.ExchangeRate,
                IsVoid = b.Status == DocumentStatus.Void,
                ReverseCharge = b.IsReverseCharge,
                Taxable = Math.Round(b.TaxableAmount * b.ExchangeRate, 2),
                Cgst = Math.Round(b.CgstAmount * b.ExchangeRate, 2),
                Sgst = Math.Round(b.SgstAmount * b.ExchangeRate, 2),
                Igst = Math.Round(b.IgstAmount * b.ExchangeRate, 2),
                Cess = Math.Round(b.CessAmount * b.ExchangeRate, 2),
                Tds = Math.Round(b.TdsAmount * b.ExchangeRate, 2),
                Total = b.TotalAmountBase,
                TotalSource = b.TotalAmount,
                Payable = b.TotalAmountBase
                    - Math.Round(b.TdsAmount * b.ExchangeRate, 2)
                    - (b.IsReverseCharge
                        ? Math.Round(b.CgstAmount * b.ExchangeRate, 2) + Math.Round(b.SgstAmount * b.ExchangeRate, 2)
                            + Math.Round(b.IgstAmount * b.ExchangeRate, 2) + Math.Round(b.CessAmount * b.ExchangeRate, 2)
                        : 0m),
            };

        if (request.IncludeDebitNotes)
        {
            IQueryable<PayableInvoiceFigure> debits =
                from d in debitNotes
                where (d.Status == DocumentStatus.Posted || d.Status == DocumentStatus.Void)
                    && d.DocumentDate >= start && d.DocumentDate <= end
                select new PayableInvoiceFigure
                {
                    TypeCode = d.TransactionTypeCode,
                    DocumentId = d.DebitNoteId,
                    DocumentNo = d.DocumentNo,
                    VendorBillNo = null,
                    DocumentDate = d.DocumentDate,
                    DueDate = null,
                    ContactId = d.ContactId,
                    CurrencyCode = d.CurrencyCode,
                    Sign = -1,
                    ExchangeRate = d.ExchangeRate,
                    IsVoid = d.Status == DocumentStatus.Void,
                    ReverseCharge = false,
                    Taxable = -Math.Round(d.TaxableAmount * d.ExchangeRate, 2),
                    Cgst = -Math.Round(d.CgstAmount * d.ExchangeRate, 2),
                    Sgst = -Math.Round(d.SgstAmount * d.ExchangeRate, 2),
                    Igst = -Math.Round(d.IgstAmount * d.ExchangeRate, 2),
                    Cess = -Math.Round(d.CessAmount * d.ExchangeRate, 2),
                    Tds = 0m,
                    Total = -d.TotalAmountBase,
                    TotalSource = -d.TotalAmount,
                    Payable = -d.TotalAmountBase,
                };

            figures = figures.Concat(debits);
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
    /// What was applied to each document, in base currency, by settlements effective on or before the date that have
    /// not been voided: a bill at the <b>source</b> end of a row of <c>acc.TransactionRatios</c> (it is the credit side),
    /// a debit note at the target end. A row where either end is a money document is in that document's currency and is
    /// taken to base at the measured document's own rate (its base total over its total); any other is in base already.
    /// Rounded to two places. Signed as the document is, so a debit note's is negative.
    /// </summary>
    public static IQueryable<PaidAmount> PaidAmounts(
        IQueryable<TransactionRatioRead> ratios, IQueryable<PayableInvoiceFigure> figures, DateOnly asAt)
    {
        IQueryable<TransactionRatioRead> live = ratios.Where(t => !t.IsVoided && t.AllocationDate <= asAt);

        IQueryable<PaidAmount> onBills =
            from t in live
            join f in figures
                on new { Code = t.SourceTransactionTypeCode, Id = t.SourceTransactionId }
                equals new { Code = f.TypeCode, Id = f.DocumentId }
            where f.Sign > 0 && !f.IsVoid
            select new PaidAmount
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

        IQueryable<PaidAmount> onDebitNotes =
            from t in live
            join f in figures
                on new { Code = t.TargetTransactionTypeCode, Id = t.TargetTransactionId }
                equals new { Code = f.TypeCode, Id = f.DocumentId }
            where f.Sign < 0 && !f.IsVoid
            select new PaidAmount
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

        return onBills.Concat(onDebitNotes);
    }

    /// <summary>
    /// One row for each document with what was paid, what is due and its status as at the date. What the vendor is
    /// owed and what was paid are added up together in one pass (the document's own payable, and each settlement), so no
    /// document is joined to a possibly empty sub-query. The status: void, else paid when nothing is left, else overdue
    /// when it has a due date before the date, else partly paid when something was paid, else awaiting payment.
    /// </summary>
    public static IQueryable<PayableInvoiceRow> Rows(
        IQueryable<PayableInvoiceFigure> figures, IQueryable<PaidAmount> paid, DateOnly asAt)
    {
        IQueryable<PayableSum> sums = figures
            .Select(f => new PayableSum { TypeCode = f.TypeCode, DocumentId = f.DocumentId, Payable = f.Payable, Paid = 0m })
            .Concat(paid.Select(r => new PayableSum { TypeCode = r.TypeCode, DocumentId = r.DocumentId, Payable = 0m, Paid = r.Amount }))
            .GroupBy(x => new { x.TypeCode, x.DocumentId })
            .Select(g => new PayableSum
            {
                TypeCode = g.Key.TypeCode,
                DocumentId = g.Key.DocumentId,
                Payable = g.Sum(x => x.Payable),
                Paid = g.Sum(x => x.Paid),
            });

        return from f in figures
               join s in sums
                   on new { Code = f.TypeCode, Id = f.DocumentId }
                   equals new { Code = s.TypeCode, Id = s.DocumentId }
               select new PayableInvoiceRow
               {
                   TypeCode = f.TypeCode,
                   DocumentId = f.DocumentId,
                   DocumentNo = f.DocumentNo,
                   VendorBillNo = f.VendorBillNo,
                   DocumentDate = f.DocumentDate,
                   DueDate = f.DueDate,
                   ContactId = f.ContactId,
                   CurrencyCode = f.CurrencyCode,
                   ReverseCharge = f.ReverseCharge,
                   ExchangeRate = f.ExchangeRate,
                   TotalSource = f.TotalSource,
                   Taxable = f.Taxable,
                   Cgst = f.Cgst,
                   Sgst = f.Sgst,
                   Igst = f.Igst,
                   Cess = f.Cess,
                   Tds = f.Tds,
                   Total = f.Total,
                   Payable = f.Payable,
                   Paid = f.IsVoid ? 0m : s.Paid,
                   Due = f.IsVoid ? 0m : s.Payable - s.Paid,
                   Status = f.IsVoid
                       ? PayableInvoiceStatus.Void
                       : (s.Payable - s.Paid) * f.Sign <= 0m
                           ? PayableInvoiceStatus.Paid
                           : f.Sign > 0 && f.DueDate != null && f.DueDate < asAt
                               ? PayableInvoiceStatus.Overdue
                               : s.Paid != 0m
                                   ? PayableInvoiceStatus.PartlyPaid
                                   : PayableInvoiceStatus.AwaitingPayment,
               };
    }

    /// <summary>The rows with the status asked for; with none asked for, every row but the void ones. LINQ only.</summary>
    public static IQueryable<PayableInvoiceRow> WithStatus(IQueryable<PayableInvoiceRow> rows, PayableInvoiceStatus? status) =>
        status is PayableInvoiceStatus chosen
            ? rows.Where(r => r.Status == chosen)
            : rows.Where(r => r.Status != PayableInvoiceStatus.Void);

    // --- compose: pure, so a test needs no database --------------------------------------

    /// <summary>
    /// The page's rows with their vendors' names, how many documents have each status and the totals. Every status has
    /// a count, a nought included. Days overdue are given only to an overdue bill, counted from its due date to the To
    /// date. Pure.
    /// </summary>
    public static PayableInvoiceSummaryReportResultDto Compose(
        PayableInvoiceSummaryReportRequest request,
        IReadOnlyList<PayableInvoiceRow> page,
        int totalInvoices,
        IReadOnlyList<VendorRef> vendors,
        IReadOnlyList<PayableStatusCount> counts,
        PayableInvoiceSummaryTotalsDto totals,
        string baseCurrency)
    {
        DateOnly end = request.ToDate!.Value;
        Dictionary<long, VendorRef> names = vendors.GroupBy(c => c.ContactId).ToDictionary(g => g.Key, g => g.First());

        Dictionary<PayableInvoiceStatus, int> statusCounts = Enum.GetValues<PayableInvoiceStatus>().ToDictionary(s => s, _ => 0);
        foreach (PayableStatusCount count in counts)
        {
            statusCounts[count.Status] = count.Count;
        }

        return new PayableInvoiceSummaryReportResultDto
        {
            FromDate = request.FromDate!.Value,
            ToDate = end,
            BaseCurrencyCode = baseCurrency,
            Invoices =
            [
                .. page.Select(r =>
                {
                    VendorRef? vendor = names.GetValueOrDefault(r.ContactId);
                    return new PayableInvoiceSummaryRowDto
                    {
                        TypeCode = r.TypeCode,
                        DocumentId = r.DocumentId,
                        DocumentNo = r.DocumentNo,
                        VendorBillNo = r.VendorBillNo,
                        DocumentDate = r.DocumentDate,
                        DueDate = r.DueDate,
                        DaysOverdue = r.Status == PayableInvoiceStatus.Overdue && r.DueDate is DateOnly due ? end.DayNumber - due.DayNumber : null,
                        ContactId = r.ContactId,
                        ContactCode = vendor?.Code ?? string.Empty,
                        ContactName = vendor?.Name ?? $"Vendor {r.ContactId}",
                        CurrencyCode = r.CurrencyCode,
                        TotalInCurrency = r.TotalSource,
                        Taxable = r.Taxable,
                        Cgst = r.Cgst,
                        Sgst = r.Sgst,
                        Igst = r.Igst,
                        Cess = r.Cess,
                        Tax = r.Cgst + r.Sgst + r.Igst + r.Cess,
                        ReverseCharge = r.ReverseCharge,
                        Tds = r.Tds,
                        Total = r.Total,
                        Payable = r.Payable,
                        Paid = r.Paid,
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

/// <summary>One posted or voided document in the period, in base currency, signed (a bill positive, a debit note negative).</summary>
public sealed class PayableInvoiceFigure
{
    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    public string DocumentNo { get; set; } = string.Empty;

    public string? VendorBillNo { get; set; }

    public DateOnly DocumentDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public long ContactId { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>1 for a bill, −1 for a debit note.</summary>
    public int Sign { get; set; }

    /// <summary>The document's rate, base per unit of its own currency (TK-236: the detail takes a line to base with it).</summary>
    public decimal ExchangeRate { get; set; }

    public bool IsVoid { get; set; }

    public bool ReverseCharge { get; set; }

    public decimal Taxable { get; set; }

    public decimal Cgst { get; set; }

    public decimal Sgst { get; set; }

    public decimal Igst { get; set; }

    public decimal Cess { get; set; }

    public decimal Tds { get; set; }

    /// <summary>The document's total in base currency.</summary>
    public decimal Total { get; set; }

    /// <summary>The same total in the document's own currency.</summary>
    public decimal TotalSource { get; set; }

    /// <summary>What the vendor is owed on it: the total less TDS and, on a reverse-charge bill, the tax.</summary>
    public decimal Payable { get; set; }
}

/// <summary>What one settlement applied to a document, in base currency, signed as the document is.</summary>
public sealed class PaidAmount
{
    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    public decimal Amount { get; set; }
}

/// <summary>A document's payable and what was paid, added up.</summary>
public sealed class PayableSum
{
    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    public decimal Payable { get; set; }

    public decimal Paid { get; set; }
}

/// <summary>A document with what was paid, what is due and its status.</summary>
public sealed class PayableInvoiceRow
{
    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    public string DocumentNo { get; set; } = string.Empty;

    public string? VendorBillNo { get; set; }

    public DateOnly DocumentDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public long ContactId { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public bool ReverseCharge { get; set; }

    public decimal ExchangeRate { get; set; }

    public decimal TotalSource { get; set; }

    public decimal Taxable { get; set; }

    public decimal Cgst { get; set; }

    public decimal Sgst { get; set; }

    public decimal Igst { get; set; }

    public decimal Cess { get; set; }

    public decimal Tds { get; set; }

    public decimal Total { get; set; }

    public decimal Payable { get; set; }

    public decimal Paid { get; set; }

    public decimal Due { get; set; }

    public PayableInvoiceStatus Status { get; set; }
}

/// <summary>How many documents have a status.</summary>
public sealed class PayableStatusCount
{
    public PayableInvoiceStatus Status { get; set; }

    public int Count { get; set; }
}

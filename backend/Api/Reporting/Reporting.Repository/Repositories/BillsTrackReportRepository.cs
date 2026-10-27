using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Approvals;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Bills Track Report (TK-237): where each vendor bill is in its life, a page at a time. It is the
/// Payable Invoice Summary's bills (posted and voided, with what was paid and what is still owed to the
/// vendor as at the To date, from that report's own builders) with the drafts and the ready-to-post bills
/// of the period beside them, and for each the dates of its steps: when it was posted, its due date and
/// the last settlement of it, and, where the bill went through an approval chain, where that stands.
///
/// <b>A draft is not in the books.</b> It owes nothing and has been paid nothing, so its paid and due are
/// nought and it has no due date (a bill takes one from its payment term when it posts), and the totals
/// add up posted bills that still stand (not a draft, not a voided one); otherwise the amount owed less
/// what was paid would not be the due. The tab counts and the totals cover every bill the filters match,
/// worked out in the database; only the posted-on and last-payment dates and the approval of the page's
/// own bills are read after it, since they decide neither a status nor a total. A bill's approval is a
/// fact beside its status, not one of them: an approved draft is still a draft until it is posted.
/// </summary>
public sealed class BillsTrackReportRepository
{
    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public BillsTrackReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>The refusals a request needs beyond both dates being given, with the field each belongs to. Pure.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(BillsTrackReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from)
        {
            refusals.Add((nameof(BillsTrackReportRequest.ToDate), "To date cannot be before the from date."));
        }

        if (request.Status is BillTrackStatus status && !Enum.IsDefined(status))
        {
            refusals.Add((nameof(BillsTrackReportRequest.Status), "Status must be one of the statuses offered."));
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
    public async Task<BillsTrackReportResultDto> RunAsync(BillsTrackReportRequest request, CancellationToken ct)
    {
        DateOnly start = request.FromDate!.Value;
        DateOnly end = request.ToDate!.Value;
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

        IQueryable<BillTrackEntry> rows = Entries(_db.Bills, _db.TransactionRatios, request, start, end);

        // The tabs count every status the other filters let through, so a tab never hides a bill behind the one chosen.
        List<BillTrackStatusCount> counts = await rows
            .GroupBy(r => r.Status)
            .Select(g => new BillTrackStatusCount { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        IQueryable<BillTrackEntry> matching = WithStatus(rows, request.Status);

        BillsTrackTotalsDto totals = await Posted(matching)
            .GroupBy(r => 1)
            .Select(g => new BillsTrackTotalsDto { Total = g.Sum(r => r.Total), Paid = g.Sum(r => r.Paid), Due = g.Sum(r => r.Due) })
            .FirstOrDefaultAsync(ct) ?? new BillsTrackTotalsDto();

        int matches = await matching.CountAsync(ct);

        List<BillTrackEntry> page = await matching
            .OrderByDescending(r => r.DocumentDate)
            .ThenByDescending(r => r.DocumentId)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        List<long> contactIds = [.. page.Select(r => r.ContactId).Distinct()];
        List<VendorRef> vendors = contactIds.Count == 0
            ? []
            : await _db.Contacts
                .Where(c => contactIds.Contains(c.ContactId))
                .Select(c => new VendorRef { ContactId = c.ContactId, Code = c.ContactCode, Name = c.DisplayName, IsMsme = c.IsMsme })
                .ToListAsync(ct);

        List<long> billIds = [.. page.Select(r => r.DocumentId)];
        List<BillStamp> stamps = billIds.Count == 0
            ? []
            : await Stamps(_db.Bills, billIds).ToListAsync(ct);
        List<BillLastPayment> payments = billIds.Count == 0
            ? []
            : await LastPayments(_db.TransactionRatios, billIds, end).ToListAsync(ct);

        return Compose(request, page, matches, vendors, stamps, payments, counts, totals, baseCurrency);
    }

    // --- builders: LINQ over the read models, so a test runs them over lists -------------

    /// <summary>
    /// Every bill dated in the period for the filters: the posted and voided ones with what was paid
    /// and what is still owed as at the date, from the Payable Invoice Summary's builders (no debit
    /// notes), and the drafts and ready-to-post ones, which owe nothing. The vendor filter applies to both.
    /// </summary>
    public static IQueryable<BillTrackEntry> Entries(
        IQueryable<BillRead> bills,
        IQueryable<TransactionRatioRead> ratios,
        BillsTrackReportRequest request,
        DateOnly start,
        DateOnly end)
    {
        var summary = new PayableInvoiceSummaryReportRequest { FromDate = start, ToDate = end, ContactId = request.ContactId, IncludeDebitNotes = false };
        IQueryable<PayableInvoiceFigure> figures = PayableInvoiceSummaryReportRepository.Figures(bills, Enumerable.Empty<DebitNoteRead>().AsQueryable(), summary, start, end);
        IQueryable<PayableInvoiceRow> booked = PayableInvoiceSummaryReportRepository.Rows(
            figures, PayableInvoiceSummaryReportRepository.PaidAmounts(ratios, figures, end), end);

        return Drafts(bills, request.ContactId, start, end).Concat(Booked(booked));
    }

    /// <summary>The posted and voided rows as track entries, the summary's status read as this report's.</summary>
    public static IQueryable<BillTrackEntry> Booked(IQueryable<PayableInvoiceRow> rows) =>
        from r in rows
        select new BillTrackEntry
        {
            TypeCode = r.TypeCode,
            DocumentId = r.DocumentId,
            DocumentNo = r.DocumentNo,
            VendorBillNo = r.VendorBillNo,
            DocumentDate = r.DocumentDate,
            DueDate = r.DueDate,
            ContactId = r.ContactId,
            CurrencyCode = r.CurrencyCode,
            ReverseCharge = r.ReverseCharge,
            TotalSource = r.TotalSource,
            Total = r.Total,
            Payable = r.Payable,
            Paid = r.Paid,
            Due = r.Due,
            Status = r.Status == PayableInvoiceStatus.Void
                ? BillTrackStatus.Void
                : r.Status == PayableInvoiceStatus.Paid
                    ? BillTrackStatus.Paid
                    : r.Status == PayableInvoiceStatus.Overdue
                        ? BillTrackStatus.Overdue
                        : r.Status == PayableInvoiceStatus.PartlyPaid
                            ? BillTrackStatus.PartlyPaid
                            : BillTrackStatus.AwaitingPayment,
        };

    /// <summary>The bills dated in the period that are not in the books yet: drafts and ready to post. They have no due date yet.</summary>
    public static IQueryable<BillTrackEntry> Drafts(IQueryable<BillRead> bills, long? contactId, DateOnly start, DateOnly end)
    {
        IQueryable<BillTrackEntry> drafts =
            from b in bills
            where (b.Status == DocumentStatus.Draft || b.Status == DocumentStatus.ReadyToPost)
                && b.DocumentDate >= start && b.DocumentDate <= end
            select new BillTrackEntry
            {
                TypeCode = b.TransactionTypeCode,
                DocumentId = b.BillId,
                DocumentNo = b.DocumentNo,
                VendorBillNo = b.VendorBillNo,
                DocumentDate = b.DocumentDate,
                DueDate = null,
                ContactId = b.ContactId,
                CurrencyCode = b.CurrencyCode,
                ReverseCharge = b.IsReverseCharge,
                TotalSource = b.TotalAmount,
                Total = b.TotalAmountBase,
                Payable = 0m,
                Paid = 0m,
                Due = 0m,
                Status = b.Status == DocumentStatus.Draft ? BillTrackStatus.Draft : BillTrackStatus.ReadyToPost,
            };

        return contactId is long contact ? drafts.Where(d => d.ContactId == contact) : drafts;
    }

    /// <summary>The entries with the status asked for; with none asked for, every entry but the void ones. LINQ only.</summary>
    public static IQueryable<BillTrackEntry> WithStatus(IQueryable<BillTrackEntry> rows, BillTrackStatus? status) =>
        status is BillTrackStatus chosen
            ? rows.Where(r => r.Status == chosen)
            : rows.Where(r => r.Status != BillTrackStatus.Void);

    /// <summary>
    /// The entries the totals add up: posted bills that still stand. A draft or one ready to post is
    /// not in the books yet and a voided one has been withdrawn.
    /// </summary>
    public static IQueryable<BillTrackEntry> Posted(IQueryable<BillTrackEntry> rows) =>
        rows.Where(r => r.Status != BillTrackStatus.Draft && r.Status != BillTrackStatus.ReadyToPost && r.Status != BillTrackStatus.Void);

    /// <summary>When each of the given bills was posted and where its approval stands. LINQ only.</summary>
    public static IQueryable<BillStamp> Stamps(IQueryable<BillRead> bills, IReadOnlyCollection<long> billIds) =>
        bills
            .Where(b => billIds.Contains(b.BillId))
            .Select(b => new BillStamp
            {
                DocumentId = b.BillId,
                PostedAt = b.PostedAt,
                ApprovalStatus = b.ApprovalStatus,
                StepLabel = b.CurrentStepLabel,
            });

    /// <summary>
    /// The latest settlement of each of the given bills dated on or before the date and not voided:
    /// any row of <c>acc.TransactionRatios</c> that names the bill at its source end, a payment or a
    /// debit note or an advance applied to it.
    /// </summary>
    public static IQueryable<BillLastPayment> LastPayments(IQueryable<TransactionRatioRead> ratios, IReadOnlyCollection<long> billIds, DateOnly asAt) =>
        ratios
            .Where(t => !t.IsVoided && t.AllocationDate <= asAt
                && t.SourceTransactionTypeCode == "BIL"
                && billIds.Contains(t.SourceTransactionId))
            .GroupBy(t => new { t.SourceTransactionTypeCode, t.SourceTransactionId })
            .Select(g => new BillLastPayment
            {
                TypeCode = g.Key.SourceTransactionTypeCode,
                DocumentId = g.Key.SourceTransactionId,
                Date = g.Max(t => t.AllocationDate),
            });

    // --- compose: pure, so a test needs no database --------------------------------------

    /// <summary>
    /// The page's rows with their vendors' names, posted-on, last-payment and approval, how many bills
    /// have each status and the totals. Every status has a count, a nought included. Days overdue are
    /// given only to an overdue bill, counted from its due date to the To date. An approval step is
    /// given only to a bill that is in approval. Pure.
    /// </summary>
    public static BillsTrackReportResultDto Compose(
        BillsTrackReportRequest request,
        IReadOnlyList<BillTrackEntry> page,
        int totalBills,
        IReadOnlyList<VendorRef> vendors,
        IReadOnlyList<BillStamp> stamps,
        IReadOnlyList<BillLastPayment> payments,
        IReadOnlyList<BillTrackStatusCount> counts,
        BillsTrackTotalsDto totals,
        string baseCurrency)
    {
        DateOnly end = request.ToDate!.Value;
        Dictionary<long, VendorRef> names = vendors.GroupBy(c => c.ContactId).ToDictionary(g => g.Key, g => g.First());
        Dictionary<long, BillStamp> stamped = stamps.GroupBy(s => s.DocumentId).ToDictionary(g => g.Key, g => g.First());
        Dictionary<(string, long), DateOnly> last = payments.GroupBy(p => (p.TypeCode, p.DocumentId)).ToDictionary(g => g.Key, g => g.Max(p => p.Date));

        Dictionary<BillTrackStatus, int> statusCounts = Enum.GetValues<BillTrackStatus>().ToDictionary(s => s, _ => 0);
        foreach (BillTrackStatusCount count in counts)
        {
            statusCounts[count.Status] = count.Count;
        }

        return new BillsTrackReportResultDto
        {
            FromDate = request.FromDate!.Value,
            ToDate = end,
            BaseCurrencyCode = baseCurrency,
            Bills =
            [
                .. page.Select(r =>
                {
                    VendorRef? vendor = names.GetValueOrDefault(r.ContactId);
                    BillStamp? stamp = stamped.GetValueOrDefault(r.DocumentId);
                    return new BillsTrackRowDto
                    {
                        TypeCode = r.TypeCode,
                        DocumentId = r.DocumentId,
                        DocumentNo = r.DocumentNo,
                        VendorBillNo = r.VendorBillNo,
                        DocumentDate = r.DocumentDate,
                        PostedAt = stamp?.PostedAt,
                        DueDate = r.DueDate,
                        LastPaymentDate = last.TryGetValue((r.TypeCode, r.DocumentId), out DateOnly paid) ? paid : null,
                        DaysOverdue = r.Status == BillTrackStatus.Overdue && r.DueDate is DateOnly due ? end.DayNumber - due.DayNumber : null,
                        ContactId = r.ContactId,
                        ContactCode = vendor?.Code ?? string.Empty,
                        ContactName = vendor?.Name ?? $"Vendor {r.ContactId}",
                        CurrencyCode = r.CurrencyCode,
                        ReverseCharge = r.ReverseCharge,
                        TotalInCurrency = r.TotalSource,
                        Total = r.Total,
                        Payable = r.Payable,
                        Paid = r.Paid,
                        Due = r.Due,
                        Status = r.Status,
                        ApprovalStatus = stamp?.ApprovalStatus,
                        ApprovalStep = stamp?.ApprovalStatus == ApprovalStatus.InApproval && !string.IsNullOrWhiteSpace(stamp.StepLabel) ? stamp.StepLabel : null,
                    };
                }),
            ],
            TotalBills = totalBills,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            StatusCounts = statusCounts,
            Totals = totals,
        };
    }
}

/// <summary>A bill with what was paid, what is still owed and its status in the track report.</summary>
public sealed class BillTrackEntry
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

    public decimal TotalSource { get; set; }

    public decimal Total { get; set; }

    public decimal Payable { get; set; }

    public decimal Paid { get; set; }

    public decimal Due { get; set; }

    public BillTrackStatus Status { get; set; }
}

/// <summary>When a bill was posted and where its approval stands; null while it is not in the books, and none when no workflow was involved.</summary>
public sealed class BillStamp
{
    public long DocumentId { get; set; }

    public DateTimeOffset? PostedAt { get; set; }

    public ApprovalStatus? ApprovalStatus { get; set; }

    public string? StepLabel { get; set; }
}

/// <summary>The latest settlement date of a bill.</summary>
public sealed class BillLastPayment
{
    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    public DateOnly Date { get; set; }
}

/// <summary>How many bills have a status.</summary>
public sealed class BillTrackStatusCount
{
    public BillTrackStatus Status { get; set; }

    public int Count { get; set; }
}

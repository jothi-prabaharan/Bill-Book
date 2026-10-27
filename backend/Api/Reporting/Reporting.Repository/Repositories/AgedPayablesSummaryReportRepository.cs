using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Aged Payables Summary (TK-233): what the branch owed each vendor on a date and how overdue it was,
/// as Current and four overdue columns. It is the Aged Receivables Summary (TK-222) the other way round, and
/// the two share one rule rather than copying it: the open documents come from
/// <see cref="AgedReceivablesSummaryReportRepository"/>'s builders, run on the payable account with the sign
/// turned so that what is owed is positive.
///
/// <b>A document is what is aged.</b> The branch's Accounts Payable legs up to the date are grouped by the
/// document that posted them: a bill, a debit note, a payment, an opening balance, a manual journal. Credits
/// less debits is what is owed. A settlement is read from <c>acc.TransactionRatios</c> that are not voided and
/// were effective on or before the date, and takes its amount off the <b>source</b> (the credit side, the bill)
/// and off the target (the debit side, the payment), so one settlement reduces both ends by the same figure and
/// the vendors add up to the ledger whatever was applied to what. A money document's line is in its own
/// currency, so it is taken to base at the rate the settled bill was booked at; the figure is rounded once.
///
/// <b>Ageing.</b> A bill is aged from its due date, or its bill date when asked; a document with no due date
/// (an opening balance, a journal) from its ledger date. Not yet due is Current; then 1 to one bucket's width,
/// and so on. A debit note and a payment or advance not yet applied are negative and sit in Current, so a
/// vendor's total is what the branch owes them.
/// </summary>
public sealed class AgedPayablesSummaryReportRepository
{
    /// <summary>The name given to the row for what no vendor claims.</summary>
    public const string UnassignedName = "Not tied to a vendor";

    private const long NoContact = 0;

    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public AgedPayablesSummaryReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>What a request's bucket width and age basis come to when left blank. Pure.</summary>
    public static (AgeBasis AgeBy, int BucketDays) Defaults(AgedPayablesSummaryReportRequest request) =>
        AgedReceivablesSummaryReportRepository.Defaults(request.AgeBy, request.BucketDays);

    /// <summary>The refusals a request needs beyond the date being given, with the field each belongs to. Pure.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(AgedPayablesSummaryReportRequest request) =>
        Refusals(request.AgeBy, request.BucketDays);

    /// <summary>
    /// The refusals for an age basis and a bucket width, with the field each belongs to, shared by the reports
    /// that age payables so they refuse the same things in the same words. Pure.
    /// </summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(AgeBasis? ageBy, int? bucketDays)
    {
        List<(string, string)> refusals = [];

        if (bucketDays is int days && !AgedReceivablesSummaryReportRepository.BucketSizes.Contains(days))
        {
            refusals.Add((nameof(AgedPayablesSummaryReportRequest.BucketDays), "Bucket size must be 7, 15 or 30 days."));
        }

        if (ageBy is AgeBasis basis && !Enum.IsDefined(basis))
        {
            refusals.Add((nameof(AgedPayablesSummaryReportRequest.AgeBy), "Age by must be the due date or the bill date."));
        }

        return refusals;
    }

    /// <summary>Whether a vendor is on this branch; the query filter decides, so another branch's reads as none.</summary>
    public Task<bool> ContactExistsAsync(long contactId, CancellationToken ct) =>
        _db.Contacts.AnyAsync(c => c.ContactId == contactId, ct);

    /// <summary>The vendors the filter offers, by name.</summary>
    public Task<List<AgedPayablesVendorChoiceDto>> VendorsAsync(CancellationToken ct) =>
        VendorChoices(_db.Contacts).ToListAsync(ct);

    /// <summary>The vendors a filter offers, by name; the summary's and the detail's. LINQ only.</summary>
    public static IQueryable<AgedPayablesVendorChoiceDto> VendorChoices(IQueryable<ContactRead> contacts) =>
        contacts
            .Where(c => c.IsVendor)
            .OrderBy(c => c.DisplayName)
            .ThenBy(c => c.ContactId)
            .Select(c => new AgedPayablesVendorChoiceDto
            {
                ContactId = c.ContactId,
                ContactCode = c.ContactCode,
                ContactName = c.DisplayName,
            });

    /// <summary>The report for a request the controller has already validated.</summary>
    public async Task<AgedPayablesSummaryReportResultDto> RunAsync(AgedPayablesSummaryReportRequest request, CancellationToken ct)
    {
        DateOnly asAt = request.AsAtDate!.Value;
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

        List<OpenReceivable> rows = await OpenOn(_db, asAt, request.ContactId).ToListAsync(ct);

        // The vendors named by an open document, and every vendor when the zero ones are wanted.
        List<long> named = [.. rows.Where(r => r.ContactId != null).Select(r => r.ContactId!.Value).Distinct()];
        IQueryable<ContactRead> contacts = _db.Contacts;
        contacts = request.ShowZeroBalances
            ? contacts.Where(c => c.IsVendor || named.Contains(c.ContactId))
            : contacts.Where(c => named.Contains(c.ContactId));

        if (request.ContactId is long only)
        {
            contacts = contacts.Where(c => c.ContactId == only);
        }

        List<VendorRef> vendors = await contacts
            .Select(c => new VendorRef { ContactId = c.ContactId, Code = c.ContactCode, Name = c.DisplayName, IsMsme = c.IsMsme })
            .ToListAsync(ct);

        return Compose(request, rows, vendors, baseCurrency);
    }

    // --- builders: LINQ over the read models, so a test runs them over lists -------------

    /// <summary>
    /// The branch's open payable documents on a date, optionally for one vendor: the documents, their
    /// settlements and the bills' dates put together, the way <see cref="AgedReceivablesSummaryReportRepository.OpenOn"/>
    /// does it for receivables. The summary, the detail and the catalog source all start here, so a vendor's
    /// balance is the same figure in each. LINQ only.
    /// </summary>
    public static IQueryable<OpenReceivable> OpenOn(ReportingDbContext db, DateOnly asAt, long? contactId)
    {
        IQueryable<JournalLedgerRead> ledger = db.Ledger;
        if (contactId is long chosen)
        {
            ledger = ledger.Where(l => l.ContactId == chosen);
        }

        IQueryable<ReceivableDocument> documents = Documents(ledger, db.Accounts, asAt);
        return AgedReceivablesSummaryReportRepository.OpenPayableDocuments(documents, Settlements(db.TransactionRatios, documents, asAt), db.Bills);
    }

    /// <summary>
    /// The payable legs up to a date, one row for each document that posted them, signed so that what the
    /// branch owes is positive: credits less debits, in base currency and in the document's own.
    /// </summary>
    public static IQueryable<ReceivableDocument> Documents(
        IQueryable<JournalLedgerRead> ledger, IQueryable<AccountRead> accounts, DateOnly asAt) =>
        AgedReceivablesSummaryReportRepository.Documents(
            ledger, accounts, asAt, ForeignCurrencyGainLossReportRepository.PayableAccountName, -1);

    /// <summary>
    /// The settlements effective on or before the date that this report holds, each as one figure in base
    /// currency: a row of <c>acc.TransactionRatios</c> counts when its source, the bill, is one of
    /// <paramref name="documents"/>, and it is that bill's rate that takes a payment's line to base.
    /// </summary>
    public static IQueryable<ReceivableSettlement> Settlements(
        IQueryable<TransactionRatioRead> ratios, IQueryable<ReceivableDocument> documents, DateOnly asAt) =>
        AgedReceivablesSummaryReportRepository.Settlements(ratios, documents, asAt, payable: true);

    // --- compose: pure, so a test needs no database --------------------------------------

    /// <summary>
    /// The rows, one for each vendor with something open, and the totals. A negative document (a debit note,
    /// an unapplied payment) goes in Current; a positive one in the column its age gives. A vendor whose
    /// columns are all nought is owed nothing and is listed only when asked. Vendors sort by name, and what
    /// no vendor claims comes last. Pure.
    /// </summary>
    public static AgedPayablesSummaryReportResultDto Compose(
        AgedPayablesSummaryReportRequest request,
        IReadOnlyList<OpenReceivable> open,
        IReadOnlyList<VendorRef> vendors,
        string baseCurrency)
    {
        DateOnly asAt = request.AsAtDate!.Value;
        (AgeBasis ageBy, int bucketDays) = Defaults(request);
        int columnCount = AgedReceivablesSummaryReportRepository.ColumnCount;

        Dictionary<long, VendorRef> names = vendors
            .GroupBy(c => c.ContactId)
            .ToDictionary(g => g.Key, g => g.First());

        // Contact ids start at 1, so nought stands for the documents no vendor claims.
        Dictionary<long, decimal[]> columns = [];
        foreach (OpenReceivable document in open)
        {
            long key = document.ContactId ?? NoContact;
            if (!columns.TryGetValue(key, out decimal[]? amounts))
            {
                amounts = new decimal[columnCount];
                columns[key] = amounts;
            }

            amounts[AgedReceivablesSummaryReportRepository.ColumnOf(document, asAt, ageBy, bucketDays)] += document.Amount;
        }

        if (request.ShowZeroBalances)
        {
            foreach (long contactId in names.Keys)
            {
                columns.TryAdd(contactId, new decimal[columnCount]);
            }
        }

        List<AgedPayablesSummaryRowDto> rows = [];
        foreach ((long key, decimal[] amounts) in columns)
        {
            if (!request.ShowZeroBalances && amounts.All(a => a == 0m))
            {
                continue;
            }

            long? contactId = key == NoContact ? null : key;
            VendorRef? vendor = names.GetValueOrDefault(key);
            rows.Add(RowOf(
                contactId,
                vendor?.Code ?? string.Empty,
                contactId is null ? UnassignedName : vendor?.Name ?? $"Vendor {key}",
                vendor?.IsMsme ?? false,
                amounts));
        }

        List<AgedPayablesSummaryRowDto> ordered =
        [
            .. rows
                .OrderBy(r => r.ContactId is null)
                .ThenBy(r => r.ContactName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.ContactCode, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.ContactId),
        ];

        decimal[] totals = new decimal[columnCount];
        foreach (AgedPayablesSummaryRowDto row in ordered)
        {
            totals[0] += row.Current;
            totals[1] += row.Bucket1;
            totals[2] += row.Bucket2;
            totals[3] += row.Bucket3;
            totals[4] += row.Bucket4;
        }

        return new AgedPayablesSummaryReportResultDto
        {
            AsAtDate = asAt,
            AgeBy = ageBy,
            BucketDays = bucketDays,
            BaseCurrencyCode = baseCurrency,
            BucketLabels = [.. AgedReceivablesSummaryReportRepository.BucketLabels(bucketDays)],
            Vendors = ordered,
            Totals = RowOf(null, string.Empty, "Total", false, totals),
        };
    }

    private static AgedPayablesSummaryRowDto RowOf(long? contactId, string code, string name, bool isMsme, decimal[] amounts) => new()
    {
        ContactId = contactId,
        ContactCode = code,
        ContactName = name,
        IsMsme = isMsme,
        Current = amounts[0],
        Bucket1 = amounts[1],
        Bucket2 = amounts[2],
        Bucket3 = amounts[3],
        Bucket4 = amounts[4],
        Total = amounts.Sum(),
    };
}

/// <summary>A vendor's code and name, and whether it is a micro or small enterprise.</summary>
public sealed class VendorRef
{
    public long ContactId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsMsme { get; set; }
}

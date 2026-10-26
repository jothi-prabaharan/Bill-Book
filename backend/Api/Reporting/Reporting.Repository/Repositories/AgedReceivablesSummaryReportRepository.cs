using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Aged Receivables Summary (TK-222): what each customer owed on a date and how
/// overdue it was, as Current and four overdue columns.
///
/// <b>A document is what is aged.</b> The branch's Accounts Receivable legs up to the
/// date are grouped by the document that posted them (<see cref="Documents"/>): an
/// invoice, a credit note, a receipt, an opening balance, a manual journal. A document
/// that nets to nothing is not open. What settled a document is read the way the Settle
/// documents workspace reads it, from <c>acc.TransactionRatios</c> that are not voided
/// and were effective on or before the date (<see cref="Settlements"/>): a receipt, a credit
/// note or an advance applied with Allocate takes its amount off the invoice it settled
/// <b>and</b> off its own, so one settlement reduces both ends by the same figure and the
/// customers add up to the ledger whatever was applied to what.
///
/// <b>A settlement is one figure in base currency.</b> A money document's line is in the
/// document's own currency, so its amount is taken to base at the rate its target
/// document was booked at (the document's base total over its total in its currency),
/// the rate the receipt's receivable leg is posted at; any other allocation is already
/// in base. The figure is rounded to two places once and used at both ends.
///
/// <b>Ageing.</b> An invoice is aged from its due date, or its invoice date when asked;
/// a document with no due date (an opening balance, a journal) from its ledger date.
/// Not yet due is Current; then 1 to one bucket's width, and so on, with the last
/// column everything past three buckets. A credit note and a receipt or advance not yet
/// applied are negative, and sit in Current, so a customer's total is their balance on
/// the receivable account.
///
/// The same open documents feed the catalog's <c>ar-aging-summary</c> source and, later,
/// the detail report, so the three agree.
/// </summary>
public sealed class AgedReceivablesSummaryReportRepository
{
    /// <summary>The widths an overdue column may have, in days; the first is the default.</summary>
    public static readonly IReadOnlyList<int> BucketSizes = [30, 15, 7];

    /// <summary>How many columns the report has: Current and four overdue ones.</summary>
    public const int ColumnCount = 5;

    /// <summary>The name given to the row for what no customer claims.</summary>
    public const string UnassignedName = "Not tied to a customer";

    private const long NoContact = 0;

    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public AgedReceivablesSummaryReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>What a request's bucket width and age basis come to when left blank. Pure.</summary>
    public static (AgeBasis AgeBy, int BucketDays) Defaults(AgedReceivablesSummaryReportRequest request) =>
        (request.AgeBy ?? AgeBasis.DueDate, request.BucketDays ?? BucketSizes[0]);

    /// <summary>The refusals a request needs beyond the date being given, with the field each belongs to. Pure.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(AgedReceivablesSummaryReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.BucketDays is int days && !BucketSizes.Contains(days))
        {
            refusals.Add((nameof(AgedReceivablesSummaryReportRequest.BucketDays), "Bucket size must be 7, 15 or 30 days."));
        }

        if (request.AgeBy is AgeBasis basis && !Enum.IsDefined(basis))
        {
            refusals.Add((nameof(AgedReceivablesSummaryReportRequest.AgeBy), "Age by must be the due date or the invoice date."));
        }

        return refusals;
    }

    /// <summary>The five column headings for a bucket width, Current first. Pure.</summary>
    public static IReadOnlyList<string> BucketLabels(int bucketDays) =>
    [
        "Current",
        $"1-{bucketDays} days",
        $"{bucketDays + 1}-{2 * bucketDays} days",
        $"{(2 * bucketDays) + 1}-{3 * bucketDays} days",
        $"Over {3 * bucketDays} days",
    ];

    /// <summary>
    /// Which column an amount falls in, 0 for Current to 4 for the last, from how many
    /// days past the date it counts from the as-at date is. Not yet due, or due on the
    /// date, is Current. Pure.
    /// </summary>
    public static int ColumnFor(int daysOverdue, int bucketDays) =>
        daysOverdue <= 0 ? 0
        : daysOverdue <= bucketDays ? 1
        : daysOverdue <= 2 * bucketDays ? 2
        : daysOverdue <= 3 * bucketDays ? 3
        : 4;

    /// <summary>Whether a customer is on this branch; the query filter decides, so another branch's reads as none.</summary>
    public Task<bool> ContactExistsAsync(long contactId, CancellationToken ct) =>
        _db.Contacts.AnyAsync(c => c.ContactId == contactId, ct);

    /// <summary>The customers the filter offers, by name. LINQ only.</summary>
    public Task<List<AgedReceivablesCustomerChoiceDto>> CustomersAsync(CancellationToken ct) =>
        _db.Contacts
            .Where(c => c.IsCustomer)
            .OrderBy(c => c.DisplayName)
            .ThenBy(c => c.ContactId)
            .Select(c => new AgedReceivablesCustomerChoiceDto
            {
                ContactId = c.ContactId,
                ContactCode = c.ContactCode,
                ContactName = c.DisplayName,
            })
            .ToListAsync(ct);

    /// <summary>The report for a request the controller has already validated.</summary>
    public async Task<AgedReceivablesSummaryReportResultDto> RunAsync(AgedReceivablesSummaryReportRequest request, CancellationToken ct)
    {
        DateOnly asAt = request.AsAtDate!.Value;
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

        IQueryable<JournalLedgerRead> ledger = _db.Ledger;
        if (request.ContactId is long chosen)
        {
            ledger = ledger.Where(l => l.ContactId == chosen);
        }

        IQueryable<ReceivableDocument> documents = Documents(ledger, _db.Accounts, asAt);
        IQueryable<OpenReceivable> open = OpenDocuments(documents, Settlements(_db.TransactionRatios, documents, asAt), _db.Invoices);

        List<OpenReceivable> rows = await open.ToListAsync(ct);

        // The customers named by an open document, and every customer when the zero ones are wanted.
        List<long> named = [.. rows.Where(r => r.ContactId != null).Select(r => r.ContactId!.Value).Distinct()];
        IQueryable<ContactRead> contacts = _db.Contacts;
        contacts = request.ShowZeroBalances
            ? contacts.Where(c => c.IsCustomer || named.Contains(c.ContactId))
            : contacts.Where(c => named.Contains(c.ContactId));

        if (request.ContactId is long only)
        {
            contacts = contacts.Where(c => c.ContactId == only);
        }

        List<CustomerRef> customers = await contacts
            .Select(c => new CustomerRef { ContactId = c.ContactId, Code = c.ContactCode, Name = c.DisplayName })
            .ToListAsync(ct);

        return Compose(request, rows, customers, baseCurrency);
    }

    // --- builders: LINQ over the read models, so a test runs them over lists -------------

    /// <summary>
    /// The receivable legs up to a date, one row for each document that posted them: what
    /// it came to in base currency and in its own, its earliest ledger date and its number.
    /// A payment's bank leg is on another account, so the account decides what counts.
    /// </summary>
    public static IQueryable<ReceivableDocument> Documents(
        IQueryable<JournalLedgerRead> ledger, IQueryable<AccountRead> accounts, DateOnly asAt) =>
        from l in ledger
        where l.LedgerDate <= asAt
        join a in accounts on l.AccountId equals a.AccountId
        where a.AccountSystemName == ForeignCurrencyGainLossReportRepository.ReceivableAccountName
        group l by new { l.ContactId, l.TransactionTypeCode, l.TransactionId } into g
        select new ReceivableDocument
        {
            ContactId = g.Key.ContactId,
            TypeCode = g.Key.TransactionTypeCode,
            DocumentId = g.Key.TransactionId,
            NetBase = g.Sum(x => x.DebitAmountBase) - g.Sum(x => x.CreditAmountBase),
            NetSource = g.Sum(x => x.DebitAmount) - g.Sum(x => x.CreditAmount),
            LedgerDate = g.Min(x => x.LedgerDate),
            DocumentNo = g.Max(x => x.DocumentNo),
        };

    /// <summary>
    /// The settlements effective on or before the date that this report holds, each as one
    /// figure in base currency. A row of <c>acc.TransactionRatios</c> counts only when its
    /// target is one of <paramref name="documents"/>: it is that document's rate that takes
    /// a money document's line to base, and a settlement of a document this report does not
    /// hold is none of its business. Voided rows hold nothing. The source is the credit
    /// side (a receipt, a credit note, an advance) and the target the debit side (an
    /// invoice), the way Accounting writes them.
    /// </summary>
    public static IQueryable<ReceivableSettlement> Settlements(
        IQueryable<TransactionRatioRead> ratios, IQueryable<ReceivableDocument> documents, DateOnly asAt) =>
        from t in ratios
        where !t.IsVoided && t.AllocationDate <= asAt
        join d in documents
            on new { Code = t.TargetTransactionTypeCode, Id = t.TargetTransactionId }
            equals new { Code = d.TypeCode, Id = d.DocumentId }
        select new ReceivableSettlement
        {
            SourceTypeCode = t.SourceTransactionTypeCode,
            SourceId = t.SourceTransactionId,
            TargetTypeCode = t.TargetTransactionTypeCode,
            TargetId = t.TargetTransactionId,
            Amount = Math.Round(
                (t.SourceTransactionTypeCode == "RCM" || t.SourceTransactionTypeCode == "SPM"
                    || t.TargetTransactionTypeCode == "RCM" || t.TargetTransactionTypeCode == "SPM")
                && d.NetSource != 0m
                    ? t.Amount * Math.Abs(d.NetBase) / Math.Abs(d.NetSource)
                    : t.Amount,
                2),
        };

    /// <summary>
    /// The documents that still have something open: each with what is left once its
    /// settlements are taken into account, signed (what is owed positive, a credit not yet
    /// applied negative), and the dates it can be aged from. A settlement takes its amount
    /// off the target and off the source, so the amounts add up to the ledger's balance
    /// whatever was applied to what. An invoice brings its own invoice and due dates; any
    /// other document its ledger date for both. A document with nothing left is gone.
    /// </summary>
    public static IQueryable<OpenReceivable> OpenDocuments(
        IQueryable<ReceivableDocument> documents, IQueryable<ReceivableSettlement> settlements, IQueryable<InvoiceRead> invoices)
    {
        // Each document's own balance, less what settled it as the debit side, plus what it settled as the credit side.
        IQueryable<ReceivableDelta> deltas = documents
            .Select(d => new ReceivableDelta { TypeCode = d.TypeCode, DocumentId = d.DocumentId, Amount = d.NetBase })
            .Concat(settlements.Select(s => new ReceivableDelta { TypeCode = s.TargetTypeCode, DocumentId = s.TargetId, Amount = -s.Amount }))
            .Concat(settlements.Select(s => new ReceivableDelta { TypeCode = s.SourceTypeCode, DocumentId = s.SourceId, Amount = s.Amount }))
            .GroupBy(x => new { x.TypeCode, x.DocumentId })
            .Select(g => new ReceivableDelta { TypeCode = g.Key.TypeCode, DocumentId = g.Key.DocumentId, Amount = g.Sum(x => x.Amount) });

        return from d in documents
               where d.NetBase != 0m
               join t in deltas
                   on new { Code = d.TypeCode, Id = d.DocumentId }
                   equals new { Code = t.TypeCode, Id = t.DocumentId }
               where t.Amount != 0m
               join i in invoices
                   on new { Code = d.TypeCode, Id = d.DocumentId }
                   equals new { Code = i.TransactionTypeCode, Id = i.InvoiceId }
                   into raised
               from i in raised.DefaultIfEmpty()
               select new OpenReceivable
               {
                   ContactId = d.ContactId,
                   TypeCode = d.TypeCode,
                   DocumentId = d.DocumentId,
                   DocumentNo = d.DocumentNo,
                   DocumentDate = i == null ? d.LedgerDate : i.DocumentDate,
                   DueDate = i == null ? d.LedgerDate : (i.DueDate ?? i.DocumentDate),
                   Amount = t.Amount,
               };
    }

    // --- compose: pure, so a test needs no database --------------------------------------

    /// <summary>
    /// The rows, one for each customer with something open, and the totals. A negative
    /// document (a credit note, an unapplied receipt) goes in Current; a positive one in
    /// the column its age gives. A customer whose columns are all nought has nothing
    /// owed and is listed only when asked. Customers sort by name, and what no customer
    /// claims comes last. Pure.
    /// </summary>
    public static AgedReceivablesSummaryReportResultDto Compose(
        AgedReceivablesSummaryReportRequest request,
        IReadOnlyList<OpenReceivable> open,
        IReadOnlyList<CustomerRef> customers,
        string baseCurrency)
    {
        DateOnly asAt = request.AsAtDate!.Value;
        (AgeBasis ageBy, int bucketDays) = Defaults(request);

        Dictionary<long, CustomerRef> names = customers
            .GroupBy(c => c.ContactId)
            .ToDictionary(g => g.Key, g => g.First());

        // Contact ids start at 1, so nought stands for the documents no customer claims.
        Dictionary<long, decimal[]> columns = [];
        foreach (OpenReceivable document in open)
        {
            long key = document.ContactId ?? NoContact;
            if (!columns.TryGetValue(key, out decimal[]? amounts))
            {
                amounts = new decimal[ColumnCount];
                columns[key] = amounts;
            }

            DateOnly counted = ageBy == AgeBasis.InvoiceDate ? document.DocumentDate : document.DueDate;
            int column = document.Amount < 0m ? 0 : ColumnFor(asAt.DayNumber - counted.DayNumber, bucketDays);
            amounts[column] += document.Amount;
        }

        if (request.ShowZeroBalances)
        {
            foreach (long contactId in names.Keys)
            {
                columns.TryAdd(contactId, new decimal[ColumnCount]);
            }
        }

        List<AgedReceivablesSummaryRowDto> rows = [];
        foreach ((long key, decimal[] amounts) in columns)
        {
            if (!request.ShowZeroBalances && amounts.All(a => a == 0m))
            {
                continue;
            }

            long? contactId = key == NoContact ? null : key;
            CustomerRef? customer = names.GetValueOrDefault(key);
            rows.Add(RowOf(
                contactId,
                customer?.Code ?? string.Empty,
                contactId is null ? UnassignedName : customer?.Name ?? $"Customer {key}",
                amounts));
        }

        List<AgedReceivablesSummaryRowDto> ordered =
        [
            .. rows
                .OrderBy(r => r.ContactId is null)
                .ThenBy(r => r.ContactName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.ContactCode, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.ContactId),
        ];

        decimal[] totals = new decimal[ColumnCount];
        foreach (AgedReceivablesSummaryRowDto row in ordered)
        {
            totals[0] += row.Current;
            totals[1] += row.Bucket1;
            totals[2] += row.Bucket2;
            totals[3] += row.Bucket3;
            totals[4] += row.Bucket4;
        }

        return new AgedReceivablesSummaryReportResultDto
        {
            AsAtDate = asAt,
            AgeBy = ageBy,
            BucketDays = bucketDays,
            BaseCurrencyCode = baseCurrency,
            BucketLabels = [.. BucketLabels(bucketDays)],
            Customers = ordered,
            Totals = RowOf(null, string.Empty, "Total", totals),
        };
    }

    private static AgedReceivablesSummaryRowDto RowOf(long? contactId, string code, string name, decimal[] amounts) => new()
    {
        ContactId = contactId,
        ContactCode = code,
        ContactName = name,
        Current = amounts[0],
        Bucket1 = amounts[1],
        Bucket2 = amounts[2],
        Bucket3 = amounts[3],
        Bucket4 = amounts[4],
        Total = amounts.Sum(),
    };
}

/// <summary>One document's receivable legs up to the date, netted. Positive is owed to the branch.</summary>
public sealed class ReceivableDocument
{
    public long? ContactId { get; set; }

    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    /// <summary>Debits less credits in base currency.</summary>
    public decimal NetBase { get; set; }

    /// <summary>Debits less credits in the document's own currency.</summary>
    public decimal NetSource { get; set; }

    public DateOnly LedgerDate { get; set; }

    public string? DocumentNo { get; set; }
}

/// <summary>One settlement between two documents, in base currency, always positive.</summary>
public sealed class ReceivableSettlement
{
    public string SourceTypeCode { get; set; } = string.Empty;

    public long SourceId { get; set; }

    public string TargetTypeCode { get; set; } = string.Empty;

    public long TargetId { get; set; }

    public decimal Amount { get; set; }
}

/// <summary>A signed amount against a document: its balance, or a settlement's effect on one end of it.</summary>
public sealed class ReceivableDelta
{
    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    public decimal Amount { get; set; }
}

/// <summary>
/// A document with something still open on the date: <see cref="Amount"/> is positive for
/// what is owed and negative for a credit not yet applied, and the two dates are what it
/// can be aged from.
/// </summary>
public sealed class OpenReceivable
{
    public long? ContactId { get; set; }

    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    public string? DocumentNo { get; set; }

    public DateOnly DocumentDate { get; set; }

    public DateOnly DueDate { get; set; }

    public decimal Amount { get; set; }
}

/// <summary>A customer's code and name.</summary>
public sealed class CustomerRef
{
    public long ContactId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}

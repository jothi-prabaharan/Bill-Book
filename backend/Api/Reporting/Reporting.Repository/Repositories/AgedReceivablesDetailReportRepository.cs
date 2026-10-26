using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Aged Receivables Detail (TK-223): the Aged Receivables Summary one document at a
/// time. For each customer, every open invoice, credit note, receipt, advance, opening
/// balance or journal on the date, with what it came to, what has been applied to it, what
/// is left, and the amount placed in the column its age gives.
///
/// <b>It does not work anything out for itself.</b> The open documents are the summary's
/// (<see cref="AgedReceivablesSummaryReportRepository.OpenOn"/>: documents, settlements, open
/// amounts as signed deltas), the column an amount falls in is the summary's
/// (<see cref="AgedReceivablesSummaryReportRepository.ColumnOf"/>) and the defaults and
/// refusals are the summary's, so a customer's subtotal and every column total are the
/// summary's figures by construction. A document applied in full before the date is not listed;
/// one applied after it is listed with what was open then, because settlements count only if
/// they were effective on or before the date.
///
/// A receipt or payment takes its number from its own document, which the ledger cannot carry
/// (money documents are numbered in the transaction after the one that posts them); anything
/// else has the number on its ledger rows, else its kind and id.
/// </summary>
public sealed class AgedReceivablesDetailReportRepository
{
    private const string ReceiveMoneyCode = "RCM";
    private const string SpendMoneyCode = "SPM";

    // Contact ids start at 1, so nought stands for the documents no customer claims.
    private const long NoContact = 0;

    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public AgedReceivablesDetailReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>The refusals a request needs beyond the date being given, the summary's. Pure.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(AgedReceivablesDetailReportRequest request) =>
        AgedReceivablesSummaryReportRepository.Refusals(request.AgeBy, request.BucketDays);

    /// <summary>What a document is, from the code its postings carry. Pure.</summary>
    public static ReceivableDocumentKind KindOf(string typeCode) => typeCode switch
    {
        "INV" or "POS" => ReceivableDocumentKind.Invoice,
        "CRN" => ReceivableDocumentKind.CreditNote,
        ReceiveMoneyCode => ReceivableDocumentKind.Receipt,
        SpendMoneyCode => ReceivableDocumentKind.Payment,
        "JRN" => ReceivableDocumentKind.Journal,
        "OPB" => ReceivableDocumentKind.OpeningBalance,
        _ => ReceivableDocumentKind.Other,
    };

    /// <summary>Whether a customer is on this branch; the query filter decides, so another branch's reads as none.</summary>
    public Task<bool> ContactExistsAsync(long contactId, CancellationToken ct) =>
        _db.Contacts.AnyAsync(c => c.ContactId == contactId, ct);

    /// <summary>The customers the filter offers, by name: the summary's list.</summary>
    public Task<List<AgedReceivablesCustomerChoiceDto>> CustomersAsync(CancellationToken ct) =>
        AgedReceivablesSummaryReportRepository.CustomerChoices(_db.Contacts).ToListAsync(ct);

    /// <summary>The report for a request the controller has already validated.</summary>
    public async Task<AgedReceivablesDetailReportResultDto> RunAsync(AgedReceivablesDetailReportRequest request, CancellationToken ct)
    {
        DateOnly asAt = request.AsAtDate!.Value;
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

        List<OpenReceivable> open = await AgedReceivablesSummaryReportRepository.OpenOn(_db, asAt, request.ContactId).ToListAsync(ct);

        List<long> named = [.. open.Where(o => o.ContactId != null).Select(o => o.ContactId!.Value).Distinct()];
        List<CustomerRef> customers = named.Count == 0
            ? []
            : await _db.Contacts
                .Where(c => named.Contains(c.ContactId))
                .Select(c => new CustomerRef { ContactId = c.ContactId, Code = c.ContactCode, Name = c.DisplayName })
                .ToListAsync(ct);

        // Only a receipt or payment needs its number looked up, and only for the ones on the report.
        List<long> receiptIds = [.. open.Where(o => o.TypeCode == ReceiveMoneyCode && o.DocumentNo == null).Select(o => o.DocumentId)];
        List<long> paymentIds = [.. open.Where(o => o.TypeCode == SpendMoneyCode && o.DocumentNo == null).Select(o => o.DocumentId)];

        List<MoneyNumber> numbers = [];
        if (receiptIds.Count > 0)
        {
            numbers.AddRange(await ReceiptNumbers(_db.ReceiveMoney, receiptIds).ToListAsync(ct));
        }

        if (paymentIds.Count > 0)
        {
            numbers.AddRange(await PaymentNumbers(_db.SpendMoney, paymentIds).ToListAsync(ct));
        }

        return Compose(request, open, customers, numbers, baseCurrency);
    }

    // --- builders: LINQ over the read models, so a test runs them over lists -------------

    /// <summary>The numbers of the receipts with these ids; a receipt not yet numbered has none and is left out. LINQ only.</summary>
    public static IQueryable<MoneyNumber> ReceiptNumbers(IQueryable<ReceiveMoneyRead> receipts, IReadOnlyCollection<long> ids) =>
        receipts
            .Where(m => ids.Contains(m.ReceiveMoneyId) && m.TransactionNo != null)
            .Select(m => new MoneyNumber { TypeCode = ReceiveMoneyCode, DocumentId = m.ReceiveMoneyId, Number = m.TransactionNo! });

    /// <summary>The numbers of the payments with these ids; a payment not yet numbered has none and is left out. LINQ only.</summary>
    public static IQueryable<MoneyNumber> PaymentNumbers(IQueryable<SpendMoneyRead> payments, IReadOnlyCollection<long> ids) =>
        payments
            .Where(m => ids.Contains(m.SpendMoneyId) && m.TransactionNo != null)
            .Select(m => new MoneyNumber { TypeCode = SpendMoneyCode, DocumentId = m.SpendMoneyId, Number = m.TransactionNo! });

    // --- compose: pure, so a test needs no database --------------------------------------

    /// <summary>
    /// The customers, each with its open documents oldest first, a subtotal and the totals.
    /// Customers sort by name and what no customer claims comes last, as on the summary. A
    /// document is placed in the column the summary's rule gives; what was applied to it is
    /// its original amount less what is left. Pure.
    /// </summary>
    public static AgedReceivablesDetailReportResultDto Compose(
        AgedReceivablesDetailReportRequest request,
        IReadOnlyList<OpenReceivable> open,
        IReadOnlyList<CustomerRef> customers,
        IReadOnlyList<MoneyNumber> numbers,
        string baseCurrency)
    {
        DateOnly asAt = request.AsAtDate!.Value;
        (AgeBasis ageBy, int bucketDays) = AgedReceivablesSummaryReportRepository.Defaults(request.AgeBy, request.BucketDays);

        Dictionary<long, CustomerRef> names = customers.GroupBy(c => c.ContactId).ToDictionary(g => g.Key, g => g.First());
        Dictionary<(string, long), string> money = numbers
            .GroupBy(n => (n.TypeCode, n.DocumentId))
            .ToDictionary(g => g.Key, g => g.First().Number);

        List<AgedReceivablesDetailCustomerDto> rows = [];
        foreach (IGrouping<long, OpenReceivable> group in open.GroupBy(o => o.ContactId ?? NoContact))
        {
            CustomerRef? customer = names.GetValueOrDefault(group.Key);
            List<AgedReceivablesDetailDocumentDto> documents =
            [
                .. group
                    .OrderBy(o => o.DocumentDate)
                    .ThenBy(o => o.TypeCode, StringComparer.Ordinal)
                    .ThenBy(o => o.DocumentId)
                    .Select(o => DocumentOf(o, asAt, ageBy, bucketDays, money)),
            ];

            rows.Add(new AgedReceivablesDetailCustomerDto
            {
                ContactId = group.Key == NoContact ? null : group.Key,
                ContactCode = customer?.Code ?? string.Empty,
                ContactName = group.Key == NoContact
                    ? AgedReceivablesSummaryReportRepository.UnassignedName
                    : customer?.Name ?? $"Customer {group.Key}",
                Documents = documents,
                Subtotal = TotalOf(documents),
            });
        }

        List<AgedReceivablesDetailCustomerDto> ordered =
        [
            .. rows
                .OrderBy(r => r.ContactId is null)
                .ThenBy(r => r.ContactName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.ContactCode, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.ContactId),
        ];

        return new AgedReceivablesDetailReportResultDto
        {
            AsAtDate = asAt,
            AgeBy = ageBy,
            BucketDays = bucketDays,
            BaseCurrencyCode = baseCurrency,
            BucketLabels = [.. AgedReceivablesSummaryReportRepository.BucketLabels(bucketDays)],
            Customers = ordered,
            Totals = TotalOf([.. ordered.SelectMany(c => c.Documents)]),
        };
    }

    private static AgedReceivablesDetailDocumentDto DocumentOf(
        OpenReceivable open, DateOnly asAt, AgeBasis ageBy, int bucketDays, IReadOnlyDictionary<(string, long), string> money)
    {
        ReceivableDocumentKind kind = KindOf(open.TypeCode);
        int column = AgedReceivablesSummaryReportRepository.ColumnOf(open, asAt, ageBy, bucketDays);
        DateOnly counted = ageBy == AgeBasis.InvoiceDate ? open.DocumentDate : open.DueDate;

        var document = new AgedReceivablesDetailDocumentDto
        {
            Kind = kind,
            TypeCode = open.TypeCode,
            DocumentId = open.DocumentId,
            DocumentNo = open.DocumentNo
                ?? money.GetValueOrDefault((open.TypeCode, open.DocumentId))
                ?? $"{open.TypeCode}-{open.DocumentId}",
            DocumentDate = open.DocumentDate,
            DueDate = kind == ReceivableDocumentKind.Invoice ? open.DueDate : null,
            DaysOverdue = open.Amount > 0m && asAt.DayNumber > counted.DayNumber ? asAt.DayNumber - counted.DayNumber : null,
            Original = open.Original,
            Settled = open.Original - open.Amount,
            Outstanding = open.Amount,
        };

        switch (column)
        {
            case 0: document.Current = open.Amount; break;
            case 1: document.Bucket1 = open.Amount; break;
            case 2: document.Bucket2 = open.Amount; break;
            case 3: document.Bucket3 = open.Amount; break;
            default: document.Bucket4 = open.Amount; break;
        }

        return document;
    }

    private static AgedReceivablesDetailTotalsDto TotalOf(IReadOnlyCollection<AgedReceivablesDetailDocumentDto> documents) => new()
    {
        Original = documents.Sum(d => d.Original),
        Settled = documents.Sum(d => d.Settled),
        Outstanding = documents.Sum(d => d.Outstanding),
        Current = documents.Sum(d => d.Current),
        Bucket1 = documents.Sum(d => d.Bucket1),
        Bucket2 = documents.Sum(d => d.Bucket2),
        Bucket3 = documents.Sum(d => d.Bucket3),
        Bucket4 = documents.Sum(d => d.Bucket4),
    };
}

/// <summary>A receipt's or payment's own number, which the ledger cannot carry.</summary>
public sealed class MoneyNumber
{
    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    public string Number { get; set; } = string.Empty;
}

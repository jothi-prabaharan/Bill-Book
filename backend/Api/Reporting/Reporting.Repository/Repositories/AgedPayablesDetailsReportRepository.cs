using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Aged Payables Details (TK-234): the Aged Payables Summary one document at a time. For each vendor, every open bill,
/// debit note, payment, vendor refund, opening balance or journal on the date, with what it came to, what has been applied
/// to it, what is left, and the amount placed in the column its age gives.
///
/// <b>It does not work anything out for itself.</b> The open documents are the summary's
/// (<see cref="AgedPayablesSummaryReportRepository.OpenOn"/>), the column an amount falls in is the receivable reports'
/// shared rule (<see cref="AgedReceivablesSummaryReportRepository.ColumnOf"/>) and the defaults and refusals are the
/// summary's, so a vendor's subtotal and every column total are the summary's figures by construction. A document applied
/// in full before the date is not listed; one applied after it is listed with what was open then, because settlements count
/// only if they were effective on or before the date.
///
/// A payment or receipt takes its number from its own document, which the ledger cannot carry (money documents are
/// numbered in the transaction after the one that posts them); anything else has the number on its ledger rows, else its
/// kind and id.
/// </summary>
public sealed class AgedPayablesDetailsReportRepository
{
    private const string ReceiveMoneyCode = "RCM";
    private const string SpendMoneyCode = "SPM";

    // Contact ids start at 1, so nought stands for the documents no vendor claims.
    private const long NoContact = 0;

    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public AgedPayablesDetailsReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>The refusals a request needs beyond the date being given, the summary's. Pure.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(AgedPayablesDetailsReportRequest request) =>
        AgedPayablesSummaryReportRepository.Refusals(request.AgeBy, request.BucketDays);

    /// <summary>What a document is, from the code its postings carry. Pure.</summary>
    public static PayableDocumentKind KindOf(string typeCode) => typeCode switch
    {
        "BIL" => PayableDocumentKind.Bill,
        "DBN" => PayableDocumentKind.DebitNote,
        SpendMoneyCode => PayableDocumentKind.Payment,
        ReceiveMoneyCode => PayableDocumentKind.Receipt,
        "JRN" => PayableDocumentKind.Journal,
        "OPB" => PayableDocumentKind.OpeningBalance,
        _ => PayableDocumentKind.Other,
    };

    /// <summary>Whether a vendor is on this branch; the query filter decides, so another branch's reads as none.</summary>
    public Task<bool> ContactExistsAsync(long contactId, CancellationToken ct) =>
        _db.Contacts.AnyAsync(c => c.ContactId == contactId, ct);

    /// <summary>The vendors the filter offers, by name: the summary's list.</summary>
    public Task<List<AgedPayablesVendorChoiceDto>> VendorsAsync(CancellationToken ct) =>
        AgedPayablesSummaryReportRepository.VendorChoices(_db.Contacts).ToListAsync(ct);

    /// <summary>The report for a request the controller has already validated.</summary>
    public async Task<AgedPayablesDetailsReportResultDto> RunAsync(AgedPayablesDetailsReportRequest request, CancellationToken ct)
    {
        DateOnly asAt = request.AsAtDate!.Value;
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

        List<OpenReceivable> open = await AgedPayablesSummaryReportRepository.OpenOn(_db, asAt, request.ContactId).ToListAsync(ct);

        List<long> named = [.. open.Where(o => o.ContactId != null).Select(o => o.ContactId!.Value).Distinct()];
        List<VendorRef> vendors = named.Count == 0
            ? []
            : await _db.Contacts
                .Where(c => named.Contains(c.ContactId))
                .Select(c => new VendorRef { ContactId = c.ContactId, Code = c.ContactCode, Name = c.DisplayName, IsMsme = c.IsMsme })
                .ToListAsync(ct);

        // Only a payment or receipt needs its number looked up, and only for the ones on the report.
        List<long> receiptIds = [.. open.Where(o => o.TypeCode == ReceiveMoneyCode && o.DocumentNo == null).Select(o => o.DocumentId)];
        List<long> paymentIds = [.. open.Where(o => o.TypeCode == SpendMoneyCode && o.DocumentNo == null).Select(o => o.DocumentId)];

        List<MoneyNumber> numbers = [];
        if (receiptIds.Count > 0)
        {
            numbers.AddRange(await AgedReceivablesDetailReportRepository.ReceiptNumbers(_db.ReceiveMoney, receiptIds).ToListAsync(ct));
        }

        if (paymentIds.Count > 0)
        {
            numbers.AddRange(await AgedReceivablesDetailReportRepository.PaymentNumbers(_db.SpendMoney, paymentIds).ToListAsync(ct));
        }

        return Compose(request, open, vendors, numbers, baseCurrency);
    }

    // --- compose: pure, so a test needs no database --------------------------------------

    /// <summary>
    /// The vendors, each with its open documents oldest first, a subtotal and the totals. Vendors sort by name and what no
    /// vendor claims comes last, as on the summary. A document is placed in the column the shared rule gives; what was
    /// applied to it is its original amount less what is left. Pure.
    /// </summary>
    public static AgedPayablesDetailsReportResultDto Compose(
        AgedPayablesDetailsReportRequest request,
        IReadOnlyList<OpenReceivable> open,
        IReadOnlyList<VendorRef> vendors,
        IReadOnlyList<MoneyNumber> numbers,
        string baseCurrency)
    {
        DateOnly asAt = request.AsAtDate!.Value;
        (AgeBasis ageBy, int bucketDays) = AgedReceivablesSummaryReportRepository.Defaults(request.AgeBy, request.BucketDays);

        Dictionary<long, VendorRef> names = vendors.GroupBy(c => c.ContactId).ToDictionary(g => g.Key, g => g.First());
        Dictionary<(string, long), string> money = numbers
            .GroupBy(n => (n.TypeCode, n.DocumentId))
            .ToDictionary(g => g.Key, g => g.First().Number);

        List<AgedPayablesDetailsVendorDto> rows = [];
        foreach (IGrouping<long, OpenReceivable> group in open.GroupBy(o => o.ContactId ?? NoContact))
        {
            VendorRef? vendor = names.GetValueOrDefault(group.Key);
            List<AgedPayablesDetailsDocumentDto> documents =
            [
                .. group
                    .OrderBy(o => o.DocumentDate)
                    .ThenBy(o => o.TypeCode, StringComparer.Ordinal)
                    .ThenBy(o => o.DocumentId)
                    .Select(o => DocumentOf(o, asAt, ageBy, bucketDays, money)),
            ];

            rows.Add(new AgedPayablesDetailsVendorDto
            {
                ContactId = group.Key == NoContact ? null : group.Key,
                ContactCode = vendor?.Code ?? string.Empty,
                ContactName = group.Key == NoContact
                    ? AgedPayablesSummaryReportRepository.UnassignedName
                    : vendor?.Name ?? $"Vendor {group.Key}",
                IsMsme = vendor?.IsMsme ?? false,
                Documents = documents,
                Subtotal = TotalOf(documents),
            });
        }

        List<AgedPayablesDetailsVendorDto> ordered =
        [
            .. rows
                .OrderBy(r => r.ContactId is null)
                .ThenBy(r => r.ContactName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.ContactCode, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.ContactId),
        ];

        return new AgedPayablesDetailsReportResultDto
        {
            AsAtDate = asAt,
            AgeBy = ageBy,
            BucketDays = bucketDays,
            BaseCurrencyCode = baseCurrency,
            BucketLabels = [.. AgedReceivablesSummaryReportRepository.BucketLabels(bucketDays)],
            Vendors = ordered,
            Totals = TotalOf([.. ordered.SelectMany(c => c.Documents)]),
        };
    }

    private static AgedPayablesDetailsDocumentDto DocumentOf(
        OpenReceivable open, DateOnly asAt, AgeBasis ageBy, int bucketDays, IReadOnlyDictionary<(string, long), string> money)
    {
        PayableDocumentKind kind = KindOf(open.TypeCode);
        int column = AgedReceivablesSummaryReportRepository.ColumnOf(open, asAt, ageBy, bucketDays);
        DateOnly counted = ageBy == AgeBasis.InvoiceDate ? open.DocumentDate : open.DueDate;

        var document = new AgedPayablesDetailsDocumentDto
        {
            Kind = kind,
            TypeCode = open.TypeCode,
            DocumentId = open.DocumentId,
            DocumentNo = open.DocumentNo
                ?? money.GetValueOrDefault((open.TypeCode, open.DocumentId))
                ?? $"{open.TypeCode}-{open.DocumentId}",
            DocumentDate = open.DocumentDate,
            DueDate = kind == PayableDocumentKind.Bill ? open.DueDate : null,
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

    private static AgedPayablesDetailsTotalsDto TotalOf(IReadOnlyCollection<AgedPayablesDetailsDocumentDto> documents) => new()
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

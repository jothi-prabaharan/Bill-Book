using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Bill/DN Payment Report (TK-238): what was paid to vendors in a period and which bills each
/// payment settled, a page of payments at a time. A payment is a posted <c>acc.SpendMoney</c> dated
/// in the period (a draft was never paid and a voided payment was taken back) that is not a TDS
/// challan, which pays the government rather than a vendor; the documents it settled are the
/// not-voided <c>acc.TransactionRatios</c> rows that name it as their <b>target</b>: on the payable
/// side the bill is the credit-side document, so it is the source and the payment the target.
///
/// <b>A payment is in its own currency.</b> What left the bank, what was settled and what was
/// not are in that currency; the base figure is the amount over the payment's rate (a money
/// document's rate is foreign units per base unit), rounded to two places as the ledger does, so
/// the paid total equals the posted payments' total. A payment's lines settle what left the bank
/// <i>and</i> the TDS withheld from the vendor on an advance, so what it did not settle is those
/// two together less what it settled: an advance, or an overpayment, on account.
///
/// The totals, the vendors' totals and the page are each one query over every payment the
/// filters match, so none is worked out from the loaded page; only the documents of the page's own
/// payments are read after it.
/// </summary>
public sealed class BillDnPaymentReportRepository
{
    private const int TopVendorCount = 10;

    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public BillDnPaymentReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>The refusals a request needs beyond both dates being given, with the field each belongs to. Pure.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(BillDnPaymentReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from)
        {
            refusals.Add((nameof(BillDnPaymentReportRequest.ToDate), "To date cannot be before the from date."));
        }

        if (request.PaymentMethod is PaymentMethod method && !Enum.IsDefined(method))
        {
            refusals.Add((nameof(BillDnPaymentReportRequest.PaymentMethod), "Payment method must be one of the methods offered."));
        }

        return refusals;
    }

    /// <summary>Whether a vendor is on this branch; the query filter decides, so another branch's reads as none.</summary>
    public Task<bool> ContactExistsAsync(long contactId, CancellationToken ct) =>
        _db.Contacts.AnyAsync(c => c.ContactId == contactId, ct);

    /// <summary>Whether a bank account is on this branch; another branch's reads as none.</summary>
    public Task<bool> BankAccountExistsAsync(long bankAccountId, CancellationToken ct) =>
        _db.BankAccounts.AnyAsync(b => b.BankAccountId == bankAccountId, ct);

    /// <summary>The vendors the filter offers, by name: the Aged Payables list.</summary>
    public Task<List<AgedPayablesVendorChoiceDto>> VendorsAsync(CancellationToken ct) =>
        AgedPayablesSummaryReportRepository.VendorChoices(_db.Contacts).ToListAsync(ct);

    /// <summary>The bank accounts the filter offers, by name.</summary>
    public Task<List<PaymentBankAccountChoiceDto>> BankAccountsAsync(CancellationToken ct) =>
        BankAccountChoices(_db.BankAccounts).ToListAsync(ct);

    /// <summary>The report for a request the controller has already validated.</summary>
    public async Task<BillDnPaymentReportResultDto> RunAsync(BillDnPaymentReportRequest request, CancellationToken ct)
    {
        DateOnly start = request.FromDate!.Value;
        DateOnly end = request.ToDate!.Value;
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

        IQueryable<PaymentRow> rows = Rows(Payments(_db.SpendMoney, request, start, end), _db.TransactionRatios);

        BillDnPaymentTotalsDto totals = await rows
            .GroupBy(r => 1)
            .Select(g => new BillDnPaymentTotalsDto
            {
                Payments = g.Count(),
                Paid = g.Sum(r => r.AmountBase),
                Unallocated = g.Sum(r => r.UnallocatedBase),
            })
            .FirstOrDefaultAsync(ct) ?? new BillDnPaymentTotalsDto();

        List<VendorPaymentRow> top = await VendorTotals(rows).Take(TopVendorCount + 1).ToListAsync(ct);
        int vendorCount = await rows.Select(r => r.ContactId).Distinct().CountAsync(ct);

        List<PaymentRow> page = await rows
            .OrderByDescending(r => r.PaymentDate)
            .ThenByDescending(r => r.SpendMoneyId)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        List<long> paymentIds = [.. page.Select(r => r.SpendMoneyId)];
        List<PaymentSettlementRow> settlements = paymentIds.Count == 0 ? [] : await Settlements(_db.TransactionRatios, paymentIds).ToListAsync(ct);

        List<long> billIds = [.. settlements.Where(s => s.TypeCode == "BIL").Select(s => s.DocumentId).Distinct()];
        List<BillRef> bills = billIds.Count == 0 ? [] : await BillRefs(_db.Bills, billIds).ToListAsync(ct);

        List<long> contactIds = [.. page.Select(r => r.ContactId).Concat(top.Select(t => t.ContactId)).Distinct()];
        List<VendorRef> vendors = contactIds.Count == 0
            ? []
            : await _db.Contacts
                .Where(c => contactIds.Contains(c.ContactId))
                .Select(c => new VendorRef { ContactId = c.ContactId, Code = c.ContactCode, Name = c.DisplayName })
                .ToListAsync(ct);

        List<long> bankIds = [.. page.Select(r => r.BankAccountId).Distinct()];
        List<BankAccountRef> banks = bankIds.Count == 0
            ? []
            : await _db.BankAccounts
                .Where(b => bankIds.Contains(b.BankAccountId))
                .Select(b => new BankAccountRef { BankAccountId = b.BankAccountId, Name = b.AccountName })
                .ToListAsync(ct);

        return Compose(request, page, totals, top, vendorCount, settlements, bills, vendors, banks, baseCurrency);
    }

    // --- builders: LINQ over the read models, so a test runs them over lists -------------

    /// <summary>The posted payments dated in the period, narrowed by vendor, bank account and payment method.</summary>
    public static IQueryable<SpendMoneyRead> Payments(
        IQueryable<SpendMoneyRead> payments, BillDnPaymentReportRequest request, DateOnly start, DateOnly end)
    {
        IQueryable<SpendMoneyRead> matching = payments.Where(r =>
            r.Status == MoneyDocumentStatus.Posted && !r.IsTdsChallan && r.TransactionDate >= start && r.TransactionDate <= end);

        if (request.ContactId is long contact)
        {
            matching = matching.Where(r => r.ContactId == contact);
        }

        if (request.BankAccountId is long bank)
        {
            matching = matching.Where(r => r.BankAccountId == bank);
        }

        if (request.PaymentMethod is PaymentMethod method)
        {
            matching = matching.Where(r => r.PaymentMethod == method);
        }

        return matching;
    }

    /// <summary>
    /// What each payment's lines should settle (what reached the bank and what was withheld as TDS)
    /// and what its settlements did, added up together in one pass so no payment is joined to a
    /// possibly empty sub-query. The rate rides along on the payment's own side.
    /// </summary>
    public static IQueryable<PaymentSum> Sums(IQueryable<SpendMoneyRead> payments, IQueryable<TransactionRatioRead> ratios) =>
        payments
            .Select(r => new PaymentSum { SpendMoneyId = r.SpendMoneyId, Gross = r.Amount + r.TdsAmount, Settled = 0m, Rate = r.ExchangeRate })
            .Concat(
                from t in ratios
                join r in payments on t.TargetTransactionId equals r.SpendMoneyId
                where !t.IsVoided && t.TargetTransactionTypeCode == "SPM"
                select new PaymentSum { SpendMoneyId = r.SpendMoneyId, Gross = 0m, Settled = t.Amount, Rate = 0m })
            .GroupBy(x => x.SpendMoneyId)
            .Select(g => new PaymentSum
            {
                SpendMoneyId = g.Key,
                Gross = g.Sum(x => x.Gross),
                Settled = g.Sum(x => x.Settled),
                Rate = g.Max(x => x.Rate),
            });

    /// <summary>
    /// One row for each payment with what it settled and what it did not, in its own currency and in
    /// base. The base figure is the amount over the rate, rounded to two places, as the ledger takes
    /// it; a rate of one is the amount as it is.
    /// </summary>
    public static IQueryable<PaymentRow> Rows(IQueryable<SpendMoneyRead> payments, IQueryable<TransactionRatioRead> ratios) =>
        from r in payments
        join s in Sums(payments, ratios) on r.SpendMoneyId equals s.SpendMoneyId
        select new PaymentRow
        {
            SpendMoneyId = r.SpendMoneyId,
            PaymentNo = r.TransactionNo ?? string.Empty,
            PaymentDate = r.TransactionDate,
            ContactId = r.ContactId,
            BankAccountId = r.BankAccountId,
            PaymentMethod = r.PaymentMethod,
            ReferenceNo = r.ReferenceNo,
            CurrencyCode = r.CurrencyCode,
            Amount = r.Amount,
            Tds = r.TdsAmount,
            Settled = s.Settled,
            Unallocated = s.Gross - s.Settled,
            AmountBase = r.ExchangeRate > 0m && r.ExchangeRate != 1m ? Math.Round(r.Amount / r.ExchangeRate, 2) : r.Amount,
            UnallocatedBase = r.ExchangeRate > 0m && r.ExchangeRate != 1m ? Math.Round((s.Gross - s.Settled) / r.ExchangeRate, 2) : s.Gross - s.Settled,
        };

    /// <summary>What each vendor was paid in the payments, in base currency, largest first.</summary>
    public static IQueryable<VendorPaymentRow> VendorTotals(IQueryable<PaymentRow> rows) =>
        rows
            .GroupBy(r => r.ContactId)
            .Select(g => new VendorPaymentRow { ContactId = g.Key, Payments = g.Count(), Paid = g.Sum(r => r.AmountBase) })
            .OrderByDescending(c => c.Paid)
            .ThenBy(c => c.ContactId);

    /// <summary>The documents the given payments settled: the not-voided settlements that name a payment as their target.</summary>
    public static IQueryable<PaymentSettlementRow> Settlements(IQueryable<TransactionRatioRead> ratios, IReadOnlyCollection<long> paymentIds) =>
        ratios
            .Where(t => !t.IsVoided && t.TargetTransactionTypeCode == "SPM" && paymentIds.Contains(t.TargetTransactionId))
            .Select(t => new PaymentSettlementRow
            {
                SpendMoneyId = t.TargetTransactionId,
                TypeCode = t.SourceTransactionTypeCode,
                DocumentId = t.SourceTransactionId,
                Amount = t.Amount,
                AllocationDate = t.AllocationDate,
            });

    /// <summary>The number and date of the given bills; </summary>
    public static IQueryable<BillRef> BillRefs(IQueryable<BillRead> bills, IReadOnlyCollection<long> billIds) =>
        bills
            .Where(i => billIds.Contains(i.BillId))
            .Select(i => new BillRef { BillId = i.BillId, DocumentNo = i.DocumentNo, VendorBillNo = i.VendorBillNo, DocumentDate = i.DocumentDate });

    /// <summary>The bank accounts a filter offers, by name. LINQ only.</summary>
    public static IQueryable<PaymentBankAccountChoiceDto> BankAccountChoices(IQueryable<BankAccountRead> accounts) =>
        accounts
            .OrderBy(b => b.AccountName)
            .ThenBy(b => b.BankAccountId)
            .Select(b => new PaymentBankAccountChoiceDto { BankAccountId = b.BankAccountId, AccountName = b.AccountName, CurrencyCode = b.CurrencyCode });

    // --- compose: pure, so a test needs no database --------------------------------------

    /// <summary>
    /// The page's payments with their vendors, bank accounts and the documents each settled, the
    /// totals, and the vendors paid most. The ten largest vendors are listed and the rest
    /// are added into one line, so the list is short whatever the branch's size. Pure.
    /// </summary>
    public static BillDnPaymentReportResultDto Compose(
        BillDnPaymentReportRequest request,
        IReadOnlyList<PaymentRow> page,
        BillDnPaymentTotalsDto totals,
        IReadOnlyList<VendorPaymentRow> topVendors,
        int vendorCount,
        IReadOnlyList<PaymentSettlementRow> settlements,
        IReadOnlyList<BillRef> bills,
        IReadOnlyList<VendorRef> vendors,
        IReadOnlyList<BankAccountRef> banks,
        string baseCurrency)
    {
        Dictionary<long, VendorRef> names = vendors.GroupBy(c => c.ContactId).ToDictionary(g => g.Key, g => g.First());
        Dictionary<long, string> bankNames = banks.GroupBy(b => b.BankAccountId).ToDictionary(g => g.Key, g => g.First().Name);
        Dictionary<long, BillRef> numbered = bills.GroupBy(i => i.BillId).ToDictionary(g => g.Key, g => g.First());
        ILookup<long, PaymentSettlementRow> byPayment = settlements.ToLookup(s => s.SpendMoneyId);

        List<VendorPaymentRow> listed = [.. topVendors.Take(TopVendorCount)];

        return new BillDnPaymentReportResultDto
        {
            FromDate = request.FromDate!.Value,
            ToDate = request.ToDate!.Value,
            BaseCurrencyCode = baseCurrency,
            Payments =
            [
                .. page.Select(r =>
                {
                    VendorRef? vendor = names.GetValueOrDefault(r.ContactId);
                    return new VendorPaymentDto
                    {
                        SpendMoneyId = r.SpendMoneyId,
                        PaymentNo = r.PaymentNo,
                        PaymentDate = r.PaymentDate,
                        ContactId = r.ContactId,
                        ContactCode = vendor?.Code ?? string.Empty,
                        ContactName = vendor?.Name ?? $"Vendor {r.ContactId}",
                        BankAccountId = r.BankAccountId,
                        BankAccountName = bankNames.GetValueOrDefault(r.BankAccountId) ?? $"Account {r.BankAccountId}",
                        PaymentMethod = r.PaymentMethod,
                        ReferenceNo = r.ReferenceNo,
                        CurrencyCode = r.CurrencyCode,
                        Amount = r.Amount,
                        AmountBase = r.AmountBase,
                        TdsAmount = r.Tds,
                        Settled = r.Settled,
                        Unallocated = r.Unallocated,
                        Documents =
                        [
                            .. byPayment[r.SpendMoneyId]
                                .Select(s =>
                                {
                                    bool isBill = s.TypeCode == "BIL";
                                    BillRef? bill = isBill ? numbered.GetValueOrDefault(s.DocumentId) : null;
                                    return new PaidDocumentDto
                                    {
                                        TypeCode = s.TypeCode,
                                        DocumentId = s.DocumentId,
                                        DocumentNo = bill?.DocumentNo ?? $"{s.TypeCode}-{s.DocumentId}",
                                        VendorBillNo = bill?.VendorBillNo,
                                        DocumentDate = bill?.DocumentDate,
                                        AllocationDate = s.AllocationDate,
                                        Amount = s.Amount,
                                    };
                                })
                                .OrderBy(d => d.DocumentDate ?? d.AllocationDate)
                                .ThenBy(d => d.TypeCode, StringComparer.Ordinal)
                                .ThenBy(d => d.DocumentId),
                        ],
                    };
                }),
            ],
            TotalPayments = totals.Payments,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            Totals = totals,
            TopVendors =
            [
                .. listed.Select(t =>
                {
                    VendorRef? vendor = names.GetValueOrDefault(t.ContactId);
                    return new VendorPaidDto
                    {
                        ContactId = t.ContactId,
                        ContactCode = vendor?.Code ?? string.Empty,
                        ContactName = vendor?.Name ?? $"Vendor {t.ContactId}",
                        Payments = t.Payments,
                        Paid = t.Paid,
                    };
                }),
            ],
            OtherVendors = Math.Max(0, vendorCount - listed.Count),
            OtherVendorsPaid = totals.Paid - listed.Sum(t => t.Paid),
        };
    }
}

/// <summary>What a payment's lines should settle and what its settlements did, added up.</summary>
public sealed class PaymentSum
{
    public long SpendMoneyId { get; set; }

    /// <summary>What reached the bank plus the TDS withheld, in the payment's own currency.</summary>
    public decimal Gross { get; set; }

    /// <summary>What went to documents, in the payment's own currency.</summary>
    public decimal Settled { get; set; }

    public decimal Rate { get; set; }
}

/// <summary>A posted payment with what it settled and what it did not, in its own currency and in base.</summary>
public sealed class PaymentRow
{
    public long SpendMoneyId { get; set; }

    public string PaymentNo { get; set; } = string.Empty;

    public DateOnly PaymentDate { get; set; }

    public long ContactId { get; set; }

    public long BankAccountId { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public string? ReferenceNo { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public decimal Tds { get; set; }

    public decimal Settled { get; set; }

    public decimal Unallocated { get; set; }

    public decimal AmountBase { get; set; }

    public decimal UnallocatedBase { get; set; }
}

/// <summary>What one vendor paid, in base currency.</summary>
public sealed class VendorPaymentRow
{
    public long ContactId { get; set; }

    public int Payments { get; set; }

    public decimal Paid { get; set; }
}

/// <summary>One settlement of a document by a payment.</summary>
public sealed class PaymentSettlementRow
{
    public long SpendMoneyId { get; set; }

    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    public decimal Amount { get; set; }

    public DateOnly AllocationDate { get; set; }
}

/// <summary>A bill's number and date.</summary>
public sealed class BillRef
{
    public long BillId { get; set; }

    public string DocumentNo { get; set; } = string.Empty;

    public string? VendorBillNo { get; set; }

    public DateOnly DocumentDate { get; set; }
}

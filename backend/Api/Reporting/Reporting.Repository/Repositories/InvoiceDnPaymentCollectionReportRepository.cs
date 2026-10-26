using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Invoice/DN Payment Collection Report (TK-227): what was collected in a period and which
/// documents each receipt settled, a page of receipts at a time. A receipt is a posted
/// <c>acc.ReceiveMoney</c> dated in the period (a draft was never collected and a voided receipt
/// was taken back); the documents it settled are the not-voided <c>acc.TransactionRatios</c> rows
/// that name it as their source, which is how posting a receipt records what its lines settle.
///
/// <b>A receipt is in its own currency.</b> What reached the bank, what was settled and what was
/// not are in that currency; the base figure is the amount over the receipt's rate (a money
/// document's rate is foreign units per base unit), rounded to two places as the ledger does, so
/// the collected total equals the posted receipts' total. A receipt's lines settle what reached
/// the bank <i>and</i> what the customer withheld as TDS, so what it did not settle is those two
/// together less what it settled: an advance, or an overpayment, on account.
///
/// The totals, the customers' totals and the page are each one query over every receipt the
/// filters match, so none is worked out from the loaded page; only the documents of the page's own
/// receipts are read after it.
/// </summary>
public sealed class InvoiceDnPaymentCollectionReportRepository
{
    private const int TopCustomerCount = 10;

    private readonly ReportingDbContext _db;
    private readonly IBaseCurrencyProvider _currency;

    public InvoiceDnPaymentCollectionReportRepository(ReportingDbContext db, IBaseCurrencyProvider currency)
    {
        _db = db;
        _currency = currency;
    }

    /// <summary>The refusals a request needs beyond both dates being given, with the field each belongs to. Pure.</summary>
    public static IReadOnlyList<(string Field, string Message)> Refusals(InvoiceDnPaymentCollectionReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from)
        {
            refusals.Add((nameof(InvoiceDnPaymentCollectionReportRequest.ToDate), "To date cannot be before the from date."));
        }

        if (request.PaymentMethod is PaymentMethod method && !Enum.IsDefined(method))
        {
            refusals.Add((nameof(InvoiceDnPaymentCollectionReportRequest.PaymentMethod), "Payment method must be one of the methods offered."));
        }

        return refusals;
    }

    /// <summary>Whether a customer is on this branch; the query filter decides, so another branch's reads as none.</summary>
    public Task<bool> ContactExistsAsync(long contactId, CancellationToken ct) =>
        _db.Contacts.AnyAsync(c => c.ContactId == contactId, ct);

    /// <summary>Whether a bank account is on this branch; another branch's reads as none.</summary>
    public Task<bool> BankAccountExistsAsync(long bankAccountId, CancellationToken ct) =>
        _db.BankAccounts.AnyAsync(b => b.BankAccountId == bankAccountId, ct);

    /// <summary>The customers the filter offers, by name: the Aged Receivables list.</summary>
    public Task<List<AgedReceivablesCustomerChoiceDto>> CustomersAsync(CancellationToken ct) =>
        AgedReceivablesSummaryReportRepository.CustomerChoices(_db.Contacts).ToListAsync(ct);

    /// <summary>The bank accounts the filter offers, by name.</summary>
    public Task<List<CollectionBankAccountChoiceDto>> BankAccountsAsync(CancellationToken ct) =>
        BankAccountChoices(_db.BankAccounts).ToListAsync(ct);

    /// <summary>The report for a request the controller has already validated.</summary>
    public async Task<InvoiceDnPaymentCollectionReportResultDto> RunAsync(InvoiceDnPaymentCollectionReportRequest request, CancellationToken ct)
    {
        DateOnly start = request.FromDate!.Value;
        DateOnly end = request.ToDate!.Value;
        string baseCurrency = await _currency.GetBaseCurrencyAsync(ct) ?? string.Empty;

        IQueryable<CollectionRow> rows = Rows(Receipts(_db.ReceiveMoney, request, start, end), _db.TransactionRatios);

        InvoiceDnPaymentCollectionTotalsDto totals = await rows
            .GroupBy(r => 1)
            .Select(g => new InvoiceDnPaymentCollectionTotalsDto
            {
                Receipts = g.Count(),
                Collected = g.Sum(r => r.AmountBase),
                Unallocated = g.Sum(r => r.UnallocatedBase),
            })
            .FirstOrDefaultAsync(ct) ?? new InvoiceDnPaymentCollectionTotalsDto();

        List<CustomerCollectionRow> top = await CustomerTotals(rows).Take(TopCustomerCount + 1).ToListAsync(ct);
        int customerCount = await rows.Select(r => r.ContactId).Distinct().CountAsync(ct);

        List<CollectionRow> page = await rows
            .OrderByDescending(r => r.ReceiptDate)
            .ThenByDescending(r => r.ReceiveMoneyId)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        List<long> receiptIds = [.. page.Select(r => r.ReceiveMoneyId)];
        List<SettlementRow> settlements = receiptIds.Count == 0 ? [] : await Settlements(_db.TransactionRatios, receiptIds).ToListAsync(ct);

        List<long> invoiceIds = [.. settlements.Where(s => s.TypeCode is "INV" or "POS").Select(s => s.DocumentId).Distinct()];
        List<InvoiceRef> invoices = invoiceIds.Count == 0 ? [] : await InvoiceRefs(_db.Invoices, invoiceIds).ToListAsync(ct);

        List<long> contactIds = [.. page.Select(r => r.ContactId).Concat(top.Select(t => t.ContactId)).Distinct()];
        List<CustomerRef> customers = contactIds.Count == 0
            ? []
            : await _db.Contacts
                .Where(c => contactIds.Contains(c.ContactId))
                .Select(c => new CustomerRef { ContactId = c.ContactId, Code = c.ContactCode, Name = c.DisplayName })
                .ToListAsync(ct);

        List<long> bankIds = [.. page.Select(r => r.BankAccountId).Distinct()];
        List<BankAccountRef> banks = bankIds.Count == 0
            ? []
            : await _db.BankAccounts
                .Where(b => bankIds.Contains(b.BankAccountId))
                .Select(b => new BankAccountRef { BankAccountId = b.BankAccountId, Name = b.AccountName })
                .ToListAsync(ct);

        return Compose(request, page, totals, top, customerCount, settlements, invoices, customers, banks, baseCurrency);
    }

    // --- builders: LINQ over the read models, so a test runs them over lists -------------

    /// <summary>The posted receipts dated in the period, narrowed by customer, bank account and payment method.</summary>
    public static IQueryable<ReceiveMoneyRead> Receipts(
        IQueryable<ReceiveMoneyRead> receipts, InvoiceDnPaymentCollectionReportRequest request, DateOnly start, DateOnly end)
    {
        IQueryable<ReceiveMoneyRead> matching = receipts.Where(r =>
            r.Status == MoneyDocumentStatus.Posted && r.TransactionDate >= start && r.TransactionDate <= end);

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
    /// What each receipt's lines should settle (what reached the bank and what was withheld as TDS)
    /// and what its settlements did, added up together in one pass so no receipt is joined to a
    /// possibly empty sub-query. The rate rides along on the receipt's own side.
    /// </summary>
    public static IQueryable<CollectionSum> Sums(IQueryable<ReceiveMoneyRead> receipts, IQueryable<TransactionRatioRead> ratios) =>
        receipts
            .Select(r => new CollectionSum { ReceiveMoneyId = r.ReceiveMoneyId, Gross = r.Amount + r.TdsAmount, Settled = 0m, Rate = r.ExchangeRate })
            .Concat(
                from t in ratios
                join r in receipts on t.SourceTransactionId equals r.ReceiveMoneyId
                where !t.IsVoided && t.SourceTransactionTypeCode == "RCM"
                select new CollectionSum { ReceiveMoneyId = r.ReceiveMoneyId, Gross = 0m, Settled = t.Amount, Rate = 0m })
            .GroupBy(x => x.ReceiveMoneyId)
            .Select(g => new CollectionSum
            {
                ReceiveMoneyId = g.Key,
                Gross = g.Sum(x => x.Gross),
                Settled = g.Sum(x => x.Settled),
                Rate = g.Max(x => x.Rate),
            });

    /// <summary>
    /// One row for each receipt with what it settled and what it did not, in its own currency and in
    /// base. The base figure is the amount over the rate, rounded to two places, as the ledger takes
    /// it; a rate of one is the amount as it is.
    /// </summary>
    public static IQueryable<CollectionRow> Rows(IQueryable<ReceiveMoneyRead> receipts, IQueryable<TransactionRatioRead> ratios) =>
        from r in receipts
        join s in Sums(receipts, ratios) on r.ReceiveMoneyId equals s.ReceiveMoneyId
        select new CollectionRow
        {
            ReceiveMoneyId = r.ReceiveMoneyId,
            ReceiptNo = r.TransactionNo ?? string.Empty,
            ReceiptDate = r.TransactionDate,
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

    /// <summary>What each customer paid in the receipts, in base currency, largest first.</summary>
    public static IQueryable<CustomerCollectionRow> CustomerTotals(IQueryable<CollectionRow> rows) =>
        rows
            .GroupBy(r => r.ContactId)
            .Select(g => new CustomerCollectionRow { ContactId = g.Key, Receipts = g.Count(), Collected = g.Sum(r => r.AmountBase) })
            .OrderByDescending(c => c.Collected)
            .ThenBy(c => c.ContactId);

    /// <summary>The documents the given receipts settled: the not-voided settlements that name a receipt as their source.</summary>
    public static IQueryable<SettlementRow> Settlements(IQueryable<TransactionRatioRead> ratios, IReadOnlyCollection<long> receiptIds) =>
        ratios
            .Where(t => !t.IsVoided && t.SourceTransactionTypeCode == "RCM" && receiptIds.Contains(t.SourceTransactionId))
            .Select(t => new SettlementRow
            {
                ReceiveMoneyId = t.SourceTransactionId,
                TypeCode = t.TargetTransactionTypeCode,
                DocumentId = t.TargetTransactionId,
                Amount = t.Amount,
                AllocationDate = t.AllocationDate,
            });

    /// <summary>The number and date of the given invoices; the id is unique across invoices and POS sales.</summary>
    public static IQueryable<InvoiceRef> InvoiceRefs(IQueryable<InvoiceRead> invoices, IReadOnlyCollection<long> invoiceIds) =>
        invoices
            .Where(i => invoiceIds.Contains(i.InvoiceId))
            .Select(i => new InvoiceRef { InvoiceId = i.InvoiceId, DocumentNo = i.DocumentNo, DocumentDate = i.DocumentDate });

    /// <summary>The bank accounts a filter offers, by name. LINQ only.</summary>
    public static IQueryable<CollectionBankAccountChoiceDto> BankAccountChoices(IQueryable<BankAccountRead> accounts) =>
        accounts
            .OrderBy(b => b.AccountName)
            .ThenBy(b => b.BankAccountId)
            .Select(b => new CollectionBankAccountChoiceDto { BankAccountId = b.BankAccountId, AccountName = b.AccountName, CurrencyCode = b.CurrencyCode });

    // --- compose: pure, so a test needs no database --------------------------------------

    /// <summary>
    /// The page's receipts with their customers, bank accounts and the documents each settled, the
    /// totals, and the customers who paid most. The ten largest customers are listed and the rest
    /// are added into one line, so the list is short whatever the branch's size. Pure.
    /// </summary>
    public static InvoiceDnPaymentCollectionReportResultDto Compose(
        InvoiceDnPaymentCollectionReportRequest request,
        IReadOnlyList<CollectionRow> page,
        InvoiceDnPaymentCollectionTotalsDto totals,
        IReadOnlyList<CustomerCollectionRow> topCustomers,
        int customerCount,
        IReadOnlyList<SettlementRow> settlements,
        IReadOnlyList<InvoiceRef> invoices,
        IReadOnlyList<CustomerRef> customers,
        IReadOnlyList<BankAccountRef> banks,
        string baseCurrency)
    {
        Dictionary<long, CustomerRef> names = customers.GroupBy(c => c.ContactId).ToDictionary(g => g.Key, g => g.First());
        Dictionary<long, string> bankNames = banks.GroupBy(b => b.BankAccountId).ToDictionary(g => g.Key, g => g.First().Name);
        Dictionary<long, InvoiceRef> numbered = invoices.GroupBy(i => i.InvoiceId).ToDictionary(g => g.Key, g => g.First());
        ILookup<long, SettlementRow> byReceipt = settlements.ToLookup(s => s.ReceiveMoneyId);

        List<CustomerCollectionRow> listed = [.. topCustomers.Take(TopCustomerCount)];

        return new InvoiceDnPaymentCollectionReportResultDto
        {
            FromDate = request.FromDate!.Value,
            ToDate = request.ToDate!.Value,
            BaseCurrencyCode = baseCurrency,
            Receipts =
            [
                .. page.Select(r =>
                {
                    CustomerRef? customer = names.GetValueOrDefault(r.ContactId);
                    return new ReceiptCollectionDto
                    {
                        ReceiveMoneyId = r.ReceiveMoneyId,
                        ReceiptNo = r.ReceiptNo,
                        ReceiptDate = r.ReceiptDate,
                        ContactId = r.ContactId,
                        ContactCode = customer?.Code ?? string.Empty,
                        ContactName = customer?.Name ?? $"Customer {r.ContactId}",
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
                            .. byReceipt[r.ReceiveMoneyId]
                                .Select(s =>
                                {
                                    bool isInvoice = s.TypeCode is "INV" or "POS";
                                    InvoiceRef? invoice = isInvoice ? numbered.GetValueOrDefault(s.DocumentId) : null;
                                    return new SettledDocumentDto
                                    {
                                        TypeCode = s.TypeCode,
                                        DocumentId = s.DocumentId,
                                        DocumentNo = invoice?.DocumentNo ?? $"{s.TypeCode}-{s.DocumentId}",
                                        DocumentDate = invoice?.DocumentDate,
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
            TotalReceipts = totals.Receipts,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            Totals = totals,
            TopCustomers =
            [
                .. listed.Select(t =>
                {
                    CustomerRef? customer = names.GetValueOrDefault(t.ContactId);
                    return new CustomerCollectionDto
                    {
                        ContactId = t.ContactId,
                        ContactCode = customer?.Code ?? string.Empty,
                        ContactName = customer?.Name ?? $"Customer {t.ContactId}",
                        Receipts = t.Receipts,
                        Collected = t.Collected,
                    };
                }),
            ],
            OtherCustomers = Math.Max(0, customerCount - listed.Count),
            OtherCustomersCollected = totals.Collected - listed.Sum(t => t.Collected),
        };
    }
}

/// <summary>What a receipt's lines should settle and what its settlements did, added up.</summary>
public sealed class CollectionSum
{
    public long ReceiveMoneyId { get; set; }

    /// <summary>What reached the bank plus the TDS withheld, in the receipt's own currency.</summary>
    public decimal Gross { get; set; }

    /// <summary>What went to documents, in the receipt's own currency.</summary>
    public decimal Settled { get; set; }

    public decimal Rate { get; set; }
}

/// <summary>A posted receipt with what it settled and what it did not, in its own currency and in base.</summary>
public sealed class CollectionRow
{
    public long ReceiveMoneyId { get; set; }

    public string ReceiptNo { get; set; } = string.Empty;

    public DateOnly ReceiptDate { get; set; }

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

/// <summary>What one customer paid, in base currency.</summary>
public sealed class CustomerCollectionRow
{
    public long ContactId { get; set; }

    public int Receipts { get; set; }

    public decimal Collected { get; set; }
}

/// <summary>One settlement of a document by a receipt.</summary>
public sealed class SettlementRow
{
    public long ReceiveMoneyId { get; set; }

    public string TypeCode { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    public decimal Amount { get; set; }

    public DateOnly AllocationDate { get; set; }
}

/// <summary>An invoice's number and date.</summary>
public sealed class InvoiceRef
{
    public long InvoiceId { get; set; }

    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }
}

/// <summary>A bank account's name.</summary>
public sealed class BankAccountRef
{
    public long BankAccountId { get; set; }

    public string Name { get; set; } = string.Empty;
}

using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using static Reporting.Repository.Repositories.BatchedNameResolverRepository;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Cash Flow Statement – Direct (TK-201): every movement of cash in a
/// period, traced to where it came from or went, and grouped into operating,
/// investing and financing activities, with the cash at each end.
///
/// <b>Cash is the accounts marked <c>IsBank</c></b>: the cash, wallet, bank,
/// overdraft and card accounts Banking provisions under Cash in Hand, Bank
/// Accounts and Bank OD &amp; Credit Cards. Business Performance's total cash
/// balance reads the same flag, so the two agree.
///
/// <b>A movement is traced through its own posting</b>, one transaction type,
/// id and date in <c>acc.JournalLedger</c>. A posting balances, so the cash it
/// moves equals the credits less debits of its other accounts, and each of
/// those accounts accounts for exactly its own share: a cash sale's sales
/// account for the price, its output GST account for the tax. No allocation is
/// needed. Two things are left out of a posting first, both balanced among
/// themselves: the cost-of-sales and goods-delivered pairs an invoice carries,
/// which move no cash. Opening-balance postings are not flows either: they are
/// counted in the cash at the beginning.
///
/// <b>Each account falls on a line</b> by its system name and type (see
/// <see cref="Classify"/>). GST, TDS, round-off and realized exchange legs go
/// with the rest of their posting when that falls on one line, so a customer's
/// receipt counts in full under receipts from customers whether it settled an
/// invoice or paid for a cash sale; standing alone, as when GST is paid to the
/// government, they keep their own line. Fixed-asset legs go to purchases or
/// sales by whether their posting paid cash out or brought it in.
///
/// Every figure is in base currency, scoped to the token's branch by the query filter.
/// </summary>
public sealed class CashFlowReportRepository
{
    // mst.LedgerTypes and mst.LedgerSources ids, contractual: the same numbers the posters use.
    private const int CogsLedgerType = 4;
    private const int GdniLedgerType = 7;
    private const int OpeningBalanceSource = 13;

    // AccountSystemName as Accounting.Entity.Enums.SystemAccountNames spells it.
    // A branch may rename an account; it cannot rename these.
    private const string AccountsReceivable = "Accounts Receivable";
    private const string AccountsPayable = "Accounts Payable";
    private const string SalaryPayable = "Salary Payable";
    private const string Inventory = "Inventory";
    private const string GoodsReceivedNotInvoiced = "Goods Received Not Invoiced";
    private const string GoodsDeliveredNotInvoiced = "Goods Delivered Not Invoiced";
    private const string FixedAsset = "Fixed Asset";
    private const string AssetDisposalGainLoss = "Gain/Loss on Asset Disposal";
    private const string RoundOff = "Round Off";
    private const string RealizedFxGainLoss = "Realized FX Gain/Loss";
    private const string UnrealizedFxGainLoss = "Unrealized FX Gain/Loss";

    private static readonly HashSet<string> TaxAccounts =
    [
        "Input GST", "Output GST", "GST Payable (Reverse Charge)", "TDS Receivable", "TDS Payable",
    ];

    private static readonly (CashFlowActivity Activity, string Title, CashFlowLine[] Lines)[] Layout =
    [
        (CashFlowActivity.Operating, "Operating activities",
            [CashFlowLine.ReceiptsFromCustomers, CashFlowLine.PaymentsToSuppliersAndEmployees,
             CashFlowLine.GstAndTds, CashFlowLine.OtherOperating]),
        (CashFlowActivity.Investing, "Investing activities",
            [CashFlowLine.FixedAssetPurchases, CashFlowLine.FixedAssetSales]),
        (CashFlowActivity.Financing, "Financing activities",
            [CashFlowLine.CapitalAndDrawings]),
    ];

    private readonly ReportingDbContext _db;
    private readonly BatchedNameResolverRepository _master;

    public CashFlowReportRepository(ReportingDbContext db, BatchedNameResolverRepository master)
    {
        _db = db;
        _master = master;
    }

    /// <summary>
    /// Every reason the request's dates cannot be run, by field, empty when
    /// there is none. A comparison period needs both dates or neither. Pure.
    /// </summary>
    public static IReadOnlyList<(string Field, string Message)> DateRefusals(CashFlowReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from)
        {
            refusals.Add((nameof(CashFlowReportRequest.ToDate), "To date cannot be before the from date."));
        }

        if (request.CompareFromDate is null && request.CompareToDate is not null)
        {
            refusals.Add((nameof(CashFlowReportRequest.CompareFromDate), "Give both comparison dates, or neither."));
        }
        else if (request.CompareFromDate is not null && request.CompareToDate is null)
        {
            refusals.Add((nameof(CashFlowReportRequest.CompareToDate), "Give both comparison dates, or neither."));
        }
        else if (request.CompareFromDate is DateOnly compareFrom && request.CompareToDate is DateOnly compareTo
            && compareTo < compareFrom)
        {
            refusals.Add((nameof(CashFlowReportRequest.CompareToDate), "Comparison to date cannot be before its from date."));
        }

        return refusals;
    }

    /// <summary>The statement for a request the controller has already validated.</summary>
    public async Task<CashFlowReportResultDto> RunAsync(CashFlowReportRequest request, CancellationToken ct)
    {
        DateOnly from = request.FromDate!.Value;
        DateOnly to = request.ToDate!.Value;
        DateOnly? compareFrom = request.CompareFromDate;
        DateOnly? compareTo = request.CompareToDate;

        List<AccountTypeDto> types = await _master.GetAccountTypesFullAsync(ct);

        List<CashFlowAccount> accounts = await _db.Accounts
            .Select(a => new CashFlowAccount
            {
                AccountId = a.AccountId,
                AccountTypeId = a.AccountTypeId,
                AccountCode = a.AccountCode,
                AccountName = a.AccountName,
                AccountSystemName = a.AccountSystemName,
                IsBank = a.IsBank,
                IsSales = a.IsSales,
            })
            .ToListAsync(ct);

        // The accounts the fixed-asset categories post cost and depreciation to.
        HashSet<long> fixedAssetAccounts =
        [
            .. (await _db.FixedAssetCategories
                .Select(c => new { c.AssetAccountId, c.AccumulatedDepreciationAccountId })
                .ToListAsync(ct))
                .SelectMany(c => new[] { c.AssetAccountId, c.AccumulatedDepreciationAccountId }),
        ];

        long[] cash = [.. accounts.Where(a => a.IsBank).Select(a => a.AccountId)];

        DateOnly earliest = compareFrom is DateOnly cf && cf < from ? cf : from;
        DateOnly latest = compareTo is DateOnly ctd && ctd > to ? ctd : to;

        List<PostingAccountNet> nets = await PostingNets(_db.Ledger, cash, earliest, latest).ToListAsync(ct);
        CashBalances balances = await Balances(_db.Ledger, cash, from, to, compareFrom, compareTo)
            .FirstOrDefaultAsync(ct) ?? new CashBalances();

        return Compose(accounts, types, fixedAssetAccounts, nets, balances, from, to, compareFrom, compareTo);
    }

    /// <summary>
    /// Every account's credits less debits in each posting between two dates
    /// that moves a cash account, the cash accounts included. Cost-of-sales
    /// and goods-delivered legs, and opening balances, are left out. Built from
    /// a plain queryable so a test runs it on a list. LINQ only.
    /// </summary>
    public static IQueryable<PostingAccountNet> PostingNets(
        IQueryable<JournalLedgerRead> ledger, long[] cashAccountIds, DateOnly from, DateOnly to)
    {
        IQueryable<JournalLedgerRead> legs = ledger.Where(l =>
            l.LedgerDate >= from && l.LedgerDate <= to
            && l.LedgerSourceId != OpeningBalanceSource
            && l.LedgerTypeId != CogsLedgerType
            && l.LedgerTypeId != GdniLedgerType);

        var cashPostings = legs
            .Where(l => cashAccountIds.Contains(l.AccountId))
            .Select(l => new { l.TransactionTypeCode, l.TransactionId, l.LedgerDate })
            .Distinct();

        return from l in legs
               join p in cashPostings
                   on new { l.TransactionTypeCode, l.TransactionId, l.LedgerDate }
                   equals new { p.TransactionTypeCode, p.TransactionId, p.LedgerDate }
               group l by new { l.TransactionTypeCode, l.TransactionId, l.LedgerDate, l.AccountId } into g
               select new PostingAccountNet
               {
                   TransactionTypeCode = g.Key.TransactionTypeCode,
                   TransactionId = g.Key.TransactionId,
                   LedgerDate = g.Key.LedgerDate,
                   AccountId = g.Key.AccountId,
                   Net = g.Sum(x => x.CreditAmountBase - x.DebitAmountBase),
               };
    }

    /// <summary>
    /// The cash accounts' debits less credits at each end of the period and of
    /// the comparison period, in one pass. Opening balances entered within a
    /// period count in its opening cash, since they are not flows. LINQ only.
    /// </summary>
    public static IQueryable<CashBalances> Balances(
        IQueryable<JournalLedgerRead> ledger,
        long[] cashAccountIds,
        DateOnly from,
        DateOnly to,
        DateOnly? compareFrom,
        DateOnly? compareTo)
    {
        DateOnly cFrom = compareFrom ?? from;
        DateOnly cTo = compareTo ?? to;
        DateOnly latest = cTo > to ? cTo : to;

        return ledger
            .Where(l => cashAccountIds.Contains(l.AccountId) && l.LedgerDate <= latest)
            .GroupBy(l => l.OrgId)
            .Select(g => new CashBalances
            {
                Opening = g.Sum(l => l.LedgerDate < from || (l.LedgerSourceId == OpeningBalanceSource && l.LedgerDate <= to)
                    ? l.DebitAmountBase - l.CreditAmountBase : 0m),
                Closing = g.Sum(l => l.LedgerDate <= to ? l.DebitAmountBase - l.CreditAmountBase : 0m),
                CompareOpening = g.Sum(l => l.LedgerDate < cFrom || (l.LedgerSourceId == OpeningBalanceSource && l.LedgerDate <= cTo)
                    ? l.DebitAmountBase - l.CreditAmountBase : 0m),
                CompareClosing = g.Sum(l => l.LedgerDate <= cTo ? l.DebitAmountBase - l.CreditAmountBase : 0m),
            });
    }

    /// <summary>
    /// The statement from the accounts, the postings' nets and the cash
    /// balances. Pure.
    ///
    /// Every line is shown, so the statement always reads the same way; under
    /// each, the accounts that moved cash in either period, by code. An inflow
    /// is positive.
    /// </summary>
    public static CashFlowReportResultDto Compose(
        IReadOnlyList<CashFlowAccount> accounts,
        IReadOnlyList<AccountTypeDto> types,
        IReadOnlySet<long> fixedAssetAccounts,
        IReadOnlyList<PostingAccountNet> nets,
        CashBalances balances,
        DateOnly from,
        DateOnly to,
        DateOnly? compareFrom,
        DateOnly? compareTo)
    {
        bool comparing = compareFrom.HasValue && compareTo.HasValue;
        Dictionary<long, CashFlowAccount> accountById = accounts.ToDictionary(a => a.AccountId);
        Dictionary<int, AccountTypeDto> typeById = types.ToDictionary(t => t.AccountTypeId);

        // (line, account) → (period, comparison)
        Dictionary<(CashFlowLine Line, long AccountId), (decimal Amount, decimal Compare)> sums = [];

        foreach (var posting in nets.GroupBy(n => (n.TransactionTypeCode, n.TransactionId, n.LedgerDate)))
        {
            DateOnly date = posting.Key.LedgerDate;
            bool inPeriod = date >= from && date <= to;
            bool inCompare = comparing && date >= compareFrom!.Value && date <= compareTo!.Value;

            if (!inPeriod && !inCompare)
            {
                continue;
            }

            List<(long AccountId, decimal Net, CashFlowLine Line)> legs = Trace(
                posting.Where(n => !(accountById.TryGetValue(n.AccountId, out CashFlowAccount? a) && a.IsBank))
                    .Select(n => (n.AccountId, n.Net)),
                id => accountById.GetValueOrDefault(id),
                typeById,
                fixedAssetAccounts);

            foreach ((long accountId, decimal net, CashFlowLine line) in legs)
            {
                (decimal amount, decimal compare) = sums.GetValueOrDefault((line, accountId));
                sums[(line, accountId)] = (amount + (inPeriod ? net : 0m), compare + (inCompare ? net : 0m));
            }
        }

        CashFlowLineDto LineOf(CashFlowLine line)
        {
            List<CashFlowAccountDto> lineAccounts =
            [
                .. sums
                    .Where(s => s.Key.Line == line && (s.Value.Amount != 0m || (comparing && s.Value.Compare != 0m)))
                    .Select(s =>
                    {
                        CashFlowAccount? account = accountById.GetValueOrDefault(s.Key.AccountId);
                        return new CashFlowAccountDto
                        {
                            AccountId = s.Key.AccountId,
                            AccountCode = account?.AccountCode ?? string.Empty,
                            AccountName = account?.AccountName ?? $"Account {s.Key.AccountId}",
                            Amount = s.Value.Amount,
                            CompareAmount = comparing ? s.Value.Compare : null,
                        };
                    })
                    .OrderBy(a => a.AccountCode, StringComparer.Ordinal)
                    .ThenBy(a => a.AccountId),
            ];

            return new CashFlowLineDto
            {
                Line = line,
                Title = TitleOf(line),
                Amount = lineAccounts.Sum(a => a.Amount),
                CompareAmount = comparing ? lineAccounts.Sum(a => a.CompareAmount ?? 0m) : null,
                Accounts = lineAccounts,
            };
        }

        List<CashFlowSectionDto> sections =
        [
            .. Layout.Select(part =>
            {
                List<CashFlowLineDto> lines = [.. part.Lines.Select(LineOf)];
                return new CashFlowSectionDto
                {
                    Activity = part.Activity,
                    Title = part.Title,
                    Lines = lines,
                    Total = lines.Sum(l => l.Amount),
                    CompareTotal = comparing ? lines.Sum(l => l.CompareAmount ?? 0m) : null,
                };
            }),
        ];

        CashFlowLineDto exchange = LineOf(CashFlowLine.ExchangeRateEffect);

        decimal netChange = sections.Sum(s => s.Total);
        decimal? compareNetChange = comparing ? sections.Sum(s => s.CompareTotal ?? 0m) : null;

        var totals = new CashFlowTotalsDto
        {
            NetChange = netChange,
            OpeningCash = balances.Opening,
            ClosingCash = balances.Closing,
            Difference = balances.Closing - balances.Opening - netChange - exchange.Amount,
        };

        if (comparing)
        {
            totals.CompareNetChange = compareNetChange;
            totals.CompareOpeningCash = balances.CompareOpening;
            totals.CompareClosingCash = balances.CompareClosing;
            totals.CompareDifference = balances.CompareClosing - balances.CompareOpening
                - compareNetChange - (exchange.CompareAmount ?? 0m);
        }

        return new CashFlowReportResultDto
        {
            FromDate = from,
            ToDate = to,
            CompareFromDate = comparing ? compareFrom : null,
            CompareToDate = comparing ? compareTo : null,
            Sections = sections,
            ExchangeRateEffect = exchange,
            Totals = totals,
            IsReconciled = totals.Difference == 0m && (totals.CompareDifference ?? 0m) == 0m,
        };
    }

    /// <summary>
    /// The line each of one posting's other accounts falls on, with the cash it
    /// accounts for (its credits less debits). GST, TDS, round-off and realized
    /// exchange legs take the line the rest of the posting falls on when that is
    /// a single line; fixed-asset legs are purchases when the posting paid cash
    /// out and sales when it brought cash in. Pure.
    /// </summary>
    public static List<(long AccountId, decimal Net, CashFlowLine Line)> Trace(
        IEnumerable<(long AccountId, decimal Net)> otherLegs,
        Func<long, CashFlowAccount?> accountOf,
        IReadOnlyDictionary<int, AccountTypeDto> typeById,
        IReadOnlySet<long> fixedAssetAccounts)
    {
        List<(long AccountId, decimal Net, CashFlowLine Line, bool Follows)> legs =
        [
            .. otherLegs
                .Where(l => l.Net != 0m)
                .Select(l =>
                {
                    CashFlowAccount? account = accountOf(l.AccountId);
                    AccountTypeDto? type = account is null ? null : typeById.GetValueOrDefault(account.AccountTypeId);
                    (CashFlowLine line, bool follows) = Classify(account, type, fixedAssetAccounts);
                    return (l.AccountId, l.Net, line, follows);
                }),
        ];

        List<CashFlowLine> main = [.. legs.Where(l => !l.Follows).Select(l => l.Line).Distinct()];
        CashFlowLine? only = main.Count == 1 ? main[0] : null;

        List<(long AccountId, decimal Net, CashFlowLine Line)> traced =
        [
            .. legs.Select(l => (l.AccountId, l.Net, l.Follows && only is CashFlowLine one ? one : l.Line)),
        ];

        // Fixed assets: one line or the other for the whole posting, by its sign.
        decimal investing = traced.Where(l => l.Line == CashFlowLine.FixedAssetPurchases).Sum(l => l.Net);
        if (investing > 0m)
        {
            traced = [.. traced.Select(l => l.Line == CashFlowLine.FixedAssetPurchases
                ? (l.AccountId, l.Net, CashFlowLine.FixedAssetSales)
                : l)];
        }

        return traced;
    }

    /// <summary>
    /// The line an account falls on, and whether it instead goes with the rest
    /// of its posting when that falls on one line (the second value), in which
    /// case the line is where it goes standing alone. Fixed-asset accounts come
    /// back as <see cref="CashFlowLine.FixedAssetPurchases"/>; <see cref="Trace"/>
    /// settles purchases against sales. Pure.
    /// </summary>
    public static (CashFlowLine Line, bool Follows) Classify(
        CashFlowAccount? account, AccountTypeDto? type, IReadOnlySet<long> fixedAssetAccounts)
    {
        if (account is null)
        {
            return (CashFlowLine.OtherOperating, false);
        }

        string? name = account.AccountSystemName;

        if (name is not null && TaxAccounts.Contains(name))
        {
            return (CashFlowLine.GstAndTds, true);
        }

        if (name is RoundOff or RealizedFxGainLoss)
        {
            return (CashFlowLine.OtherOperating, true);
        }

        if (name is UnrealizedFxGainLoss)
        {
            return (CashFlowLine.ExchangeRateEffect, false);
        }

        if (name is FixedAsset or AssetDisposalGainLoss || fixedAssetAccounts.Contains(account.AccountId))
        {
            return (CashFlowLine.FixedAssetPurchases, false);
        }

        if (name is AccountsReceivable || (type?.SystemName == "Income" && account.IsSales))
        {
            return (CashFlowLine.ReceiptsFromCustomers, false);
        }

        if (name is AccountsPayable or SalaryPayable or Inventory or GoodsReceivedNotInvoiced or GoodsDeliveredNotInvoiced
            || type?.SystemName == "Expense")
        {
            return (CashFlowLine.PaymentsToSuppliersAndEmployees, false);
        }

        if (type?.SystemName == "Equity")
        {
            return (CashFlowLine.CapitalAndDrawings, false);
        }

        return (CashFlowLine.OtherOperating, false);
    }

    /// <summary>The words a line is shown under. Pure.</summary>
    public static string TitleOf(CashFlowLine line) => line switch
    {
        CashFlowLine.ReceiptsFromCustomers => "Receipts from customers",
        CashFlowLine.PaymentsToSuppliersAndEmployees => "Payments to suppliers and employees",
        CashFlowLine.GstAndTds => "GST and TDS paid or refunded",
        CashFlowLine.OtherOperating => "Other operating activities",
        CashFlowLine.FixedAssetPurchases => "Purchase of fixed assets",
        CashFlowLine.FixedAssetSales => "Sale of fixed assets",
        CashFlowLine.CapitalAndDrawings => "Capital introduced and drawings",
        _ => "Effect of exchange rate changes",
    };
}

/// <summary>An account as the cash flow statement needs it.</summary>
public sealed class CashFlowAccount
{
    public long AccountId { get; set; }
    public int AccountTypeId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string? AccountSystemName { get; set; }
    public bool IsBank { get; set; }
    public bool IsSales { get; set; }
}

/// <summary>One account's credits less debits within one posting that moves cash.</summary>
public sealed class PostingAccountNet
{
    public string TransactionTypeCode { get; set; } = string.Empty;
    public long TransactionId { get; set; }
    public DateOnly LedgerDate { get; set; }
    public long AccountId { get; set; }
    public decimal Net { get; set; }
}

/// <summary>The cash accounts' balance at each end of the period and of the comparison period.</summary>
public sealed class CashBalances
{
    public decimal Opening { get; set; }
    public decimal Closing { get; set; }
    public decimal CompareOpening { get; set; }
    public decimal CompareClosing { get; set; }
}

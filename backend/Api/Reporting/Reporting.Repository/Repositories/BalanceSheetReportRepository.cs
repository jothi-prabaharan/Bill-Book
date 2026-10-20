using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Numbering;
using static Reporting.Repository.Repositories.BatchedNameResolverRepository;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Balance Sheet (TK-199): every balance-sheet account's balance at an
/// as-at date, and at a comparison date when asked, grouped into assets,
/// liabilities and equity, with the profit the income and expense accounts
/// hold carried into equity.
///
/// <b>Balances are summed in the database</b>, one row per account, so the
/// report reads a few hundred rows however long the ledger is. The ledger is
/// <c>acc.JournalLedger</c>, which holds posted legs only, scoped to the
/// token's branch by the query filter. Amounts are in base currency.
///
/// <b>Profit is split at the financial year.</b> Income less expenses up to
/// the start of the year the date falls in is earlier years' profit; from that
/// start to the date it is the current year's. A year-end entry that moves
/// profit into a retained earnings account leaves both figures right: the
/// income and expense accounts it empties stop counting, and the account it
/// fills is listed with its own balance.
///
/// Account types (which side each is on, and whether it belongs on the balance
/// sheet) are <c>mst.AccountTypes</c>, read from Master and cached.
/// </summary>
public sealed class BalanceSheetReportRepository
{
    private readonly ReportingDbContext _db;
    private readonly BatchedNameResolverRepository _master;
    private readonly IFinancialYearProvider _financialYear;

    public BalanceSheetReportRepository(
        ReportingDbContext db, BatchedNameResolverRepository master, IFinancialYearProvider financialYear)
    {
        _db = db;
        _master = master;
        _financialYear = financialYear;
    }

    /// <summary>The balance sheet for a request the controller has already validated.</summary>
    public async Task<BalanceSheetReportResultDto> RunAsync(BalanceSheetReportRequest request, CancellationToken ct)
    {
        DateOnly asOf = request.AsOfDate!.Value;
        DateOnly? compare = request.CompareAsOfDate;

        int startMonth = await _financialYear.GetStartMonthAsync(ct);
        DateOnly yearStart = PortalAccountRepository.FinancialYearStart(asOf, startMonth);
        DateOnly? compareYearStart = compare is DateOnly c ? PortalAccountRepository.FinancialYearStart(c, startMonth) : null;

        List<AccountTypeDto> types = await _master.GetAccountTypesFullAsync(ct);

        List<BalanceSheetAccount> accounts = await _db.Accounts
            .Select(a => new BalanceSheetAccount
            {
                AccountId = a.AccountId,
                AccountTypeId = a.AccountTypeId,
                AccountCode = a.AccountCode,
                AccountName = a.AccountName,
                IsContra = a.IsContra,
                IsActive = a.IsActive,
            })
            .ToListAsync(ct);

        List<AccountLedgerBalance> balances =
            await Balances(_db.Ledger, asOf, compare, yearStart, compareYearStart).ToListAsync(ct);

        return Compose(accounts, balances, types, asOf, compare, yearStart, compareYearStart, request.IsZeroBalanceShown);
    }

    /// <summary>
    /// Debits less credits per account: to the as-at date, before its year's
    /// start, and the same two for the comparison date. One pass over the legs
    /// up to the later date. Built from a plain queryable so a test runs it on
    /// a list. LINQ only.
    /// </summary>
    public static IQueryable<AccountLedgerBalance> Balances(
        IQueryable<JournalLedgerRead> ledger,
        DateOnly asOf,
        DateOnly? compare,
        DateOnly yearStart,
        DateOnly? compareYearStart)
    {
        DateOnly compareDate = compare ?? asOf;
        DateOnly compareStart = compareYearStart ?? yearStart;
        DateOnly latest = compareDate > asOf ? compareDate : asOf;

        return ledger
            .Where(l => l.LedgerDate <= latest)
            .GroupBy(l => l.AccountId)
            .Select(g => new AccountLedgerBalance
            {
                AccountId = g.Key,
                Net = g.Sum(l => l.LedgerDate <= asOf ? l.DebitAmountBase - l.CreditAmountBase : 0m),
                NetBeforeYear = g.Sum(l => l.LedgerDate < yearStart ? l.DebitAmountBase - l.CreditAmountBase : 0m),
                CompareNet = g.Sum(l => l.LedgerDate <= compareDate ? l.DebitAmountBase - l.CreditAmountBase : 0m),
                CompareNetBeforeYear = g.Sum(l => l.LedgerDate < compareStart ? l.DebitAmountBase - l.CreditAmountBase : 0m),
            });
    }

    /// <summary>
    /// The report from the accounts, their balances and the account types. Pure.
    ///
    /// A balance is shown positive on its section's own side: debits less
    /// credits for an asset, credits less debits for a liability or equity. A
    /// contra account therefore reads negative within its section, as
    /// accumulated depreciation does under assets. An account with no balance
    /// on any date shown is left out unless asked for, and an inactive one is
    /// shown only while it still holds a balance, since leaving it out would
    /// unbalance the sheet.
    /// </summary>
    public static BalanceSheetReportResultDto Compose(
        IReadOnlyList<BalanceSheetAccount> accounts,
        IReadOnlyList<AccountLedgerBalance> balances,
        IReadOnlyList<AccountTypeDto> types,
        DateOnly asOf,
        DateOnly? compare,
        DateOnly yearStart,
        DateOnly? compareYearStart,
        bool isZeroBalanceShown)
    {
        bool comparing = compare.HasValue;
        Dictionary<int, AccountTypeDto> typeById = types.ToDictionary(t => t.AccountTypeId);
        Dictionary<long, AccountLedgerBalance> balanceById = balances.ToDictionary(b => b.AccountId);

        Dictionary<BalanceSheetSection, List<BalanceSheetLineDto>> lines = new()
        {
            [BalanceSheetSection.Assets] = [],
            [BalanceSheetSection.Liabilities] = [],
            [BalanceSheetSection.Equity] = [],
        };

        // Credits less debits on the income and expense accounts: profit.
        decimal profit = 0m, profitBeforeYear = 0m, compareProfit = 0m, compareProfitBeforeYear = 0m;

        foreach (BalanceSheetAccount account in accounts)
        {
            if (!typeById.TryGetValue(account.AccountTypeId, out AccountTypeDto? type))
            {
                continue;
            }

            AccountLedgerBalance balance = balanceById.GetValueOrDefault(account.AccountId) ?? new AccountLedgerBalance();

            if (type.ReportSection == "ProfitAndLoss")
            {
                profit -= balance.Net;
                profitBeforeYear -= balance.NetBeforeYear;
                compareProfit -= balance.CompareNet;
                compareProfitBeforeYear -= balance.CompareNetBeforeYear;
                continue;
            }

            if (SectionOf(type) is not BalanceSheetSection section)
            {
                continue;
            }

            decimal side = type.NormalBalance == "Debit" ? 1m : -1m;
            decimal shown = side * balance.Net;
            decimal? compareShown = comparing ? side * balance.CompareNet : null;
            bool hasBalance = shown != 0m || (compareShown ?? 0m) != 0m;

            if (!hasBalance && !(isZeroBalanceShown && account.IsActive))
            {
                continue;
            }

            lines[section].Add(new BalanceSheetLineDto
            {
                AccountId = account.AccountId,
                AccountCode = account.AccountCode,
                AccountName = account.AccountName,
                IsContra = account.IsContra,
                Balance = shown,
                CompareBalance = compareShown,
            });
        }

        foreach (List<BalanceSheetLineDto> sectionLines in lines.Values)
        {
            sectionLines.Sort((a, b) => string.CompareOrdinal(a.AccountCode, b.AccountCode));
        }

        AddEarnings(lines[BalanceSheetSection.Equity], "Profit of earlier years",
            profitBeforeYear, comparing ? compareProfitBeforeYear : null, isZeroBalanceShown);
        AddEarnings(lines[BalanceSheetSection.Equity], "Current year earnings",
            profit - profitBeforeYear, comparing ? compareProfit - compareProfitBeforeYear : null, isZeroBalanceShown);

        List<BalanceSheetSectionDto> sections =
        [
            .. lines.Select(pair => new BalanceSheetSectionDto
            {
                Section = pair.Key,
                Title = TitleOf(pair.Key),
                Lines = pair.Value,
                Total = pair.Value.Sum(l => l.Balance),
                CompareTotal = comparing ? pair.Value.Sum(l => l.CompareBalance ?? 0m) : null,
            }),
        ];

        BalanceSheetTotalsDto totals = Totals(sections, comparing);

        return new BalanceSheetReportResultDto
        {
            AsOfDate = asOf,
            CompareAsOfDate = compare,
            FinancialYearStart = yearStart,
            CompareFinancialYearStart = compareYearStart,
            Sections = sections,
            Totals = totals,
            IsBalanced = totals.Difference == 0m && (totals.CompareDifference ?? 0m) == 0m,
        };
    }

    /// <summary>Which section an account type belongs in, or null for one off the balance sheet. Pure.</summary>
    public static BalanceSheetSection? SectionOf(AccountTypeDto type) =>
        type.ReportSection != "BalanceSheet"
            ? null
            : type.SystemName switch
            {
                "Asset" => BalanceSheetSection.Assets,
                "Liability" => BalanceSheetSection.Liabilities,
                "Equity" => BalanceSheetSection.Equity,
                _ => null,
            };

    private static string TitleOf(BalanceSheetSection section) => section switch
    {
        BalanceSheetSection.Assets => "Assets",
        BalanceSheetSection.Liabilities => "Liabilities",
        _ => "Equity",
    };

    /// <summary>A worked-out profit line, shown when it is not zero on a date shown, or when zero balances are asked for.</summary>
    private static void AddEarnings(
        List<BalanceSheetLineDto> equity, string name, decimal amount, decimal? compareAmount, bool isZeroBalanceShown)
    {
        if (amount == 0m && (compareAmount ?? 0m) == 0m && !isZeroBalanceShown)
        {
            return;
        }

        equity.Add(new BalanceSheetLineDto
        {
            AccountName = name,
            IsComputed = true,
            Balance = amount,
            CompareBalance = compareAmount,
        });
    }

    private static BalanceSheetTotalsDto Totals(IReadOnlyList<BalanceSheetSectionDto> sections, bool comparing)
    {
        BalanceSheetSectionDto Of(BalanceSheetSection s) => sections.Single(x => x.Section == s);

        BalanceSheetSectionDto assets = Of(BalanceSheetSection.Assets);
        BalanceSheetSectionDto liabilities = Of(BalanceSheetSection.Liabilities);
        BalanceSheetSectionDto equity = Of(BalanceSheetSection.Equity);

        decimal both = liabilities.Total + equity.Total;
        decimal? compareBoth = comparing ? liabilities.CompareTotal + equity.CompareTotal : null;

        return new BalanceSheetTotalsDto
        {
            TotalAssets = assets.Total,
            TotalLiabilities = liabilities.Total,
            TotalEquity = equity.Total,
            TotalLiabilitiesAndEquity = both,
            Difference = assets.Total - both,
            CompareTotalAssets = assets.CompareTotal,
            CompareTotalLiabilities = liabilities.CompareTotal,
            CompareTotalEquity = equity.CompareTotal,
            CompareTotalLiabilitiesAndEquity = compareBoth,
            CompareDifference = comparing ? assets.CompareTotal - compareBoth : null,
        };
    }
}

/// <summary>An account as the balance sheet needs it.</summary>
public sealed class BalanceSheetAccount
{
    public long AccountId { get; set; }
    public int AccountTypeId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public bool IsContra { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>One account's debits less credits at the dates the report needs.</summary>
public sealed class AccountLedgerBalance
{
    public long AccountId { get; set; }

    /// <summary>To the end of the as-at date.</summary>
    public decimal Net { get; set; }

    /// <summary>Before the first day of the as-at date's financial year.</summary>
    public decimal NetBeforeYear { get; set; }

    public decimal CompareNet { get; set; }

    public decimal CompareNetBeforeYear { get; set; }
}

using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;
using static Reporting.Repository.Repositories.BatchedNameResolverRepository;

namespace Reporting.Repository.Repositories;

/// <summary>
/// The Profit &amp; Loss (TK-200): what each income and expense account moved
/// by over a period, and over a comparison period when asked, grouped into
/// revenue, cost of sales, other income and expenses, down to gross and net
/// profit.
///
/// <b>Classified as Business Performance classifies them</b>, so the two
/// reports agree: revenue is the income accounts marked for sales
/// (<c>IsSales</c>), so sales returns and discounts given net off and FX gains
/// do not count as trading; cost of sales is the accounts the seed names Cost
/// of Goods Sold and Purchase Returns; every other income account is other
/// income and every other expense account an expense.
///
/// <b>Movements are summed in the database</b>, one row per account. The
/// ledger is <c>acc.JournalLedger</c>, posted legs only, scoped to the token's
/// branch by the query filter; amounts are in base currency. Net profit for a
/// period is therefore exactly the change the Balance Sheet's current year
/// earnings shows over the same days within one financial year.
/// </summary>
public sealed class ProfitAndLossReportRepository
{
    private readonly ReportingDbContext _db;
    private readonly BatchedNameResolverRepository _master;

    public ProfitAndLossReportRepository(ReportingDbContext db, BatchedNameResolverRepository master)
    {
        _db = db;
        _master = master;
    }

    /// <summary>
    /// Every reason the request's dates cannot be run, by field, empty when
    /// there is none. A comparison period needs both dates or neither. Pure.
    /// </summary>
    public static IReadOnlyList<(string Field, string Message)> DateRefusals(ProfitAndLossReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from)
        {
            refusals.Add((nameof(ProfitAndLossReportRequest.ToDate), "To date cannot be before the from date."));
        }

        if (request.CompareFromDate is null && request.CompareToDate is not null)
        {
            refusals.Add((nameof(ProfitAndLossReportRequest.CompareFromDate), "Give both comparison dates, or neither."));
        }
        else if (request.CompareFromDate is not null && request.CompareToDate is null)
        {
            refusals.Add((nameof(ProfitAndLossReportRequest.CompareToDate), "Give both comparison dates, or neither."));
        }
        else if (request.CompareFromDate is DateOnly compareFrom && request.CompareToDate is DateOnly compareTo
            && compareTo < compareFrom)
        {
            refusals.Add((nameof(ProfitAndLossReportRequest.CompareToDate), "Comparison to date cannot be before its from date."));
        }

        return refusals;
    }

    /// <summary>The profit and loss for a request the controller has already validated.</summary>
    public async Task<ProfitAndLossReportResultDto> RunAsync(ProfitAndLossReportRequest request, CancellationToken ct)
    {
        DateOnly from = request.FromDate!.Value;
        DateOnly to = request.ToDate!.Value;

        List<AccountTypeDto> types = await _master.GetAccountTypesFullAsync(ct);

        List<ProfitAndLossAccount> accounts = await _db.Accounts
            .Select(a => new ProfitAndLossAccount
            {
                AccountId = a.AccountId,
                AccountTypeId = a.AccountTypeId,
                AccountCode = a.AccountCode,
                AccountName = a.AccountName,
                AccountSystemName = a.AccountSystemName,
                IsContra = a.IsContra,
                IsSales = a.IsSales,
                IsActive = a.IsActive,
            })
            .ToListAsync(ct);

        List<AccountPeriodMovement> movements = await Movements(
            _db.Ledger, from, to, request.CompareFromDate, request.CompareToDate).ToListAsync(ct);

        return Compose(
            accounts, movements, types, from, to, request.CompareFromDate, request.CompareToDate, request.IsZeroBalanceShown);
    }

    /// <summary>
    /// Debits less credits per account in the period and in the comparison
    /// period, in one pass over the legs spanning both. Built from a plain
    /// queryable so a test runs it on a list. LINQ only.
    /// </summary>
    public static IQueryable<AccountPeriodMovement> Movements(
        IQueryable<JournalLedgerRead> ledger,
        DateOnly from,
        DateOnly to,
        DateOnly? compareFrom,
        DateOnly? compareTo)
    {
        DateOnly cFrom = compareFrom ?? from;
        DateOnly cTo = compareTo ?? to;
        DateOnly earliest = cFrom < from ? cFrom : from;
        DateOnly latest = cTo > to ? cTo : to;

        return ledger
            .Where(l => l.LedgerDate >= earliest && l.LedgerDate <= latest)
            .GroupBy(l => l.AccountId)
            .Select(g => new AccountPeriodMovement
            {
                AccountId = g.Key,
                Net = g.Sum(l => l.LedgerDate >= from && l.LedgerDate <= to
                    ? l.DebitAmountBase - l.CreditAmountBase : 0m),
                CompareNet = g.Sum(l => l.LedgerDate >= cFrom && l.LedgerDate <= cTo
                    ? l.DebitAmountBase - l.CreditAmountBase : 0m),
            });
    }

    /// <summary>
    /// The report from the accounts, their movements and the account types. Pure.
    ///
    /// An amount is shown positive on its section's own side: credits less
    /// debits for income, debits less credits for an expense. A contra account
    /// therefore reads negative and reduces its section, as sales returns do
    /// revenue. An account with no movement in either period is left out unless
    /// asked for, and then only while it is active.
    /// </summary>
    public static ProfitAndLossReportResultDto Compose(
        IReadOnlyList<ProfitAndLossAccount> accounts,
        IReadOnlyList<AccountPeriodMovement> movements,
        IReadOnlyList<AccountTypeDto> types,
        DateOnly from,
        DateOnly to,
        DateOnly? compareFrom,
        DateOnly? compareTo,
        bool isZeroBalanceShown)
    {
        bool comparing = compareFrom.HasValue && compareTo.HasValue;
        Dictionary<int, AccountTypeDto> typeById = types.ToDictionary(t => t.AccountTypeId);
        Dictionary<long, AccountPeriodMovement> movementById = movements.ToDictionary(m => m.AccountId);

        Dictionary<ProfitAndLossSection, List<ProfitAndLossLineDto>> lines = new()
        {
            [ProfitAndLossSection.Revenue] = [],
            [ProfitAndLossSection.CostOfSales] = [],
            [ProfitAndLossSection.OtherIncome] = [],
            [ProfitAndLossSection.Expenses] = [],
        };

        foreach (ProfitAndLossAccount account in accounts)
        {
            if (!typeById.TryGetValue(account.AccountTypeId, out AccountTypeDto? type)
                || SectionOf(type, account) is not ProfitAndLossSection section)
            {
                continue;
            }

            AccountPeriodMovement movement = movementById.GetValueOrDefault(account.AccountId) ?? new AccountPeriodMovement();
            decimal side = type.NormalBalance == "Debit" ? 1m : -1m;
            decimal amount = side * movement.Net;
            decimal? compareAmount = comparing ? side * movement.CompareNet : null;
            bool moved = amount != 0m || (compareAmount ?? 0m) != 0m;

            if (!moved && !(isZeroBalanceShown && account.IsActive))
            {
                continue;
            }

            lines[section].Add(new ProfitAndLossLineDto
            {
                AccountId = account.AccountId,
                AccountCode = account.AccountCode,
                AccountName = account.AccountName,
                IsContra = account.IsContra,
                Amount = amount,
                CompareAmount = compareAmount,
            });
        }

        List<ProfitAndLossSectionDto> sections =
        [
            .. lines.Select(pair => new ProfitAndLossSectionDto
            {
                Section = pair.Key,
                Title = TitleOf(pair.Key),
                Lines = [.. pair.Value.OrderBy(l => l.AccountCode, StringComparer.Ordinal)],
                Total = pair.Value.Sum(l => l.Amount),
                CompareTotal = comparing ? pair.Value.Sum(l => l.CompareAmount ?? 0m) : null,
            }),
        ];

        return new ProfitAndLossReportResultDto
        {
            FromDate = from,
            ToDate = to,
            CompareFromDate = comparing ? compareFrom : null,
            CompareToDate = comparing ? compareTo : null,
            Sections = sections,
            Totals = Totals(sections, comparing),
        };
    }

    /// <summary>
    /// Which section an account belongs in, or null for one off the profit and
    /// loss. By its type's report section and side, then as Business
    /// Performance splits them. Pure.
    /// </summary>
    public static ProfitAndLossSection? SectionOf(AccountTypeDto type, ProfitAndLossAccount account)
    {
        if (type.ReportSection != "ProfitAndLoss")
        {
            return null;
        }

        if (type.NormalBalance == "Credit")
        {
            return account.IsSales ? ProfitAndLossSection.Revenue : ProfitAndLossSection.OtherIncome;
        }

        return account.AccountSystemName is BusinessPerformanceSourceRepository.CostOfGoodsSold
            or BusinessPerformanceSourceRepository.PurchaseReturns
            ? ProfitAndLossSection.CostOfSales
            : ProfitAndLossSection.Expenses;
    }

    /// <summary>A share of revenue as a percentage, to two places half away from zero; null without revenue. Pure.</summary>
    public static decimal? MarginPercent(decimal profit, decimal revenue) =>
        revenue == 0m ? null : Math.Round(profit / revenue * 100m, 2, MidpointRounding.AwayFromZero);

    private static string TitleOf(ProfitAndLossSection section) => section switch
    {
        ProfitAndLossSection.Revenue => "Revenue",
        ProfitAndLossSection.CostOfSales => "Cost of sales",
        ProfitAndLossSection.OtherIncome => "Other income",
        _ => "Expenses",
    };

    private static ProfitAndLossTotalsDto Totals(IReadOnlyList<ProfitAndLossSectionDto> sections, bool comparing)
    {
        ProfitAndLossSectionDto Of(ProfitAndLossSection s) => sections.Single(x => x.Section == s);

        ProfitAndLossSectionDto revenue = Of(ProfitAndLossSection.Revenue);
        ProfitAndLossSectionDto cost = Of(ProfitAndLossSection.CostOfSales);
        ProfitAndLossSectionDto other = Of(ProfitAndLossSection.OtherIncome);
        ProfitAndLossSectionDto expenses = Of(ProfitAndLossSection.Expenses);

        decimal gross = revenue.Total - cost.Total;
        decimal net = gross + other.Total - expenses.Total;

        var totals = new ProfitAndLossTotalsDto
        {
            Revenue = revenue.Total,
            CostOfSales = cost.Total,
            GrossProfit = gross,
            OtherIncome = other.Total,
            Expenses = expenses.Total,
            NetProfit = net,
            GrossMarginPercent = MarginPercent(gross, revenue.Total),
            NetMarginPercent = MarginPercent(net, revenue.Total),
        };

        if (comparing)
        {
            decimal compareRevenue = revenue.CompareTotal ?? 0m;
            decimal compareGross = compareRevenue - (cost.CompareTotal ?? 0m);
            decimal compareNet = compareGross + (other.CompareTotal ?? 0m) - (expenses.CompareTotal ?? 0m);

            totals.CompareRevenue = compareRevenue;
            totals.CompareCostOfSales = cost.CompareTotal;
            totals.CompareGrossProfit = compareGross;
            totals.CompareOtherIncome = other.CompareTotal;
            totals.CompareExpenses = expenses.CompareTotal;
            totals.CompareNetProfit = compareNet;
            totals.CompareGrossMarginPercent = MarginPercent(compareGross, compareRevenue);
            totals.CompareNetMarginPercent = MarginPercent(compareNet, compareRevenue);
        }

        return totals;
    }
}

/// <summary>An account as the profit and loss needs it.</summary>
public sealed class ProfitAndLossAccount
{
    public long AccountId { get; set; }
    public int AccountTypeId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string? AccountSystemName { get; set; }
    public bool IsContra { get; set; }
    public bool IsSales { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>One account's debits less credits in the period and the comparison period.</summary>
public sealed class AccountPeriodMovement
{
    public long AccountId { get; set; }
    public decimal Net { get; set; }
    public decimal CompareNet { get; set; }
}

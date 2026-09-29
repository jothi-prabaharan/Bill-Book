using System.Globalization;
using Accounting.Entity.Enums;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Budgets;
using Shared.Kernel.Tenancy;

namespace Accounting.Repository.Repositories;

/// <summary>
/// Whether a document being saved takes an expense account (and project, on a
/// budget by project) past the branch's approved budget, for the document's
/// month or, when the branch says so, the year to date (TK-109, design
/// "Budgeting" → Warnings). It only warns: the save has already succeeded, and
/// blocking a purchase because a budget ran out is an approval decision.
/// </summary>
public sealed class BudgetCheckRepository
{
    private const int ExpenseType = 5;
    private static readonly CultureInfo Indian = CultureInfo.GetCultureInfo("en-IN");

    private readonly AccountingDbContext _db;
    private readonly IBranchSettingsProvider _settings;

    public BudgetCheckRepository(AccountingDbContext db, IBranchSettingsProvider settings)
    {
        _db = db;
        _settings = settings;
    }

    public async Task<List<string>> WarningsAsync(DateOnly date, IEnumerable<BudgetSpendLine> lines, CancellationToken ct)
    {
        List<BudgetSpendLine> spends = [.. lines.Where(l => l.Amount != 0m)];
        if (spends.Count == 0)
        {
            return [];
        }

        Budget? budget = await _db.Budgets.AsNoTracking()
            .Where(b => b.IsBranchDefault && b.Status == BudgetStatus.Approved && b.FinancialYearStart <= date)
            .OrderByDescending(b => b.FinancialYearStart)
            .FirstOrDefaultAsync(ct);

        if (budget is null || budget.FinancialYearStart.AddYears(1) <= date)
        {
            return [];
        }

        bool yearToDate = (await _settings.GetSettingsAsync(ct))?.BudgetWarningYearToDate ?? false;
        int period = BudgetMathRepository.PeriodOf(budget.FinancialYearStart, date);
        int firstPeriod = yearToDate ? 1 : period;
        DateOnly start = budget.FinancialYearStart.AddMonths(firstPeriod - 1);
        DateOnly end = budget.FinancialYearStart.AddMonths(period).AddDays(-1);

        List<long> accountIds = [.. spends.Select(s => s.AccountId).Distinct()];
        Dictionary<long, string> expenses = await _db.Accounts.AsNoTracking()
            .Where(a => accountIds.Contains(a.AccountId) && a.AccountTypeId == ExpenseType && !a.IsContra)
            .ToDictionaryAsync(a => a.AccountId, a => a.AccountName, ct);

        // One figure per account (and project, when the budget is by project).
        var groups = spends
            .Where(s => expenses.ContainsKey(s.AccountId))
            .GroupBy(s => (s.AccountId, ProjectId: budget.ByProject ? s.ProjectId : null))
            .Select(g => (g.Key.AccountId, g.Key.ProjectId, Amount: g.Sum(s => s.Amount)))
            .Where(g => g.Amount > 0m)
            .ToList();

        string when = yearToDate
            ? $"the budget for the year to {start.AddMonths(period - firstPeriod).ToString("MMM yyyy", CultureInfo.InvariantCulture)}"
            : $"the {start.ToString("MMM yyyy", CultureInfo.InvariantCulture)} budget";

        List<string> warnings = [];

        foreach ((long accountId, long? projectId, decimal amount) in groups)
        {
            IQueryable<BudgetLine> planned = _db.BudgetLines.AsNoTracking()
                .Where(l => l.BudgetId == budget.BudgetId && l.AccountId == accountId && (!budget.ByProject || l.ProjectId == projectId));

            // An account the budget does not mention is unbudgeted, not over budget.
            if (!await planned.AnyAsync(ct))
            {
                continue;
            }

            decimal budgeted = await planned.Where(l => l.Period >= firstPeriod && l.Period <= period).SumAsync(l => l.Amount, ct);
            decimal spent = await _db.JournalLedger.AsNoTracking()
                .Where(l => l.AccountId == accountId && l.LedgerDate >= start && l.LedgerDate <= end
                    && (!budget.ByProject || l.ProjectId == projectId))
                .SumAsync(l => l.DebitAmountBase - l.CreditAmountBase, ct);

            decimal over = spent + amount - budgeted;
            if (over > 0m)
            {
                warnings.Add($"{expenses[accountId]} is {over.ToString("N2", Indian)} over {when}.");
            }
        }

        return warnings;
    }
}

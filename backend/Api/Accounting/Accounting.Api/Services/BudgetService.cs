using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;
using Shared.Kernel.Persistence;

namespace Accounting.Api.Services;

/// <summary>
/// Budgets (TK-108, design "Budgeting"): a year of monthly amounts per account
/// for one branch. A draft is entered and changed freely; an approved budget
/// is locked; a revision copies an approved budget into a new draft, and
/// approving it supersedes the one it revised.
/// </summary>
public sealed class BudgetService
{
    private const int IncomeType = 4;
    private const int ExpenseType = 5;

    private readonly AccountingDbContext _db;
    private readonly IFinancialYearProvider _financialYear;
    private readonly TimeProvider _clock;

    public BudgetService(AccountingDbContext db, IFinancialYearProvider financialYear, TimeProvider clock)
    {
        _db = db;
        _financialYear = financialYear;
        _clock = clock;
    }

    public async Task<List<BudgetListItem>> ListAsync(int? year, CancellationToken ct)
    {
        IQueryable<Budget> budgets = _db.Budgets.AsNoTracking();

        if (year is int y)
        {
            DateOnly from = new(y, 1, 1);
            DateOnly to = new(y, 12, 31);
            budgets = budgets.Where(b => b.FinancialYearStart >= from && b.FinancialYearStart <= to);
        }

        return await budgets
            .OrderByDescending(b => b.FinancialYearStart).ThenByDescending(b => b.IsBranchDefault).ThenBy(b => b.Name).ThenBy(b => b.BudgetId)
            .Select(b => new BudgetListItem
            {
                BudgetId = b.BudgetId,
                Name = b.Name,
                FinancialYearStart = b.FinancialYearStart,
                Status = b.Status.ToString(),
                IsBranchDefault = b.IsBranchDefault,
                ByProject = b.ByProject,
                RevisionOf = b.RevisionOf,
                RevisionReason = b.RevisionReason,
                ApprovedAt = b.ApprovedAt,
                Total = _db.BudgetLines.Where(l => l.BudgetId == b.BudgetId).Sum(l => l.Amount),
            })
            .ToListAsync(ct);
    }

    public async Task<BudgetView?> GetAsync(long id, CancellationToken ct)
    {
        Budget? budget = await _db.Budgets.AsNoTracking().FirstOrDefaultAsync(b => b.BudgetId == id, ct);
        if (budget is null)
        {
            return null;
        }

        var cells = await (
            from l in _db.BudgetLines.AsNoTracking()
            where l.BudgetId == id
            join a in _db.Accounts on l.AccountId equals a.AccountId
            join p in _db.Projects on l.ProjectId equals p.ProjectId into gp
            from p in gp.DefaultIfEmpty()
            select new { l.AccountId, a.AccountCode, a.AccountName, a.AccountTypeId, l.ProjectId, ProjectCode = p != null ? p.ProjectCode : null, l.Period, l.Amount })
            .ToListAsync(ct);

        List<BudgetLineView> lines = [.. cells
            .GroupBy(c => new { c.AccountId, c.AccountCode, c.AccountName, c.AccountTypeId, c.ProjectId, c.ProjectCode })
            .Select(g =>
            {
                decimal[] amounts = new decimal[BudgetMath.Months];
                foreach (var c in g)
                {
                    amounts[c.Period - 1] = c.Amount;
                }

                return new BudgetLineView
                {
                    AccountId = g.Key.AccountId,
                    AccountCode = g.Key.AccountCode,
                    AccountName = g.Key.AccountName,
                    AccountTypeId = g.Key.AccountTypeId,
                    ProjectId = g.Key.ProjectId,
                    ProjectCode = g.Key.ProjectCode,
                    Amounts = amounts,
                    Total = amounts.Sum(),
                };
            })
            .OrderBy(l => l.AccountCode, StringComparer.Ordinal)
            .ThenBy(l => l.ProjectCode, StringComparer.Ordinal)];

        return new BudgetView
        {
            BudgetId = budget.BudgetId,
            Name = budget.Name,
            FinancialYearStart = budget.FinancialYearStart,
            Status = budget.Status.ToString(),
            IsBranchDefault = budget.IsBranchDefault,
            ByProject = budget.ByProject,
            RevisionOf = budget.RevisionOf,
            RevisionReason = budget.RevisionReason,
            ApprovedAt = budget.ApprovedAt,
            Lines = lines,
            Total = lines.Sum(l => l.Total),
        };
    }

    /// <summary>
    /// Last financial year's profit-and-loss actuals, month by month, in each
    /// account's normal direction: what "spread by last year's shape" and
    /// "start from last year" read.
    /// </summary>
    public async Task<List<BudgetLineView>?> LastYearAsync(long id, CancellationToken ct)
    {
        Budget? budget = await _db.Budgets.AsNoTracking().FirstOrDefaultAsync(b => b.BudgetId == id, ct);
        return budget is null ? null : await ActualsAsync(budget.FinancialYearStart.AddYears(-1), budget.ByProject, ct);
    }

    public async Task<BudgetResult> CreateAsync(CreateBudgetRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse(request.Source, ignoreCase: true, out BudgetSource source) || !Enum.IsDefined(source))
        {
            return new BudgetResult(BudgetOutcome.Refused, Detail: "Start from a blank budget, last year's actuals or another budget.");
        }

        int startMonth = await _financialYear.GetStartMonthAsync(ct);
        DateOnly yearStart = new(request.Year, startMonth, 1);
        Dictionary<int, decimal> byType = request.TypeAdjustments
            .GroupBy(a => a.AccountTypeId)
            .ToDictionary(g => g.Key, g => g.Last().Percent);

        List<BudgetLineView> lines = [];

        if (source == BudgetSource.LastYearActuals)
        {
            lines = await ActualsAsync(yearStart.AddYears(-1), request.ByProject, ct);
        }
        else if (source == BudgetSource.OtherBudget)
        {
            if (request.SourceBudgetId is not long sourceId || await GetAsync(sourceId, ct) is not { } other)
            {
                return new BudgetResult(BudgetOutcome.Refused, Detail: "Choose the budget to copy.");
            }

            lines = request.ByProject || !other.ByProject
                ? other.Lines
                : [.. other.Lines
                    .GroupBy(l => new { l.AccountId, l.AccountCode, l.AccountName, l.AccountTypeId })
                    .Select(g => new BudgetLineView
                    {
                        AccountId = g.Key.AccountId,
                        AccountCode = g.Key.AccountCode,
                        AccountName = g.Key.AccountName,
                        AccountTypeId = g.Key.AccountTypeId,
                        Amounts = [.. Enumerable.Range(0, BudgetMath.Months).Select(m => g.Sum(l => l.Amounts[m]))],
                    })];
        }

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        Budget budget = new()
        {
            Name = request.Name.Trim(),
            FinancialYearStart = yearStart,
            ByProject = request.ByProject,
            Status = BudgetStatus.Draft,
        };
        _db.Budgets.Add(budget);
        await _db.SaveChangesAsync(ct);

        foreach (BudgetLineView line in lines)
        {
            decimal percent = source == BudgetSource.Blank ? 0m : BudgetMath.PercentFor(line.AccountTypeId, request.AdjustPercent, byType);
            AddLines(budget.BudgetId, line.AccountId, request.ByProject ? line.ProjectId : null, BudgetMath.Adjust(line.Amounts, percent));
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new BudgetResult(BudgetOutcome.Ok, budget.BudgetId);
    }

    /// <summary>Renames a draft and replaces its lines.</summary>
    public async Task<BudgetResult> SaveAsync(long id, SaveBudgetRequest request, CancellationToken ct)
    {
        Budget? budget = await _db.Budgets.FirstOrDefaultAsync(b => b.BudgetId == id, ct);
        if (budget is null)
        {
            return new BudgetResult(BudgetOutcome.NotFound);
        }

        if (budget.Status != BudgetStatus.Draft)
        {
            return Locked(budget);
        }

        if (await RefusalAsync(budget, request.Lines, ct) is string refusal)
        {
            return new BudgetResult(BudgetOutcome.Refused, id, refusal);
        }

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        budget.Name = request.Name.Trim();
        await ReplaceLinesAsync(budget, request.Lines.Select(l => (l.AccountId, l.ProjectId, (IReadOnlyList<decimal>)l.Amounts)), ct);

        await tx.CommitAsync(ct);
        return new BudgetResult(BudgetOutcome.Ok, id);
    }

    public async Task<BudgetResult> DeleteAsync(long id, CancellationToken ct)
    {
        Budget? budget = await _db.Budgets.FirstOrDefaultAsync(b => b.BudgetId == id, ct);
        if (budget is null)
        {
            return new BudgetResult(BudgetOutcome.NotFound);
        }

        if (budget.Status != BudgetStatus.Draft)
        {
            return new BudgetResult(BudgetOutcome.Locked, id, "Only a draft budget can be deleted. An approved one is kept as a record.");
        }

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);
        await _db.BudgetLines.Where(l => l.BudgetId == id).ExecuteDeleteAsync(ct);
        _db.Budgets.Remove(budget);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new BudgetResult(BudgetOutcome.Ok, id);
    }

    /// <summary>
    /// Approves a draft, which locks it. A revision takes over the budget it
    /// revised: that one becomes superseded, and the branch default moves
    /// across if it held it.
    /// </summary>
    public async Task<BudgetResult> ApproveAsync(long id, bool makeDefault, Guid actor, CancellationToken ct)
    {
        Budget? budget = await _db.Budgets.FirstOrDefaultAsync(b => b.BudgetId == id, ct);
        if (budget is null)
        {
            return new BudgetResult(BudgetOutcome.NotFound);
        }

        if (budget.Status != BudgetStatus.Draft)
        {
            return new BudgetResult(BudgetOutcome.Refused, id, "Only a draft budget can be approved.");
        }

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        bool takeDefault = makeDefault;

        if (budget.RevisionOf is long revised)
        {
            Budget? original = await _db.Budgets.FirstOrDefaultAsync(b => b.BudgetId == revised, ct);
            if (original is null || original.Status != BudgetStatus.Approved)
            {
                return new BudgetResult(BudgetOutcome.Refused, id,
                    "The budget this revises is no longer the approved one, so the revision cannot replace it.");
            }

            takeDefault |= original.IsBranchDefault;
            original.IsBranchDefault = false;
            original.Status = BudgetStatus.Superseded;
            await _db.SaveChangesAsync(ct);
        }

        if (takeDefault)
        {
            await ClearDefaultAsync(budget.FinancialYearStart, ct);
        }

        budget.Status = BudgetStatus.Approved;
        budget.ApprovedAt = _clock.GetUtcNow();
        budget.ApprovedBy = actor;
        budget.IsBranchDefault = takeDefault;
        await _db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        return new BudgetResult(BudgetOutcome.Ok, id);
    }

    /// <summary>Makes an approved budget the branch's budget for its year.</summary>
    public async Task<BudgetResult> MakeDefaultAsync(long id, CancellationToken ct)
    {
        Budget? budget = await _db.Budgets.FirstOrDefaultAsync(b => b.BudgetId == id, ct);
        if (budget is null)
        {
            return new BudgetResult(BudgetOutcome.NotFound);
        }

        if (budget.Status != BudgetStatus.Approved)
        {
            return new BudgetResult(BudgetOutcome.Refused, id, "Only an approved budget can be the branch's budget for the year.");
        }

        if (budget.IsBranchDefault)
        {
            return new BudgetResult(BudgetOutcome.Ok, id);
        }

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);
        await ClearDefaultAsync(budget.FinancialYearStart, ct);
        budget.IsBranchDefault = true;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new BudgetResult(BudgetOutcome.Ok, id);
    }

    /// <summary>Copies an approved budget into a new draft that will supersede it once approved.</summary>
    public async Task<BudgetResult> ReviseAsync(long id, string reason, CancellationToken ct)
    {
        Budget? budget = await _db.Budgets.AsNoTracking().FirstOrDefaultAsync(b => b.BudgetId == id, ct);
        if (budget is null)
        {
            return new BudgetResult(BudgetOutcome.NotFound);
        }

        if (budget.Status != BudgetStatus.Approved)
        {
            return new BudgetResult(BudgetOutcome.Refused, id, "Only the approved budget can be revised.");
        }

        if (await _db.Budgets.AnyAsync(b => b.RevisionOf == id && b.Status == BudgetStatus.Draft, ct))
        {
            return new BudgetResult(BudgetOutcome.Refused, id, "A revision of this budget is already in draft. Finish or delete that one first.");
        }

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        Budget revision = new()
        {
            Name = budget.Name,
            FinancialYearStart = budget.FinancialYearStart,
            ByProject = budget.ByProject,
            Status = BudgetStatus.Draft,
            RevisionOf = id,
            RevisionReason = reason.Trim(),
        };
        _db.Budgets.Add(revision);
        await _db.SaveChangesAsync(ct);

        List<BudgetLine> lines = await _db.BudgetLines.AsNoTracking().Where(l => l.BudgetId == id).ToListAsync(ct);
        _db.BudgetLines.AddRange(lines.Select(l => new BudgetLine
        {
            BudgetId = revision.BudgetId,
            AccountId = l.AccountId,
            ProjectId = l.ProjectId,
            Period = l.Period,
            Amount = l.Amount,
        }));
        await _db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        return new BudgetResult(BudgetOutcome.Ok, revision.BudgetId);
    }

    /// <summary>The budget as a sheet: one row per account (and project), twelve months and a total.</summary>
    public async Task<StatementExport?> ExportAsync(long id, CancellationToken ct)
    {
        BudgetView? budget = await GetAsync(id, ct);
        if (budget is null)
        {
            return null;
        }

        List<string[]> rows = [.. budget.Lines.Select(l => (string[])[
            l.AccountCode,
            .. (budget.ByProject ? new[] { l.ProjectCode ?? string.Empty } : []),
            l.AccountName,
            .. l.Amounts.Select(a => a.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)),
            l.Total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)])];

        return new StatementExport(budget.Name, BudgetMath.SheetHeader(budget.FinancialYearStart, budget.ByProject), rows);
    }

    /// <summary>
    /// Replaces a draft's lines with a sheet in the export layout. Every code is
    /// resolved before anything is written: an unknown account or project code
    /// refuses the whole file and names each one.
    /// </summary>
    public async Task<BudgetResult> ImportAsync(long id, IReadOnlyList<string[]> rows, CancellationToken ct)
    {
        Budget? budget = await _db.Budgets.FirstOrDefaultAsync(b => b.BudgetId == id, ct);
        if (budget is null)
        {
            return new BudgetResult(BudgetOutcome.NotFound);
        }

        if (budget.Status != BudgetStatus.Draft)
        {
            return Locked(budget);
        }

        SheetParse sheet = BudgetMath.ParseSheet(rows, budget.ByProject);
        if (sheet.Error is string error)
        {
            return new BudgetResult(BudgetOutcome.Refused, id, error);
        }

        List<string> accountCodes = [.. sheet.Rows.Select(r => r.AccountCode).Distinct(StringComparer.OrdinalIgnoreCase)];
        List<string> projectCodes = [.. sheet.Rows.Where(r => r.ProjectCode != null).Select(r => r.ProjectCode!).Distinct(StringComparer.OrdinalIgnoreCase)];

        Dictionary<string, long> accounts = (await _db.Accounts.AsNoTracking()
                .Where(a => accountCodes.Contains(a.AccountCode))
                .Select(a => new { a.AccountCode, a.AccountId })
                .ToListAsync(ct))
            .ToDictionary(a => a.AccountCode, a => a.AccountId, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, long> projects = (await _db.Projects.AsNoTracking()
                .Where(p => projectCodes.Contains(p.ProjectCode))
                .Select(p => new { p.ProjectCode, p.ProjectId })
                .ToListAsync(ct))
            .ToDictionary(p => p.ProjectCode, p => p.ProjectId, StringComparer.OrdinalIgnoreCase);

        List<string> unknown =
        [
            .. accountCodes.Where(c => !accounts.ContainsKey(c)).Select(c => $"Account {c}"),
            .. projectCodes.Where(c => !projects.ContainsKey(c)).Select(c => $"Project {c}"),
        ];

        if (unknown.Count > 0)
        {
            return new BudgetResult(BudgetOutcome.UnknownCodes, id,
                "The file names codes this branch does not have, so nothing was imported.", unknown);
        }

        List<SaveBudgetLineRequest> lines = [.. sheet.Rows.Select(r => new SaveBudgetLineRequest
        {
            AccountId = accounts[r.AccountCode],
            ProjectId = r.ProjectCode is string code ? projects[code] : null,
            Amounts = [.. r.Amounts],
        })];

        if (await RefusalAsync(budget, lines, ct) is string refusal)
        {
            return new BudgetResult(BudgetOutcome.Refused, id, refusal);
        }

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);
        await ReplaceLinesAsync(budget, lines.Select(l => (l.AccountId, l.ProjectId, (IReadOnlyList<decimal>)l.Amounts)), ct);
        await tx.CommitAsync(ct);
        return new BudgetResult(BudgetOutcome.Ok, id);
    }

    // ---- helpers ---------------------------------------------------------------

    private static BudgetResult Locked(Budget budget) =>
        new(BudgetOutcome.Locked, budget.BudgetId, budget.Status == BudgetStatus.Approved
            ? "This budget is approved, so it cannot be changed. Revise it to make a new draft."
            : "This budget has been superseded by a revision and cannot be changed.");

    /// <summary>Why a set of lines cannot be saved on this budget, or null when they can.</summary>
    private async Task<string?> RefusalAsync(Budget budget, IReadOnlyList<SaveBudgetLineRequest> lines, CancellationToken ct)
    {
        if (lines.Any(l => l.Amounts is null || l.Amounts.Count != BudgetMath.Months))
        {
            return "Each line needs exactly twelve monthly amounts.";
        }

        if (lines.Any(l => l.Amounts.Any(a => a < 0m || a > 999_999_999_999_999m)))
        {
            return "A budget amount cannot be negative.";
        }

        if (!budget.ByProject && lines.Any(l => l.ProjectId != null))
        {
            return "This budget is not by project, so its lines cannot name one.";
        }

        if (lines.GroupBy(l => (l.AccountId, l.ProjectId)).Any(g => g.Count() > 1))
        {
            return "An account appears twice. Give each account (and project) one line.";
        }

        List<long> accountIds = [.. lines.Select(l => l.AccountId).Distinct()];
        List<long> known = await _db.Accounts.AsNoTracking()
            .Where(a => accountIds.Contains(a.AccountId) && a.IsActive)
            .Select(a => a.AccountId)
            .ToListAsync(ct);

        if (known.Count != accountIds.Count)
        {
            return "A line names an account this branch does not have, or one that is inactive.";
        }

        if (await _db.Accounts.AnyAsync(a => a.ParentAccountId != null && accountIds.Contains(a.ParentAccountId.Value), ct))
        {
            return "A line names a parent account. Budget the accounts beneath it instead.";
        }

        List<long> projectIds = [.. lines.Where(l => l.ProjectId != null).Select(l => l.ProjectId!.Value).Distinct()];
        if (projectIds.Count > 0
            && await _db.Projects.CountAsync(p => projectIds.Contains(p.ProjectId), ct) != projectIds.Count)
        {
            return "A line names a project this branch does not have.";
        }

        return null;
    }

    /// <summary>Deletes a draft's lines and writes the new ones. Months at zero are not stored.</summary>
    private async Task ReplaceLinesAsync(Budget budget, IEnumerable<(long AccountId, long? ProjectId, IReadOnlyList<decimal> Amounts)> lines, CancellationToken ct)
    {
        await _db.BudgetLines.Where(l => l.BudgetId == budget.BudgetId).ExecuteDeleteAsync(ct);

        foreach ((long accountId, long? projectId, IReadOnlyList<decimal> amounts) in lines)
        {
            AddLines(budget.BudgetId, accountId, projectId, amounts);
        }

        await _db.SaveChangesAsync(ct);
    }

    private void AddLines(long budgetId, long accountId, long? projectId, IReadOnlyList<decimal> amounts)
    {
        for (int m = 0; m < BudgetMath.Months; m++)
        {
            if (amounts[m] == 0m)
            {
                continue;
            }

            _db.BudgetLines.Add(new BudgetLine
            {
                BudgetId = budgetId,
                AccountId = accountId,
                ProjectId = projectId,
                Period = (short)(m + 1),
                Amount = Math.Round(amounts[m], 2, MidpointRounding.AwayFromZero),
            });
        }
    }

    /// <summary>Takes the default from whichever budget holds it for the year. Saved at once, so the unique index sees one default at a time.</summary>
    private async Task ClearDefaultAsync(DateOnly yearStart, CancellationToken ct) =>
        await _db.Budgets
            .Where(b => b.FinancialYearStart == yearStart && b.IsBranchDefault)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.IsBranchDefault, false), ct);

    /// <summary>A financial year's income and expense movements, month by month, in each account's normal direction.</summary>
    private async Task<List<BudgetLineView>> ActualsAsync(DateOnly yearStart, bool byProject, CancellationToken ct)
    {
        DateOnly yearEnd = yearStart.AddYears(1);

        var movements = await (
            from l in _db.JournalLedger.AsNoTracking()
            where l.LedgerDate >= yearStart && l.LedgerDate < yearEnd
            join a in _db.Accounts on l.AccountId equals a.AccountId
            where a.AccountTypeId == IncomeType || a.AccountTypeId == ExpenseType
            group l by new
            {
                l.AccountId,
                a.AccountCode,
                a.AccountName,
                a.AccountTypeId,
                a.IsContra,
                ProjectId = byProject ? l.ProjectId : null,
                l.LedgerDate.Year,
                l.LedgerDate.Month,
            } into g
            select new
            {
                g.Key.AccountId,
                g.Key.AccountCode,
                g.Key.AccountName,
                g.Key.AccountTypeId,
                g.Key.IsContra,
                g.Key.ProjectId,
                g.Key.Year,
                g.Key.Month,
                Debit = g.Sum(x => x.DebitAmountBase),
                Credit = g.Sum(x => x.CreditAmountBase),
            })
            .ToListAsync(ct);

        Dictionary<long, string> projectCodes = byProject
            ? await _db.Projects.AsNoTracking().ToDictionaryAsync(p => p.ProjectId, p => p.ProjectCode, ct)
            : [];

        return [.. movements
            .GroupBy(m => new { m.AccountId, m.AccountCode, m.AccountName, m.AccountTypeId, m.IsContra, m.ProjectId })
            .Select(g =>
            {
                decimal[] amounts = new decimal[BudgetMath.Months];
                foreach (var m in g)
                {
                    int period = BudgetMath.PeriodOf(yearStart, new DateOnly(m.Year, m.Month, 1));
                    if (period > 0)
                    {
                        amounts[period - 1] += BudgetMath.NormalAmount(m.AccountTypeId, m.IsContra, m.Debit, m.Credit);
                    }
                }

                return new BudgetLineView
                {
                    AccountId = g.Key.AccountId,
                    AccountCode = g.Key.AccountCode,
                    AccountName = g.Key.AccountName,
                    AccountTypeId = g.Key.AccountTypeId,
                    ProjectId = g.Key.ProjectId,
                    ProjectCode = g.Key.ProjectId is long p ? projectCodes.GetValueOrDefault(p) : null,
                    Amounts = amounts,
                    Total = amounts.Sum(),
                };
            })
            .OrderBy(l => l.AccountCode, StringComparer.Ordinal)
            .ThenBy(l => l.ProjectCode, StringComparer.Ordinal)];
    }
}

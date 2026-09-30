using Shared.Kernel.Audit;
using Accounting.Entity.Enums;
using Accounting.Entity.Models;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Persistence;
using Shared.Kernel.Projects;

namespace Accounting.Repository.Repositories;

/// <summary>
/// Billing a project's time, re-billable expenses and milestones (TK-106,
/// design "Project accounting", decision 6). Items become invoice lines only
/// when the invoice posts, by a guarded claim whose row count is the answer:
/// two invoices drafted at once cannot both bill the same hours, because the
/// second claim touches fewer rows than it asked for and is refused whole. A
/// void releases what its invoice claimed.
/// </summary>
public sealed class ProjectBillingRepository
{
    private readonly AccountingDbContext _db;

    public ProjectBillingRepository(AccountingDbContext db) => _db = db;

    /// <summary>What the project has left to bill, with each hour priced by the project's rate basis.</summary>
    public async Task<ProjectUnbilledView?> UnbilledAsync(long projectId, CancellationToken ct)
    {
        var project = await _db.Projects.AsNoTracking()
            .Where(p => p.ProjectId == projectId)
            .Select(p => new { p.ProjectId, p.ContactId, p.BillingMethod, p.RateBasis, p.HourlyRate })
            .FirstOrDefaultAsync(ct);
        if (project is null)
        {
            return null;
        }

        var time = await (
            from t in _db.TimeEntries.AsNoTracking()
            join k in _db.ProjectTasks on t.ProjectTaskId equals k.ProjectTaskId
            where t.ProjectId == projectId && t.IsBillable && t.BillingStatus == TimeBillingStatus.Unbilled
            orderby t.WorkDate, t.TimeEntryId
            select new { t.TimeEntryId, t.WorkDate, t.UserId, t.ProjectTaskId, k.TaskName, TaskRate = k.HourlyRate, t.Hours, t.Notes })
            .ToListAsync(ct);

        Dictionary<Guid, decimal?> memberRates = await _db.ProjectMembers.AsNoTracking()
            .Where(m => m.ProjectId == projectId)
            .ToDictionaryAsync(m => m.UserId, m => m.HourlyRate, ct);

        var expenses = await (
            from d in _db.SpendMoneyDetails.AsNoTracking()
            join m in _db.SpendMoney on d.SpendMoneyId equals m.SpendMoneyId
            where d.ProjectId == projectId && d.IsBillable && d.BilledInvoiceId == null && m.Status == MoneyDocumentStatus.Posted
            orderby m.TransactionDate, d.SpendMoneyDetailId
            select new { d.SpendMoneyDetailId, m.TransactionDate, m.TransactionNo, d.LineMemo, d.Amount, m.ExchangeRate, d.MarkupPercent })
            .ToListAsync(ct);

        List<UnbilledMilestoneItem> milestones = await _db.ProjectMilestones.AsNoTracking()
            .Where(m => m.ProjectId == projectId && m.InvoiceId == null)
            .OrderBy(m => m.DueDate).ThenBy(m => m.ProjectMilestoneId)
            .Select(m => new UnbilledMilestoneItem { ProjectMilestoneId = m.ProjectMilestoneId, Name = m.Name, Amount = m.Amount, DueDate = m.DueDate })
            .ToListAsync(ct);

        return new ProjectUnbilledView
        {
            ProjectId = project.ProjectId,
            ContactId = project.ContactId,
            BillingMethod = project.BillingMethod.ToString(),
            Time = [.. time.Select(t =>
            {
                decimal rate = RateFor(project.RateBasis, project.HourlyRate, t.TaskRate, memberRates.GetValueOrDefault(t.UserId));
                return new UnbilledTimeItem
                {
                    TimeEntryId = t.TimeEntryId,
                    WorkDate = t.WorkDate,
                    UserId = t.UserId,
                    ProjectTaskId = t.ProjectTaskId,
                    TaskName = t.TaskName,
                    Hours = t.Hours,
                    Notes = t.Notes,
                    Rate = rate,
                    Amount = Math.Round(t.Hours * rate, 2, MidpointRounding.AwayFromZero),
                };
            })],
            Expenses = [.. expenses.Select(e =>
            {
                decimal amount = MoneyPostingRepository.Base(e.Amount, e.ExchangeRate);
                return new UnbilledExpenseItem
                {
                    SpendMoneyDetailId = e.SpendMoneyDetailId,
                    TransactionDate = e.TransactionDate,
                    TransactionNo = e.TransactionNo,
                    Memo = e.LineMemo,
                    Amount = amount,
                    MarkupPercent = e.MarkupPercent,
                    BillableAmount = WithMarkup(amount, e.MarkupPercent),
                };
            })],
            Milestones = milestones,
        };
    }

    /// <summary>The hourly rate for one entry: the project's, the task's or the person's, by the project's basis.</summary>
    public static decimal RateFor(ProjectRateBasis? basis, decimal? projectRate, decimal? taskRate, decimal? memberRate) => basis switch
    {
        ProjectRateBasis.TaskRate => taskRate ?? 0m,
        ProjectRateBasis.UserRate => memberRate ?? 0m,
        _ => projectRate ?? 0m,
    };

    /// <summary>An amount charged on with its markup, to two decimals.</summary>
    public static decimal WithMarkup(decimal amount, decimal? markupPercent) =>
        Math.Round(amount * (1m + ((markupPercent ?? 0m) / 100m)), 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Claims every item for the invoice, or none. An item the same invoice
    /// already holds counts as claimed, so a retried post is safe; an item
    /// another invoice holds, or one that is no longer billable, is not, and
    /// the whole claim is taken back.
    /// </summary>
    public async Task<ClaimProjectItemsResponse> ClaimAsync(long invoiceId, IReadOnlyCollection<ProjectItemRef> items, CancellationToken ct)
    {
        List<long> time = Ids(items, ProjectItemKind.TimeEntry);
        List<long> expenses = Ids(items, ProjectItemKind.Expense);
        List<long> milestones = Ids(items, ProjectItemKind.Milestone);
        int requested = time.Count + expenses.Count + milestones.Count;

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        int touched = 0;
        if (time.Count > 0)
        {
            touched += await _db.TimeEntries
                .Where(t => time.Contains(t.TimeEntryId) && t.IsBillable
                    && (t.BillingStatus == TimeBillingStatus.Unbilled
                        || (t.BillingStatus == TimeBillingStatus.Billed && t.InvoiceId == invoiceId)))
                .ExecuteUpdateLoggedAsync(_db, u => u
                    .SetProperty(t => t.BillingStatus, TimeBillingStatus.Billed)
                    .SetProperty(t => t.InvoiceId, invoiceId), ct);
        }

        if (expenses.Count > 0)
        {
            touched += await _db.SpendMoneyDetails
                .Where(d => expenses.Contains(d.SpendMoneyDetailId) && d.IsBillable
                    && (d.BilledInvoiceId == null || d.BilledInvoiceId == invoiceId))
                .ExecuteUpdateLoggedAsync(_db, u => u.SetProperty(d => d.BilledInvoiceId, invoiceId), ct);
        }

        if (milestones.Count > 0)
        {
            touched += await _db.ProjectMilestones
                .Where(m => milestones.Contains(m.ProjectMilestoneId) && (m.InvoiceId == null || m.InvoiceId == invoiceId))
                .ExecuteUpdateLoggedAsync(_db, u => u.SetProperty(m => m.InvoiceId, invoiceId), ct);
        }

        if (touched != requested)
        {
            await tx.RollbackAsync(ct);
            return new ClaimProjectItemsResponse
            {
                Claimed = false,
                Requested = requested,
                Touched = touched,
                Detail = ProjectBillingMessages.AlreadyBilled,
            };
        }

        await tx.CommitAsync(ct);
        return new ClaimProjectItemsResponse { Claimed = true, Requested = requested, Touched = touched };
    }

    /// <summary>Gives back everything the invoice claimed. The number of items released.</summary>
    public async Task<int> ReleaseAsync(long invoiceId, CancellationToken ct)
    {
        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        int released = await _db.TimeEntries
            .Where(t => t.InvoiceId == invoiceId)
            .ExecuteUpdateLoggedAsync(_db, u => u
                .SetProperty(t => t.BillingStatus, TimeBillingStatus.Unbilled)
                .SetProperty(t => t.InvoiceId, (long?)null), ct);

        released += await _db.SpendMoneyDetails
            .Where(d => d.BilledInvoiceId == invoiceId)
            .ExecuteUpdateLoggedAsync(_db, u => u.SetProperty(d => d.BilledInvoiceId, (long?)null), ct);

        released += await _db.ProjectMilestones
            .Where(m => m.InvoiceId == invoiceId)
            .ExecuteUpdateLoggedAsync(_db, u => u.SetProperty(m => m.InvoiceId, (long?)null), ct);

        await tx.CommitAsync(ct);
        return released;
    }

    private static List<long> Ids(IEnumerable<ProjectItemRef> items, ProjectItemKind kind) =>
        [.. items.Where(i => i.Kind == kind).Select(i => i.ItemId).Distinct()];
}

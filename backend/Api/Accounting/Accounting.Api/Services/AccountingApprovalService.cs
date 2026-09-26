using Accounting.Entity.Enums;
using Accounting.Entity.TableEntities;
using Accounting.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Approvals;
using Shared.Kernel.Entities;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Tenancy;

namespace Accounting.Api.Services;

/// <summary>
/// Approval chains for spend money and manual journals (TK-101): the shared
/// flow of <see cref="DocumentApprovalService{TStep}"/> over <c>acc.ApprovalSteps</c>.
///
/// Neither document has a ReadyToPost state, so approving changes only the
/// summary: an <c>Approved</c> summary is what lets the ordinary post through.
/// A hand-written journal is gated; a module's own system journal is not,
/// because no person wrote it.
/// </summary>
public sealed class AccountingApprovalService : DocumentApprovalService<AccountingApprovalStep>
{
    private readonly AccountingDbContext _db;

    public AccountingApprovalService(
        AccountingDbContext db, IApprovalChainClient chains, ITenantContext tenant, ICurrentUser user, TimeProvider clock)
        : base(db, chains, tenant, user, clock)
    {
        _db = db;
    }

    protected override string ServiceName => "accounting";

    protected override string SegmentOf(ApprovalRequestKind kind) =>
        kind == ApprovalRequestKind.SpendMoney ? "spend-money" : "journals";

    protected override async Task<IApprovalSummary?> LoadAsync(ApprovalRequestKind kind, long id, CancellationToken ct) => kind switch
    {
        ApprovalRequestKind.SpendMoney => await _db.SpendMoney.FirstOrDefaultAsync(d => d.SpendMoneyId == id, ct),
        ApprovalRequestKind.ManualJournal => await _db.Journals.FirstOrDefaultAsync(j => j.JournalId == id, ct),
        _ => null,
    };

    protected override bool IsDraft(IApprovalSummary document) => document switch
    {
        SpendMoney payment => payment.Status == MoneyDocumentStatus.Draft,
        Journal journal => journal.Status == JournalStatus.Draft,
        _ => false,
    };

    protected override async Task<decimal> AmountOfAsync(IApprovalSummary document, CancellationToken ct) => document switch
    {
        SpendMoney payment => MoneyPosting.Base(payment.Amount, payment.ExchangeRate),

        // A journal's size is what it moves: its debits, which equal its credits
        // once it balances. Read from the saved lines, which are what posts.
        Journal journal => await _db.JournalDetails
            .Where(d => d.JournalId == journal.JournalId)
            .SumAsync(d => d.DebitAmountBase, ct),

        _ => 0m,
    };

    protected override Guid? RequesterOf(IApprovalSummary document) => ((AuditableEntity)document).CreatedBy;

    protected override AccountingApprovalStep NewStep(int round) => new() { Round = round };

    protected override void AddSteps(IEnumerable<AccountingApprovalStep> steps) => _db.ApprovalSteps.AddRange(steps);

    protected override Task<int?> LatestRoundAsync(ApprovalRequestKind kind, long id, CancellationToken ct) =>
        _db.ApprovalSteps.Where(s => s.RequestKind == kind && s.RequestId == id).Select(s => (int?)s.Round).MaxAsync(ct);

    protected override Task<List<AccountingApprovalStep>> RoundStepsAsync(ApprovalRequestKind kind, long id, int round, CancellationToken ct) =>
        _db.ApprovalSteps
            .Where(s => s.RequestKind == kind && s.RequestId == id && s.Round == round)
            .OrderBy(s => s.Sequence)
            .ToListAsync(ct);

    protected override Task<List<AccountingApprovalStep>> PendingForAsync(Guid userId, int? roleId, CancellationToken ct) =>
        _db.ApprovalSteps
            .AsNoTracking()
            .Where(s => s.StepStatus == ApprovalStepStatus.Pending
                && (s.ApproverUserId == userId || (roleId != null && s.RoleId == roleId)))
            .ToListAsync(ct);

    /// <summary>A draft has no number yet — one is taken at post — so the inbox names it by id.</summary>
    protected override async Task<List<ApprovalDocumentRow>> DescribeAsync(
        ApprovalRequestKind kind, IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        if (kind == ApprovalRequestKind.SpendMoney)
        {
            var payments = await _db.SpendMoney.AsNoTracking()
                .Where(d => ids.Contains(d.SpendMoneyId))
                .Select(d => new { d.SpendMoneyId, d.TransactionNo, d.TransactionDate, d.Amount, d.ExchangeRate })
                .ToListAsync(ct);

            return [.. payments.Select(d => new ApprovalDocumentRow(
                d.SpendMoneyId, d.TransactionNo ?? $"Draft payment {d.SpendMoneyId}", d.TransactionDate,
                MoneyPosting.Base(d.Amount, d.ExchangeRate)))];
        }

        var journals = await _db.Journals.AsNoTracking()
            .Where(j => ids.Contains(j.JournalId))
            .Select(j => new
            {
                j.JournalId,
                j.JournalNo,
                j.JournalDate,
                Amount = _db.JournalDetails.Where(d => d.JournalId == j.JournalId).Sum(d => d.DebitAmountBase),
            })
            .ToListAsync(ct);

        return [.. journals.Select(j => new ApprovalDocumentRow(
            j.JournalId, j.JournalNo ?? $"Draft journal {j.JournalId}", j.JournalDate, j.Amount))];
    }
}

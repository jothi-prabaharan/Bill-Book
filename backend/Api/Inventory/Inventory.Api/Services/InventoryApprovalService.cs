using Inventory.Entity.Enums;
using Inventory.Entity.TableEntities;
using Inventory.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Approvals;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Tenancy;

namespace Inventory.Api.Services;

/// <summary>
/// Approval chains for stock adjustments (TK-102): the shared flow of
/// <see cref="DocumentApprovalService{TStep}"/> over <c>inv.ApprovalSteps</c>.
/// A sheet has no ReadyToPost state, so approval changes only the summary: an
/// <c>Approved</c> summary is what lets the ordinary post through.
/// </summary>
public sealed class InventoryApprovalService : DocumentApprovalService<InventoryApprovalStep>
{
    private readonly InventoryDbContext _db;

    public InventoryApprovalService(
        InventoryDbContext db, IApprovalChainClient chains, ITenantContext tenant, ICurrentUser user, TimeProvider clock)
        : base(db, chains, tenant, user, clock)
    {
        _db = db;
    }

    protected override string ServiceName => "inventory";

    protected override string SegmentOf(ApprovalRequestKind kind) => "stock-adjustments";

    protected override async Task<IApprovalSummary?> LoadAsync(ApprovalRequestKind kind, long id, CancellationToken ct) =>
        kind == ApprovalRequestKind.StockAdjustment
            ? await _db.StockAdjustments.FirstOrDefaultAsync(a => a.StockAdjustmentId == id, ct)
            : null;

    protected override bool IsDraft(IApprovalSummary document) =>
        ((StockAdjustment)document).Status == StockAdjustmentStatus.Draft;

    /// <summary>
    /// What the sheet moves, at the costs it states. A write-off carries no
    /// cost until it posts, so a sheet of write-offs weighs nothing here and
    /// only a workflow level with no minimum amount catches it.
    /// </summary>
    protected override async Task<decimal> AmountOfAsync(IApprovalSummary document, CancellationToken ct)
    {
        long id = ((StockAdjustment)document).StockAdjustmentId;
        return await _db.StockAdjustmentLines
            .Where(l => l.StockAdjustmentId == id && l.UnitCost != null)
            .SumAsync(l => l.Quantity * l.UnitCost!.Value, ct);
    }

    protected override Guid? RequesterOf(IApprovalSummary document) => ((StockAdjustment)document).CreatedBy;

    protected override InventoryApprovalStep NewStep(int round) => new() { Round = round };

    protected override void AddSteps(IEnumerable<InventoryApprovalStep> steps) => _db.ApprovalSteps.AddRange(steps);

    protected override Task<int?> LatestRoundAsync(ApprovalRequestKind kind, long id, CancellationToken ct) =>
        _db.ApprovalSteps.Where(s => s.RequestKind == kind && s.RequestId == id).Select(s => (int?)s.Round).MaxAsync(ct);

    protected override Task<List<InventoryApprovalStep>> RoundStepsAsync(ApprovalRequestKind kind, long id, int round, CancellationToken ct) =>
        _db.ApprovalSteps
            .Where(s => s.RequestKind == kind && s.RequestId == id && s.Round == round)
            .OrderBy(s => s.Sequence)
            .ToListAsync(ct);

    protected override Task<List<InventoryApprovalStep>> PendingForAsync(Guid userId, int? roleId, CancellationToken ct) =>
        _db.ApprovalSteps
            .AsNoTracking()
            .Where(s => s.StepStatus == ApprovalStepStatus.Pending
                && (s.ApproverUserId == userId || (roleId != null && s.RoleId == roleId)))
            .ToListAsync(ct);

    /// <summary>A draft has no number yet — one is taken at post — so the inbox names it by id.</summary>
    protected override async Task<List<ApprovalDocumentRow>> DescribeAsync(
        ApprovalRequestKind kind, IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        var sheets = await _db.StockAdjustments.AsNoTracking()
            .Where(a => ids.Contains(a.StockAdjustmentId))
            .Select(a => new
            {
                a.StockAdjustmentId,
                a.AdjustmentNo,
                a.AdjustmentDate,
                Amount = _db.StockAdjustmentLines
                    .Where(l => l.StockAdjustmentId == a.StockAdjustmentId && l.UnitCost != null)
                    .Sum(l => l.Quantity * l.UnitCost!.Value),
            })
            .ToListAsync(ct);

        return [.. sheets.Select(a => new ApprovalDocumentRow(
            a.StockAdjustmentId, a.AdjustmentNo ?? $"Draft adjustment {a.StockAdjustmentId}", a.AdjustmentDate, a.Amount))];
    }
}

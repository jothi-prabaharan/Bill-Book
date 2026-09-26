using Microsoft.EntityFrameworkCore;
using Purchase.Entity.TableEntities;
using Purchase.Repository;
using Shared.Kernel.Approvals;
using Shared.Kernel.Documents;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Tenancy;

namespace Purchase.Api.Services;

/// <summary>
/// Approval chains for purchase orders, bills and debit notes (TK-100): the
/// shared flow of <see cref="DocumentApprovalService{TStep}"/> over
/// <c>pur.ApprovalSteps</c>. Approving makes a draft <c>ReadyToPost</c>, and an
/// edit's return to draft takes <c>ReadyToPost</c> back to Draft.
/// </summary>
public sealed class PurchaseApprovalService : DocumentApprovalService<PurchaseApprovalStep>
{
    private readonly PurchaseDbContext _db;

    public PurchaseApprovalService(
        PurchaseDbContext db, IApprovalChainClient chains, ITenantContext tenant, ICurrentUser user, TimeProvider clock)
        : base(db, chains, tenant, user, clock)
    {
        _db = db;
    }

    /// <summary>The kind a route segment names, or null.</summary>
    public static ApprovalRequestKind? KindOf(string document) => document switch
    {
        "purchase-orders" => ApprovalRequestKind.PurchaseOrder,
        "bills" => ApprovalRequestKind.PurchaseBill,
        "debit-notes" => ApprovalRequestKind.DebitNote,
        _ => null,
    };

    protected override string ServiceName => "purchase";

    protected override string SegmentOf(ApprovalRequestKind kind) => kind switch
    {
        ApprovalRequestKind.PurchaseOrder => "purchase-orders",
        ApprovalRequestKind.PurchaseBill => "bills",
        _ => "debit-notes",
    };

    protected override async Task<IApprovalSummary?> LoadAsync(ApprovalRequestKind kind, long id, CancellationToken ct) => kind switch
    {
        ApprovalRequestKind.PurchaseOrder => await _db.PurchaseOrders.FirstOrDefaultAsync(d => d.PurchaseOrderId == id, ct),
        ApprovalRequestKind.PurchaseBill => await _db.Bills.FirstOrDefaultAsync(d => d.BillId == id, ct),
        ApprovalRequestKind.DebitNote => await _db.DebitNotes.FirstOrDefaultAsync(d => d.DebitNoteId == id, ct),
        _ => null,
    };

    protected override bool IsDraft(IApprovalSummary document) => ((DocumentHeaderBase)document).Status == DocumentStatus.Draft;

    protected override Task<decimal> AmountOfAsync(IApprovalSummary document, CancellationToken ct) =>
        Task.FromResult(((DocumentHeaderBase)document).TotalAmountBase);

    protected override Guid? RequesterOf(IApprovalSummary document) => ((DocumentHeaderBase)document).CreatedBy;

    protected override PurchaseApprovalStep NewStep(int round) => new() { Round = round };

    protected override void AddSteps(IEnumerable<PurchaseApprovalStep> steps) => _db.ApprovalSteps.AddRange(steps);

    protected override Task<int?> LatestRoundAsync(ApprovalRequestKind kind, long id, CancellationToken ct) =>
        _db.ApprovalSteps.Where(s => s.RequestKind == kind && s.RequestId == id).Select(s => (int?)s.Round).MaxAsync(ct);

    protected override Task<List<PurchaseApprovalStep>> RoundStepsAsync(ApprovalRequestKind kind, long id, int round, CancellationToken ct) =>
        _db.ApprovalSteps
            .Where(s => s.RequestKind == kind && s.RequestId == id && s.Round == round)
            .OrderBy(s => s.Sequence)
            .ToListAsync(ct);

    protected override Task<List<PurchaseApprovalStep>> PendingForAsync(Guid userId, int? roleId, CancellationToken ct) =>
        _db.ApprovalSteps
            .AsNoTracking()
            .Where(s => s.StepStatus == ApprovalStepStatus.Pending
                && (s.ApproverUserId == userId || (roleId != null && s.RoleId == roleId)))
            .ToListAsync(ct);

    protected override async Task<List<ApprovalDocumentRow>> DescribeAsync(
        ApprovalRequestKind kind, IReadOnlyCollection<long> ids, CancellationToken ct) => kind switch
    {
        ApprovalRequestKind.PurchaseOrder => await _db.PurchaseOrders.AsNoTracking().Where(d => ids.Contains(d.PurchaseOrderId))
            .Select(d => new ApprovalDocumentRow(d.PurchaseOrderId, d.DocumentNo, d.DocumentDate, d.TotalAmountBase)).ToListAsync(ct),
        ApprovalRequestKind.PurchaseBill => await _db.Bills.AsNoTracking().Where(d => ids.Contains(d.BillId))
            .Select(d => new ApprovalDocumentRow(d.BillId, d.DocumentNo, d.DocumentDate, d.TotalAmountBase)).ToListAsync(ct),
        _ => await _db.DebitNotes.AsNoTracking().Where(d => ids.Contains(d.DebitNoteId))
            .Select(d => new ApprovalDocumentRow(d.DebitNoteId, d.DocumentNo, d.DocumentDate, d.TotalAmountBase)).ToListAsync(ct),
    };

    /// <summary>The last approval readies the document to post; nothing posts it but a person.</summary>
    protected override void OnApproved(IApprovalSummary document)
    {
        var header = (DocumentHeaderBase)document;
        if (header.Status == DocumentStatus.Draft)
        {
            header.Status = DocumentStatus.ReadyToPost;
        }
    }

    protected override void OnReturnedToDraft(IApprovalSummary document)
    {
        var header = (DocumentHeaderBase)document;
        if (header.Status == DocumentStatus.ReadyToPost)
        {
            header.Status = DocumentStatus.Draft;
        }
    }
}

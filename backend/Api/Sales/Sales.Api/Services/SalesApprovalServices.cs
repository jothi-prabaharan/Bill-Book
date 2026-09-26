using Microsoft.EntityFrameworkCore;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Approvals;
using Shared.Kernel.Documents;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Tenancy;

namespace Sales.Api.Services;

/// <summary>
/// Sales' side of the approval engine (TK-102), over <c>sal.ApprovalSteps</c>.
/// One subclass per document type, because an invoice and an order can share
/// an id: each reads and writes only the steps carrying its own
/// <see cref="SalesApprovalStep.DocumentType"/>.
/// </summary>
public abstract class SalesApprovalService : DocumentApprovalService<SalesApprovalStep>
{
    protected SalesApprovalService(
        SalesDbContext db, IApprovalChainClient chains, ITenantContext tenant, ICurrentUser user, TimeProvider clock)
        : base(db, chains, tenant, user, clock)
    {
        Sales = db;
    }

    protected SalesDbContext Sales { get; }

    /// <summary><c>INV</c>, <c>SOR</c> or <c>CRN</c>.</summary>
    protected abstract string DocumentType { get; }

    protected override string ServiceName => "sales";

    protected override Guid? RequesterOf(IApprovalSummary document) => HeaderOf(document).CreatedBy;

    protected override SalesApprovalStep NewStep(int round) => new() { Round = round, DocumentType = DocumentType };

    protected override void AddSteps(IEnumerable<SalesApprovalStep> steps) => Sales.ApprovalSteps.AddRange(steps);

    protected override Task<int?> LatestRoundAsync(ApprovalRequestKind kind, long id, CancellationToken ct) =>
        Steps(kind, id).Select(s => (int?)s.Round).MaxAsync(ct);

    protected override Task<List<SalesApprovalStep>> RoundStepsAsync(ApprovalRequestKind kind, long id, int round, CancellationToken ct) =>
        Steps(kind, id).Where(s => s.Round == round).OrderBy(s => s.Sequence).ToListAsync(ct);

    protected override Task<List<SalesApprovalStep>> PendingForAsync(Guid userId, int? roleId, CancellationToken ct) =>
        Sales.ApprovalSteps
            .AsNoTracking()
            .Where(s => s.DocumentType == DocumentType
                && s.StepStatus == ApprovalStepStatus.Pending
                && (s.ApproverUserId == userId || (roleId != null && s.RoleId == roleId)))
            .ToListAsync(ct);

    /// <summary>The header behind a document or an override on it.</summary>
    protected static DocumentHeaderBase HeaderOf(IApprovalSummary document) =>
        document is SalesOverrideSummary o ? o.Header : (DocumentHeaderBase)document;

    private IQueryable<SalesApprovalStep> Steps(ApprovalRequestKind kind, long id) =>
        Sales.ApprovalSteps.Where(s => s.DocumentType == DocumentType && s.RequestKind == kind && s.RequestId == id);
}

/// <summary>
/// A credit note's approval chain (TK-102). As on a purchase document, the last
/// approval makes the draft <c>ReadyToPost</c>, and an edit takes it back.
/// </summary>
public sealed class CreditNoteApprovalService : SalesApprovalService
{
    public CreditNoteApprovalService(
        SalesDbContext db, IApprovalChainClient chains, ITenantContext tenant, ICurrentUser user, TimeProvider clock)
        : base(db, chains, tenant, user, clock)
    {
    }

    protected override string DocumentType => "CRN";

    protected override string SegmentOf(ApprovalRequestKind kind) => "credit-notes";

    protected override async Task<IApprovalSummary?> LoadAsync(ApprovalRequestKind kind, long id, CancellationToken ct) =>
        kind == ApprovalRequestKind.CreditNote
            ? await Sales.CreditNotes.FirstOrDefaultAsync(c => c.CreditNoteId == id, ct)
            : null;

    protected override bool IsDraft(IApprovalSummary document) => HeaderOf(document).Status == DocumentStatus.Draft;

    protected override Task<decimal> AmountOfAsync(IApprovalSummary document, CancellationToken ct) =>
        Task.FromResult(HeaderOf(document).TotalAmountBase);

    protected override async Task<List<ApprovalDocumentRow>> DescribeAsync(
        ApprovalRequestKind kind, IReadOnlyCollection<long> ids, CancellationToken ct) =>
        await Sales.CreditNotes.AsNoTracking().Where(c => ids.Contains(c.CreditNoteId))
            .Select(c => new ApprovalDocumentRow(c.CreditNoteId, c.DocumentNo, c.DocumentDate, c.TotalAmountBase))
            .ToListAsync(ct);

    protected override void OnApproved(IApprovalSummary document)
    {
        DocumentHeaderBase header = HeaderOf(document);
        if (header.Status == DocumentStatus.Draft)
        {
            header.Status = DocumentStatus.ReadyToPost;
        }
    }

    protected override void OnReturnedToDraft(IApprovalSummary document)
    {
        DocumentHeaderBase header = HeaderOf(document);
        if (header.Status == DocumentStatus.ReadyToPost)
        {
            header.Status = DocumentStatus.Draft;
        }
    }
}

/// <summary>
/// An override on one invoice or sales order (TK-102): past the customer's
/// credit limit, or past the line discount limit (D-29). The chain's status
/// is the document's <see cref="ISalesOverrides"/> column for that kind; what
/// it waits on goes in the document's shared summary columns, so a list can
/// say so.
///
/// An override is asked for by saving with <c>requestApproval</c>, never by a
/// separate submit, because the save is what finds the breach. It approves
/// this document and no other, and an edit ends it.
/// </summary>
public abstract class SalesOverrideService<THeader> : SalesApprovalService
    where THeader : DocumentHeaderBase, ISalesOverrides
{
    protected SalesOverrideService(
        SalesDbContext db, IApprovalChainClient chains, ITenantContext tenant, ICurrentUser user, TimeProvider clock)
        : base(db, chains, tenant, user, clock)
    {
    }

    /// <summary>The route segment for the kind of override, or null.</summary>
    public static ApprovalRequestKind? KindOf(string segment) => segment switch
    {
        "credit-limit" => ApprovalRequestKind.CreditLimitOverride,
        "discount" => ApprovalRequestKind.SalesDiscountOverride,
        _ => null,
    };

    protected abstract Task<THeader?> FindAsync(long id, CancellationToken ct);

    protected abstract long IdOf(THeader header);

    protected override async Task<IApprovalSummary?> LoadAsync(ApprovalRequestKind kind, long id, CancellationToken ct) =>
        IsOverride(kind) && await FindAsync(id, ct) is THeader header ? new SalesOverrideSummary(header, header, kind) : null;

    protected override bool IsDraft(IApprovalSummary document) => HeaderOf(document).Status == DocumentStatus.Draft;

    /// <summary>A credit override is weighed by the whole sale; a discount override by the discount given.</summary>
    protected override Task<decimal> AmountOfAsync(IApprovalSummary document, CancellationToken ct)
    {
        DocumentHeaderBase header = HeaderOf(document);
        return Task.FromResult(document is SalesOverrideSummary { Kind: ApprovalRequestKind.SalesDiscountOverride }
            ? header.DiscountAmount * header.ExchangeRate
            : header.TotalAmountBase);
    }

    /// <summary>The summary a document's override is read and written through.</summary>
    public static IApprovalSummary Summary(THeader header, ApprovalRequestKind kind) => new SalesOverrideSummary(header, header, kind);

    /// <summary>
    /// An edit ends every override on the document (an approval approves what
    /// was seen): open steps are cancelled and stay as history, and both
    /// statuses clear so the save's own check decides afresh. The caller's
    /// save writes it.
    /// </summary>
    public async Task ResetAsync(THeader header, CancellationToken ct)
    {
        foreach (ApprovalRequestKind kind in (ApprovalRequestKind[])[ApprovalRequestKind.CreditLimitOverride, ApprovalRequestKind.SalesDiscountOverride])
        {
            IApprovalSummary summary = Summary(header, kind);
            if (summary.ApprovalStatus is not null)
            {
                await ReturnToDraftAsync(kind, IdOf(header), summary, ct);
                summary.ApprovalStatus = null;
            }
        }
    }

    /// <summary>
    /// Why a document may not go further (post, confirm) while an override it
    /// asked for is open or refused, or null. A document that never needed
    /// one passes: its save found nothing to override.
    /// </summary>
    public static string? Blocks(ISalesOverrides document) =>
        Blocked(document.CreditOverrideStatus, "the customer's credit limit")
        ?? Blocked(document.DiscountOverrideStatus, "the discount limit");

    private static string? Blocked(ApprovalStatus? status, string what) => status switch
    {
        null or ApprovalStatus.Approved => null,
        ApprovalStatus.Rejected => $"The override past {what} was rejected. Edit the document, or save it again asking for approval.",
        ApprovalStatus.Draft => $"The override past {what} was sent back. Edit the document, or save it again asking for approval.",
        _ => $"This document is past {what} and waits on an approved override.",
    };

    private static bool IsOverride(ApprovalRequestKind kind) =>
        kind is ApprovalRequestKind.CreditLimitOverride or ApprovalRequestKind.SalesDiscountOverride;
}

public sealed class InvoiceOverrideService : SalesOverrideService<Invoice>
{
    public InvoiceOverrideService(
        SalesDbContext db, IApprovalChainClient chains, ITenantContext tenant, ICurrentUser user, TimeProvider clock)
        : base(db, chains, tenant, user, clock)
    {
    }

    protected override string DocumentType => "INV";

    protected override string SegmentOf(ApprovalRequestKind kind) => "invoices";

    protected override async Task<List<ApprovalDocumentRow>> DescribeAsync(
        ApprovalRequestKind kind, IReadOnlyCollection<long> ids, CancellationToken ct) =>
        await Sales.Invoices.AsNoTracking().Where(i => ids.Contains(i.InvoiceId))
            .Select(i => new ApprovalDocumentRow(i.InvoiceId, i.DocumentNo, i.DocumentDate, i.TotalAmountBase))
            .ToListAsync(ct);

    protected override long IdOf(Invoice header) => header.InvoiceId;

    protected override Task<Invoice?> FindAsync(long id, CancellationToken ct) =>
        Sales.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == id, ct);
}

public sealed class SalesOrderOverrideService : SalesOverrideService<SalesOrder>
{
    public SalesOrderOverrideService(
        SalesDbContext db, IApprovalChainClient chains, ITenantContext tenant, ICurrentUser user, TimeProvider clock)
        : base(db, chains, tenant, user, clock)
    {
    }

    protected override string DocumentType => "SOR";

    protected override string SegmentOf(ApprovalRequestKind kind) => "sales-orders";

    protected override async Task<List<ApprovalDocumentRow>> DescribeAsync(
        ApprovalRequestKind kind, IReadOnlyCollection<long> ids, CancellationToken ct) =>
        await Sales.SalesOrders.AsNoTracking().Where(o => ids.Contains(o.SalesOrderId))
            .Select(o => new ApprovalDocumentRow(o.SalesOrderId, o.DocumentNo, o.DocumentDate, o.TotalAmountBase))
            .ToListAsync(ct);

    protected override long IdOf(SalesOrder header) => header.SalesOrderId;

    protected override Task<SalesOrder?> FindAsync(long id, CancellationToken ct) =>
        Sales.SalesOrders.FirstOrDefaultAsync(o => o.SalesOrderId == id, ct);
}

/// <summary>
/// One override on one document, as the engine sees it: the status is the
/// override's own column, and what it waits on is the document's summary.
/// </summary>
public sealed class SalesOverrideSummary : IApprovalSummary
{
    private readonly ISalesOverrides _overrides;

    public SalesOverrideSummary(DocumentHeaderBase header, ISalesOverrides overrides, ApprovalRequestKind kind)
    {
        Header = header;
        _overrides = overrides;
        Kind = kind;
    }

    public DocumentHeaderBase Header { get; }

    public ApprovalRequestKind Kind { get; }

    public ApprovalStatus? ApprovalStatus
    {
        get => Kind == ApprovalRequestKind.CreditLimitOverride ? _overrides.CreditOverrideStatus : _overrides.DiscountOverrideStatus;
        set
        {
            if (Kind == ApprovalRequestKind.CreditLimitOverride)
            {
                _overrides.CreditOverrideStatus = value;
            }
            else
            {
                _overrides.DiscountOverrideStatus = value;
            }
        }
    }

    public string? CurrentStepLabel { get => Header.CurrentStepLabel; set => Header.CurrentStepLabel = value; }

    public Guid? CurrentApproverUserId { get => Header.CurrentApproverUserId; set => Header.CurrentApproverUserId = value; }

    public int? CurrentApproverRoleId { get => Header.CurrentApproverRoleId; set => Header.CurrentApproverRoleId = value; }
}

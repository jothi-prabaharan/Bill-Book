using Microsoft.EntityFrameworkCore;
using Purchase.Entity.TableEntities;
using Purchase.Repository;
using Shared.Kernel.Approvals;
using Shared.Kernel.Documents;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Tenancy;

namespace Purchase.Api.Services;

public enum PurchaseApprovalOutcome
{
    Ok = 0,
    NotFound = 1,

    /// <summary>No workflow applies: the ordinary approve action does.</summary>
    NoWorkflow = 2,

    /// <summary>The move is not open: the document is not in a state for it, or a comment is missing.</summary>
    Refused = 3,

    /// <summary>The user is not the step's approver, a holder of its role, or their delegate.</summary>
    NotTheApprover = 4,

    /// <summary>Master could not be asked. Nothing changed.</summary>
    Unavailable = 5,
}

public sealed record PurchaseApprovalResult(PurchaseApprovalOutcome Outcome, string? ApprovalStatus = null, string? Detail = null);

/// <summary>One level of a document's chain, as the document screen shows it.</summary>
public sealed class ApprovalStepView
{
    public int Sequence { get; set; }

    public string Label { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public Guid? ApproverUserId { get; set; }

    public int? RoleId { get; set; }

    public Guid? ActedByUserId { get; set; }

    public DateTimeOffset? ActedAt { get; set; }

    public string? Comments { get; set; }
}

public sealed class ApprovalChainView
{
    /// <summary>Null when no workflow has been involved.</summary>
    public string? ApprovalStatus { get; set; }

    public List<ApprovalStepView> Steps { get; set; } = [];

    /// <summary>Whether the signed-in user may act on the level it waits on.</summary>
    public bool CanAct { get; set; }
}

/// <summary>A document waiting on the signed-in user, for the approvals inbox.</summary>
public sealed class ApprovalInboxItem
{
    public string Service { get; set; } = "purchase";

    /// <summary>The route segment: purchase-orders, bills or debit-notes.</summary>
    public string Document { get; set; } = string.Empty;

    public string RequestKind { get; set; } = string.Empty;

    public long RequestId { get; set; }

    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    public decimal Amount { get; set; }

    public string Label { get; set; } = string.Empty;
}

/// <summary>
/// Approval chains for purchase orders, bills and debit notes (TK-100, design
/// "Workflow approvals for RetailErp documents").
///
/// <b>Submit</b> asks Master for the chain for the document's kind and base
/// total. No workflow: the ordinary approve action applies, as it always has. A
/// chain: its steps are stored here as a new round and the first level waits.
/// <b>Act</b> is open only to the level's approver, a holder of its role, or
/// their delegate — being the approver is the authority, not a permission — and
/// the last approval moves the document to <c>ReadyToPost</c>. <b>Editing</b> a
/// document in approval, or approved, cancels the round's open steps and returns
/// it to Draft: an approval approves what was seen. The ordinary approve action
/// and posting a draft are <b>gated</b>: refused while a chain is open, and
/// refused when a workflow would apply, so a chain cannot be walked around.
/// </summary>
public sealed class PurchaseApprovalService
{
    private readonly PurchaseDbContext _db;
    private readonly IApprovalChainClient _chains;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public PurchaseApprovalService(
        PurchaseDbContext db, IApprovalChainClient chains, ITenantContext tenant, ICurrentUser user, TimeProvider clock)
    {
        _db = db;
        _chains = chains;
        _tenant = tenant;
        _user = user;
        _clock = clock;
    }

    private DateOnly Today => DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

    /// <summary>The kind a route segment names, or null.</summary>
    public static ApprovalRequestKind? KindOf(string document) => document switch
    {
        "purchase-orders" => ApprovalRequestKind.PurchaseOrder,
        "bills" => ApprovalRequestKind.PurchaseBill,
        "debit-notes" => ApprovalRequestKind.DebitNote,
        _ => null,
    };

    public static string SegmentOf(ApprovalRequestKind kind) => kind switch
    {
        ApprovalRequestKind.PurchaseOrder => "purchase-orders",
        ApprovalRequestKind.PurchaseBill => "bills",
        _ => "debit-notes",
    };

    public async Task<PurchaseApprovalResult> SubmitAsync(ApprovalRequestKind kind, long id, CancellationToken ct)
    {
        DocumentHeaderBase? document = await LoadAsync(kind, id, ct);
        if (document is null)
        {
            return new PurchaseApprovalResult(PurchaseApprovalOutcome.NotFound);
        }

        if (document.Status != DocumentStatus.Draft)
        {
            return Refused("Only a draft can be submitted for approval.");
        }

        if (document.ApprovalStatus == ApprovalStatus.InApproval)
        {
            return Refused("This document is already waiting for approval.");
        }

        ResolveChainResponse? resolved = await ResolveAsync(kind, document, ct);
        if (resolved is null)
        {
            return new PurchaseApprovalResult(PurchaseApprovalOutcome.Unavailable,
                Detail: "The approval rules could not be read. Try again in a moment.");
        }

        switch (resolved.Outcome)
        {
            case ResolveChainOutcome.NoWorkflow:
                return new PurchaseApprovalResult(PurchaseApprovalOutcome.NoWorkflow,
                    Detail: "No approval workflow applies to this document. Approve it as usual.");
            case ResolveChainOutcome.Unresolvable:
                return Refused(resolved.Detail ?? "The approval chain could not be worked out.");
        }

        int round = await _db.ApprovalSteps
            .Where(s => s.RequestKind == kind && s.RequestId == id)
            .Select(s => (int?)s.Round)
            .MaxAsync(ct) ?? 0;

        List<PurchaseApprovalStep> steps = DocumentApproval.Steps(
            resolved, id, kind, () => new PurchaseApprovalStep { Round = round + 1 });
        _db.ApprovalSteps.AddRange(steps);

        ApprovalStatus status = ApprovalChain.Start(steps);
        if (status == ApprovalStatus.Approved)
        {
            // Every level skipped — the requester approves everything above
            // them, say — so there is nothing to wait for.
            document.Status = DocumentStatus.ReadyToPost;
        }

        DocumentApproval.Summarize(document, status, steps);
        await _db.SaveChangesAsync(ct);
        return new PurchaseApprovalResult(PurchaseApprovalOutcome.Ok, status.ToString());
    }

    public async Task<PurchaseApprovalResult> ActAsync(
        ApprovalRequestKind kind, long id, ApprovalAction action, string? comments, CancellationToken ct)
    {
        DocumentHeaderBase? document = await LoadAsync(kind, id, ct);
        if (document is null)
        {
            return new PurchaseApprovalResult(PurchaseApprovalOutcome.NotFound);
        }

        List<PurchaseApprovalStep> steps = await RoundAsync(kind, id, ct);
        if (document.ApprovalStatus != ApprovalStatus.InApproval || ApprovalChain.Current(steps) is not { } current)
        {
            return Refused(ApprovalChain.Message(ApprovalRefusal.NotInApproval));
        }

        if (_user.UserId is not Guid actor)
        {
            return new PurchaseApprovalResult(PurchaseApprovalOutcome.NotTheApprover,
                Detail: ApprovalChain.Message(ApprovalRefusal.NotTheApprover));
        }

        bool mayAct = await MayActAsync(current, actor, ct);
        ApprovalOutcome outcome = ApprovalChain.Act(steps, action, actor, mayAct, comments, _clock.GetUtcNow());

        if (outcome.IsRefused)
        {
            return outcome.Refusal == ApprovalRefusal.NotTheApprover
                ? new PurchaseApprovalResult(PurchaseApprovalOutcome.NotTheApprover, Detail: ApprovalChain.Message(outcome.Refusal))
                : Refused(ApprovalChain.Message(outcome.Refusal));
        }

        if (outcome.RequestStatus == ApprovalStatus.Approved && document.Status == DocumentStatus.Draft)
        {
            document.Status = DocumentStatus.ReadyToPost;
        }

        DocumentApproval.Summarize(document, outcome.RequestStatus, steps);
        await _db.SaveChangesAsync(ct);
        return new PurchaseApprovalResult(PurchaseApprovalOutcome.Ok, outcome.RequestStatus.ToString());
    }

    public async Task<ApprovalChainView?> ChainAsync(ApprovalRequestKind kind, long id, CancellationToken ct)
    {
        DocumentHeaderBase? document = await LoadAsync(kind, id, ct);
        if (document is null)
        {
            return null;
        }

        List<PurchaseApprovalStep> steps = await RoundAsync(kind, id, ct);
        PurchaseApprovalStep? current = document.ApprovalStatus == ApprovalStatus.InApproval ? ApprovalChain.Current(steps) : null;

        return new ApprovalChainView
        {
            ApprovalStatus = document.ApprovalStatus?.ToString(),
            CanAct = current is not null && _user.UserId is Guid actor && await MayActAsync(current, actor, ct),
            Steps = [.. steps.Select(s => new ApprovalStepView
            {
                Sequence = s.Sequence,
                Label = s.Label,
                Status = s.StepStatus.ToString(),
                ApproverUserId = s.ApproverUserId,
                RoleId = s.RoleId,
                ActedByUserId = s.ActedByUserId,
                ActedAt = s.ActedAt,
                Comments = s.Comments,
            })],
        };
    }

    /// <summary>
    /// Whether the ordinary approve action, or posting a draft, may go ahead.
    /// Null when it may; otherwise why not. An approved chain passes; an open or
    /// rejected one does not; a document no chain has touched passes only when
    /// no workflow applies to it — and Master unreachable is a refusal, because
    /// "could not ask" must not read as "no rules".
    /// </summary>
    public async Task<string?> GateAsync(ApprovalRequestKind kind, DocumentHeaderBase document, CancellationToken ct)
    {
        if (DocumentApproval.Blocks(document) is string blocked)
        {
            return blocked;
        }

        if (document.ApprovalStatus == ApprovalStatus.Approved)
        {
            return null;
        }

        ResolveChainResponse? resolved = await ResolveAsync(kind, document, ct);
        return resolved switch
        {
            null => "The approval rules could not be read. Try again in a moment.",
            { Outcome: ResolveChainOutcome.NoWorkflow } => null,
            _ => "This document needs approval. Submit it for approval first.",
        };
    }

    /// <summary>
    /// An edit to a document in approval, or approved, sends it back (design,
    /// decision 6): the round's open steps are cancelled and stay as history,
    /// and the document is a plain draft again. True when that happened, so the
    /// caller can say so. The caller's save writes it.
    /// </summary>
    public async Task<bool> ReturnToDraftAsync(ApprovalRequestKind kind, long id, DocumentHeaderBase document, CancellationToken ct)
    {
        if (document.ApprovalStatus is null or ApprovalStatus.Draft)
        {
            return false;
        }

        List<PurchaseApprovalStep> steps = await RoundAsync(kind, id, ct);
        ApprovalChain.CancelOpen(steps);

        if (document.Status == DocumentStatus.ReadyToPost)
        {
            document.Status = DocumentStatus.Draft;
        }

        DocumentApproval.Summarize<PurchaseApprovalStep>(document, null, []);
        return true;
    }

    /// <summary>What waits on the signed-in user: levels naming them, or naming their role.</summary>
    public async Task<List<ApprovalInboxItem>> MineAsync(CancellationToken ct)
    {
        if (_user.UserId is not Guid me)
        {
            return [];
        }

        int? role = _user.RoleId;

        List<PurchaseApprovalStep> waiting = await _db.ApprovalSteps
            .AsNoTracking()
            .Where(s => s.StepStatus == ApprovalStepStatus.Pending
                && (s.ApproverUserId == me || (role != null && s.RoleId == role)))
            .ToListAsync(ct);

        List<ApprovalInboxItem> items = [];
        foreach (IGrouping<ApprovalRequestKind, PurchaseApprovalStep> byKind in waiting.GroupBy(s => s.RequestKind))
        {
            List<long> ids = [.. byKind.Select(s => s.RequestId).Distinct()];
            var documents = byKind.Key switch
            {
                ApprovalRequestKind.PurchaseOrder => await _db.PurchaseOrders.AsNoTracking().Where(d => ids.Contains(d.PurchaseOrderId))
                    .Select(d => new { Id = d.PurchaseOrderId, d.DocumentNo, d.DocumentDate, d.TotalAmountBase }).ToListAsync(ct),
                ApprovalRequestKind.PurchaseBill => await _db.Bills.AsNoTracking().Where(d => ids.Contains(d.BillId))
                    .Select(d => new { Id = d.BillId, d.DocumentNo, d.DocumentDate, d.TotalAmountBase }).ToListAsync(ct),
                _ => await _db.DebitNotes.AsNoTracking().Where(d => ids.Contains(d.DebitNoteId))
                    .Select(d => new { Id = d.DebitNoteId, d.DocumentNo, d.DocumentDate, d.TotalAmountBase }).ToListAsync(ct),
            };

            items.AddRange(documents.Select(d => new ApprovalInboxItem
            {
                Document = SegmentOf(byKind.Key),
                RequestKind = byKind.Key.ToString(),
                RequestId = d.Id,
                DocumentNo = d.DocumentNo,
                DocumentDate = d.DocumentDate,
                Amount = d.TotalAmountBase,
                Label = byKind.First(s => s.RequestId == d.Id).Label,
            }));
        }

        return [.. items.OrderBy(i => i.DocumentDate)];
    }

    private async Task<bool> MayActAsync(PurchaseApprovalStep current, Guid actor, CancellationToken ct)
    {
        HashSet<int> roles = _user.RoleId is int role ? [role] : [];

        bool isDelegate = current.ApproverUserId is Guid approver
            && approver != actor
            && await _chains.IsDelegateAsync(new DelegateCheckRequest
            {
                CustomerId = _tenant.CustomerId ?? Guid.Empty,
                OrgId = _tenant.OrgId ?? Guid.Empty,
                ApproverUserId = approver,
                ActorUserId = actor,
                OnDate = Today,
            }, ct);

        return ApprovalChain.MayAct(current, actor, roles, isDelegate);
    }

    private Task<ResolveChainResponse?> ResolveAsync(ApprovalRequestKind kind, DocumentHeaderBase document, CancellationToken ct) =>
        _chains.ResolveAsync(new ResolveChainRequest
        {
            CustomerId = _tenant.CustomerId ?? Guid.Empty,
            OrgId = _tenant.OrgId ?? Guid.Empty,
            RequestKind = kind,
            RequesterUserId = document.CreatedBy ?? _user.UserId,
            Amount = document.TotalAmountBase,
            OnDate = Today,
        }, ct);

    /// <summary>The latest round's steps: an earlier round's approvals never count in this one.</summary>
    private async Task<List<PurchaseApprovalStep>> RoundAsync(ApprovalRequestKind kind, long id, CancellationToken ct)
    {
        int? round = await _db.ApprovalSteps
            .Where(s => s.RequestKind == kind && s.RequestId == id)
            .Select(s => (int?)s.Round)
            .MaxAsync(ct);

        return round is null
            ? []
            : await _db.ApprovalSteps
                .Where(s => s.RequestKind == kind && s.RequestId == id && s.Round == round)
                .OrderBy(s => s.Sequence)
                .ToListAsync(ct);
    }

    private async Task<DocumentHeaderBase?> LoadAsync(ApprovalRequestKind kind, long id, CancellationToken ct) => kind switch
    {
        ApprovalRequestKind.PurchaseOrder => await _db.PurchaseOrders.FirstOrDefaultAsync(d => d.PurchaseOrderId == id, ct),
        ApprovalRequestKind.PurchaseBill => await _db.Bills.FirstOrDefaultAsync(d => d.BillId == id, ct),
        ApprovalRequestKind.DebitNote => await _db.DebitNotes.FirstOrDefaultAsync(d => d.DebitNoteId == id, ct),
        _ => null,
    };

    private static PurchaseApprovalResult Refused(string detail) => new(PurchaseApprovalOutcome.Refused, Detail: detail);
}

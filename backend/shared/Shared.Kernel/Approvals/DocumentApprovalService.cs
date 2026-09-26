using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.Approvals;

/// <summary>
/// The approval summary a document carries (TK-100): where it stands in a
/// chain and who it waits on. On every sales and purchase header, and on
/// Accounting's spend money and manual journal (TK-101).
/// </summary>
public interface IApprovalSummary
{
    ApprovalStatus? ApprovalStatus { get; set; }

    string? CurrentStepLabel { get; set; }

    Guid? CurrentApproverUserId { get; set; }

    int? CurrentApproverRoleId { get; set; }
}

public enum ApprovalResultOutcome
{
    Ok = 0,
    NotFound = 1,

    /// <summary>No workflow applies: the ordinary approve or post does.</summary>
    NoWorkflow = 2,

    /// <summary>The move is not open: the document is not in a state for it, or a comment is missing.</summary>
    Refused = 3,

    /// <summary>The user is not the level's approver, a holder of its role, or their delegate.</summary>
    NotTheApprover = 4,

    /// <summary>Master could not be asked. Nothing changed.</summary>
    Unavailable = 5,
}

public sealed record ApprovalResult(ApprovalResultOutcome Outcome, string? ApprovalStatus = null, string? Detail = null);

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

/// <summary>A document waiting on the signed-in user, for the approvals inbox (TK-103).</summary>
public sealed class ApprovalInboxItem
{
    /// <summary>The service that holds it: purchase, accounting, sales, inventory.</summary>
    public string Service { get; set; } = string.Empty;

    /// <summary>The route segment the document's own API sits under, e.g. purchase-orders.</summary>
    public string Document { get; set; } = string.Empty;

    public string RequestKind { get; set; } = string.Empty;

    public long RequestId { get; set; }

    public string DocumentNo { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    public decimal Amount { get; set; }

    public string Label { get; set; } = string.Empty;
}

/// <summary>One document an inbox row names, as the owning service describes it.</summary>
public sealed record ApprovalDocumentRow(long Id, string DocumentNo, DateOnly DocumentDate, decimal Amount);

/// <summary>
/// The document side of the approval engine (TK-100, TK-101, design "Workflow
/// approvals for RetailErp documents"), written once for every service that
/// stores steps. Each service supplies how to find its documents and steps;
/// the flow is here.
///
/// <b>Submit</b> asks Master for the chain for the document's kind and base
/// amount. No workflow: the ordinary approve or post applies, as it always has.
/// A chain: its steps are stored as a new <i>round</i> and the first level waits.
/// <b>Act</b> is open only to the level's approver, a holder of its role, or
/// their delegate — being the approver is the authority, not a permission — and
/// the last approval approves the document, and nobody approves two levels of
/// one round. <b>Editing</b> a document in
/// approval, or approved, cancels the round's open steps: an approval approves
/// what was seen. <b>The gate</b> refuses the ordinary approve or post while a
/// chain is open or rejected, or when a workflow applies, so a chain cannot be
/// walked around; Master unreachable is a refusal.
/// </summary>
public abstract class DocumentApprovalService<TStep>
    where TStep : ApprovalStepBase
{
    private readonly IApprovalChainClient _chains;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    protected DocumentApprovalService(
        DbContext db, IApprovalChainClient chains, ITenantContext tenant, ICurrentUser user, TimeProvider clock)
    {
        Db = db;
        _chains = chains;
        _tenant = tenant;
        _user = user;
        _clock = clock;
    }

    protected DbContext Db { get; }

    /// <summary>The service name inbox rows carry.</summary>
    protected abstract string ServiceName { get; }

    /// <summary>The route segment for a kind, for inbox rows.</summary>
    protected abstract string SegmentOf(ApprovalRequestKind kind);

    protected abstract Task<IApprovalSummary?> LoadAsync(ApprovalRequestKind kind, long id, CancellationToken ct);

    /// <summary>Whether the document is still a draft, the only state a chain starts from.</summary>
    protected abstract bool IsDraft(IApprovalSummary document);

    /// <summary>
    /// The amount the workflow's levels are compared against, in base currency.
    /// Asynchronous because a journal's is the sum of its saved lines.
    /// </summary>
    protected abstract Task<decimal> AmountOfAsync(IApprovalSummary document, CancellationToken ct);

    /// <summary>Who raised it: the requester, whom the skip rules never ask to approve.</summary>
    protected abstract Guid? RequesterOf(IApprovalSummary document);

    /// <summary>A new step in <paramref name="round"/>.</summary>
    protected abstract TStep NewStep(int round);

    protected abstract void AddSteps(IEnumerable<TStep> steps);

    protected abstract Task<int?> LatestRoundAsync(ApprovalRequestKind kind, long id, CancellationToken ct);

    protected abstract Task<List<TStep>> RoundStepsAsync(ApprovalRequestKind kind, long id, int round, CancellationToken ct);

    /// <summary>Pending steps naming the user, or a role they hold.</summary>
    protected abstract Task<List<TStep>> PendingForAsync(Guid userId, int? roleId, CancellationToken ct);

    /// <summary>The documents a set of inbox steps name, for their number, date and amount.</summary>
    protected abstract Task<List<ApprovalDocumentRow>> DescribeAsync(ApprovalRequestKind kind, IReadOnlyCollection<long> ids, CancellationToken ct);

    /// <summary>What approving does to the document beyond the summary: a sales or purchase document becomes ReadyToPost.</summary>
    protected virtual void OnApproved(IApprovalSummary document)
    {
    }

    /// <summary>What an edit's return to draft does beyond the summary: ReadyToPost goes back to Draft.</summary>
    protected virtual void OnReturnedToDraft(IApprovalSummary document)
    {
    }

    private DateOnly Today => DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

    public async Task<ApprovalResult> SubmitAsync(ApprovalRequestKind kind, long id, CancellationToken ct)
    {
        IApprovalSummary? document = await LoadAsync(kind, id, ct);
        if (document is null)
        {
            return new ApprovalResult(ApprovalResultOutcome.NotFound);
        }

        if (!IsDraft(document))
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
            return new ApprovalResult(ApprovalResultOutcome.Unavailable,
                Detail: "The approval rules could not be read. Try again in a moment.");
        }

        switch (resolved.Outcome)
        {
            case ResolveChainOutcome.NoWorkflow:
                return new ApprovalResult(ApprovalResultOutcome.NoWorkflow,
                    Detail: "No approval workflow applies to this document. Approve or post it as usual.");
            case ResolveChainOutcome.Unresolvable:
                return Refused(resolved.Detail ?? "The approval chain could not be worked out.");
        }

        int round = (await LatestRoundAsync(kind, id, ct) ?? 0) + 1;
        List<TStep> steps = DocumentApproval.Steps(resolved, id, kind, () => NewStep(round));
        AddSteps(steps);

        ApprovalStatus status = ApprovalChain.Start(steps);
        if (status == ApprovalStatus.Approved)
        {
            // Every level skipped — the requester outranks them all, say.
            OnApproved(document);
        }

        DocumentApproval.Summarize(document, status, steps);
        await Db.SaveChangesAsync(ct);
        return new ApprovalResult(ApprovalResultOutcome.Ok, status.ToString());
    }

    public async Task<ApprovalResult> ActAsync(
        ApprovalRequestKind kind, long id, ApprovalAction action, string? comments, CancellationToken ct)
    {
        IApprovalSummary? document = await LoadAsync(kind, id, ct);
        if (document is null)
        {
            return new ApprovalResult(ApprovalResultOutcome.NotFound);
        }

        List<TStep> steps = await CurrentRoundAsync(kind, id, ct);
        if (document.ApprovalStatus != ApprovalStatus.InApproval || ApprovalChain.Current(steps) is not { } current)
        {
            return Refused(ApprovalChain.Message(ApprovalRefusal.NotInApproval));
        }

        if (_user.UserId is not Guid actor)
        {
            return new ApprovalResult(ApprovalResultOutcome.NotTheApprover, Detail: ApprovalChain.Message(ApprovalRefusal.NotTheApprover));
        }

        // A document's levels are separate pairs of eyes: whoever approved one
        // level of this round does not approve another, even holding its role.
        // HRMS requests leave this to Master's skip rules; documents enforce it.
        if (action == ApprovalAction.Approve && ApprovalChain.ApprovedEarlier(steps, actor))
        {
            return Refused(ApprovalChain.Message(ApprovalRefusal.ApprovedEarlierLevel));
        }

        bool mayAct = await MayActAsync(current, actor, ct);
        ApprovalOutcome outcome = ApprovalChain.Act(steps, action, actor, mayAct, comments, _clock.GetUtcNow());

        if (outcome.IsRefused)
        {
            return outcome.Refusal == ApprovalRefusal.NotTheApprover
                ? new ApprovalResult(ApprovalResultOutcome.NotTheApprover, Detail: ApprovalChain.Message(outcome.Refusal))
                : Refused(ApprovalChain.Message(outcome.Refusal));
        }

        if (outcome.RequestStatus == ApprovalStatus.Approved)
        {
            OnApproved(document);
        }

        DocumentApproval.Summarize(document, outcome.RequestStatus, steps);
        await Db.SaveChangesAsync(ct);
        return new ApprovalResult(ApprovalResultOutcome.Ok, outcome.RequestStatus.ToString());
    }

    public async Task<ApprovalChainView?> ChainAsync(ApprovalRequestKind kind, long id, CancellationToken ct)
    {
        IApprovalSummary? document = await LoadAsync(kind, id, ct);
        if (document is null)
        {
            return null;
        }

        List<TStep> steps = await CurrentRoundAsync(kind, id, ct);
        TStep? current = document.ApprovalStatus == ApprovalStatus.InApproval ? ApprovalChain.Current(steps) : null;

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
    /// Whether the ordinary approve, or posting a draft, may go ahead: null when
    /// it may, otherwise why not. An approved chain passes; an open or rejected
    /// one does not; a document no chain has touched passes only when no
    /// workflow applies — and "could not ask" never reads as "no rules".
    /// </summary>
    public async Task<string?> GateAsync(ApprovalRequestKind kind, IApprovalSummary document, CancellationToken ct)
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
    /// decision 6): the round's open steps are cancelled and stay as history.
    /// True when that happened, so the caller can say so. The caller's save writes it.
    /// </summary>
    public async Task<bool> ReturnToDraftAsync(ApprovalRequestKind kind, long id, IApprovalSummary document, CancellationToken ct)
    {
        if (document.ApprovalStatus is null or ApprovalStatus.Draft)
        {
            return false;
        }

        ApprovalChain.CancelOpen(await CurrentRoundAsync(kind, id, ct));
        OnReturnedToDraft(document);
        DocumentApproval.Summarize<TStep>(document, null, []);
        return true;
    }

    /// <summary>What waits on the signed-in user: levels naming them, or their role.</summary>
    public async Task<List<ApprovalInboxItem>> MineAsync(CancellationToken ct)
    {
        if (_user.UserId is not Guid me)
        {
            return [];
        }

        List<TStep> waiting = await PendingForAsync(me, _user.RoleId, ct);
        List<ApprovalInboxItem> items = [];

        foreach (IGrouping<ApprovalRequestKind, TStep> byKind in waiting.GroupBy(s => s.RequestKind))
        {
            List<long> ids = [.. byKind.Select(s => s.RequestId).Distinct()];
            foreach (ApprovalDocumentRow row in await DescribeAsync(byKind.Key, ids, ct))
            {
                items.Add(new ApprovalInboxItem
                {
                    Service = ServiceName,
                    Document = SegmentOf(byKind.Key),
                    RequestKind = byKind.Key.ToString(),
                    RequestId = row.Id,
                    DocumentNo = row.DocumentNo,
                    DocumentDate = row.DocumentDate,
                    Amount = row.Amount,
                    Label = byKind.First(s => s.RequestId == row.Id).Label,
                });
            }
        }

        return [.. items.OrderBy(i => i.DocumentDate)];
    }

    private async Task<List<TStep>> CurrentRoundAsync(ApprovalRequestKind kind, long id, CancellationToken ct) =>
        await LatestRoundAsync(kind, id, ct) is int round ? await RoundStepsAsync(kind, id, round, ct) : [];

    private async Task<bool> MayActAsync(TStep current, Guid actor, CancellationToken ct)
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

    private async Task<ResolveChainResponse?> ResolveAsync(ApprovalRequestKind kind, IApprovalSummary document, CancellationToken ct) =>
        await _chains.ResolveAsync(new ResolveChainRequest
        {
            CustomerId = _tenant.CustomerId ?? Guid.Empty,
            OrgId = _tenant.OrgId ?? Guid.Empty,
            RequestKind = kind,
            RequesterUserId = RequesterOf(document) ?? _user.UserId,
            Amount = await AmountOfAsync(document, ct),
            OnDate = Today,
        }, ct);

    private static ApprovalResult Refused(string detail) => new(ApprovalResultOutcome.Refused, Detail: detail);
}

/// <summary>
/// An approver's move on a document's current level (TK-100, TK-101): the body
/// every service's <c>…/approval</c> route takes.
/// </summary>
public sealed class DocumentApprovalActionRequest
{
    [EnumDataType(typeof(ApprovalAction), ErrorMessage = "Choose approve, reject or send back.")]
    public ApprovalAction Action { get; set; } = ApprovalAction.Approve;

    [MaxLength(2000, ErrorMessage = "Comments cannot exceed 2000 characters.")]
    public string? Comments { get; set; }
}

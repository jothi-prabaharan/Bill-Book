namespace Shared.Kernel.Approvals;

/// <summary>What an actor asked to do to the current step.</summary>
public enum ApprovalAction
{
    Approve = 1,
    Reject = 2,
    SendBack = 3,
}

/// <summary>Why an action was refused, or <see cref="None"/>.</summary>
public enum ApprovalRefusal
{
    None = 0,

    /// <summary>No step is pending: the request is not in approval.</summary>
    NotInApproval = 1,

    /// <summary>The actor is not the pending step's approver, a holder of its role, or a delegate.</summary>
    NotTheApprover = 2,

    /// <summary>The level requires a comment, or the action always does (reject, send back).</summary>
    CommentRequired = 3,
}

/// <summary>The outcome of one action on a chain.</summary>
public sealed record ApprovalOutcome(ApprovalRefusal Refusal, ApprovalStatus RequestStatus, bool ReturnedToRequester = false)
{
    public bool IsRefused => Refusal != ApprovalRefusal.None;
}

/// <summary>
/// The step state machine (D-26, TK-99), pure, so every service moves its steps
/// the same way and one set of tests covers them all.
///
/// <list type="bullet">
/// <item>Exactly one step is Pending, in sequence; the rest wait.</item>
/// <item><b>Approve</b> moves to the next level; the last approval approves the request.</item>
/// <item><b>Reject</b> ends the request.</item>
/// <item><b>Send back</b> returns it to the previous level, or to the requester
/// from the first, with a comment. The chain resumes from there.</item>
/// <item>Skipped steps are passed over, never made Pending.</item>
/// </list>
/// </summary>
public static class ApprovalChain
{
    /// <summary>One sentence per refusal, for every service's controller. Never names a table or a figure.</summary>
    public static string Message(ApprovalRefusal refusal) => refusal switch
    {
        ApprovalRefusal.NotInApproval => "This request is not waiting for an approval.",
        ApprovalRefusal.NotTheApprover => "This request is waiting for someone else's approval.",
        ApprovalRefusal.CommentRequired => "Add a comment to say why.",
        _ => string.Empty,
    };

    /// <summary>The step that is Pending now, or null.</summary>
    public static T? Current<T>(IEnumerable<T> steps) where T : ApprovalStepBase =>
        steps.Where(s => s.StepStatus == ApprovalStepStatus.Pending).OrderBy(s => s.Sequence).FirstOrDefault();

    /// <summary>
    /// Starts a freshly resolved chain: the first step that is not skipped
    /// becomes Pending. With every step skipped, the request is approved at once.
    /// </summary>
    public static ApprovalStatus Start<T>(IReadOnlyList<T> steps) where T : ApprovalStepBase
    {
        T? first = Next(steps, after: 0);
        if (first is null)
        {
            return ApprovalStatus.Approved;
        }

        first.StepStatus = ApprovalStepStatus.Pending;
        return ApprovalStatus.InApproval;
    }

    /// <summary>
    /// Whether <paramref name="actor"/> may act on <paramref name="step"/>: its
    /// user, a holder of its role, or an active delegate of its user.
    /// </summary>
    public static bool MayAct(ApprovalStepBase step, Guid actor, IReadOnlySet<int> actorRoles, bool actorIsDelegate) =>
        step.ApproverUserId == actor
        || (step.RoleId is int role && actorRoles.Contains(role))
        || (step.ApproverUserId is not null && actorIsDelegate);

    /// <summary>
    /// Applies an action by <paramref name="actor"/> to the Pending step.
    /// <paramref name="mayAct"/> is the caller's answer to <see cref="MayAct"/>,
    /// since only the caller knows the actor's roles and delegations.
    /// </summary>
    public static ApprovalOutcome Act<T>(
        IReadOnlyList<T> steps, ApprovalAction action, Guid actor, bool mayAct, string? comments, DateTimeOffset now)
        where T : ApprovalStepBase
    {
        T? current = Current(steps);
        if (current is null)
        {
            return new ApprovalOutcome(ApprovalRefusal.NotInApproval, ApprovalStatus.Draft);
        }

        if (!mayAct)
        {
            return new ApprovalOutcome(ApprovalRefusal.NotTheApprover, ApprovalStatus.InApproval);
        }

        bool commentNeeded = action != ApprovalAction.Approve || current.IsCommentRequired;
        if (commentNeeded && string.IsNullOrWhiteSpace(comments))
        {
            return new ApprovalOutcome(ApprovalRefusal.CommentRequired, ApprovalStatus.InApproval);
        }

        current.ActedByUserId = actor;
        current.ActedAt = now;
        current.Comments = string.IsNullOrWhiteSpace(comments) ? null : comments.Trim();

        switch (action)
        {
            case ApprovalAction.Approve:
            {
                current.StepStatus = ApprovalStepStatus.Approved;
                T? next = Next(steps, current.Sequence);
                if (next is null)
                {
                    return new ApprovalOutcome(ApprovalRefusal.None, ApprovalStatus.Approved);
                }

                next.StepStatus = ApprovalStepStatus.Pending;
                return new ApprovalOutcome(ApprovalRefusal.None, ApprovalStatus.InApproval);
            }

            case ApprovalAction.Reject:
                current.StepStatus = ApprovalStepStatus.Rejected;
                foreach (T waiting in steps.Where(s => s.StepStatus == ApprovalStepStatus.Waiting))
                {
                    waiting.StepStatus = ApprovalStepStatus.Cancelled;
                }

                return new ApprovalOutcome(ApprovalRefusal.None, ApprovalStatus.Rejected);

            default:
            {
                current.StepStatus = ApprovalStepStatus.SentBack;
                T? previous = steps
                    .Where(s => s.Sequence < current.Sequence && s.StepStatus == ApprovalStepStatus.Approved)
                    .OrderByDescending(s => s.Sequence)
                    .FirstOrDefault();

                if (previous is null)
                {
                    // Back to the requester, who edits and submits again.
                    foreach (T waiting in steps.Where(s => s.StepStatus == ApprovalStepStatus.Waiting))
                    {
                        waiting.StepStatus = ApprovalStepStatus.Cancelled;
                    }

                    return new ApprovalOutcome(ApprovalRefusal.None, ApprovalStatus.Draft, ReturnedToRequester: true);
                }

                // The previous level looks again: its decision is reopened, and
                // the one that sent it back waits its turn once more.
                previous.StepStatus = ApprovalStepStatus.Pending;
                previous.ActedAt = null;
                previous.ActedByUserId = null;
                current.StepStatus = ApprovalStepStatus.Waiting;
                return new ApprovalOutcome(ApprovalRefusal.None, ApprovalStatus.InApproval);
            }
        }
    }

    /// <summary>Cancels every open step, e.g. when the requester edits or withdraws the request.</summary>
    public static void CancelOpen<T>(IEnumerable<T> steps) where T : ApprovalStepBase
    {
        foreach (T step in steps.Where(s => s.StepStatus is ApprovalStepStatus.Pending or ApprovalStepStatus.Waiting))
        {
            step.StepStatus = ApprovalStepStatus.Cancelled;
        }
    }

    private static T? Next<T>(IReadOnlyList<T> steps, int after) where T : ApprovalStepBase =>
        steps
            .Where(s => s.Sequence > after && s.StepStatus == ApprovalStepStatus.Waiting)
            .OrderBy(s => s.Sequence)
            .FirstOrDefault();
}

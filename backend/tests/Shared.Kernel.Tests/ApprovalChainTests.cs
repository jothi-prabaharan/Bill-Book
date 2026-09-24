using Shared.Kernel.Approvals;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// The one approval state machine (D-26, TK-99). Every service that stores
/// steps moves them through <see cref="ApprovalChain"/>, so these cases are
/// the answer for leave, purchase orders and claims alike.
/// </summary>
public sealed class ApprovalChainTests
{
    private sealed class Step : ApprovalStepBase;

    private static readonly Guid Manager = Guid.NewGuid();
    private static readonly Guid Hr = Guid.NewGuid();
    private static readonly Guid Stranger = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 10, 0, 0, TimeSpan.Zero);

    private static List<Step> TwoLevels() =>
    [
        new() { Sequence = 1, Label = "Manager", ApproverUserId = Manager },
        new() { Sequence = 2, Label = "HR", ApproverUserId = Hr },
    ];

    [Fact]
    public void Starting_makes_the_first_step_pending_and_the_rest_wait()
    {
        List<Step> steps = TwoLevels();

        Assert.Equal(ApprovalStatus.InApproval, ApprovalChain.Start(steps));
        Assert.Equal(ApprovalStepStatus.Pending, steps[0].StepStatus);
        Assert.Equal(ApprovalStepStatus.Waiting, steps[1].StepStatus);
        Assert.Same(steps[0], ApprovalChain.Current(steps));
    }

    [Fact]
    public void A_skipped_step_is_passed_over_when_starting()
    {
        List<Step> steps = TwoLevels();
        steps[0].StepStatus = ApprovalStepStatus.Skipped;

        ApprovalChain.Start(steps);

        Assert.Equal(ApprovalStepStatus.Skipped, steps[0].StepStatus);
        Assert.Equal(ApprovalStepStatus.Pending, steps[1].StepStatus);
    }

    [Fact]
    public void A_chain_with_every_step_skipped_is_approved_at_once()
    {
        List<Step> steps = TwoLevels();
        steps.ForEach(s => s.StepStatus = ApprovalStepStatus.Skipped);

        Assert.Equal(ApprovalStatus.Approved, ApprovalChain.Start(steps));
    }

    [Fact]
    public void An_empty_chain_is_approved_at_once() =>
        Assert.Equal(ApprovalStatus.Approved, ApprovalChain.Start(new List<Step>()));

    [Fact]
    public void Approving_moves_to_the_next_level_then_approves_the_request()
    {
        List<Step> steps = TwoLevels();
        ApprovalChain.Start(steps);

        ApprovalOutcome first = ApprovalChain.Act(steps, ApprovalAction.Approve, Manager, true, null, Now);
        Assert.Equal(ApprovalStatus.InApproval, first.RequestStatus);
        Assert.Equal(ApprovalStepStatus.Approved, steps[0].StepStatus);
        Assert.Equal(Manager, steps[0].ActedByUserId);
        Assert.Equal(Now, steps[0].ActedAt);
        Assert.Equal(ApprovalStepStatus.Pending, steps[1].StepStatus);

        ApprovalOutcome last = ApprovalChain.Act(steps, ApprovalAction.Approve, Hr, true, null, Now);
        Assert.False(last.IsRefused);
        Assert.Equal(ApprovalStatus.Approved, last.RequestStatus);
        Assert.Null(ApprovalChain.Current(steps));
    }

    [Fact]
    public void Approving_skips_over_a_skipped_level_in_the_middle()
    {
        List<Step> steps =
        [
            new() { Sequence = 1, Label = "Lead", ApproverUserId = Manager },
            new() { Sequence = 2, Label = "Manager", StepStatus = ApprovalStepStatus.Skipped },
            new() { Sequence = 3, Label = "HR", ApproverUserId = Hr },
        ];
        ApprovalChain.Start(steps);

        ApprovalChain.Act(steps, ApprovalAction.Approve, Manager, true, null, Now);

        Assert.Equal(ApprovalStepStatus.Skipped, steps[1].StepStatus);
        Assert.Equal(ApprovalStepStatus.Pending, steps[2].StepStatus);
    }

    [Fact]
    public void Someone_who_may_not_act_is_refused_and_nothing_moves()
    {
        List<Step> steps = TwoLevels();
        ApprovalChain.Start(steps);

        ApprovalOutcome outcome = ApprovalChain.Act(steps, ApprovalAction.Approve, Stranger, false, null, Now);

        Assert.Equal(ApprovalRefusal.NotTheApprover, outcome.Refusal);
        Assert.Equal(ApprovalStepStatus.Pending, steps[0].StepStatus);
        Assert.Null(steps[0].ActedByUserId);
    }

    [Fact]
    public void Acting_on_a_request_that_is_not_in_approval_is_refused()
    {
        List<Step> steps = TwoLevels();

        Assert.Equal(ApprovalRefusal.NotInApproval,
            ApprovalChain.Act(steps, ApprovalAction.Approve, Manager, true, null, Now).Refusal);
    }

    [Theory]
    [InlineData(ApprovalAction.Reject)]
    [InlineData(ApprovalAction.SendBack)]
    public void Reject_and_send_back_always_need_a_comment(ApprovalAction action)
    {
        List<Step> steps = TwoLevels();
        ApprovalChain.Start(steps);

        Assert.Equal(ApprovalRefusal.CommentRequired, ApprovalChain.Act(steps, action, Manager, true, "  ", Now).Refusal);
        Assert.Equal(ApprovalStepStatus.Pending, steps[0].StepStatus);
    }

    [Fact]
    public void A_level_that_requires_a_comment_needs_one_to_approve()
    {
        List<Step> steps = TwoLevels();
        steps[0].IsCommentRequired = true;
        ApprovalChain.Start(steps);

        Assert.Equal(ApprovalRefusal.CommentRequired,
            ApprovalChain.Act(steps, ApprovalAction.Approve, Manager, true, null, Now).Refusal);
        Assert.False(ApprovalChain.Act(steps, ApprovalAction.Approve, Manager, true, "Covered by Priya", Now).IsRefused);
        Assert.Equal("Covered by Priya", steps[0].Comments);
    }

    [Fact]
    public void Rejecting_ends_the_request_and_cancels_the_levels_still_waiting()
    {
        List<Step> steps = TwoLevels();
        ApprovalChain.Start(steps);

        ApprovalOutcome outcome = ApprovalChain.Act(steps, ApprovalAction.Reject, Manager, true, "Peak season", Now);

        Assert.Equal(ApprovalStatus.Rejected, outcome.RequestStatus);
        Assert.Equal(ApprovalStepStatus.Rejected, steps[0].StepStatus);
        Assert.Equal(ApprovalStepStatus.Cancelled, steps[1].StepStatus);
    }

    [Fact]
    public void Sending_back_from_the_first_level_returns_the_request_to_the_requester()
    {
        List<Step> steps = TwoLevels();
        ApprovalChain.Start(steps);

        ApprovalOutcome outcome = ApprovalChain.Act(steps, ApprovalAction.SendBack, Manager, true, "Wrong dates", Now);

        Assert.True(outcome.ReturnedToRequester);
        Assert.Equal(ApprovalStatus.Draft, outcome.RequestStatus);
        Assert.Equal(ApprovalStepStatus.SentBack, steps[0].StepStatus);
        Assert.Equal(ApprovalStepStatus.Cancelled, steps[1].StepStatus);
    }

    [Fact]
    public void Sending_back_from_a_later_level_reopens_the_level_before()
    {
        List<Step> steps = TwoLevels();
        ApprovalChain.Start(steps);
        ApprovalChain.Act(steps, ApprovalAction.Approve, Manager, true, null, Now);

        ApprovalOutcome outcome = ApprovalChain.Act(steps, ApprovalAction.SendBack, Hr, true, "Check the balance", Now);

        Assert.False(outcome.ReturnedToRequester);
        Assert.Equal(ApprovalStatus.InApproval, outcome.RequestStatus);
        Assert.Equal(ApprovalStepStatus.Pending, steps[0].StepStatus);
        Assert.Null(steps[0].ActedByUserId);
        Assert.Equal(ApprovalStepStatus.Waiting, steps[1].StepStatus);

        // The chain resumes from there.
        ApprovalChain.Act(steps, ApprovalAction.Approve, Manager, true, null, Now);
        Assert.Equal(ApprovalStepStatus.Pending, steps[1].StepStatus);
    }

    [Fact]
    public void Its_user_a_holder_of_its_role_or_a_delegate_may_act()
    {
        var userStep = new Step { Label = "Manager", ApproverUserId = Manager };
        var roleStep = new Step { Label = "HR", RoleId = 7 };
        var none = new HashSet<int>();

        Assert.True(ApprovalChain.MayAct(userStep, Manager, none, actorIsDelegate: false));
        Assert.True(ApprovalChain.MayAct(userStep, Stranger, none, actorIsDelegate: true));
        Assert.False(ApprovalChain.MayAct(userStep, Stranger, none, actorIsDelegate: false));
        Assert.True(ApprovalChain.MayAct(roleStep, Stranger, new HashSet<int> { 7 }, actorIsDelegate: false));
        Assert.False(ApprovalChain.MayAct(roleStep, Stranger, new HashSet<int> { 8 }, actorIsDelegate: false));

        // A delegation stands in for a person, never for a role.
        Assert.False(ApprovalChain.MayAct(roleStep, Stranger, none, actorIsDelegate: true));
    }

    [Fact]
    public void Withdrawing_cancels_every_open_step_and_keeps_the_decided_ones()
    {
        List<Step> steps = TwoLevels();
        ApprovalChain.Start(steps);
        ApprovalChain.Act(steps, ApprovalAction.Approve, Manager, true, null, Now);

        ApprovalChain.CancelOpen(steps);

        Assert.Equal(ApprovalStepStatus.Approved, steps[0].StepStatus);
        Assert.Equal(ApprovalStepStatus.Cancelled, steps[1].StepStatus);
    }

    [Fact]
    public void Every_refusal_has_a_sentence()
    {
        foreach (ApprovalRefusal refusal in Enum.GetValues<ApprovalRefusal>().Where(r => r != ApprovalRefusal.None))
        {
            Assert.False(string.IsNullOrWhiteSpace(ApprovalChain.Message(refusal)));
        }
    }
}

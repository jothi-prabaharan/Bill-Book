using Hrm.Api.Services;
using Hrm.Entity.Enums;
using Xunit;
using Node = Hrm.Api.Services.EmployeeApproverService.Node;

namespace Hrm.Api.Tests;

/// <summary>
/// The employee approvers Hrm answers for Master (D-26, TK-49): walking the
/// reporting chain, and which employees can act at all. Pure.
/// </summary>
public sealed class EmployeeApproverTests
{
    private static readonly Guid AsokUser = Guid.NewGuid();
    private static readonly Guid MeenaUser = Guid.NewGuid();

    // 1 reports to 2, who reports to 3, who reports to nobody.
    private static Dictionary<long, Node> Chain() => new()
    {
        [1] = new Node(1, 2, 10, Guid.NewGuid(), EmployeeStatus.Active),
        [2] = new Node(2, 3, 10, AsokUser, EmployeeStatus.Active),
        [3] = new Node(3, null, 10, MeenaUser, EmployeeStatus.Active),
    };

    [Theory]
    [InlineData(1, 2L)]
    [InlineData(2, 3L)]
    [InlineData(3, null)]
    public void Depth_counts_managers_up_the_chain(int depth, long? expected) =>
        Assert.Equal(expected, EmployeeApproverService.Manager(Chain(), 1, depth));

    [Fact]
    public void Someone_with_no_manager_has_no_reporting_approver() =>
        Assert.Null(EmployeeApproverService.Manager(Chain(), 3, 1));

    [Fact]
    public void A_loop_in_the_reporting_line_ends_rather_than_spins()
    {
        Dictionary<long, Node> loop = new()
        {
            [1] = new Node(1, 2, 10, null, EmployeeStatus.Active),
            [2] = new Node(2, 1, 10, null, EmployeeStatus.Active),
        };

        Assert.Equal(2, EmployeeApproverService.Manager(loop, 1, 1));
        Assert.Null(EmployeeApproverService.Manager(loop, 1, 2));
    }

    [Fact]
    public void An_approver_with_a_login_is_answered_with_it()
    {
        var answer = EmployeeApproverService.Answer(Chain(), 1, 2);

        Assert.Equal(2, answer.EmployeeId);
        Assert.Equal(AsokUser, answer.UserId);
        Assert.Equal(1, answer.Sequence);
    }

    [Fact]
    public void An_approver_who_has_left_cannot_act()
    {
        Dictionary<long, Node> graph = Chain();
        graph[2] = graph[2] with { EmployeeStatus = EmployeeStatus.Exited };

        Assert.Null(EmployeeApproverService.Answer(graph, 1, 2).UserId);
    }

    [Fact]
    public void An_approver_with_no_login_cannot_act()
    {
        Dictionary<long, Node> graph = Chain();
        graph[2] = graph[2] with { UserId = null };

        Assert.Null(EmployeeApproverService.Answer(graph, 1, 2).UserId);
    }

    [Fact]
    public void No_approver_found_is_an_empty_answer()
    {
        var answer = EmployeeApproverService.Answer(Chain(), 4, null);

        Assert.Equal(4, answer.Sequence);
        Assert.Null(answer.EmployeeId);
        Assert.Null(answer.UserId);
    }
}

using Master.Api.Services;
using Master.Entity.TableEntities;
using Shared.Kernel.Approvals;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// How Master turns a workflow into a chain (D-26, TK-99): which workflow
/// applies, which levels the amount calls for, and the skip rules. All pure,
/// so none of it needs a database or Hrm.
/// </summary>
public sealed class ApprovalChainResolverTests
{
    private static readonly Guid Requester = Guid.NewGuid();
    private static readonly Guid Manager = Guid.NewGuid();
    private static readonly Guid Director = Guid.NewGuid();

    private static ApprovalWorkflow Workflow(long id, long? department = null, long? grade = null, long? location = null, int day = 1) => new()
    {
        ApprovalWorkflowId = id,
        Name = $"W{id}",
        RequestKind = ApprovalRequestKind.Leave,
        DepartmentId = department,
        GradeId = grade,
        WorkLocationId = location,
        EffectiveFrom = new DateOnly(2026, 4, day),
    };

    private static ResolveChainRequest For(long department = 10, long grade = 20, long location = 30) => new()
    {
        RequestKind = ApprovalRequestKind.Leave,
        DepartmentId = department,
        GradeId = grade,
        WorkLocationId = location,
        RequesterUserId = Requester,
        OnDate = new DateOnly(2026, 9, 24),
    };

    // ---- Which workflow -----------------------------------------------------

    [Fact]
    public void The_workflow_naming_most_of_the_requester_wins()
    {
        ApprovalWorkflow general = Workflow(1);
        ApprovalWorkflow department = Workflow(2, department: 10);
        ApprovalWorkflow departmentAndGrade = Workflow(3, department: 10, grade: 20);

        Assert.Same(departmentAndGrade, ApprovalChainResolver.MostSpecific([general, department, departmentAndGrade], For()));
    }

    [Fact]
    public void A_workflow_for_another_department_never_applies()
    {
        ApprovalWorkflow general = Workflow(1);
        ApprovalWorkflow sales = Workflow(2, department: 99);

        Assert.Same(general, ApprovalChainResolver.MostSpecific([general, sales], For()));
        Assert.Null(ApprovalChainResolver.MostSpecific([sales], For()));
    }

    [Fact]
    public void Between_equally_specific_workflows_the_latest_in_force_wins()
    {
        ApprovalWorkflow older = Workflow(1, day: 1);
        ApprovalWorkflow newer = Workflow(2, day: 15);

        Assert.Same(newer, ApprovalChainResolver.MostSpecific([older, newer], For()));
    }

    // ---- Amount thresholds -------------------------------------------------

    [Theory]
    [InlineData(null, false)]
    [InlineData(50000.0, false)]
    [InlineData(50000.01, true)]
    public void A_level_with_a_floor_applies_only_above_it(double? amount, bool applies)
    {
        var level = new ApprovalWorkflowLevel { AboveAmount = 50000m };

        // No amount at all never clears a floor.
        Assert.Equal(applies, ApprovalChainResolver.AppliesTo(level, (decimal?)amount));
    }

    [Fact]
    public void A_level_with_no_floor_always_applies() =>
        Assert.True(ApprovalChainResolver.AppliesTo(new ApprovalWorkflowLevel(), null));

    // ---- Skip rules ----------------------------------------------------------

    private static ApprovalWorkflowLevel Level(int sequence, ApproverKind kind, bool optional = false, Guid? user = null, int? role = null) => new()
    {
        Sequence = sequence,
        Label = $"L{sequence}",
        ApproverKind = kind,
        UserId = user,
        RoleId = role,
        IsOptional = optional,
    };

    private static Dictionary<int, EmployeeApproverAnswer> Employees(params (int Sequence, Guid? User)[] answers) =>
        answers.ToDictionary(a => a.Sequence, a => new EmployeeApproverAnswer { Sequence = a.Sequence, EmployeeId = 1, UserId = a.User });

    [Fact]
    public void Each_level_takes_its_approver_from_the_right_source()
    {
        ResolveChainResponse chain = ApprovalChainResolver.Apply("W",
            [Level(1, ApproverKind.ReportingChain), Level(2, ApproverKind.RoleHolder, role: 7), Level(3, ApproverKind.NamedUser, user: Director)],
            Employees((1, Manager)), Requester);

        Assert.Equal(ResolveChainOutcome.Resolved, chain.Outcome);
        Assert.Equal(Manager, chain.Steps[0].ApproverUserId);
        Assert.Equal(7, chain.Steps[1].RoleId);
        Assert.Equal(Director, chain.Steps[2].ApproverUserId);
        Assert.All(chain.Steps, s => Assert.False(s.IsSkipped));
    }

    [Fact]
    public void Nobody_approves_their_own_request()
    {
        ResolveChainResponse chain = ApprovalChainResolver.Apply("W",
            [Level(1, ApproverKind.NamedUser, user: Requester), Level(2, ApproverKind.NamedUser, user: Director)],
            Employees(), Requester);

        Assert.True(chain.Steps[0].IsSkipped);
        Assert.False(chain.Steps[1].IsSkipped);
    }

    [Fact]
    public void Nobody_approves_the_same_request_twice_in_a_row()
    {
        // The manager is also the department head: one approval, not two.
        ResolveChainResponse chain = ApprovalChainResolver.Apply("W",
            [Level(1, ApproverKind.ReportingChain), Level(2, ApproverKind.DepartmentHead)],
            Employees((1, Manager), (2, Manager)), Requester);

        Assert.False(chain.Steps[0].IsSkipped);
        Assert.True(chain.Steps[1].IsSkipped);
    }

    [Fact]
    public void An_optional_level_with_no_approver_is_skipped()
    {
        ResolveChainResponse chain = ApprovalChainResolver.Apply("W",
            [Level(1, ApproverKind.ReportingChain, optional: true), Level(2, ApproverKind.NamedUser, user: Director)],
            Employees(), Requester);

        Assert.Equal(ResolveChainOutcome.Resolved, chain.Outcome);
        Assert.True(chain.Steps[0].IsSkipped);
    }

    [Fact]
    public void An_employee_approver_with_no_login_cannot_act()
    {
        ResolveChainResponse chain = ApprovalChainResolver.Apply("W",
            [Level(1, ApproverKind.ReportingChain, optional: true)],
            Employees((1, null)), Requester);

        Assert.True(chain.Steps[0].IsSkipped);
    }

    [Fact]
    public void A_required_level_with_no_approver_refuses_the_chain_and_names_the_level()
    {
        ResolveChainResponse chain = ApprovalChainResolver.Apply("W",
            [Level(1, ApproverKind.NamedUser, user: Director), Level(2, ApproverKind.DepartmentHead)],
            Employees(), Requester);

        Assert.Equal(ResolveChainOutcome.Unresolvable, chain.Outcome);
        Assert.Contains("L2", chain.Detail);
        Assert.Empty(chain.Steps);
    }

    // ---- The card's Done when ----------------------------------------------

    /// <summary>"Accountant, then Owner above ₹1,00,000": one step for ₹50,000, two for ₹2,00,000.</summary>
    [Theory]
    [InlineData(50000.0, 1)]
    [InlineData(200000.0, 2)]
    public void Accountant_then_owner_above_one_lakh(double amount, int expectedSteps)
    {
        ApprovalWorkflowLevel accountant = Level(1, ApproverKind.RoleHolder, role: 3);
        ApprovalWorkflowLevel owner = Level(2, ApproverKind.RoleHolder, role: 1);
        owner.AboveAmount = 100000m;

        List<ApprovalWorkflowLevel> levels = new[] { accountant, owner }
            .Where(l => ApprovalChainResolver.AppliesTo(l, (decimal)amount))
            .ToList();
        ResolveChainResponse chain = ApprovalChainResolver.Apply("Purchase bills", levels, Employees(), Requester);

        Assert.Equal(ResolveChainOutcome.Resolved, chain.Outcome);
        Assert.Equal(expectedSteps, chain.Steps.Count);
        Assert.Equal(3, chain.Steps[0].RoleId);
    }
}

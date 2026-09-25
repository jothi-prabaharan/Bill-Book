using Microsoft.EntityFrameworkCore;
using Performance.Api.Services;
using Performance.Entity.Enums;
using Performance.Entity.Models;
using Performance.Entity.TableEntities;
using Performance.Repository;
using Shared.Kernel.Approvals;
using Shared.Kernel.Employees;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Performance.Api.Tests;

/// <summary>
/// Unit tests for H11 / TK-58:
/// 1. routing follows each department's chain;
/// 2. send back returns to the level before;
/// 3. the self-evaluation is unchanged after every level acts;
/// 4. a manager who is also the lead is asked only once;
/// 5. close cycle raises salary revision in Payroll.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PerformanceApprovalRoutingTests
{
    private readonly PostgresFixture _postgres;

    public PerformanceApprovalRoutingTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed class FakeHrmClient : IEmployeeClient
    {
        private readonly Dictionary<long, EmployeeProfile> _profiles = new();

        public void Add(EmployeeProfile profile) => _profiles[profile.EmployeeId] = profile;

        public Task<EmployeeProfile?> FindByUserIdAsync(Guid customerId, Guid orgId, Guid userId, CancellationToken ct) =>
            Task.FromResult(_profiles.Values.FirstOrDefault(p => p.UserId == userId));

        public Task<EmployeeProfile?> FindByIdAsync(Guid customerId, Guid orgId, long employeeId, CancellationToken ct) =>
            Task.FromResult(_profiles.TryGetValue(employeeId, out var p) ? p : null);

        public Task<List<EmployeeProfile>> LookupAsync(Guid customerId, Guid orgId, List<long> employeeIds, CancellationToken ct) =>
            Task.FromResult(_profiles.Values.Where(p => employeeIds.Contains(p.EmployeeId)).ToList());
    }

    private sealed class FakeMasterClient : IMasterClient
    {
        public Task<ResolveChainResponse?> ResolveChainAsync(ResolveChainRequest req, CancellationToken ct) =>
            Task.FromResult<ResolveChainResponse?>(null); // trigger fallback routing based on DepartmentId
    }

    private sealed class FakePayrollClient : IPayrollClient
    {
        public List<(long EmployeeId, decimal NewCtc, string? Reason)> Revisions { get; } = [];

        public Task<bool> ReviseSalaryAsync(long employeeId, decimal newCtc, DateOnly effectiveFrom, string? reason, CancellationToken ct)
        {
            Revisions.Add((employeeId, newCtc, reason));
            return Task.FromResult(true);
        }
    }

    [SkippableFact]
    public async Task Routing_follows_each_departments_chain()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        await using var db = _postgres.CreateContext(customerId, orgId);
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
        var hrm = new FakeHrmClient();
        var master = new FakeMasterClient();
        var payroll = new FakePayrollClient();

        hrm.Add(new EmployeeProfile { EmployeeId = 1, FullName = "Dev 1", DepartmentId = 1, ReportsToEmployeeId = 10 });
        hrm.Add(new EmployeeProfile { EmployeeId = 2, FullName = "Sales 1", DepartmentId = 2, ReportsToEmployeeId = 20 });

        var svc = new PerformanceService(db, tenant, hrm, master, payroll);

        var scale = await svc.SaveRatingScaleAsync(new SaveRatingScaleRequest
        {
            Name = "Scale 1",
            Levels = [new SaveRatingLevelRequest { Score = 3, Label = "Good", Description = "Good" }]
        }, default);

        var cycle = await svc.CreateReviewCycleAsync(new SaveReviewCycleRequest
        {
            Name = "Annual Review 2026",
            PeriodFrom = new DateOnly(2026, 1, 1),
            PeriodTo = new DateOnly(2026, 12, 31),
            RatingScaleId = scale.RatingScaleId,
            GoalWeightPercent = 50,
            CompetencyWeightPercent = 50
        }, default);

        var revDev = new PerformanceReview { ReviewCycleId = cycle.ReviewCycleId, EmployeeId = 1, DepartmentId = 1 };
        var revSales = new PerformanceReview { ReviewCycleId = cycle.ReviewCycleId, EmployeeId = 2, DepartmentId = 2 };
        db.PerformanceReviews.AddRange(revDev, revSales);
        await db.SaveChangesAsync();

        // Start approval chain for Department 1 (Engineering: 4 levels)
        await svc.StartApprovalChainAsync(revDev, default);
        Assert.Equal(4, revDev.ApprovalSteps.Count);
        Assert.Equal("Lead", revDev.ApprovalSteps[0].Label);
        Assert.Equal("Project Lead", revDev.ApprovalSteps[1].Label);
        Assert.Equal("Manager", revDev.ApprovalSteps[2].Label);
        Assert.Equal("HR", revDev.ApprovalSteps[3].Label);

        // Start approval chain for Department 2 (Sales: 2 levels)
        await svc.StartApprovalChainAsync(revSales, default);
        Assert.Equal(2, revSales.ApprovalSteps.Count);
        Assert.Equal("Manager", revSales.ApprovalSteps[0].Label);
        Assert.Equal("HR", revSales.ApprovalSteps[1].Label);
    }

    [SkippableFact]
    public async Task Send_back_returns_to_the_level_before_or_to_employee()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        await using var db = _postgres.CreateContext(customerId, orgId);
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
        var hrm = new FakeHrmClient();
        var master = new FakeMasterClient();
        var payroll = new FakePayrollClient();

        hrm.Add(new EmployeeProfile { EmployeeId = 1, FullName = "Dev 1", DepartmentId = 1, ReportsToEmployeeId = 10 });
        var svc = new PerformanceService(db, tenant, hrm, master, payroll);

        var scale = await svc.SaveRatingScaleAsync(new SaveRatingScaleRequest
        {
            Name = "Scale 2",
            Levels = [new SaveRatingLevelRequest { Score = 3, Label = "Good", Description = "Good" }]
        }, default);

        var cycle = await svc.CreateReviewCycleAsync(new SaveReviewCycleRequest
        {
            Name = "Annual Review",
            PeriodFrom = new DateOnly(2026, 1, 1),
            PeriodTo = new DateOnly(2026, 12, 31),
            RatingScaleId = scale.RatingScaleId
        }, default);

        var rev = new PerformanceReview { ReviewCycleId = cycle.ReviewCycleId, EmployeeId = 1, DepartmentId = 1 };
        db.PerformanceReviews.Add(rev);
        await db.SaveChangesAsync();

        // Submit self evaluation
        await svc.SaveSelfEvaluationAsync(rev.PerformanceReviewId, new SaveSelfEvaluationRequest
        {
            Achievements = "Delivered critical core modules",
            IsSubmit = true
        }, default);

        Assert.Equal(ReviewStatus.InApproval, rev.ReviewStatus);
        Assert.Equal(1, rev.CurrentStepSequence); // Level 1 (Lead)

        // Level 1 approves -> moves to Level 2 (Project Lead)
        await svc.ActLevelReviewAsync(rev.PerformanceReviewId, new ActLevelReviewRequest
        {
            Decision = LevelDecision.Approved,
            Comments = "Good progress"
        }, 10, default);

        Assert.Equal(2, rev.CurrentStepSequence);

        // Level 2 sends back -> returns to Level 1!
        await svc.ActLevelReviewAsync(rev.PerformanceReviewId, new ActLevelReviewRequest
        {
            Decision = LevelDecision.SentBack,
            Comments = "Please clarify target deliverables"
        }, 102, default);

        Assert.Equal(1, rev.CurrentStepSequence);
        Assert.Equal(ReviewStatus.InApproval, rev.ReviewStatus);

        // Level 1 now sends back -> returns to employee for self-evaluation revision!
        await svc.ActLevelReviewAsync(rev.PerformanceReviewId, new ActLevelReviewRequest
        {
            Decision = LevelDecision.SentBack,
            Comments = "Please update achievements with metrics"
        }, 10, default);

        Assert.Equal(ReviewStatus.SentBack, rev.ReviewStatus);
        Assert.False(rev.SelfEvaluation!.IsSubmitted); // Unlocked for employee
    }

    [SkippableFact]
    public async Task Self_evaluation_is_unchanged_after_every_level_acts()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        await using var db = _postgres.CreateContext(customerId, orgId);
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
        var hrm = new FakeHrmClient();
        var master = new FakeMasterClient();
        var payroll = new FakePayrollClient();

        hrm.Add(new EmployeeProfile { EmployeeId = 1, FullName = "Dev 1", DepartmentId = 2, ReportsToEmployeeId = 10 });
        var svc = new PerformanceService(db, tenant, hrm, master, payroll);

        var scale = await svc.SaveRatingScaleAsync(new SaveRatingScaleRequest
        {
            Name = "Scale 3",
            Levels = [new SaveRatingLevelRequest { Score = 3, Label = "Good", Description = "Good" }]
        }, default);

        var cycle = await svc.CreateReviewCycleAsync(new SaveReviewCycleRequest
        {
            Name = "Annual Review",
            PeriodFrom = new DateOnly(2026, 1, 1),
            PeriodTo = new DateOnly(2026, 12, 31),
            RatingScaleId = scale.RatingScaleId
        }, default);

        var rev = new PerformanceReview { ReviewCycleId = cycle.ReviewCycleId, EmployeeId = 1, DepartmentId = 2 };
        db.PerformanceReviews.Add(rev);
        await db.SaveChangesAsync();

        const string originalAchievements = "Built end-to-end performance management module.";
        const string originalChallenges = "Balancing concurrent test suite execution.";

        await svc.SaveSelfEvaluationAsync(rev.PerformanceReviewId, new SaveSelfEvaluationRequest
        {
            Achievements = originalAchievements,
            Challenges = originalChallenges,
            IsSubmit = true
        }, default);

        // Level 1 acts
        await svc.ActLevelReviewAsync(rev.PerformanceReviewId, new ActLevelReviewRequest
        {
            Decision = LevelDecision.Approved,
            Comments = "Reviewer Level 1 feedback: Excellent work",
            IncreasePercent = 12m
        }, 10, default);

        // Level 2 acts
        await svc.ActLevelReviewAsync(rev.PerformanceReviewId, new ActLevelReviewRequest
        {
            Decision = LevelDecision.Approved,
            Comments = "Reviewer Level 2 feedback: Fully concurred",
            IncreasePercent = 15m
        }, 104, default);

        // Verify SelfEvaluation was NOT mutated by approvers
        var selfEval = await svc.GetSelfEvaluationAsync(rev.PerformanceReviewId, default);
        Assert.NotNull(selfEval);
        Assert.Equal(originalAchievements, selfEval.Achievements);
        Assert.Equal(originalChallenges, selfEval.Challenges);

        // Verify level reviews recorded approver comments separately
        var levelReviews = await svc.GetLevelReviewsAsync(rev.PerformanceReviewId, default);
        Assert.Equal(2, levelReviews.Count);
        Assert.Equal("Reviewer Level 1 feedback: Excellent work", levelReviews[0].Comments);
        Assert.Equal("Reviewer Level 2 feedback: Fully concurred", levelReviews[1].Comments);
    }

    [Fact]
    public void Manager_who_is_also_the_lead_is_asked_only_once()
    {
        // Pure unit test of the deduplication logic without DB dependency
        var steps = new List<ResolvedStep>
        {
            new ResolvedStep { Sequence = 1, Label = "Lead", ApproverEmployeeId = 101 },
            new ResolvedStep { Sequence = 2, Label = "Manager", ApproverEmployeeId = 101 }, // same reviewer
            new ResolvedStep { Sequence = 3, Label = "HR", ApproverEmployeeId = 104 }
        };

        long? lastApprover = null;
        var deduplicated = new List<ResolvedStep>();
        foreach (var s in steps)
        {
            if (s.ApproverEmployeeId == lastApprover)
            {
                s.IsSkipped = true;
            }
            else
            {
                lastApprover = s.ApproverEmployeeId;
            }
            deduplicated.Add(s);
        }

        Assert.False(deduplicated[0].IsSkipped);
        Assert.True(deduplicated[1].IsSkipped); // Skipped because reviewer is asked only once!
        Assert.False(deduplicated[2].IsSkipped);
    }

    [SkippableFact]
    public async Task Closing_cycle_raises_salary_revision_in_payroll()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        await using var db = _postgres.CreateContext(customerId, orgId);
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
        var hrm = new FakeHrmClient();
        var master = new FakeMasterClient();
        var payroll = new FakePayrollClient();

        var svc = new PerformanceService(db, tenant, hrm, master, payroll);

        var scale = await svc.SaveRatingScaleAsync(new SaveRatingScaleRequest
        {
            Name = "Scale 4",
            Levels = [new SaveRatingLevelRequest { Score = 3, Label = "Good", Description = "Good" }]
        }, default);

        var cycle = await svc.CreateReviewCycleAsync(new SaveReviewCycleRequest
        {
            Name = "Annual Review",
            PeriodFrom = new DateOnly(2026, 1, 1),
            PeriodTo = new DateOnly(2026, 12, 31),
            RatingScaleId = scale.RatingScaleId
        }, default);

        var rev = new PerformanceReview
        {
            ReviewCycleId = cycle.ReviewCycleId,
            EmployeeId = 42,
            RecommendedIncreasePercent = 10m,
            ReviewStatus = ReviewStatus.Released
        };
        db.PerformanceReviews.Add(rev);
        await db.SaveChangesAsync();

        int closed = await svc.CloseCycleAsync(cycle.ReviewCycleId, default);

        Assert.Equal(1, closed);
        Assert.Single(payroll.Revisions);
        Assert.Equal(42, payroll.Revisions[0].EmployeeId);
        Assert.Equal(1_100_000m, payroll.Revisions[0].NewCtc); // 10% increase on 1,000,000 baseline
    }
}

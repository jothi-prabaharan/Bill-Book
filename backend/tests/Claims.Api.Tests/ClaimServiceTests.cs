using Claims.Api.Services;
using Claims.Entity.Enums;
using Claims.Entity.Models;
using Claims.Entity.TableEntities;
using Claims.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Employees;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Claims.Api.Tests;

[Collection(nameof(PostgresCollection))]
public sealed class ClaimServiceTests
{
    private readonly PostgresFixture _postgres;

    public ClaimServiceTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed class MockEmployeeClient : IEmployeeClient
    {
        public EmployeeProfile? Profile { get; set; }

        public Task<EmployeeProfile?> FindByUserIdAsync(Guid customerId, Guid orgId, Guid userId, CancellationToken ct) =>
            Task.FromResult(Profile);

        public Task<EmployeeProfile?> FindByIdAsync(Guid customerId, Guid orgId, long employeeId, CancellationToken ct) =>
            Task.FromResult(Profile);

        public Task<List<EmployeeProfile>> LookupAsync(Guid customerId, Guid orgId, List<long> employeeIds, CancellationToken ct) =>
            Task.FromResult(Profile is not null ? new List<EmployeeProfile> { Profile } : new List<EmployeeProfile>());
    }

    private sealed class MockAccountingClient : IAccountingClient
    {
        public bool ShouldSucceed { get; set; } = true;
        public PostLedgerRequest? LastRequest { get; private set; }

        public Task<PostLedgerOutcomeResult> PostLedgerAsync(PostLedgerRequest request, CancellationToken ct)
        {
            LastRequest = request;
            return Task.FromResult(new PostLedgerOutcomeResult(ShouldSucceed, ShouldSucceed ? null : "Ledger error"));
        }
    }

    private sealed class MockPayrollClient : IPayrollClient
    {
        public bool Attached { get; private set; }

        public Task<bool> AttachToPayrollRunAsync(long payrollRunId, long expenseClaimId, decimal amount, CancellationToken ct)
        {
            Attached = true;
            return Task.FromResult(true);
        }
    }

    private sealed class MockNumberGenerator : INumberGenerator
    {
        private long _next = 1;

        public Task<NumberAllocation> NextAsync(string seriesCode, DateOnly onDate, CancellationToken ct) =>
            Task.FromResult(new NumberAllocation(1, _next++, $"CLM-2026-{_next:D4}"));

        public Task<string?> PeekAsync(string seriesCode, DateOnly onDate, CancellationToken ct) =>
            Task.FromResult<string?>($"CLM-2026-{_next:D4}");
    }

    [SkippableFact]
    public async Task Create_claim_creates_draft_with_lines_and_generated_number()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid() };
        await using var db = _postgres.CreateContext(tenant.CustomerId.Value, tenant.OrgId.Value);
        var hrm = new MockEmployeeClient();
        var acc = new MockAccountingClient();
        var pay = new MockPayrollClient();
        var num = new MockNumberGenerator();

        var service = new ClaimService(db, tenant, hrm, acc, pay, num);

        var cat = await service.CreateCategoryAsync(new CreateClaimCategoryRequest
        {
            Code = $"TRV_{Guid.NewGuid():N}"[..8],
            Name = "Travel",
            IsReceiptRequired = true
        }, default);

        var req = new CreateExpenseClaimRequest
        {
            EmployeeId = 101,
            ClaimDate = new DateOnly(2026, 9, 25),
            PayoutMode = PayoutMode.Direct,
            Lines = new List<SaveExpenseClaimLineRequest>
            {
                new()
                {
                    ClaimCategoryId = cat.ClaimCategoryId,
                    ExpenseDate = new DateOnly(2026, 9, 25),
                    Description = "Taxi to client office",
                    Amount = 450,
                    ReceiptAttachmentKey = "receipt-001.pdf"
                }
            }
        };

        var claim = await service.CreateClaimAsync(req, default);

        Assert.NotNull(claim);
        Assert.Equal(ClaimStatus.Draft, claim.ClaimStatus);
        Assert.Equal(450, claim.TotalAmount);
        Assert.StartsWith("CLM-2026-", claim.ClaimNo);
        Assert.Single(claim.Lines);
    }

    [SkippableFact]
    public async Task Submit_refuses_when_receipt_is_missing_for_required_category()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid() };
        await using var db = _postgres.CreateContext(tenant.CustomerId.Value, tenant.OrgId.Value);
        var hrm = new MockEmployeeClient();
        var acc = new MockAccountingClient();
        var pay = new MockPayrollClient();
        var num = new MockNumberGenerator();

        var service = new ClaimService(db, tenant, hrm, acc, pay, num);

        var cat = await service.CreateCategoryAsync(new CreateClaimCategoryRequest
        {
            Code = $"HTL_{Guid.NewGuid():N}"[..8],
            Name = "Hotel Stay",
            IsReceiptRequired = true
        }, default);

        var claim = await service.CreateClaimAsync(new CreateExpenseClaimRequest
        {
            EmployeeId = 101,
            ClaimDate = new DateOnly(2026, 9, 25),
            Lines = new List<SaveExpenseClaimLineRequest>
            {
                new()
                {
                    ClaimCategoryId = cat.ClaimCategoryId,
                    ExpenseDate = new DateOnly(2026, 9, 25),
                    Description = "Overnight stay",
                    Amount = 2500,
                    ReceiptAttachmentKey = null // Missing receipt!
                }
            }
        }, default);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SubmitClaimAsync(claim.ExpenseClaimId, default));
        Assert.Contains("Receipt is required", ex.Message);
    }

    [SkippableFact]
    public async Task Submit_refuses_when_amount_exceeds_category_limit()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid() };
        await using var db = _postgres.CreateContext(tenant.CustomerId.Value, tenant.OrgId.Value);
        var hrm = new MockEmployeeClient
        {
            Profile = new EmployeeProfile
            {
                EmployeeId = 101,
                EmployeeCode = "EMP001",
                FullName = "Alice Smith",
                GradeId = 1
            }
        };
        var acc = new MockAccountingClient();
        var pay = new MockPayrollClient();
        var num = new MockNumberGenerator();

        var service = new ClaimService(db, tenant, hrm, acc, pay, num);

        var cat = await service.CreateCategoryAsync(new CreateClaimCategoryRequest
        {
            Code = $"MEL_{Guid.NewGuid():N}"[..8],
            Name = "Food & Meals",
            IsReceiptRequired = false
        }, default);

        await service.SaveLimitAsync(new SaveClaimLimitRequest
        {
            ClaimCategoryId = cat.ClaimCategoryId,
            GradeId = 1,
            LimitPeriod = LimitPeriod.PerClaim,
            Amount = 500
        }, default);

        var claim = await service.CreateClaimAsync(new CreateExpenseClaimRequest
        {
            EmployeeId = 101,
            ClaimDate = new DateOnly(2026, 9, 25),
            Lines = new List<SaveExpenseClaimLineRequest>
            {
                new()
                {
                    ClaimCategoryId = cat.ClaimCategoryId,
                    ExpenseDate = new DateOnly(2026, 9, 25),
                    Description = "Team lunch",
                    Amount = 750 // Exceeds limit 500!
                }
            }
        }, default);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SubmitClaimAsync(claim.ExpenseClaimId, default));
        Assert.Contains("exceeds the Per-Claim limit", ex.Message);
    }

    [SkippableFact]
    public async Task Approval_progression_and_direct_payout_posts_balanced_ledger()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid() };
        await using var db = _postgres.CreateContext(tenant.CustomerId.Value, tenant.OrgId.Value);
        var hrm = new MockEmployeeClient
        {
            Profile = new EmployeeProfile
            {
                EmployeeId = 101,
                EmployeeCode = "EMP001",
                FullName = "Bob Jones",
                ReportsToEmployeeId = 202
            }
        };
        var acc = new MockAccountingClient();
        var pay = new MockPayrollClient();
        var num = new MockNumberGenerator();

        var service = new ClaimService(db, tenant, hrm, acc, pay, num);

        var cat = await service.CreateCategoryAsync(new CreateClaimCategoryRequest
        {
            Code = $"TAX_{Guid.NewGuid():N}"[..8],
            Name = "Taxi Fare",
            IsReceiptRequired = false,
            LedgerAccountId = 501
        }, default);

        // Seed workflow
        var wf = new ApprovalWorkflow
        {
            CustomerId = tenant.CustomerId.Value,
            OrgId = tenant.OrgId.Value,
            Name = "Claim Flow",
            RequestKind = ClaimRequestKind.Claim,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            IsActive = true,
            Levels = new List<ApprovalWorkflowLevel>
            {
                new()
                {
                    CustomerId = tenant.CustomerId.Value,
                    OrgId = tenant.OrgId.Value,
                    Sequence = 1,
                    Label = "Manager",
                    ApproverKind = ApproverKind.ReportingChain
                }
            }
        };
        db.ApprovalWorkflows.Add(wf);
        await db.SaveChangesAsync();

        var claim = await service.CreateClaimAsync(new CreateExpenseClaimRequest
        {
            EmployeeId = 101,
            ClaimDate = new DateOnly(2026, 9, 25),
            PayoutMode = PayoutMode.Direct,
            Lines = new List<SaveExpenseClaimLineRequest>
            {
                new()
                {
                    ClaimCategoryId = cat.ClaimCategoryId,
                    ExpenseDate = new DateOnly(2026, 9, 25),
                    Description = "Cab ride",
                    Amount = 300
                }
            }
        }, default);

        // Submit
        var submitted = await service.SubmitClaimAsync(claim.ExpenseClaimId, default);
        Assert.Equal(ClaimStatus.Submitted, submitted.ClaimStatus);
        Assert.Equal(ApprovalStatus.InApproval, submitted.ApprovalStatus);

        // Approve
        var approved = await service.ActApprovalAsync(claim.ExpenseClaimId, new ActClaimApprovalRequest
        {
            Action = "Approve",
            Comments = "Looks good"
        }, Guid.NewGuid(), default);

        Assert.Equal(ClaimStatus.Approved, approved.ClaimStatus);
        Assert.Equal(ApprovalStatus.Approved, approved.ApprovalStatus);
        Assert.Equal(300, approved.ApprovedAmount);

        // Payout Direct
        var paid = await service.PayoutClaimAsync(claim.ExpenseClaimId, new PayoutClaimRequest
        {
            PayoutMode = PayoutMode.Direct,
            BankAccountId = 901
        }, default);

        Assert.Equal(ClaimStatus.Paid, paid.ClaimStatus);
        Assert.NotNull(acc.LastRequest);
        Assert.Equal(300, acc.LastRequest.Legs.Where(l => l.Debit > 0).Sum(l => l.Debit));
        Assert.Equal(300, acc.LastRequest.Legs.Where(l => l.Credit > 0).Sum(l => l.Credit));
    }
}

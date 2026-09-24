using Microsoft.EntityFrameworkCore;
using Payroll.Api.Services;
using Payroll.Entity.Enums;
using Payroll.Entity.Models;
using Payroll.Entity.TableEntities;
using Payroll.Repository;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Payroll.Api.Tests;

[Collection(nameof(PostgresCollection))]
public sealed class PayrollServiceTests
{
    private readonly PostgresFixture _postgres;

    public PayrollServiceTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed class FakeLedgerClient : ILedgerClient
    {
        public List<PostLedgerRequest> PostedRequests { get; } = [];

        public Task<PostLedgerOutcomeResult> PostAsync(PostLedgerRequest request, CancellationToken ct)
        {
            PostedRequests.Add(request);
            return Task.FromResult(new PostLedgerOutcomeResult(true, null));
        }
    }

    [SkippableFact]
    public async Task A_run_posts_one_balanced_journal_and_salary_payable_ties_to_unpaid_net()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
        await using PayrollDbContext db = _postgres.CreateContext(customerId, orgId);
        var ledger = new FakeLedgerClient();

        var setup = new SalarySetupService(db);
        var runService = new PayrollRunService(db, tenant, ledger);

        // 1. Setup Component and Structure
        long compId = await setup.SaveComponentAsync(null, new SaveSalaryComponentRequest
        {
            Name = "Basic Salary",
            Kind = ComponentKind.Earning,
            ValueType = SalaryValueType.FlatAmount
        }, default);

        long structId = await setup.SaveStructureAsync(null, new SaveSalaryStructureRequest
        {
            Name = "Standard Structure",
            Components =
            [
                new SaveSalaryStructureComponentRequest
                {
                    SalaryComponentId = compId,
                    ValueType = SalaryValueType.FlatAmount,
                    FlatAmount = 50000m
                }
            ]
        }, default);

        // 2. Assign Salary to Employee
        long employeeId = 101;
        await setup.AssignEmployeeSalaryAsync(new SaveEmployeeSalaryRequest
        {
            EmployeeId = employeeId,
            SalaryStructureId = structId,
            AnnualCtc = 600000m,
            EffectiveFrom = new DateOnly(2026, 1, 1)
        }, default);

        // 3. Process Run for Month
        DateOnly month = new DateOnly(2026, 9, 1);
        long runId = await runService.ProcessRunAsync(month, default);
        Assert.True(runId > 0);

        var run = await runService.GetRunAsync(runId, default);
        Assert.NotNull(run);
        Assert.Equal(PayrollRunStatus.Processed, run.Status);
        Assert.Equal(50000m, run.TotalNetPay);

        // 4. Approve and Post Run
        await runService.ApproveRunAsync(runId, default);
        await runService.PostRunAsync(runId, default);

        // 5. Verify Ledger Posting
        Assert.Single(ledger.PostedRequests);
        PostLedgerRequest post = ledger.PostedRequests[0];
        Assert.Equal("PAY", post.TransactionTypeCode);
        Assert.Equal(runId, post.TransactionId);

        decimal totalDebits = post.Legs.Sum(l => l.DebitAmount);
        decimal totalCredits = post.Legs.Sum(l => l.CreditAmount);

        // One balanced journal: Dr = Cr
        Assert.Equal(totalDebits, totalCredits);
        Assert.Equal(50000m, totalDebits);

        // Salary Payable ties to unpaid net
        LedgerLegRequest salaryPayableLeg = post.Legs.Single(l => l.AccountSystemName == "Salary Payable");
        Assert.Equal(run.TotalNetPay, salaryPayableLeg.CreditAmount);

        // 6. Mark Paid
        await runService.MarkPaidAsync(runId, default);
        var markedPaidRun = await runService.GetRunAsync(runId, default);
        Assert.Equal(PayrollRunStatus.MarkedPaid, markedPaidRun!.Status);
    }

    [SkippableFact]
    public async Task Back_dated_revision_pays_arrears_in_next_run()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
        await using PayrollDbContext db = _postgres.CreateContext(customerId, orgId);
        var ledger = new FakeLedgerClient();

        var setup = new SalarySetupService(db);
        var runService = new PayrollRunService(db, tenant, ledger);

        long compId = await setup.SaveComponentAsync(null, new SaveSalaryComponentRequest
        {
            Name = "Basic Salary",
            Kind = ComponentKind.Earning,
            ValueType = SalaryValueType.FlatAmount
        }, default);

        long structId = await setup.SaveStructureAsync(null, new SaveSalaryStructureRequest
        {
            Name = "Standard Structure",
            Components =
            [
                new SaveSalaryStructureComponentRequest
                {
                    SalaryComponentId = compId,
                    ValueType = SalaryValueType.FlatAmount,
                    FlatAmount = 50000m
                }
            ]
        }, default);

        long employeeId = 102;
        await setup.AssignEmployeeSalaryAsync(new SaveEmployeeSalaryRequest
        {
            EmployeeId = employeeId,
            SalaryStructureId = structId,
            AnnualCtc = 600000m,
            EffectiveFrom = new DateOnly(2026, 1, 1)
        }, default);

        // Backdated revision effective from August (1 month back)
        await setup.ReviseSalaryAsync(new SaveSalaryRevisionRequest
        {
            EmployeeId = employeeId,
            NewCtc = 720000m,
            EffectiveFrom = new DateOnly(2026, 8, 1),
            Reason = "Promotion with backdated increment"
        }, default);

        // Run for September
        DateOnly september = new DateOnly(2026, 9, 1);
        long runId = await runService.ProcessRunAsync(september, default);

        var payslips = await runService.GetPayslipsAsync(runId, default);
        Assert.Single(payslips);
        var p = payslips[0];

        // Should include regular salary (50,000) + arrears (10,000) = 60,000
        Assert.Contains(p.Lines, l => l.ComponentName == "Salary Revision Arrears" && l.Amount == 10000m);
        Assert.Equal(60000m, p.NetPay);

        // Post the run
        await runService.ApproveRunAsync(runId, default);
        await runService.PostRunAsync(runId, default);

        // Verify arrears marked processed
        SalaryRevision rev = await db.SalaryRevisions.SingleAsync(r => r.EmployeeId == employeeId);
        Assert.True(rev.ArrearsProcessed);
    }

    [SkippableFact]
    public async Task Reversal_restores_posted_journal()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
        await using PayrollDbContext db = _postgres.CreateContext(customerId, orgId);
        var ledger = new FakeLedgerClient();

        var setup = new SalarySetupService(db);
        var runService = new PayrollRunService(db, tenant, ledger);

        long compId = await setup.SaveComponentAsync(null, new SaveSalaryComponentRequest
        {
            Name = "Basic",
            Kind = ComponentKind.Earning,
            ValueType = SalaryValueType.FlatAmount
        }, default);

        long structId = await setup.SaveStructureAsync(null, new SaveSalaryStructureRequest
        {
            Name = "Base",
            Components = [new SaveSalaryStructureComponentRequest { SalaryComponentId = compId, FlatAmount = 30000m }]
        }, default);

        await setup.AssignEmployeeSalaryAsync(new SaveEmployeeSalaryRequest
        {
            EmployeeId = 103,
            SalaryStructureId = structId,
            AnnualCtc = 360000m,
            EffectiveFrom = new DateOnly(2026, 1, 1)
        }, default);

        long runId = await runService.ProcessRunAsync(new DateOnly(2026, 9, 1), default);
        await runService.ApproveRunAsync(runId, default);
        await runService.PostRunAsync(runId, default);

        Assert.Single(ledger.PostedRequests);

        // Reverse
        await runService.ReverseRunAsync(runId, default);

        var run = await runService.GetRunAsync(runId, default);
        Assert.Equal(PayrollRunStatus.Reversed, run!.Status);

        // Ledger should have received a reversal / withdrawal request
        Assert.Equal(2, ledger.PostedRequests.Count);
        PostLedgerRequest withdrawal = ledger.PostedRequests[1];
        Assert.NotEmpty(withdrawal.WithdrawLedgerTypeIds);
    }
}

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

    [SkippableFact]
    public async Task Ecr_file_matches_the_posted_payslips_to_the_rupee()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
        await using PayrollDbContext db = _postgres.CreateContext(customerId, orgId);
        var ledger = new FakeLedgerClient();

        var setup = new SalarySetupService(db);
        var runService = new PayrollRunService(db, tenant, ledger);
        var statutory = new StatutoryService(db);

        // Configure PF setting
        await statutory.SavePfSettingAsync(new SavePfSettingRequest
        {
            EffectiveFrom = new DateOnly(2020, 1, 1),
            EmployeeContributionRate = 12.0m,
            EmployerContributionRate = 12.0m,
            WageCeiling = 15000.0m,
            RestrictToWageCeiling = true
        }, default);

        long compId = await setup.SaveComponentAsync(null, new SaveSalaryComponentRequest
        {
            Name = "Basic",
            Kind = ComponentKind.Earning,
            ValueType = SalaryValueType.FlatAmount
        }, default);

        long structId = await setup.SaveStructureAsync(null, new SaveSalaryStructureRequest
        {
            Name = "Structure 1",
            Components = [new SaveSalaryStructureComponentRequest { SalaryComponentId = compId, FlatAmount = 15000m }]
        }, default);

        long employeeId = 555;
        await setup.AssignEmployeeSalaryAsync(new SaveEmployeeSalaryRequest
        {
            EmployeeId = employeeId,
            SalaryStructureId = structId,
            AnnualCtc = 180000m,
            EffectiveFrom = new DateOnly(2026, 1, 1)
        }, default);

        long runId = await runService.ProcessRunAsync(new DateOnly(2026, 9, 1), default);
        await runService.ApproveRunAsync(runId, default);
        await runService.PostRunAsync(runId, default);

        // Generate ECR
        string ecr = await statutory.GeneratePfEcrAsync(runId, default);
        Assert.NotEmpty(ecr);

        // Verify ECR line format and figures match payslip
        // Format: #~#UAN#~#MEMBER_NAME#~#GROSS_WAGES#~#EPF_WAGES#~#EPS_WAGES#~#EDLI_WAGES#~#EE_SHARE#~#EPS_SHARE#~#ER_SHARE_DIFF#~#NCP_DAYS#~#REFUND
        string[] parts = ecr.Trim().Split("#~#", StringSplitOptions.RemoveEmptyEntries);
        Assert.True(parts.Length >= 10);

        decimal grossWages = decimal.Parse(parts[2]);
        decimal epfWages = decimal.Parse(parts[3]);
        decimal eeShare = decimal.Parse(parts[6]);

        Assert.Equal(15000m, grossWages);
        Assert.Equal(15000m, epfWages);
        Assert.Equal(1800m, eeShare); // 12% of 15000 = 1800
    }

    [SkippableFact]
    public async Task A_mid_year_joiner_with_income_from_a_previous_employer_is_taxed_the_same_by_a_monthly_run_and_by_the_year_end_recomputation()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
        await using PayrollDbContext db = _postgres.CreateContext(customerId, orgId);

        var taxService = new TaxCalculationService(db);
        var seeder = new PayrollSeeder(db);
        await seeder.SeedForOrganizationAsync(orgId, default);

        long employeeId = 999;
        string fy = "2026-2027";

        // Previous employer income
        await taxService.SavePreviousEmployerIncomeAsync(new SavePreviousEmployerIncomeRequest
        {
            EmployeeId = employeeId,
            FinancialYear = fy,
            GrossIncome = 500000m,
            TotalTdsDeducted = 15000m,
            EmployerName = "Prior Tech Corp"
        }, default);

        // Current employer: 5,00,000 over 6 remaining months
        decimal currentGross = 500000m;
        int remainingMonths = 6;

        // 1. Monthly projection
        var monthlyProjection = await taxService.ComputeTaxAsync(employeeId, fy, currentGross, remainingMonths, default);
        decimal monthlyTds = monthlyProjection.MonthlyTds;
        decimal totalMonthlyTdsOverRemainingPeriod = monthlyTds * remainingMonths;

        // 2. Year-end recomputation (remainingMonths = 0, full year actuals)
        var yearEndRecomputation = await taxService.ComputeTaxAsync(employeeId, fy, currentGross, remainingMonths: 0, default);

        // Both must arrive at the exact same net tax payable
        Assert.Equal(yearEndRecomputation.NetTaxPayable, totalMonthlyTdsOverRemainingPeriod);
        Assert.Equal(yearEndRecomputation.TotalAnnualTax, monthlyProjection.TotalAnnualTax);
        Assert.Equal(15000m, yearEndRecomputation.PreviousEmployerTds);
    }

    private sealed class FakeHrmClient : IHrmClient
    {
        public List<(long EmployeeId, DateOnly Lwd)> SettledEmployees { get; } = [];

        public Task SettleEmployeeAsync(long employeeId, DateOnly lastWorkingDate, CancellationToken ct)
        {
            SettledEmployees.Add((employeeId, lastWorkingDate));
            return Task.CompletedTask;
        }

        public Task<Shared.Kernel.Employees.EmployeeProfile?> FindByUserIdAsync(Guid customerId, Guid orgId, Guid userId, CancellationToken ct) => Task.FromResult<Shared.Kernel.Employees.EmployeeProfile?>(null);

        public Task<Shared.Kernel.Employees.EmployeeProfile?> FindByIdAsync(Guid customerId, Guid orgId, long employeeId, CancellationToken ct) => Task.FromResult<Shared.Kernel.Employees.EmployeeProfile?>(null);
    }

    private sealed class FakeMasterUserClient : IMasterUserClient
    {
        public List<Guid> DeactivatedUsers { get; } = [];

        public Task DeactivateUserAsync(Guid userId, CancellationToken ct)
        {
            DeactivatedUsers.Add(userId);
            return Task.CompletedTask;
        }
    }

    [SkippableFact]
    public async Task Settling_an_exit_pays_through_a_full_and_final_run_and_the_employees_login_stops_working()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
        await using PayrollDbContext db = _postgres.CreateContext(customerId, orgId);

        var ledger = new FakeLedgerClient();
        var hrm = new FakeHrmClient();
        var master = new FakeMasterUserClient();
        var setup = new SalarySetupService(db);
        var fnfService = new FnfSettlementService(db, tenant, ledger, hrm, master);

        // 1. Setup Employee Salary
        long compId = await setup.SaveComponentAsync(null, new SaveSalaryComponentRequest
        {
            Name = "Basic Salary",
            Kind = ComponentKind.Earning,
            ValueType = SalaryValueType.FlatAmount
        }, default);

        long structId = await setup.SaveStructureAsync(null, new SaveSalaryStructureRequest
        {
            Name = "Standard Structure",
            Components = [new SaveSalaryStructureComponentRequest { SalaryComponentId = compId, FlatAmount = 60000m }]
        }, default);

        long employeeId = 777;
        await setup.AssignEmployeeSalaryAsync(new SaveEmployeeSalaryRequest
        {
            EmployeeId = employeeId,
            SalaryStructureId = structId,
            AnnualCtc = 720000m,
            EffectiveFrom = new DateOnly(2025, 1, 1)
        }, default);

        // 2. Calculate and create F&F settlement for exit on 15th Sep
        DateOnly lwd = new DateOnly(2026, 9, 15);
        long settlementId = await fnfService.CalculateAndCreateSettlementAsync(new CreateFnfSettlementRequest
        {
            EmployeeId = employeeId,
            LastWorkingDate = lwd,
            LinkedUserId = userId,
            CompletedYearsOfService = 5,
            NoticeShortfallDays = 0,
            Remarks = "Exit clearance complete"
        }, default);

        var settlement = await fnfService.GetSettlementAsync(settlementId, default);
        Assert.NotNull(settlement);
        Assert.Equal(FnfStatus.Draft, settlement.Status);
        Assert.True(settlement.NetPayable > 0m);

        // 3. Approve and Post settlement
        await fnfService.ApproveSettlementAsync(settlementId, default);
        long runId = await fnfService.PostSettlementAsync(settlementId, userId, default);
        Assert.True(runId > 0);

        // 4. Verify PayrollRun created of kind FullAndFinal
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.PayrollRunId == runId);
        Assert.NotNull(run);
        Assert.Equal(PayrollRunKind.FullAndFinal, run.Kind);
        Assert.Equal(PayrollRunStatus.Posted, run.Status);
        Assert.Equal(settlement.NetPayable, run.TotalNetPay);

        // 5. Verify ledger posting
        Assert.Single(ledger.PostedRequests);
        var post = ledger.PostedRequests[0];
        Assert.Equal("PAY", post.TransactionTypeCode);
        Assert.Equal(runId, post.TransactionId);

        // 6. Verify HRMS settlement notified and linked login deactivated
        Assert.Contains((employeeId, lwd), hrm.SettledEmployees);
        Assert.Contains(userId, master.DeactivatedUsers);
    }
}

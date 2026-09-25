using Fee.Api.Services;
using Fee.Entity.Enums;
using Fee.Entity.Models;
using Fee.Entity.TableEntities;
using Fee.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shared.Kernel.Contacts;
using Shared.Kernel.Ledgers;
using Shared.Kernel.Numbering;
using Shared.Kernel.School;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Fee.Api.Tests;

/// <summary>The ledger, recorded: every posting Fee sends, replacing by document as Accounting does.</summary>
internal sealed class RecordingLedger : IFeeLedger
{
    public Dictionary<(string Type, long Id), LedgerPosting> Postings { get; } = [];

    public bool Refuse { get; set; }

    public Task<LedgerOutcome> PostAsync(LedgerPosting posting, CancellationToken ct)
    {
        if (Refuse)
        {
            return Task.FromResult(new LedgerOutcome(false, "422: refused"));
        }

        if (posting.Legs.Count == 0)
        {
            Postings.Remove((posting.TransactionTypeCode, posting.TransactionId));
        }
        else
        {
            Postings[(posting.TransactionTypeCode, posting.TransactionId)] = posting;
        }

        return Task.FromResult(new LedgerOutcome(true, null));
    }

    /// <summary>The guardian's receivable, one purpose of it: debits less credits.</summary>
    public decimal Receivable(long contactId, int purpose) =>
        Postings.Values.SelectMany(p => p.Legs)
            .Where(l => l.AccountSystemName == FeeRules.AccountsReceivable && l.SubAccountReferenceId == contactId && l.SubAccountPurpose == purpose)
            .Sum(l => l.DebitAmount - l.CreditAmount);
}

internal sealed class FakeSchool : ISisClient
{
    public List<EnrolmentInfo> Enrolments { get; } =
    [
        new() { EnrolmentId = 1, StudentId = 11, StudentName = "Arun", AdmissionNo = "ADM-1", AcademicYearId = 1, SchoolClassId = 6, SectionId = 60, ClassName = "VI", SectionName = "A", PrimaryGuardianContactId = 501, IsActive = true },
        new() { EnrolmentId = 2, StudentId = 12, StudentName = "Meera", AdmissionNo = "ADM-2", AcademicYearId = 1, SchoolClassId = 6, SectionId = 60, ClassName = "VI", SectionName = "A", PrimaryGuardianContactId = 502, IsActive = true },
        new() { EnrolmentId = 3, StudentId = 13, StudentName = "Priya", AdmissionNo = "ADM-3", AcademicYearId = 1, SchoolClassId = 6, SectionId = 60, ClassName = "VI", SectionName = "A", PrimaryGuardianContactId = null, IsActive = true },
        new() { EnrolmentId = 4, StudentId = 14, StudentName = "Ravi", AdmissionNo = "ADM-4", AcademicYearId = 1, SchoolClassId = 6, SectionId = 60, ClassName = "VI", SectionName = "A", PrimaryGuardianContactId = 503, IsActive = false },
    ];

    public Task<IReadOnlyList<EnrolmentInfo>> EnrolmentsAsync(EnrolmentQueryRequest query, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<EnrolmentInfo>>(Enrolments);

    public Task<AcademicCheckResponse> CheckAsync(long? academicYearId, long? schoolClassId, long? sectionId, CancellationToken ct) =>
        Task.FromResult(new AcademicCheckResponse { YearExists = true, ClassExists = true });

    public Task<SectionRollResponse> RollAsync(long sectionId, CancellationToken ct) => throw new NotSupportedException();

    public Task<AdmitStudentResponse> AdmitAsync(AdmitStudentRequest request, CancellationToken ct) => throw new NotSupportedException();
}

internal sealed class FakeGuardians : IContactDirectory
{
    public Task<IReadOnlyDictionary<long, ContactSummary>> FindAsync(IEnumerable<long> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<long, ContactSummary>>(ids.Where(id => id is 501 or 502).ToDictionary(
            id => id, id => new ContactSummary { ContactId = id, ContactCode = $"C{id}", DisplayName = $"Guardian {id}", IsGuardian = true, IsActive = true }));

    public Task<EnsureGuardianResponse> EnsureGuardianAsync(string displayName, string mobileNumber, string? email, CancellationToken ct) =>
        throw new NotSupportedException();
}

internal sealed class FakeAccounts : IAccountDirectory
{
    public Task<IReadOnlyList<AccountSummary>> AccountsAsync(IEnumerable<int> accountTypeIds, IEnumerable<long> accountIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AccountSummary>>(
        [
            new() { AccountId = 41, AccountCode = "4300", AccountName = "Fee Income", AccountTypeId = 4, IsActive = true },
            new() { AccountId = 24, AccountCode = "2400", AccountName = "Refundable Deposits", AccountTypeId = 2, IsActive = true },
        ]);

    public Task<IReadOnlyList<BankAccountSummary>> BankAccountsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<BankAccountSummary>>([new() { BankAccountId = 7, AccountName = "Cash", MaskedNumber = "0001", IsCash = true }]);
}

internal sealed class Rupees : IBaseCurrencyProvider
{
    public Task<string?> GetBaseCurrencyAsync(CancellationToken ct = default) => Task.FromResult<string?>("INR");
}

internal sealed class AprilYear : IFinancialYearProvider
{
    public Task<int> GetStartMonthAsync(CancellationToken ct = default) => Task.FromResult(4);
}

/// <summary>
/// Fees against a real database (S4, TK-64), with Sis, Master and Accounting
/// faked. The card's Done-when: a demand and its receipt post balanced
/// journals, and the guardian's receivable ties to the open demands.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class FeeServiceTests
{
    private readonly PostgresFixture _postgres;

    public FeeServiceTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed record Branch(FeeDbContext Db, TenantContext Tenant, RecordingLedger Ledger, FakeSchool School, long Structure, long Tuition, long Exam);

    private async Task<Branch> NewBranchAsync()
    {
        var tenant = new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid() };
        FeeDbContext db = _postgres.CreateContext(tenant.CustomerId!.Value, tenant.OrgId!.Value);
        await new FeeSeeder(db).SeedForOrganizationAsync(tenant.OrgId.Value, default);

        long tuition = await db.FeeHeads.Where(h => h.Code == "TUITION").Select(h => h.FeeHeadId).SingleAsync();
        long exam = await db.FeeHeads.Where(h => h.Code == "EXAM").Select(h => h.FeeHeadId).SingleAsync();
        var school = new FakeSchool();
        var setup = new FeeSetupService(db, new FakeAccounts(), school, NullLogger<FeeSetupService>.Instance);
        long structure = (await setup.SaveStructureAsync(null, new SaveFeeStructureRequest
        {
            AcademicYearId = 1,
            SchoolClassId = 6,
            Name = "Day scholar",
            FirstMonth = 6,
            Lines =
            [
                new() { FeeHeadId = tuition, Amount = 2500m, Frequency = FeeFrequency.Monthly, DueDay = 10 },
                new() { FeeHeadId = exam, Amount = 600m, Frequency = FeeFrequency.Termly, DueDay = 15 },
            ],
        }, default)).Id!.Value;

        return new Branch(db, tenant, new RecordingLedger(), school, structure, tuition, exam);
    }

    private static NumberGenerator Numbers(FeeDbContext db) => new(db, Options.Create(new NumberingOptions()), new AprilYear());

    private static DemandService Demands(Branch b) =>
        new(b.Db, Numbers(b.Db), b.School, b.Ledger, new Rupees(), b.Tenant, NullLogger<DemandService>.Instance);

    private static ReceiptService Receipts(Branch b) =>
        new(b.Db, Numbers(b.Db), new FakeGuardians(), new FakeAccounts(), b.Ledger, new Rupees(), b.Tenant, NullLogger<ReceiptService>.Instance);

    private static GenerateDemandsRequest June(Branch b) =>
        new() { FeeStructureId = b.Structure, PeriodKey = "2026-06", DemandDate = new DateOnly(2026, 6, 1) };

    [SkippableFact]
    public async Task A_demand_and_its_receipt_post_balanced_and_the_receivable_ties_to_the_open_demands()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using FeeDbContext _ = b.Db;

        await Demands(b).GenerateAsync(June(b), default);
        Assert.Equal(FeeOutcome.Ok, (await Demands(b).PostAsync(new PostDemandsRequest { FeeStructureId = b.Structure, PeriodKey = "2026-06" }, default)).Outcome);

        // June is the first month: tuition and the first term's exam fee.
        FeeDemand arun = await b.Db.FeeDemands.Include(d => d.Lines).SingleAsync(d => d.ContactId == 501);
        Assert.Equal(3100m, arun.NetAmount);
        Assert.StartsWith("FDM/", arun.DemandNo);

        // A part payment.
        FeeResult receipt = await Receipts(b).CreateAsync(new SaveReceiptRequest
        {
            ContactId = 501, ReceiptDate = new DateOnly(2026, 6, 5), BankAccountId = 7, Amount = 2000m,
        }, default);
        Assert.Equal(FeeOutcome.Ok, receipt.Outcome);

        Assert.All(b.Ledger.Postings.Values, p => Assert.True(FeeRules.Balances(p.Legs), $"{p.TransactionTypeCode} {p.TransactionId} does not balance."));

        decimal open = await b.Db.FeeDemands.Where(d => d.ContactId == 501 && d.DocumentStatus == FeeDocumentStatus.Posted)
            .SumAsync(d => d.NetAmount - d.PaidAmount);
        Assert.Equal(1100m, open);
        Assert.Equal(open, b.Ledger.Receivable(501, FeeRules.TradePurpose));
    }

    [SkippableFact]
    public async Task An_overpayment_is_held_as_an_advance_and_the_receivable_still_ties()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using FeeDbContext _ = b.Db;

        await Demands(b).GenerateAsync(June(b), default);
        await Demands(b).PostAsync(new PostDemandsRequest { FeeStructureId = b.Structure, PeriodKey = "2026-06" }, default);

        await Receipts(b).CreateAsync(new SaveReceiptRequest { ContactId = 501, ReceiptDate = new DateOnly(2026, 6, 5), BankAccountId = 7, Amount = 5000m }, default);

        Assert.Equal(0m, b.Ledger.Receivable(501, FeeRules.TradePurpose));
        Assert.Equal(-1900m, b.Ledger.Receivable(501, FeeRules.OverpaymentPurpose));
        Assert.Equal(1900m, await b.Db.FeeReceipts.Select(r => r.UnallocatedAmount).SingleAsync());
    }

    [SkippableFact]
    public async Task Generating_a_period_twice_raises_each_demand_once_and_skips_who_it_cannot_bill()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using FeeDbContext _ = b.Db;

        var first = (GenerateDemandsResponse)(await Demands(b).GenerateAsync(June(b), default)).Body!;
        var second = (GenerateDemandsResponse)(await Demands(b).GenerateAsync(June(b), default)).Body!;

        // Priya has no primary guardian; Ravi's enrolment is not active.
        Assert.Equal(2, first.Created);
        Assert.Equal(1, first.Skipped);
        Assert.Equal(0, second.Created);
        Assert.Equal(2, second.AlreadyRaised);
        Assert.Equal(2, await b.Db.FeeDemands.CountAsync());
    }

    [SkippableFact]
    public async Task An_approved_concession_reduces_the_demand_and_posts_to_discount_given()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using FeeDbContext _ = b.Db;

        var setup = new FeeSetupService(b.Db, new FakeAccounts(), b.School, NullLogger<FeeSetupService>.Instance);
        await setup.SaveConcessionAsync(null, new SaveConcessionRequest
        {
            StudentId = 11, FeeHeadId = b.Tuition, ConcessionKind = ConcessionKind.Percent, Value = 50m, Reason = "Sibling",
            ValidFrom = new DateOnly(2026, 6, 1), ValidTo = new DateOnly(2027, 3, 31), IsApproved = true,
        }, mayApprove: true, default);

        await Demands(b).GenerateAsync(June(b), default);
        await Demands(b).PostAsync(new PostDemandsRequest { FeeStructureId = b.Structure, PeriodKey = "2026-06" }, default);

        FeeDemand arun = await b.Db.FeeDemands.SingleAsync(d => d.ContactId == 501);
        Assert.Equal(1250m, arun.ConcessionAmount);
        Assert.Equal(1850m, arun.NetAmount);

        LedgerPosting posting = b.Ledger.Postings[("FDM", arun.FeeDemandId)];
        Assert.True(FeeRules.Balances(posting.Legs));
        Assert.Equal(1250m, posting.Legs.Where(l => l.AccountSystemName == FeeRules.DiscountGiven).Sum(l => l.DebitAmount));
    }

    [SkippableFact]
    public async Task A_concession_is_approved_only_by_someone_who_may_approve()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using FeeDbContext _ = b.Db;

        var setup = new FeeSetupService(b.Db, new FakeAccounts(), b.School, NullLogger<FeeSetupService>.Instance);
        FeeResult result = await setup.SaveConcessionAsync(null, new SaveConcessionRequest
        {
            StudentId = 11, FeeHeadId = b.Tuition, Value = 10m, Reason = "Staff child",
            ValidFrom = new DateOnly(2026, 6, 1), ValidTo = new DateOnly(2027, 3, 31), IsApproved = true,
        }, mayApprove: false, default);

        Assert.Equal(FeeOutcome.StateRule, result.Outcome);
    }

    [SkippableFact]
    public async Task A_paid_demand_cannot_be_voided_until_its_receipt_is()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using FeeDbContext _ = b.Db;

        await Demands(b).GenerateAsync(June(b), default);
        await Demands(b).PostAsync(new PostDemandsRequest { FeeStructureId = b.Structure, PeriodKey = "2026-06" }, default);
        long demand = await b.Db.FeeDemands.Where(d => d.ContactId == 501).Select(d => d.FeeDemandId).SingleAsync();
        long receipt = (await Receipts(b).CreateAsync(new SaveReceiptRequest { ContactId = 501, ReceiptDate = new DateOnly(2026, 6, 5), BankAccountId = 7, Amount = 500m }, default)).Id!.Value;

        Assert.Equal(FeeOutcome.StateRule, (await Demands(b).VoidAsync(demand, "Wrong class", default)).Outcome);

        Assert.Equal(FeeOutcome.Ok, (await Receipts(b).VoidAsync(receipt, "Bounced cheque", default)).Outcome);
        b.Db.ChangeTracker.Clear();
        Assert.Equal(FeeOutcome.Ok, (await Demands(b).VoidAsync(demand, "Wrong class", default)).Outcome);

        Assert.DoesNotContain(("FDM", demand), b.Ledger.Postings.Keys);
        Assert.DoesNotContain(("FRC", receipt), b.Ledger.Postings.Keys);
        Assert.Equal(0m, b.Ledger.Receivable(501, FeeRules.TradePurpose));
    }

    [SkippableFact]
    public async Task A_refused_posting_leaves_the_demand_a_draft_under_the_request_transaction()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using FeeDbContext _ = b.Db;

        await Demands(b).GenerateAsync(June(b), default);
        b.Ledger.Refuse = true;

        await using var tx = await b.Db.Database.BeginTransactionAsync();
        Assert.Equal(FeeOutcome.LedgerRefused,
            (await Demands(b).PostAsync(new PostDemandsRequest { FeeStructureId = b.Structure, PeriodKey = "2026-06" }, default)).Outcome);
        await tx.RollbackAsync();
        b.Db.ChangeTracker.Clear();

        Assert.All(await b.Db.FeeDemands.ToListAsync(), d => Assert.Equal(FeeDocumentStatus.Draft, d.DocumentStatus));
        Assert.Equal(1, await b.Db.NumberingSeries.Where(n => n.SeriesCode == "FDM").Select(n => n.NextNumber).SingleAsync());
    }

    [SkippableFact]
    public async Task A_receipt_from_someone_not_a_guardian_or_into_an_unknown_account_is_refused()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using FeeDbContext _ = b.Db;

        Assert.Equal(FeeOutcome.Invalid, (await Receipts(b).CreateAsync(
            new SaveReceiptRequest { ContactId = 999, ReceiptDate = new DateOnly(2026, 6, 5), BankAccountId = 7, Amount = 100m }, default)).Outcome);
        Assert.Equal(FeeOutcome.Invalid, (await Receipts(b).CreateAsync(
            new SaveReceiptRequest { ContactId = 501, ReceiptDate = new DateOnly(2026, 6, 5), BankAccountId = 8, Amount = 100m }, default)).Outcome);
    }
}

/// <summary>The fee arithmetic and legs, pure (S4, TK-64).</summary>
public sealed class FeeRuleTests
{
    [Theory]
    [InlineData(FeeFrequency.Monthly, 6, 9, true)]
    [InlineData(FeeFrequency.OneTime, 6, 6, true)]
    [InlineData(FeeFrequency.OneTime, 6, 7, false)]
    [InlineData(FeeFrequency.Annual, 4, 4, true)]
    [InlineData(FeeFrequency.Quarterly, 6, 9, true)]
    [InlineData(FeeFrequency.Quarterly, 6, 10, false)]
    [InlineData(FeeFrequency.Quarterly, 6, 3, true)]
    [InlineData(FeeFrequency.Termly, 6, 10, true)]
    [InlineData(FeeFrequency.Termly, 6, 2, true)]
    [InlineData(FeeFrequency.Termly, 6, 3, false)]
    public void A_line_falls_due_by_its_frequency_from_the_years_first_month(FeeFrequency frequency, int firstMonth, int month, bool due) =>
        Assert.Equal(due, FeeRules.IsDue(frequency, firstMonth, month));

    [Theory]
    [InlineData(ConcessionKind.Percent, 2500, 50, 1250)]
    [InlineData(ConcessionKind.Percent, 333.33, 10, 33.33)]
    [InlineData(ConcessionKind.Percent, 100, 100, 100)]
    [InlineData(ConcessionKind.Amount, 600, 200, 200)]
    [InlineData(ConcessionKind.Amount, 600, 900, 600)]
    public void A_concession_is_a_rounded_share_or_a_sum_never_above_the_fee(ConcessionKind kind, double amount, double value, double expected) =>
        Assert.Equal((decimal)expected, FeeRules.ConcessionOn((decimal)amount, kind, (decimal)value));

    [Fact]
    public void Only_an_approved_concession_in_force_applies_and_the_best_of_two_wins()
    {
        var on = new DateOnly(2026, 6, 1);
        FeeConcession[] concessions =
        [
            new() { FeeHeadId = 1, ConcessionKind = ConcessionKind.Percent, Value = 10, IsApproved = true, ValidFrom = on, ValidTo = on.AddDays(30) },
            new() { FeeHeadId = 1, ConcessionKind = ConcessionKind.Amount, Value = 500, IsApproved = true, ValidFrom = on, ValidTo = on.AddDays(30) },
            new() { FeeHeadId = 1, ConcessionKind = ConcessionKind.Percent, Value = 90, IsApproved = false, ValidFrom = on, ValidTo = on.AddDays(30) },
            new() { FeeHeadId = 1, ConcessionKind = ConcessionKind.Percent, Value = 80, IsApproved = true, ValidFrom = on.AddDays(40), ValidTo = on.AddDays(60) },
        ];

        Assert.Equal(500m, FeeRules.BestConcession(2500m, concessions, 1, on));
        Assert.Equal(0m, FeeRules.BestConcession(2500m, concessions, 2, on));
    }

    [Fact]
    public void A_demand_posts_net_to_the_receivable_full_fees_to_income_and_concessions_to_discount()
    {
        var heads = new Dictionary<long, FeeHead>
        {
            [1] = new() { FeeHeadId = 1, Code = "TUITION", Name = "Tuition" },
            [2] = new() { FeeHeadId = 2, Code = "CAUTION", Name = "Caution", IsRefundable = true },
            [3] = new() { FeeHeadId = 3, Code = "BUS", Name = "Bus", IncomeAccountId = 77 },
        };
        var demand = new FeeDemand
        {
            ContactId = 501, TotalAmount = 6000m, ConcessionAmount = 500m, NetAmount = 5500m,
            Lines =
            [
                new() { FeeDemandLineId = 10, FeeHeadId = 1, Amount = 2500m, ConcessionAmount = 500m },
                new() { FeeDemandLineId = 11, FeeHeadId = 2, Amount = 3000m },
                new() { FeeDemandLineId = 12, FeeHeadId = 3, Amount = 500m },
            ],
        };

        List<LedgerLeg> legs = FeeRules.DemandLegs(demand, heads);

        Assert.True(FeeRules.Balances(legs));
        Assert.Equal(5500m, legs.Single(l => l.AccountSystemName == FeeRules.AccountsReceivable).DebitAmount);
        Assert.Equal(2500m, legs.Single(l => l.AccountSystemName == FeeRules.FeeIncome).CreditAmount);
        Assert.Equal(3000m, legs.Single(l => l.AccountSystemName == FeeRules.RefundableDeposits).CreditAmount);
        Assert.Equal(500m, legs.Single(l => l.AccountId == 77).CreditAmount);
        Assert.Equal(500m, legs.Single(l => l.AccountSystemName == FeeRules.DiscountGiven).DebitAmount);
    }

    [Fact]
    public void A_fully_waived_demand_posts_no_receivable_and_still_balances()
    {
        var heads = new Dictionary<long, FeeHead> { [1] = new() { FeeHeadId = 1, Code = "TUITION", Name = "Tuition" } };
        var demand = new FeeDemand
        {
            ContactId = 501, TotalAmount = 2500m, ConcessionAmount = 2500m, NetAmount = 0m,
            Lines = [new() { FeeDemandLineId = 10, FeeHeadId = 1, Amount = 2500m, ConcessionAmount = 2500m }],
        };

        List<LedgerLeg> legs = FeeRules.DemandLegs(demand, heads);

        Assert.True(FeeRules.Balances(legs));
        Assert.DoesNotContain(legs, l => l.AccountSystemName == FeeRules.AccountsReceivable);
    }

    [Fact]
    public void A_receipt_splits_between_the_receivable_and_an_advance()
    {
        List<LedgerLeg> legs = FeeRules.ReceiptLegs(new FeeReceipt { ContactId = 501, BankAccountId = 7, Amount = 5000m, UnallocatedAmount = 1900m });

        Assert.True(FeeRules.Balances(legs));
        Assert.Equal(5000m, legs.Single(l => l.BankAccountId == 7).DebitAmount);
        Assert.Equal(3100m, legs.Single(l => l.SubAccountPurpose == FeeRules.TradePurpose && l.CreditAmount > 0).CreditAmount);
        Assert.Equal(1900m, legs.Single(l => l.SubAccountPurpose == FeeRules.OverpaymentPurpose).CreditAmount);
    }

    [Fact]
    public void Money_settles_the_oldest_demands_first_and_keeps_the_rest()
    {
        var allocations = FeeRules.AutoAllocate(4000m, [(1, 3100m), (2, 2500m), (3, 2500m)]);

        Assert.Equal([(1L, 3100m), (2L, 900m)], allocations);
    }

    [Fact]
    public void A_chosen_allocation_stays_within_what_is_owed_and_what_was_paid()
    {
        var open = new Dictionary<long, decimal> { [1] = 3100m, [2] = 2500m };

        Assert.Null(FeeRules.AllocationProblem(4000m, [(1, 3100m), (2, 900m)], open));
        Assert.NotNull(FeeRules.AllocationProblem(4000m, [(1, 3200m)], open));
        Assert.NotNull(FeeRules.AllocationProblem(1000m, [(1, 600m), (2, 600m)], open));
        Assert.NotNull(FeeRules.AllocationProblem(4000m, [(9, 100m)], open));
        Assert.NotNull(FeeRules.AllocationProblem(4000m, [(1, 100m), (1, 100m)], open));
    }
}

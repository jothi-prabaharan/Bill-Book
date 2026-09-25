using Employee.Api.Services;
using Employee.Entity.Enums;
using Employee.Entity.Models;
using Employee.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Kernel.Numbering;
using Xunit;

namespace Employee.Api.Tests;

/// <summary>
/// The employee master (TK-48), through the service against a real database.
/// The card's Done-when line: an employee is created with family, nominees and
/// bank details, linked to a user, and listed.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class EmployeeServiceTests
{
    private readonly PostgresFixture _postgres;

    public EmployeeServiceTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed class AprilYear : IFinancialYearProvider
    {
        public Task<int> GetStartMonthAsync(CancellationToken ct = default) => Task.FromResult(4);
    }

    private sealed class Caller(bool salary, Guid? userId = null) : ICallerPermissions
    {
        public bool Has(string permission) => salary && permission == EmployeeService.SalaryPermission;

        public Guid? UserId => userId;
    }

    private sealed record Branch(EmployeeDbContext Db, long Department, long Designation, long Grade, long Location);

    private async Task<Branch> NewBranchAsync()
    {
        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        EmployeeDbContext db = _postgres.CreateContext(customerId, orgId);
        await new EmployeeSeeder(db).SeedForOrganizationAsync(orgId, default);

        return new Branch(
            db,
            await db.Departments.Select(d => d.DepartmentId).SingleAsync(),
            await db.Designations.Select(d => d.DesignationId).SingleAsync(),
            await db.Grades.Select(d => d.GradeId).SingleAsync(),
            await db.WorkLocations.Select(d => d.WorkLocationId).SingleAsync());
    }

    private static EmployeeService Service(EmployeeDbContext db, ICallerPermissions caller) =>
        new(db, new NumberGenerator(db, Options.Create(new NumberingOptions()), new AprilYear()), caller, TimeProvider.System);

    private static SaveEmployeeRequest Priya(Branch b, Guid? userId = null) => new()
    {
        FirstName = "Priya",
        LastName = "Raman",
        DateOfBirth = new DateOnly(1994, 5, 12),
        Gender = Gender.Female,
        DepartmentId = b.Department,
        DesignationId = b.Designation,
        GradeId = b.Grade,
        WorkLocationId = b.Location,
        JoiningDate = new DateOnly(2026, 4, 1),
        Phone = "9840012345",
        Pan = "ABCDE1234F",
        Aadhaar = "123412341234",
        UserId = userId,
        FamilyMembers =
        [
            new() { Name = "Raman", Relationship = Relationship.Father },
            new() { Name = "Lakshmi", Relationship = Relationship.Mother },
        ],
        Nominees =
        [
            new() { FamilyMemberIndex = 0, NominationKind = NominationKind.Pf, SharePercent = 60m },
            new() { FamilyMemberIndex = 1, NominationKind = NominationKind.Pf, SharePercent = 40m },
        ],
        BankDetails =
        [
            new() { AccountHolder = "Priya Raman", AccountNo = "001234567890", Ifsc = "HDFC0001234", BankName = "HDFC Bank", IsPrimary = true },
        ],
    };

    [SkippableFact]
    public async Task An_employee_is_created_with_family_nominees_and_bank_linked_to_a_user_and_listed_masked()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Branch b = await NewBranchAsync();
        await using EmployeeDbContext _ = b.Db;
        Guid login = Guid.NewGuid();

        EmployeeResult created = await Service(b.Db, new Caller(salary: false)).CreateAsync(Priya(b, login), default);

        Assert.Equal(EmployeeOutcome.Ok, created.Outcome);
        Assert.StartsWith("EMP-", created.EmployeeCode);

        EmployeeListPage page = await Service(b.Db, new Caller(salary: true)).ListAsync(null, null, null, 1, 20, default);
        EmployeeListItem row = Assert.Single(page.Items);
        Assert.Equal("Priya Raman", row.FullName);
        Assert.True(row.HasLogin);
        // Masked on the list even for a caller who holds payroll.view.
        Assert.Equal("XXXXXX234F", row.MaskedPan);
        Assert.Equal("XXXXXXXX1234", row.MaskedAadhaar);

        Assert.Equal(2, await b.Db.EmployeeFamilyMembers.CountAsync());
        Assert.Equal(100m, await b.Db.EmployeeNominees.SumAsync(n => n.SharePercent));
        Assert.Equal(1, await b.Db.EmployeeBankDetails.CountAsync(x => x.IsPrimary));
        Assert.Equal(EmploymentChangeKind.Joined, (await b.Db.EmploymentHistories.SingleAsync()).ChangeKind);
    }

    [SkippableFact]
    public async Task The_detail_shows_sensitive_numbers_only_to_payroll_or_to_the_employee()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Branch b = await NewBranchAsync();
        await using EmployeeDbContext _ = b.Db;
        Guid login = Guid.NewGuid();
        long id = (await Service(b.Db, new Caller(false)).CreateAsync(Priya(b, login), default)).EmployeeId;

        EmployeeDetail hr = (await Service(b.Db, new Caller(false)).GetAsync(id, default))!;
        EmployeeDetail payroll = (await Service(b.Db, new Caller(true)).GetAsync(id, default))!;
        EmployeeDetail self = (await Service(b.Db, new Caller(false, login)).GetAsync(id, default))!;

        Assert.False(hr.SensitiveShown);
        Assert.Equal("XXXXXX234F", hr.Pan);
        Assert.Equal("XXXXXXXX7890", hr.BankDetails[0].AccountNo);
        Assert.Equal("ABCDE1234F", payroll.Pan);
        Assert.Equal("001234567890", self.BankDetails[0].AccountNo);
    }

    [SkippableFact]
    public async Task Saving_the_masked_detail_back_keeps_the_stored_numbers()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Branch b = await NewBranchAsync();
        await using EmployeeDbContext _ = b.Db;
        EmployeeService hr = Service(b.Db, new Caller(false));
        long id = (await hr.CreateAsync(Priya(b), default)).EmployeeId;

        EmployeeDetail masked = (await hr.GetAsync(id, default))!;
        masked.Phone = "9840099999";
        Assert.Equal(EmployeeOutcome.Ok, (await hr.UpdateAsync(id, masked, default)).Outcome);

        b.Db.ChangeTracker.Clear();
        var stored = await b.Db.Employees.AsNoTracking().SingleAsync(e => e.EmployeeId == id);
        Assert.Equal("ABCDE1234F", stored.Pan);
        Assert.Equal("123412341234", stored.Aadhaar);
        Assert.Equal("9840099999", stored.Phone);
        Assert.Equal("001234567890", (await b.Db.EmployeeBankDetails.AsNoTracking().SingleAsync()).AccountNo);
    }

    [SkippableFact]
    public async Task Nominee_shares_that_do_not_make_100_are_refused_and_nothing_is_saved()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Branch b = await NewBranchAsync();
        await using EmployeeDbContext _ = b.Db;
        SaveEmployeeRequest request = Priya(b);
        request.Nominees[1].SharePercent = 30m;

        EmployeeResult result = await Service(b.Db, new Caller(false)).CreateAsync(request, default);

        Assert.Equal(EmployeeOutcome.NomineeShares, result.Outcome);
        Assert.False(await b.Db.Employees.AnyAsync());
    }

    [SkippableFact]
    public async Task A_manager_chain_back_to_the_employee_is_refused()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Branch b = await NewBranchAsync();
        await using EmployeeDbContext _ = b.Db;
        EmployeeService hr = Service(b.Db, new Caller(false));
        long boss = (await hr.CreateAsync(Priya(b), default)).EmployeeId;
        SaveEmployeeRequest report = Priya(b);
        report.FirstName = "Arun";
        report.ReportsToEmployeeId = boss;
        long arun = (await hr.CreateAsync(report, default)).EmployeeId;

        EmployeeDetail bossDetail = (await Service(b.Db, new Caller(true)).GetAsync(boss, default))!;
        bossDetail.ReportsToEmployeeId = arun;

        Assert.Equal(EmployeeOutcome.ManagerCycle, (await hr.UpdateAsync(boss, bossDetail, default)).Outcome);
    }

    [SkippableFact]
    public async Task A_transfer_appends_a_history_row_and_keeps_the_joining_one()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Branch b = await NewBranchAsync();
        await using EmployeeDbContext _ = b.Db;
        EmployeeService hr = Service(b.Db, new Caller(true));
        long id = (await hr.CreateAsync(Priya(b), default)).EmployeeId;
        long sales = (await new OrganisationService(b.Db).SaveDepartmentAsync(null, new SaveDepartmentRequest { Code = "SAL", Name = "Sales" }, default)).Id;

        EmployeeDetail detail = (await hr.GetAsync(id, default))!;
        detail.DepartmentId = sales;
        detail.EffectiveDate = new DateOnly(2026, 9, 1);
        Assert.Equal(EmployeeOutcome.Ok, (await hr.UpdateAsync(id, detail, default)).Outcome);

        List<EmploymentChangeKind> kinds = await b.Db.EmploymentHistories.OrderBy(h => h.EffectiveDate).Select(h => h.ChangeKind).ToListAsync();
        Assert.Equal([EmploymentChangeKind.Joined, EmploymentChangeKind.Transfer], kinds);
    }

    [SkippableFact]
    public async Task One_login_links_to_one_employee_per_branch()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Branch b = await NewBranchAsync();
        await using EmployeeDbContext _ = b.Db;
        Guid login = Guid.NewGuid();
        EmployeeService hr = Service(b.Db, new Caller(false));
        await hr.CreateAsync(Priya(b, login), default);

        Assert.Equal(EmployeeOutcome.UserAlreadyLinked, (await hr.CreateAsync(Priya(b, login), default)).Outcome);
    }

    [SkippableFact]
    public async Task Another_branch_cannot_read_the_employee()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Branch b = await NewBranchAsync();
        await using EmployeeDbContext _ = b.Db;
        long id = (await Service(b.Db, new Caller(false)).CreateAsync(Priya(b), default)).EmployeeId;

        await using EmployeeDbContext other = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        Assert.Null(await Service(other, new Caller(true)).GetAsync(id, default));
    }
}

/// <summary>The rules that need no database (TK-48).</summary>
public sealed class EmployeeRuleTests
{
    private static SaveEmployeeRequest Valid() => new()
    {
        FirstName = "Priya",
        DateOfBirth = new DateOnly(1994, 5, 12),
        JoiningDate = new DateOnly(2026, 4, 1),
        Phone = "9840012345",
        DepartmentId = 1, DesignationId = 1, GradeId = 1, WorkLocationId = 1,
    };

    [Fact]
    public void A_valid_request_passes() =>
        Assert.Equal(EmployeeOutcome.Ok, EmployeeService.CheckRules(Valid(), "ABCDE1234F", "123412341234"));

    [Fact]
    public void Younger_than_fourteen_on_joining_is_refused()
    {
        SaveEmployeeRequest r = Valid();
        r.DateOfBirth = new DateOnly(2013, 1, 1);
        Assert.Equal(EmployeeOutcome.TooYoung, EmployeeService.CheckRules(r, null, null));
    }

    [Fact]
    public void An_exited_employee_needs_an_exit_date()
    {
        SaveEmployeeRequest r = Valid();
        r.EmployeeStatus = EmployeeStatus.Exited;
        Assert.Equal(EmployeeOutcome.ExitDateRequired, EmployeeService.CheckRules(r, null, null));
    }

    [Theory]
    [InlineData("ABCD1234F", null)]
    [InlineData(null, "12345")]
    public void A_malformed_pan_or_aadhaar_is_refused(string? pan, string? aadhaar) =>
        Assert.Equal(EmployeeOutcome.InvalidIdentityNumber, EmployeeService.CheckRules(Valid(), pan, aadhaar));

    [Fact]
    public void Bank_accounts_need_exactly_one_primary()
    {
        SaveEmployeeRequest r = Valid();
        r.BankDetails = [new() { AccountHolder = "A", AccountNo = "1", Ifsc = "HDFC0001234", BankName = "B" }];
        Assert.Equal(EmployeeOutcome.PrimaryBank, EmployeeService.CheckRules(r, null, null));
    }

    [Fact]
    public void Two_addresses_of_one_kind_are_refused()
    {
        SaveEmployeeRequest r = Valid();
        r.Addresses = [new() { AddressKind = AddressKind.Current, AddressLine1 = "1" }, new() { AddressKind = AddressKind.Current, AddressLine1 = "2" }];
        Assert.Equal(EmployeeOutcome.DuplicateAddress, EmployeeService.CheckRules(r, null, null));
    }

    [Fact]
    public void A_nominee_must_name_a_family_member_in_the_request()
    {
        SaveEmployeeRequest r = Valid();
        r.Nominees = [new() { FamilyMemberIndex = 0, NominationKind = NominationKind.Pf, SharePercent = 100m }];
        Assert.Equal(EmployeeOutcome.NomineeFamilyMember, EmployeeService.CheckRules(r, null, null));
    }

    [Fact]
    public void Every_refusal_has_a_sentence_that_names_no_figure()
    {
        foreach (EmployeeOutcome outcome in Enum.GetValues<EmployeeOutcome>().Where(o => o is not (EmployeeOutcome.Ok or EmployeeOutcome.NotFound)))
        {
            string sentence = Employee.Api.Controllers.EmployeesController.Sentence(outcome);
            Assert.NotEqual("The employee could not be saved.", sentence);
            Assert.DoesNotContain("Employees", sentence);
        }
    }
}

/// <summary>Masking PAN, Aadhaar and bank numbers (TK-48).</summary>
public sealed class SensitiveMaskTests
{
    [Theory]
    [InlineData("ABCDE1234F", "XXXXXX234F")]
    [InlineData("123412341234", "XXXXXXXX1234")]
    [InlineData("12", "X2")]
    [InlineData(null, null)]
    public void Keeps_the_last_four_and_hides_the_rest(string? value, string? masked) =>
        Assert.Equal(masked, SensitiveMask.Mask(value));

    [Fact]
    public void A_masked_value_sent_back_means_unchanged_and_a_new_value_replaces()
    {
        Assert.Equal("ABCDE1234F", SensitiveMask.Resolve("XXXXXX234F", "ABCDE1234F"));
        Assert.Equal("PQRSX9876Z", SensitiveMask.Resolve("PQRSX9876Z", "ABCDE1234F"));
        Assert.Null(SensitiveMask.Resolve(null, "ABCDE1234F"));
    }
}

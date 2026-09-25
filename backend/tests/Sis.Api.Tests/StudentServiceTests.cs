using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shared.Kernel.Contacts;
using Shared.Kernel.Numbering;
using Sis.Api.Services;
using Sis.Entity.Enums;
using Sis.Entity.Models;
using Sis.Repository;
using Xunit;

namespace Sis.Api.Tests;

/// <summary>A contact directory standing in for Master: the ids it knows, and whether each is a guardian.</summary>
internal sealed class FakeContacts : IContactDirectory
{
    public Dictionary<long, ContactSummary> Known { get; } = [];

    public bool Down { get; set; }

    public FakeContacts Guardian(long id, string name, bool active = true)
    {
        Known[id] = new ContactSummary { ContactId = id, ContactCode = $"C{id}", DisplayName = name, IsGuardian = true, IsActive = active };
        return this;
    }

    public FakeContacts Vendor(long id, string name)
    {
        Known[id] = new ContactSummary { ContactId = id, ContactCode = $"V{id}", DisplayName = name, IsVendor = true, IsActive = true };
        return this;
    }

    public Task<IReadOnlyDictionary<long, ContactSummary>> FindAsync(IEnumerable<long> ids, CancellationToken ct)
    {
        if (Down)
        {
            throw new HttpRequestException("Master is down.");
        }

        IReadOnlyDictionary<long, ContactSummary> found = ids.Where(Known.ContainsKey).Distinct().ToDictionary(id => id, id => Known[id]);
        return Task.FromResult(found);
    }
}

internal sealed class AprilYear : IFinancialYearProvider
{
    public Task<int> GetStartMonthAsync(CancellationToken ct = default) => Task.FromResult(4);
}

/// <summary>
/// Students through the service against a real database (S1, TK-61). The
/// card's Done-when: a student is admitted directly, enrolled in a section and
/// listed.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class StudentServiceTests
{
    private readonly PostgresFixture _postgres;

    public StudentServiceTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed record Branch(SisDbContext Db, long Year, long SectionA, FakeContacts Contacts);

    private async Task<Branch> NewBranchAsync(int? capacity = null)
    {
        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        SisDbContext db = _postgres.CreateContext(customerId, orgId);
        await new SisSeeder(db).SeedForOrganizationAsync(orgId, default);

        var academic = new AcademicService(db);
        long year = (await academic.SaveYearAsync(null, new SaveAcademicYearRequest
        {
            Code = "2026-27", StartDate = new DateOnly(2026, 6, 1), EndDate = new DateOnly(2027, 3, 31), IsCurrent = true,
        }, default)).Id!.Value;
        long classVi = await db.SchoolClasses.Where(c => c.Code == "VI").Select(c => c.SchoolClassId).SingleAsync();
        long section = (await academic.SaveSectionAsync(null, new SaveSectionRequest
        {
            AcademicYearId = year, SchoolClassId = classVi, Name = "A", Capacity = capacity,
        }, default)).Id!.Value;

        return new Branch(db, year, section, new FakeContacts().Guardian(501, "Lakshmi Raman").Guardian(502, "Raman K"));
    }

    private static StudentService Service(Branch b) =>
        new(b.Db, new NumberGenerator(b.Db, Options.Create(new NumberingOptions()), new AprilYear()), b.Contacts, NullLogger<StudentService>.Instance);

    private static SaveStudentRequest Arun(Branch b, int? roll = 1) => new()
    {
        FirstName = "Arun",
        LastName = "Raman",
        DateOfBirth = new DateOnly(2015, 2, 10),
        Gender = Gender.Male,
        AdmissionDate = new DateOnly(2026, 6, 1),
        NationalId = "123456789012",
        Guardians =
        [
            new() { ContactId = 501, Relationship = GuardianRelationship.Mother, IsPrimary = true, HasPortalAccess = true },
            new() { ContactId = 502, Relationship = GuardianRelationship.Father },
        ],
        Enrol = new EnrolRequest { AcademicYearId = b.Year, SectionId = b.SectionA, RollNo = roll },
    };

    [SkippableFact]
    public async Task A_student_admitted_directly_is_numbered_enrolled_and_listed()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using SisDbContext _ = b.Db;

        SisResult created = await Service(b).CreateAsync(Arun(b), default);

        Assert.Equal(SisOutcome.Ok, created.Outcome);
        StudentView view = (await Service(b).GetAsync(created.Id!.Value, default))!;
        Assert.Equal("ADM-00001", view.AdmissionNo);
        Assert.Equal(2, view.Guardians.Count);
        Assert.Equal("Lakshmi Raman", view.Guardians.Single(g => g.IsPrimary).DisplayName);
        Assert.Single(view.Enrolments);

        StudentListItem listed = Assert.Single(await Service(b).ListAsync(null, null, b.SectionA, default));
        Assert.Equal("Arun Raman", listed.FullName);
        Assert.Equal(1, listed.RollNo);
        Assert.Equal("••••••••9012", listed.NationalId);

        RollEntry roll = Assert.Single(await Service(b).RollAsync(b.SectionA, default));
        Assert.Equal(view.Enrolments[0].EnrolmentId, roll.EnrolmentId);
    }

    [SkippableFact]
    public async Task A_guardian_that_is_not_a_guardian_contact_is_refused_and_no_number_is_spent()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using SisDbContext _ = b.Db;
        b.Contacts.Vendor(503, "Stationery House");

        SaveStudentRequest request = Arun(b);
        request.Guardians = [new() { ContactId = 503, IsPrimary = true }];

        Assert.Equal(SisOutcome.GuardianRule, (await Service(b).CreateAsync(request, default)).Outcome);
        Assert.Equal(0, await b.Db.Students.CountAsync());
        Assert.Equal(1, await b.Db.NumberingSeries.Where(n => n.SeriesCode == "ADM").Select(n => n.NextNumber).SingleAsync());
    }

    [SkippableFact]
    public async Task An_unknown_or_inactive_guardian_is_refused()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using SisDbContext _ = b.Db;
        b.Contacts.Guardian(504, "Moved away", active: false);

        SaveStudentRequest unknown = Arun(b);
        unknown.Guardians = [new() { ContactId = 999, IsPrimary = true }];
        SaveStudentRequest inactive = Arun(b);
        inactive.Guardians = [new() { ContactId = 504, IsPrimary = true }];

        Assert.Equal(SisOutcome.GuardianRule, (await Service(b).CreateAsync(unknown, default)).Outcome);
        Assert.Equal(SisOutcome.GuardianRule, (await Service(b).CreateAsync(inactive, default)).Outcome);
    }

    [SkippableFact]
    public async Task Master_being_down_is_unavailable_not_a_refusal()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using SisDbContext _ = b.Db;
        b.Contacts.Down = true;

        Assert.Equal(SisOutcome.Unavailable, (await Service(b).CreateAsync(Arun(b), default)).Outcome);
    }

    [SkippableFact]
    public async Task A_full_section_refuses_the_next_enrolment()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync(capacity: 1);
        await using SisDbContext _ = b.Db;

        Assert.Equal(SisOutcome.Ok, (await Service(b).CreateAsync(Arun(b, roll: 1), default)).Outcome);
        Assert.Equal(SisOutcome.SectionFull, (await Service(b).CreateAsync(Arun(b, roll: 2), default)).Outcome);
    }

    [SkippableFact]
    public async Task A_student_is_enrolled_once_per_year_and_a_roll_number_once_per_section()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using SisDbContext _ = b.Db;

        long first = (await Service(b).CreateAsync(Arun(b, roll: 7), default)).Id!.Value;
        Assert.Equal(SisOutcome.Duplicate,
            (await Service(b).EnrolAsync(new EnrolRequest { StudentId = first, AcademicYearId = b.Year, SectionId = b.SectionA }, default)).Outcome);

        SaveStudentRequest second = Arun(b, roll: 7);
        second.Enrol = null;
        long other = (await Service(b).CreateAsync(second, default)).Id!.Value;
        Assert.Equal(SisOutcome.Duplicate,
            (await Service(b).EnrolAsync(new EnrolRequest { StudentId = other, AcademicYearId = b.Year, SectionId = b.SectionA, RollNo = 7 }, default)).Outcome);
    }

    [SkippableFact]
    public async Task A_closed_year_takes_no_enrolment()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using SisDbContext _ = b.Db;

        await new AcademicService(b.Db).SaveYearAsync(b.Year, new SaveAcademicYearRequest
        {
            Code = "2026-27", StartDate = new DateOnly(2026, 6, 1), EndDate = new DateOnly(2027, 3, 31), IsClosed = true,
        }, default);

        Assert.Equal(SisOutcome.YearRule, (await Service(b).CreateAsync(Arun(b), default)).Outcome);
    }

    [SkippableFact]
    public async Task Admitting_the_same_application_twice_returns_one_student()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using SisDbContext _ = b.Db;

        SisResult first = await Service(b).CreateAsync(Arun(b), default, sourceApplicationId: 77);
        SisResult second = await Service(b).CreateAsync(Arun(b, roll: 2), default, sourceApplicationId: 77);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await b.Db.Students.CountAsync());
    }

    [SkippableFact]
    public async Task Making_a_year_current_makes_the_other_not_current()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using SisDbContext _ = b.Db;

        await new AcademicService(b.Db).SaveYearAsync(null, new SaveAcademicYearRequest
        {
            Code = "2027-28", StartDate = new DateOnly(2027, 6, 1), EndDate = new DateOnly(2028, 3, 31), IsCurrent = true,
        }, default);

        Assert.Equal("2027-28", await b.Db.AcademicYears.Where(y => y.IsCurrent).Select(y => y.Code).SingleAsync());
    }

    [SkippableFact]
    public async Task One_branch_never_sees_another_branchs_students()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch mine = await NewBranchAsync();
        Branch theirs = await NewBranchAsync();
        await using SisDbContext _a = mine.Db;
        await using SisDbContext _b = theirs.Db;

        await Service(theirs).CreateAsync(Arun(theirs), default);

        Assert.Empty(await Service(mine).ListAsync(null, null, null, default));
    }
}

/// <summary>The student rules, pure (S1, TK-61).</summary>
public sealed class StudentRuleTests
{
    private static SaveStudentRequest Valid() => new()
    {
        FirstName = "Arun",
        DateOfBirth = new DateOnly(2015, 2, 10),
        AdmissionDate = new DateOnly(2026, 6, 1),
        Guardians = [new() { ContactId = 1, IsPrimary = true }],
    };

    [Fact]
    public void A_valid_student_passes() => Assert.Null(StudentService.CheckStudent(Valid()));

    [Fact]
    public void Born_on_or_after_admission_is_refused()
    {
        SaveStudentRequest r = Valid();
        r.DateOfBirth = r.AdmissionDate;
        Assert.NotNull(StudentService.CheckStudent(r));
    }

    [Theory]
    [InlineData(StudentStatus.Alumni)]
    [InlineData(StudentStatus.Withdrawn)]
    [InlineData(StudentStatus.Transferred)]
    public void Leaving_needs_a_leaving_date(StudentStatus status)
    {
        SaveStudentRequest r = Valid();
        r.StudentStatus = status;
        Assert.NotNull(StudentService.CheckStudent(r));
        r.LeavingDate = new DateOnly(2027, 3, 31);
        Assert.Null(StudentService.CheckStudent(r));
    }

    [Fact]
    public void No_guardian_or_three_guardians_is_refused()
    {
        Assert.NotNull(StudentService.CheckGuardianShape([]));
        Assert.NotNull(StudentService.CheckGuardianShape(
        [
            new() { ContactId = 1, IsPrimary = true }, new() { ContactId = 2 }, new() { ContactId = 3 },
        ]));
    }

    [Fact]
    public void Exactly_one_guardian_is_primary()
    {
        Assert.NotNull(StudentService.CheckGuardianShape([new() { ContactId = 1 }, new() { ContactId = 2 }]));
        Assert.NotNull(StudentService.CheckGuardianShape([new() { ContactId = 1, IsPrimary = true }, new() { ContactId = 2, IsPrimary = true }]));
        Assert.Null(StudentService.CheckGuardianShape([new() { ContactId = 1, IsPrimary = true }, new() { ContactId = 2 }]));
    }

    [Fact]
    public void The_same_guardian_twice_is_refused() =>
        Assert.NotNull(StudentService.CheckGuardianShape([new() { ContactId = 1, IsPrimary = true }, new() { ContactId = 1 }]));

    [Theory]
    [InlineData(null, 500, false)]
    [InlineData(40, 39, false)]
    [InlineData(40, 40, true)]
    public void A_section_is_full_at_its_capacity(int? capacity, int enrolled, bool full) =>
        Assert.Equal(full, StudentService.IsFull(capacity, enrolled));

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("123", "•••")]
    [InlineData("123456789012", "••••••••9012")]
    public void A_national_id_shows_only_its_last_four(string? value, string? masked) =>
        Assert.Equal(masked, StudentService.Mask(value));

    [Fact]
    public void Years_that_touch_overlap()
    {
        (DateOnly, DateOnly)[] others = [(new DateOnly(2026, 6, 1), new DateOnly(2027, 3, 31))];

        Assert.True(AcademicService.Overlaps(new DateOnly(2027, 3, 31), new DateOnly(2028, 3, 31), others));
        Assert.False(AcademicService.Overlaps(new DateOnly(2027, 4, 1), new DateOnly(2028, 3, 31), others));
    }
}

using Attendance.Api.Services;
using Attendance.Entity.Enums;
using Attendance.Entity.Models;
using Attendance.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Kernel.School;
using Xunit;

namespace Attendance.Api.Tests;

/// <summary>Student's roll, as the register sees it: one section of three students in 2026-27.</summary>
internal sealed class FakeRoll : IStudentClient
{
    public bool Down { get; set; }

    public bool YearIsClosed { get; set; }

    public Task<SectionRollResponse> RollAsync(long sectionId, CancellationToken ct)
    {
        if (Down)
        {
            throw new HttpRequestException("Student is down.");
        }

        return Task.FromResult(sectionId != 10
            ? new SectionRollResponse { SectionExists = false }
            : new SectionRollResponse
            {
                SectionExists = true,
                YearStart = new DateOnly(2026, 6, 1),
                YearEnd = new DateOnly(2027, 3, 31),
                YearIsClosed = YearIsClosed,
                Roll =
                [
                    new() { EnrolmentId = 1, StudentId = 1, AdmissionNo = "ADM-1", FullName = "Arun", RollNo = 1 },
                    new() { EnrolmentId = 2, StudentId = 2, AdmissionNo = "ADM-2", FullName = "Meera", RollNo = 2 },
                    new() { EnrolmentId = 3, StudentId = 3, AdmissionNo = "ADM-3", FullName = "Priya", RollNo = 3 },
                ],
            });
    }

    public Task<AcademicCheckResponse> CheckAsync(long? academicYearId, long? schoolClassId, long? sectionId, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<EnrolmentInfo>> EnrolmentsAsync(EnrolmentQueryRequest query, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<AdmitStudentResponse> AdmitAsync(AdmitStudentRequest request, CancellationToken ct) =>
        throw new NotSupportedException();
}

internal sealed class Caller(bool mayUnlock) : ICallerPermissions
{
    public bool Has(string permission) => mayUnlock && permission == RegisterService.UnlockPermission;
}

internal sealed class FixedClock(DateOnly today) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(today.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero);
}

/// <summary>
/// The register against a real database (S3, TK-63). The card's Done-when: a
/// locked day refuses an edit from a teacher and accepts one from
/// <c>attendance.unlock</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class RegisterTests
{
    private static readonly DateOnly Day = new(2026, 7, 15);

    private readonly PostgresFixture _postgres;

    public RegisterTests(PostgresFixture postgres) => _postgres = postgres;

    private static RegisterService Service(AttendanceDbContext db, bool mayUnlock, FakeRoll? roll = null) =>
        new(db, roll ?? new FakeRoll(), new Caller(mayUnlock), new FixedClock(new DateOnly(2026, 7, 20)), NullLogger<RegisterService>.Instance);

    private static SaveRegisterRequest Register(AttendanceStatus arun = AttendanceStatus.Present) => new()
    {
        SectionId = 10,
        AttendanceDate = Day,
        Rows =
        [
            new() { EnrolmentId = 1, AttendanceStatus = arun },
            new() { EnrolmentId = 2, AttendanceStatus = AttendanceStatus.Absent, Remarks = "Fever" },
            new() { EnrolmentId = 3, AttendanceStatus = AttendanceStatus.Late },
        ],
    };

    [SkippableFact]
    public async Task A_locked_day_refuses_a_teacher_and_accepts_someone_who_may_unlock()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using AttendanceDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        RegisterService teacher = Service(db, mayUnlock: false);
        Assert.Equal(RegisterOutcome.Ok, (await teacher.SaveAsync(Register(), default)).Outcome);
        Assert.Equal(RegisterOutcome.Ok, (await teacher.LockAsync(new LockRequest { SectionId = 10, AttendanceDate = Day }, default)).Outcome);

        Assert.Equal(RegisterOutcome.Locked, (await teacher.SaveAsync(Register(AttendanceStatus.Absent), default)).Outcome);
        Assert.Equal(AttendanceStatus.Present, await db.StudentAttendance.Where(a => a.EnrolmentId == 1).Select(a => a.AttendanceStatus).SingleAsync());

        RegisterService principal = Service(db, mayUnlock: true);
        Assert.Equal(RegisterOutcome.Ok, (await principal.SaveAsync(Register(AttendanceStatus.Absent), default)).Outcome);
        db.ChangeTracker.Clear();
        Assert.Equal(AttendanceStatus.Absent, await db.StudentAttendance.Where(a => a.EnrolmentId == 1).Select(a => a.AttendanceStatus).SingleAsync());

        // Unlocked, the teacher can change it again.
        Assert.Equal(RegisterOutcome.Ok, (await principal.UnlockAsync(new LockRequest { SectionId = 10, AttendanceDate = Day }, default)).Outcome);
        Assert.Equal(RegisterOutcome.Ok, (await teacher.SaveAsync(Register(), default)).Outcome);
    }

    [SkippableFact]
    public async Task Saving_twice_keeps_one_mark_per_student_and_the_register_reads_it_back()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using AttendanceDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        RegisterService service = Service(db, mayUnlock: false);

        await service.SaveAsync(Register(), default);
        await service.SaveAsync(Register(AttendanceStatus.HalfDay), default);

        Assert.Equal(3, await db.StudentAttendance.CountAsync());
        (RegisterResult _, RegisterView? view) = await service.GetAsync(10, Day, default);
        Assert.True(view!.IsTaken);
        Assert.Equal(AttendanceStatus.HalfDay, view.Rows.Single(r => r.EnrolmentId == 1).AttendanceStatus);
        Assert.Equal("Fever", view.Rows.Single(r => r.EnrolmentId == 2).Remarks);
    }

    [SkippableFact]
    public async Task An_untaken_day_reads_as_everyone_present_and_cannot_be_locked()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using AttendanceDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        RegisterService service = Service(db, mayUnlock: false);

        (RegisterResult _, RegisterView? view) = await service.GetAsync(10, Day, default);
        Assert.False(view!.IsTaken);
        Assert.All(view.Rows, r => Assert.Equal(AttendanceStatus.Present, r.AttendanceStatus));
        Assert.Equal(RegisterOutcome.Invalid, (await service.LockAsync(new LockRequest { SectionId = 10, AttendanceDate = Day }, default)).Outcome);
    }

    [SkippableFact]
    public async Task A_mark_for_someone_off_the_roll_is_refused()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using AttendanceDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        SaveRegisterRequest request = Register();
        request.Rows.Add(new RegisterRow { EnrolmentId = 99 });

        Assert.Equal(RegisterOutcome.Invalid, (await Service(db, false).SaveAsync(request, default)).Outcome);
        Assert.Equal(0, await db.StudentAttendance.CountAsync());
    }

    [SkippableFact]
    public async Task A_future_day_a_day_outside_the_year_and_a_closed_year_are_refused()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using AttendanceDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        SaveRegisterRequest future = Register();
        future.AttendanceDate = new DateOnly(2026, 7, 21);
        SaveRegisterRequest before = Register();
        before.AttendanceDate = new DateOnly(2026, 5, 31);

        Assert.Equal(RegisterOutcome.Invalid, (await Service(db, true).SaveAsync(future, default)).Outcome);
        Assert.Equal(RegisterOutcome.Invalid, (await Service(db, true).SaveAsync(before, default)).Outcome);
        Assert.Equal(RegisterOutcome.Invalid, (await Service(db, true, new FakeRoll { YearIsClosed = true }).SaveAsync(Register(), default)).Outcome);
    }

    [SkippableFact]
    public async Task An_unknown_section_is_not_found_and_sis_being_down_is_unavailable()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using AttendanceDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        SaveRegisterRequest other = Register();
        other.SectionId = 11;

        Assert.Equal(RegisterOutcome.NotFound, (await Service(db, false).SaveAsync(other, default)).Outcome);
        Assert.Equal(RegisterOutcome.Unavailable, (await Service(db, false, new FakeRoll { Down = true }).SaveAsync(Register(), default)).Outcome);
    }

    [SkippableFact]
    public async Task The_summary_counts_each_kind_of_day()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using AttendanceDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        RegisterService service = Service(db, false);

        await service.SaveAsync(Register(), default);
        SaveRegisterRequest next = Register(AttendanceStatus.Absent);
        next.AttendanceDate = Day.AddDays(1);
        await service.SaveAsync(next, default);

        AttendanceSummary arun = (await service.SummaryAsync([1], Day, Day.AddDays(1), default)).Single();
        Assert.Equal(1, arun.Present);
        Assert.Equal(1, arun.Absent);
    }
}

/// <summary>The register rules, pure (S3, TK-63).</summary>
public sealed class RegisterRuleTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void A_locked_day_changes_only_for_someone_who_may_unlock(bool locked, bool mayUnlock, bool allowed) =>
        Assert.Equal(allowed, RegisterService.MayChange(locked, mayUnlock));

    [Fact]
    public void A_day_is_inside_the_open_year_and_not_in_the_future()
    {
        DateOnly start = new(2026, 6, 1), end = new(2027, 3, 31), today = new(2026, 7, 20);

        Assert.Null(RegisterService.DayProblem(today, today, start, end, false));
        Assert.NotNull(RegisterService.DayProblem(today.AddDays(1), today, start, end, false));
        Assert.NotNull(RegisterService.DayProblem(start.AddDays(-1), today, start, end, false));
        Assert.NotNull(RegisterService.DayProblem(today, today, start, end, true));
    }
}

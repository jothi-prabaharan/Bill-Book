using Attendance.Api.Services;
using Attendance.Entity.Enums;
using Attendance.Entity.Models;
using Attendance.Entity.TableEntities;
using Attendance.Repository;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Kernel.School;
using Xunit;

namespace Attendance.Api.Tests;

/// <summary>Sis, as the portal asks it: guardian 800 with portal access has student 1 (enrolment 1).</summary>
internal sealed class FakeGuardians : ISisClient
{
    public bool Down { get; set; }

    public EnrolmentQueryRequest? Asked { get; private set; }

    public Task<IReadOnlyList<EnrolmentInfo>> EnrolmentsAsync(EnrolmentQueryRequest query, CancellationToken ct)
    {
        if (Down)
        {
            throw new HttpRequestException("Sis is down.");
        }

        Asked = query;
        IReadOnlyList<EnrolmentInfo> rows = query.GuardianContactId == 800 && query.PortalAccessOnly
            ? [new EnrolmentInfo { EnrolmentId = 1, StudentId = 1, StudentName = "Arun", AdmissionNo = "ADM-1", ClassName = "VI", SectionName = "A", IsActive = true }]
            : [];
        return Task.FromResult(rows);
    }

    public Task<SectionRollResponse> RollAsync(long sectionId, CancellationToken ct) => throw new NotSupportedException();

    public Task<AcademicCheckResponse> CheckAsync(long? academicYearId, long? schoolClassId, long? sectionId, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<AdmitStudentResponse> AdmitAsync(AdmitStudentRequest request, CancellationToken ct) => throw new NotSupportedException();
}

/// <summary>
/// A child's month of attendance in the parent portal (S9, TK-69): the
/// guardian sees their own child's days and counts, and another child is not
/// found.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PortalAttendanceTests
{
    private readonly PostgresFixture _postgres;

    public PortalAttendanceTests(PostgresFixture postgres) => _postgres = postgres;

    private static StudentAttendance Mark(long enrolment, int day, AttendanceStatus status) =>
        new() { SectionId = 10, EnrolmentId = enrolment, AttendanceDate = new DateOnly(2026, 7, day), AttendanceStatus = status };

    [SkippableFact]
    public async Task A_guardian_sees_their_childs_month_with_counts()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using AttendanceDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        db.StudentAttendance.AddRange(
            Mark(1, 1, AttendanceStatus.Present), Mark(1, 2, AttendanceStatus.Absent), Mark(1, 3, AttendanceStatus.Late),
            new StudentAttendance { SectionId = 10, EnrolmentId = 1, AttendanceDate = new DateOnly(2026, 8, 1), AttendanceStatus = AttendanceStatus.Present },
            Mark(2, 1, AttendanceStatus.Absent));
        await db.SaveChangesAsync();
        var sis = new FakeGuardians();

        (PortalAttendanceOutcome outcome, PortalAttendanceView? view) =
            await new PortalAttendanceService(db, sis, NullLogger<PortalAttendanceService>.Instance).MonthAsync(800, 1, new DateOnly(2026, 7, 15), default);

        Assert.Equal(PortalAttendanceOutcome.Ok, outcome);
        Assert.Equal(new DateOnly(2026, 7, 1), view!.Month);
        Assert.Equal(3, view.Days.Count);
        Assert.Equal((1, 1, 1), (view.Present, view.Absent, view.Late));
        Assert.True(sis.Asked!.PortalAccessOnly);
    }

    [SkippableFact]
    public async Task Another_child_is_not_found_and_sis_down_says_so()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using AttendanceDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        var sis = new FakeGuardians();
        var portal = new PortalAttendanceService(db, sis, NullLogger<PortalAttendanceService>.Instance);

        Assert.Equal(PortalAttendanceOutcome.NotFound, (await portal.MonthAsync(800, 2, new DateOnly(2026, 7, 1), default)).Outcome);
        Assert.Equal(PortalAttendanceOutcome.NotFound, (await portal.MonthAsync(801, 1, new DateOnly(2026, 7, 1), default)).Outcome);

        sis.Down = true;
        Assert.Equal(PortalAttendanceOutcome.Unavailable, (await portal.MonthAsync(800, 1, new DateOnly(2026, 7, 1), default)).Outcome);
    }

    [Fact]
    public void Counts_add_up_by_status()
    {
        PortalAttendanceView view = PortalAttendanceService.Summarise(1, new DateOnly(2026, 7, 1),
        [
            new() { AttendanceDate = new DateOnly(2026, 7, 1), AttendanceStatus = AttendanceStatus.Holiday },
            new() { AttendanceDate = new DateOnly(2026, 7, 2), AttendanceStatus = AttendanceStatus.Leave },
            new() { AttendanceDate = new DateOnly(2026, 7, 3), AttendanceStatus = AttendanceStatus.HalfDay },
        ]);

        Assert.Equal((0, 1, 1, 1), (view.Present, view.Holiday, view.Leave, view.HalfDay));
    }
}

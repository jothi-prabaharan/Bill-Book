using Microsoft.EntityFrameworkCore;
using Student.Api.Services;
using Student.Entity.Enums;
using Student.Entity.Models;
using Student.Entity.TableEntities;
using Student.Repository;
using Xunit;

namespace Student.Api.Tests;

/// <summary>
/// The parent portal's children and marks (S9, TK-69): a guardian with portal
/// access sees their own children and published marks, and nothing else.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PortalServiceTests
{
    private const long Mother = 900;
    private const long Stranger = 901;
    private const long Uncle = 902;

    private readonly PostgresFixture _postgres;

    public PortalServiceTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed record School(StudentDbContext Db, long Year, long Section, long ExamSubject, long Exam, long Meera, long Arun);

    /// <summary>Meera and Arun, both the mother's; the uncle is Arun's second guardian without portal access. Meera has 81 in maths.</summary>
    private async Task<School> NewSchoolAsync()
    {
        Guid orgId = Guid.NewGuid();
        StudentDbContext db = _postgres.CreateContext(Guid.NewGuid(), orgId);
        await new StudentSeeder(db).SeedForOrganizationAsync(orgId, default);

        var academic = new AcademicService(db);
        long year = (await academic.SaveYearAsync(null, new SaveAcademicYearRequest
        {
            Code = "2026-27", StartDate = new DateOnly(2026, 6, 1), EndDate = new DateOnly(2027, 3, 31), IsCurrent = true,
        }, default)).Id!.Value;
        long classVi = await db.SchoolClasses.Where(c => c.Code == "VI").Select(c => c.SchoolClassId).SingleAsync();
        long section = (await academic.SaveSectionAsync(null, new SaveSectionRequest { AcademicYearId = year, SchoolClassId = classVi, Name = "A" }, default)).Id!.Value;
        long maths = (await academic.SaveSubjectAsync(null, new SaveSubjectRequest { Code = "MAT", Name = "Mathematics" }, default)).Id!.Value;

        var meera = new StudentRecord { AdmissionNo = "ADM-1", FirstName = "Meera", LastName = "Raman", DateOfBirth = new DateOnly(2015, 2, 10), AdmissionDate = new DateOnly(2026, 6, 1) };
        var arun = new StudentRecord { AdmissionNo = "ADM-2", FirstName = "Arun", LastName = "Raman", DateOfBirth = new DateOnly(2017, 5, 3), AdmissionDate = new DateOnly(2026, 6, 1) };
        db.Students.AddRange(meera, arun);
        await db.SaveChangesAsync();
        db.StudentGuardians.AddRange(
            new StudentGuardian { StudentId = meera.StudentId, ContactId = Mother, IsPrimary = true, HasPortalAccess = true },
            new StudentGuardian { StudentId = arun.StudentId, ContactId = Mother, IsPrimary = true, HasPortalAccess = true },
            new StudentGuardian { StudentId = arun.StudentId, ContactId = Uncle, IsPrimary = false, HasPortalAccess = false });
        var enrolment = new Enrolment { StudentId = meera.StudentId, AcademicYearId = year, SectionId = section, RollNo = 1 };
        db.Enrolments.Add(enrolment);
        await db.SaveChangesAsync();

        var exams = new ExamService(db);
        long exam = (await exams.SaveAsync(null, new SaveExamRequest
        {
            AcademicYearId = year, Name = "Term 1", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 9, 10),
            Subjects = [new() { SubjectId = maths, SchoolClassId = classVi, MaxMarks = 100, PassMarks = 35 }],
        }, default)).Id!.Value;
        long examSubject = await db.ExamSubjects.Where(s => s.ExamId == exam).Select(s => s.ExamSubjectId).SingleAsync();
        await exams.MoveAsync(exam, ExamStatus.MarksOpen, default);
        await exams.SaveMarksAsync(exam, new SaveMarksRequest
        {
            ExamSubjectId = examSubject, Marks = [new() { EnrolmentId = enrolment.EnrolmentId, Marks = 81 }],
        }, default);

        return new School(db, year, section, examSubject, exam, meera.StudentId, arun.StudentId);
    }

    [SkippableFact]
    public async Task A_guardian_sees_their_children_with_class_and_section()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        School s = await NewSchoolAsync();
        await using StudentDbContext _ = s.Db;

        List<PortalChildView> children = await new PortalService(s.Db).ChildrenAsync(Mother, default);

        Assert.Equal(["Arun Raman", "Meera Raman"], children.Select(c => c.StudentName));
        PortalChildView meera = children.Single(c => c.StudentId == s.Meera);
        Assert.Equal("VI", meera.ClassName);
        Assert.Equal("A", meera.SectionName);
        Assert.Equal("2026-27", meera.AcademicYearCode);
        Assert.Null(children.Single(c => c.StudentId == s.Arun).ClassName);
    }

    [SkippableFact]
    public async Task A_guardian_without_portal_access_or_a_stranger_sees_no_children()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        School s = await NewSchoolAsync();
        await using StudentDbContext _ = s.Db;
        var portal = new PortalService(s.Db);

        Assert.Empty(await portal.ChildrenAsync(Uncle, default));
        Assert.Empty(await portal.ChildrenAsync(Stranger, default));
        Assert.Null(await portal.MarksAsync(Uncle, s.Arun, default));
        Assert.Null(await portal.MarksAsync(Stranger, s.Meera, default));
    }

    [SkippableFact]
    public async Task Marks_show_only_once_the_exam_is_published()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        School s = await NewSchoolAsync();
        await using StudentDbContext _ = s.Db;
        var portal = new PortalService(s.Db);

        Assert.Empty((await portal.MarksAsync(Mother, s.Meera, default))!);

        await new ExamService(s.Db).MoveAsync(s.Exam, ExamStatus.Published, default);
        PortalExamView exam = Assert.Single((await portal.MarksAsync(Mother, s.Meera, default))!);

        Assert.Equal("Term 1", exam.ExamName);
        PortalMarkView maths = Assert.Single(exam.Subjects);
        Assert.Equal("Mathematics", maths.SubjectName);
        Assert.Equal(81m, maths.Marks);
        Assert.True(maths.Passed);
        Assert.Empty((await portal.MarksAsync(Mother, s.Arun, default))!);
    }
}

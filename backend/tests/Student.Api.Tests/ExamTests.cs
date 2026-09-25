using Microsoft.EntityFrameworkCore;
using Student.Api.Services;
using Student.Entity.Enums;
using Student.Entity.Models;
using Student.Repository;
using Xunit;

namespace Student.Api.Tests;

/// <summary>Exams and marks (S1, TK-61): the state machine and the marks rules, pure.</summary>
public sealed class ExamRuleTests
{
    [Theory]
    [InlineData(ExamStatus.Planned, ExamStatus.MarksOpen, true)]
    [InlineData(ExamStatus.MarksOpen, ExamStatus.Published, true)]
    [InlineData(ExamStatus.Published, ExamStatus.MarksOpen, true)]
    [InlineData(ExamStatus.Published, ExamStatus.Locked, true)]
    [InlineData(ExamStatus.Planned, ExamStatus.Published, false)]
    [InlineData(ExamStatus.MarksOpen, ExamStatus.Locked, false)]
    [InlineData(ExamStatus.Locked, ExamStatus.MarksOpen, false)]
    [InlineData(ExamStatus.Locked, ExamStatus.Published, false)]
    public void An_exam_moves_only_forward_except_to_correct_a_published_one(ExamStatus from, ExamStatus to, bool allowed) =>
        Assert.Equal(allowed, ExamService.CanMove(from, to));

    [Fact]
    public void Subjects_are_needed_once_per_class_with_pass_within_max()
    {
        Assert.NotNull(ExamService.CheckSubjects([]));
        Assert.NotNull(ExamService.CheckSubjects(
        [
            new() { SubjectId = 1, SchoolClassId = 1, MaxMarks = 100, PassMarks = 35 },
            new() { SubjectId = 1, SchoolClassId = 1, MaxMarks = 50, PassMarks = 20 },
        ]));
        Assert.NotNull(ExamService.CheckSubjects([new() { SubjectId = 1, SchoolClassId = 1, MaxMarks = 50, PassMarks = 60 }]));
        Assert.Null(ExamService.CheckSubjects(
        [
            new() { SubjectId = 1, SchoolClassId = 1, MaxMarks = 100, PassMarks = 35 },
            new() { SubjectId = 1, SchoolClassId = 2, MaxMarks = 100, PassMarks = 35 },
        ]));
    }

    [Theory]
    [InlineData(null, true, true)]
    [InlineData(null, false, false)]
    [InlineData(-1.0, false, false)]
    [InlineData(0.0, false, true)]
    [InlineData(100.0, false, true)]
    [InlineData(100.5, false, false)]
    public void A_mark_is_absent_or_between_zero_and_the_maximum(double? marks, bool absent, bool valid) =>
        Assert.Equal(valid, ExamService.CheckMark(new MarkRow { EnrolmentId = 1, Marks = (decimal?)marks, IsAbsent = absent }, 100m) is null);
}

/// <summary>Marks against a real database: only while open, only for the class's students.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class ExamServiceTests
{
    private readonly PostgresFixture _postgres;

    public ExamServiceTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task Marks_are_taken_only_while_the_exam_is_open_and_published_marks_can_be_read_back()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid orgId = Guid.NewGuid();
        await using StudentDbContext db = _postgres.CreateContext(Guid.NewGuid(), orgId);
        await new StudentSeeder(db).SeedForOrganizationAsync(orgId, default);

        var academic = new AcademicService(db);
        long year = (await academic.SaveYearAsync(null, new SaveAcademicYearRequest
        {
            Code = "2026-27", StartDate = new DateOnly(2026, 6, 1), EndDate = new DateOnly(2027, 3, 31), IsCurrent = true,
        }, default)).Id!.Value;
        long classVi = await db.SchoolClasses.Where(c => c.Code == "VI").Select(c => c.SchoolClassId).SingleAsync();
        long section = (await academic.SaveSectionAsync(null, new SaveSectionRequest { AcademicYearId = year, SchoolClassId = classVi, Name = "A" }, default)).Id!.Value;
        long maths = (await academic.SaveSubjectAsync(null, new SaveSubjectRequest { Code = "MAT", Name = "Mathematics" }, default)).Id!.Value;

        // A student straight into the table: the student service is tested on its own.
        var student = new Student.Entity.TableEntities.StudentRecord
        {
            AdmissionNo = "ADM-1", FirstName = "Arun", DateOfBirth = new DateOnly(2015, 2, 10), AdmissionDate = new DateOnly(2026, 6, 1),
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();
        var enrolment = new Student.Entity.TableEntities.Enrolment { StudentId = student.StudentId, AcademicYearId = year, SectionId = section, RollNo = 1 };
        db.Enrolments.Add(enrolment);
        await db.SaveChangesAsync();

        var exams = new ExamService(db);
        long exam = (await exams.SaveAsync(null, new SaveExamRequest
        {
            AcademicYearId = year, Name = "Term 1", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 9, 10),
            Subjects = [new() { SubjectId = maths, SchoolClassId = classVi, MaxMarks = 100, PassMarks = 35 }],
        }, default)).Id!.Value;
        long examSubject = await db.ExamSubjects.Where(s => s.ExamId == exam).Select(s => s.ExamSubjectId).SingleAsync();
        var marks = new SaveMarksRequest { ExamSubjectId = examSubject, Marks = [new() { EnrolmentId = enrolment.EnrolmentId, Marks = 78 }] };

        Assert.Equal(StudentOutcome.ExamState, (await exams.SaveMarksAsync(exam, marks, default)).Outcome);

        await exams.MoveAsync(exam, ExamStatus.MarksOpen, default);
        Assert.Equal(StudentOutcome.Ok, (await exams.SaveMarksAsync(exam, marks, default)).Outcome);

        // Entering again updates the one row.
        marks.Marks[0].Marks = 81;
        await exams.SaveMarksAsync(exam, marks, default);
        Assert.Equal(81m, (await exams.SheetAsync(examSubject, section, default))!.Single().Marks);

        await exams.MoveAsync(exam, ExamStatus.Published, default);
        Assert.Equal(StudentOutcome.ExamState, (await exams.SaveMarksAsync(exam, marks, default)).Outcome);
        Assert.Equal(StudentOutcome.ExamState, (await exams.SaveAsync(exam, new SaveExamRequest
        {
            AcademicYearId = year, Name = "Term 1 changed", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 9, 10),
            Subjects = [new() { SubjectId = maths, SchoolClassId = classVi, MaxMarks = 100, PassMarks = 35 }],
        }, default)).Outcome);
    }
}

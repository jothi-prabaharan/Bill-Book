using Microsoft.EntityFrameworkCore;
using Sis.Entity.Enums;
using Sis.Entity.Models;
using Sis.Entity.TableEntities;
using Sis.Repository;

namespace Sis.Api.Services;

/// <summary>
/// Exams, their subjects per class, and marks (S1, TK-61).
///
/// An exam moves Planned → MarksOpen → Published → Locked. Marks are written
/// only while MarksOpen; Published is what the parent portal shows; Locked is
/// final. A published exam may be reopened for a correction, a locked one never.
/// </summary>
public sealed class ExamService
{
    private readonly SisDbContext _db;

    public ExamService(SisDbContext db) => _db = db;

    public async Task<List<ExamView>> ListAsync(long? academicYearId, CancellationToken ct)
    {
        List<Exam> exams = await _db.Exams.AsNoTracking().Include(e => e.Subjects)
            .Where(e => academicYearId == null || e.AcademicYearId == academicYearId)
            .OrderByDescending(e => e.StartDate)
            .ToListAsync(ct);

        return [.. exams.Select(View)];
    }

    public async Task<SisResult> SaveAsync(long? id, SaveExamRequest request, CancellationToken ct)
    {
        if (request.EndDate < request.StartDate)
        {
            return SisResult.Fail(SisOutcome.Invalid, "An exam must end on or after the day it starts.");
        }

        if (CheckSubjects(request.Subjects) is string problem)
        {
            return SisResult.Fail(SisOutcome.Invalid, problem);
        }

        AcademicYear? year = await _db.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.AcademicYearId == request.AcademicYearId, ct);
        if (year is null)
        {
            return SisResult.Fail(SisOutcome.Invalid, "Choose a school year from this branch.");
        }

        if (year.IsClosed)
        {
            return SisResult.Fail(SisOutcome.YearRule, "That school year is closed.");
        }

        HashSet<long> subjectIds = [.. request.Subjects.Select(s => s.SubjectId)];
        HashSet<long> classIds = [.. request.Subjects.Select(s => s.SchoolClassId)];
        if (await _db.Subjects.CountAsync(s => subjectIds.Contains(s.SubjectId), ct) != subjectIds.Count
            || await _db.SchoolClasses.CountAsync(c => classIds.Contains(c.SchoolClassId), ct) != classIds.Count)
        {
            return SisResult.Fail(SisOutcome.Invalid, "Choose subjects and classes from this branch.");
        }

        Exam? exam = id is long existing
            ? await _db.Exams.Include(e => e.Subjects).FirstOrDefaultAsync(e => e.ExamId == existing, ct)
            : new Exam();
        if (exam is null)
        {
            return SisResult.Fail(SisOutcome.NotFound);
        }

        // The subjects are the exam's structure: fixed once marks can be entered.
        if (id is not null && exam.ExamStatus != ExamStatus.Planned)
        {
            return SisResult.Fail(SisOutcome.ExamState, "An exam can be changed only while it is planned.");
        }

        exam.AcademicYearId = request.AcademicYearId;
        exam.Name = request.Name.Trim();
        exam.StartDate = request.StartDate;
        exam.EndDate = request.EndDate;

        if (id is null)
        {
            _db.Exams.Add(exam);
        }
        else
        {
            _db.ExamSubjects.RemoveRange(exam.Subjects);
            await _db.SaveChangesAsync(ct);
            exam.Subjects.Clear();
        }

        foreach (ExamSubjectModel s in request.Subjects)
        {
            exam.Subjects.Add(new ExamSubject { SubjectId = s.SubjectId, SchoolClassId = s.SchoolClassId, MaxMarks = s.MaxMarks, PassMarks = s.PassMarks });
        }

        await _db.SaveChangesAsync(ct);
        return SisResult.Ok(exam.ExamId);
    }

    public async Task<SisResult> MoveAsync(long id, ExamStatus to, CancellationToken ct)
    {
        Exam? exam = await _db.Exams.FirstOrDefaultAsync(e => e.ExamId == id, ct);
        if (exam is null)
        {
            return SisResult.Fail(SisOutcome.NotFound);
        }

        if (!CanMove(exam.ExamStatus, to))
        {
            return SisResult.Fail(SisOutcome.ExamState, $"An exam that is {Words(exam.ExamStatus)} cannot be made {Words(to)}.");
        }

        exam.ExamStatus = to;
        await _db.SaveChangesAsync(ct);
        return SisResult.Ok(exam.ExamId);
    }

    /// <summary>The marks sheet for one exam subject and one section: every student on the roll, with marks where entered.</summary>
    public async Task<List<MarkRow>?> SheetAsync(long examSubjectId, long sectionId, CancellationToken ct)
    {
        ExamSubject? subject = await _db.ExamSubjects.AsNoTracking().FirstOrDefaultAsync(s => s.ExamSubjectId == examSubjectId, ct);
        if (subject is null)
        {
            return null;
        }

        var rows = await (
            from en in _db.Enrolments.AsNoTracking()
            join st in _db.Students on en.StudentId equals st.StudentId
            join mk in _db.ExamMarks.Where(m => m.ExamSubjectId == examSubjectId) on en.EnrolmentId equals mk.EnrolmentId into mks
            from mk in mks.DefaultIfEmpty()
            where en.SectionId == sectionId && en.EnrolmentStatus == EnrolmentStatus.Active
            orderby en.RollNo, st.FirstName
            select new { en.EnrolmentId, en.RollNo, st.FirstName, st.LastName, Marks = mk == null ? null : mk.Marks, IsAbsent = mk != null && mk.IsAbsent })
            .ToListAsync(ct);

        return [.. rows.Select(r => new MarkRow
        {
            EnrolmentId = r.EnrolmentId, RollNo = r.RollNo, StudentName = StudentService.FullName(r.FirstName, r.LastName),
            Marks = r.Marks, IsAbsent = r.IsAbsent,
        })];
    }

    public async Task<SisResult> SaveMarksAsync(long examId, SaveMarksRequest request, CancellationToken ct)
    {
        Exam? exam = await _db.Exams.AsNoTracking().FirstOrDefaultAsync(e => e.ExamId == examId, ct);
        ExamSubject? subject = await _db.ExamSubjects.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ExamSubjectId == request.ExamSubjectId && s.ExamId == examId, ct);
        if (exam is null || subject is null)
        {
            return SisResult.Fail(SisOutcome.NotFound);
        }

        if (exam.ExamStatus != ExamStatus.MarksOpen)
        {
            return SisResult.Fail(SisOutcome.ExamState, "Marks can be entered only while the exam is open for marks.");
        }

        if (request.Marks.FirstOrDefault(m => CheckMark(m, subject.MaxMarks) is not null) is MarkRow bad)
        {
            return SisResult.Fail(SisOutcome.Invalid, CheckMark(bad, subject.MaxMarks));
        }

        // Every student marked must be enrolled in that class for the exam's year.
        HashSet<long> enrolmentIds = [.. request.Marks.Select(m => m.EnrolmentId)];
        int eligible = await (
            from en in _db.Enrolments
            join se in _db.Sections on en.SectionId equals se.SectionId
            where enrolmentIds.Contains(en.EnrolmentId)
                && en.AcademicYearId == exam.AcademicYearId
                && se.SchoolClassId == subject.SchoolClassId
            select en.EnrolmentId).CountAsync(ct);
        if (eligible != enrolmentIds.Count)
        {
            return SisResult.Fail(SisOutcome.Invalid, "Marks can be entered only for students of that class in the exam's year.");
        }

        Dictionary<long, ExamMark> existing = await _db.ExamMarks
            .Where(m => m.ExamSubjectId == subject.ExamSubjectId && enrolmentIds.Contains(m.EnrolmentId))
            .ToDictionaryAsync(m => m.EnrolmentId, ct);

        foreach (MarkRow row in request.Marks)
        {
            if (!existing.TryGetValue(row.EnrolmentId, out ExamMark? mark))
            {
                mark = new ExamMark { ExamSubjectId = subject.ExamSubjectId, EnrolmentId = row.EnrolmentId };
                _db.ExamMarks.Add(mark);
            }

            mark.IsAbsent = row.IsAbsent;
            mark.Marks = row.IsAbsent ? null : row.Marks;
        }

        await _db.SaveChangesAsync(ct);
        return SisResult.Ok(exam.ExamId);
    }

    // ---- Rules, pure and public for tests -----------------------------------

    /// <summary>The moves an exam may make.</summary>
    public static bool CanMove(ExamStatus from, ExamStatus to) => (from, to) switch
    {
        (ExamStatus.Planned, ExamStatus.MarksOpen) => true,
        (ExamStatus.MarksOpen, ExamStatus.Published) => true,
        (ExamStatus.Published, ExamStatus.MarksOpen) => true,
        (ExamStatus.Published, ExamStatus.Locked) => true,
        _ => false,
    };

    /// <summary>A subject once per class, and pass marks within the maximum.</summary>
    public static string? CheckSubjects(IReadOnlyCollection<ExamSubjectModel> subjects)
    {
        if (subjects.Count == 0)
        {
            return "Add at least one subject.";
        }

        if (subjects.GroupBy(s => (s.SubjectId, s.SchoolClassId)).Any(g => g.Count() > 1))
        {
            return "A subject is listed twice for the same class.";
        }

        return subjects.Any(s => s.PassMarks > s.MaxMarks) ? "Pass marks cannot be more than the maximum." : null;
    }

    /// <summary>A mark is absent with no marks, or present with marks from zero to the maximum.</summary>
    public static string? CheckMark(MarkRow row, decimal maxMarks)
    {
        if (row.IsAbsent)
        {
            return null;
        }

        return row.Marks is decimal marks && marks >= 0 && marks <= maxMarks
            ? null
            : "Enter marks between zero and the maximum, or mark the student absent.";
    }

    private static string Words(ExamStatus status) => status switch
    {
        ExamStatus.Planned => "planned",
        ExamStatus.MarksOpen => "open for marks",
        ExamStatus.Published => "published",
        _ => "locked",
    };

    private static ExamView View(Exam e) => new()
    {
        ExamId = e.ExamId,
        AcademicYearId = e.AcademicYearId,
        Name = e.Name,
        StartDate = e.StartDate,
        EndDate = e.EndDate,
        ExamStatus = e.ExamStatus,
        Subjects = [.. e.Subjects.Select(s => new ExamSubjectModel
        {
            ExamSubjectId = s.ExamSubjectId, SubjectId = s.SubjectId, SchoolClassId = s.SchoolClassId,
            MaxMarks = s.MaxMarks, PassMarks = s.PassMarks,
        })],
    };
}

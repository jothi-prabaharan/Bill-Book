using Microsoft.EntityFrameworkCore;
using Sis.Entity.Enums;
using Sis.Entity.Models;
using Sis.Repository;

namespace Sis.Api.Services;

/// <summary>
/// What a guardian sees of their children in the parent portal (S9, TK-69).
/// A child is theirs when a <c>StudentGuardian</c> row links the two and grants
/// portal access; anything else answers as not found. Marks show only for
/// published or locked exams, never while teachers are still entering them.
/// </summary>
public sealed class PortalService
{
    private readonly SisDbContext _db;

    public PortalService(SisDbContext db) => _db = db;

    public static readonly ExamStatus[] Visible = [ExamStatus.Published, ExamStatus.Locked];

    public async Task<List<PortalChildView>> ChildrenAsync(long contactId, CancellationToken ct)
    {
        var students = await (
            from g in _db.StudentGuardians.AsNoTracking()
            join st in _db.Students on g.StudentId equals st.StudentId
            where g.ContactId == contactId && g.HasPortalAccess
            orderby st.FirstName, st.LastName
            select new { st.StudentId, st.FirstName, st.LastName, st.AdmissionNo, st.StudentStatus })
            .ToListAsync(ct);

        List<long> ids = [.. students.Select(s => s.StudentId)];
        var latest = (await (
                from en in _db.Enrolments.AsNoTracking()
                join se in _db.Sections on en.SectionId equals se.SectionId
                join cl in _db.SchoolClasses on se.SchoolClassId equals cl.SchoolClassId
                join ay in _db.AcademicYears on en.AcademicYearId equals ay.AcademicYearId
                where ids.Contains(en.StudentId)
                select new { en.StudentId, ay.StartDate, YearCode = ay.Code, ClassName = cl.Name, SectionName = se.Name })
                .ToListAsync(ct))
            .GroupBy(e => e.StudentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.StartDate).First());

        return [.. students.Select(s => new PortalChildView
        {
            StudentId = s.StudentId,
            StudentName = StudentService.FullName(s.FirstName, s.LastName),
            AdmissionNo = s.AdmissionNo,
            ClassName = latest.GetValueOrDefault(s.StudentId)?.ClassName,
            SectionName = latest.GetValueOrDefault(s.StudentId)?.SectionName,
            AcademicYearCode = latest.GetValueOrDefault(s.StudentId)?.YearCode,
            IsActive = s.StudentStatus == StudentStatus.Active,
        })];
    }

    /// <summary>The child's marks in every published or locked exam, newest first; null when the child is not this guardian's.</summary>
    public async Task<List<PortalExamView>?> MarksAsync(long contactId, long studentId, CancellationToken ct)
    {
        if (!await IsTheirsAsync(contactId, studentId, ct))
        {
            return null;
        }

        var rows = await (
            from m in _db.ExamMarks.AsNoTracking()
            join en in _db.Enrolments on m.EnrolmentId equals en.EnrolmentId
            join es in _db.ExamSubjects on m.ExamSubjectId equals es.ExamSubjectId
            join ex in _db.Exams on es.ExamId equals ex.ExamId
            join su in _db.Subjects on es.SubjectId equals su.SubjectId
            join ay in _db.AcademicYears on ex.AcademicYearId equals ay.AcademicYearId
            where en.StudentId == studentId && Visible.Contains(ex.ExamStatus)
            select new
            {
                ex.ExamId, ExamName = ex.Name, YearCode = ay.Code, ex.StartDate, ex.EndDate,
                SubjectName = su.Name, es.MaxMarks, es.PassMarks, m.Marks, m.IsAbsent,
            })
            .ToListAsync(ct);

        return [.. rows
            .GroupBy(r => new { r.ExamId, r.ExamName, r.YearCode, r.StartDate, r.EndDate })
            .OrderByDescending(g => g.Key.StartDate)
            .Select(g => new PortalExamView
            {
                ExamId = g.Key.ExamId,
                ExamName = g.Key.ExamName,
                AcademicYearCode = g.Key.YearCode,
                StartDate = g.Key.StartDate,
                EndDate = g.Key.EndDate,
                Subjects = [.. g.OrderBy(r => r.SubjectName).Select(r => new PortalMarkView
                {
                    SubjectName = r.SubjectName,
                    MaxMarks = r.MaxMarks,
                    PassMarks = r.PassMarks,
                    Marks = r.IsAbsent ? null : r.Marks,
                    IsAbsent = r.IsAbsent,
                    Passed = !r.IsAbsent && r.Marks is decimal marks && marks >= r.PassMarks,
                })],
            })];
    }

    private Task<bool> IsTheirsAsync(long contactId, long studentId, CancellationToken ct) =>
        _db.StudentGuardians.AnyAsync(g => g.StudentId == studentId && g.ContactId == contactId && g.HasPortalAccess, ct);
}

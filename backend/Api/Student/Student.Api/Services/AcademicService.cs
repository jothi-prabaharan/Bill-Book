using Microsoft.EntityFrameworkCore;
using Student.Entity.Models;
using Student.Entity.TableEntities;
using Student.Repository;

namespace Student.Api.Services;

/// <summary>
/// Academic years, classes, sections and subjects (S1, TK-61). Codes are unique
/// per branch; one year is current; years in a branch never overlap; a section
/// belongs to one year and one class.
/// </summary>
public sealed class AcademicService
{
    private readonly StudentDbContext _db;

    public AcademicService(StudentDbContext db) => _db = db;

    // ---- Years ---------------------------------------------------------------

    public Task<List<AcademicYearView>> YearsAsync(CancellationToken ct) =>
        _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate)
            .Select(y => new AcademicYearView
            {
                AcademicYearId = y.AcademicYearId, Code = y.Code, StartDate = y.StartDate,
                EndDate = y.EndDate, IsCurrent = y.IsCurrent, IsClosed = y.IsClosed,
            })
            .ToListAsync(ct);

    public async Task<StudentResult> SaveYearAsync(long? id, SaveAcademicYearRequest request, CancellationToken ct)
    {
        if (request.EndDate <= request.StartDate)
        {
            return StudentResult.Fail(StudentOutcome.YearRule, "A school year must end after it starts.");
        }

        string code = request.Code.Trim();
        if (await _db.AcademicYears.AnyAsync(y => y.AcademicYearId != (id ?? 0) && y.Code == code, ct))
        {
            return StudentResult.Fail(StudentOutcome.Duplicate);
        }

        List<(DateOnly Start, DateOnly End)> others = await _db.AcademicYears
            .Where(y => y.AcademicYearId != (id ?? 0))
            .Select(y => new ValueTuple<DateOnly, DateOnly>(y.StartDate, y.EndDate))
            .ToListAsync(ct);
        if (Overlaps(request.StartDate, request.EndDate, others))
        {
            return StudentResult.Fail(StudentOutcome.YearRule, "School years in a branch cannot overlap.");
        }

        AcademicYear? year = id is long existing
            ? await _db.AcademicYears.FirstOrDefaultAsync(y => y.AcademicYearId == existing, ct)
            : new AcademicYear();
        if (year is null)
        {
            return StudentResult.Fail(StudentOutcome.NotFound);
        }

        if (request.IsCurrent && request.IsClosed)
        {
            return StudentResult.Fail(StudentOutcome.YearRule, "The current school year cannot be closed.");
        }

        // Making this year current makes every other one not current, in the
        // same transaction, so the one-current index never sees two.
        if (request.IsCurrent && !year.IsCurrent)
        {
            await _db.AcademicYears
                .Where(y => y.IsCurrent && y.AcademicYearId != (id ?? 0))
                .ExecuteUpdateAsync(s => s.SetProperty(y => y.IsCurrent, false), ct);
        }

        year.Code = code;
        year.StartDate = request.StartDate;
        year.EndDate = request.EndDate;
        year.IsCurrent = request.IsCurrent;
        year.IsClosed = request.IsClosed;

        if (id is null)
        {
            _db.AcademicYears.Add(year);
        }

        await _db.SaveChangesAsync(ct);
        return StudentResult.Ok(year.AcademicYearId);
    }

    /// <summary>Whether a date range meets any other. Inclusive at both ends. Public for tests.</summary>
    public static bool Overlaps(DateOnly start, DateOnly end, IEnumerable<(DateOnly Start, DateOnly End)> others) =>
        others.Any(o => start <= o.End && o.Start <= end);

    // ---- Classes -------------------------------------------------------------

    public Task<List<SchoolClassView>> ClassesAsync(CancellationToken ct) =>
        _db.SchoolClasses.AsNoTracking().OrderBy(c => c.SortOrder)
            .Select(c => new SchoolClassView { SchoolClassId = c.SchoolClassId, Code = c.Code, Name = c.Name, SortOrder = c.SortOrder, IsActive = c.IsActive })
            .ToListAsync(ct);

    public async Task<StudentResult> SaveClassAsync(long? id, SaveSchoolClassRequest request, CancellationToken ct)
    {
        string code = request.Code.Trim();
        if (await _db.SchoolClasses.AnyAsync(c => c.SchoolClassId != (id ?? 0) && c.Code == code, ct))
        {
            return StudentResult.Fail(StudentOutcome.Duplicate);
        }

        SchoolClass? row = id is long existing
            ? await _db.SchoolClasses.FirstOrDefaultAsync(c => c.SchoolClassId == existing, ct)
            : new SchoolClass();
        if (row is null)
        {
            return StudentResult.Fail(StudentOutcome.NotFound);
        }

        row.Code = code;
        row.Name = request.Name.Trim();
        row.SortOrder = request.SortOrder;
        row.IsActive = request.IsActive;
        if (id is null)
        {
            _db.SchoolClasses.Add(row);
        }

        await _db.SaveChangesAsync(ct);
        return StudentResult.Ok(row.SchoolClassId);
    }

    // ---- Sections ------------------------------------------------------------

    public Task<List<SectionView>> SectionsAsync(long? academicYearId, CancellationToken ct) =>
        (from s in _db.Sections.AsNoTracking()
         join y in _db.AcademicYears on s.AcademicYearId equals y.AcademicYearId
         join c in _db.SchoolClasses on s.SchoolClassId equals c.SchoolClassId
         where academicYearId == null || s.AcademicYearId == academicYearId
         orderby c.SortOrder, s.Name
         select new SectionView
         {
             SectionId = s.SectionId,
             AcademicYearId = s.AcademicYearId,
             AcademicYearCode = y.Code,
             SchoolClassId = s.SchoolClassId,
             ClassName = c.Name,
             Name = s.Name,
             Capacity = s.Capacity,
             Enrolled = _db.Enrolments.Count(e => e.SectionId == s.SectionId && e.EnrolmentStatus == Entity.Enums.EnrolmentStatus.Active),
             ClassTeacherEmployeeId = s.ClassTeacherEmployeeId,
             RoomSpaceId = s.RoomSpaceId,
         }).ToListAsync(ct);

    public async Task<StudentResult> SaveSectionAsync(long? id, SaveSectionRequest request, CancellationToken ct)
    {
        AcademicYear? year = await _db.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.AcademicYearId == request.AcademicYearId, ct);
        if (year is null || !await _db.SchoolClasses.AnyAsync(c => c.SchoolClassId == request.SchoolClassId, ct))
        {
            return StudentResult.Fail(StudentOutcome.Invalid, "Choose a school year and a class from this branch.");
        }

        if (year.IsClosed)
        {
            return StudentResult.Fail(StudentOutcome.YearRule, "That school year is closed.");
        }

        string name = request.Name.Trim();
        if (await _db.Sections.AnyAsync(s => s.SectionId != (id ?? 0) && s.AcademicYearId == request.AcademicYearId
            && s.SchoolClassId == request.SchoolClassId && s.Name == name, ct))
        {
            return StudentResult.Fail(StudentOutcome.Duplicate);
        }

        Section? row = id is long existing
            ? await _db.Sections.FirstOrDefaultAsync(s => s.SectionId == existing, ct)
            : new Section();
        if (row is null)
        {
            return StudentResult.Fail(StudentOutcome.NotFound);
        }

        // A section with students keeps its year and class; moving it would
        // move every enrolment in it silently.
        if (id is not null && (row.AcademicYearId != request.AcademicYearId || row.SchoolClassId != request.SchoolClassId)
            && await _db.Enrolments.AnyAsync(e => e.SectionId == row.SectionId, ct))
        {
            return StudentResult.Fail(StudentOutcome.YearRule, "A section with students cannot move to another year or class.");
        }

        if (request.Capacity is int capacity
            && await _db.Enrolments.CountAsync(e => e.SectionId == row.SectionId && e.EnrolmentStatus == Entity.Enums.EnrolmentStatus.Active, ct) > capacity)
        {
            return StudentResult.Fail(StudentOutcome.SectionFull, "The section already has more students than that capacity.");
        }

        row.AcademicYearId = request.AcademicYearId;
        row.SchoolClassId = request.SchoolClassId;
        row.Name = name;
        row.Capacity = request.Capacity;
        row.ClassTeacherEmployeeId = request.ClassTeacherEmployeeId;
        row.RoomSpaceId = request.RoomSpaceId;
        if (id is null)
        {
            _db.Sections.Add(row);
        }

        await _db.SaveChangesAsync(ct);
        return StudentResult.Ok(row.SectionId);
    }

    // ---- Subjects ------------------------------------------------------------

    public Task<List<SubjectView>> SubjectsAsync(CancellationToken ct) =>
        _db.Subjects.AsNoTracking().OrderBy(s => s.Code)
            .Select(s => new SubjectView { SubjectId = s.SubjectId, Code = s.Code, Name = s.Name, SubjectKind = s.SubjectKind, IsActive = s.IsActive })
            .ToListAsync(ct);

    public async Task<StudentResult> SaveSubjectAsync(long? id, SaveSubjectRequest request, CancellationToken ct)
    {
        string code = request.Code.Trim();
        if (await _db.Subjects.AnyAsync(s => s.SubjectId != (id ?? 0) && s.Code == code, ct))
        {
            return StudentResult.Fail(StudentOutcome.Duplicate);
        }

        Subject? row = id is long existing
            ? await _db.Subjects.FirstOrDefaultAsync(s => s.SubjectId == existing, ct)
            : new Subject();
        if (row is null)
        {
            return StudentResult.Fail(StudentOutcome.NotFound);
        }

        row.Code = code;
        row.Name = request.Name.Trim();
        row.SubjectKind = request.SubjectKind;
        row.IsActive = request.IsActive;
        if (id is null)
        {
            _db.Subjects.Add(row);
        }

        await _db.SaveChangesAsync(ct);
        return StudentResult.Ok(row.SubjectId);
    }
}

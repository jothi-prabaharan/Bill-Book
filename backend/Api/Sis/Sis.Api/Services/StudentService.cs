using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Contacts;
using Shared.Kernel.Numbering;
using Sis.Entity.Enums;
using Sis.Entity.Models;
using Sis.Entity.TableEntities;
using Sis.Repository;
using Sis.Repository.SeedData;

namespace Sis.Api.Services;

/// <summary>
/// Students, their guardians and their enrolments (S1, TK-61).
///
/// <list type="bullet">
/// <item><b>Guardians are contacts</b>, checked through Master: each must be a
/// guardian contact of this branch and active, one or two per student, exactly
/// one primary.</item>
/// <item><b>The admission number comes from ADM</b>, in the same transaction
/// as the student, so a refused save gives it back.</item>
/// <item><b>An enrolment</b> is one per student per year, into a section of
/// that year, never past its capacity, never into a closed year.</item>
/// </list>
/// </summary>
public sealed class StudentService
{
    public const int MaxGuardians = 2;

    private readonly SisDbContext _db;
    private readonly INumberGenerator _numbers;
    private readonly IContactDirectory _contacts;
    private readonly ILogger<StudentService> _log;

    public StudentService(SisDbContext db, INumberGenerator numbers, IContactDirectory contacts, ILogger<StudentService> log)
    {
        _db = db;
        _numbers = numbers;
        _contacts = contacts;
        _log = log;
    }

    public async Task<List<StudentListItem>> ListAsync(string? search, StudentStatus? status, long? sectionId, CancellationToken ct)
    {
        long? currentYear = await _db.AcademicYears.Where(y => y.IsCurrent).Select(y => (long?)y.AcademicYearId).FirstOrDefaultAsync(ct);

        IQueryable<Student> students = _db.Students.AsNoTracking();
        if (status is StudentStatus s)
        {
            students = students.Where(x => x.StudentStatus == s);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = $"%{search.Trim()}%";
            students = students.Where(x => EF.Functions.ILike(x.FirstName, term)
                || (x.LastName != null && EF.Functions.ILike(x.LastName, term))
                || EF.Functions.ILike(x.AdmissionNo, term));
        }

        var rows = await (
            from st in students
            join en in _db.Enrolments.Where(e => sectionId != null ? e.SectionId == sectionId : e.AcademicYearId == currentYear)
                on st.StudentId equals en.StudentId into ens
            from en in ens.DefaultIfEmpty()
            join se in _db.Sections on en.SectionId equals se.SectionId into ses
            from se in ses.DefaultIfEmpty()
            join cl in _db.SchoolClasses on se.SchoolClassId equals cl.SchoolClassId into cls
            from cl in cls.DefaultIfEmpty()
            where sectionId == null || en != null
            orderby cl.SortOrder, se.Name, en.RollNo, st.FirstName
            select new
            {
                st.StudentId, st.AdmissionNo, st.FirstName, st.LastName, st.Gender, st.StudentStatus, st.NationalId,
                ClassName = cl == null ? null : cl.Name,
                SectionName = se == null ? null : se.Name,
                RollNo = en == null ? null : en.RollNo,
            }).Take(1000).ToListAsync(ct);

        return [.. rows.Select(r => new StudentListItem
        {
            StudentId = r.StudentId,
            AdmissionNo = r.AdmissionNo,
            FullName = FullName(r.FirstName, r.LastName),
            Gender = r.Gender,
            StudentStatus = r.StudentStatus,
            ClassName = r.ClassName,
            SectionName = r.SectionName,
            RollNo = r.RollNo,
            NationalId = Mask(r.NationalId),
        })];
    }

    public async Task<StudentView?> GetAsync(long id, CancellationToken ct)
    {
        Student? student = await _db.Students.AsNoTracking().Include(s => s.Guardians).FirstOrDefaultAsync(s => s.StudentId == id, ct);
        if (student is null)
        {
            return null;
        }

        Dictionary<long, ContactSummary> names = await NamesAsync(student.Guardians.Select(g => g.ContactId), ct);

        List<EnrolmentView> enrolments = await (
            from en in _db.Enrolments.AsNoTracking()
            join y in _db.AcademicYears on en.AcademicYearId equals y.AcademicYearId
            join se in _db.Sections on en.SectionId equals se.SectionId
            join cl in _db.SchoolClasses on se.SchoolClassId equals cl.SchoolClassId
            where en.StudentId == id
            orderby y.StartDate descending
            select new EnrolmentView
            {
                EnrolmentId = en.EnrolmentId, AcademicYearId = y.AcademicYearId, AcademicYearCode = y.Code,
                SectionId = se.SectionId, ClassName = cl.Name, SectionName = se.Name, RollNo = en.RollNo,
                EnrolmentStatus = en.EnrolmentStatus,
            }).ToListAsync(ct);

        return new StudentView
        {
            StudentId = student.StudentId,
            AdmissionNo = student.AdmissionNo,
            FirstName = student.FirstName,
            LastName = student.LastName,
            DateOfBirth = student.DateOfBirth,
            Gender = student.Gender,
            AdmissionDate = student.AdmissionDate,
            StudentStatus = student.StudentStatus,
            LeavingDate = student.LeavingDate,
            BloodGroup = student.BloodGroup,
            NationalId = student.NationalId,
            SourceApplicationId = student.SourceApplicationId,
            Guardians = [.. student.Guardians.OrderByDescending(g => g.IsPrimary).Select(g => new StudentGuardianModel
            {
                ContactId = g.ContactId,
                Relationship = g.Relationship,
                IsPrimary = g.IsPrimary,
                HasPortalAccess = g.HasPortalAccess,
                DisplayName = names.TryGetValue(g.ContactId, out ContactSummary? c) ? c.DisplayName : null,
            })],
            Enrolments = enrolments,
        };
    }

    /// <summary>
    /// Creates a student, with an admission number and, optionally, an
    /// enrolment. With <paramref name="sourceApplicationId"/> it is idempotent:
    /// an application admitted twice returns the student the first admit made.
    /// </summary>
    public async Task<SisResult> CreateAsync(SaveStudentRequest request, CancellationToken ct, long? sourceApplicationId = null)
    {
        if (sourceApplicationId is long applicationId
            && await _db.Students.Where(s => s.SourceApplicationId == applicationId).Select(s => (long?)s.StudentId).FirstOrDefaultAsync(ct) is long already)
        {
            return SisResult.Ok(already);
        }

        if (CheckStudent(request) is string problem)
        {
            return SisResult.Fail(SisOutcome.Invalid, problem);
        }

        SisResult? guardians = await CheckGuardiansAsync(request.Guardians, ct);
        if (guardians is not null)
        {
            return guardians;
        }

        NumberAllocation number = await _numbers.NextAsync(SisSeed.AdmissionSeriesCode, request.AdmissionDate, ct);
        var student = new Student
        {
            AdmissionNo = number.Code,
            SourceApplicationId = sourceApplicationId,
        };
        Apply(student, request);
        _db.Students.Add(student);
        await _db.SaveChangesAsync(ct);

        if (request.Enrol is EnrolRequest enrol)
        {
            enrol.StudentId = student.StudentId;
            SisResult enrolled = await EnrolAsync(enrol, ct);
            if (enrolled.Outcome != SisOutcome.Ok)
            {
                // The request's transaction rolls back on a refusal, taking the
                // student and its number with it.
                return enrolled;
            }
        }

        return SisResult.Ok(student.StudentId);
    }

    public async Task<SisResult> UpdateAsync(long id, SaveStudentRequest request, CancellationToken ct)
    {
        Student? student = await _db.Students.Include(s => s.Guardians).FirstOrDefaultAsync(s => s.StudentId == id, ct);
        if (student is null)
        {
            return SisResult.Fail(SisOutcome.NotFound);
        }

        if (CheckStudent(request) is string problem)
        {
            return SisResult.Fail(SisOutcome.Invalid, problem);
        }

        SisResult? guardians = await CheckGuardiansAsync(request.Guardians, ct);
        if (guardians is not null)
        {
            return guardians;
        }

        _db.StudentGuardians.RemoveRange(student.Guardians);
        await _db.SaveChangesAsync(ct);
        student.Guardians.Clear();

        Apply(student, request);
        await _db.SaveChangesAsync(ct);
        return SisResult.Ok(student.StudentId);
    }

    public async Task<SisResult> EnrolAsync(EnrolRequest request, CancellationToken ct)
    {
        Student? student = await _db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.StudentId == request.StudentId, ct);
        if (student is null)
        {
            return SisResult.Fail(SisOutcome.NotFound);
        }

        if (student.StudentStatus != StudentStatus.Active)
        {
            return SisResult.Fail(SisOutcome.Invalid, "Only an active student can be enrolled.");
        }

        AcademicYear? year = await _db.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.AcademicYearId == request.AcademicYearId, ct);
        Section? section = await _db.Sections.AsNoTracking().FirstOrDefaultAsync(s => s.SectionId == request.SectionId, ct);
        if (year is null || section is null)
        {
            return SisResult.Fail(SisOutcome.Invalid, "Choose a school year and a section from this branch.");
        }

        if (section.AcademicYearId != year.AcademicYearId)
        {
            return SisResult.Fail(SisOutcome.YearRule, "The section belongs to another school year.");
        }

        if (year.IsClosed)
        {
            return SisResult.Fail(SisOutcome.YearRule, "That school year is closed.");
        }

        if (await _db.Enrolments.AnyAsync(e => e.StudentId == request.StudentId && e.AcademicYearId == request.AcademicYearId, ct))
        {
            return SisResult.Fail(SisOutcome.Duplicate, "The student is already enrolled in that school year.");
        }

        int enrolled = await _db.Enrolments.CountAsync(e => e.SectionId == section.SectionId && e.EnrolmentStatus == EnrolmentStatus.Active, ct);
        if (IsFull(section.Capacity, enrolled))
        {
            return SisResult.Fail(SisOutcome.SectionFull, "The section is full.");
        }

        if (request.RollNo is int roll && await _db.Enrolments.AnyAsync(e => e.SectionId == section.SectionId && e.RollNo == roll, ct))
        {
            return SisResult.Fail(SisOutcome.Duplicate, "Another student in the section has that roll number.");
        }

        var enrolment = new Enrolment
        {
            StudentId = request.StudentId,
            AcademicYearId = request.AcademicYearId,
            SectionId = request.SectionId,
            RollNo = request.RollNo,
        };
        _db.Enrolments.Add(enrolment);
        await _db.SaveChangesAsync(ct);
        return SisResult.Ok(enrolment.EnrolmentId);
    }

    /// <summary>Who is in a section, by roll number: the attendance register's and the fee run's list.</summary>
    public async Task<List<RollEntry>> RollAsync(long sectionId, CancellationToken ct)
    {
        var rows = await (
            from en in _db.Enrolments.AsNoTracking()
            join st in _db.Students on en.StudentId equals st.StudentId
            where en.SectionId == sectionId && en.EnrolmentStatus == EnrolmentStatus.Active && st.StudentStatus == StudentStatus.Active
            orderby en.RollNo, st.FirstName
            select new { en.EnrolmentId, st.StudentId, st.AdmissionNo, st.FirstName, st.LastName, en.RollNo })
            .ToListAsync(ct);

        return [.. rows.Select(r => new RollEntry
        {
            EnrolmentId = r.EnrolmentId, StudentId = r.StudentId, AdmissionNo = r.AdmissionNo,
            FullName = FullName(r.FirstName, r.LastName), RollNo = r.RollNo,
        })];
    }

    // ---- Rules, pure and public for tests -----------------------------------

    /// <summary>A student's own rules; null when none is broken.</summary>
    public static string? CheckStudent(SaveStudentRequest request)
    {
        if (request.DateOfBirth >= request.AdmissionDate)
        {
            return "The date of birth must be before the admission date.";
        }

        if (request.StudentStatus != StudentStatus.Active && request.LeavingDate is null)
        {
            return "A student who has left needs a leaving date.";
        }

        if (request.LeavingDate is DateOnly leaving && leaving < request.AdmissionDate)
        {
            return "The leaving date cannot be before the admission date.";
        }

        return CheckGuardianShape(request.Guardians);
    }

    /// <summary>One or two guardians, no contact twice, exactly one primary.</summary>
    public static string? CheckGuardianShape(IReadOnlyCollection<StudentGuardianModel> guardians)
    {
        if (guardians.Count is 0 or > MaxGuardians)
        {
            return "A student needs one or two guardians.";
        }

        if (guardians.Select(g => g.ContactId).Distinct().Count() != guardians.Count)
        {
            return "The same guardian is listed twice.";
        }

        return guardians.Count(g => g.IsPrimary) == 1
            ? null
            : "Mark exactly one guardian as primary. The primary guardian receives the fee demands.";
    }

    /// <summary>Whether a section of this capacity is full at this many students. No capacity means never full.</summary>
    public static bool IsFull(int? capacity, int enrolled) => capacity is int c && enrolled >= c;

    /// <summary>The last four characters, the rest hidden. Public for tests.</summary>
    public static string? Mask(string? value) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= 4 ? new string('•', value.Length) : new string('•', value.Length - 4) + value[^4..];

    public static string FullName(string first, string? last) =>
        string.IsNullOrWhiteSpace(last) ? first : $"{first} {last}";

    // ---- Helpers ---------------------------------------------------------------

    private async Task<SisResult?> CheckGuardiansAsync(IReadOnlyCollection<StudentGuardianModel> guardians, CancellationToken ct)
    {
        IReadOnlyDictionary<long, ContactSummary> found;
        try
        {
            found = await _contacts.FindAsync(guardians.Select(g => g.ContactId), ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogError(ex, "Guardian contacts could not be checked.");
            return SisResult.Fail(SisOutcome.Unavailable);
        }

        return guardians.All(g => found.TryGetValue(g.ContactId, out ContactSummary? c) && c.IsGuardian && c.IsActive)
            ? null
            : SisResult.Fail(SisOutcome.GuardianRule, "Each guardian must be an active contact of this branch marked as a guardian.");
    }

    private async Task<Dictionary<long, ContactSummary>> NamesAsync(IEnumerable<long> ids, CancellationToken ct)
    {
        try
        {
            return new Dictionary<long, ContactSummary>(await _contacts.FindAsync(ids, ct));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Names are for display; the record still reads without them.
            _log.LogWarning(ex, "Guardian names could not be read.");
            return [];
        }
    }

    private static void Apply(Student student, SaveStudentRequest request)
    {
        student.FirstName = request.FirstName.Trim();
        student.LastName = string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName.Trim();
        student.DateOfBirth = request.DateOfBirth;
        student.Gender = request.Gender;
        student.AdmissionDate = request.AdmissionDate;
        student.StudentStatus = request.StudentStatus;
        student.LeavingDate = request.StudentStatus == StudentStatus.Active ? null : request.LeavingDate;
        student.BloodGroup = string.IsNullOrWhiteSpace(request.BloodGroup) ? null : request.BloodGroup.Trim();
        student.NationalId = string.IsNullOrWhiteSpace(request.NationalId) ? null : request.NationalId.Trim();

        foreach (StudentGuardianModel g in request.Guardians)
        {
            student.Guardians.Add(new StudentGuardian
            {
                ContactId = g.ContactId,
                Relationship = g.Relationship,
                IsPrimary = g.IsPrimary,
                HasPortalAccess = g.HasPortalAccess,
            });
        }
    }
}

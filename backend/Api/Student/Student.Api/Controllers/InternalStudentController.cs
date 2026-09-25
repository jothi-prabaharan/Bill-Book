using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Internal;
using Shared.Kernel.School;
using Shared.Kernel.Tenancy;
using Student.Api.Services;
using Student.Entity.Enums;
using Student.Entity.Models;
using Student.Repository;

namespace Student.Api.Controllers;

/// <summary>
/// What the other School services ask Student (TK-62 onward). The branch comes in
/// the body, as on every internal route, and is set before a context is resolved.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/sis")]
public sealed class InternalStudentController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalStudentController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost("academic-check")]
    public async Task<IActionResult> AcademicCheck([FromBody] AcademicCheckRequest request, CancellationToken ct)
    {
        if (!Apply(request.CustomerId, request.OrgId))
        {
            return BadRequest(new StudentMessage("A customer and an organization are required."));
        }

        var db = _services.GetRequiredService<StudentDbContext>();
        var year = request.AcademicYearId is long y
            ? await db.AcademicYears.AsNoTracking().Where(a => a.AcademicYearId == y).Select(a => new { a.IsClosed }).FirstOrDefaultAsync(ct)
            : null;
        var section = request.SectionId is long s
            ? await db.Sections.AsNoTracking().Where(x => x.SectionId == s).Select(x => new { x.AcademicYearId, x.SchoolClassId }).FirstOrDefaultAsync(ct)
            : null;

        return Ok(new AcademicCheckResponse
        {
            YearExists = year is not null,
            YearIsClosed = year?.IsClosed == true,
            ClassExists = request.SchoolClassId is long c && await db.SchoolClasses.AnyAsync(x => x.SchoolClassId == c && x.IsActive, ct),
            SectionExists = section is not null,
            SectionMatches = section is not null
                && (request.AcademicYearId is null || section.AcademicYearId == request.AcademicYearId)
                && (request.SchoolClassId is null || section.SchoolClassId == request.SchoolClassId),
        });
    }

    /// <summary>The student an application admits, created once: a second admit returns the first student.</summary>
    [HttpPost("students/admit")]
    public async Task<IActionResult> Admit([FromBody] AdmitStudentRequest request, CancellationToken ct)
    {
        if (!Apply(request.CustomerId, request.OrgId))
        {
            return BadRequest(new StudentMessage("A customer and an organization are required."));
        }

        if (!Enum.TryParse(request.Gender, out Gender gender) || !Enum.TryParse(request.GuardianRelationship, out GuardianRelationship relationship))
        {
            return UnprocessableEntity(new StudentMessage("The gender or the guardian's relationship is not one this system knows."));
        }

        var students = _services.GetRequiredService<StudentService>();
        StudentResult result = await students.CreateAsync(new SaveStudentRequest
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            DateOfBirth = request.DateOfBirth,
            Gender = gender,
            AdmissionDate = request.AdmissionDate,
            Guardians = [new StudentGuardianModel { ContactId = request.GuardianContactId, Relationship = relationship, IsPrimary = true, HasPortalAccess = true }],
            Enrol = request.SectionId is long section
                ? new EnrolRequest { AcademicYearId = request.AcademicYearId, SectionId = section, RollNo = request.RollNo }
                : null,
        }, ct, request.SourceApplicationId);

        if (result.Outcome != StudentOutcome.Ok)
        {
            return this.Answer(result);
        }

        string number = await _services.GetRequiredService<StudentDbContext>().Students
            .Where(s => s.StudentId == result.Id)
            .Select(s => s.AdmissionNo)
            .SingleAsync(ct);
        return Ok(new AdmitStudentResponse { StudentId = result.Id!.Value, AdmissionNo = number });
    }

    /// <summary>A section's roll and its school year's dates, for the attendance register (S3, TK-63).</summary>
    [HttpPost("sections/roll")]
    public async Task<IActionResult> Roll([FromBody] SectionRollRequest request, CancellationToken ct)
    {
        if (!Apply(request.CustomerId, request.OrgId))
        {
            return BadRequest(new StudentMessage("A customer and an organization are required."));
        }

        var db = _services.GetRequiredService<StudentDbContext>();
        var year = await (
            from s in db.Sections.AsNoTracking()
            join y in db.AcademicYears on s.AcademicYearId equals y.AcademicYearId
            where s.SectionId == request.SectionId
            select new { y.StartDate, y.EndDate, y.IsClosed }).FirstOrDefaultAsync(ct);
        if (year is null)
        {
            return Ok(new SectionRollResponse { SectionExists = false });
        }

        List<RollEntry> roll = await _services.GetRequiredService<StudentService>().RollAsync(request.SectionId, ct);
        return Ok(new SectionRollResponse
        {
            SectionExists = true,
            YearStart = year.StartDate,
            YearEnd = year.EndDate,
            YearIsClosed = year.IsClosed,
            Roll = [.. roll.Select(r => new RollMember
            {
                EnrolmentId = r.EnrolmentId, StudentId = r.StudentId, AdmissionNo = r.AdmissionNo, FullName = r.FullName, RollNo = r.RollNo,
            })],
        });
    }

    /// <summary>Enrolments with their primary guardian (S4, TK-64; S9 by guardian).</summary>
    [HttpPost("enrolments")]
    public async Task<IActionResult> Enrolments([FromBody] EnrolmentQueryRequest request, CancellationToken ct)
    {
        if (!Apply(request.CustomerId, request.OrgId))
        {
            return BadRequest(new StudentMessage("A customer and an organization are required."));
        }

        if (request.AcademicYearId is null && request.EnrolmentIds.Count == 0 && request.GuardianContactId is null)
        {
            return BadRequest(new StudentMessage("Name a school year, enrolments or a guardian."));
        }

        var db = _services.GetRequiredService<StudentDbContext>();
        List<long> ids = [.. request.EnrolmentIds.Distinct().Take(2000)];

        var rows = await (
            from en in db.Enrolments.AsNoTracking()
            join st in db.Students on en.StudentId equals st.StudentId
            join se in db.Sections on en.SectionId equals se.SectionId
            join cl in db.SchoolClasses on se.SchoolClassId equals cl.SchoolClassId
            where (request.AcademicYearId == null || en.AcademicYearId == request.AcademicYearId)
                && (request.SchoolClassId == null || se.SchoolClassId == request.SchoolClassId)
                && (ids.Count == 0 || ids.Contains(en.EnrolmentId))
                && (request.GuardianContactId == null
                    || db.StudentGuardians.Any(g => g.StudentId == st.StudentId && g.ContactId == request.GuardianContactId
                        && (!request.PortalAccessOnly || g.HasPortalAccess)))
            orderby cl.SortOrder, se.Name, en.RollNo, st.FirstName
            select new
            {
                en.EnrolmentId, st.StudentId, st.FirstName, st.LastName, st.AdmissionNo, en.AcademicYearId, en.SectionId,
                se.SchoolClassId, ClassName = cl.Name, SectionName = se.Name,
                Primary = db.StudentGuardians.Where(g => g.StudentId == st.StudentId && g.IsPrimary).Select(g => (long?)g.ContactId).FirstOrDefault(),
                Active = en.EnrolmentStatus == EnrolmentStatus.Active && st.StudentStatus == StudentStatus.Active,
            }).ToListAsync(ct);

        return Ok(rows.Select(r => new EnrolmentInfo
        {
            EnrolmentId = r.EnrolmentId,
            StudentId = r.StudentId,
            StudentName = StudentService.FullName(r.FirstName, r.LastName),
            AdmissionNo = r.AdmissionNo,
            AcademicYearId = r.AcademicYearId,
            SectionId = r.SectionId,
            SchoolClassId = r.SchoolClassId,
            ClassName = r.ClassName,
            SectionName = r.SectionName,
            PrimaryGuardianContactId = r.Primary,
            IsActive = r.Active,
        }));
    }

    private bool Apply(Guid customerId, Guid orgId) =>
        InternalTenant.Apply(_tenant, customerId, orgId) == InternalTenantOutcome.Applied;
}

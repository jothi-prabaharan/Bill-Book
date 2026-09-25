using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Internal;
using Shared.Kernel.School;
using Shared.Kernel.Tenancy;
using Sis.Api.Services;
using Sis.Entity.Enums;
using Sis.Entity.Models;
using Sis.Repository;

namespace Sis.Api.Controllers;

/// <summary>
/// What the other School services ask Sis (TK-62 onward). The branch comes in
/// the body, as on every internal route, and is set before a context is resolved.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/sis")]
public sealed class InternalSisController : ControllerBase
{
    private readonly TenantContext _tenant;
    private readonly IServiceProvider _services;

    public InternalSisController(TenantContext tenant, IServiceProvider services)
    {
        _tenant = tenant;
        _services = services;
    }

    [HttpPost("academic-check")]
    public async Task<IActionResult> AcademicCheck([FromBody] AcademicCheckRequest request, CancellationToken ct)
    {
        if (!Apply(request.CustomerId, request.OrgId))
        {
            return BadRequest(new SisMessage("A customer and an organization are required."));
        }

        var db = _services.GetRequiredService<SisDbContext>();
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
            return BadRequest(new SisMessage("A customer and an organization are required."));
        }

        if (!Enum.TryParse(request.Gender, out Gender gender) || !Enum.TryParse(request.GuardianRelationship, out GuardianRelationship relationship))
        {
            return UnprocessableEntity(new SisMessage("The gender or the guardian's relationship is not one this system knows."));
        }

        var students = _services.GetRequiredService<StudentService>();
        SisResult result = await students.CreateAsync(new SaveStudentRequest
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

        if (result.Outcome != SisOutcome.Ok)
        {
            return this.Answer(result);
        }

        string number = await _services.GetRequiredService<SisDbContext>().Students
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
            return BadRequest(new SisMessage("A customer and an organization are required."));
        }

        var db = _services.GetRequiredService<SisDbContext>();
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

    private bool Apply(Guid customerId, Guid orgId) =>
        InternalTenant.Apply(_tenant, customerId, orgId) == InternalTenantOutcome.Applied;
}
